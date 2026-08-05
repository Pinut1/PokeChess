using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 인벤토리 20칸을 ItemManager 보유 목록과 동기화하고, 드롭 지점의 유닛에 장착을 위임한다.
/// 칸은 고정 개수(씬의 itemBox_Panel 20개 = ItemManager.MAX_INVENTORY_SIZE)라 생성/파괴가 없다.
///
/// 기존 IMGUI 프로토타입(ItemInventoryHud)의 정식 uGUI 버전이다.
/// 유닛 탐지 방식(카메라 레이캐스트 → GetComponentInParent&lt;PokemonUnit&gt;)과
/// 쇼핑 페이즈 제한은 프로토타입·UnitDragController와 동일하게 맞췄다.
/// 장착 해제는 이 컴포넌트의 범위가 아니다(아이템 제거기로 별도 담당).
/// </summary>
public class ItemInventoryUI : MonoBehaviour
{
    [Tooltip("씬의 itemBox_Panel 20칸. 순서는 표시 순서일 뿐 슬롯 인덱스 의미는 없다.")]
    [SerializeField] private ItemSlotUI[] _slots;

    [Tooltip("드래그 중 아이콘을 잠시 옮겨 담을 부모. 보통 최상위 Canvas — 다른 UI에 가려지지 않게 한다.")]
    [SerializeField] private RectTransform _dragLayer;

    [Header("도구 아이콘")]
    // 제거기/재조합기는 ItemManager가 ConsumableData 리스트가 아니라 bool/int로 들고 있다
    // (_hasRemover / ReforgerCount). 그래서 칸에 그릴 아이콘만 에셋에서 직접 받는다.
    [Tooltip("제거기 표시용 ConsumableData(Golden Remover_Consumable). 아이콘만 사용한다.")]
    [SerializeField] private ConsumableData _removerData;
    [Tooltip("재조합기 표시용 ConsumableData(Reforger_Consumable). 아이콘만 사용한다.")]
    [SerializeField] private ConsumableData _reforgerData;

    [Header("설명창")]
    [Tooltip("칸에 커서를 올리면 띄울 아이템 설명창 컨트롤러. 비우면 설명창이 뜨지 않는다.")]
    [SerializeField] private ItemTooltipController _tooltip;

    [Header("드롭 판정")]
    [Tooltip("비우면 Camera.main을 쓴다.")]
    [SerializeField] private Camera _camera;
    [Tooltip("유닛 Collider가 속한 레이어. 보드 외 오브젝트가 잡히면 좁혀준다.")]
    [SerializeField] private LayerMask _raycastMask = ~0;

    [Header("재조합기 인벤토리 직접 사용")]
    [Tooltip("재조합기를 다른 칸의 진화의 돌 위로 끌 때 뜨는 공용 홀드 게이지. 씬에 하나만 두고 재사용한다.")]
    [SerializeField] private ReforgeHoldGaugeUI _reforgeGauge;
    [Tooltip("홀드가 완료되기까지 걸리는 시간(초). 롤토체스 아이템 조합 홀드와 동일한 감각을 노린 기본값.")]
    [SerializeField] private float _reforgeHoldDuration = 2f;

