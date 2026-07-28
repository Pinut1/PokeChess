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

    private const float SLOT_W = 82f;
    private const float SLOT_H = 36f;
    private const float SLOT_GAP = 3f;
    private const int COLS = 3;
    private const float PANEL_X = 10f;

    private const float INVENTORY_PANEL_W = 280f;
    private const float INVENTORY_PANEL_H = 170f;
    private const float INVENTORY_BOTTOM_MARGIN = 40f;

    // 좌측 상단은 SynergyHud가 사용하므로
    // 인벤토리는 화면 하단 왼쪽에서 위쪽으로 확장한다.
    private float _panelY;

    private enum DragToolType
    {
        None,
        Remover,
        Reforger
    }

    private ScriptableObject _dragging;
    private DragToolType _draggingTool;
    private string _dragLabel;

    private bool IsDragging =>
        _dragging != null ||
        _draggingTool != DragToolType.None;

    private PokemonUnit _inspectUnit;

    // 유닛 정보 패널 스크롤 위치
    private Vector2 _inspectScroll;

    // 인벤토리 패널 스크롤 위치
    private Vector2 _inventoryScroll;

    private void Awake()
    {
        if (_camera == null)
            _camera = Camera.main;
    }

    private void OnGUI()
    {
        if (!GameManager.TryGet(out var gm) || gm.Item == null) return;

        bool isLobby =
            gm.Phase != null &&
            gm.Phase.CurrentPhase == GamePhase.Lobby;

        bool isShopping =
            gm.Phase == null ||
            gm.Phase.CurrentPhase == GamePhase.Shopping;

        bool isBattlePhase =
            gm.Phase != null &&
            gm.Phase.CurrentPhase == GamePhase.Battle;

        // QA 편의를 위해 초기 Lobby에서도 아이템 장착 입력을 허용한다.
        bool canEquip =
            isLobby ||
            isShopping ||
            isBattlePhase;

        // 장착 불가능한 페이즈로 변경되면
        // 진행 중이던 아이템 드래그만 취소한다.
        if (!canEquip)
        {
            ClearDragging();
        }

        // ─────────────────────────────
        // 인벤토리 패널 위치 계산
        // ─────────────────────────────

        // 인벤토리 패널은 화면 하단에 고정하고,
        // 보유 항목이 많아지면 패널 내부에서 스크롤한다.
        _panelY =
            Screen.height -
            INVENTORY_BOTTOM_MARGIN -
            INVENTORY_PANEL_H;

        HandleInput(gm, canEquip);

        // 인벤토리 목록은 모든 페이즈에서 표시한다.
        // 실제 장착 입력만 canEquip 조건으로 제한한다.
        DrawInventory(gm);

        if (canEquip)
        {
            DrawDragGhost();
        }

        // 선택 유닛 정보는 모든 페이즈에서 표시한다.
        DrawInspectPanel(
            gm,
            isShopping,
            isBattlePhase
        );
    }

    private void ClearDragging()
    {
        _dragging = null;
        _draggingTool = DragToolType.None;
        _dragLabel = null;
    }

    /// <summary>
    /// 인벤토리 스크롤 콘텐츠 내부의 슬롯 위치.
    /// 화면 절대 좌표가 아니라 스크롤 영역 기준 로컬 좌표다.
    /// </summary>
    private Rect InventorySlotRect(int index)
    {
        float x =
            index % COLS *
            (SLOT_W + SLOT_GAP);

        float y =
            index / COLS *
            (SLOT_H + SLOT_GAP);

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

        int toolCount = 0;

        if (itemManager.HasRemover)
            toolCount++;

        if (itemManager.ReforgerCount > 0)
            toolCount++;

        int total =
            toolCount +
            itemManager.Items.Count +
            itemManager.Stones.Count;

        int rows = Mathf.Max(
            1,
            Mathf.CeilToInt(total / (float)COLS)
        );

        float contentWidth =
            COLS * (SLOT_W + SLOT_GAP);

        float contentHeight =
            rows * (SLOT_H + SLOT_GAP);

        Rect panelRect =
            new Rect(
                PANEL_X,
                _panelY,
                INVENTORY_PANEL_W,
                INVENTORY_PANEL_H
            );

        GUI.Box(
            panelRect,
            $"인벤토리 {itemManager.InventoryCount}/20"
        );

        Rect scrollViewRect =
            new Rect(
                panelRect.x + 6f,
                panelRect.y + 24f,
                panelRect.width - 12f,
                panelRect.height - 30f
            );

        Rect contentRect =
            new Rect(
                0f,
                0f,
                Mathf.Max(
                    contentWidth,
                    scrollViewRect.width - 16f
                ),
                Mathf.Max(
                    contentHeight,
                    scrollViewRect.height
                )
            );

        _inventoryScroll =
            GUI.BeginScrollView(
                scrollViewRect,
                _inventoryScroll,
                contentRect,
                false,
                true
            );

        int index = 0;

        // ─────────────────────────────
        // 제거기
        // ─────────────────────────────

        if (itemManager.HasRemover)
        {
            GUI.Box(
                InventorySlotRect(index),
                "[도구] 제거기"
            );

            index++;
        }

        // ─────────────────────────────
        // 재조합기
        // ─────────────────────────────

        if (itemManager.ReforgerCount > 0)
        {
            GUI.Box(
                InventorySlotRect(index),
                $"[도구] 재조합기\n×{itemManager.ReforgerCount}"
            );

            index++;
        }

        // ─────────────────────────────
        // 일반 장비
        // ─────────────────────────────

        foreach (ItemData item in itemManager.Items)
        {
            string label =
                item != null
                    ? item.itemName
                    : "?";

            GUI.Box(
                InventorySlotRect(index),
                label
            );

            index++;
        }

        // ─────────────────────────────
        // 진화의 돌
        // ─────────────────────────────

        foreach (EvolutionStoneData stone in itemManager.Stones)
        {
            string label =
                stone != null
                    ? $"[돌]{stone.stoneName}"
                    : "?";

            GUI.Box(
                InventorySlotRect(index),
                label
            );

            index++;
        }

        GUI.EndScrollView();
    }

    private void HandleInput(
    GameManager gm,
    bool canEquip)
    {
        Event currentEvent = Event.current;
        ItemManager itemManager = gm.Item;

        Rect inventoryScrollRect =
            new Rect(
                PANEL_X + 6f,
                _panelY + 24f,
                INVENTORY_PANEL_W - 12f,
                INVENTORY_PANEL_H - 30f
            );

        Vector2 inventoryMousePosition =
            currentEvent.mousePosition -
            inventoryScrollRect.position +
            _inventoryScroll;

        if (currentEvent.type == EventType.MouseDown &&
            !IsDragging)
        {
            // ─────────────────────────────
            // 인벤토리 슬롯 선택
            // ─────────────────────────────

            if (canEquip &&
                inventoryScrollRect.Contains(
                    currentEvent.mousePosition))
            {
                int index = 0;

                // 제거기
                if (itemManager.HasRemover)
                {
                    if (InventorySlotRect(index).Contains(
                            inventoryMousePosition))
                    {
                        _dragging = null;
                        _draggingTool = DragToolType.Remover;
                        _dragLabel = "제거기";

                        currentEvent.Use();
                        return;
                    }

                    index++;
                }

                // 재조합기
                if (itemManager.ReforgerCount > 0)
                {
                    if (InventorySlotRect(index).Contains(
                            inventoryMousePosition))
                    {
                        _dragging = null;
                        _draggingTool = DragToolType.Reforger;
                        _dragLabel =
                            $"재조합기 ×{itemManager.ReforgerCount}";

                        currentEvent.Use();
                        return;
                    }

                    index++;
                }

                // 일반 장비
                foreach (ItemData item in itemManager.Items)
                {
                    if (InventorySlotRect(index).Contains(
                            inventoryMousePosition))
                    {
                        _dragging = item;
                        _draggingTool = DragToolType.None;

                        _dragLabel =
                            item != null
                                ? item.itemName
                                : "?";

                        currentEvent.Use();
                        return;
                    }

                    index++;
                }

                // 진화의 돌
                foreach (EvolutionStoneData stone in itemManager.Stones)
                {
                    if (InventorySlotRect(index).Contains(
                            inventoryMousePosition))
                    {
                        _dragging = stone;
                        _draggingTool = DragToolType.None;

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
                if (_inspectUnit != clickedUnit)
                    _inspectScroll = Vector2.zero;

                _inspectUnit = clickedUnit;

                currentEvent.Use();
            }
        }
        else if (
            canEquip &&
            currentEvent.type == EventType.MouseUp &&
            IsDragging)
        {
            PokemonUnit targetUnit =
                RaycastUnit(currentEvent.mousePosition);

            bool success = false;

            if (targetUnit != null)
            {
                switch (_draggingTool)
                {
                    case DragToolType.Remover:
                        success =
                            itemManager.UseRemover(
                                targetUnit
                            );
                        break;

                    case DragToolType.Reforger:
                        success =
                            itemManager.UseReforger(
                                targetUnit
                            );
                        break;

                    case DragToolType.None:
                        if (_dragging != null)
                        {
                            success =
                                itemManager.EquipToUnit(
                                    _dragging,
                                    targetUnit
                                );
                        }
                        break;
                }

                Debug.Log(
                    $"[ItemHud] " +
                    $"{(success ? "사용 성공" : "사용 실패")}: " +
                    $"{_dragLabel} → " +
                    $"{targetUnit.data?.pokemonName}"
                );
            }

            ClearDragging();

            currentEvent.Use();
        }
    }

    private void DrawDragGhost()
    {
        if (!IsDragging)
            return;

        Vector2 mousePosition =
            Event.current.mousePosition;

        GUI.Box(
            new Rect(
                mousePosition.x - 45f,
                mousePosition.y - 18f,
                90f,
                36f
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

        const float panelWidth = 690f;

        // 화면 높이를 넘지 않도록 제한한다.
        float panelHeight = Mathf.Clamp(
            Screen.height - 20f,
            300f,
            770f
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

        DrawStatComparisonTable(unit, data);

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
    /// QA 정보창에서 사용하는 능력치 묶음.
    /// </summary>
    private struct UnitStatSnapshot
    {
        public float hp;
        public float attack;
        public float defense;
        public float attackSpeed;
        public float spellPower;
        public float critChance;
        public float critMultiplier;
    }

    /// <summary>
    /// DB 기본값, 일반진화 적용값, 특수진화 적용값,
    /// 장비 증가량, 장비 포함 최종 능력치를
    /// 한 화면에서 가로로 비교한다.
    /// </summary>
    private static void DrawStatComparisonTable(PokemonUnit unit, PokemonData data)
    {
        bool isSpecialEvolution =
            unit.isTradeEvolved ||
            unit.equippedStone != null;

        bool isNormalEvolution =
            !isSpecialEvolution &&
            unit.starLevel > 1;

        UnitStatSnapshot evolvedStats =
            CreateCurrentUnitStats(unit);

        UnitStatSnapshot finalStats =
            ApplyEquipmentStats(
                evolvedStats,
                unit.items
            );

        GUILayout.Label("── 능력치 비교 QA ──");

        GUILayout.Space(4f);

        // 헤더
        GUILayout.BeginHorizontal();

        DrawTableCell(
            "능력치\nDB 기본값",
            122f,
            54f
        );

        DrawTableCell(
            "일반진화",
            122f,
            54f
        );

        DrawTableCell(
            "특수진화\n진화의 돌 / 통신진화",
            122f,
            54f
        );

        DrawTableCell(
            "장비\n증가량",
            122f,
            54f
        );

        DrawTableCell(
            "최종능력치\n장비 포함",
            122f,
            54f
        );

        GUILayout.EndHorizontal();

        DrawStatRow(
    "체력",
    data.hp,
    isNormalEvolution ? evolvedStats.hp : 0f,
    isSpecialEvolution ? evolvedStats.hp : 0f,
    evolvedStats.hp,
    finalStats.hp,
    "0"
);

        DrawStatRow(
            "공격력",
            data.attack,
            isNormalEvolution ? evolvedStats.attack : 0f,
            isSpecialEvolution ? evolvedStats.attack : 0f,
            evolvedStats.attack,
            finalStats.attack,
            "0"
        );

        DrawStatRow(
            "방어력",
            data.defense,
            isNormalEvolution ? evolvedStats.defense : 0f,
            isSpecialEvolution ? evolvedStats.defense : 0f,
            evolvedStats.defense,
            finalStats.defense,
            "0"
        );

        DrawStatRow(
            "공격속도",
            data.attackSpeed,
            isNormalEvolution
                ? evolvedStats.attackSpeed
                : 0f,
            isSpecialEvolution
                ? evolvedStats.attackSpeed
                : 0f,
            evolvedStats.attackSpeed,
            finalStats.attackSpeed,
            "0.00"
        );

        DrawStatRow(
            "스킬 위력",
            data.spellPower,
            isNormalEvolution
                ? evolvedStats.spellPower
                : 0f,
            isSpecialEvolution
                ? evolvedStats.spellPower
                : 0f,
            evolvedStats.spellPower,
            finalStats.spellPower,
            "0"
        );

        DrawStatRow(
            "치명타율",
            evolvedStats.critChance * 100f,
            0f,
            0f,
            evolvedStats.critChance * 100f,
            finalStats.critChance * 100f,
            "0.#"
        );

        DrawStatRow(
            "치명타 피해",
            evolvedStats.critMultiplier * 100f,
            0f,
            0f,
            evolvedStats.critMultiplier * 100f,
            finalStats.critMultiplier * 100f,
            "0.#"
        );

        GUILayout.Space(6f);

        string evolutionType;

        if (isSpecialEvolution)
        {
            evolutionType =
                unit.isTradeEvolved
                    ? "통신진화"
                    : "진화의 돌";
        }
        else if (isNormalEvolution)
        {
            evolutionType = "일반진화";
        }
        else
        {
            evolutionType = "미진화";
        }

        GUILayout.Label(
            $"현재 진화 분류: {evolutionType}"
        );

        GUILayout.Label(
            $"성급: {unit.starLevel}성"
        );

        GUILayout.Label(
            $"장착 일반 아이템: " +
            $"{(unit.items != null ? unit.items.Count : 0)}개"
        );

        DrawEquipmentSummary(unit);

        GUILayout.Label(
            $"공격 사거리: {unit.Range}"
        );

        GUILayout.Label(
            $"마나 비용: " +
            $"{data.manaCost} → {unit.EffectiveManaCost}"
        );

        GUILayout.Label(
            "※ 일반진화·특수진화·장비는 각각의 적용값을 표시하며, " +
            "현재 유닛에 적용되지 않은 항목은 0으로 표시"
        );

        GUILayout.Space(8f);
    }

    /// <summary>
    /// 현재 장착된 일반 아이템과 주요 스탯 효과를 QA용으로 표시한다.
    /// </summary>
    /// <summary>
    /// 현재 장착된 일반 아이템과 진화의 돌을
    /// QA용 장비 정보로 표시한다.
    /// </summary>
    private static void DrawEquipmentSummary(
        PokemonUnit unit)
    {
        GUILayout.Space(6f);

        GUILayout.Label("── 장착 장비 효과 ──");

        bool hasItems =
            unit.items != null &&
            unit.items.Count > 0;

        bool hasStone =
            unit.equippedStone != null;

        if (!hasItems && !hasStone)
        {
            GUILayout.Label(
                "장착된 장비 및 진화의 돌 없음"
            );

            return;
        }

        // ─────────────────────────────
        // 일반 장비
        // ─────────────────────────────

        if (hasItems)
        {
            foreach (ItemData item in unit.items)
            {
                if (item == null)
                    continue;

                GUILayout.Label(
                    $"• [일반 장비] {item.itemName}"
                );

                List<string> effects =
                    new List<string>();

                if (Mathf.Abs(item.hpBonus) > 0.0001f)
                {
                    effects.Add(
                        $"체력 +{item.hpBonus:0}"
                    );
                }

                if (Mathf.Abs(item.maxHpPct) > 0.0001f)
                {
                    effects.Add(
                        $"최대 체력 +{item.maxHpPct:0.#}%"
                    );
                }

                if (Mathf.Abs(item.attackBonus) > 0.0001f)
                {
                    effects.Add(
                        $"공격력 +{item.attackBonus:0}"
                    );
                }

                if (Mathf.Abs(item.defenseBonus) > 0.0001f)
                {
                    effects.Add(
                        $"방어력 +{item.defenseBonus:0}"
                    );
                }

                if (Mathf.Abs(item.spDefBonus) > 0.0001f)
                {
                    effects.Add(
                        $"방어력 +{item.spDefBonus:0}"
                    );
                }

                if (Mathf.Abs(item.attackSpeedBonus) > 0.0001f)
                {
                    effects.Add(
                        $"공격속도 +{item.attackSpeedBonus:0.#}%"
                    );
                }

                if (Mathf.Abs(item.spAtkPct) > 0.0001f)
                {
                    effects.Add(
                        $"스킬 위력 +{item.spAtkPct:0.#}%"
                    );
                }

                if (Mathf.Abs(item.criPct) > 0.0001f)
                {
                    effects.Add(
                        $"치명타율 +{item.criPct:0.#}%"
                    );
                }

                if (Mathf.Abs(item.criDmgPct) > 0.0001f)
                {
                    effects.Add(
                        $"치명타 피해 +{item.criDmgPct:0.#}%"
                    );
                }

                GUILayout.Label("  기본 능력치");

                if (effects.Count == 0)
                {
                    GUILayout.Label("  - 없음");
                }
                else
                {
                    foreach (string effect in effects)
                    {
                        GUILayout.Label(
                            $"  - {effect}"
                        );
                    }
                }

                GUILayout.Label("  아이템 효과");

                string effectDescription =
                    item.description;

                if (string.IsNullOrWhiteSpace(
                        effectDescription))
                {
                    GUILayout.Label("  - 없음");
                }
                else
                {
                    effectDescription =
                        effectDescription
                            .Replace("\\n", "\n")
                            .Replace("\r\n", "\n");

                    GUILayout.Label(
                        $"  {effectDescription}"
                    );
                }

                GUILayout.Space(4f);
            }
        }

        // ─────────────────────────────
        // 진화의 돌
        // ─────────────────────────────

        if (hasStone)
        {
            EvolutionStoneData stone =
                unit.equippedStone;

            GUILayout.Label(
                $"• [진화의 돌] {stone.stoneName}"
            );

            GUILayout.Label(
                $"  현재 포켓몬: " +
                $"{unit.data?.pokemonName}"
            );

            if (unit.preStoneData != null)
            {
                GUILayout.Label(
                    $"  진화 전 포켓몬: " +
                    $"{unit.preStoneData.pokemonName}"
                );

                GUILayout.Label(
                    $"  진화 결과: " +
                    $"{unit.preStoneData.pokemonName} → " +
                    $"{unit.data?.pokemonName}"
                );
            }

            GUILayout.Label(
                "  특수진화 성급 배율 적용"
            );
        }
    }

    /// <summary>
    /// PokemonUnit에서 성급·진화·영웅증강까지 적용된
    /// 장비 적용 전 전투 기초 능력치를 가져온다.
    /// 일반 아이템 효과는 ApplyEquipmentStats에서 별도로 적용한다.
    /// </summary>
    private static UnitStatSnapshot CreateCurrentUnitStats(
        PokemonUnit unit)
    {
        return new UnitStatSnapshot
        {
            hp = unit.MaxHp,
            attack = unit.Attack,
            defense = unit.Defense,
            attackSpeed = unit.AttackSpeed,
            spellPower = unit.SpellPower,

            // QA 표시용 기본값.
            // 현재 프로젝트 BattleUnit 기본값이 다르면
            // 그 값에 맞춰 변경한다.
            critChance = 0f,
            critMultiplier = 1.5f
        };
    }

    /// <summary>
    /// 장비 적용 전 능력치에 일반 아이템 효과를
    /// ItemStatEffect.OnCombatStart와 동일한 순서로 적용하여
    /// 장비 포함 최종 능력치를 반환한다.
    /// </summary>
    private static UnitStatSnapshot ApplyEquipmentStats(
        UnitStatSnapshot stats,
        List<ItemData> items)
    {
        if (items == null)
            return stats;

        foreach (ItemData item in items)
        {
            if (item == null)
                continue;

            // 최대 체력:
            // 고정 증가량 + 현재 최대 체력 기준 퍼센트 증가
            float bonusHp =
                item.hpBonus +
                stats.hp *
                (item.maxHpPct * 0.01f);

            stats.hp += bonusHp;

            // 공격력 고정 증가
            stats.attack +=
                item.attackBonus;

            // 스킬 위력 퍼센트 증가
            stats.spellPower +=
                stats.spellPower *
                (item.spAtkPct * 0.01f);

            // 공격속도 퍼센트 증가
            stats.attackSpeed +=
                stats.attackSpeed *
                (item.attackSpeedBonus * 0.01f);

            // 특수방어 폐지 후 일반 방어력으로 통합
            stats.defense +=
                item.defenseBonus +
                item.spDefBonus;

            // 치명타율 및 치명타 피해
            stats.critChance +=
                item.criPct * 0.01f;

            stats.critMultiplier +=
                item.criDmgPct * 0.01f;
        }

        return stats;
    }

    /// <summary>
    /// DB 기본값, 일반진화, 특수진화,
    /// 장비 증가량, 최종능력치를 한 행에 표시한다.
    /// </summary>
    private static void DrawStatRow(
        string statName,
        float dbValue,
        float normalValue,
        float specialValue,
        float preEquipmentValue,
        float finalValue,
        string format,
        string suffix = "")
    {
        const float rowHeight = 42f;

        float equipmentBonus =
            finalValue - preEquipmentValue;

        string equipmentText;

        if (Mathf.Abs(equipmentBonus) < 0.0001f)
        {
            equipmentText = "0";
        }
        else
        {
            string sign =
                equipmentBonus > 0f
                    ? "+"
                    : "";

            equipmentText =
                sign +
                equipmentBonus.ToString(format) +
                suffix;
        }

        GUILayout.BeginHorizontal();

        DrawTableCell(
    $"{statName}\n{dbValue.ToString(format)}{suffix}",
    122f,
    rowHeight
);

        DrawTableCell(
            $"{normalValue.ToString(format)}{suffix}",
            122f,
            rowHeight
        );

        DrawTableCell(
            $"{specialValue.ToString(format)}{suffix}",
            122f,
            rowHeight
        );

        DrawTableCell(
            equipmentText,
            122f,
            rowHeight
        );

        DrawTableCell(
            $"{finalValue.ToString(format)}{suffix}",
            122f,
            rowHeight
        );

        GUILayout.EndHorizontal();
    }

    private static void DrawTableCell(string text, float width, float height)
    {
        GUILayout.Box(text, GUILayout.Width(width), GUILayout.Height(height));
    }

    /// <summary>
    /// 포인터 아래 모든 Collider를 검사하여
    /// 가장 가까운 PokemonUnit을 찾는다.
    /// 필드 타일 Collider가 먼저 맞아도 유닛 선택이 가능하다.
    /// </summary>
    private PokemonUnit RaycastUnit(Vector2 guiMousePosition)
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