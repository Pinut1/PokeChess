using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 견본덱 확인 창(uGUI). 예전 <see cref="UIManager"/>의 IMGUI <c>GUI.Window</c> 구현을 대체한다.
///
/// <b>두 페이지 구조</b> — 롤체지지 덱 목록/공략과 같은 흐름이다.
/// <list type="number">
/// <item>목록 — 등록된 견본덱을 한 줄 요약(이름·시너지·유닛·비용)으로 나열</item>
/// <item>상세 — [공략 더 보기]로 진입. 왼쪽에 활성 시너지, 오른쪽에 앞/뒷라인 배치도</item>
/// </list>
/// 창은 열린 채로 두 페이지를 오간다(<see cref="ShowList"/> / <see cref="ShowDetail"/>).
///
/// <b>배치 규칙</b> — <see cref="OptionsPanelUI"/>와 같이 <b>항상 활성인 오브젝트(Canvas)에 붙일 것</b>.
/// 패널 자신에 붙이면 닫혀 있는 동안 열기 버튼의 클릭을 받지 못한다.
///
/// <b>🚨 데이터에 없어서 파생시키는 값</b> — 견본덱 시트(deck_data.json)에는 덱 이름·유닛·목표
/// 성급·활성 시너지·완성 비용만 있다. 아래 넷은 <b>시트에 컬럼이 생기기 전까지의 임시 규칙</b>이며,
/// 확정되면 각각의 메서드 하나만 갈아끼우면 된다.
/// <list type="bullet">
/// <item><see cref="IsFrontLine"/> — 역할군으로 앞/뒷라인 추정(Tanker·Warrior가 앞)</item>
/// <item><see cref="CoreUnitsOf"/> — 목표 성급 → 코스트 순 상위 <see cref="CORE_UNIT_COUNT"/>명을 핵심으로</item>
/// <item><see cref="GetRecommendedItems"/> — ItemDatabase에서 무작위(덱+유닛 조합별로 캐시)</item>
/// <item><see cref="CompletionLevelOf"/> — 유닛 수 = 완성 레벨(레벨당 배치 수가 1:1이라 성립)</item>
/// </list>
/// </summary>
public class SampleDeckPanelUI : MonoBehaviour
{
    /// <summary>핵심 유닛으로 뽑을 최대 인원. 이 인원까지만 배치도에 아이템이 붙는다.</summary>
    private const int CORE_UNIT_COUNT = 3;

    /// <summary>핵심 유닛 한 명에게 붙일 아이템 수.</summary>
    private const int ITEMS_PER_CORE_UNIT = 2;

    // ─────────────────────────────────────────
    // 인스펙터 — 여닫기
    // ─────────────────────────────────────────

    [Header("여닫기")]
    [Tooltip("견본덱 창 루트. SetActive(true/false)로 여닫는다. 씬에는 꺼둔 채 저장할 것.")]
    [SerializeField] private GameObject _panelRoot;

    [Tooltip("창을 여는 버튼(Info_Panel/OptionTool_BackPanel/ExDeck_Button). 누를 때마다 열기/닫기가 토글된다.")]
    [SerializeField] private Button _openButton;

    [SerializeField] private Button _closeButton;

    // ─────────────────────────────────────────
    // 인스펙터 — 1페이지: 목록
    // ─────────────────────────────────────────

    [Header("1페이지 — 덱 목록")]
    [Tooltip("목록 페이지 루트.")]
    [SerializeField] private GameObject _listPage;

    [Tooltip("덱 줄이 담기는 부모(ScrollRect Content). VerticalLayoutGroup을 붙여둘 것.")]
    [SerializeField] private RectTransform _listContent;

    [Tooltip("복제할 덱 줄 템플릿. Content 아래에 비활성으로 둘 것.")]
    [SerializeField] private SampleDeckListRowUI _listRowTemplate;

    [Tooltip("덱이 하나도 없을 때만 켜지는 안내.")]
    [SerializeField] private GameObject _listEmptyNotice;

    // ─────────────────────────────────────────
    // 인스펙터 — 2페이지: 상세
    // ─────────────────────────────────────────

    [Header("2페이지 — 덱 상세")]
    [Tooltip("상세 페이지 루트.")]
    [SerializeField] private GameObject _detailPage;

