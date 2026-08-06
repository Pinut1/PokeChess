using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면(NetworkTest 씬)의 로그인·로비 계층을 만들고 <see cref="TitleScreenUI"/>·
/// <see cref="RoomListUI"/>의 인스펙터 참조를 자동으로 연결하는 에디터 전용 도구.
///
/// <b>이미 있는 것은 건드리지 않는다.</b> Canvas 아래 기존 연출(Logo_BackPanel, PressAnyKeyText,
/// LoginRow의 배경·버튼 디자인)은 그대로 두고, 없는 오브젝트만 만든다. 예외는 두 가지다.
/// <list type="bullet">
/// <item>LobbyPanel 계층 — 이 도구가 만든 것이므로 실행할 때마다 배치 값을 다시 덮어쓴다.</item>
/// <item>Login_ID — 지금은 그냥 TextMeshProUGUI라 글자를 칠 수 없다. TMP_InputField 구조로 바꾼다
///       (기존 문구는 Placeholder로 옮긴다).</item>
/// </list>
///
/// <see cref="OptionsPanelLayoutTool"/>과 달리 오브젝트를 <b>새로 만들기도 한다</b> — 로비 화면은
/// 씬에 아직 아무것도 없기 때문이다. 전부 Undo로 묶여 있어 한 번에 되돌릴 수 있다.
/// </summary>
public static class TitleScreenLayoutTool
{
    private const string UNDO_NAME = "Create Title Screen Layout";

    private const string MODAL_DIALOG_PREFAB_PATH = "Assets/Art/UI/Ui_Prefabs/ModalDialog_Pf.prefab";

    // ── 색상(옵션창 톤에 맞춤 — 팀 아트 확정 시 인스펙터에서 조정) ──
    private static readonly Color DIM_COLOR = new Color32(0, 0, 0, 120);
    private static readonly Color WINDOW_COLOR = new Color32(18, 18, 22, 235);
    private static readonly Color LIST_BG_COLOR = new Color32(10, 10, 13, 200);
    private static readonly Color ENTRY_COLOR = new Color32(38, 38, 46, 255);
    private static readonly Color ENTRY_CHECK_COLOR = new Color32(90, 170, 255, 255);
    private static readonly Color BUTTON_COLOR = new Color32(52, 52, 62, 255);
    private static readonly Color TEXT_COLOR = new Color32(235, 235, 240, 255);
    private static readonly Color SUBTEXT_COLOR = new Color32(170, 170, 180, 255);
    private static readonly Color WARN_COLOR = new Color32(255, 150, 150, 255);

    // ── 방 목록 배치 ──
    // 목록 영역의 위/아래 여백(Lobby_Window 기준).
    private const float LIST_TOP = 124f;
    private const float LIST_BOTTOM = 132f;

    // 칸 경계. Lobby_Window의 왼/오른쪽 끝 기준이며, 머리글과 줄이 같은 값을 써야 세로로 맞는다.
    private const float COLUMN_NAME_LEFT = 70f;
    private const float COLUMN_HOST_RIGHT = 206f;
    private const float COLUMN_COUNT_RIGHT = 138f;
    private const float COLUMN_STATE_RIGHT = 46f;

    // 창 좌표 → 줄 좌표 보정. ScrollView 여백(24) + Viewport 여백(4) + Content 안쪽 여백(6).
    private const float LIST_INSET = 34f;

