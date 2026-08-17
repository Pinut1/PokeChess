using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 견본덱 창(uGUI) 계층을 Canvas 아래에 만들고 <see cref="SampleDeckPanelUI"/>의 인스펙터 참조를
/// 자동으로 연결하는 에디터 전용 도구. 예전 IMGUI <c>GUI.Window</c> 구현을 대체하는 계층이다.
///
/// 구성은 두 페이지다 — 1페이지 덱 목록, 2페이지 덱 상세(왼쪽 활성 시너지 / 오른쪽 앞·뒷라인 배치도).
///
/// <see cref="TitleScreenLayoutTool"/>과 같은 방식으로, <b>없는 오브젝트만 만들고</b> 이미 있으면
/// 배치 값만 다시 덮어쓴다. 여러 번 실행해도 계층이 불어나지 않는다(단, 이 도구가 만든 오브젝트의
/// 위치·크기는 실행할 때마다 아래 상수로 되돌아간다 — 아트 조정이 끝나면 상수를 같이 고칠 것).
///
/// <b>단, 반복해서 찍어내는 두 템플릿은 예외로 프리팹이다</b> — 덱 목록 줄(<c>SampleDeckRow_Pf</c>)과
/// 배치도 유닛 칸(<c>SampleDeckUnitCard_Pf</c>). 처음 실행할 때 코드로 한 벌 만들어 프리팹으로
/// 저장하고, 그 뒤로는 <b>내용을 일절 건드리지 않는다</b>(<see cref="EnsurePrefabTemplate"/>).
/// 프리팹 모드에서 화면을 보며 고치면 모든 줄에 한 번에 반영된다.
/// 처음부터 다시 뽑고 싶으면 프리팹 파일을 지우고 메뉴를 다시 돌리면 된다.
///
/// <b>게임 안 프리팹을 그대로 물려 쓴다</b> — 유닛 칸은 <c>UnitSlot_Pf</c>(코스트별 테두리),
/// 시너지 행은 <c>SynergyRow_Pf</c>(등급 프레임·문턱 표기). 견본덱만 다른 규칙으로 보이지 않게 하려는 것이라
/// 프리팹을 못 찾으면 그 부분은 비워두고 경고만 남긴다.
///
/// 열기 버튼은 씬에 이미 있는 <c>ExDeck_Button</c>을 찾아 물린다. 클릭 연결은 씬의 OnClick 목록이
/// 아니라 <see cref="SampleDeckPanelUI"/>가 Awake에서 코드로 건다 — 옵션창(설정 버튼)과 같은 방식이라
/// 씬 병합 시 충돌할 여지가 적다.
///
/// ⚠️ <b>그 버튼은 Y축 180° 회전된 반전 그래픽이라 기본 설정으로는 클릭이 아예 안 들어온다.</b>
/// GraphicRaycaster의 <c>Ignore Reversed Graphics</c>(기본 켜짐)가 뒤집힌 그래픽을 레이캐스트
/// 대상에서 빼기 때문이다. <see cref="EnsureOpenButtonClickable"/>이 이걸 감지해 꺼준다 —
/// 배선을 아무리 확인해도 안 눌리면 이 옵션부터 볼 것.
///
/// 실행: 메뉴 <b>PokeChess/UI/Create Sample Deck Panel</b> (GameSceneTest를 연 상태에서)
/// </summary>
public static class SampleDeckPanelLayoutTool
{
    private const string UNDO_NAME = "Create Sample Deck Panel";

    private const string PANEL_NAME = "SampleDeckPanel";
    private const string OPEN_BUTTON_NAME = "ExDeck_Button";

    private const string UNIT_SLOT_PREFAB_PATH = "Assets/Art/UI/Ui_Prefabs/UnitSlot_Pf.prefab";
    private const string SYNERGY_ROW_PREFAB_PATH = "Assets/Art/UI/Ui_Prefabs/SynergyRow_Pf.prefab";

    // 이 도구가 처음 한 번 만들어 저장하는 프리팹. 저장된 뒤로는 내용을 건드리지 않는다.
    private const string DECK_ROW_PREFAB_PATH = "Assets/Art/UI/Ui_Prefabs/SampleDeckRow_Pf.prefab";
    private const string UNIT_CARD_PREFAB_PATH = "Assets/Art/UI/Ui_Prefabs/SampleDeckUnitCard_Pf.prefab";

    // 성급 뱃지. 인덱스 0 = 1성.
    private static readonly string[] STAR_SPRITE_PATHS =
    {
        "Assets/Art/UI/Info/panel_1star.png",
        "Assets/Art/UI/Info/panel_2star.png",
        "Assets/Art/UI/Info/panel_3star.png",
    };

    // ── 색상(옵션창/타이틀 도구와 같은 톤. 아트 확정 시 여기만 고치면 된다) ──
    private static readonly Color DIM_COLOR = new Color32(0, 0, 0, 170);
    private static readonly Color WINDOW_COLOR = new Color32(18, 18, 22, 245);
    private static readonly Color SECTION_COLOR = new Color32(10, 10, 13, 200);
    private static readonly Color ROW_COLOR = new Color32(32, 32, 40, 255);
    private static readonly Color BUTTON_COLOR = new Color32(52, 52, 62, 255);
    private static readonly Color ACCENT_BUTTON_COLOR = new Color32(46, 86, 132, 255);
    private static readonly Color DIVIDER_COLOR = new Color32(70, 70, 82, 255);
    private static readonly Color TEXT_COLOR = new Color32(235, 235, 240, 255);
    private static readonly Color SUBTEXT_COLOR = new Color32(170, 170, 180, 255);
    private static readonly Color ACCENT_COLOR = new Color32(255, 216, 120, 255);
    private static readonly Color TOOLTIP_COLOR = new Color32(14, 14, 18, 250);

    /// <summary>아이템 칸 배경. 아트가 스프라이트를 물리기 전까지의 임시 색이다.</summary>
    private static readonly Color ITEM_SLOT_BG_COLOR = new Color32(24, 24, 30, 235);

    /// <summary>아이템 칸 배경과 아이콘 사이 여백. 배경 테두리가 보이는 폭이 된다.</summary>
    private const float ITEM_SLOT_PADDING = 2f;

    // ── 창 ──
    private const float WINDOW_WIDTH = 1500f;
    private const float WINDOW_HEIGHT = 820f;

    private const float OUTER_MARGIN = 24f;
    private const float HEADER_HEIGHT = 96f;

    private const float CONTENT_WIDTH = WINDOW_WIDTH - OUTER_MARGIN * 2f;
    private const float CONTENT_HEIGHT = WINDOW_HEIGHT - HEADER_HEIGHT - OUTER_MARGIN;

    // ── 1페이지: 덱 목록 줄 ──
    // ⚠️ 아래 LIST_* / UNIT_* 값은 프리팹을 <b>처음 만들 때만</b> 쓰인다. 한 번 저장된 뒤로는
    //    프리팹이 배치의 주인이라 여기를 고쳐도 반영되지 않는다 — 프리팹을 열어 고칠 것.
    //    (처음부터 다시 뽑고 싶으면 프리팹 파일을 지우고 메뉴를 다시 돌린다)
    private const float LIST_ROW_HEIGHT = 104f;
    private const float LIST_SYNERGY_ICON = 32f;
    private const float LIST_UNIT_SLOT = 52f;
    private const float LIST_ITEM_ICON_SIZE = 15f;

    /// <summary>목록 유닛 칸의 전체 높이 — 아이콘 + 아이템 줄.</summary>
    private const float LIST_UNIT_CARD_HEIGHT = LIST_UNIT_SLOT + LIST_ITEM_ICON_SIZE + 6f;

    // 줄 안에서의 칸 경계(줄 왼쪽 끝 기준). 머리글이 없는 표라 줄마다 같은 값을 쓰면 세로로 맞는다.
    private const float LIST_NAME_LEFT = 20f;
    private const float LIST_NAME_WIDTH = 260f;
    private const float LIST_SYNERGY_LEFT = 292f;