    [Tooltip("목록으로 돌아가는 버튼.")]
    [SerializeField] private Button _backButton;

    [SerializeField] private TMP_Text _detailDeckNameText;

    [Tooltip("'완성 기준 Lv.9'. 숫자는 유닛 수에서 파생된다.")]
    [SerializeField] private TMP_Text _detailLevelText;

    [Tooltip("'87' — 완성 비용. 단위는 씬에 적어둔다.")]
    [SerializeField] private TMP_Text _detailGoldText;

    [Header("2페이지 — 활성 시너지(왼쪽)")]
    [Tooltip("시너지 행이 담기는 부모. VerticalLayoutGroup을 붙여둘 것.")]
    [SerializeField] private RectTransform _synergyRowArea;

    [Tooltip("복제할 시너지 행(SynergyRow_Pf 인스턴스). 게임 안 시너지 패널과 같은 프리팹을 쓴다.")]
    [SerializeField] private SynergyRowUI _synergyRowTemplate;

    [Header("2페이지 — 배치도(오른쪽)")]
    [Tooltip("앞라인 유닛이 깔리는 부모. HorizontalLayoutGroup을 붙여둘 것.")]
    [SerializeField] private RectTransform _frontLineArea;

    [Tooltip("뒷라인 유닛이 깔리는 부모. HorizontalLayoutGroup을 붙여둘 것.")]
    [SerializeField] private RectTransform _backLineArea;

    [Tooltip("복제할 유닛 칸 템플릿. 앞/뒷라인이 같은 템플릿을 쓴다.")]
    [SerializeField] private SampleDeckUnitCardUI _unitCardTemplate;

    [Tooltip("유닛 설명창. 창 루트 아래 항상 활성인 자리에 둘 것.")]
    [SerializeField] private SampleDeckUnitTooltipUI _unitTooltip;

    [Header("연결")]
    [Tooltip("IMGUI 전적창을 닫는 데만 쓴다 — 견본덱을 열 때 두 창이 겹치지 않게. 비어도 동작한다.")]
    [SerializeField] private UIManager _uiManager;

    // ─────────────────────────────────────────
    // 상태
    // ─────────────────────────────────────────

    private readonly List<DeckData> _decks = new();

    private readonly List<SampleDeckListRowUI> _listRows = new();
    private readonly List<SynergyRowUI> _synergyRows = new();
    private readonly List<SampleDeckUnitCardUI> _frontCards = new();
    private readonly List<SampleDeckUnitCardUI> _backCards = new();

    // 추천 아이템은 무작위 임시값이라 매번 다시 뽑으면 화면이 흔들린다.
    // "덱ID_포켓몬ID" 기준으로 한 번 뽑은 결과를 들고 있는다.
    private readonly Dictionary<string, ItemData[]> _recommendedItems = new();

    // 상세에 들어간 덱. 목록으로 나가면 -1.
    private int _detailDeckIndex = -1;

    /// <summary>지금 창이 열려 있는지.</summary>
    public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

    // ─────────────────────────────────────────
    // 생명주기
    // ─────────────────────────────────────────