    [MenuItem("PokeChess/UI/Create Title Screen Layout")]
    private static void CreateLayout()
    {
        GameObject canvasGO = FindCanvas();
        if (canvasGO == null)
        {
            Debug.LogError("[TitleScreenLayoutTool] 열려 있는 씬에서 Canvas를 찾지 못했습니다. NetworkTest 씬을 연 뒤 다시 실행해 주세요.");
            return;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_NAME);

        Transform canvas = canvasGO.transform;

        var titleUI = EnsureComponent<TitleScreenUI>(canvasGO);

        ConvertLoginIdToInputField(canvas);
        RectTransform loginMessage = EnsureLoginMessageText(canvas);
        GameObject lobbyPanel = BuildLobbyPanel(canvas);
        ModalDialogUI modal = EnsureModalDialog(canvas);

        // 폰트는 이 도구가 만든 것에만 적용한다 — Canvas 전체에 돌리면 타이틀 로고·LOGIN 문구처럼
        // 일부러 다른 폰트를 쓴 기존 텍스트까지 덮어써 버린다.
        TMP_FontAsset koreanFont = FindKoreanFontAsset();
        if (lobbyPanel != null) ApplyFontToAllTMP(lobbyPanel.transform, koreanFont);
        if (loginMessage != null) ApplyFontToAllTMP(loginMessage, koreanFont);

        WireTitleScreenUI(titleUI, canvas, loginMessage, lobbyPanel, modal);

        EditorSceneManager.MarkSceneDirty(canvasGO.scene);
        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log("[TitleScreenLayoutTool] 타이틀 화면 계층 생성/연결 완료. " +
                  "LobbyPanel은 재생 중 TitleScreenUI가 켜고 끄므로 씬에서는 꺼진 채로 둡니다.");
    }

    // ─────────────────────────────────────────
    // 탐색
    // ─────────────────────────────────────────

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