    // ─────────────────────────────
    // 재조합기 인벤토리 직접 사용 — 홀드 상태
    // ─────────────────────────────
    // 대상 슬롯이 바뀌면 반드시 처음부터 다시 시작해야 하므로, 진행 중인 홀드는 슬롯 참조 하나로만 추적한다
    // (같은 슬롯을 유지할 때만 이어가고, 그 외 모든 경로는 CancelReforgeHold로 합류시킨다).
    private ItemSlotUI _reforgeDragSource;   // 지금 드래그 중인 원본이 재조합기 칸이면 그 칸, 아니면 null
    private ItemSlotUI _reforgeTargetSlot;   // 홀드 중인 대상 진화의 돌 칸
    private EvolutionStoneData _reforgeHoldStone; // 홀드 시작 당시 대상 칸의 돌(드롭 직전 재검증용)
    private Coroutine _reforgeHoldCoroutine;
    private bool _reforgeHoldComplete;

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null) continue;

            _slots[i].Initialize(_dragLayer);
            _slots[i].Dropped += HandleDropped;
            _slots[i].Hovered += HandleHovered;
            _slots[i].DragBegan += HandleDragBegan;
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null) continue;

            _slots[i].Dropped -= HandleDropped;
            _slots[i].Hovered -= HandleHovered;
            _slots[i].DragBegan -= HandleDragBegan;
        }
    }

    /// <summary>칸에 커서가 들고 날 때 설명창을 여닫고, 재조합기 홀드 시작/취소를 판정한다.</summary>
    private void HandleHovered(ItemSlotUI slot, bool entered)
    {
        if (entered) HandleReforgeHoverEnter(slot);
        else         HandleReforgeHoverExit(slot);

        if (_tooltip == null) return;

        if (entered) _tooltip.Show(slot, slot.CurrentData);
        else         _tooltip.Hide(slot);
    }

    /// <summary>드래그가 시작되면 원본이 재조합기인지만 기억해둔다. 이전 홀드는 새 드래그 시작 시점에 정리한다.</summary>
    private void HandleDragBegan(ItemSlotUI slot)
    {
        CancelReforgeHold();
        _reforgeDragSource = (slot != null && slot.CurrentData == _reforgerData) ? slot : null;
    }

    private void OnEnable()
    {
        GameEvents.OnInventoryChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        GameEvents.OnInventoryChanged -= Refresh;

        // UI가 꺼지는 동안 홀드 코루틴이 남아 있으면 안 된다(요구사항: UI 비활성화 시 즉시 초기화).
        _reforgeDragSource = null;
        CancelReforgeHold();

        // 인벤토리가 닫히는데 설명창만 남는 일이 없도록.
        // HideAll이 아니라 칸별 Hide인 이유 — 이 컨트롤러는 아이템 상점과 공유하므로
        // HideAll은 상점이 띄운 툴팁까지 꺼버린다. Hide(owner)는 자기 것이 아니면 무시된다.
        if (_tooltip == null) return;

        for (int i = 0; i < _slots.Length; i++)
            if (_slots[i] != null) _tooltip.Hide(_slots[i]);
    }

    /// <summary>
    /// 보유 목록을 앞칸부터 채우고 나머지는 비운다. 아이템 → 진화의 돌 → 도구(제거기·재조합기) 순서.
    /// 도구 칸 수는 ItemManager.InventoryCount의 계산과 같다 — 재조합기는 수량과 무관하게 한 칸.
    /// </summary>
    private void Refresh()
    {
        if (!GameManager.TryGet(out var gm) || gm.Item == null) return;

        var item = gm.Item;
        var items = item.Items;
        var stones = item.Stones;
        int cursor = 0;

        for (int i = 0; i < items.Count && cursor < _slots.Length; i++, cursor++)
            _slots[cursor]?.Bind(items[i]);

        for (int i = 0; i < stones.Count && cursor < _slots.Length; i++, cursor++)
            _slots[cursor]?.Bind(stones[i]);

        if (item.HasRemover && _removerData != null && cursor < _slots.Length)
            _slots[cursor++]?.Bind(_removerData);

        // 재조합기는 중첩하지 않고 개수만큼 낱개로 놓인다(InventoryCount도 같은 기준).
        if (_reforgerData != null)
            for (int i = 0; i < item.ReforgerCount && cursor < _slots.Length; i++, cursor++)
                _slots[cursor]?.Bind(_reforgerData);

        for (; cursor < _slots.Length; cursor++)
            _slots[cursor]?.Clear();

        // 인벤토리가 바뀌는 동안(Refresh는 칸을 통째로 다시 바인딩한다) 홀드 중인 대상 칸의 내용물이
        // 더 이상 홀드 시작 당시의 그 돌이 아니게 됐다면 즉시 취소한다("대상 아이템이 변경됨" 규칙).
        if (_reforgeTargetSlot != null && _reforgeTargetSlot.CurrentData != _reforgeHoldStone)
            CancelReforgeHold();

        // 같은 이유로, 드래그 원본이 재조합기가 아니게 됐다면(이론상 드문 경우) 추적을 놓는다.
        if (_reforgeDragSource != null && _reforgeDragSource.CurrentData != _reforgerData)
            _reforgeDragSource = null;
    }

    /// <summary>
    /// 드롭 처리 진입점. 인벤토리 슬롯 대상 재조합기 사용을 3D 유닛 레이캐스트보다 먼저 판정한다 —
    /// 칸 뒤에 우연히 유닛이 겹쳐 보이더라도 슬롯 재조합이 우선해야 한다.
    /// </summary>
    private void HandleDropped(ItemSlotUI slot, PointerEventData eventData)
    {
        if (slot == null || slot.IsEmpty) { _reforgeDragSource = null; CancelReforgeHold(); return; }
        if (!GameManager.TryGet(out var gm) || gm.Item == null) { _reforgeDragSource = null; CancelReforgeHold(); return; }

        // 전투 중에는 유닛이 비활성/미러 좌표라 장착이 의미가 없다(UnitDragController와 동일 제약).
        if (gm.Phase != null && gm.Phase.CurrentPhase != GamePhase.Shopping)
        {
            _reforgeDragSource = null;
            CancelReforgeHold();
            return;
        }

        // 1) 인벤토리 슬롯 대상 재조합 — 조건을 만족해 "시도"했다면 성공/실패와 무관하게 여기서 끝낸다.
        if (TryResolveInventoryReforge(slot, gm))
        {
            _reforgeDragSource = null;
            CancelReforgeHold();
            return;
        }

        _reforgeDragSource = null;
        CancelReforgeHold();

        // 2) 기존 경로 — 3D 월드 유닛 대상 처리.
        var unit = RaycastUnit(eventData.position);
        if (unit == null) return;

        // 도구는 장착이 아니라 "사용"이라 전용 경로로 보낸다(IMGUI 프로토타입과 동일 규칙).
        // 어느 도구인지는 인스펙터에 꽂아둔 표시용 데이터와 같은 에셋인지로 가른다.
        if (slot.CurrentData == _removerData)  { gm.Item.UseRemover(unit);  return; }
        if (slot.CurrentData == _reforgerData) { gm.Item.UseReforger(unit); return; }

        gm.Item.EquipToUnit(slot.CurrentData, unit);
    }

    /// <summary>
    /// 드래그 소스가 재조합기이고, 홀드가 완료된 진화의 돌 칸 위에서 놓였는지 드롭 직전에 다시 검증한다.
    /// 조건을 만족해 재조합을 "시도"했다면(ItemManager 내부 성공/실패와 무관) true를 반환한다 —
    /// 이 경우 호출부는 유닛 레이캐스트 경로로 넘어가지 않는다.
    /// </summary>
    private bool TryResolveInventoryReforge(ItemSlotUI dragSlot, GameManager gm)
    {
        if (dragSlot != _reforgeDragSource) return false;   // 드래그 소스가 재조합기 칸이 아니었음
        if (dragSlot.CurrentData != _reforgerData) return false;

        ItemSlotUI targetSlot = _reforgeTargetSlot;
        if (targetSlot == null || !_reforgeHoldComplete) return false; // 홀드 미완료 — 확정 금지
        if (targetSlot == dragSlot) return false;                      // 자기 자신 방지(원칙적으로 발생 안 함)

        if (targetSlot.CurrentData is not EvolutionStoneData targetStone) return false;
        if (targetStone != _reforgeHoldStone) return false; // 홀드 시작 당시와 다른 돌 — 재검증 실패

        gm.Item.ReforgeStoneInInventory(targetStone);
        return true;
    }

    // ─────────────────────────────
    // 재조합기 인벤토리 직접 사용 — 홀드 게이지 진행
    // ─────────────────────────────

    /// <summary>드래그 중인 게 재조합기고 대상이 다른 칸의 진화의 돌이면 홀드를 새로 시작한다.</summary>
    private void HandleReforgeHoverEnter(ItemSlotUI slot)
    {
        if (_reforgeDragSource == null || slot == null || slot == _reforgeDragSource) return;
        if (slot.CurrentData is not EvolutionStoneData stone) return;

        StartReforgeHold(slot, stone);
    }

    /// <summary>지금 홀드 중인 대상 칸에서 커서가 나가면 완료 여부와 무관하게 즉시 취소한다.</summary>
    private void HandleReforgeHoverExit(ItemSlotUI slot)
    {
        if (slot == null || slot != _reforgeTargetSlot) return;

        CancelReforgeHold();
    }

    /// <summary>
    /// 새 대상 칸에서 홀드를 처음부터 시작한다. 다른 칸으로 옮겨갔을 때도 이 메서드가 호출되므로,
    /// 맨 앞의 CancelReforgeHold가 이전 진행도/코루틴을 반드시 먼저 정리한다("처음부터 다시 시작" 규칙).
    /// </summary>
    private void StartReforgeHold(ItemSlotUI slot, EvolutionStoneData stone)
    {
        CancelReforgeHold();

        _reforgeTargetSlot = slot;
        _reforgeHoldStone = stone;
        _reforgeHoldComplete = false;

        if (_reforgeGauge != null)
            _reforgeGauge.Show(slot.transform as RectTransform);

        _reforgeHoldCoroutine = StartCoroutine(ReforgeHoldRoutine());
    }

    /// <summary>2초(설정값) 동안 게이지를 0→1로 채운 뒤 완료 플래그만 세운다 — 자동 변환은 하지 않는다.</summary>
    private System.Collections.IEnumerator ReforgeHoldRoutine()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, _reforgeHoldDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _reforgeGauge?.SetProgress(elapsed / duration);
            yield return null;
        }

        _reforgeGauge?.SetProgress(1f);
        _reforgeHoldComplete = true;
        _reforgeHoldCoroutine = null;
    }

    /// <summary>
    /// 진행 중인 홀드를 중단하고 상태를 완전히 초기화한다.
    /// 이탈/대상 변경/드래그 취소·종료/UI 비활성화/재조합 성공·실패 처리 완료 등 모든 종료 경로가 이걸 거친다.
    /// </summary>
    private void CancelReforgeHold()
    {
        if (_reforgeHoldCoroutine != null)
        {
            StopCoroutine(_reforgeHoldCoroutine);
            _reforgeHoldCoroutine = null;
        }

        _reforgeGauge?.Hide();

        _reforgeTargetSlot = null;
        _reforgeHoldStone = null;
        _reforgeHoldComplete = false;
    }

    /// <summary>
    /// PointerEventData.position은 이미 스크린 좌표(좌하단 원점)라 IMGUI처럼 y를 뒤집지 않는다.
    /// </summary>
    private PokemonUnit RaycastUnit(Vector2 screenPos)
    {
        if (_camera == null) return null;

        Ray ray = _camera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, _raycastMask))
            return hit.collider.GetComponentInParent<PokemonUnit>();

        return null;
    }
}
