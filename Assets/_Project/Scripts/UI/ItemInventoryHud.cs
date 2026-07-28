using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인벤토리 아이템/진화의 돌을 마우스로 집어 보드 위 유닛에 드래그해서 장착하는 프로토타입 UI(IMGUI).
/// 정식 uGUI 드래그앤드롭은 황해인 담당 — 이건 그 전까지 쓰는 임시 버전.
///
/// 장착 해제는 유닛을 클릭해 목록을 펼치고 버튼으로 처리한다.
/// 장착 시도는 쇼핑·전투 페이즈 모두에서 가능하다.
/// 전투 중 필드 유닛에 대한 실제 장착 거부는 ItemManager.EquipToUnit이 처리한다.
/// 장착 해제는 쇼핑 페이즈에서만 가능하다.
/// </summary>
public class ItemInventoryHud : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private LayerMask _raycastMask = ~0;

    private const float SLOT_W = 90f;
    private const float SLOT_H = 40f;
    private const float SLOT_GAP = 4f;
    private const int COLS = 4;
    private const float PANEL_X = 10f;

    // 좌측 상단은 SynergyHud가 사용하므로
    // 인벤토리는 화면 하단 왼쪽에서 위쪽으로 확장한다.
    private float _panelY;

    private ScriptableObject _dragging;
    private string _dragLabel;

    private PokemonUnit _inspectUnit;

    // 유닛 정보 패널 스크롤 위치
    private Vector2 _inspectScroll;

    private void Awake()
    {
        if (_camera == null)
            _camera = Camera.main;
    }

    private void OnGUI()
    {
        if (!GameManager.TryGet(out var gm) || gm.Item == null) return;

        bool isShopping =
            gm.Phase == null ||
            gm.Phase.CurrentPhase == GamePhase.Shopping;

        bool isBattlePhase =
            gm.Phase != null &&
            gm.Phase.CurrentPhase == GamePhase.Battle;

        // 쇼핑 또는 전투 중에만 인벤토리 장착 입력을 허용한다.
        bool canEquip = isShopping || isBattlePhase;

        // 장착 불가능한 페이즈로 변경되면
        // 진행 중이던 아이템 드래그만 취소한다.
        if (!canEquip)
        {
            _dragging = null;
            _dragLabel = null;
        }

        // ─────────────────────────────
        // 인벤토리 패널 위치 계산
        // ─────────────────────────────

        int total =
            gm.Item.Items.Count +
            gm.Item.Stones.Count;

        int rowCount = Mathf.Max(
            1,
            Mathf.CeilToInt(total / (float)COLS)
        );

        float panelHeight =
            24f +
            rowCount * (SLOT_H + SLOT_GAP) +
            6f;

        _panelY =
            Screen.height -
            150f -
            panelHeight;

        HandleInput(gm, canEquip);

        if (canEquip)
        {
            DrawInventory(gm);
            DrawDragGhost();
        }

        // 선택 유닛 정보는 모든 페이즈에서 표시한다.
        DrawInspectPanel(
            gm,
            isShopping,
            isBattlePhase
        );
    }

    private Rect SlotRect(int index)
    {
        float x =
            PANEL_X +
            index % COLS * (SLOT_W + SLOT_GAP);

        float y =
            _panelY +
            24f +
            index / COLS * (SLOT_H + SLOT_GAP);

        return new Rect(
            x,
            y,
            SLOT_W,
            SLOT_H
        );
    }

    private void DrawInventory(GameManager gm)
    {
        ItemManager itemManager = gm.Item;

        int total =
            itemManager.Items.Count +
            itemManager.Stones.Count;

        int rows = Mathf.Max(
            1,
            Mathf.CeilToInt(total / (float)COLS)
        );

        float width =
            COLS * (SLOT_W + SLOT_GAP) +
            6f;

        float height =
            24f +
            rows * (SLOT_H + SLOT_GAP) +
            6f;

        GUI.Box(
            new Rect(
                PANEL_X - 5f,
                _panelY,
                width,
                height
            ),
            "인벤토리 (드래그해서 유닛에 장착)"
        );

        int index = 0;

        foreach (ItemData item in itemManager.Items)
        {
            string label =
                item != null
                    ? item.itemName
                    : "?";

            GUI.Box(
                SlotRect(index),
                label
            );

            index++;
        }

        foreach (var stone in itemManager.Stones)
        {
            string label =
                stone != null
                    ? $"[돌]{stone.stoneName}"
                    : "?";

            GUI.Box(
                SlotRect(index),
                label
            );

            index++;
        }
    }

    private void HandleInput(
        GameManager gm,
        bool canEquip)
    {
        Event currentEvent = Event.current;
        ItemManager itemManager = gm.Item;

        if (currentEvent.type == EventType.MouseDown &&
            _dragging == null)
        {
            // ─────────────────────────────
            // 인벤토리 아이템 선택
            // ─────────────────────────────

            if (canEquip)
            {
                int index = 0;

                foreach (ItemData item in itemManager.Items)
                {
                    if (SlotRect(index).Contains(
                            currentEvent.mousePosition))
                    {
                        _dragging = item;
                        _dragLabel =
                            item != null
                                ? item.itemName
                                : "?";

                        currentEvent.Use();
                        return;
                    }

                    index++;
                }

                foreach (EvolutionStoneData stone in itemManager.Stones)
                {
                    if (SlotRect(index).Contains(
                            currentEvent.mousePosition))
                    {
                        _dragging = stone;
                        _dragLabel =
                            stone != null
                                ? stone.stoneName
                                : "?";

                        currentEvent.Use();
                        return;
                    }

                    index++;
                }
            }

            // ─────────────────────────────
            // 유닛 선택
            // ─────────────────────────────

            PokemonUnit clickedUnit =
                RaycastUnit(currentEvent.mousePosition);

            if (clickedUnit != null)
            {
                // 다른 유닛을 선택하면 스크롤을 맨 위로 초기화한다.
                if (_inspectUnit != clickedUnit)
                    _inspectScroll = Vector2.zero;

                _inspectUnit = clickedUnit;

                currentEvent.Use();
            }
        }
        else if (
            canEquip &&
            currentEvent.type == EventType.MouseUp &&
            _dragging != null)
        {
            // ─────────────────────────────
            // 아이템 장착 시도
            // ─────────────────────────────

            PokemonUnit targetUnit =
                RaycastUnit(currentEvent.mousePosition);

            if (targetUnit != null)
            {
                bool success =
                    itemManager.EquipToUnit(
                        _dragging,
                        targetUnit
                    );

                Debug.Log(
                    $"[ItemHud] 장착 " +
                    $"{(success ? "성공" : "실패")}: " +
                    $"{_dragLabel} → " +
                    $"{targetUnit.data?.pokemonName}"
                );
            }

            _dragging = null;
            _dragLabel = null;

            currentEvent.Use();
        }
    }

    private void DrawDragGhost()
    {
        if (_dragging == null)
            return;

        Vector2 mousePosition =
            Event.current.mousePosition;

        GUI.Box(
            new Rect(
                mousePosition.x - 40f,
                mousePosition.y - 15f,
                80f,
                30f
            ),
            _dragLabel
        );
    }

    private void DrawInspectPanel(
        GameManager gm,
        bool isShopping,
        bool isBattlePhase)
    {
        if (_inspectUnit == null)
            return;

        // 전투 중 유닛이 제거되었거나 데이터가 사라진 경우 안전 처리
        if (_inspectUnit.data == null)
        {
            _inspectUnit = null;
            _inspectScroll = Vector2.zero;
            return;
        }

        PokemonUnit unit = _inspectUnit;
        PokemonData data = unit.data;

        const float panelWidth = 350f;

        // 화면 높이를 넘지 않도록 제한한다.
        float panelHeight = Mathf.Clamp(
            Screen.height - 20f,
            300f,
            900f
        );

        GUILayout.BeginArea(
            new Rect(
                Screen.width - panelWidth - 10f,
                10f,
                panelWidth,
                panelHeight
            ),
            GUI.skin.box
        );

        // 패널 내부 콘텐츠가 높이를 초과하면 스크롤한다.
        _inspectScroll = GUILayout.BeginScrollView(
            _inspectScroll,
            false,
            true
        );

        // ─────────────────────────────
        // 기본 정보
        // ─────────────────────────────

        GUILayout.Label(
            $"[{data.pokemonName}] 유닛 정보"
        );

        GUILayout.Space(4f);

        GUILayout.Label(
            $"영문명: {GetSafeText(data.pokemonNameEn)}"
        );

        GUILayout.Label(
            $"포켓몬 ID: {data.id}"
        );

        GUILayout.Label(
            $"성급: {unit.starLevel}성"
        );

        GUILayout.Label(
            $"코스트: {data.cost}"
        );

        GUILayout.Label(
            $"위치: {(unit.isOnBoard ? "필드" : "벤치")}"
        );

        GUILayout.Label(
            $"역할: {unit.Role}"
        );

        GUILayout.Space(8f);

        // ─────────────────────────────
        // 능력치
        // ─────────────────────────────

        GUILayout.Label("── 능력치 ──");

        GUILayout.Label(
            $"기본 체력: {data.hp:0}"
        );

        GUILayout.Label(
            $"기본 공격력: {data.attack:0}"
        );

        GUILayout.Label(
            $"기본 방어력: {data.defense:0}"
        );

        GUILayout.Label(
            $"기본 공격속도: {data.attackSpeed:0.00}"
        );

        GUILayout.Label(
            $"공격 사거리: {data.range}"
        );

        GUILayout.Label(
            $"스킬 위력: {data.spellPower:0}"
        );

        GUILayout.Label(
            $"기본 마나 비용: {data.manaCost}"
        );

        GUILayout.Label(
            $"적용 마나 비용: {unit.EffectiveManaCost}"
        );

        GUILayout.Space(8f);

        // ─────────────────────────────
        // 스킬 / 증강
        // ─────────────────────────────

        GUILayout.Label("── 스킬·증강 ──");

        var effectiveSkill = unit.EffectiveSkill;

        GUILayout.Label(
            $"기본 스킬 ID: {GetSafeText(data.skillId)}"
        );

        GUILayout.Label(
            $"주입 스킬 보유: {GetYesNo(unit.HasGrantedSkill)}"
        );

        if (effectiveSkill != null &&
            effectiveSkill.HasSkill)
        {
            GUILayout.Label(
                $"적용 스킬 ID: " +
                $"{GetSafeText(effectiveSkill.skillId)}"
            );

            GUILayout.Label(
                $"효과 타입: {effectiveSkill.effectType}"
            );

            GUILayout.Label(
                $"효과 범위: {effectiveSkill.areaRadius}"
            );
        }
        else
        {
            GUILayout.Label(
                "적용 스킬: 없음"
            );
        }

        GUILayout.Space(8f);

        // ─────────────────────────────
        // 진화 상태
        // ─────────────────────────────

        GUILayout.Label("── 진화 상태 ──");

        GUILayout.Label(
            $"일반 진화 잠금: " +
            $"{(unit.evolutionLocked ? "잠금" : "해제")}"
        );

        GUILayout.Label(
            $"통신진화체: " +
            $"{GetYesNo(unit.isTradeEvolved)}"
        );

        if (unit.equippedStone != null)
        {
            GUILayout.Label(
                $"장착 진화의 돌: " +
                $"{unit.equippedStone.stoneName}"
            );
        }
        else
        {
            GUILayout.Label(
                "장착 진화의 돌: 없음"
            );
        }

        if (unit.preStoneData != null)
        {
            GUILayout.Label(
                $"돌 진화 이전 종: " +
                $"{unit.preStoneData.pokemonName}"
            );
        }
        else
        {
            GUILayout.Label(
                "돌 진화 이전 종: 없음"
            );
        }

        GUILayout.Space(8f);

        // ─────────────────────────────
        // 장착 아이템
        // ─────────────────────────────

        GUILayout.Label("── 장착 아이템 ──");

        if (unit.items == null ||
            unit.items.Count == 0)
        {
            GUILayout.Label(
                "일반 아이템 없음"
            );
        }
        else
        {
            // 버튼 클릭 중 컬렉션이 변경될 수 있어 복사본을 순회한다.
            List<ItemData> equippedItems =
                new List<ItemData>(unit.items);

            for (int i = 0;
                 i < equippedItems.Count;
                 i++)
            {
                ItemData item = equippedItems[i];

                if (item == null)
                {
                    GUILayout.Label(
                        $"슬롯 {i + 1}: 비어 있음"
                    );

                    continue;
                }

                GUILayout.Label(
                    $"슬롯 {i + 1}: {item.itemName}"
                );

                if (isShopping)
                {
                    if (GUILayout.Button(
                            $"해제: {item.itemName}"))
                    {
                        gm.Item.UnequipFromUnit(
                            item,
                            unit
                        );
                    }
                }
            }
        }

        if (unit.equippedStone != null)
        {
            string stoneName =
                unit.equippedStone.stoneName;

            GUILayout.Label(
                $"진화의 돌: {stoneName}"
            );

            if (isShopping)
            {
                if (GUILayout.Button(
                        $"해제: [돌]{stoneName}"))
                {
                    gm.Item.UnequipFromUnit(
                        unit.equippedStone,
                        unit
                    );
                }
            }
        }
        else
        {
            GUILayout.Label(
                "진화의 돌: 없음"
            );
        }

        if (isBattlePhase)
        {
            GUILayout.Space(4f);

            GUILayout.Label(
                "전투 중에는 장비를 해제할 수 없습니다."
            );
        }

        GUILayout.Space(8f);

        // ─────────────────────────────
        // QA 상태
        // ─────────────────────────────

        GUILayout.Label("── QA 상태 ──");

        string phaseText =
            gm.Phase != null
                ? gm.Phase.CurrentPhase.ToString()
                : "Phase 없음";

        GUILayout.Label(
            $"현재 페이즈: {phaseText}"
        );

        GUILayout.Label(
            $"장비 해제 가능: " +
            $"{GetYesNo(isShopping)}"
        );

        GUILayout.Label(
            $"전투 중 필드 유닛: " +
            $"{GetYesNo(isBattlePhase && unit.isOnBoard)}"
        );

        GUILayout.Label(
            $"영웅증강 진화 잠금: " +
            $"{(unit.evolutionLocked ? "적용" : "미적용")}"
        );

        GUILayout.Label(
            $"통신진화 배율 대상: " +
            $"{GetYesNo(unit.isTradeEvolved)}"
        );

        bool isSpecialEvolved =
            unit.isTradeEvolved ||
            unit.equippedStone != null;

        GUILayout.Label(
            $"특수진화 상태: " +
            $"{GetYesNo(isSpecialEvolved)}"
        );

        GUILayout.Space(10f);

        if (GUILayout.Button(
                "닫기",
                GUILayout.Height(30f)))
        {
            _inspectUnit = null;
            _inspectScroll = Vector2.zero;
        }

        GUILayout.Space(4f);

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    /// <summary>
    /// 포인터 아래 모든 Collider를 검사하여
    /// 가장 가까운 PokemonUnit을 찾는다.
    /// 필드 타일 Collider가 먼저 맞아도 유닛 선택이 가능하다.
    /// </summary>
    private PokemonUnit RaycastUnit(
        Vector2 guiMousePosition)
    {
        if (_camera == null)
            return null;

        Vector3 screenPosition =
            new Vector3(
                guiMousePosition.x,
                Screen.height - guiMousePosition.y,
                0f
            );

        Ray ray =
            _camera.ScreenPointToRay(
                screenPosition
            );

        RaycastHit[] hits =
            Physics.RaycastAll(
                ray,
                1000f,
                _raycastMask
            );

        System.Array.Sort(
            hits,
            (a, b) =>
                a.distance.CompareTo(b.distance)
        );

        foreach (RaycastHit hit in hits)
        {
            PokemonUnit unit =
                hit.collider
                    .GetComponentInParent<PokemonUnit>();

            if (unit != null)
                return unit;

            BattleManager battleManager =
                GameManager.Instance != null
                    ? GameManager.Instance
                        .GetComponent<BattleManager>()
                    : null;

            if (battleManager == null)
                continue;

            PokemonUnit sourceUnit =
                battleManager.GetSourceUnitFromVisual(
                    hit.collider.gameObject
                );

            if (sourceUnit != null)
                return sourceUnit;
        }

        return null;
    }

    private static string GetYesNo(bool value)
    {
        return value
            ? "예"
            : "아니오";
    }

    private static string GetSafeText(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "-"
            : value;
    }
}