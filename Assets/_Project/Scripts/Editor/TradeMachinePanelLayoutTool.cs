using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 통신기 안내창(<see cref="TradeMachinePanelUI"/>) 프리팹을 만들고, Canvas에 한 벌 깔고,
/// 씬의 통신기(<see cref="SellZone"/> Trade)에 물려주는 에디터 전용 도구.
///
/// <see cref="SampleDeckPanelLayoutTool"/>과 같은 규약이다 —
/// <b>프리팹이 없을 때만</b> 코드로 한 벌 만들어 저장하고, 저장된 뒤로는 내용을 일절 건드리지 않는다.
/// 화면을 보며 고치려면 프리팹을 열어 고치면 되고, 처음부터 다시 뽑고 싶으면 프리팹 파일을 지우고
/// 메뉴를 다시 돌리면 된다.
///
/// 창의 자리는 통신기 위로 <see cref="TradeMachinePanelUI"/>가 매 프레임 잡아준다 —
/// 여기서는 기준점(아래 가운데)과 폴백 위치만 정해 둔다.
///
/// 아이콘 한 칸은 게임 안에서 쓰는 <c>UnitSlot_Pf</c>를 그대로 물린다 — 코스트별 테두리 규칙을
/// 통신기만 따로 갖지 않기 위해서다. 프리팹을 못 찾으면 그 칸만 비우고 경고를 남긴다.
///
/// 실행: 메뉴 <b>PokeChess/UI/Create Trade Machine Panel</b> (GameSceneTest를 연 상태에서)
/// </summary>
public static class TradeMachinePanelLayoutTool
{
    private const string UNDO_NAME = "Create Trade Machine Panel";

    private const string PANEL_NAME = "TradeMachinePanel";
    private const string PANEL_PREFAB_PATH = "Assets/Art/UI/Ui_Prefabs/TradeMachinePanel_Pf.prefab";
    private const string UNIT_SLOT_PREFAB_PATH = "Assets/Art/UI/Ui_Prefabs/UnitSlot_Pf.prefab";

    // ── 색 (견본덱·옵션창 도구와 같은 톤. 아트 확정 시 여기만 고치면 된다) ──
    private static readonly Color WINDOW_COLOR = new Color32(18, 18, 22, 245);
    private static readonly Color DIVIDER_COLOR = new Color32(70, 70, 82, 255);
    private static readonly Color TEXT_COLOR = new Color32(235, 235, 240, 255);
    private static readonly Color SUBTEXT_COLOR = new Color32(170, 170, 180, 255);
    private static readonly Color ACCENT_COLOR = new Color32(255, 216, 120, 255);
    private static readonly Color BUTTON_COLOR = new Color32(52, 52, 62, 255);

    private static readonly Color READY_COLOR = new Color32(115, 235, 158, 255);
    private static readonly Color BLOCKED_COLOR = new Color32(235, 115, 115, 255);

    // ── 크기 ──
    private const float PANEL_WIDTH = 430f;
    private const float PADDING = 18f;
    private const float SPACING = 7f;
    private const float SLOT_SIZE = 52f;
    private const float BUTTON_HEIGHT = 40f;

    /// <summary>진화 전/후 줄과 그 사이 화살표. 이름으로 찾아 덧붙이므로 프리팹과 철자가 같아야 한다.</summary>
    private const string EVOLVE_SLOT_ROW_NAME = "Evolve_Slot_Panel";

    private const string EVOLVE_RESULT_ROW_NAME = "Evolve_Result_Panel";
    private const string EVOLVE_ARROW_NAME = "Evolve_Arrow_Image";

    /// <summary>화살표 칸 높이. 스프라이트는 preserveAspect로 이 높이에 맞춰 가운데 놓인다.</summary>
    private const float ARROW_HEIGHT = 26f;

    /// <summary>통신진화 대상은 현재 7종이다. 한 줄에 다 놓이도록 열 수를 맞춰 둔다.</summary>
    private const int SLOT_COLUMNS = 7;