    // 시너지가 가장 많은 덱이 7개다(32px 뱃지 7개 + 간격 4px 6칸 = 248).
    private const float LIST_SYNERGY_WIDTH = 250f;

    private const float LIST_UNITS_LEFT = 556f;
    private const float LIST_UNITS_WIDTH = 600f;
    private const float LIST_GOLD_RIGHT = 190f;
    private const float LIST_BUTTON_RIGHT = 20f;

    // ── 2페이지: 상세 ──
    private const float DETAIL_HEADER_HEIGHT = 56f;
    private const float SYNERGY_COLUMN_WIDTH = 330f;
    private const float COLUMN_GAP = 16f;

    // SynergyRow_Pf의 원래 크기. 프리팹 내부가 전부 고정 크기·절대 위치라 rect를 키워도
    // 내용은 안 커진다 — 그래서 크기 조절은 localScale로 한다(아래 SYNERGY_ROW_SCALE).
    private const float SYNERGY_ROW_WIDTH = 200f;
    private const float SYNERGY_ROW_HEIGHT = 60f;

    /// <summary>
    /// 상세 페이지 시너지 행의 확대 배율. <b>공용 프리팹은 건드리지 않고</b> 이 화면의 인스턴스만
    /// 키운다 — 전투 화면 시너지 패널은 원래 크기 그대로 둬야 하기 때문이다.
    ///
    /// 레이아웃은 스케일을 모르므로(항상 rect 크기로 자리를 잡는다) 늘어난 만큼을
    /// VerticalLayoutGroup의 spacing으로 보정한다. 행 높이가 전부 같아서 성립하는 계산이다.
    /// </summary>
    private const float SYNERGY_ROW_SCALE = 1.25f;

    /// <summary>확대와 무관하게 행 사이에 남길 실제 간격.</summary>
    private const float SYNERGY_ROW_GAP = 6f;
    private const float UNIT_CARD_HEIGHT = 150f;
    private const float UNIT_ICON_SIZE = 76f;

    // 배치도 두 줄의 세로 위치(배치 칸 위쪽 기준, 아래로 갈수록 음수). 라벨이 이 값에,
    // 유닛 줄이 30px 아래에 놓인다. 두 줄 간격을 벌리려면 BACK_LINE_TOP만 내리면 된다.
    private const float FRONT_LINE_TOP = -60f;
    private const float BACK_LINE_TOP = -270f;
    private const float STAR_BADGE_WIDTH = 54f;
    private const float STAR_BADGE_HEIGHT = 18f;
    private const float ITEM_ICON_SIZE = 26f;

    /// <summary>유닛 설명창의 시너지 아이콘 크기. 줄 높이도 이 값을 따라간다.</summary>
    private const float TOOLTIP_SYNERGY_ICON = 22f;