        Debug.LogWarning("[TitleScreenLayoutTool] 이름에 'NEXON'이 든 TMP_FontAsset을 찾지 못해 폰트 적용을 건너뜁니다.");
        return null;
    }

    // ─────────────────────────────────────────
    // 생성 헬퍼
    // ─────────────────────────────────────────

    /// <summary>이름이 같은 자식이 있으면 그대로 쓰고, 없으면 RectTransform만 가진 빈 UI 오브젝트를 만든다.</summary>
    private static RectTransform EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            var existingRect = existing as RectTransform;
            if (existingRect != null) return existingRect;

            Debug.LogWarning($"[TitleScreenLayoutTool] '{name}'에 RectTransform이 없습니다(UI 오브젝트가 아님) — 건너뜁니다.");
            return null;
        }

        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, UNDO_NAME);

        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;

        return rect;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(go);
    }

    /// <summary>앵커/피벗과 위치·크기를 한 번에 적용한다. 스트레치 축은 offset(Left/Right/Top/Bottom)으로 지정한다.</summary>
    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        if (rect == null) return;

        Undo.RecordObject(rect, UNDO_NAME);

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

        Undo.RecordObject(rect, UNDO_NAME);

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        rect.localScale = Vector3.one;

        EditorUtility.SetDirty(rect);
    }

    private static Image EnsureImage(RectTransform rect, Color color, bool raycastTarget)
    {
        var image = EnsureComponent<Image>(rect.gameObject);

        Undo.RecordObject(image, UNDO_NAME);
        image.color = color;
        image.raycastTarget = raycastTarget;
        EditorUtility.SetDirty(image);

        return image;
    }

    private static TMP_Text EnsureText(
        RectTransform rect, string content, float fontSize, Color color,
        TextAlignmentOptions alignment)
    {
        var text = rect.GetComponent<TMP_Text>();
        if (text == null) text = Undo.AddComponent<TextMeshProUGUI>(rect.gameObject);

        Undo.RecordObject(text, UNDO_NAME);
        if (content != null) text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        EditorUtility.SetDirty(text);

        return text;
    }

    /// <summary>Image + Button + "Text (TMP)" 자식으로 된 표준 버튼 하나를 보장한다.</summary>
    private static Button EnsureButton(Transform parent, string name, string label)
    {
        RectTransform rect = EnsureChild(parent, name);
        if (rect == null) return null;

        EnsureImage(rect, BUTTON_COLOR, true);
        var button = EnsureComponent<Button>(rect.gameObject);

        RectTransform textRect = EnsureChild(rect, "Text (TMP)");
        SetStretch(textRect, 4f, 4f, 2f, 2f);
        EnsureText(textRect, label, 15f, TEXT_COLOR, TextAlignmentOptions.Center);

        return button;
    }

    // ─────────────────────────────────────────
    // Login_ID → TMP_InputField
    // ─────────────────────────────────────────

    /// <summary>
    /// Login_ID를 실제로 글자를 칠 수 있는 <see cref="TMP_InputField"/>로 바꾼다.
    ///
    /// 지금 Login_ID에는 TextMeshProUGUI 하나만 붙어 있어 "보이기만 하고 입력은 안 되는" 상태다.
    /// InputField는 입력용 Text와 Placeholder를 <b>따로 가진</b> 구조를 요구하므로, 기존 텍스트
    /// 컴포넌트는 지우고 그 서식(폰트 크기·색)과 문구를 Placeholder로 옮긴다.
    ///
    /// 배경은 새로 그리지 않는다 — 이미 LoginPanel이 배경 이미지를 갖고 있으므로 Login_ID에는
    /// 클릭을 받기 위한 <b>완전 투명</b> Image만 둔다(Image가 없으면 클릭 자체가 통과해 버린다).
    /// </summary>
    private static void ConvertLoginIdToInputField(Transform canvas)
    {
        Transform loginId = canvas.Find("LoginRow/LoginPanel/Login_ID");
        if (loginId == null)
        {
            Debug.LogWarning("[TitleScreenLayoutTool] LoginRow/LoginPanel/Login_ID를 찾지 못해 입력칸 변환을 건너뜁니다.");
            return;
        }

        var rect = (RectTransform)loginId;

        // 이미 변환돼 있으면 다시 만들지 않는다(문구를 손댔을 수 있으므로 그대로 둔다).
        if (loginId.GetComponent<TMP_InputField>() != null) return;

        // 기존 텍스트의 서식/문구를 살려 Placeholder로 넘긴다. 폰트까지 물려받아야
        // 입력칸만 다른 글꼴로 튀지 않는다.
        string placeholderText = "아이디를 입력하세요";
        float fontSize = 20f;
        Color textColor = TEXT_COLOR;
        TMP_FontAsset font = null;

        var legacyText = loginId.GetComponent<TMP_Text>();
        if (legacyText != null)
        {
            if (!string.IsNullOrWhiteSpace(legacyText.text)) placeholderText = legacyText.text;
            fontSize = legacyText.fontSize;
            textColor = legacyText.color;
            font = legacyText.font;

            Undo.DestroyObjectImmediate(legacyText);
        }

        EnsureImage(rect, new Color(1f, 1f, 1f, 0f), true);

        RectTransform textArea = EnsureChild(rect, "Text Area");
        SetStretch(textArea, 10f, 10f, 6f, 6f);
        EnsureComponent<RectMask2D>(textArea.gameObject);

        RectTransform placeholderRect = EnsureChild(textArea, "Placeholder");
        SetStretch(placeholderRect, 0f, 0f, 0f, 0f);
        TMP_Text placeholder = EnsureText(
            placeholderRect, placeholderText, fontSize,
            new Color(textColor.r, textColor.g, textColor.b, 0.5f), TextAlignmentOptions.MidlineLeft);
        placeholder.textWrappingMode = TextWrappingModes.NoWrap;
        ApplyFont(placeholder, font);

        RectTransform textRect = EnsureChild(textArea, "Text");
        SetStretch(textRect, 0f, 0f, 0f, 0f);
        TMP_Text text = EnsureText(textRect, "", fontSize, textColor, TextAlignmentOptions.MidlineLeft);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        ApplyFont(text, font);

        var input = Undo.AddComponent<TMP_InputField>(rect.gameObject);

        Undo.RecordObject(input, UNDO_NAME);
        input.textViewport = textArea;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 16;     // 기존 IMGUI TextField와 같은 제한.
        input.customCaretColor = true;
        input.caretColor = textColor;
        input.caretWidth = 2;
        input.text = "";
        EditorUtility.SetDirty(input);

        Debug.Log("[TitleScreenLayoutTool] Login_ID를 TMP_InputField로 변환했습니다(기존 문구는 Placeholder로 이동).");
    }

    /// <summary>
    /// 로그인 오류 안내("아이디를 입력해 주세요"). LoginRow의 HorizontalLayoutGroup에 끌려가지 않도록
    /// LayoutElement.ignoreLayout으로 빼두고, 줄 아래에 겹쳐 놓는다.
    /// </summary>
    private static RectTransform EnsureLoginMessageText(Transform canvas)
    {
        Transform loginRow = canvas.Find("LoginRow");
        if (loginRow == null)
        {
            Debug.LogWarning("[TitleScreenLayoutTool] LoginRow를 찾지 못해 로그인 안내 문구를 만들지 못했습니다.");
            return null;
        }

        RectTransform rect = EnsureChild(loginRow, "Login_MessageText");
        if (rect == null) return null;

        var layoutElement = EnsureComponent<LayoutElement>(rect.gameObject);
        Undo.RecordObject(layoutElement, UNDO_NAME);
        layoutElement.ignoreLayout = true;
        EditorUtility.SetDirty(layoutElement);

        SetRect(rect,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
            new Vector2(0f, -8f), new Vector2(500f, 28f));

        EnsureText(rect, "아이디를 입력해 주세요.", 16f, WARN_COLOR, TextAlignmentOptions.Center);

        SetActiveWithUndo(rect.gameObject, false);

        return rect;
    }

    // ─────────────────────────────────────────
    // 로비 패널
    // ─────────────────────────────────────────

    private static GameObject BuildLobbyPanel(Transform canvas)
    {
        RectTransform panel = EnsureChild(canvas, "LobbyPanel");
        if (panel == null) return null;

        SetStretch(panel, 0f, 0f, 0f, 0f);
        EnsureImage(panel, DIM_COLOR, true);
        EnsureComponent<RoomListUI>(panel.gameObject);

        RectTransform window = EnsureChild(panel, "Lobby_Window");
        SetRect(window,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(760f, 560f));
        EnsureImage(window, WINDOW_COLOR, true);

        RectTransform title = EnsureChild(window, "Lobby_TitleText");
        SetRect(title,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -16f), new Vector2(-48f, 40f));
        EnsureText(title, "로비", 26f, TEXT_COLOR, TextAlignmentOptions.Center);

        RectTransform countText = EnsureChild(window, "RoomList_CountText");
        SetRect(countText,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(24f, -66f), new Vector2(90f, 28f));
        EnsureText(countText, "(0)", 18f, TEXT_COLOR, TextAlignmentOptions.MidlineLeft);

        Button refresh = EnsureButton(window, "RefreshButton", "새로고침");
        if (refresh != null)
        {
            SetRect((RectTransform)refresh.transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -62f), new Vector2(120f, 34f));
        }

        BuildRoomListHeaderRow(window);
        BuildRoomListScrollView(window);

        RectTransform status = EnsureChild(window, "RoomList_StatusText");
        SetStretch(status, 24f, 24f, LIST_TOP, LIST_BOTTOM);
        EnsureText(status, "방 목록을 불러오는 중입니다...", 17f, SUBTEXT_COLOR, TextAlignmentOptions.Center);

        BuildLobbyButtonRow(window);

        RectTransform message = EnsureChild(window, "Lobby_MessageText");
        SetRect(message,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 26f), new Vector2(-48f, 26f));
        EnsureText(message, "", 16f, WARN_COLOR, TextAlignmentOptions.Center);
        SetActiveWithUndo(message.gameObject, false);

        // 재생 중에는 TitleScreenUI가 단계에 맞춰 켠다 — 씬에서는 꺼둬야 타이틀 연출을 가리지 않는다.
        SetActiveWithUndo(panel.gameObject, false);

        return panel.gameObject;
    }

    /// <summary>
    /// 줄의 네 칸이 무엇인지 알려주는 머리글. 줄 안의 칸과 <b>같은 x 오프셋</b>을 쓰도록
    /// COLUMN_* 상수를 공유한다 — 한쪽만 옮기면 머리글과 값이 어긋난다.
    /// </summary>
    private static void BuildRoomListHeaderRow(RectTransform window)
    {
        RectTransform row = EnsureChild(window, "RoomList_HeaderRow");
        SetRect(row,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -98f), new Vector2(0f, 22f));

        RectTransform name = EnsureChild(row, "NameLabel");
        SetRect(name,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(COLUMN_NAME_LEFT, 0f), new Vector2(220f, 22f));
        EnsureText(name, "방 이름", 14f, SUBTEXT_COLOR, TextAlignmentOptions.MidlineLeft);

        RectTransform host = EnsureChild(row, "HostLabel");
        SetRect(host,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-COLUMN_HOST_RIGHT, 0f), new Vector2(190f, 22f));
        EnsureText(host, "방장", 14f, SUBTEXT_COLOR, TextAlignmentOptions.MidlineRight);

        RectTransform count = EnsureChild(row, "CountLabel");
        SetRect(count,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-COLUMN_COUNT_RIGHT, 0f), new Vector2(56f, 22f));
        EnsureText(count, "인원", 14f, SUBTEXT_COLOR, TextAlignmentOptions.MidlineRight);

        RectTransform state = EnsureChild(row, "StateLabel");
        SetRect(state,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-COLUMN_STATE_RIGHT, 0f), new Vector2(80f, 22f));
        EnsureText(state, "상태", 14f, SUBTEXT_COLOR, TextAlignmentOptions.MidlineRight);
    }

    private static void BuildRoomListScrollView(RectTransform window)
    {
        RectTransform scrollView = EnsureChild(window, "RoomList_ScrollView");
        SetStretch(scrollView, 24f, 24f, LIST_TOP, LIST_BOTTOM);
        EnsureImage(scrollView, LIST_BG_COLOR, true);

        RectTransform viewport = EnsureChild(scrollView, "Viewport");
        SetStretch(viewport, 4f, 4f, 4f, 4f);
        EnsureImage(viewport, new Color(1f, 1f, 1f, 0f), true);
        EnsureComponent<RectMask2D>(viewport.gameObject);

        RectTransform content = EnsureChild(viewport, "Content");
        SetRect(content,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, 0f));

        var layout = EnsureComponent<VerticalLayoutGroup>(content.gameObject);
        Undo.RecordObject(layout, UNDO_NAME);
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        EditorUtility.SetDirty(layout);

        var fitter = EnsureComponent<ContentSizeFitter>(content.gameObject);
        Undo.RecordObject(fitter, UNDO_NAME);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        EditorUtility.SetDirty(fitter);

        // 줄 사이의 상호 배타는 이 그룹이 보장한다 — 옵션창 Nav_Panel과 같은 방식.
        // allowSwitchOff는 켜둔다: 고른 방을 다시 눌러 선택을 풀 수 있어야 한다.
        var group = EnsureComponent<ToggleGroup>(content.gameObject);
        Undo.RecordObject(group, UNDO_NAME);
        group.allowSwitchOff = true;
        EditorUtility.SetDirty(group);

        var scroll = EnsureComponent<ScrollRect>(scrollView.gameObject);
        Undo.RecordObject(scroll, UNDO_NAME);
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 25f;
        EditorUtility.SetDirty(scroll);

        BuildRoomEntryTemplate(content);
    }

    /// <summary>
    /// 복제해서 쓸 줄 원본. 프리팹 에셋으로 빼지 않고 씬에 비활성으로 둔다 — 타이틀 화면 한 곳에서만
    /// 쓰는 줄이라 에셋으로 분리할 이득이 없고, 디자인을 고칠 때 씬에서 켜보고 바로 확인할 수 있다.
    /// </summary>
    private static void BuildRoomEntryTemplate(RectTransform content)
    {
        RectTransform entry = EnsureChild(content, "RoomEntry_Template");
        SetRect(entry,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, 40f));

        Image background = EnsureImage(entry, ENTRY_COLOR, true);

        var layoutElement = EnsureComponent<LayoutElement>(entry.gameObject);
        Undo.RecordObject(layoutElement, UNDO_NAME);
        layoutElement.minHeight = 40f;
        layoutElement.preferredHeight = 40f;
        EditorUtility.SetDirty(layoutElement);

        RectTransform checkmark = EnsureChild(entry, "Checkmark");
        SetRect(checkmark,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(10f, 0f), new Vector2(16f, 16f));
        Image checkImage = EnsureImage(checkmark, ENTRY_CHECK_COLOR, false);

        // 오른쪽 세 칸(상태·인원·방장)은 오른쪽 끝을 기준으로 고정 폭을 차례로 쌓고,
        // 방 이름만 남은 공간을 전부 늘려 쓴다 — 이름이 길어져도 다른 칸과 겹치지 않는다.
        RectTransform stateText = EnsureChild(entry, "StateText");
        SetRect(stateText,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-(COLUMN_STATE_RIGHT - LIST_INSET), 0f), new Vector2(80f, 24f));
        EnsureText(stateText, "진행 중", 15f, SUBTEXT_COLOR, TextAlignmentOptions.MidlineRight);

        RectTransform countText = EnsureChild(entry, "CountText");
        SetRect(countText,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-(COLUMN_COUNT_RIGHT - LIST_INSET), 0f), new Vector2(56f, 24f));
        EnsureText(countText, "1/2", 16f, TEXT_COLOR, TextAlignmentOptions.MidlineRight);

        RectTransform hostText = EnsureChild(entry, "HostText");
        SetRect(hostText,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-(COLUMN_HOST_RIGHT - LIST_INSET), 0f), new Vector2(190f, 24f));
        EnsureText(hostText, "방장닉네임", 16f, SUBTEXT_COLOR, TextAlignmentOptions.MidlineRight);

        RectTransform nameText = EnsureChild(entry, "NameText");
        SetStretch(nameText, COLUMN_NAME_LEFT - LIST_INSET, COLUMN_HOST_RIGHT - LIST_INSET + 190f, 0f, 0f);
        EnsureText(nameText, "TestRoom_1234", 17f, TEXT_COLOR, TextAlignmentOptions.MidlineLeft);

        var toggle = EnsureComponent<Toggle>(entry.gameObject);
        Undo.RecordObject(toggle, UNDO_NAME);
        toggle.targetGraphic = background;
        toggle.graphic = checkImage;
        toggle.isOn = false;
        EditorUtility.SetDirty(toggle);

        var entryUI = EnsureComponent<RoomEntryUI>(entry.gameObject);
        var so = new SerializedObject(entryUI);
        WireField(so, "_toggle", toggle);
        WireField(so, "_nameText", nameText.GetComponent<TMP_Text>());
        WireField(so, "_hostText", hostText.GetComponent<TMP_Text>());
        WireField(so, "_countText", countText.GetComponent<TMP_Text>());
        WireField(so, "_stateText", stateText.GetComponent<TMP_Text>());
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(entryUI);

        // 원본은 목록에 섞이면 안 된다.
        SetActiveWithUndo(entry.gameObject, false);
    }

    private static void BuildLobbyButtonRow(RectTransform window)
    {
        RectTransform row = EnsureChild(window, "Lobby_ButtonRow");
        SetRect(row,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 62f), new Vector2(-48f, 44f));

        var layout = EnsureComponent<HorizontalLayoutGroup>(row.gameObject);
        Undo.RecordObject(layout, UNDO_NAME);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        EditorUtility.SetDirty(layout);

        EnsureButton(row, "JoinSelectedButton", "선택 방 입장");
        EnsureButton(row, "CreateRoomButton", "방 만들기");
        EnsureButton(row, "JoinRandomButton", "랜덤 입장");
        EnsureButton(row, "RejoinButton", "이전 게임 복귀");
        EnsureButton(row, "BackButton", "뒤로");
        EnsureButton(row, "QuitButton", "게임 종료");
    }

    // ─────────────────────────────────────────
    // 공용 모달
    // ─────────────────────────────────────────

    /// <summary>
    /// 이전 세션 안내에 쓸 ModalDialog_Pf 인스턴스를 Canvas 직계에 하나 보장한다.
    /// 이미 씬에 있으면 그대로 쓴다(중복 배치 금지 — 두 장이 서로 덮어쓴다).
    /// </summary>
    private static ModalDialogUI EnsureModalDialog(Transform canvas)
    {
        var existing = Object.FindFirstObjectByType<ModalDialogUI>(FindObjectsInactive.Include);
        if (existing != null) return existing;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MODAL_DIALOG_PREFAB_PATH);
        if (prefab == null)
        {
            Debug.LogWarning($"[TitleScreenLayoutTool] {MODAL_DIALOG_PREFAB_PATH}를 찾지 못했습니다 — " +
                             "_modalDialog는 수동으로 연결해 주세요(연결 전까지 이전 세션 안내가 뜨지 않습니다).");
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas);
        Undo.RegisterCreatedObjectUndo(instance, UNDO_NAME);

        var rect = instance.transform as RectTransform;
        if (rect != null) SetStretch(rect, 0f, 0f, 0f, 0f);

        SetActiveWithUndo(instance, false);

        return instance.GetComponent<ModalDialogUI>();
    }

    // ─────────────────────────────────────────
    // 자동 연결
    // ─────────────────────────────────────────

    private static void WireTitleScreenUI(
        TitleScreenUI titleUI, Transform canvas,
        RectTransform loginMessage, GameObject lobbyPanel, ModalDialogUI modal)
    {
        var so = new SerializedObject(titleUI);
        Undo.RecordObject(titleUI, UNDO_NAME);

        Transform pressAnyKey = canvas.Find("PressAnyKeyText");
        Transform loginRow = canvas.Find("LoginRow");
        Transform loginId = canvas.Find("LoginRow/LoginPanel/Login_ID");
        Transform loginButton = canvas.Find("LoginRow/Login_Button");

        WireField(so, "_pressAnyKeyPanel", pressAnyKey != null ? pressAnyKey.gameObject : null);
        WirePressAnyKeyText(so, canvas);

        WireField(so, "_loginPanel", loginRow != null ? loginRow.gameObject : null);
        WireField(so, "_nicknameInput", loginId != null ? loginId.GetComponent<TMP_InputField>() : null);
        WireField(so, "_loginButton", loginButton != null ? loginButton.GetComponent<Button>() : null);
        WireField(so, "_loginMessageText", loginMessage != null ? loginMessage.GetComponent<TMP_Text>() : null);

        if (lobbyPanel != null)
        {
            Transform window = lobbyPanel.transform.Find("Lobby_Window");

            WireField(so, "_lobbyPanel", lobbyPanel);
            WireField(so, "_roomList", lobbyPanel.GetComponent<RoomListUI>());

            if (window != null)
            {
                Transform buttonRow = window.Find("Lobby_ButtonRow");

                WireField(so, "_joinSelectedButton", FindButton(buttonRow, "JoinSelectedButton"));
                WireField(so, "_createRoomButton", FindButton(buttonRow, "CreateRoomButton"));
                WireField(so, "_joinRandomButton", FindButton(buttonRow, "JoinRandomButton"));
                WireField(so, "_rejoinButton", FindButton(buttonRow, "RejoinButton"));
                WireField(so, "_backButton", FindButton(buttonRow, "BackButton"));
                WireField(so, "_quitButton", FindButton(buttonRow, "QuitButton"));

                Transform message = window.Find("Lobby_MessageText");
                WireField(so, "_lobbyMessageText", message != null ? message.GetComponent<TMP_Text>() : null);
            }

            WireRoomListUI(lobbyPanel);
        }

        WireField(so, "_modalDialog", modal);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(titleUI);
    }

    /// <summary>
    /// "Press any Button" 텍스트에 이미 <see cref="TextGlowPulse"/>가 붙어 있으면 _pressAnyKeyText는
    /// 비워둔다 — 둘 다 켜면 알파 깜빡임과 발광 맥동이 겹쳐 지저분해진다. 연출이 없을 때만 연결해
    /// TitleScreenUI의 기본 깜빡임이 대신 돌게 한다.
    /// </summary>
    private static void WirePressAnyKeyText(SerializedObject so, Transform canvas)
    {
        Transform text = canvas.Find("PressAnyKeyText/PressAnyKey_Text");
        if (text == null)
        {
            Debug.LogWarning("[TitleScreenLayoutTool] PressAnyKeyText/PressAnyKey_Text를 찾지 못했습니다 — _pressAnyKeyText는 기존 값 유지.");
            return;
        }

        if (text.GetComponent<TextGlowPulse>() != null)
        {
            Debug.Log("[TitleScreenLayoutTool] PressAnyKey_Text에 TextGlowPulse가 있어 _pressAnyKeyText는 비워둡니다 " +
                      "(깜빡임 연출이 겹치지 않도록).");
            return;
        }

        WireField(so, "_pressAnyKeyText", text.GetComponent<Graphic>());
    }

    private static void WireRoomListUI(GameObject lobbyPanel)
    {
        var roomList = lobbyPanel.GetComponent<RoomListUI>();
        if (roomList == null) return;

        Transform window = lobbyPanel.transform.Find("Lobby_Window");
        if (window == null) return;

        Transform content = window.Find("RoomList_ScrollView/Viewport/Content");
        Transform template = content != null ? content.Find("RoomEntry_Template") : null;
        Transform countText = window.Find("RoomList_CountText");
        Transform statusText = window.Find("RoomList_StatusText");
        Transform refreshButton = window.Find("RefreshButton");

        Undo.RecordObject(roomList, UNDO_NAME);
        var so = new SerializedObject(roomList);

        WireField(so, "_content", content as RectTransform);
        WireField(so, "_entryTemplate", template != null ? template.GetComponent<RoomEntryUI>() : null);
        WireField(so, "_toggleGroup", content != null ? content.GetComponent<ToggleGroup>() : null);
        WireField(so, "_countText", countText != null ? countText.GetComponent<TMP_Text>() : null);
        WireField(so, "_statusText", statusText != null ? statusText.GetComponent<TMP_Text>() : null);
        WireField(so, "_refreshButton", refreshButton != null ? refreshButton.GetComponent<Button>() : null);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(roomList);
    }

    private static Button FindButton(Transform parent, string name)
    {
        if (parent == null) return null;

        Transform child = parent.Find(name);
        return child != null ? child.GetComponent<Button>() : null;
    }

    /// <summary>
    /// 이미 올바른 값이 연결돼 있으면 건드리지 않는다. target이 null이면 경고만 남기고 기존 값은 그대로 둔다
    /// (배선이 덜 된 상태에서 멀쩡한 참조를 지우지 않기 위해).
    /// </summary>
    private static void WireField(SerializedObject so, string fieldName, Object target)
    {
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogWarning($"[TitleScreenLayoutTool] 필드를 찾지 못했습니다: {fieldName}");
            return;
        }

        if (target == null)
        {
            Debug.LogWarning($"[TitleScreenLayoutTool] {fieldName}에 연결할 대상을 찾지 못했습니다 — 기존 값 유지.");
            return;
        }

        if (prop.objectReferenceValue == target) return;

        prop.objectReferenceValue = target;
    }

    // ─────────────────────────────────────────
    // 공용
    // ─────────────────────────────────────────

    private static void SetActiveWithUndo(GameObject go, bool active)
    {
        if (go == null || go.activeSelf == active) return;

        Undo.RecordObject(go, UNDO_NAME);
        go.SetActive(active);
        EditorUtility.SetDirty(go);
    }

    /// <summary>root 아래 모든 TMP_Text(비활성 포함)의 폰트만 교체한다. 머티리얼은 건드리지 않는다.</summary>
    private static void ApplyFontToAllTMP(Transform root, TMP_FontAsset font)
    {
        if (font == null) return;

        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            ApplyFont(text, font);
    }

    /// <summary>
    /// 폰트 에셋만 바꾼다. 머티리얼(m_fontMaterial/m_fontSharedMaterial)은 건드리지 않으므로
    /// 발광 프리셋 같은 기존 설정이 날아가지 않는다.
    /// </summary>
    private static void ApplyFont(TMP_Text text, TMP_FontAsset font)
    {
        if (text == null || font == null || text.font == font) return;

        var so = new SerializedObject(text);
        SerializedProperty fontProp = so.FindProperty("m_fontAsset");
        if (fontProp == null) return;

        Undo.RecordObject(text, UNDO_NAME);
        fontProp.objectReferenceValue = font;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(text);
    }
}