    private void Awake()
    {
        if (_openButton != null) _openButton.onClick.AddListener(Toggle);
        if (_closeButton != null) _closeButton.onClick.AddListener(Close);
        if (_backButton != null) _backButton.onClick.AddListener(ShowList);

        SampleDeckPool.HideTemplate(_listRowTemplate);
        SampleDeckPool.HideTemplate(_synergyRowTemplate);
        SampleDeckPool.HideTemplate(_unitCardTemplate);

        if (_panelRoot != null) _panelRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_openButton != null) _openButton.onClick.RemoveListener(Toggle);
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
        if (_backButton != null) _backButton.onClick.RemoveListener(ShowList);
    }

    // ─────────────────────────────────────────
    // 여닫기 / 페이지 전환
    // ─────────────────────────────────────────

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (_panelRoot == null)
        {
            Debug.LogError("[SampleDeckPanelUI] 창 루트(_panelRoot) 미배선 — 견본덱 창이 열리지 않는다", this);
            return;
        }

        // IMGUI 전적창은 Canvas보다 위에 그려져 견본덱 창을 덮는다. 열 때 같이 닫는다.
        if (_uiManager != null) _uiManager.CloseMatchHistoryWindow();

        _panelRoot.SetActive(true);

        RefreshDecks();
        ShowList();
    }

    public void Close()
    {
        if (_panelRoot != null) _panelRoot.SetActive(false);
        if (_unitTooltip != null) _unitTooltip.HideAll();
    }

    /// <summary>1페이지(덱 목록)로.</summary>
    public void ShowList()
    {
        _detailDeckIndex = -1;

        if (_unitTooltip != null) _unitTooltip.HideAll();

        if (_listPage != null) _listPage.SetActive(true);
        if (_detailPage != null) _detailPage.SetActive(false);
    }

    /// <summary>2페이지(덱 상세)로. 범위를 벗어난 인덱스면 아무 일도 하지 않는다.</summary>
    public void ShowDetail(int deckIndex)
    {
        if (deckIndex < 0 || deckIndex >= _decks.Count) return;

        _detailDeckIndex = deckIndex;

        if (_unitTooltip != null) _unitTooltip.HideAll();

        BuildDetail(_decks[deckIndex]);

        if (_listPage != null) _listPage.SetActive(false);
        if (_detailPage != null) _detailPage.SetActive(true);
    }

    // ─────────────────────────────────────────
    // 1페이지 — 덱 목록
    // ─────────────────────────────────────────

    /// <summary>DeckDatabase에서 전체 견본덱을 다시 읽어 목록을 새로 그린다.</summary>
    public void RefreshDecks()
    {
        _decks.Clear();
        _recommendedItems.Clear();

        DeckDatabase db = DeckDatabase.Instance;

        if (db != null && db.decks != null)
        {
            for (int i = 0; i < db.decks.Count; i++)
                if (db.decks[i] != null) _decks.Add(db.decks[i]);

            _decks.Sort((a, b) => a.deckId.CompareTo(b.deckId));
        }

        if (_listEmptyNotice != null) _listEmptyNotice.SetActive(_decks.Count == 0);

        BuildList();
    }

    private void BuildList()
    {
        for (int i = 0; i < _decks.Count; i++)
        {
            DeckData deck = _decks[i];

            SampleDeckListRowUI row = SampleDeckPool.Take(
                _listRows, _listRowTemplate, _listContent, i,
                created => created.DetailRequested += HandleDetailRequested);

            if (row == null)
            {
                WarnMissingTemplate("덱 목록 줄");
                break;
            }

            row.Bind(
                i,
                DeckDisplayName(deck),
                deck.totalGoldToBuild,
                SynergyStatusesOf(deck, includeInactive: false),
                ListUnitsOf(deck));
        }

        SampleDeckPool.HideExtras(_listRows, _decks.Count);
    }

    private void HandleDetailRequested(SampleDeckListRowUI row) => ShowDetail(row.Index);

    private static string DeckDisplayName(DeckData deck) =>
        string.IsNullOrWhiteSpace(deck.deckName) ? $"덱 {deck.deckId}" : deck.deckName;

    // ─────────────────────────────────────────
    // 2페이지 — 덱 상세
    // ─────────────────────────────────────────

    private void BuildDetail(DeckData deck)
    {
        if (_detailDeckNameText != null) _detailDeckNameText.text = DeckDisplayName(deck);
        if (_detailGoldText != null) _detailGoldText.text = deck.totalGoldToBuild.ToString();

        if (_detailLevelText != null)
            _detailLevelText.text = $"완성 기준 Lv.{CompletionLevelOf(deck)}";

        BuildSynergyRows(deck);
        BuildBoard(deck);
    }

    /// <summary>
    /// 왼쪽 활성 시너지 목록. 게임 안 시너지 패널과 <b>같은 행 프리팹</b>을 써서
    /// 아이콘·등급 프레임·문턱 표기가 전투 화면과 어긋나지 않게 한다.
    /// </summary>
    private void BuildSynergyRows(DeckData deck)
    {
        // 상세는 비활성 시너지도 보여준다 — 행 프리팹이 비활성 양식("2 / 4")을 따로 갖고 있다.
        List<SynergyStatus> statuses = SynergyStatusesOf(deck, includeInactive: true);

        for (int i = 0; i < statuses.Count; i++)
        {
            SynergyRowUI row = SampleDeckPool.Take(_synergyRows, _synergyRowTemplate, _synergyRowArea, i);

            if (row == null)
            {
                WarnMissingTemplate("시너지 행");
                break;
            }

            row.Bind(statuses[i]);
        }

        SampleDeckPool.HideExtras(_synergyRows, statuses.Count);
    }

    /// <summary>
    /// 배치도. 실제 좌표(q,r)는 견본덱 데이터에 없으므로 <b>앞/뒷라인 두 줄</b>로만 나눈다 —
    /// 조합을 볼 때 필요한 건 "누가 앞에 서고 누가 뒤에 서는가"까지다.
    /// </summary>
    private void BuildBoard(DeckData deck)
    {
        List<DeckUnitEntry> units = SortedUnits(deck);
        HashSet<int> coreIds = CoreUnitsOf(units);

        int front = 0;
        int back = 0;

        for (int i = 0; i < units.Count; i++)
        {
            DeckUnitEntry entry = units[i];
            PokemonData pokemon = FindPokemonById(entry.pokemonId);

            bool isFront = IsFrontLine(pokemon);

            SampleDeckUnitCardUI card = SampleDeckPool.Take(
                isFront ? _frontCards : _backCards,
                _unitCardTemplate,
                isFront ? _frontLineArea : _backLineArea,
                isFront ? front : back,
                created => created.Hovered += HandleUnitHovered);

            if (card == null)
            {
                WarnMissingTemplate("유닛 칸");
                break;
            }

            // 핵심 유닛에게만 아이템을 붙인다 — 나머지는 빈 줄이 아니라 아예 표시하지 않는다.
            ItemData[] items = coreIds.Contains(entry.pokemonId)
                ? GetRecommendedItems(deck.deckId, entry.pokemonId)
                : null;

            card.Bind(pokemon, entry.starLevel, items);

            if (isFront) front++;
            else back++;
        }

        SampleDeckPool.HideExtras(_frontCards, front);
        SampleDeckPool.HideExtras(_backCards, back);
    }

    private void HandleUnitHovered(SampleDeckUnitCardUI card, bool entered)
    {
        if (_unitTooltip == null) return;

        if (!entered)
        {
            _unitTooltip.Hide(card);
            return;
        }

        if (card.Data == null) return;

        // 배치도에 아이템이 안 붙은 유닛도 설명창에서는 추천 아이템을 보여준다 —
        // 핵심이 아니라고 해서 아이템을 안 끼우는 건 아니기 때문이다.
        DeckData deck = CurrentDeck;

        ItemData[] items = deck != null
            ? GetRecommendedItems(deck.deckId, card.Data.id)
            : null;

        _unitTooltip.Show(card, card.Data, items);
    }

    private DeckData CurrentDeck =>
        _detailDeckIndex >= 0 && _detailDeckIndex < _decks.Count ? _decks[_detailDeckIndex] : null;

    // ─────────────────────────────────────────
    // 덱 데이터 → 표시용 변환
    // ─────────────────────────────────────────

    /// <summary>시트 순서(slot)대로 세운 목표 유닛 목록. null 항목은 걸러낸다.</summary>
    private static List<DeckUnitEntry> SortedUnits(DeckData deck)
    {
        var units = new List<DeckUnitEntry>();

        if (deck.units != null)
        {
            for (int i = 0; i < deck.units.Count; i++)
                if (deck.units[i] != null) units.Add(deck.units[i]);
        }

        units.Sort((a, b) => a.slot.CompareTo(b.slot));
        return units;
    }

    /// <summary>
    /// 목록 줄에 보여줄 유닛. 코스트 높은 순이라 덱의 주력이 앞에 온다.
    /// 아이템은 배치도와 같은 규칙 — <b>핵심 유닛에만</b> 붙는다.
    /// </summary>
    private List<SampleDeckUnitView> ListUnitsOf(DeckData deck)
    {
        List<DeckUnitEntry> entries = SortedUnits(deck);
        HashSet<int> coreIds = CoreUnitsOf(entries);

        var result = new List<SampleDeckUnitView>();

        foreach (DeckUnitEntry entry in entries)
        {
            PokemonData pokemon = FindPokemonById(entry.pokemonId);
            if (pokemon == null) continue;

            ItemData[] items = coreIds.Contains(entry.pokemonId)
                ? GetRecommendedItems(deck.deckId, entry.pokemonId)
                : null;

            result.Add(new SampleDeckUnitView(pokemon, entry.starLevel, items));
        }

        result.Sort((a, b) =>
        {
            int byCost = b.pokemon.cost.CompareTo(a.pokemon.cost);
            return byCost != 0 ? byCost : a.pokemon.id.CompareTo(b.pokemon.id);
        });

        return result;
    }

    /// <summary>
    /// 시너지 행 프리팹이 요구하는 <see cref="SynergyStatus"/>를 덱 데이터로 만들어 준다.
    /// 보드에서 계산한 것이 아니라 "이 덱을 완성했을 때"의 상태다.
    /// </summary>
    /// <param name="includeInactive">
    /// 문턱을 못 넘은 시너지(<c>Electric(1/4)</c>처럼 4기가 필요한데 1기뿐인 줄)를 포함할지.
    ///
    /// <b>목록(1페이지)은 false</b> — 한 줄에 뱃지만 늘어놓는 자리라, 발동하지도 않는 시너지가
    /// 섞이면 그 덱이 뭘 노리는지 읽히지 않는다.
    ///
    /// <b>상세(2페이지)는 true</b> — 시너지 행이 활성/비활성 두 양식을 다 갖고 있어서
    /// "이 덱은 여기까지만 채운다"는 정보가 그대로 드러난다.
    /// </param>
    private static List<SynergyStatus> SynergyStatusesOf(DeckData deck, bool includeInactive)
    {
        var result = new List<SynergyStatus>();

        if (deck.activeSynergies == null) return result;

        for (int i = 0; i < deck.activeSynergies.Count; i++)
        {
            string text = deck.activeSynergies[i];

            SynergyData data = SampleDeckSynergyLookup.Find(text);
            if (data == null) continue;

            int count = SampleDeckSynergyLookup.ExtractCurrentCount(text);
            int tierIndex = ActiveTierIndexOf(data, count);

            if (tierIndex < 0 && !includeInactive) continue;

            result.Add(new SynergyStatus
            {
                data = data,
                uniqueCount = count,
                activeTierIndex = tierIndex,
            });
        }

        // 등급 높은 순 → 같으면 이름순. 게임 안 시너지 패널과 같은 정렬이라 눈이 헷갈리지 않는다.
        result.Sort((a, b) =>
        {
            int byGrade = SynergyGradeUtil.Of(b).CompareTo(SynergyGradeUtil.Of(a));
            if (byGrade != 0) return byGrade;

            return string.Compare(a.data.synergyName, b.data.synergyName, System.StringComparison.Ordinal);
        });

        return result;
    }

    /// <summary>현재 수로 도달한 가장 높은 문턱의 인덱스. 하나도 못 넘었으면 -1(비활성).</summary>
    private static int ActiveTierIndexOf(SynergyData data, int count)
    {
        if (data.tiers == null) return -1;

        int index = -1;

        for (int i = 0; i < data.tiers.Count; i++)
            if (data.tiers[i] != null && count >= data.tiers[i].count) index = i;

        return index;
    }

    // ─────────────────────────────────────────
    // 🚨 시트에 없어서 파생시키는 값 (컬럼이 생기면 이 아래만 갈아끼울 것)
    // ─────────────────────────────────────────

    /// <summary>
    /// 완성 기준 레벨. ShopManager의 레벨별 배치 수가 <b>레벨과 1:1</b>(Lv7 = 7기)이라
    /// 유닛 수를 그대로 레벨로 쓴다. 그 표가 바뀌면 여기도 같이 바꿔야 한다.
    /// </summary>
    private static int CompletionLevelOf(DeckData deck) => deck.unitCount;

    /// <summary>
    /// 앞라인 판정. 견본덱 데이터에 좌표가 없어 <b>역할군으로 추정</b>한다 —
    /// 탱커·전사는 앞, 나머지(원거리·마법사·서포터·암살자)는 뒤.
    /// 역할군을 못 읽으면 뒤로 보낸다(앞라인이 비는 편이 눈에 잘 띄어 데이터 구멍을 알아채기 쉽다).
    /// </summary>
    private static bool IsFrontLine(PokemonData pokemon)
    {
        if (pokemon == null || string.IsNullOrWhiteSpace(pokemon.role)) return false;

        return pokemon.role.Equals("Tanker", System.StringComparison.OrdinalIgnoreCase) ||
               pokemon.role.Equals("Warrior", System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 핵심 유닛. 시트에 "핵심" 표시가 없어 <b>목표 성급 → 코스트 → 시트 순서</b>로 정렬해
    /// 상위 <see cref="CORE_UNIT_COUNT"/>명을 고른다.
    ///
    /// 성급을 먼저 보는 이유는 3성을 목표로 잡은 유닛이 곧 그 덱의 캐리이기 때문이다.
    /// 다만 <b>7개 덱 중 3개는 3성 유닛이 아예 없어</b> 그때는 코스트가 실질 기준이 된다.
    /// </summary>
    private static HashSet<int> CoreUnitsOf(List<DeckUnitEntry> units)
    {
        var ordered = new List<DeckUnitEntry>(units);

        ordered.Sort((a, b) =>
        {
            int byStar = b.starLevel.CompareTo(a.starLevel);
            if (byStar != 0) return byStar;

            // UnityEngine.Object에 ?.를 쓰면 파괴된 객체의 가짜 null을 놓친다 — 명시적으로 검사한다.
            PokemonData pokemonA = FindPokemonById(a.pokemonId);
            PokemonData pokemonB = FindPokemonById(b.pokemonId);

            int costA = pokemonA != null ? pokemonA.cost : 0;
            int costB = pokemonB != null ? pokemonB.cost : 0;

            int byCost = costB.CompareTo(costA);
            return byCost != 0 ? byCost : a.slot.CompareTo(b.slot);
        });

        var core = new HashSet<int>();

        for (int i = 0; i < ordered.Count && core.Count < CORE_UNIT_COUNT; i++)
            core.Add(ordered[i].pokemonId);

        return core;
    }

    /// <summary>
    /// 덱+포켓몬 조합별 추천 아이템. <b>아직 기획 미확정이라 ItemDatabase에서 무작위로 뽑는
    /// 임시값이다</b> — 확정되면 이 메서드만 실제 배정 데이터로 갈아끼우면 된다.
    /// 한 번 뽑은 결과는 캐시해 창을 다시 열기 전까지 같은 아이템이 나온다.
    /// </summary>
    private ItemData[] GetRecommendedItems(int deckId, int pokemonId)
    {
        string key = $"{deckId}_{pokemonId}";

        if (_recommendedItems.TryGetValue(key, out ItemData[] cached)) return cached;

        ItemDatabase db = ItemDatabase.Instance;
        var result = new ItemData[ITEMS_PER_CORE_UNIT];

        if (db != null && db.all != null)
        {
            var available = new List<ItemData>();

            for (int i = 0; i < db.all.Count; i++)
                if (db.all[i] != null) available.Add(db.all[i]);

            if (available.Count > 0)
            {
                for (int i = 0; i < result.Length; i++)
                    result[i] = available[Random.Range(0, available.Count)];
            }
        }

        _recommendedItems[key] = result;
        return result;
    }

    // ─────────────────────────────────────────
    // 조회 / 경고
    // ─────────────────────────────────────────

    private static PokemonData FindPokemonById(int pokemonId)
    {
        PokemonDatabase db = PokemonDatabase.Instance;
        return db != null ? db.GetById(pokemonId) : null;
    }

    // 템플릿 미배선 경고는 종류당 한 번만 — 다시 그릴 때마다 로그가 쌓인다.
    private readonly HashSet<string> _warnedTemplates = new();

    private void WarnMissingTemplate(string what)
    {
        if (!_warnedTemplates.Add(what)) return;

        Debug.LogWarning(
            $"[SampleDeckPanelUI] '{what}' 템플릿이 비어 있어 그리지 못했습니다. " +
            "PokeChess/UI/Create Sample Deck Panel 메뉴로 계층을 다시 만들거나 인스펙터에서 물려 주세요.",
            this);
    }
}