    [MenuItem("PokeChess/UI/Create Sample Deck Panel")]
    private static void CreateLayout()
    {
        GameObject canvasGO = FindCanvas();
        if (canvasGO == null)
        {
            Debug.LogError("[SampleDeckPanelLayoutTool] 열려 있는 씬에서 Canvas를 찾지 못했습니다. " +
                           "GameSceneTest 씬을 연 뒤 다시 실행해 주세요.");
            return;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_NAME);

        Transform canvas = canvasGO.transform;

        var panelUI = EnsureComponent<SampleDeckPanelUI>(canvasGO);
        RectTransform panel = BuildPanel(canvas);

        ApplyFontToAllTMP(panel, FindKoreanFontAsset());

        Button openButton = FindOpenButton(canvas);

        WirePanelUI(panelUI, panel, openButton);
        EnsureOpenButtonClickable(canvas, openButton);

        SetActiveWithUndo(panel.gameObject, false);

        EditorSceneManager.MarkSceneDirty(canvasGO.scene);
        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"[SampleDeckPanelLayoutTool] '{PANEL_NAME}' 생성/배선 완료 (덱 목록 + 덱 상세 2페이지).");
    }

    // ─────────────────────────────────────────
    // 계층 생성
    // ─────────────────────────────────────────

    private static RectTransform BuildPanel(Transform canvas)
    {
        RectTransform panel = EnsureChild(canvas, PANEL_NAME);
        SetStretch(panel, 0f, 0f, 0f, 0f);

        // 창 뒤를 클릭해도 보드로 새지 않게 Raycast Target을 켠 전체화면 딤을 깐다.
        EnsureImage(panel, DIM_COLOR, true);

        RectTransform window = EnsureChild(panel, "Window");
        SetRect(window, Center, Center, Center, Vector2.zero, new Vector2(WINDOW_WIDTH, WINDOW_HEIGHT));
        EnsureImage(window, WINDOW_COLOR, true);

        RemoveLegacyObjects(window);

        BuildHeader(window);
        BuildListPage(window);
        BuildDetailPage(window);

        // 설명창은 두 페이지 바깥(창 직계)에 둔다 — 페이지를 꺼도 같이 꺼지면 안 되고,
        // 무엇보다 맨 앞에 떠야 한다.
        BuildUnitTooltip(panel);

        return panel;
    }

    /// <summary>
    /// 1차 버전(3단 한 페이지) 계층의 잔재를 지운다. 그때 쓰던 <c>SampleDeckRowUI</c>가 없어져
    /// 그대로 두면 "Missing script"가 붙은 오브젝트가 창 안에 남고, 새 페이지와 겹쳐 그려진다.
    ///
    /// <b>이름을 하나하나 적어둔 목록만</b> 지운다 — "내가 아는 것 외에는 다 지우기"로 만들면
    /// 나중에 아트가 창에 얹은 장식까지 날아간다.
    /// </summary>
    private static void RemoveLegacyObjects(RectTransform window)
    {
        string[] legacyNames =
        {
            "Content",        // DeckList_Panel / DeckDetail_Panel / UnitDetail_Panel / SynergyDetail_Panel을 담던 칸
            "EmptyNotice",    // 목록 페이지 안으로 옮겨졌다
            "DeckCountText",  // 목록에 유닛 아이콘이 보이므로 없앴다
            "RefreshButton",  // 창을 열 때마다 자동 갱신이라 버튼이 필요 없어졌다
        };

        foreach (string name in legacyNames)
        {
            Transform legacy = window.Find(name);
            if (legacy == null) continue;

            Debug.Log($"[SampleDeckPanelLayoutTool] 이전 버전 오브젝트 '{name}'를 제거했습니다.");
            Undo.DestroyObjectImmediate(legacy.gameObject);
        }
    }

    private static void BuildHeader(RectTransform window)
    {
        RectTransform title = EnsureChild(window, "TitleText");
        SetRect(title, TopLeft, TopLeft, TopLeft, new Vector2(OUTER_MARGIN, -20f), new Vector2(360f, 40f));
        EnsureText(title, "견본덱", 30f, TEXT_COLOR, TextAlignmentOptions.TopLeft);

        RectTransform subtitle = EnsureChild(window, "SubtitleText");
        SetRect(subtitle, TopLeft, TopLeft, TopLeft, new Vector2(OUTER_MARGIN, -60f), new Vector2(820f, 26f));
        EnsureText(subtitle, "덱을 고르고 [공략 더 보기]를 누르면 배치와 시너지를 볼 수 있습니다.",
            16f, SUBTEXT_COLOR, TextAlignmentOptions.TopLeft);

        Button close = EnsureButton(window, "CloseButton", "닫기", BUTTON_COLOR);
        SetRect((RectTransform)close.transform, TopRight, TopRight, TopRight,
            new Vector2(-OUTER_MARGIN, -20f), new Vector2(88f, 34f));
    }

    // ── 1페이지: 덱 목록 ──

    private static void BuildListPage(RectTransform window)
    {
        RectTransform page = EnsureChild(window, "ListPage");
        SetStretch(page, OUTER_MARGIN, OUTER_MARGIN, HEADER_HEIGHT, OUTER_MARGIN);

        RectTransform content = EnsureScrollView(page, "Scroll", 0f, 0f, 0f, 0f);
        EnsureVertical(content, 10f, 0f);

        RectTransform empty = EnsureChild(page, "EmptyNotice");
        SetTopStretch(empty, 0f, -8f, 30f);
        EnsureText(empty, "견본덱 데이터를 불러오지 못했습니다. DeckDatabase Import 상태를 확인하세요.",
            17f, ACCENT_COLOR, TextAlignmentOptions.TopLeft);

        EnsurePrefabTemplate(content, "DeckRow_Template", DECK_ROW_PREFAB_PATH, BuildListRowTemplate);
    }

    /// <summary>
    /// 덱 목록 줄을 코드로 한 벌 만든다. <b>프리팹이 없을 때만</b> 불린다 —
    /// 시너지 뱃지·유닛 칸 템플릿도 이 줄의 자식이라 같이 프리팹에 담긴다.
    /// </summary>
    private static SampleDeckListRowUI BuildListRowTemplate(RectTransform content)
    {
        RectTransform row = EnsureChild(content, "DeckRow_Template");
        SetTopStretch(row, 0f, 0f, LIST_ROW_HEIGHT);
        EnsureImage(row, ROW_COLOR, true);
        EnsureLayoutElement(row, LIST_ROW_HEIGHT);

        RectTransform name = EnsureChild(row, "DeckNameText");
        SetRect(name, LeftMiddle, LeftMiddle, LeftMiddle,
            new Vector2(LIST_NAME_LEFT, 0f), new Vector2(LIST_NAME_WIDTH, 60f));
        TMP_Text nameText = EnsureText(name, "덱 이름", 19f, TEXT_COLOR, TextAlignmentOptions.Left);
        nameText.textWrappingMode = TextWrappingModes.Normal;

        RectTransform synergyArea = EnsureChild(row, "SynergyBadges");
        SetRect(synergyArea, LeftMiddle, LeftMiddle, LeftMiddle,
            new Vector2(LIST_SYNERGY_LEFT, 0f), new Vector2(LIST_SYNERGY_WIDTH, LIST_SYNERGY_ICON + 4f));
        EnsureHorizontal(synergyArea, 4f, TextAnchor.MiddleLeft);

        SampleDeckSynergyBadgeUI synergyBadge = BuildSynergyBadgeTemplate(synergyArea);

        RectTransform unitArea = EnsureChild(row, "UnitCards");
        SetRect(unitArea, LeftMiddle, LeftMiddle, LeftMiddle,
            new Vector2(LIST_UNITS_LEFT, 0f), new Vector2(LIST_UNITS_WIDTH, LIST_UNIT_CARD_HEIGHT));
        EnsureHorizontal(unitArea, 6f, TextAnchor.MiddleLeft);

        // 목록 칸은 아이콘 + 아이템만. 성급·이름은 자리가 없어 배치도에서만 보여준다.
        SampleDeckUnitCardUI unitCard = BuildUnitCardTemplate(
            unitArea,
            iconSize: LIST_UNIT_SLOT,
            itemIconSize: LIST_ITEM_ICON_SIZE,
            withStarAndName: false);

        RectTransform gold = EnsureChild(row, "GoldText");
        SetRect(gold, RightMiddle, RightMiddle, RightMiddle,
            new Vector2(-LIST_GOLD_RIGHT, 0f), new Vector2(90f, 34f));
        EnsureText(gold, "0", 20f, ACCENT_COLOR, TextAlignmentOptions.Right);

        Button detail = EnsureButton(row, "DetailButton", "공략 더 보기", ACCENT_BUTTON_COLOR);
        SetRect((RectTransform)detail.transform, RightMiddle, RightMiddle, RightMiddle,
            new Vector2(-LIST_BUTTON_RIGHT, 0f), new Vector2(140f, 40f));

        var rowUI = EnsureComponent<SampleDeckListRowUI>(row.gameObject);
        var so = new SerializedObject(rowUI);

        WireField(so, "_deckNameText", nameText);
        WireField(so, "_goldText", gold.GetComponent<TMP_Text>());
        WireField(so, "_synergyBadgeArea", synergyArea);
        WireField(so, "_synergyBadgeTemplate", synergyBadge);
        WireField(so, "_unitCardArea", unitArea);
        WireField(so, "_unitCardTemplate", unitCard);
        WireField(so, "_detailButton", detail);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(rowUI);

        SetActiveWithUndo(row.gameObject, false);
        return rowUI;
    }

    // ── 2페이지: 덱 상세 ──

    private static void BuildDetailPage(RectTransform window)
    {
        RectTransform page = EnsureChild(window, "DetailPage");
        SetStretch(page, OUTER_MARGIN, OUTER_MARGIN, HEADER_HEIGHT, OUTER_MARGIN);

        Button back = EnsureButton(page, "BackButton", "← 목록", BUTTON_COLOR);
        SetRect((RectTransform)back.transform, TopLeft, TopLeft, TopLeft,
            new Vector2(0f, 0f), new Vector2(110f, 36f));

        RectTransform deckName = EnsureChild(page, "DeckNameText");
        SetRect(deckName, TopLeft, TopLeft, TopLeft, new Vector2(126f, -2f), new Vector2(520f, 36f));
        EnsureText(deckName, "덱 이름", 24f, TEXT_COLOR, TextAlignmentOptions.TopLeft);

        RectTransform level = EnsureChild(page, "LevelText");
        SetRect(level, TopRight, TopRight, TopRight, new Vector2(-120f, -4f), new Vector2(220f, 32f));
        EnsureText(level, "완성 기준 Lv.0", 18f, SUBTEXT_COLOR, TextAlignmentOptions.TopRight);

        RectTransform gold = EnsureChild(page, "GoldText");
        SetRect(gold, TopRight, TopRight, TopRight, new Vector2(0f, -4f), new Vector2(100f, 32f));
        EnsureText(gold, "0", 20f, ACCENT_COLOR, TextAlignmentOptions.TopRight);

        BuildSynergyColumn(page);
        BuildBoardColumn(page);
    }

    private static void BuildSynergyColumn(RectTransform page)
    {
        // 위는 상세 머리말 아래에서 시작하고 아래는 페이지 바닥까지 늘어난다.
        RectTransform column = EnsureChild(page, "SynergyColumn");
        SetColumn(column, left: 0f, width: SYNERGY_COLUMN_WIDTH);
        EnsureImage(column, SECTION_COLOR, true);

        RectTransform header = EnsureChild(column, "HeaderText");
        SetRect(header, TopLeft, TopLeft, TopLeft, new Vector2(16f, -12f), new Vector2(220f, 26f));
        EnsureText(header, "활성 시너지", 18f, TEXT_COLOR, TextAlignmentOptions.TopLeft);

        EnsureDivider(column, "Divider", -44f, 10f);

        RectTransform content = EnsureScrollView(column, "Scroll", 10f, 10f, 52f, 10f);

        // 행은 확대된 채로 놓이므로 레이아웃이 크기를 건드리면 안 된다(controlSize=false).
        // 대신 늘어난 높이만큼 spacing으로 자리를 벌린다.
        EnsureVertical(
            content,
            spacing: SYNERGY_ROW_HEIGHT * (SYNERGY_ROW_SCALE - 1f) + SYNERGY_ROW_GAP,
            padding: 0f,
            controlChildSize: false);

        EnsureSynergyRowTemplate(content);
    }

    private static void BuildBoardColumn(RectTransform page)
    {
        // 오른쪽 끝까지 늘어나는 칸(width=0 → 오른쪽 가장자리에 붙는다).
        RectTransform column = EnsureChild(page, "BoardColumn");
        SetColumn(column, left: SYNERGY_COLUMN_WIDTH + COLUMN_GAP, width: 0f);
        EnsureImage(column, SECTION_COLOR, true);

        RectTransform header = EnsureChild(column, "HeaderText");
        SetRect(header, TopLeft, TopLeft, TopLeft, new Vector2(18f, -12f), new Vector2(300f, 26f));
        EnsureText(header, "배치", 18f, TEXT_COLOR, TextAlignmentOptions.TopLeft);

        EnsureDivider(column, "Divider", -44f, 10f);

        // 앞/뒷라인 두 줄. 실제 좌표(q,r)는 견본덱 데이터에 없으므로 줄 구분까지만 한다.
        RectTransform front = EnsureLine(column, "Front", "앞라인", FRONT_LINE_TOP);
        EnsureLine(column, "Back", "뒷라인", BACK_LINE_TOP);

        // 카드 템플릿은 앞라인 아래 하나만 둔다 — 뒷라인은 복제할 때 부모만 바꿔 쓴다.
        EnsurePrefabTemplate(
            front, "UnitCard_Template", UNIT_CARD_PREFAB_PATH,
            parent => BuildUnitCardTemplate(
                parent,
                iconSize: UNIT_ICON_SIZE,
                itemIconSize: ITEM_ICON_SIZE,
                withStarAndName: true));
    }

    /// <summary>"라벨 + 유닛이 깔릴 가로 줄" 한 벌. 반환값은 유닛이 담기는 줄이다.</summary>
    private static RectTransform EnsureLine(
        RectTransform parent, string name, string label, float top)
    {
        RectTransform labelRect = EnsureChild(parent, name + "Label");
        SetTopStretch(labelRect, -36f, top, 24f);
        EnsureText(labelRect, label, 15f, SUBTEXT_COLOR, TextAlignmentOptions.TopLeft);

        RectTransform area = EnsureChild(parent, name + "Line");
        SetTopStretch(area, -36f, top - 30f, UNIT_CARD_HEIGHT + 20f);
        EnsureHorizontal(area, 10f, TextAnchor.UpperCenter);

        return area;
    }

    // ─────────────────────────────────────────
    // 템플릿
    // ─────────────────────────────────────────

    /// <summary>
    /// 게임 안 유닛 칸(UnitSlot_Pf)을 깐다. 프리팹이 없으면 null.
    /// </summary>
    /// <param name="active">
    /// 목록 줄에서는 복제할 <b>템플릿</b>이라 꺼두고(false), 배치도 카드 안에서는 항상 보이는
    /// <b>본체</b>라 켜둔다(true).
    /// </param>
    private static SynergyTooltipUnitSlot EnsureUnitSlot(
        RectTransform parent, float size, bool active, string name)
    {
        Transform existing = parent.Find(name);
        GameObject instance;

        if (existing != null)
        {
            instance = existing.gameObject;
        }
        else
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UNIT_SLOT_PREFAB_PATH);
            if (prefab == null)
            {
                Debug.LogWarning($"[SampleDeckPanelLayoutTool] {UNIT_SLOT_PREFAB_PATH}를 찾지 못했습니다 — " +
                                 "유닛 칸은 인스펙터에서 직접 물려 주세요.");
                return null;
            }

            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            Undo.RegisterCreatedObjectUndo(instance, UNDO_NAME);
        }

        var rect = instance.transform as RectTransform;
        if (rect != null)
        {
            SetRect(rect, Center, Center, Center, Vector2.zero, new Vector2(size, size));
            EnsureLayoutElementSize(rect, size, size);
        }

        SetActiveWithUndo(instance, active);
        return instance.GetComponent<SynergyTooltipUnitSlot>();
    }

    /// <summary>
    /// 반복해서 찍어내는 템플릿을 <b>프리팹으로</b> 확보한다.
    ///
    /// <list type="number">
    /// <item>프리팹이 이미 있고 씬에도 그 인스턴스가 깔려 있으면 → <b>아무것도 하지 않는다.</b>
    ///       배치·색·스프라이트는 전부 프리팹이 정한다.</item>
    /// <item>프리팹은 있는데 씬 쪽이 그 인스턴스가 아니면(옛 버전 잔재) → 치우고 새로 깐다.</item>
    /// <item>프리팹이 아직 없으면 → <paramref name="buildOnce"/>로 한 벌 만들고 그대로 프리팹으로
    ///       저장한다. 이 경로는 사실상 최초 1회만 탄다.</item>
    /// </list>
    ///
    /// 이렇게 해두면 프리팹 하나만 고쳐도 모든 줄이 바뀌고, 프리팹 모드에서 화면을 보며 조정할 수 있다.
    /// 도구를 다시 돌려도 덮어쓰지 않는다 — 처음부터 다시 뽑고 싶으면 프리팹 파일을 지우면 된다.
    /// </summary>
    private static T EnsurePrefabTemplate<T>(
        RectTransform parent,
        string objectName,
        string prefabPath,
        System.Func<RectTransform, T> buildOnce) where T : Component
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Transform existing = parent.Find(objectName);

        if (asset != null)
        {
            if (IsInstanceOf(existing, prefabPath))
            {
                SetActiveWithUndo(existing.gameObject, false);
                return existing.GetComponent<T>();
            }

            // 프리팹과 무관한 옛 오브젝트는 치운다(이전 버전이 코드로 만들어둔 것).
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            instance.name = objectName;
            Undo.RegisterCreatedObjectUndo(instance, UNDO_NAME);
            SetActiveWithUndo(instance, false);

            return instance.GetComponent<T>();
        }

        // 프리팹이 아직 없다 — 코드로 한 벌 만들어 그대로 프리팹으로 굳힌다.
        T built = buildOnce(parent);
        if (built == null) return null;

        GameObject saved = PrefabUtility.SaveAsPrefabAssetAndConnect(
            built.gameObject, prefabPath, InteractionMode.AutomatedAction);

        if (saved == null)
        {
            Debug.LogWarning($"[SampleDeckPanelLayoutTool] '{objectName}'을 프리팹으로 저장하지 못했습니다: {prefabPath}");
            return built;
        }

        Debug.Log($"[SampleDeckPanelLayoutTool] '{objectName}'을 프리팹으로 저장했습니다 — {prefabPath}\n" +
                  "이제부터 이 프리팹을 열어 고치면 모든 줄에 반영되고, 메뉴를 다시 돌려도 덮어쓰지 않습니다.");

        // 반환은 씬 인스턴스 쪽이다(에셋 루트가 아니라). 이어지는 배선이 인스턴스를 가리켜야 한다.
        return built;
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
    /// 아이템 칸 템플릿 — <b>루트가 배경 패널이고 아이콘이 자식</b>이다.
    /// uGUI는 자식이 항상 부모 위에 그려지므로 아이콘 뒤에 판을 깔려면 이 순서여야 한다.
    ///
    /// 배경 스프라이트는 넣지 않고 색만 칠해 둔다 — 아트가 인스펙터에서 스프라이트를 물리면
    /// 이 도구는 그걸 건드리지 않는다(EnsureImage는 색과 Raycast Target만 손댄다).
    /// </summary>
    private static SampleDeckItemSlotUI BuildItemSlotTemplate(RectTransform parent, float size)
    {
        RectTransform slot = EnsureChild(parent, "ItemSlot_Template");
        SetRect(slot, Center, Center, Center, Vector2.zero, new Vector2(size, size));
        EnsureImage(slot, ITEM_SLOT_BG_COLOR, false);
        EnsureLayoutElementSize(slot, size, size);

        RectTransform icon = EnsureChild(slot, "Icon");
        SetStretch(icon, ITEM_SLOT_PADDING, ITEM_SLOT_PADDING, ITEM_SLOT_PADDING, ITEM_SLOT_PADDING);
        Image iconImage = EnsureImage(icon, Color.white, false);
        iconImage.preserveAspect = true;

        var slotUI = EnsureComponent<SampleDeckItemSlotUI>(slot.gameObject);
        var so = new SerializedObject(slotUI);

        WireField(so, "_icon", iconImage);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(slotUI);

        SetActiveWithUndo(slot.gameObject, false);
        return slotUI;
    }

    /// <summary>
    /// 목록 줄의 시너지 뱃지 템플릿 — 등급 프레임 + 심볼.
    /// 프레임 스프라이트·색은 <c>SynergyRow_Pf</c>의 <see cref="SynergyRowUI"/>에서 그대로 복사한다.
    /// 손으로 다시 물리면 시너지 패널과 목록이 서로 다른 프레임을 쓰게 되기 쉽다.
    /// </summary>
    private static SampleDeckSynergyBadgeUI BuildSynergyBadgeTemplate(RectTransform parent)
    {
        RectTransform badge = EnsureChild(parent, "SynergyBadge_Template");
        SetRect(badge, Center, Center, Center, Vector2.zero,
            new Vector2(LIST_SYNERGY_ICON, LIST_SYNERGY_ICON));
        EnsureLayoutElementSize(badge, LIST_SYNERGY_ICON, LIST_SYNERGY_ICON);

        RectTransform frame = EnsureChild(badge, "Frame");
        SetStretch(frame, 0f, 0f, 0f, 0f);
        EnsureImage(frame, Color.white, false).preserveAspect = true;

        // 심볼은 프레임 안쪽에 조금 들여 넣는다 — 꽉 채우면 육각 프레임 모서리를 넘는다.
        RectTransform symbol = EnsureChild(badge, "Symbol");
        SetStretch(symbol, 6f, 6f, 6f, 6f);
        EnsureImage(symbol, Color.black, false).preserveAspect = true;

        var badgeUI = EnsureComponent<SampleDeckSynergyBadgeUI>(badge.gameObject);
        var so = new SerializedObject(badgeUI);

        WireField(so, "_frame", frame.GetComponent<Image>());
        WireField(so, "_symbol", symbol.GetComponent<Image>());

        CopySynergyFrameStyle(so);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(badgeUI);

        SetActiveWithUndo(badge.gameObject, false);
        return badgeUI;
    }

    /// <summary>SynergyRow_Pf의 등급 프레임 스프라이트·색·심볼 색을 뱃지로 복사한다.</summary>
    private static void CopySynergyFrameStyle(SerializedObject badgeSo)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SYNERGY_ROW_PREFAB_PATH);
        var source = prefab != null ? prefab.GetComponent<SynergyRowUI>() : null;

        if (source == null)
        {
            Debug.LogWarning($"[SampleDeckPanelLayoutTool] {SYNERGY_ROW_PREFAB_PATH}에서 SynergyRowUI를 " +
                             "찾지 못해 등급 프레임을 복사하지 못했습니다 — 뱃지는 프레임 없이 심볼만 나옵니다.");
            return;
        }

        var sourceSo = new SerializedObject(source);

        string[] fields =
        {
            "_uniqueFrame", "_bronzeFrame", "_silverFrame", "_goldFrame", "_prismFrame",
        };

        foreach (string field in fields)
            CopyObjectField(sourceSo, badgeSo, field);

        string[] colorFields =
        {
            "_uniqueFrameColor", "_bronzeFrameColor", "_silverFrameColor",
            "_goldFrameColor", "_prismFrameColor",
        };

        foreach (string field in colorFields)
            CopyColorField(sourceSo, badgeSo, field);

        // 활성 심볼 색만 가져온다 — 비활성은 프레임 없이 흐리게 찍는 쪽이 목록에서 더 잘 구분된다.
        CopyColorField(sourceSo, badgeSo, "_activeSymbolColor");
    }

    private static void CopyObjectField(SerializedObject from, SerializedObject to, string field)
    {
        SerializedProperty src = from.FindProperty(field);
        SerializedProperty dst = to.FindProperty(field);

        if (src != null && dst != null) dst.objectReferenceValue = src.objectReferenceValue;
    }

    private static void CopyColorField(SerializedObject from, SerializedObject to, string field)
    {
        SerializedProperty src = from.FindProperty(field);
        SerializedProperty dst = to.FindProperty(field);

        if (src != null && dst != null) dst.colorValue = src.colorValue;
    }

    /// <summary>게임 안 시너지 행(SynergyRow_Pf)을 템플릿으로 깐다. 프리팹이 없으면 null.</summary>
    private static SynergyRowUI EnsureSynergyRowTemplate(RectTransform parent)
    {
        const string NAME = "SynergyRow_Template";

        Transform existing = parent.Find(NAME);
        GameObject instance;

        if (existing != null)
        {
            instance = existing.gameObject;
        }
        else
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SYNERGY_ROW_PREFAB_PATH);
            if (prefab == null)
            {
                Debug.LogWarning($"[SampleDeckPanelLayoutTool] {SYNERGY_ROW_PREFAB_PATH}를 찾지 못했습니다 — " +
                                 "시너지 행 템플릿은 인스펙터에서 직접 물려 주세요.");
                return null;
            }

            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = NAME;
            Undo.RegisterCreatedObjectUndo(instance, UNDO_NAME);
        }

        var rect = instance.transform as RectTransform;
        if (rect != null)
        {
            // 피벗을 좌상단으로 — 확대가 오른쪽·아래로만 자라야 칸 왼쪽으로 삐져나가지 않는다.
            SetRect(rect, TopLeft, TopLeft, TopLeft, Vector2.zero,
                new Vector2(SYNERGY_ROW_WIDTH, SYNERGY_ROW_HEIGHT));

            Undo.RecordObject(rect, UNDO_NAME);
            rect.localScale = Vector3.one * SYNERGY_ROW_SCALE;
            EditorUtility.SetDirty(rect);

            // LayoutElement가 남아 있으면 controlSize=false인 지금은 무시되지만,
            // 나중에 누가 controlSize를 켰을 때 크기가 두 번 먹혀 헷갈린다.
            RemoveComponent<LayoutElement>(rect.gameObject);
        }

        SetActiveWithUndo(instance, false);
        return instance.GetComponent<SynergyRowUI>();
    }

    /// <summary>
    /// 유닛 칸 템플릿 — 유닛 아이콘 + (선택)성급 뱃지·이름 + 아이템 줄.
    /// 목록(1페이지)과 배치도(2페이지)가 같은 컴포넌트를 쓰고 크기·구성만 다르다.
    /// </summary>
    /// <param name="withStarAndName">
    /// 배치도는 켠다. 목록은 칸이 좁아 끄고, 그러면 <see cref="SampleDeckUnitCardUI"/>가
    /// 미배선 참조를 알아서 건너뛰어 아이콘과 아이템만 나온다.
    /// </param>
    private static SampleDeckUnitCardUI BuildUnitCardTemplate(
        RectTransform parent, float iconSize, float itemIconSize, bool withStarAndName)
    {
        float starHeight = withStarAndName ? STAR_BADGE_HEIGHT + 4f : 0f;
        float nameHeight = withStarAndName ? 20f : 0f;

        float cardWidth = Mathf.Max(iconSize, itemIconSize * 3f + 6f);
        float cardHeight = starHeight + iconSize + nameHeight + itemIconSize + 4f;

        RectTransform card = EnsureChild(parent, "UnitCard_Template");
        SetRect(card, Center, Center, Center, Vector2.zero, new Vector2(cardWidth, cardHeight));
        EnsureLayoutElementSize(card, cardWidth, cardHeight);

        // 알파 0 + Raycast Target — 자식 아이콘 위 커서 이벤트를 이 루트가 받게 한다.
        EnsureImage(card, new Color(1f, 1f, 1f, 0f), true);

        Image starImage = null;
        TMP_Text nameText = null;

        if (withStarAndName)
        {
            RectTransform star = EnsureChild(card, "StarBadge");
            SetRect(star, TopCenter, TopCenter, TopCenter, Vector2.zero,
                new Vector2(STAR_BADGE_WIDTH, STAR_BADGE_HEIGHT));
            starImage = EnsureImage(star, Color.white, false);
            starImage.preserveAspect = true;
        }

        RectTransform slotHolder = EnsureChild(card, "SlotHolder");
        SetRect(slotHolder, TopCenter, TopCenter, TopCenter, new Vector2(0f, -starHeight),
            new Vector2(iconSize, iconSize));

        // 카드 안의 유닛 아이콘은 복제할 템플릿이 아니라 항상 보이는 본체다.
        SynergyTooltipUnitSlot slot = EnsureUnitSlot(slotHolder, iconSize, active: true, "UnitSlot");

        if (withStarAndName)
        {
            RectTransform name = EnsureChild(card, "NameText");
            SetRect(name, TopCenter, TopCenter, TopCenter,
                new Vector2(0f, -(starHeight + iconSize + 2f)),
                new Vector2(cardWidth, nameHeight));
            nameText = EnsureText(name, "이름", 13f, TEXT_COLOR, TextAlignmentOptions.Top);
        }

        RectTransform itemArea = EnsureChild(card, "ItemArea");
        SetRect(itemArea, TopCenter, TopCenter, TopCenter,
            new Vector2(0f, -(starHeight + iconSize + nameHeight + 2f)),
            new Vector2(cardWidth, itemIconSize));
        EnsureHorizontal(itemArea, 2f, TextAnchor.UpperCenter);

        SampleDeckItemSlotUI itemSlot = BuildItemSlotTemplate(itemArea, itemIconSize);

        var cardUI = EnsureComponent<SampleDeckUnitCardUI>(card.gameObject);
        var so = new SerializedObject(cardUI);

        WireField(so, "_slot", slot);
        WireField(so, "_itemArea", itemArea);
        WireField(so, "_itemSlotTemplate", itemSlot);

        if (withStarAndName)
        {
            WireField(so, "_starBadge", starImage);
            WireField(so, "_nameText", nameText);
            WireStarSprites(so);
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(cardUI);

        SetActiveWithUndo(card.gameObject, false);
        return cardUI;
    }

    private static void WireStarSprites(SerializedObject so)
    {
        SerializedProperty array = so.FindProperty("_starSprites");
        if (array == null) return;

        array.arraySize = STAR_SPRITE_PATHS.Length;

        for (int i = 0; i < STAR_SPRITE_PATHS.Length; i++)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(STAR_SPRITE_PATHS[i]);

            if (sprite == null)
                Debug.LogWarning($"[SampleDeckPanelLayoutTool] 성급 스프라이트를 찾지 못했습니다: {STAR_SPRITE_PATHS[i]}");

            array.GetArrayElementAtIndex(i).objectReferenceValue = sprite;
        }
    }

    // ── 유닛 설명창 ──

    private static void BuildUnitTooltip(RectTransform panel)
    {
        RectTransform holder = EnsureChild(panel, "UnitTooltip");
        SetStretch(holder, 0f, 0f, 0f, 0f);

        RectTransform root = EnsureChild(holder, "Window");
        SetRect(root, TopLeft, TopLeft, TopLeft, Vector2.zero, new Vector2(280f, 10f));
        EnsureImage(root, TOOLTIP_COLOR, false);

        EnsureVertical(root, 4f, 12f);

        var fitter = EnsureComponent<ContentSizeFitter>(root.gameObject);
        Undo.RecordObject(fitter, UNDO_NAME);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        EditorUtility.SetDirty(fitter);

        // 이름 줄 — 이름(왼쪽) + 구매 비용(오른쪽)
        RectTransform nameRow = EnsureChild(root, "NameRow");
        SetTopStretch(nameRow, 0f, 0f, 28f);
        EnsureLayoutElement(nameRow, 28f);

        RectTransform name = EnsureChild(nameRow, "NameText");
        SetStretch(name, 0f, 60f, 0f, 0f);
        EnsureText(name, "이름", 19f, TEXT_COLOR, TextAlignmentOptions.Left);

        RectTransform cost = EnsureChild(nameRow, "CostText");
        SetRect(cost, RightMiddle, RightMiddle, RightMiddle, Vector2.zero, new Vector2(56f, 24f));
        EnsureText(cost, "0", 18f, ACCENT_COLOR, TextAlignmentOptions.Right);

        RectTransform role = EnsureTooltipLine(root, "RoleText", "역할군", 15f, SUBTEXT_COLOR);

        RectTransform synergy1 = EnsureSynergyLine(root, "SynergyLine1", "시너지 1");
        RectTransform synergy2 = EnsureSynergyLine(root, "SynergyLine2", "시너지 2");

        RectTransform range = EnsureTooltipLine(root, "RangeText", "■□□□□□", 15f, SUBTEXT_COLOR);

        // 추천 아이템 줄
        RectTransform itemGroup = EnsureChild(root, "ItemGroup");
        SetTopStretch(itemGroup, 0f, 0f, ITEM_ICON_SIZE + 22f);
        EnsureLayoutElement(itemGroup, ITEM_ICON_SIZE + 22f);

        RectTransform itemLabel = EnsureChild(itemGroup, "Label");
        SetTopStretch(itemLabel, 0f, 0f, 18f);
        EnsureText(itemLabel, "추천 아이템", 13f, SUBTEXT_COLOR, TextAlignmentOptions.TopLeft);

        RectTransform itemArea = EnsureChild(itemGroup, "ItemArea");
        SetTopStretch(itemArea, 0f, -20f, ITEM_ICON_SIZE);
        EnsureHorizontal(itemArea, 4f, TextAnchor.UpperLeft);

        SampleDeckItemSlotUI itemSlot = BuildItemSlotTemplate(itemArea, ITEM_ICON_SIZE);

        var tooltipUI = EnsureComponent<SampleDeckUnitTooltipUI>(holder.gameObject);
        var so = new SerializedObject(tooltipUI);

        WireField(so, "_root", root);
        WireField(so, "_nameText", name.GetComponent<TMP_Text>());
        WireField(so, "_costText", cost.GetComponent<TMP_Text>());
        WireField(so, "_roleText", role.GetComponent<TMP_Text>());
        WireSynergyLines(so, synergy1, synergy2);
        WireField(so, "_rangeText", range.GetComponent<TMP_Text>());
        WireField(so, "_itemGroup", itemGroup.gameObject);
        WireField(so, "_itemArea", itemArea);
        WireField(so, "_itemSlotTemplate", itemSlot);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(tooltipUI);

        SetActiveWithUndo(root.gameObject, false);
    }

    /// <summary>툴팁의 시너지 줄 — 왼쪽에 아이콘, 오른쪽에 이름.</summary>
    private static RectTransform EnsureSynergyLine(
        RectTransform parent, string name, string placeholder)
    {
        RectTransform line = EnsureChild(parent, name);
        SetTopStretch(line, 0f, 0f, TOOLTIP_SYNERGY_ICON);
        EnsureLayoutElement(line, TOOLTIP_SYNERGY_ICON);

        RectTransform icon = EnsureChild(line, "Icon");
        SetRect(icon, LeftMiddle, LeftMiddle, LeftMiddle, Vector2.zero,
            new Vector2(TOOLTIP_SYNERGY_ICON, TOOLTIP_SYNERGY_ICON));
        EnsureImage(icon, TEXT_COLOR, false).preserveAspect = true;

        RectTransform label = EnsureChild(line, "Label");
        SetStretch(label, TOOLTIP_SYNERGY_ICON + 6f, 0f, 0f, 0f);
        EnsureText(label, placeholder, 15f, TEXT_COLOR, TextAlignmentOptions.Left);

        return line;
    }

    /// <summary>
    /// <see cref="SampleDeckUnitTooltipUI"/>의 _synergyLines 배열(직렬화된 구조체 2칸)을 채운다.
    /// 구조체 배열이라 WireField로는 못 넣고 SerializedProperty를 직접 파고들어야 한다.
    /// </summary>
    private static void WireSynergyLines(SerializedObject so, params RectTransform[] lines)
    {
        SerializedProperty array = so.FindProperty("_synergyLines");
        if (array == null)
        {
            Debug.LogWarning("[SampleDeckPanelLayoutTool] _synergyLines 필드를 찾지 못했습니다 — 시너지 줄 배선 생략.");
            return;
        }

        array.arraySize = lines.Length;

        for (int i = 0; i < lines.Length; i++)
        {
            SerializedProperty element = array.GetArrayElementAtIndex(i);
            RectTransform line = lines[i];

            element.FindPropertyRelative("root").objectReferenceValue = line.gameObject;
            element.FindPropertyRelative("icon").objectReferenceValue =
                line.Find("Icon").GetComponent<Image>();
            element.FindPropertyRelative("label").objectReferenceValue =
                line.Find("Label").GetComponent<TMP_Text>();
        }
    }

    private static RectTransform EnsureTooltipLine(
        RectTransform parent, string name, string text, float size, Color color)
    {
        RectTransform line = EnsureChild(parent, name);
        SetTopStretch(line, 0f, 0f, 22f);
        EnsureText(line, text, size, color, TextAlignmentOptions.Left);
        EnsureLayoutElement(line, 22f);
        return line;
    }

    // ─────────────────────────────────────────
    // 자동 연결
    // ─────────────────────────────────────────

    private static void WirePanelUI(SampleDeckPanelUI panelUI, RectTransform panel, Button openButton)
    {
        Undo.RecordObject(panelUI, UNDO_NAME);
        var so = new SerializedObject(panelUI);

        Transform window = panel.Find("Window");
        Transform listPage = window.Find("ListPage");
        Transform detailPage = window.Find("DetailPage");

        WireField(so, "_panelRoot", panel.gameObject);
        WireField(so, "_openButton", openButton);
        WireField(so, "_closeButton", FindComponent<Button>(window, "CloseButton"));

        // 머리말 문구는 페이지에 따라 런타임에 갈아끼운다 — 씬에 적힌 건 디자인용 견본이다.
        WireField(so, "_subtitleText", FindComponent<TMP_Text>(window, "SubtitleText"));

        // 1페이지
        Transform listContent = listPage.Find("Scroll/Viewport/Content");

        WireField(so, "_listPage", listPage.gameObject);
        WireField(so, "_listContent", listContent as RectTransform);
        WireField(so, "_listRowTemplate", FindComponent<SampleDeckListRowUI>(listContent, "DeckRow_Template"));
        WireField(so, "_listEmptyNotice", FindGameObject(listPage, "EmptyNotice"));

        // 2페이지
        Transform synergyColumn = detailPage.Find("SynergyColumn");
        Transform synergyContent = synergyColumn.Find("Scroll/Viewport/Content");
        Transform boardColumn = detailPage.Find("BoardColumn");
        Transform frontLine = boardColumn.Find("FrontLine");

        WireField(so, "_detailPage", detailPage.gameObject);
        WireField(so, "_backButton", FindComponent<Button>(detailPage, "BackButton"));
        WireField(so, "_detailDeckNameText", FindComponent<TMP_Text>(detailPage, "DeckNameText"));
        WireField(so, "_detailLevelText", FindComponent<TMP_Text>(detailPage, "LevelText"));
        WireField(so, "_detailGoldText", FindComponent<TMP_Text>(detailPage, "GoldText"));

        WireField(so, "_synergyRowArea", synergyContent as RectTransform);
        WireField(so, "_synergyRowTemplate",
            FindComponent<SynergyRowUI>(synergyContent, "SynergyRow_Template"));

        WireField(so, "_frontLineArea", frontLine as RectTransform);
        WireField(so, "_backLineArea", boardColumn.Find("BackLine") as RectTransform);
        WireField(so, "_unitCardTemplate",
            FindComponent<SampleDeckUnitCardUI>(frontLine, "UnitCard_Template"));

        WireField(so, "_unitTooltip", FindComponent<SampleDeckUnitTooltipUI>(panel, "UnitTooltip"));

        WireField(so, "_uiManager",
            Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include));

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(panelUI);
    }

    /// <summary>
    /// 열기 버튼이 <b>뒤집힌 그래픽</b>이면 Canvas의 GraphicRaycaster에서
    /// <c>Ignore Reversed Graphics</c>를 꺼준다.
    ///
    /// <para>
    /// 배경 — <c>ExDeck_Button</c>은 프레임 스프라이트를 좌우 반전해 쓰려고 Y축 180° 회전이 걸려
    /// 있고(아이콘 자식이 다시 180° 돌아 정방향으로 보인다), GraphicRaycaster는 <b>기본값에서
    /// 뒤집힌 그래픽을 레이캐스트 대상에서 통째로 제외</b>한다. 그래서 onClick에 무엇을 붙이든
    /// 이벤트 자체가 도달하지 않는다 — 회전이 없는 Option_Button은 멀쩡히 눌리므로 배선 문제로
    /// 착각하기 쉽다.
    /// </para>
    ///
    /// 버튼 쪽 회전을 푸는 대신 이 옵션을 끄는 이유는 아트가 잡아둔 반전 배치를 건드리지 않기
    /// 위해서다. 이 씬에서 일부러 뒤집어 놓은 UI는 이 버튼뿐이라 부작용이 없다.
    /// </summary>
    private static void EnsureOpenButtonClickable(Transform canvas, Button openButton)
    {
        if (openButton == null) return;
        if (!IsReversed(openButton.transform)) return;

        var raycaster = canvas.GetComponentInParent<GraphicRaycaster>();
        if (raycaster == null) raycaster = canvas.GetComponentInChildren<GraphicRaycaster>(true);

        if (raycaster == null)
        {
            Debug.LogWarning("[SampleDeckPanelLayoutTool] Canvas에서 GraphicRaycaster를 찾지 못했습니다 — " +
                             $"'{OPEN_BUTTON_NAME}'이 뒤집힌 그래픽이라 이대로면 클릭이 들어오지 않습니다.");
            return;
        }

        if (!raycaster.ignoreReversedGraphics) return;

        Undo.RecordObject(raycaster, UNDO_NAME);
        raycaster.ignoreReversedGraphics = false;
        EditorUtility.SetDirty(raycaster);

        Debug.Log($"[SampleDeckPanelLayoutTool] '{OPEN_BUTTON_NAME}'이 Y축 180° 회전된 반전 그래픽이라 " +
                  "GraphicRaycaster의 Ignore Reversed Graphics를 껐습니다 — 이걸 켠 채로 두면 " +
                  "버튼에 클릭 이벤트가 아예 도달하지 않습니다.");
    }

    /// <summary>
    /// GraphicRaycaster가 "뒤집혔다"고 판정하는 것과 같은 계산.
    /// 오버레이 캔버스에서는 그래픽의 정면(+Z)이 화면 쪽을 향하는지만 본다.
    /// </summary>
    private static bool IsReversed(Transform graphic) =>
        Vector3.Dot(Vector3.forward, graphic.rotation * Vector3.forward) <= 0f;

    /// <summary>씬 어디에 있든 이름으로 열기 버튼을 찾는다(Info_Panel 아래에 있지만 위치는 바뀔 수 있다).</summary>
    private static Button FindOpenButton(Transform canvas)
    {
        GameObject found = FindByNameRecursive(canvas, OPEN_BUTTON_NAME);

        if (found == null)
        {
            Debug.LogWarning($"[SampleDeckPanelLayoutTool] '{OPEN_BUTTON_NAME}'를 찾지 못했습니다 — " +
                             "_openButton은 인스펙터에서 직접 물려 주세요.");
            return null;
        }

        Button button = found.GetComponent<Button>();

        if (button == null)
            Debug.LogWarning($"[SampleDeckPanelLayoutTool] '{OPEN_BUTTON_NAME}'에 Button 컴포넌트가 없습니다.");

        return button;
    }

    // ─────────────────────────────────────────
    // 생성 헬퍼
    // ─────────────────────────────────────────

    private static readonly Vector2 Center = new(0.5f, 0.5f);
    private static readonly Vector2 TopLeft = new(0f, 1f);
    private static readonly Vector2 TopRight = new(1f, 1f);
    private static readonly Vector2 TopCenter = new(0.5f, 1f);
    private static readonly Vector2 LeftMiddle = new(0f, 0.5f);
    private static readonly Vector2 RightMiddle = new(1f, 0.5f);

    private static void EnsureDivider(RectTransform parent, string name, float top, float sideMargin)
    {
        RectTransform divider = EnsureChild(parent, name);
        SetTopStretch(divider, -sideMargin * 2f, top, 1f);
        EnsureImage(divider, DIVIDER_COLOR, false);
    }

    /// <summary>세로 전용 ScrollRect 한 벌. 반환값은 항목을 담을 Content다.</summary>
    private static RectTransform EnsureScrollView(
        RectTransform parent, string name, float left, float right, float top, float bottom)
    {
        RectTransform scroll = EnsureChild(parent, name);
        SetStretch(scroll, left, right, top, bottom);

        var scrollRect = EnsureComponent<ScrollRect>(scroll.gameObject);

        RectTransform viewport = EnsureChild(scroll, "Viewport");
        SetStretch(viewport, 0f, 0f, 0f, 0f);
        EnsureComponent<RectMask2D>(viewport.gameObject);

        RectTransform content = EnsureChild(viewport, "Content");

        Undo.RecordObject(content, UNDO_NAME);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        content.localScale = Vector3.one;
        EditorUtility.SetDirty(content);

        var fitter = EnsureComponent<ContentSizeFitter>(content.gameObject);
        Undo.RecordObject(fitter, UNDO_NAME);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        EditorUtility.SetDirty(fitter);

        Undo.RecordObject(scrollRect, UNDO_NAME);
        scrollRect.content = content;
        scrollRect.viewport = viewport;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;
        scrollRect.horizontalScrollbar = null;
        scrollRect.verticalScrollbar = null;
        EditorUtility.SetDirty(scrollRect);

        return content;
    }

    /// <param name="controlChildSize">
    /// false면 자식의 rect 크기를 그대로 두고 자리만 잡는다 — 확대(localScale)해 둔 자식을
    /// 레이아웃이 다시 늘렸다 줄였다 하지 않게 할 때 쓴다.
    /// </param>
    private static void EnsureVertical(
        RectTransform rect, float spacing, float padding, bool controlChildSize = true)
    {
        var layout = EnsureComponent<VerticalLayoutGroup>(rect.gameObject);

        Undo.RecordObject(layout, UNDO_NAME);
        layout.spacing = spacing;
        layout.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = controlChildSize;
        layout.childForceExpandWidth = controlChildSize;
        layout.childControlHeight = controlChildSize;
        layout.childForceExpandHeight = false;
        EditorUtility.SetDirty(layout);
    }

    private static void RemoveComponent<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        if (component != null) Undo.DestroyObjectImmediate(component);
    }

    private static void EnsureHorizontal(RectTransform rect, float spacing, TextAnchor alignment)
    {
        var layout = EnsureComponent<HorizontalLayoutGroup>(rect.gameObject);

        Undo.RecordObject(layout, UNDO_NAME);
        layout.spacing = spacing;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = alignment;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        EditorUtility.SetDirty(layout);
    }

    private static void EnsureLayoutElement(RectTransform rect, float height)
    {
        var element = EnsureComponent<LayoutElement>(rect.gameObject);

        Undo.RecordObject(element, UNDO_NAME);
        element.minHeight = height;
        element.preferredHeight = height;
        EditorUtility.SetDirty(element);
    }

    private static void EnsureLayoutElementSize(RectTransform rect, float width, float height)
    {
        var element = EnsureComponent<LayoutElement>(rect.gameObject);

        Undo.RecordObject(element, UNDO_NAME);
        element.minWidth = width;
        element.preferredWidth = width;
        element.minHeight = height;
        element.preferredHeight = height;
        EditorUtility.SetDirty(element);
    }

    /// <summary>이름이 같은 자식이 있으면 그대로 쓰고, 없으면 RectTransform만 가진 빈 UI 오브젝트를 만든다.</summary>
    private static RectTransform EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            if (existing is RectTransform existingRect) return existingRect;

            Debug.LogWarning($"[SampleDeckPanelLayoutTool] '{name}'에 RectTransform이 없습니다(UI 오브젝트가 아님).");
            return null;
        }

        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, UNDO_NAME);

        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;

        // 씬의 다른 UI와 같은 레이어로 맞춘다(레이어 기반 필터를 쓰는 도구가 있을 때를 위해).
        go.layer = parent.gameObject.layer;

        return rect;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(go);
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
        RectTransform rect, string content, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        TMP_Text text = rect.GetComponent<TMP_Text>();
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

    private static Button EnsureButton(Transform parent, string name, string label, Color color)
    {
        RectTransform rect = EnsureChild(parent, name);

        EnsureImage(rect, color, true);
        var button = EnsureComponent<Button>(rect.gameObject);

        RectTransform textRect = EnsureChild(rect, "Text (TMP)");
        SetStretch(textRect, 4f, 4f, 2f, 2f);
        EnsureText(textRect, label, 15f, TEXT_COLOR, TextAlignmentOptions.Center);

        return button;
    }

    private static void SetRect(
        RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
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

    /// <summary>가로로는 부모를 따라 늘어나고 세로로는 위쪽에 붙는 줄.</summary>
    /// <param name="widthDelta">부모 폭 대비 증감. 좌우 8px씩 띄우려면 -16.</param>
    /// <param name="top">위쪽에서 떨어진 거리(아래로 갈수록 음수).</param>
    private static void SetTopStretch(RectTransform rect, float widthDelta, float top, float height)
    {
        if (rect == null) return;

        Undo.RecordObject(rect, UNDO_NAME);

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, top);
        rect.sizeDelta = new Vector2(widthDelta, height);
        rect.localScale = Vector3.one;

        EditorUtility.SetDirty(rect);
    }

    /// <summary>
    /// 상세 페이지의 세로 칸. 위쪽은 상세 머리말 아래에서 시작하고 아래는 페이지 바닥까지 늘어난다.
    /// </summary>
    /// <param name="left">페이지 왼쪽 끝에서 떨어진 거리.</param>
    /// <param name="width">칸 너비. 0이면 페이지 오른쪽 끝까지 늘어난다.</param>
    private static void SetColumn(RectTransform rect, float left, float width)
    {
        if (rect == null) return;

        Undo.RecordObject(rect, UNDO_NAME);

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(width > 0f ? 0f : 1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.offsetMin = new Vector2(left, 0f);
        rect.offsetMax = new Vector2(width > 0f ? left + width : 0f, -DETAIL_HEADER_HEIGHT);
        rect.localScale = Vector3.one;

        EditorUtility.SetDirty(rect);
    }

    private static void SetStretch(RectTransform rect, float left, float right, float top, float bottom)
    {
        if (rect == null) return;

        Undo.RecordObject(rect, UNDO_NAME);

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = Center;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        rect.localScale = Vector3.one;

        EditorUtility.SetDirty(rect);
    }

    // ─────────────────────────────────────────
    // 탐색 / 공용
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

    private static GameObject FindByNameRecursive(Transform current, string name)
    {
        if (current.name == name) return current.gameObject;

        for (int i = 0; i < current.childCount; i++)
        {
            GameObject found = FindByNameRecursive(current.GetChild(i), name);
            if (found != null) return found;
        }

        return null;
    }

    private static T FindComponent<T>(Transform parent, string name) where T : Component
    {
        if (parent == null) return null;

        Transform child = parent.Find(name);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static GameObject FindGameObject(Transform parent, string name)
    {
        if (parent == null) return null;

        Transform child = parent.Find(name);
        return child != null ? child.gameObject : null;
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
            Debug.LogWarning($"[SampleDeckPanelLayoutTool] 필드를 찾지 못했습니다: {fieldName}");
            return;
        }

        if (target == null)
        {
            Debug.LogWarning($"[SampleDeckPanelLayoutTool] {fieldName}에 연결할 대상을 찾지 못했습니다 — 기존 값 유지.");
            return;
        }

        if (prop.objectReferenceValue == target) return;

        prop.objectReferenceValue = target;
    }

    private static void SetActiveWithUndo(GameObject go, bool active)
    {
        if (go == null || go.activeSelf == active) return;

        Undo.RecordObject(go, UNDO_NAME);
        go.SetActive(active);
        EditorUtility.SetDirty(go);
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

        Debug.LogWarning("[SampleDeckPanelLayoutTool] 이름에 'NEXON'이 든 TMP_FontAsset을 찾지 못해 폰트 적용을 건너뜁니다.");
        return null;
    }

    /// <summary>
    /// root 아래 모든 TMP_Text(비활성 포함)의 폰트만 교체한다. 머티리얼은 건드리지 않는다.
    /// 프리팹 인스턴스(UnitSlot_Pf·SynergyRow_Pf)는 건너뛴다 — 그쪽 폰트는 프리팹이 정한다.
    /// </summary>
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

            Undo.RecordObject(text, UNDO_NAME);
            fontProp.objectReferenceValue = font;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(text);
        }
    }
}