    // ── 문구 (기획 확정본. 코드가 바꾸는 건 전송 상태 줄과 대기 마릿수뿐이다) ──
    private const string TITLE = "통신기";

    private const string SEND_BODY =
        "- 라운드당 1회 전송할 수 있습니다\n" +
        "- 코스트·성급 제한 없음\n" +
        "- 장착한 아이템과 진화의 돌도 함께 넘어갑니다";

    private const string EVOLVE_HEADER = "통신진화: 통신 교환으로 진화하는 포켓몬";
    private const string EVOLVE_BODY = "- 받는 즉시 진화하며 되돌릴 수 없습니다";

    private const string RECEIVE_BODY =
        "- 클릭하면 빈 벤치 칸만큼 도착 순서대로 받습니다\n" +
        "- 벤치가 가득 차면 받을 수 없습니다";

    private const string RECEIVE_LABEL = "포켓몬 받기";

    [MenuItem("PokeChess/UI/Create Trade Machine Panel")]
    private static void CreateLayout()
    {
        GameObject canvasGO = FindCanvas();
        if (canvasGO == null)
        {
            Debug.LogError("[TradeMachinePanelLayoutTool] 열려 있는 씬에서 Canvas를 찾지 못했습니다. " +
                           "GameSceneTest 씬을 연 뒤 다시 실행해 주세요.");
            return;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_NAME);

        Transform canvas = canvasGO.transform;

        TradeMachinePanelUI panel = EnsurePanel(canvas);
        if (panel == null)
        {
            Debug.LogError("[TradeMachinePanelLayoutTool] 안내창을 만들지 못했습니다.");
            return;
        }

        WireSellZone(panel);

        // 씬에는 꺼둔 채로 저장한다 — 커서를 올렸을 때만 열리는 창이다.
        SetActiveWithUndo(panel.gameObject, false);

        EditorSceneManager.MarkSceneDirty(canvasGO.scene);
        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"[TradeMachinePanelLayoutTool] '{PANEL_NAME}' 생성/배선 완료.\n" +
                  "창은 통신기 위에 붙어 뜹니다 — 씬에서 옮길 필요 없이 창 인스펙터의 " +
                  "'자리 > 월드 오프셋(Y)'으로 높이를 조절하세요.");
    }

    /// <summary>
    /// 이미 만들어진 프리팹에 <b>진화 후 줄</b>(화살표 + 아이콘 줄)만 덧붙인다.
    ///
    /// 생성 도구는 프리팹이 있으면 손대지 않는 것이 규약이라, 나중에 늘어난 구성은 이렇게 따로 부른다.
    /// <b>없는 것만 만들고</b> 이미 있으면 그대로 두므로 여러 번 눌러도 안전하다.
    ///
    /// 실행: 메뉴 <b>PokeChess/UI/Add Trade Evolve Result Row</b>
    /// </summary>
    [MenuItem("PokeChess/UI/Add Trade Evolve Result Row")]
    private static void AddEvolveResultRow()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PANEL_PREFAB_PATH) == null)
        {
            Debug.LogError($"[TradeMachinePanelLayoutTool] 프리팹이 없습니다: {PANEL_PREFAB_PATH} — " +
                           "먼저 PokeChess/UI/Create Trade Machine Panel을 실행하세요.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PANEL_PREFAB_PATH);
        _editingPrefabContents = true;

        try
        {
            var panel = (RectTransform)root.transform;
            var ui = root.GetComponent<TradeMachinePanelUI>();

            if (ui == null)
            {
                Debug.LogError("[TradeMachinePanelLayoutTool] 프리팹 루트에 TradeMachinePanelUI가 없습니다.");
                return;
            }

            Transform slotRow = panel.Find(EVOLVE_SLOT_ROW_NAME);

            if (slotRow == null)
            {
                Debug.LogError($"[TradeMachinePanelLayoutTool] '{EVOLVE_SLOT_ROW_NAME}'을 찾지 못했습니다 — " +
                               "프리팹 구조가 도구와 다릅니다. 줄을 직접 추가한 뒤 인스펙터에서 물려 주세요.");
                return;
            }

            GameObject arrow = BuildArrow(panel, EVOLVE_ARROW_NAME);
            RectTransform resultRow = BuildSlotRow(panel, EVOLVE_RESULT_ROW_NAME);

            if (arrow == null || resultRow == null) return;

            // 진화 전 줄 → 화살표 → 진화 후 줄 순서로 끼운다.
            arrow.transform.SetSiblingIndex(slotRow.GetSiblingIndex() + 1);
            resultRow.SetSiblingIndex(arrow.transform.GetSiblingIndex() + 1);

            var so = new SerializedObject(ui);
            WireField(so, "_evolveArrow", arrow);
            WireField(so, "_evolveResultSlotRoot", resultRow);
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(root, PANEL_PREFAB_PATH);

            Debug.Log($"[TradeMachinePanelLayoutTool] '{EVOLVE_ARROW_NAME}' + '{EVOLVE_RESULT_ROW_NAME}'을 " +
                      "프리팹에 추가하고 배선했습니다.\n" +
                      "화살표 스프라이트는 비어 있습니다 — 프리팹을 열어 Image의 Source Image에 넣어 주세요.");
        }
        finally
        {
            _editingPrefabContents = false;
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ─────────────────────────────────────────
    // 프리팹 확보
    // ─────────────────────────────────────────

    /// <summary>
    /// 프리팹이 있으면 인스턴스만 확보하고, 없으면 코드로 한 벌 만들어 프리팹으로 굳힌다.
    /// 이미 올바른 인스턴스가 깔려 있으면 <b>아무것도 하지 않는다</b>(위치·크기 유지).
    /// </summary>
    private static TradeMachinePanelUI EnsurePanel(Transform canvas)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PANEL_PREFAB_PATH);
        Transform existing = canvas.Find(PANEL_NAME);

        if (asset != null)
        {
            if (IsInstanceOf(existing, PANEL_PREFAB_PATH))
                return existing.GetComponent<TradeMachinePanelUI>();

            // 프리팹과 무관한 옛 오브젝트는 치운다(이전 버전이 코드로 만들어둔 것).
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, canvas);
            instance.name = PANEL_NAME;
            instance.transform.SetAsLastSibling();
            RegisterCreated(instance);

            return instance.GetComponent<TradeMachinePanelUI>();
        }

        // 프리팹이 아직 없다 — 코드로 한 벌 만들어 그대로 프리팹으로 저장한다(사실상 최초 1회).
        TradeMachinePanelUI built = BuildPanel(canvas);
        if (built == null) return null;

        GameObject saved = PrefabUtility.SaveAsPrefabAssetAndConnect(
            built.gameObject, PANEL_PREFAB_PATH, InteractionMode.AutomatedAction);

        if (saved == null)
        {
            Debug.LogWarning($"[TradeMachinePanelLayoutTool] 프리팹으로 저장하지 못했습니다: {PANEL_PREFAB_PATH}");
            return built;
        }

        Debug.Log($"[TradeMachinePanelLayoutTool] 안내창을 프리팹으로 저장했습니다 — {PANEL_PREFAB_PATH}\n" +
                  "이제부터 이 프리팹을 열어 고치면 되고, 메뉴를 다시 돌려도 덮어쓰지 않습니다.");

        return built;
    }

    // ─────────────────────────────────────────
    // 계층 생성 (최초 1회)
    // ─────────────────────────────────────────

    private static TradeMachinePanelUI BuildPanel(Transform canvas)
    {
        RectTransform panel = EnsureChild(canvas, PANEL_NAME);
        if (panel == null) return null;   // 같은 이름의 비UI 오브젝트가 있다(EnsureChild가 경고를 남긴다)

        panel.SetAsLastSibling();

        // 씬 뷰에서 보이는 자리. 실제 자리는 TradeMachinePanelUI가 통신기 위로 잡아주므로
        // (pivot과 무관하게 창의 아래 가운데를 통신기 위에 세운다) 여기 값은 앵커를 못 찾았을 때
        // 쓰는 폴백이다. 그래도 기준점은 '아래 가운데'로 둔다 — 씬 뷰에서 보이는 모습이 실제와 같아진다.
        SetRect(panel,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 220f), new Vector2(PANEL_WIDTH, 0f));

        // 창 뒤 UI가 눌리지 않도록 Raycast Target을 켠다. 창 밖 3D 조작은 물리 레이캐스트라 영향 없다.
        EnsureImage(panel, WINDOW_COLOR, true);

        // 내용 높이에 맞춰 창이 늘어난다 — 줄을 껐다 켜면 자리가 접힌다.
        var layout = EnsureComponent<VerticalLayoutGroup>(panel.gameObject);
        RecordUndo(layout);
        layout.padding = new RectOffset((int)PADDING, (int)PADDING, (int)PADDING, (int)PADDING);
        layout.spacing = SPACING;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        EditorUtility.SetDirty(layout);

        var fitter = EnsureComponent<ContentSizeFitter>(panel.gameObject);
        RecordUndo(fitter);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        EditorUtility.SetDirty(fitter);

        // 고정 문구 줄은 만들기만 하면 된다 — 코드가 바꾸는 건 아래 배선한 세 줄과 버튼뿐이다.
        BuildText(panel, "Title_Text", TITLE, 22f, TEXT_COLOR);
        EnsureDivider(panel, "Divider");

        TMP_Text sendHeader = BuildText(panel, "Send_Header_Text", "포켓몬: 전송 준비 완료", 17f, READY_COLOR);
        TMP_Text sendReason = BuildText(panel, "Send_Reason_Text", "", 14f, BLOCKED_COLOR);
        BuildText(panel, "Send_Body_Text", SEND_BODY, 14f, SUBTEXT_COLOR);

        EnsureDivider(panel, "Divider (1)");

        BuildText(panel, "Evolve_Header_Text", EVOLVE_HEADER, 17f, ACCENT_COLOR);
        BuildText(panel, "Evolve_Body_Text", EVOLVE_BODY, 14f, SUBTEXT_COLOR);
        RectTransform slotRoot = BuildSlotRow(panel, EVOLVE_SLOT_ROW_NAME);
        GameObject arrow = BuildArrow(panel, EVOLVE_ARROW_NAME);
        RectTransform resultRoot = BuildSlotRow(panel, EVOLVE_RESULT_ROW_NAME);

        EnsureDivider(panel, "Divider (2)");

        TMP_Text receiveHeader = BuildText(panel, "Receive_Header_Text", "파트너가 보낸 포켓몬 대기 중 : 0마리", 17f, TEXT_COLOR);
        BuildText(panel, "Receive_Body_Text", RECEIVE_BODY, 14f, SUBTEXT_COLOR);
        Button receiveButton = BuildButton(panel, "Receive_Button", RECEIVE_LABEL);

        // 사유 줄은 평소 접어 둔다 — 보낼 수 있을 때는 표시할 내용이 없다.
        if (sendReason != null) SetActiveWithUndo(sendReason.gameObject, false);

        ApplyFontToAllTMP(panel, FindKoreanFontAsset());

        var ui = EnsureComponent<TradeMachinePanelUI>(panel.gameObject);
        var so = new SerializedObject(ui);

        WireField(so, "_sendHeaderText", sendHeader);
        WireField(so, "_sendReasonText", sendReason);
        WireField(so, "_receiveHeaderText", receiveHeader);
        WireField(so, "_receiveButton", receiveButton);
        WireField(so, "_receiveButtonLabel",
                  receiveButton != null ? receiveButton.GetComponentInChildren<TMP_Text>(true) : null);
        WireField(so, "_evolveSlotRoot", slotRoot);
        WireField(so, "_evolveArrow", arrow);
        WireField(so, "_evolveResultSlotRoot", resultRoot);
        WireField(so, "_unitSlotPrefab", LoadUnitSlotPrefab());

        so.ApplyModifiedProperties();

        return ui;
    }

    /// <summary>
    /// 진화 전 줄과 진화 후 줄 사이의 아래 화살표.
    ///
    /// 스프라이트는 아트가 넣는다 — 여기서는 자리(높이)만 잡고 비워 둔다. 세로 레이아웃이 가로를
    /// 꽉 채우므로 <c>preserveAspect</c>만 켜 두면 그림이 저절로 가운데에 놓인다.
    /// 이미 있으면 손대지 않는다(넣어둔 그림·색을 지우지 않기 위해).
    /// </summary>
    private static GameObject BuildArrow(RectTransform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        RectTransform arrow = EnsureChild(parent, name);
        if (arrow == null) return null;

        var layout = EnsureComponent<LayoutElement>(arrow.gameObject);
        RecordUndo(layout);
        layout.preferredHeight = ARROW_HEIGHT;
        layout.flexibleHeight = 0f;
        EditorUtility.SetDirty(layout);

        // 스프라이트가 없는 동안은 색만 있는 띠로 보인다 — "여기에 화살표를 넣으라"는 표시다.
        Image image = EnsureImage(arrow, new Color32(90, 90, 105, 255), false);
        RecordUndo(image);
        image.preserveAspect = true;
        EditorUtility.SetDirty(image);

        return arrow.gameObject;
    }

    /// <summary>통신진화 아이콘 줄. 한 줄에 7칸이 놓이도록 열 수를 고정한다.</summary>
    private static RectTransform BuildSlotRow(RectTransform parent, string name)
    {
        RectTransform row = EnsureChild(parent, name);
        if (row == null) return null;

        var grid = EnsureComponent<GridLayoutGroup>(row.gameObject);
        RecordUndo(grid);
        grid.cellSize = new Vector2(SLOT_SIZE, SLOT_SIZE);
        grid.spacing = new Vector2(4f, 4f);
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = SLOT_COLUMNS;
        EditorUtility.SetDirty(grid);

        // 칸 한 벌을 미리 깔아둔다 — 나머지는 TradeMachinePanelUI가 실행 중에 복제한다.
        GameObject slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UNIT_SLOT_PREFAB_PATH);

        if (slotPrefab == null)
        {
            Debug.LogWarning($"[TradeMachinePanelLayoutTool] {UNIT_SLOT_PREFAB_PATH}를 찾지 못했습니다 — " +
                             "유닛 칸은 인스펙터에서 직접 물려 주세요.");
            return row;
        }

        if (row.Find("UnitSlot") == null)
        {
            var slot = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab, row);
            slot.name = "UnitSlot";
            RegisterCreated(slot);
        }

        return row;
    }

    private static SynergyTooltipUnitSlot LoadUnitSlotPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UNIT_SLOT_PREFAB_PATH);
        return prefab != null ? prefab.GetComponent<SynergyTooltipUnitSlot>() : null;
    }

    // ─────────────────────────────────────────
    // 통신기(SellZone)에 배선
    // ─────────────────────────────────────────

    /// <summary>
    /// 씬의 통신기(zoneType = Trade)를 찾아 안내창을 물린다.
    /// 판매 존에는 물리지 않는다 — 통신기 전용 창이다.
    /// </summary>
    private static void WireSellZone(TradeMachinePanelUI panel)
    {
        SellZone[] zones = Object.FindObjectsByType<SellZone>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        int wired = 0;

        foreach (SellZone zone in zones)
        {
            var so = new SerializedObject(zone);

            SerializedProperty zoneType = so.FindProperty("_zoneType");
            if (zoneType == null || zoneType.enumValueIndex != 1) continue; // 1 = Trade

            RecordUndo(zone);
            WireField(so, "_tradePanel", panel);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(zone);

            wired++;
        }

        if (wired == 0)
        {
            Debug.LogWarning("[TradeMachinePanelLayoutTool] 씬에서 통신기(SellZone의 Zone Type = Trade)를 " +
                             "찾지 못했습니다 — 안내창을 인스펙터에서 직접 물려 주세요.");
            return;
        }

        Debug.Log($"[TradeMachinePanelLayoutTool] 통신기 {wired}개에 안내창을 물렸습니다.");
    }

    // ─────────────────────────────────────────
    // 공용 헬퍼
    // ─────────────────────────────────────────

    /// <summary>
    /// 글자 한 줄. 같은 이름의 비UI 오브젝트가 이미 있으면 EnsureChild가 null을 주므로 그때는 건너뛴다
    /// (경고는 EnsureChild가 남긴다).
    /// </summary>
    private static TMP_Text BuildText(
        RectTransform parent, string name, string content, float fontSize, Color color)
    {
        RectTransform rect = EnsureChild(parent, name);
        if (rect == null) return null;

        TMP_Text text = rect.GetComponent<TMP_Text>();
        if (text == null) text = AddComponentTracked<TextMeshProUGUI>(rect.gameObject);

        RecordUndo(text);
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;

        // 세로 레이아웃이 줄 높이를 글자 수에 맞춰 잡도록 가로만 부모를 따라가게 한다.
        text.textWrappingMode = TextWrappingModes.Normal;
        EditorUtility.SetDirty(text);

        return text;
    }

    private static Button BuildButton(RectTransform parent, string name, string label)
    {
        RectTransform rect = EnsureChild(parent, name);
        if (rect == null) return null;

        EnsureImage(rect, BUTTON_COLOR, true);
        var button = EnsureComponent<Button>(rect.gameObject);

        var element = EnsureComponent<LayoutElement>(rect.gameObject);
        RecordUndo(element);
        element.minHeight = BUTTON_HEIGHT;
        element.preferredHeight = BUTTON_HEIGHT;
        EditorUtility.SetDirty(element);

        TMP_Text text = BuildText(rect, "Text (TMP)", label, 16f, TEXT_COLOR);
        text.alignment = TextAlignmentOptions.Center;
        SetStretch((RectTransform)text.transform, 4f, 4f, 2f, 2f);
        EditorUtility.SetDirty(text);

        return button;
    }

    private static void EnsureDivider(RectTransform parent, string name)
    {
        RectTransform line = EnsureChild(parent, name);
        if (line == null) return;

        EnsureImage(line, DIVIDER_COLOR, false);

        var element = EnsureComponent<LayoutElement>(line.gameObject);
        RecordUndo(element);
        element.minHeight = 2f;
        element.preferredHeight = 2f;
        EditorUtility.SetDirty(element);
    }

    /// <summary>이름이 같은 자식이 있으면 그대로 쓰고, 없으면 RectTransform만 가진 빈 UI 오브젝트를 만든다.</summary>
    private static RectTransform EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            if (existing is RectTransform existingRect) return existingRect;

            Debug.LogWarning($"[TradeMachinePanelLayoutTool] '{name}'에 RectTransform이 없습니다(UI 오브젝트가 아님).");
            return null;
        }

        var go = new GameObject(name, typeof(RectTransform));
        RegisterCreated(go);

        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        go.layer = parent.gameObject.layer;

        return rect;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : AddComponentTracked<T>(go);
    }

    private static Image EnsureImage(RectTransform rect, Color color, bool raycastTarget)
    {
        var image = EnsureComponent<Image>(rect.gameObject);

        RecordUndo(image);
        image.color = color;
        image.raycastTarget = raycastTarget;
        EditorUtility.SetDirty(image);

        return image;
    }

    private static void SetRect(
        RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        if (rect == null) return;

        RecordUndo(rect);

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;

        EditorUtility.SetDirty(rect);
    }

    private static void SetStretch(RectTransform rect, float left, float right, float top, float bottom)
    {
        if (rect == null) return;

        RecordUndo(rect);

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        rect.localScale = Vector3.one;

        EditorUtility.SetDirty(rect);
    }

    private static void SetActiveWithUndo(GameObject go, bool active)
    {
        if (go == null || go.activeSelf == active) return;

        RecordUndo(go);
        go.SetActive(active);
        EditorUtility.SetDirty(go);
    }

    /// <summary>주어진 오브젝트가 그 경로 프리팹의 인스턴스 루트인지.</summary>
    private static bool IsInstanceOf(Transform candidate, string prefabPath)
    {
        if (candidate == null) return false;

        Object source = PrefabUtility.GetCorrespondingObjectFromSource(candidate.gameObject);
        if (source == null) return false;

        return AssetDatabase.GetAssetPath(source) == prefabPath;
    }

    /// <summary>
    /// 이미 올바른 값이면 건드리지 않는다. target이 null이면 경고만 남기고 기존 값을 유지한다
    /// (배선이 덜 된 상태에서 멀쩡한 참조를 지우지 않기 위해).
    /// </summary>
    private static void WireField(SerializedObject so, string fieldName, Object target)
    {
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogWarning($"[TradeMachinePanelLayoutTool] 필드를 찾지 못했습니다: {fieldName}");
            return;
        }

        if (target == null)
        {
            Debug.LogWarning($"[TradeMachinePanelLayoutTool] {fieldName}에 연결할 대상을 찾지 못했습니다 — 기존 값 유지.");
            return;
        }

        if (prop.objectReferenceValue == target) return;

        prop.objectReferenceValue = target;
    }

    private static GameObject FindCanvas()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var canvas = root.GetComponentInChildren<Canvas>(true);
                if (canvas != null) return canvas.gameObject;
            }
        }

        return null;
    }

    private static TMP_FontAsset FindKoreanFontAsset()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

            if (font != null && font.name.IndexOf("NEXON", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return font;
        }

        Debug.LogWarning("[TradeMachinePanelLayoutTool] 이름에 'NEXON'이 든 TMP_FontAsset을 찾지 못해 폰트 적용을 건너뜁니다.");
        return null;
    }

    /// <summary>root 아래 모든 TMP_Text의 폰트만 교체한다. 프리팹 인스턴스(UnitSlot_Pf)는 건너뛴다.</summary>
    private static void ApplyFontToAllTMP(Transform root, TMP_FontAsset font)
    {
        if (font == null) return;

        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.font == font) continue;
            if (PrefabUtility.IsPartOfPrefabInstance(text)) continue;

            var so = new SerializedObject(text);
            SerializedProperty fontProp = so.FindProperty("m_fontAsset");
            if (fontProp == null) continue;

            RecordUndo(text);
            fontProp.objectReferenceValue = font;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(text);
        }
    }

    // ─────────────────────────────────────────
    // Undo 래퍼
    //
    // 프리팹 내용을 코드로 여는 동안(PrefabUtility.LoadPrefabContents)은 프리뷰 씬이라 Undo가
    // 통하지 않는다. 아래 헬퍼는 씬 경로와 프리팹 경로가 함께 쓰므로, 프리팹 편집 중에만
    // 기록을 건너뛴다. 저장은 SaveAsPrefabAsset이 하므로 되돌리기가 없어도 문제되지 않는다.
    // ─────────────────────────────────────────

    private static bool _editingPrefabContents;

    private static void RecordUndo(Object target)
    {
        if (_editingPrefabContents) return;
        Undo.RecordObject(target, UNDO_NAME);
    }

    private static void RegisterCreated(GameObject go)
    {
        if (_editingPrefabContents) return;
        Undo.RegisterCreatedObjectUndo(go, UNDO_NAME);
    }

    private static T AddComponentTracked<T>(GameObject go) where T : Component
    {
        return _editingPrefabContents ? go.AddComponent<T>() : Undo.AddComponent<T>(go);
    }
}
