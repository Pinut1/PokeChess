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

    [Header("드롭 판정")]
    [Tooltip("비우면 Camera.main을 쓴다.")]
    [SerializeField] private Camera _camera;
    [Tooltip("유닛 Collider가 속한 레이어. 보드 외 오브젝트가 잡히면 좁혀준다.")]
    [SerializeField] private LayerMask _raycastMask = ~0;

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null) continue;

            _slots[i].Initialize(_dragLayer);
            _slots[i].Dropped += HandleDropped;
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _slots.Length; i++)
            if (_slots[i] != null) _slots[i].Dropped -= HandleDropped;
    }

    private void OnEnable()
    {
        GameEvents.OnInventoryChanged += Refresh;
        Refresh();
    }

    private void OnDisable() => GameEvents.OnInventoryChanged -= Refresh;

    /// <summary>보유 목록을 앞칸부터 채우고 나머지는 비운다. 아이템 → 진화의 돌 순서.</summary>
    private void Refresh()
    {
        if (!GameManager.TryGet(out var gm) || gm.Item == null) return;

        var items = gm.Item.Items;
        var stones = gm.Item.Stones;
        int cursor = 0;

        for (int i = 0; i < items.Count && cursor < _slots.Length; i++, cursor++)
            _slots[cursor]?.Bind(items[i]);

        for (int i = 0; i < stones.Count && cursor < _slots.Length; i++, cursor++)
            _slots[cursor]?.Bind(stones[i]);

        for (; cursor < _slots.Length; cursor++)
            _slots[cursor]?.Clear();
    }

    /// <summary>
    /// 드롭 지점에서 유닛을 찾아 장착을 시도한다.
    /// 성공하면 ItemManager가 GameEvents.InventoryChanged를 발행해 Refresh가 돌고 칸이 비워진다.
    /// 실패(유닛 없음·슬롯 만석 등)하면 아무 일도 일어나지 않고 아이콘은 이미 제자리로 돌아가 있다.
    /// </summary>
    private void HandleDropped(ItemSlotUI slot, PointerEventData eventData)
    {
        if (slot == null || slot.IsEmpty) return;
        if (!GameManager.TryGet(out var gm) || gm.Item == null) return;

        // 전투 중에는 유닛이 비활성/미러 좌표라 장착이 의미가 없다(UnitDragController와 동일 제약).
        if (gm.Phase != null && gm.Phase.CurrentPhase != GamePhase.Shopping) return;

        var unit = RaycastUnit(eventData.position);
        if (unit == null) return;

        gm.Item.EquipToUnit(slot.CurrentData, unit);
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
