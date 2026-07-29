using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 게임 UI 표시와 입력 연결을 담당한다. 김태욱 파트.
///
/// 현재 구현 범위:
/// 1. 플레이어 진행 HUD
///    - 골드, 레벨, XP, 배치 가능 기물 수 표시
///    - GameEvents 변경 이벤트를 구독해 표시값 캐시
///    - Canvas XP 구매 버튼을 GameEvents 요청으로 연결
/// 2. 전적창 MVP
///    - MatchHistoryStore.LoadRecent()를 이용한 과거 전적 조회
///    - GameEvents.OnMatchRecorded 구독을 통한 신규 전적 실시간 반영
///    - 최근 전적 목록과 선택 전적 상세 표시
///    - (Phase 3, 영욱 배선 — 태욱님 리뷰 필요) 로컬/서버 출처 탭: 서버 탭은
///      GameEvents.RequestServerMatches → SupabaseMatchUploader가 조회 →
///      OnServerMatchesLoaded로 수신해 같은 목록/상세 UI를 재사용
///
/// 구조 원칙:
/// - 진행 상태는 OnGUI에서 ShopManager를 매번 조회하지 않는다.
/// - 상태 변경 시 GameEvents를 통해 전달받아 로컬 캐시에 저장하고, OnGUI는 캐시만 그린다.
/// - Start의 SyncProgressState()는 이벤트 구독 이전에 발생한 초기값을 보완하기 위한 1회 동기화다.
/// - 현재 표시는 OnGUI 기반 MVP이며, 추후 Canvas/TMP UI로 교체해도 이벤트 구독 구조는 그대로 재사용한다.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Match History")]
    [SerializeField] private int _recentMatchCount = 10;

    [Header("Canvas Shop Controls")]
    [Tooltip("비어 있으면 GameSceneTest의 'Level Button'을 런타임에 찾습니다.")]
    [SerializeField] private Button _xpPurchaseButton;
    [Tooltip("비어 있으면 GameSceneTest의 'ReRoll Button'을 런타임에 찾습니다.")]
    [SerializeField] private Button _shopRerollButton;
    [Tooltip("비어 있으면 GameSceneTest의 'BattleRaedy_Button'을 런타임에 찾습니다.")]
    [SerializeField] private Button _battleReadyButton;
    [Tooltip("비어 있으면 UnitStore_Panel 아래의 ShopChange Button을 런타임에 찾습니다.")]
    [SerializeField] private Button _showItemStoreButton;
    [Tooltip("비어 있으면 ItemStore_Panel 아래의 ShopChange Button을 런타임에 찾습니다.")]
    [SerializeField] private Button _showUnitStoreButton;

    [Tooltip("골드 부족으로 버튼이 꺼졌을 때 씌울 흑백 머티리얼(PokeChess/UI/Grayscale). " +
             "카드 일러스트와 같은 것을 쓰면 톤이 맞는다. 비워두면 Animator의 Disabled 상태에만 의존한다.")]
    [SerializeField] private Material _disabledGrayscaleMaterial;
    [SerializeField] private GameObject _unitStorePanel;
    [SerializeField] private GameObject _itemStorePanel;

    [Header("Canvas Progress Texts")]
    [Tooltip("비어 있으면 Level_Panel 아래의 LevelText를 런타임에 찾습니다.")]
    [SerializeField] private TMP_Text _levelText;
    [Tooltip("비어 있으면 Level_Panel 아래의 LevelSubText를 런타임에 찾습니다.")]
    [SerializeField] private TMP_Text _levelSubText;
    [Tooltip("비어 있으면 Coin_Panel 아래의 CoinText를 런타임에 찾습니다.")]
    [SerializeField] private TMP_Text _goldText;
    [Tooltip("비어 있으면 Coupon_Panel 아래의 CoinText를 런타임에 찾습니다.")]
    [SerializeField] private TMP_Text _couponText;
    [Tooltip("비어 있으면 RateText_Group 아래의 RateText1~5를 런타임에 찾습니다. 1~5코스트 순.")]
    [SerializeField] private TMP_Text[] _costRateTexts = new TMP_Text[CostTierCount];

    /// <summary>상점 코스트 등급 수(1~5코스트). RateText_Group의 자식 수와 같아야 한다.</summary>
    private const int CostTierCount = 5;

    private bool _canvasProgressTextsReady;
    private bool _canvasProgressTextsWarningLogged;
    private int _itemCoupon;

    private bool _canvasShopControlsReady;
    private bool _canvasShopControlsWarningLogged;
    private GamePhase _currentPhase = GamePhase.Lobby;
    private bool _readyRequestSubmitted;

    // ──────────────────────────────────────────
    // 전적 데이터 및 전적창 상태
    // ──────────────────────────────────────────

    private readonly List<MatchRecord> _recentMatches = new List<MatchRecord>();

    /// <summary>전적 출처. Local=jsonl, Server=Supabase(이벤트로 SupabaseMatchUploader에 요청).</summary>
    private enum HistorySource { Local, Server }

    // (Phase 3, 영욱 배선 — 태욱님 리뷰 필요) 서버 전적 탭 상태.
    // 서버 목록도 _recentMatches에 채워 기존 목록/상세 그리기 코드를 그대로 재사용한다.
    private HistorySource _historySource = HistorySource.Local;
    private string _serverStatus = "";   // 서버 탭 빈 목록일 때 표시(불러오는 중/실패 사유)

    private bool _showMatchHistory;
    private int _selectedMatchIndex;
    private Vector2 _listScroll;
    private Vector2 _detailScroll;

    private Rect _matchHistoryWindowRect = new Rect(260f, 80f, 980f, 650f);

    // ──────────────────────────────────────────
    // 견본덱 데이터 및 견본덱창 상태
    // ──────────────────────────────────────────

    private readonly List<DeckData> _sampleDecks = new List<DeckData>();

    private bool _showSampleDeck;
    private int _selectedSampleDeckIndex;

    private Vector2 _sampleDeckListScroll;
    private Vector2 _sampleDeckDetailScroll;

    private Rect _sampleDeckWindowRect = new Rect(80f, 50f, 1500f, 780f);

    // 현재 클릭해 고정한 목표 유닛.
    // -1이면 아직 선택된 유닛이 없다는 뜻이다.
    private int _selectedSampleDeckPokemonId = -1;

    // 현재 클릭해 고정한 활성 시너지.
    // -1이면 아직 선택된 시너지가 없다는 뜻이다.
    private int _selectedSampleDeckSynergyIndex = -1;

    // 마우스를 올린 유닛과 시너지의 임시 툴팁 표시용 인덱스.
    private int _hoveredSampleDeckUnitIndex = -1;
    private int _hoveredSampleDeckSynergyIndex = -1;

    // 견본덱 장비 툴팁 상태.
    // Hover 중인 장비 이름과 클릭으로 고정된 장비 이름을 따로 관리한다.
    private string _hoveredSampleDeckItemName = "";
    private string _pinnedSampleDeckItemName = "";

    // 장비 항목 오른쪽 위치를 화면 좌표로 저장한다.
    // 스크롤 내부와 외부의 GUI 좌표계 차이를 피하기 위해 화면 좌표를 사용한다.
    private Vector2 _sampleDeckItemTooltipAnchorScreenPosition;

    // 오른쪽 유닛/시너지 상세 패널의 개별 스크롤 위치.
    private Vector2 _sampleDeckUnitDetailScroll;
    private Vector2 _sampleDeckSynergyDetailScroll;

    // 랜덤 추천 장비는 매 OnGUI 호출마다 바뀌면 안 되므로
    // 덱 ID + 포켓몬 ID 기준으로 한 번 생성한 결과를 보관한다.
    private readonly Dictionary<string, string[]> _temporaryRecommendedItems = new Dictionary<string, string[]>();

    // 현재 이벤트에서 장비 항목이 클릭되었는지 기록한다.
    // 창의 다른 곳 클릭 처리와 충돌하지 않게 한다.
    private bool _sampleDeckItemClickedThisEvent;

    // ──────────────────────────────────────────
    // 플레이어 진행 상태 캐시
    // ──────────────────────────────────────────
    // 아래 값은 GameEvents를 통해 갱신한다.
    // OnGUI에서 ShopManager.CurrentXp 등을 직접 조회하지 않아 매 프레임 폴링을 피한다.
    // XP 구매 비용/획득량은 런타임 중 변하지 않는 설정값이므로 시작 시 한 번만 가져온다.

    private int _gold;
    private int _currentLevel = 1;
    private int _currentXp;
    private int _requiredXp;
    private int _unitCap = 1;

    // ──────────────────────────────────────────
    // OnGUI 스타일 캐시
    // ──────────────────────────────────────────

    private GUIStyle _titleStyle;
    private GUIStyle _sectionTitleStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _smallStyle;
    private GUIStyle _mutedStyle;
    private GUIStyle _winStyle;
    private GUIStyle _loseStyle;
    private GUIStyle _badgeStyle;
    private GUIStyle _unitCardStyle;
    private GUIStyle _selectedListCardStyle;
    private GUIStyle _normalListCardStyle;
    private GUIStyle _itemTooltipStyle;
    private Texture2D _itemTooltipBackground;

    // ──────────────────────────────────────────
    // Unity 생명주기 / 이벤트 구독
    // ──────────────────────────────────────────
    private void OnEnable()
    {
        GameEvents.OnMatchRecorded += HandleMatchRecorded;
        GameEvents.OnServerMatchesLoaded += HandleServerMatchesLoaded;

        GameEvents.OnGoldChanged += HandleGoldChanged;
        GameEvents.OnLevelChanged += HandleLevelChanged;
        GameEvents.OnXpChanged += HandleXpChanged;
        GameEvents.OnUnitCapChanged += HandleUnitCapChanged;
        GameEvents.OnPhaseChanged += HandlePhaseChanged;
        GameEvents.OnItemCouponChanged += HandleItemCouponChanged;
        // 무료 리롤 자원이 늘면 골드가 그대로여도 리롤 버튼이 눌릴 수 있어야 한다.
        GameEvents.OnRerollCountChanged += HandleRerollCountChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnMatchRecorded -= HandleMatchRecorded;
        GameEvents.OnServerMatchesLoaded -= HandleServerMatchesLoaded;

        GameEvents.OnGoldChanged -= HandleGoldChanged;
        GameEvents.OnLevelChanged -= HandleLevelChanged;
        GameEvents.OnXpChanged -= HandleXpChanged;
        GameEvents.OnUnitCapChanged -= HandleUnitCapChanged;
        GameEvents.OnPhaseChanged -= HandlePhaseChanged;
        GameEvents.OnRerollCountChanged -= HandleRerollCountChanged;
        GameEvents.OnItemCouponChanged -= HandleItemCouponChanged;
    }

    private void Start()
    {
        RefreshMatchHistory();
        RefreshSampleDecks();
        SyncProgressState();
        BindCanvasShopControls();
        BindCanvasProgressTexts();
        RefreshCanvasProgressTexts();
    }

    private void OnDestroy()
    {
        UnbindCanvasShopControls();
    }

    private void LateUpdate()
    {
        // 씬 초기화 순서로 비활성 패널이 아직 검색되지 않은 프레임을 한 번 보완한다.
        if (!_canvasShopControlsReady)
            BindCanvasShopControls();

        if (!_canvasProgressTextsReady && BindCanvasProgressTexts())
            RefreshCanvasProgressTexts();
    }

    // ──────────────────────────────────────────
    // Canvas 진행 표시(레벨 / XP / 골드 / 쿠폰 / 코스트 확률)
    //
    // 이 텍스트들은 아트가 배치한 순수 TMP 오브젝트라 소유 스크립트가 없었다.
    // 인스펙터 배선을 요구하면 같은 씬을 건드리는 다른 브랜치와 충돌하므로,
    // 버튼과 동일하게 이름 기반으로 찾아 붙인다(SerializeField가 채워져 있으면 그쪽 우선).
    // ──────────────────────────────────────────

    /// <summary>진행 표시용 TMP 참조를 확보한다. 전부 찾았으면 true.</summary>
    private bool BindCanvasProgressTexts()
    {
        // UnityEngine.Object의 null 연산자는 파괴된 씬 오브젝트도 null로 취급하므로
        // 씬 재진입 뒤에는 ??= 가 아니라 == null 로 재검색해야 한다.
        if (_levelText == null)
            _levelText = FindSceneText("LevelText", "Level_Panel");

        if (_levelSubText == null)
            _levelSubText = FindSceneText("LevelSubText", "Level_Panel");

        // CoinText는 Coin_Panel(골드)과 Coupon_Panel(아이템 쿠폰)에 같은 이름으로 둘 있다.
        // 이름만으로는 구분되지 않으므로 반드시 부모까지 지정해 찾는다.
        if (_goldText == null)
            _goldText = FindSceneText("CoinText", "Coin_Panel");

        if (_couponText == null)
            _couponText = FindSceneText("CoinText", "Coupon_Panel");

        if (_costRateTexts == null || _costRateTexts.Length != CostTierCount)
            _costRateTexts = new TMP_Text[CostTierCount];

        for (int i = 0; i < CostTierCount; i++)
        {
            if (_costRateTexts[i] == null)
                _costRateTexts[i] = FindSceneText("RateText" + (i + 1), "RateText_Group");
        }

        bool ratesReady = true;
        for (int i = 0; i < CostTierCount; i++)
            if (_costRateTexts[i] == null) ratesReady = false;

        _canvasProgressTextsReady = _levelText != null && _levelSubText != null &&
            _goldText != null && _couponText != null && ratesReady;

        if (!_canvasProgressTextsReady && !_canvasProgressTextsWarningLogged)
        {
            Debug.LogWarning("[UIManager] Canvas 진행 표시 텍스트 일부를 찾지 못했습니다. " +
                             "Inspector 참조 또는 씬 오브젝트 이름을 확인하세요.");
            _canvasProgressTextsWarningLogged = true;
        }

        return _canvasProgressTextsReady;
    }

    /// <summary>캐시된 진행 상태를 Canvas 텍스트에 반영한다. 값이 바뀔 때만 호출한다.</summary>
    private void RefreshCanvasProgressTexts()
    {
        if (_levelText != null) _levelText.text = $"{_currentLevel}레벨";

        // 최대 레벨에서는 필요 XP가 0으로 내려오므로 분수 대신 MAX로 표기한다.
        if (_levelSubText != null)
            _levelSubText.text = _requiredXp > 0 ? $"{_currentXp}/{_requiredXp}" : "MAX";

        if (_goldText != null) _goldText.text = _gold.ToString();
        if (_couponText != null) _couponText.text = _itemCoupon.ToString();

        RefreshCostRateTexts();
        RefreshShopButtonAffordability();
    }

    /// <summary>
    /// 골드가 모자란 상점 버튼을 비활성으로 만든다.
    /// interactable=false면 클릭뿐 아니라 Hover(Highlighted) 전환도 함께 막힌다 —
    /// Selectable이 비활성 상태에서는 상태 전환 자체를 하지 않기 때문이다.
    ///
    /// 리롤은 무료 리롤 자원이 남아 있으면 골드와 무관하게 누를 수 있다.
    /// XP 구매는 최대 레벨(RequiredXp=0)에서도 의미가 없으므로 함께 막는다.
    /// </summary>
    private void RefreshShopButtonAffordability()
    {
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        if (shop == null) return;

        SetButtonAffordable(_shopRerollButton, shop.RerollCount > 0 || _gold >= shop.RerollCost);
        SetButtonAffordable(_xpPurchaseButton, _requiredXp > 0 && _gold >= shop.BuyXpCostGold);
    }

    /// <summary>
    /// 버튼을 켜고 끄면서 흑백 머티리얼도 함께 씌운다.
    /// Animator의 Disabled 상태와 중복돼도 문제없다 — 머티리얼은 색만 바꾸고 애니메이션은 형태를 다룬다.
    /// </summary>
    private void SetButtonAffordable(Button button, bool affordable)
    {
        if (button == null) return;

        button.interactable = affordable;

        if (_disabledGrayscaleMaterial == null) return;

        var graphic = button.targetGraphic;
        if (graphic != null)
            graphic.material = affordable ? null : _disabledGrayscaleMaterial;
    }

    /// <summary>현재 레벨의 코스트별 등장 확률을 1~5코스트 순으로 표시한다.</summary>
    private void RefreshCostRateTexts()
    {
        if (_costRateTexts == null) return;

        int[] rates = GameManager.TryGet(out var gm) && gm.Shop != null
            ? gm.Shop.GetCurrentCostRates()
            : null;

        for (int i = 0; i < _costRateTexts.Length; i++)
        {
            if (_costRateTexts[i] == null) continue;

            int rate = rates != null && i < rates.Length ? rates[i] : 0;
            _costRateTexts[i].text = $"· {rate}%";
        }
    }

    /// <summary>이름과 부모 이름으로 씬 안의 TMP 텍스트를 찾는다. 동명 오브젝트 구분용.</summary>
    private static TMP_Text FindSceneText(string name, string parentName)
    {
        foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (!IsInActiveScene(text.gameObject) || text.name != name)
                continue;

            var parent = text.transform.parent;
            if (parent != null && parent.name == parentName)
                return text;
        }

        return null;
    }

    /// <summary>
    /// 기존 Canvas 버튼은 persistent onClick이 비어 있으므로 UIManager가 런타임 리스너를 소유한다.
    /// SerializeField가 연결된 씬에서는 그 참조를 우선 사용하고, 기존 GameSceneTest 씬은 이름 기반으로 보완한다.
    /// </summary>
    private void BindCanvasShopControls()
    {
        UnbindCanvasShopControls();

        // UnityEngine.Object의 null 연산자는 파괴된 씬 오브젝트도 null로 취급한다.
        // ??= 는 그 연산자를 우회하므로, 씬 재진입 뒤에는 반드시 == null로 재검색한다.
        if (_xpPurchaseButton == null)
            _xpPurchaseButton = FindSceneButton("Level Button");

        if (_shopRerollButton == null)
            _shopRerollButton = FindSceneButton("ReRoll Button");

        // 오브젝트명 오타는 af9955d4에서 교정됨(BattleRaedy_Button -> BattleReady_Button).
        // 아직 교정 전 씬을 쓰는 브랜치가 남아 있어 구 이름도 폴백으로 둔다.
        if (_battleReadyButton == null)
        {
            _battleReadyButton = FindSceneButton("BattleReady_Button");

            if (_battleReadyButton == null)
                _battleReadyButton = FindSceneButton("BattleRaedy_Button");
        }

        if (_unitStorePanel == null)
            _unitStorePanel = FindSceneObject("UnitStore_Panel");

        if (_itemStorePanel == null)
            _itemStorePanel = FindSceneObject("ItemStore_Panel");

        if (_showItemStoreButton == null || _unitStorePanel == null ||
            !_showItemStoreButton.transform.IsChildOf(_unitStorePanel.transform))
        {
            _showItemStoreButton = FindSceneButton("ShopChange Button", _unitStorePanel);
        }

        if (_showUnitStoreButton == null || _itemStorePanel == null ||
            !_showUnitStoreButton.transform.IsChildOf(_itemStorePanel.transform))
        {
            _showUnitStoreButton = FindSceneButton("ShopChange Button", _itemStorePanel);
        }

        _xpPurchaseButton?.onClick.AddListener(HandleXpPurchaseButtonClicked);
        _shopRerollButton?.onClick.AddListener(HandleShopRerollButtonClicked);
        _battleReadyButton?.onClick.AddListener(HandleBattleReadyButtonClicked);
        _showItemStoreButton?.onClick.AddListener(ShowItemStore);
        _showUnitStoreButton?.onClick.AddListener(ShowUnitStore);

        _canvasShopControlsReady = _xpPurchaseButton != null && _shopRerollButton != null &&
            _battleReadyButton != null &&
            _showItemStoreButton != null && _showUnitStoreButton != null &&
            _unitStorePanel != null && _itemStorePanel != null;

        UpdateBattleReadyButtonState();
        // 버튼을 방금 찾았으므로 첫 골드 이벤트를 기다리지 않고 초기 활성 상태를 잡는다.
        RefreshShopButtonAffordability();

        if (!_canvasShopControlsReady && !_canvasShopControlsWarningLogged)
        {
            Debug.LogWarning("[UIManager] Canvas 상점 컨트롤 일부를 찾지 못했습니다. Inspector 참조 또는 씬 오브젝트 이름을 확인하세요.");
            _canvasShopControlsWarningLogged = true;
        }
    }

    private void UnbindCanvasShopControls()
    {
        _xpPurchaseButton?.onClick.RemoveListener(HandleXpPurchaseButtonClicked);
        _shopRerollButton?.onClick.RemoveListener(HandleShopRerollButtonClicked);
        _battleReadyButton?.onClick.RemoveListener(HandleBattleReadyButtonClicked);
        _showItemStoreButton?.onClick.RemoveListener(ShowItemStore);
        _showUnitStoreButton?.onClick.RemoveListener(ShowUnitStore);
    }

    private void HandleXpPurchaseButtonClicked() => GameEvents.RequestXpPurchase();

    private void HandleShopRerollButtonClicked() => GameEvents.RequestShopReroll();

    private void HandleBattleReadyButtonClicked()
    {
        if (_currentPhase != GamePhase.Shopping || _readyRequestSubmitted)
            return;

        _readyRequestSubmitted = true;
        UpdateBattleReadyButtonState();
        GameEvents.RequestPlayerReady();
    }

    private void HandlePhaseChanged(GamePhase phase)
    {
        _currentPhase = phase;
        if (phase == GamePhase.Shopping)
            _readyRequestSubmitted = false;

        UpdateBattleReadyButtonState();
    }

    private void UpdateBattleReadyButtonState()
    {
        if (_battleReadyButton != null)
            _battleReadyButton.interactable =
                _currentPhase == GamePhase.Shopping && !_readyRequestSubmitted;
    }

    private void ShowItemStore() => SetActiveShopPanel(showUnitStore: false);

    private void ShowUnitStore() => SetActiveShopPanel(showUnitStore: true);

    private void SetActiveShopPanel(bool showUnitStore)
    {
        if (_unitStorePanel != null)
            _unitStorePanel.SetActive(showUnitStore);

        if (_itemStorePanel != null)
            _itemStorePanel.SetActive(!showUnitStore);
    }

    private static Button FindSceneButton(string name, GameObject requiredParent = null)
    {
        foreach (Button button in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (!IsInActiveScene(button.gameObject) || button.name != name)
                continue;

            if (requiredParent == null || button.transform.IsChildOf(requiredParent.transform))
                return button;
        }

        return null;
    }

    private static GameObject FindSceneObject(string name)
    {
        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (IsInActiveScene(candidate) && candidate.name == name)
                return candidate;
        }

        return null;
    }

    private static bool IsInActiveScene(GameObject candidate)
    {
        return candidate != null && candidate.scene.IsValid() && candidate.scene == SceneManager.GetActiveScene();
    }


    /// <summary>
    /// UI가 이벤트 구독을 시작하기 전에 ShopManager가 초기 이벤트를 발행했을 가능성에 대비해
    /// 현재 진행 상태를 한 번만 직접 가져온다.
    ///
    /// 이 메서드는 Start에서 1회 실행되며, OnGUI에서 반복 호출하는 폴링이 아니다.
    /// 이후 골드/레벨/XP/유닛 캡 변화는 각 GameEvents 핸들러가 캐시를 갱신한다.
    /// </summary>
    private void SyncProgressState()
    {
        if (!GameManager.TryGet(out var gm) || gm.Shop == null)
            return;

        var shop = gm.Shop;

        _gold = shop.Gold;
        _currentLevel = shop.CurrentLevel;
        _currentXp = shop.CurrentXp;
        _requiredXp = shop.RequiredXp;
        _unitCap = shop.UnitCap;

        if (gm.Item != null)
            _itemCoupon = gm.Item.ItemCoupon;
    }

    /// <summary>골드 변경 이벤트를 받아 HUD 표시용 캐시를 갱신한다.</summary>
    private void HandleGoldChanged(int gold)
    {
        _gold = gold;
        RefreshCanvasProgressTexts();
    }

    /// <summary>무료 리롤 자원 변동. 표시값은 없고 리롤 버튼 활성 여부만 바뀐다.</summary>
    private void HandleRerollCountChanged(int _) => RefreshShopButtonAffordability();

    /// <summary>레벨 변경 이벤트를 받아 HUD 표시용 캐시를 갱신한다.</summary>
    private void HandleLevelChanged(int level)
    {
        _currentLevel = level;
        // 코스트 등장 확률은 레벨에 종속되므로 여기서 같이 다시 그린다.
        RefreshCanvasProgressTexts();
    }

    /// <summary>XP 변경 이벤트를 받아 현재 XP와 다음 레벨 필요 XP를 함께 갱신한다.</summary>
    private void HandleXpChanged(int currentXp, int requiredXp)
    {
        _currentXp = currentXp;
        _requiredXp = requiredXp;
        RefreshCanvasProgressTexts();
    }

    /// <summary>아이템 쿠폰 변경 이벤트를 받아 HUD 표시용 캐시를 갱신한다.</summary>
    private void HandleItemCouponChanged(int coupon)
    {
        _itemCoupon = coupon;
        RefreshCanvasProgressTexts();
    }

    /// <summary>레벨에 따른 보드 배치 가능 기물 수 변경을 HUD 캐시에 반영한다.</summary>
    private void HandleUnitCapChanged(int unitCap)
    {
        _unitCap = unitCap;
    }

    private void OnGUI()
    {
        EnsureStyles();

        DrawOpenButton();
        DrawSampleDeckOpenButton();

        // 골드/레벨/XP는 Canvas 하단 바(RefreshCanvasProgressTexts)가 표시한다.
        // IMGUI 진행 패널은 같은 값을 중복 표시하며 상점 카드 위를 덮으므로 호출하지 않는다.
        // 메서드는 Canvas 미배선 씬을 위한 폴백으로 남겨둔다.

        if (_showMatchHistory)
            DrawMatchHistoryWindow();

        if (_showSampleDeck)
            DrawSampleDeckWindow();
    }


    // ──────────────────────────────────────────
    // 플레이어 진행 HUD
    // ──────────────────────────────────────────

    /// <summary>
    /// 이벤트로 갱신된 진행 상태 캐시를 화면에 표시한다.
    /// 이 메서드는 표시 과정에서 ShopManager의 현재 상태를 직접 조회하지 않는다.
    /// XP 구매 입력은 Canvas 버튼이 담당하므로 여기서는 정보만 표시한다.
    /// </summary>
    private void DrawProgressPanel()
    {
        const float width = 250f;
        const float height = 100f;

        // PrototypeHud의 유닛 상점 배치값과 동일하게 계산한다.
        const int shopSlotCount = 5;
        const float shopSlotWidth = 110f;
        const float shopSlotGap = 6f;

        float shopTotalWidth = shopSlotCount * (shopSlotWidth + shopSlotGap);
        float shopStartX = (Screen.width - shopTotalWidth) / 2f;

        // 유닛 상점 왼쪽에 패널 배치.
        // 작은 해상도에서는 화면 밖으로 나가지 않도록 최소 10px을 보장한다.
        float x = Mathf.Max(10f, shopStartX - width - 20f);
        float y = Screen.height - 190f;

        GUI.Box(new Rect(x, y, width, height), "플레이어 정보");

        GUI.Label(
            new Rect(x + 15f, y + 28f, 220f, 22f),
            $"골드: {_gold} G"
        );

        GUI.Label(
            new Rect(x + 15f, y + 50f, 220f, 22f),
            $"레벨: {_currentLevel} · 배치 가능: {_unitCap}"
        );

        string xpText = _requiredXp > 0
            ? $"XP: {_currentXp} / {_requiredXp}"
            : "XP: MAX";

        GUI.Label(
            new Rect(x + 15f, y + 72f, 220f, 22f),
            xpText
        );

    }

    // ──────────────────────────────────────────
    // 전적창 UI
    // ──────────────────────────────────────────

    /// <summary>
    /// 화면 상단 중앙에 전적창 열기/닫기 버튼을 표시한다.
    /// 전적창을 열 때는 로컬 저장소의 최신 전적을 다시 불러온다.
    /// </summary>
    private void DrawOpenButton()
    {
        const float width = 160f;
        const float height = 40f;

        float x = (Screen.width - width) * 0.5f;
        float y = 20f;

        string label = _showMatchHistory ? "전적창 닫기" : "전적창 열기";

        if (GUI.Button(new Rect(x, y, width, height), label))
        {
            _showMatchHistory = !_showMatchHistory;

            if (_showMatchHistory)
                RefreshMatchHistory();
        }
    }

    /// <summary>
    /// 이동 가능한 전적창 윈도우를 생성한다.
    /// 창이 화면 밖으로 완전히 벗어나지 않도록 현재 위치를 보정한다.
    /// </summary>
    private void DrawMatchHistoryWindow()
    {
        _matchHistoryWindowRect.x = Mathf.Clamp(_matchHistoryWindowRect.x, 0f, Screen.width - 160f);
        _matchHistoryWindowRect.y = Mathf.Clamp(_matchHistoryWindowRect.y, 0f, Screen.height - 120f);

        _matchHistoryWindowRect = GUI.Window(
            1001,
            _matchHistoryWindowRect,
            DrawMatchHistoryWindowContent,
            "전적"
        );
    }

    /// <summary>
    /// 전적창 내부 전체 구성을 그린다.
    /// 상단 요약, 왼쪽 전적 목록, 오른쪽 선택 전적 상세 영역으로 구성된다.
    /// 저장된 전적이 없으면 빈 상태 안내만 표시한다.
    /// </summary>
    private void DrawMatchHistoryWindowContent(int windowId)
    {
        float width = _matchHistoryWindowRect.width;
        float height = _matchHistoryWindowRect.height;

        DrawHeader(width);

        if (_recentMatches.Count == 0)
        {
            string emptyText = _historySource == HistorySource.Server
                ? (string.IsNullOrEmpty(_serverStatus) ? "서버에 저장된 전적이 없습니다." : _serverStatus)
                : "저장된 전적이 없습니다.";
            GUI.Label(new Rect(28f, 120f, 700f, 30f), emptyText, _labelStyle);
            GUI.DragWindow(new Rect(0f, 0f, width, 28f));
            return;
        }

        _selectedMatchIndex = Mathf.Clamp(_selectedMatchIndex, 0, _recentMatches.Count - 1);

        // 왼쪽: 최근 전적 목록
        DrawMatchList(new Rect(24f, 120f, 300f, height - 145f));
        // 오른쪽: 현재 선택한 전적의 상세 정보
        DrawMatchDetail(new Rect(340f, 120f, width - 365f, height - 145f), _recentMatches[_selectedMatchIndex]);

        GUI.DragWindow(new Rect(0f, 0f, width, 28f));
    }

    /// <summary>
    /// 견본덱 장비 Hover 또는 클릭 고정 상태에 따라
    /// 작은 독립 장비 툴팁을 표시한다.
    /// </summary>
    private void DrawSampleDeckItemTooltip()
    {
        string itemName =
            !string.IsNullOrWhiteSpace(_pinnedSampleDeckItemName)
                ? _pinnedSampleDeckItemName
                : _hoveredSampleDeckItemName;

        if (string.IsNullOrWhiteSpace(itemName))
            return;

        ItemData item = FindItemByDisplayName(itemName);

        string description = item != null
            ? GetSampleDeckItemDescription(item)
            : "ItemDatabase에서 장비 정보를 찾지 못했습니다.";

        const float tooltipWidth = 300f;
        const float minimumHeight = 130f;
        const float maximumHeight = 320f;

        const float offsetX = 12f;
        const float offsetY = 4f;

        GUIStyle descriptionStyle = new GUIStyle(_mutedStyle)
        {
            wordWrap = true,
            clipping = TextClipping.Clip
        };

        float descriptionHeight = descriptionStyle.CalcHeight(
            new GUIContent(description),
            tooltipWidth - 24f
        );

        float tooltipHeight = Mathf.Clamp(
            70f + descriptionHeight,
            minimumHeight,
            maximumHeight
        );

        // 화면 좌표로 저장한 기준점을
        // 현재 견본덱 GUI.Window 내부 좌표로 다시 변환한다.
        Vector2 anchorPosition =
            GUIUtility.ScreenToGUIPoint(
                _sampleDeckItemTooltipAnchorScreenPosition
            );

        float x = anchorPosition.x + offsetX;
        float y = anchorPosition.y + offsetY;

        // 오른쪽 경계를 넘으면 장비 항목의 왼쪽에 표시한다.
        if (x + tooltipWidth > _sampleDeckWindowRect.width - 15f)
        {
            x =
                anchorPosition.x -
                tooltipWidth -
                offsetX;
        }

        x = Mathf.Clamp(
            x,
            15f,
            _sampleDeckWindowRect.width - tooltipWidth - 15f
        );

        y = Mathf.Clamp(
            y,
            32f,
            _sampleDeckWindowRect.height - tooltipHeight - 15f
        );

        Rect tooltipRect = new Rect(
            x,
            y,
            tooltipWidth,
            tooltipHeight
        );

        GUI.Box(tooltipRect, GUIContent.none, _itemTooltipStyle);

        GUI.Label(
            new Rect(
                tooltipRect.x + 12f,
                tooltipRect.y + 9f,
                tooltipRect.width - 24f,
                26f
            ),
            itemName,
            _sectionTitleStyle
        );

        if (!string.IsNullOrWhiteSpace(_pinnedSampleDeckItemName))
        {
            GUI.Label(
                new Rect(
                    tooltipRect.x + tooltipRect.width - 58f,
                    tooltipRect.y + 12f,
                    46f,
                    18f
                ),
                "고정",
                _winStyle
            );
        }

        GUI.Box(
            new Rect(
                tooltipRect.x + 10f,
                tooltipRect.y + 41f,
                tooltipRect.width - 20f,
                1f
            ),
            ""
        );

        GUI.Label(
            new Rect(
                tooltipRect.x + 12f,
                tooltipRect.y + 50f,
                tooltipRect.width - 24f,
                tooltipRect.height - 62f
            ),
            description,
            descriptionStyle
        );
    }

    /// <summary>
    /// 전적창 헤더를 표시한다.
    /// 최근 전적 수와 해당 목록 기준 승률을 계산해 함께 보여준다.
    /// </summary>
    private void DrawHeader(float width)
    {
        GUI.Label(new Rect(24f, 32f, 220f, 36f), "전적", _titleStyle);
        GUI.Label(new Rect(24f, 67f, 420f, 24f), "최근 플레이 기록 · 카드를 누르면 상세가 열립니다", _mutedStyle);

        int total = _recentMatches.Count;
        int winCount = CountWins();
        int winRate = total > 0 ? Mathf.RoundToInt((float)winCount / total * 100f) : 0;

        GUI.Label(new Rect(width - 210f, 34f, 60f, 24f), total.ToString(), _sectionTitleStyle);
        GUI.Label(new Rect(width - 205f, 60f, 80f, 20f), "표시 전적", _mutedStyle);

        GUI.Label(new Rect(width - 120f, 34f, 80f, 24f), $"{winRate}%", _sectionTitleStyle);
        GUI.Label(new Rect(width - 110f, 60f, 80f, 20f), "승률", _mutedStyle);

        if (GUI.Button(new Rect(width - 100f, 84f, 70f, 28f), "닫기"))
            _showMatchHistory = false;

        if (GUI.Button(new Rect(width - 180f, 84f, 70f, 28f), "새로고침"))
            RefreshMatchHistory();

        // 전적 출처 탭 (Phase 3): 로컬 jsonl ↔ Supabase 서버. 전환 시 즉시 다시 불러온다.
        DrawSourceToggle(new Rect(24f, 84f, 150f, 28f));
    }

    /// <summary>로컬/서버 전적 출처 토글 버튼 두 개를 그린다. 현재 선택은 비활성 표시.</summary>
    private void DrawSourceToggle(Rect rect)
    {
        float half = rect.width / 2f;

        GUI.enabled = _historySource != HistorySource.Local;
        if (GUI.Button(new Rect(rect.x, rect.y, half - 2f, rect.height), "로컬"))
        {
            _historySource = HistorySource.Local;
            RefreshMatchHistory();
        }

        GUI.enabled = _historySource != HistorySource.Server;
        if (GUI.Button(new Rect(rect.x + half + 2f, rect.y, half - 2f, rect.height), "서버"))
        {
            _historySource = HistorySource.Server;
            RefreshMatchHistory();
        }

        GUI.enabled = true;
    }

    /// <summary>
    /// 최근 전적 목록을 스크롤 영역으로 표시한다.
    /// 각 전적은 선택 가능한 카드 형태로 그려진다.
    /// </summary>
    private void DrawMatchList(Rect rect)
    {
        GUI.Box(rect, "");

        float contentHeight = Mathf.Max(rect.height - 20f, _recentMatches.Count * 92f + 10f);

        _listScroll = GUI.BeginScrollView(
            rect,
            _listScroll,
            new Rect(0f, 0f, rect.width - 20f, contentHeight)
        );

        for (int i = 0; i < _recentMatches.Count; i++)
        {
            DrawMatchListCard(_recentMatches[i], i, new Rect(8f, 8f + i * 92f, rect.width - 36f, 82f));
        }

        GUI.EndScrollView();
    }

    /// <summary>
    /// 전적 목록의 카드 한 장을 표시한다.
    /// 승패, 최종 스테이지, 라운드, 플레이 모드, 플레이 시간과 경과 시간을 요약한다.
    /// 카드를 누르면 해당 전적이 상세 영역의 선택 대상으로 지정된다.
    /// </summary>
    private void DrawMatchListCard(MatchRecord record, int index, Rect rect)
    {
        if (record == null)
            return;

        bool selected = index == _selectedMatchIndex;
        GUI.Box(rect, "", selected ? _selectedListCardStyle : _normalListCardStyle);

        if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            _selectedMatchIndex = index;

        bool isWin = IsVictory(record);
        string resultText = isWin ? "승리" : "패배";
        string stageText = string.IsNullOrWhiteSpace(record.finalStageId) ? "-" : record.finalStageId;
        string modeText = GetPlayModeText(record);
        string durationText = FormatDurationShort(record.durationSeconds);
        string relativeText = FormatRelativeTime(record.endedAtUtc);

        GUI.Label(
            new Rect(rect.x + 12f, rect.y + 10f, 80f, 22f),
            resultText,
            isWin ? _winStyle : _loseStyle
        );

        GUI.Label(
            new Rect(rect.x + rect.width - 95f, rect.y + 10f, 80f, 22f),
            $"{stageText}  R{record.finalRound}",
            _smallStyle
        );

        GUI.Label(
            new Rect(rect.x + 12f, rect.y + 38f, 190f, 20f),
            $"{modeText} · {durationText}",
            _smallStyle
        );

        GUI.Label(
            new Rect(rect.x + rect.width - 85f, rect.y + 38f, 75f, 20f),
            relativeText,
            _mutedStyle
        );

        if (IsDisconnectedMatch(record))
        {
            GUI.Label(
                new Rect(rect.x + 12f, rect.y + 58f, 120f, 20f),
                "⚠ 연결 끊김",
                _loseStyle
            );
        }
    }

    /// <summary>
    /// 현재 선택한 전적의 상세 정보를 표시한다.
    /// 경기 결과와 본인·파트너 기록을 출력하고,
    /// 하단에는 개발 확인용 매치 메타 정보를 표시한다.
    /// </summary>
    private void DrawMatchDetail(Rect rect, MatchRecord record)
    {
        GUI.Box(rect, "");

        bool isWin = IsVictory(record);
        string resultText = isWin ? "승리" : "패배";
        string stageText = string.IsNullOrWhiteSpace(record.finalStageId) ? "-" : record.finalStageId;
        string durationText = FormatDurationShort(record.durationSeconds);
        string modeText = GetPlayModeText(record);

        GUI.Label(
            new Rect(rect.x + 22f, rect.y + 18f, 130f, 36f),
            resultText,
            isWin ? _winStyle : _loseStyle
        );
        if (IsDisconnectedMatch(record))
        {
            GUI.Label(
                new Rect(rect.x + 22f, rect.y + 58f, 160f, 22f),
                "⚠ 연결 끊김",
                _loseStyle
            );
        }

        const float summaryColumnWidth = 90f;
        const float summaryStartXOffset = 285f;

        float summaryStartX =
            rect.x + rect.width - summaryStartXOffset;

        float summaryValueY = rect.y + 20f;
        float summaryLabelY = rect.y + 47f;

        // 최종 스테이지
        GUI.Label(
            new Rect(
                summaryStartX,
                summaryValueY,
                summaryColumnWidth,
                24f
            ),
            stageText,
            _sectionTitleStyle
        );

        GUI.Label(
            new Rect(
                summaryStartX,
                summaryLabelY,
                summaryColumnWidth,
                20f
            ),
            "최종 스테이지",
            _mutedStyle
        );

        // 플레이 시간
        GUI.Label(
            new Rect(
                summaryStartX + summaryColumnWidth,
                summaryValueY,
                summaryColumnWidth,
                24f
            ),
            durationText,
            _sectionTitleStyle
        );

        GUI.Label(
            new Rect(
                summaryStartX + summaryColumnWidth,
                summaryLabelY,
                summaryColumnWidth,
                20f
            ),
            "플레이 시간",
            _mutedStyle
        );

        // 모드
        GUI.Label(
            new Rect(
                summaryStartX + summaryColumnWidth * 2f,
                summaryValueY,
                summaryColumnWidth,
                24f
            ),
            modeText,
            _sectionTitleStyle
        );

        GUI.Label(
            new Rect(
                summaryStartX + summaryColumnWidth * 2f,
                summaryLabelY,
                summaryColumnWidth,
                20f
            ),
            "모드",
            _mutedStyle
        );

        GUI.Box(new Rect(rect.x, rect.y + 82f, rect.width, 1f), "");

        Rect scrollRect = new Rect(rect.x + 18f, rect.y + 98f, rect.width - 36f, rect.height - 120f);
        Rect contentRect = new Rect(0f, 0f, scrollRect.width - 20f, GetDetailContentHeight(record));

        _detailScroll = GUI.BeginScrollView(scrollRect, _detailScroll, contentRect);

        float y = 0f;

        y = DrawPlayerSection(record.self, "나", y);

        if (HasPartner(record))
        {
            y += 18f;
            y = DrawPlayerSection(record.partner, "파트너", y);
        }
        else
        {
            y += 18f;
            GUI.Box(new Rect(0f, y, contentRect.width - 5f, 42f), "");
            GUI.Label(new Rect(12f, y + 11f, 520f, 20f), "파트너 기록 없음 — 솔로 플레이로 표시됩니다.", _mutedStyle);
            y += 52f;
        }

        // 개발/디버그용 전적 메타 정보.
        // - matchId: 전적 1건의 고유 ID
        // - gameVersion: 저장 당시 게임 버전
        // - endedAtUtc: 전적 종료 날짜
        // 실제 유저용 전적창에서는 없어도 되는 정보이므로,
        // 화면을 더 깔끔하게 만들고 싶으면 아래 GUI.Label 블록은 삭제해도 된다.
        y += 10f;
        GUI.Label(
            new Rect(0f, y, contentRect.width - 5f, 22f),
            $"match {SafeText(record.matchId)} · v{SafeText(record.gameVersion)} · {FormatDate(record.endedAtUtc)}",
            _mutedStyle
        );

        GUI.EndScrollView();
    }

    /// <summary>
    /// 본인 또는 파트너 한 명의 전적 정보를 표시한다.
    /// 닉네임, 레벨, 보드 유닛, 활성 시너지와 증강 정보를 순서대로 그린다.
    /// 반환값은 다음 UI 요소를 배치할 Y 좌표다.
    /// </summary>
    private float DrawPlayerSection(PlayerRecord player, string roleLabel, float y)
    {
        if (player == null)
        {
            GUI.Label(new Rect(0f, y, 400f, 24f), $"{roleLabel}: 기록 없음", _mutedStyle);
            return y + 34f;
        }

        string nickname = string.IsNullOrWhiteSpace(player.nickname) ? "-" : player.nickname;
        int level = player.level;

        GUI.Label(new Rect(0f, y, 58f, 24f), roleLabel, _badgeStyle);
        GUI.Label(new Rect(68f, y, 260f, 24f), nickname, _sectionTitleStyle);
        GUI.Label(new Rect(500f, y, 70f, 24f), $"Lv {level}", _badgeStyle);

        y += 36f;

        y = DrawUnitCards(player.board, y);

        y += 12f;

        GUI.Label(new Rect(0f, y, 120f, 20f), "활성 시너지", _mutedStyle);
        y += 24f;
        y = DrawTextBadges(player.activeSynergies, "시너지 없음", y);

        y += 12f;

        GUI.Label(new Rect(0f, y, 120f, 20f), "증강", _mutedStyle);
        y += 24f;
        y = DrawTextBadges(player.augments, "증강 없음", y);

        return y + 8f;
    }

    /// <summary>
    /// 전적에 저장된 보드 유닛들을 카드 형태로 배치한다.
    /// 한 줄에 최대 6개를 표시하며, 반환값은 다음 요소를 배치할 Y 좌표다.
    /// </summary>
    private float DrawUnitCards(UnitRecord[] units, float y)
    {
        if (units == null || units.Length == 0)
        {
            GUI.Box(new Rect(0f, y, 520f, 42f), "");
            GUI.Label(new Rect(12f, y + 11f, 420f, 20f), "보드 유닛 없음", _mutedStyle);
            return y + 52f;
        }

        const float cardWidth = 86f;
        const float cardHeight = 68f;
        const float gap = 8f;
        const int cardsPerRow = 6;

        for (int i = 0; i < units.Length; i++)
        {
            int row = i / cardsPerRow;
            int col = i % cardsPerRow;

            float x = col * (cardWidth + gap);
            float cardY = y + row * (cardHeight + gap);

            DrawUnitCard(units[i], new Rect(x, cardY, cardWidth, cardHeight));
        }

        int rowCount = Mathf.CeilToInt(units.Length / (float)cardsPerRow);
        return y + rowCount * (cardHeight + gap);
    }

    /// <summary>
    /// 전적 유닛 한 마리의 이름, 성급과 장착 아이템 슬롯을 표시한다.
    /// </summary>
    private void DrawUnitCard(UnitRecord unit, Rect rect)
    {
        if (unit == null)
            return;

        GUI.Box(rect, "", _unitCardStyle);

        string unitName = string.IsNullOrWhiteSpace(unit.nameEn) ? "Unknown" : unit.nameEn;
        GUI.Label(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, 20f), unitName, _smallStyle);

        string stars = FormatStars(unit.star);
        GUI.Label(new Rect(rect.x + 6f, rect.y + 27f, rect.width - 12f, 18f), stars, _winStyle);

        DrawItemSlots(unit, new Rect(rect.x + 6f, rect.y + 48f, rect.width - 12f, 14f));
    }

    /// <summary>
    /// 유닛의 일반 아이템 슬롯 3개와 진화의 돌 정보를 표시한다.
    /// 현재 OnGUI MVP에서는 아이템 이름을 간단한 텍스트로 함께 보여준다.
    /// </summary>
    private void DrawItemSlots(UnitRecord unit, Rect rect)
    {
        const int slotCount = 3;
        const float slotSize = 11f;
        const float gap = 4f;

        for (int i = 0; i < slotCount; i++)
        {
            Rect slotRect = new Rect(rect.x + i * (slotSize + gap), rect.y, slotSize, slotSize);

            string itemName = GetItemNameAt(unit, i);
            bool hasItem = !string.IsNullOrWhiteSpace(itemName);

            GUI.Box(slotRect, hasItem ? "●" : "");

            if (hasItem)
            {
                Rect tooltipRect = new Rect(slotRect.x - 2f, slotRect.y - 18f, 120f, 18f);
                GUI.Label(tooltipRect, itemName, _mutedStyle);
            }
        }

        if (!string.IsNullOrWhiteSpace(unit.stoneEn))
        {
            GUI.Label(new Rect(rect.x + 56f, rect.y - 2f, 90f, 18f), $"돌:{unit.stoneEn}", _mutedStyle);
        }
    }

    /// <summary>
    /// 시너지나 증강 문자열 배열을 여러 개의 배지 형태로 줄바꿈해 표시한다.
    /// 값이 없으면 전달받은 빈 상태 문구를 대신 표시한다.
    /// 반환값은 다음 요소를 배치할 Y 좌표다.
    /// </summary>
    private float DrawTextBadges(string[] values, string emptyText, float y)
    {
        if (values == null || values.Length == 0)
        {
            GUI.Label(new Rect(0f, y, 300f, 20f), emptyText, _mutedStyle);
            return y + 24f;
        }

        float x = 0f;
        float maxWidth = 540f;
        float lineHeight = 28f;

        for (int i = 0; i < values.Length; i++)
        {
            string text = string.IsNullOrWhiteSpace(values[i]) ? "-" : values[i];
            float badgeWidth = Mathf.Clamp(text.Length * 12f + 28f, 64f, 160f);

            if (x + badgeWidth > maxWidth)
            {
                x = 0f;
                y += lineHeight;
            }

            GUI.Label(new Rect(x, y, badgeWidth, 22f), text, _badgeStyle);
            x += badgeWidth + 8f;
        }

        return y + lineHeight;
    }

    // ──────────────────────────────────────────
    // 전적 데이터 갱신 / 실시간 반영
    // ──────────────────────────────────────────

    /// <summary>
    /// 로컬 전적 저장소에서 최근 전적을 다시 불러온다.
    /// 기존 목록을 교체하고 현재 선택 인덱스가 유효한 범위에 있도록 보정한다.
    /// </summary>
    private void RefreshMatchHistory()
    {
        // 서버 탭이면 이벤트로 요청하고 응답(HandleServerMatchesLoaded)에서 목록을 채운다.
        if (_historySource == HistorySource.Server)
        {
            _recentMatches.Clear();
            _selectedMatchIndex = 0;
            _serverStatus = "서버 전적 불러오는 중…";
            GameEvents.RequestServerMatches(_recentMatchCount);
            return;
        }

        _recentMatches.Clear();

        List<MatchRecord> records = MatchHistoryStore.LoadRecent(_recentMatchCount);
        if (records == null)
            return;

        _recentMatches.AddRange(records);

        if (_recentMatches.Count == 0)
            _selectedMatchIndex = 0;
        else
            _selectedMatchIndex = Mathf.Clamp(_selectedMatchIndex, 0, _recentMatches.Count - 1);
    }

    /// <summary>
    /// 서버 전적 조회 결과 수신. 서버 탭일 때만 목록에 반영한다
    /// (요청 후 로컬 탭으로 돌아간 경우 로컬 목록을 덮어쓰지 않도록).
    /// </summary>
    private void HandleServerMatchesLoaded(IReadOnlyList<MatchRecord> records, string error)
    {
        if (_historySource != HistorySource.Server)
            return;

        _recentMatches.Clear();
        _selectedMatchIndex = 0;

        if (records == null)
        {
            _serverStatus = string.IsNullOrEmpty(error) ? "서버 전적 조회 실패" : error;
            return;
        }

        _recentMatches.AddRange(records);
        _serverStatus = _recentMatches.Count == 0 ? "서버에 저장된 전적이 없습니다." : "";
    }

    /// <summary>
    /// 새 전적 기록 이벤트를 받아 목록 가장 앞에 즉시 추가한다.
    /// 설정된 최대 표시 개수를 초과한 오래된 전적은 목록 끝에서 제거한다.
    /// </summary>
    private void HandleMatchRecorded(MatchRecord record)
    {
        if (record == null)
            return;

        // 서버 탭은 조회 시점 스냅샷 — 신규 기록은 로컬 탭에만 즉시 반영(서버는 새로고침으로).
        if (_historySource != HistorySource.Local)
            return;

        _recentMatches.Insert(0, record);

        while (_recentMatches.Count > _recentMatchCount)
            _recentMatches.RemoveAt(_recentMatches.Count - 1);

        _selectedMatchIndex = 0;
    }

    // ──────────────────────────────────────────
    // OnGUI 스타일 생성
    // ──────────────────────────────────────────

    /// <summary>
    /// 전적창에서 사용하는 OnGUI 스타일을 최초 1회 생성해 캐시한다.
    /// OnGUI가 반복 호출될 때마다 GUIStyle 객체를 다시 만들지 않도록 한다.
    /// </summary>
    private void EnsureStyles()
    {
        if (_titleStyle != null)
            return;

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        _sectionTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            normal = { textColor = Color.white }
        };

        _smallStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            normal = { textColor = Color.white }
        };

        _mutedStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = new Color(0.75f, 0.78f, 0.88f) }
        };

        _winStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Overflow,
            normal =
            {
                textColor = new Color(1f,0.72f,0.18f)
            }
        };

        _loseStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.78f, 0.82f, 0.92f) }
        };

        _badgeStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        _unitCardStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 12,
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = Color.white }
        };

        _selectedListCardStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { textColor = Color.white }
        };

        _normalListCardStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { textColor = Color.white }
        };

        _itemTooltipBackground = new Texture2D(1, 1);
        _itemTooltipBackground.SetPixel(
            0,
            0,
            new Color(0.08f, 0.08f, 0.08f, 1f)
        );
        _itemTooltipBackground.Apply();

        _itemTooltipStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(12, 12, 10, 10),
            normal =
            {
                background = _itemTooltipBackground,
                textColor = Color.white
            }
                };
    }

    // ──────────────────────────────────────────
    // 전적 표시용 계산 / 포맷 유틸리티
    // ──────────────────────────────────────────

    private int CountWins()
    {
        int count = 0;

        for (int i = 0; i < _recentMatches.Count; i++)
        {
            if (IsVictory(_recentMatches[i]))
                count++;
        }

        return count;
    }

    private static bool IsVictory(MatchRecord record)
    {
        return record != null &&
               !string.IsNullOrWhiteSpace(record.result) &&
               record.result.Equals("Victory", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 파트너 데이터에 닉네임, 보드, 시너지 또는 증강 중 하나라도 있으면
    /// 팀플레이 전적으로 판단한다.
    /// </summary>
    private static bool HasPartner(MatchRecord record)
    {
        if (record == null || record.partner == null)
            return false;

        bool hasNickname = !string.IsNullOrWhiteSpace(record.partner.nickname);
        bool hasBoard = record.partner.board != null && record.partner.board.Length > 0;
        bool hasSynergy = record.partner.activeSynergies != null && record.partner.activeSynergies.Length > 0;
        bool hasAugments = record.partner.augments != null && record.partner.augments.Length > 0;

        return hasNickname || hasBoard || hasSynergy || hasAugments;
    }

    private static string GetPlayModeText(MatchRecord record)
    {
        return HasPartner(record) ? "팀플" : "솔로";
    }

    private static string GetItemNameAt(UnitRecord unit, int index)
    {
        if (unit == null || unit.itemsEn == null)
            return "";

        if (index < 0 || index >= unit.itemsEn.Length)
            return "";

        return unit.itemsEn[index];
    }

    private static string FormatStars(int star)
    {
        if (star <= 0)
            return "-";

        if (star > 5)
            star = 5;

        return new string('★', star);
    }

    private static string FormatDurationShort(int seconds)
    {
        if (seconds <= 0)
            return "-";

        int minutes = seconds / 60;

        if (minutes <= 0)
            return $"{seconds}초";

        return $"{minutes}분";
    }

    private static string FormatRelativeTime(string utcText)
    {
        if (string.IsNullOrWhiteSpace(utcText))
            return "-";

        if (!DateTime.TryParse(utcText, out DateTime utcTime))
            return "-";

        TimeSpan diff = DateTime.UtcNow - utcTime.ToUniversalTime();

        if (diff.TotalMinutes < 1)
            return "방금 전";

        if (diff.TotalHours < 1)
            return $"{Mathf.FloorToInt((float)diff.TotalMinutes)}분 전";

        if (diff.TotalDays < 1)
            return $"{Mathf.FloorToInt((float)diff.TotalHours)}시간 전";

        if (diff.TotalDays < 7)
            return $"{Mathf.FloorToInt((float)diff.TotalDays)}일 전";

        return utcTime.ToLocalTime().ToString("MM-dd");
    }

    private static string FormatDate(string utcText)
    {
        if (string.IsNullOrWhiteSpace(utcText))
            return "-";

        if (!DateTime.TryParse(utcText, out DateTime utcTime))
            return utcText;

        return utcTime.ToLocalTime().ToString("yyyy-MM-dd");
    }

    private static string SafeText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    /// <summary>
    /// 본인과 파트너의 보드 유닛 수를 기준으로 상세 스크롤 콘텐츠 높이를 계산한다.
    /// </summary>
    private static float GetDetailContentHeight(MatchRecord record)
    {
        float height = 360f;

        if (record != null && record.self != null && record.self.board != null)
        {
            int rows = Mathf.CeilToInt(record.self.board.Length / 6f);
            height += rows * 80f;
        }

        if (HasPartner(record) && record.partner != null && record.partner.board != null)
        {
            int rows = Mathf.CeilToInt(record.partner.board.Length / 6f);
            height += rows * 80f;
        }

        return Mathf.Max(520f, height);
    }

    /// <summary>
    /// 저장된 종료 사유 문자열에 연결 종료 관련 키워드가 포함됐는지 확인한다.
    /// 과거 기록의 서로 다른 종료 사유 표현을 호환하기 위해 여러 키워드를 검사한다.
    /// </summary>
    private static bool IsDisconnectedMatch(MatchRecord record)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.endReason))
            return false;

        string reason = record.endReason.ToLower();

        return reason.Contains("disconnect") ||
               reason.Contains("connection") ||
               reason.Contains("network") ||
               reason.Contains("leave") ||
               reason.Contains("left") ||
               reason.Contains("quit") ||
               reason.Contains("timeout") ||
               reason.Contains("lost") ||
               reason.Contains("연결") ||
               reason.Contains("끊김");
    }

    /// <summary>
    /// 한글 또는 영문 장비 이름으로 ItemDatabase에서 장비를 찾는다.
    /// </summary>
    private static ItemData FindItemByDisplayName(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return null;

        ItemDatabase db = ItemDatabase.Instance;

        if (db == null || db.all == null)
            return null;

        for (int i = 0; i < db.all.Count; i++)
        {
            ItemData item = db.all[i];

            if (item == null)
                continue;

            bool matchesKoreanName =
                !string.IsNullOrWhiteSpace(item.itemName) &&
                item.itemName.Equals(
                    itemName,
                    StringComparison.OrdinalIgnoreCase
                );

            bool matchesEnglishName =
                !string.IsNullOrWhiteSpace(item.itemNameEn) &&
                item.itemNameEn.Equals(
                    itemName,
                    StringComparison.OrdinalIgnoreCase
                );

            if (matchesKoreanName || matchesEnglishName)
                return item;
        }

        return null;
    }

    /// <summary>
    /// ItemData에서 실제 값이 있는 효과만 모아
    /// 견본덱 장비 툴팁에 표시할 문자열을 만든다.
    /// </summary>
    private static string GetSampleDeckItemDescription(ItemData item)
    {
        if (item == null)
            return "장비 정보 없음";

        var lines = new List<string>();

        lines.Add($"ID: {item.id}");

        if (!string.IsNullOrWhiteSpace(item.itemNameEn))
            lines.Add($"영문명: {item.itemNameEn}");

        if (!string.IsNullOrWhiteSpace(item.description))
        {
            lines.Add("");
            lines.Add(item.description.Trim());
        }

        var effects = new List<string>();

        AddFlatEffect(effects, "체력", item.hpBonus);
        AddPercentEffect(effects, "최대 체력", item.maxHpPct);
        AddPercentEffect(effects, "초당 최대 체력 회복", item.hpRegenPercent);
        AddPercentEffect(effects, "피격 피해 흡수", item.healTakenDmgPct);
        AddPercentEffect(effects, "치명적 피해 시 보호막", item.shieldPctOnFatalHit);

        AddFlatEffect(effects, "공격력", item.attackBonus);
        AddPercentEffect(effects, "특수 공격력", item.spAtkPct);
        AddPercentEffect(effects, "공격속도", item.attackSpeedBonus);
        AddPercentEffect(effects, "처치 시 이동속도", item.moveSpdPctOnKill);

        AddFlatEffect(effects, "방어력", item.defenseBonus);
        AddFlatEffect(effects, "특수방어력", item.spDefBonus);
        AddPercentEffect(effects, "물리 피해 반사", item.reflectPhysPct);
        AddPercentEffect(effects, "특수 피해 반사", item.reflectSpPct);

        if (!Mathf.Approximately(item.defSpDefPerAttacker, 0f))
        {
            effects.Add(
                $"공격 중인 적 1명당 방어력·특수방어력 " +
                FormatSignedValue(item.defSpDefPerAttacker)
            );
        }

        AddPercentEffect(effects, "치명타 확률", item.criPct);
        AddPercentEffect(effects, "치명타 피해", item.criDmgPct);

        if (item.burnNearOnPhysHit)
            effects.Add("물리 피격 시 주변 적에게 화상");

        if (item.ccImmune)
            effects.Add("첫 군중 제어 효과 무효화");

        if (effects.Count > 0)
        {
            lines.Add("");
            lines.Add("보유 효과");

            for (int i = 0; i < effects.Count; i++)
                lines.Add($"• {effects[i]}");
        }

        return string.Join("\n", lines);
    }
    private static void AddFlatEffect(
    List<string> effects,
    string label,
    float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        effects.Add($"{label} {FormatSignedValue(value)}");
    }

    private static void AddPercentEffect(
        List<string> effects,
        string label,
        float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        effects.Add($"{label} {FormatSignedValue(value)}%");
    }

    private static string FormatSignedValue(float value)
    {
        string formatted = Mathf.Approximately(value, Mathf.Round(value))
            ? Mathf.RoundToInt(value).ToString()
            : value.ToString("0.##");

        return value > 0f
            ? $"+{formatted}"
            : formatted;
    }


    // ──────────────────────────────────────────
    // 견본덱 UI
    // ──────────────────────────────────────────

    /// <summary>
    /// 화면 상단에 견본덱 열기/닫기 버튼을 표시한다.
    /// 견본덱을 열면 전적창은 닫아 두 창이 겹치지 않게 한다.
    /// </summary>
    private void DrawSampleDeckOpenButton()
    {
        const float width = 160f;
        const float height = 40f;

        float x = (Screen.width - width) * 0.5f + 175f;
        float y = 20f;

        string label = _showSampleDeck ? "견본덱 닫기" : "견본덱 열기";

        if (GUI.Button(new Rect(x, y, width, height), label))
        {
            _showSampleDeck = !_showSampleDeck;

            if (_showSampleDeck)
            {
                _showMatchHistory = false;
                RefreshSampleDecks();
            }
        }
    }

    /// <summary>
    /// 이동 가능한 견본덱 창을 생성한다.
    /// </summary>
    private void DrawSampleDeckWindow()
    {
        _sampleDeckWindowRect.x = Mathf.Clamp(
            _sampleDeckWindowRect.x,
            0f,
            Screen.width - 160f
        );

        _sampleDeckWindowRect.y = Mathf.Clamp(
            _sampleDeckWindowRect.y,
            0f,
            Screen.height - 120f
        );

        _sampleDeckWindowRect = GUI.Window(
            1002,
            _sampleDeckWindowRect,
            DrawSampleDeckWindowContent,
            "견본덱"
        );
    }

    /// <summary>
    /// 왼쪽에는 전체 견본덱 목록,
    /// 오른쪽에는 선택한 견본덱 상세 정보를 표시한다.
    /// </summary>
    private void DrawSampleDeckWindowContent(int windowId)
    {
        float width = _sampleDeckWindowRect.width;
        float height = _sampleDeckWindowRect.height;

        // 고정된 장비가 없을 때 Hover 대상은 매 OnGUI 호출마다 다시 감지한다.
        _hoveredSampleDeckItemName = "";
        _sampleDeckItemClickedThisEvent = false;

        GUI.Label(
            new Rect(24f, 32f, 240f, 36f),
            "견본덱",
            _titleStyle
        );

        GUI.Label(
            new Rect(24f, 67f, 520f, 24f),
            "덱을 선택하면 목표 유닛·성급·시너지 정보를 확인할 수 있습니다.",
            _mutedStyle
        );

        GUI.Label(
            new Rect(width - 210f, 34f, 70f, 24f),
            _sampleDecks.Count.ToString(),
            _sectionTitleStyle
        );

        GUI.Label(
            new Rect(width - 205f, 60f, 100f, 20f),
            "등록 덱",
            _mutedStyle
        );

        if (GUI.Button(new Rect(width - 180f, 84f, 70f, 28f), "새로고침"))
            RefreshSampleDecks();

        if (GUI.Button(new Rect(width - 100f, 84f, 70f, 28f), "닫기"))
            _showSampleDeck = false;

        if (_sampleDecks.Count == 0)
        {
            GUI.Label(
                new Rect(28f, 125f, 700f, 30f),
                "견본덱 데이터를 불러오지 못했습니다. DeckDatabase Import 상태를 확인하세요.",
                _labelStyle
            );

            GUI.DragWindow(new Rect(0f, 0f, width, 28f));
            return;
        }

        _selectedSampleDeckIndex = Mathf.Clamp(
            _selectedSampleDeckIndex,
            0,
            _sampleDecks.Count - 1
        );

        DeckData selectedDeck = _sampleDecks[_selectedSampleDeckIndex];

        const float outerMargin = 24f;
        const float columnGap = 14f;
        const float listWidth = 300f;
        const float centerWidth = 670f;

        float contentY = 120f;
        float contentHeight = height - 145f;

        float listX = outerMargin;
        float centerX = listX + listWidth + columnGap;
        float rightX = centerX + centerWidth + columnGap;
        float rightWidth = width - rightX - outerMargin;

        // 왼쪽: 견본덱 목록
        DrawSampleDeckList(
            new Rect(listX, contentY, listWidth, contentHeight)
        );

        // 가운데: 선택한 견본덱 구성
        DrawSampleDeckDetail(
            new Rect(centerX, contentY, centerWidth, contentHeight),
            selectedDeck
        );

        // 오른쪽 상단: 선택한 목표 유닛 상세
        float unitPanelHeight = contentHeight * 0.56f;

        DrawSampleDeckUnitDetail(
            new Rect(
                rightX,
                contentY,
                rightWidth,
                unitPanelHeight
            ),
            selectedDeck
        );

        // 오른쪽 하단: 선택한 시너지 상세
        DrawSampleDeckSynergyDetail(
            new Rect(
                rightX,
                contentY + unitPanelHeight + columnGap,
                rightWidth,
                contentHeight - unitPanelHeight - columnGap
            ),
            selectedDeck
        );

        HandleSampleDeckItemTooltipOutsideClick(
            new Rect(0f, 0f, width, height)
        );

        DrawSampleDeckItemTooltip();

        GUI.DragWindow(new Rect(0f, 0f, width, 28f));

        // 다른 모든 패널보다 나중에 그려서
        // 장비 툴팁이 패널 뒤로 가려지지 않게 한다.
        DrawSampleDeckItemTooltip();

        GUI.DragWindow(new Rect(0f, 0f, width, 28f));
    }

    /// <summary>
    /// 장비가 아닌 견본덱 창의 다른 위치를 클릭하면
    /// 고정된 장비 툴팁을 닫는다.
    /// </summary>
    private void HandleSampleDeckItemTooltipOutsideClick(
        Rect windowRect)
    {
        if (string.IsNullOrWhiteSpace(_pinnedSampleDeckItemName))
            return;

        Event currentEvent = Event.current;

        if (currentEvent.type != EventType.MouseDown ||
            currentEvent.button != 0)
        {
            return;
        }

        if (_sampleDeckItemClickedThisEvent)
            return;

        if (!windowRect.Contains(currentEvent.mousePosition))
            return;

        _pinnedSampleDeckItemName = "";
        _hoveredSampleDeckItemName = "";
    }

    /// <summary>
    /// 등록된 견본덱 목록을 선택 가능한 카드로 표시한다.
    /// </summary>
    private void DrawSampleDeckList(Rect rect)
    {
        GUI.Box(rect, "");

        float contentHeight = Mathf.Max(
            rect.height - 20f,
            _sampleDecks.Count * 76f + 10f
        );

        _sampleDeckListScroll = GUI.BeginScrollView(
            rect,
            _sampleDeckListScroll,
            new Rect(0f, 0f, rect.width - 20f, contentHeight)
        );

        for (int i = 0; i < _sampleDecks.Count; i++)
        {
            DeckData deck = _sampleDecks[i];

            Rect cardRect = new Rect(
                8f,
                8f + i * 76f,
                rect.width - 36f,
                66f
            );

            bool selected = i == _selectedSampleDeckIndex;

            GUI.Box(
                cardRect,
                "",
                selected ? _selectedListCardStyle : _normalListCardStyle
            );

            if (GUI.Button(cardRect, GUIContent.none, GUIStyle.none))
            {
                if (_selectedSampleDeckIndex != i)
                {
                    _selectedSampleDeckIndex = i;

                    // 다른 덱으로 바꾸면 이전 덱에서 선택한
                    // 유닛·시너지 고정 정보를 초기화한다.
                    _selectedSampleDeckPokemonId = -1;
                    _selectedSampleDeckSynergyIndex = -1;

                    _sampleDeckDetailScroll = Vector2.zero;
                    _sampleDeckUnitDetailScroll = Vector2.zero;
                    _sampleDeckSynergyDetailScroll = Vector2.zero;
                }
            }

            string deckName = string.IsNullOrWhiteSpace(deck.deckName)
                ? $"덱 {deck.deckId}"
                : deck.deckName;

            GUI.Label(
                new Rect(cardRect.x + 12f, cardRect.y + 9f, cardRect.width - 24f, 24f),
                deckName,
                _sectionTitleStyle
            );

            GUI.Label(
                new Rect(cardRect.x + 12f, cardRect.y + 38f, cardRect.width - 24f, 20f),
                $"덱 ID {deck.deckId} · 유닛 {deck.unitCount}기 · {deck.totalGoldToBuild}G",
                _mutedStyle
            );
        }

        GUI.EndScrollView();
    }

    /// <summary>
    /// 선택된 견본덱의 덱 이름, 유닛, 목표 성급,
    /// 활성 시너지와 총 필요 골드를 표시한다.
    /// </summary>
    private void DrawSampleDeckDetail(Rect rect, DeckData deck)
    {
        GUI.Box(rect, "");

        if (deck == null)
            return;

        string deckName = string.IsNullOrWhiteSpace(deck.deckName)
            ? $"덱 {deck.deckId}"
            : deck.deckName;

        GUI.Label(
            new Rect(rect.x + 22f, rect.y + 18f, rect.width - 250f, 32f),
            deckName,
            _sectionTitleStyle
        );

        GUI.Label(
            new Rect(rect.x + rect.width - 210f, rect.y + 18f, 180f, 24f),
            $"완성 비용 {deck.totalGoldToBuild}G",
            _winStyle
        );

        GUI.Label(
            new Rect(rect.x + 22f, rect.y + 52f, 300f, 22f),
            $"덱 ID {deck.deckId} · 목표 유닛 {deck.unitCount}기",
            _mutedStyle
        );

        GUI.Box(
            new Rect(rect.x, rect.y + 84f, rect.width, 1f),
            ""
        );

        Rect scrollRect = new Rect(
            rect.x + 18f,
            rect.y + 100f,
            rect.width - 36f,
            rect.height - 120f
        );

        float contentHeight = 500f;

        if (deck.units != null)
        {
            int rows = Mathf.CeilToInt(deck.units.Count / 4f);
            contentHeight += rows * 138f;
        }

        Rect contentRect = new Rect(
            0f,
            0f,
            scrollRect.width - 20f,
            contentHeight
        );

        _sampleDeckDetailScroll = GUI.BeginScrollView(
            scrollRect,
            _sampleDeckDetailScroll,
            contentRect
        );

        float y = 0f;

        GUI.Label(
            new Rect(0f, y, 160f, 24f),
            "목표 유닛",
            _sectionTitleStyle
        );

        y += 34f;
        y = DrawSampleDeckUnits(deck.deckId, deck.units, y);

        y += 20f;

        GUI.Label(
            new Rect(0f, y, 160f, 24f),
            "활성 시너지",
            _sectionTitleStyle
        );

        y += 32f;

        y = DrawSampleDeckSynergyBadges(
            deck.activeSynergies,
            y
        );

        y += 24f;

        int totalRequiredItemCount = deck.units != null ? deck.units.Count * 2 : 0;

        GUI.Label(
            new Rect(0f, y, 260f, 24f),
            "덱 전체 필요 장비",
            _sectionTitleStyle
        );

        GUI.Label(
            new Rect(
                contentRect.width - 130f,
                y,
                120f,
                24f
            ),
            $"총 {totalRequiredItemCount}개",
            _winStyle
        );

        y += 32f;

        y = DrawSampleDeckRequiredItems(
            deck,
            y,
            contentRect.width
        );

        GUI.EndScrollView();
    }

    /// <summary>
    /// 견본덱의 활성 시너지를 클릭 가능한 버튼 형태로 표시한다.
    /// 클릭한 시너지는 오른쪽 하단 상세 패널에 고정된다.
    /// </summary>
    private float DrawSampleDeckSynergyBadges(List<string> synergies, float y)
    {
        if (synergies == null || synergies.Count == 0)
        {
            GUI.Label(
                new Rect(0f, y, 300f, 24f),
                "활성 시너지 없음",
                _mutedStyle
            );

            return y + 28f;
        }

        float x = 0f;
        const float maxWidth = 620f;
        const float lineHeight = 34f;
        const float buttonHeight = 26f;
        const float gap = 8f;

        for (int i = 0; i < synergies.Count; i++)
        {
            string text = string.IsNullOrWhiteSpace(synergies[i])
                ? "-"
                : synergies[i];

            float buttonWidth = Mathf.Clamp(
                text.Length * 12f + 32f,
                90f,
                180f
            );

            if (x + buttonWidth > maxWidth)
            {
                x = 0f;
                y += lineHeight;
            }

            bool selected = i == _selectedSampleDeckSynergyIndex;

            GUIStyle buttonStyle = selected
                ? _selectedListCardStyle
                : GUI.skin.button;

            Rect buttonRect = new Rect(
                x,
                y,
                buttonWidth,
                buttonHeight
            );

            if (GUI.Button(buttonRect, text, buttonStyle))
            {
                _selectedSampleDeckSynergyIndex = i;
                _sampleDeckSynergyDetailScroll = Vector2.zero;

                // 시너지를 선택하면 장비 툴팁만 닫는다.
                // 현재 선택된 유닛 정보는 그대로 유지한다.
                _pinnedSampleDeckItemName = "";
                _hoveredSampleDeckItemName = "";
            }

            x += buttonWidth + gap;
        }

        return y + lineHeight;
    }

    /// <summary>
    /// 덱의 모든 목표 유닛에게 배정된 임시 추천 장비를 합산해 표시한다.
    /// 유닛 카드와 동일한 캐시를 사용하므로 카드 표시 내용과 합계가 일치한다.
    /// </summary>
    private float DrawSampleDeckRequiredItems(DeckData deck, float y, float availableWidth)
    {
        if (deck == null ||
            deck.units == null ||
            deck.units.Count == 0)
        {
            GUI.Label(
                new Rect(0f, y, availableWidth, 22f),
                "필요 장비 없음",
                _mutedStyle
            );

            return y + 28f;
        }

        var itemCounts = new Dictionary<string, int>();

        for (int i = 0; i < deck.units.Count; i++)
        {
            DeckUnitEntry entry = deck.units[i];

            if (entry == null)
                continue;

            string[] items = GetTemporaryRecommendedItems(
                deck.deckId,
                entry.pokemonId
            );

            if (items == null)
                continue;

            for (int j = 0; j < items.Length; j++)
            {
                string itemName = items[j];

                if (string.IsNullOrWhiteSpace(itemName) ||
                    itemName == "장비 없음")
                {
                    continue;
                }

                if (itemCounts.ContainsKey(itemName))
                    itemCounts[itemName]++;
                else
                    itemCounts[itemName] = 1;
            }
        }

        if (itemCounts.Count == 0)
        {
            GUI.Label(
                new Rect(0f, y, availableWidth, 22f),
                "필요 장비 없음",
                _mutedStyle
            );

            return y + 28f;
        }

        float x = 0f;
        const float badgeHeight = 28f;
        const float lineHeight = 36f;
        const float gap = 8f;

        foreach (KeyValuePair<string, int> pair in itemCounts)
        {
            string text = $"{pair.Key} ×{pair.Value}";

            float badgeWidth = Mathf.Clamp(
                text.Length * 12f + 30f,
                110f,
                210f
            );

            if (x + badgeWidth > availableWidth - 10f)
            {
                x = 0f;
                y += lineHeight;
            }

            GUI.Label(
                new Rect(
                    x,
                    y,
                    badgeWidth,
                    badgeHeight
                ),
                text,
                _badgeStyle
            );

            x += badgeWidth + gap;
        }

        return y + lineHeight;
    }

    /// <summary>
    /// 오른쪽 상단에 현재 클릭해 고정한 목표 유닛의 상세 정보를 표시한다.
    /// 현재 단계에서는 레이아웃과 선택 상태만 먼저 검증한다.
    /// </summary>
    private void DrawSampleDeckUnitDetail(Rect rect, DeckData deck)
    {
        GUI.Box(rect, "");

        GUI.Label(
            new Rect(rect.x + 18f, rect.y + 16f, rect.width - 36f, 28f),
            "유닛 상세",
            _sectionTitleStyle
        );

        GUI.Box(
            new Rect(rect.x, rect.y + 54f, rect.width, 1f),
            ""
        );

        if (deck == null ||
            deck.units == null ||
            deck.units.Count == 0 ||
            _selectedSampleDeckPokemonId < 0)

        {
            GUI.Label(
                new Rect(rect.x + 18f, rect.y + 76f, rect.width - 36f, 50f),
                "가운데 목표 유닛을 클릭하면\n상세 정보가 여기에 고정됩니다.",
                _mutedStyle
            );

            return;
        }

        DeckUnitEntry entry = null;

        for (int i = 0; i < deck.units.Count; i++)
        {
            DeckUnitEntry candidate = deck.units[i];

            if (candidate != null &&
                candidate.pokemonId == _selectedSampleDeckPokemonId)
            {
                entry = candidate;
                break;
            }
        }

        if (entry == null)
            return;

        PokemonData pokemon = FindPokemonById(entry.pokemonId);

        string pokemonName =
            pokemon != null && !string.IsNullOrWhiteSpace(pokemon.pokemonName)
                ? pokemon.pokemonName
                : $"Pokemon ID {entry.pokemonId}";

        string costText = pokemon != null
            ? $"{pokemon.cost}코스트"
            : "코스트 확인 불가";

        Rect scrollRect = new Rect(
            rect.x + 14f,
            rect.y + 66f,
            rect.width - 28f,
            rect.height - 80f
        );

        Rect contentRect = new Rect(
            0f,
            0f,
            scrollRect.width - 20f,
            520f
        );

        _sampleDeckUnitDetailScroll = GUI.BeginScrollView(
            scrollRect,
            _sampleDeckUnitDetailScroll,
            contentRect
        );

        float y = 0f;

        GUI.Label(
            new Rect(0f, y, contentRect.width, 30f),
            pokemonName,
            _sectionTitleStyle
        );

        y += 38f;

        GUI.Label(
            new Rect(0f, y, contentRect.width, 22f),
            $"도감 ID: {entry.pokemonId}",
            _labelStyle
        );

        y += 26f;

        GUI.Label(
            new Rect(0f, y, contentRect.width, 30f),
            $"목표 성급: {FormatStars(entry.starLevel)}",
            _winStyle
        );

        y += 34f;

        GUI.Label(
            new Rect(0f, y, contentRect.width, 22f),
            $"코스트: {costText}",
            _labelStyle
        );

        y += 38f;

        GUI.Label(
            new Rect(0f, y, contentRect.width, 24f),
            "능력치",
            _sectionTitleStyle
        );

        y += 34f;

        if (pokemon != null)
        {
            GUI.Label(
                new Rect(0f, y, contentRect.width, 22f),
                $"체력: {pokemon.hp:0}",
                _labelStyle
            );

            y += 25f;

            GUI.Label(
                new Rect(0f, y, contentRect.width, 22f),
                $"공격력: {pokemon.attack:0}",
                _labelStyle
            );

            y += 25f;

            GUI.Label(
                new Rect(0f, y, contentRect.width, 22f),
                $"방어력: {pokemon.defense:0}",
                _labelStyle
            );

            y += 25f;

            GUI.Label(
                new Rect(0f, y, contentRect.width, 22f),
                $"공격속도: {pokemon.attackSpeed:0.00}",
                _labelStyle
            );

            y += 25f;

            GUI.Label(
                new Rect(0f, y, contentRect.width, 22f),
                $"사거리: {pokemon.range}칸",
                _labelStyle
            );

            y += 25f;

            GUI.Label(
                new Rect(0f, y, contentRect.width, 22f),
                $"스킬 위력: {pokemon.spellPower:0}",
                _labelStyle
            );

            y += 25f;

            GUI.Label(
                new Rect(0f, y, contentRect.width, 22f),
                $"마나 비용: {pokemon.manaCost}",
                _labelStyle
            );

            y += 36f;

            GUI.Label(
                new Rect(0f, y, contentRect.width, 24f),
                "역할 · 속성",
                _sectionTitleStyle
            );

            y += 32f;

            string roleText = string.IsNullOrWhiteSpace(pokemon.role)
                ? "-"
                : pokemon.role;

            GUI.Label(
                new Rect(0f, y, contentRect.width, 22f),
                $"역할: {roleText}",
                _labelStyle
            );

            y += 26f;

            string synergyText =
                pokemon.synergies != null && pokemon.synergies.Count > 0
                    ? string.Join(", ", pokemon.synergies)
                    : "-";

            GUI.Label(
                new Rect(0f, y, contentRect.width, 44f),
                $"보유 시너지: {synergyText}",
                _labelStyle
            );

            y += 50f;

            GUI.Label(
                new Rect(0f, y, contentRect.width, 24f),
                "스킬",
                _sectionTitleStyle
            );

            y += 32f;

            string skillIdText = string.IsNullOrWhiteSpace(pokemon.skillId)
                ? "-"
                : pokemon.skillId;

            GUI.Label(
                new Rect(0f, y, contentRect.width, 22f),
                $"스킬 ID: {skillIdText}",
                _labelStyle
            );

            y += 26f;

            GUI.Label(
                new Rect(0f, y, contentRect.width, 44f),
                pokemon.skill != null
                    ? "스킬 데이터 연결됨"
                    : "스킬 데이터 없음",
                _mutedStyle
            );
                        
        }
        else
        {
            GUI.Label(
                new Rect(0f, y, contentRect.width, 44f),
                "PokemonDatabase에서 해당 포켓몬을 찾지 못했습니다.",
                _mutedStyle
            );
        }

        GUI.EndScrollView();
    }

    /// <summary>
    /// 오른쪽 하단에 클릭해 고정한 활성 시너지의
    /// 실제 단계별 효과를 SynergyDatabase에서 조회해 표시한다.
    /// </summary>
    private void DrawSampleDeckSynergyDetail(Rect rect, DeckData deck)
    {
        GUI.Box(rect, "");

        GUI.Label(
            new Rect(rect.x + 18f, rect.y + 16f, rect.width - 36f, 28f),
            "시너지 상세",
            _sectionTitleStyle
        );

        GUI.Box(
            new Rect(rect.x, rect.y + 54f, rect.width, 1f),
            ""
        );

        if (deck == null ||
            deck.activeSynergies == null ||
            deck.activeSynergies.Count == 0 ||
            _selectedSampleDeckSynergyIndex < 0 ||
            _selectedSampleDeckSynergyIndex >= deck.activeSynergies.Count)
        {
            GUI.Label(
                new Rect(rect.x + 18f, rect.y + 76f, rect.width - 36f, 50f),
                "가운데 활성 시너지를 클릭하면\n단계별 효과가 여기에 고정됩니다.",
                _mutedStyle
            );

            return;
        }

        string activeSynergyText =
            deck.activeSynergies[_selectedSampleDeckSynergyIndex];

        // 예: "Bug(4/4)"에서
        // key = "Bug", currentCount = 4를 추출한다.
        string synergyKey = ExtractSynergyKey(activeSynergyText);
        int currentCount = ExtractSynergyCurrentCount(activeSynergyText);

        SynergyDatabase db = SynergyDatabase.Instance;

        SynergyData synergy = db != null
            ? db.GetByKey(synergyKey)
            : null;

        Rect scrollRect = new Rect(
            rect.x + 14f,
            rect.y + 66f,
            rect.width - 28f,
            rect.height - 80f
        );

        float contentHeight = 260f;

        if (synergy != null && synergy.tiers != null)
            contentHeight = Mathf.Max(260f, 90f + synergy.tiers.Count * 76f);

        Rect contentRect = new Rect(
            0f,
            0f,
            scrollRect.width - 20f,
            contentHeight
        );

        _sampleDeckSynergyDetailScroll = GUI.BeginScrollView(
            scrollRect,
            _sampleDeckSynergyDetailScroll,
            contentRect
        );

        float y = 0f;

        if (synergy == null)
        {
            GUI.Label(
                new Rect(0f, y, contentRect.width, 30f),
                activeSynergyText,
                _sectionTitleStyle
            );

            y += 40f;

            GUI.Label(
                new Rect(0f, y, contentRect.width, 60f),
                $"SynergyDatabase에서 '{synergyKey}' 시너지를 찾지 못했습니다.",
                _mutedStyle
            );

            GUI.EndScrollView();
            return;
        }

        string synergyName = string.IsNullOrWhiteSpace(synergy.synergyName)
            ? synergy.synergyNameEn
            : synergy.synergyName;

        string synergyNameEn = string.IsNullOrWhiteSpace(synergy.synergyNameEn)
            ? "-"
            : synergy.synergyNameEn;

        GUI.Label(
            new Rect(0f, y, contentRect.width, 30f),
            synergyName,
            _sectionTitleStyle
        );

        y += 32f;

        GUI.Label(
            new Rect(0f, y, contentRect.width, 22f),
            $"{synergyNameEn} · 현재 {currentCount}기",
            _mutedStyle
        );

        y += 34f;

        GUI.Label(
            new Rect(0f, y, contentRect.width, 24f),
            "단계별 효과",
            _sectionTitleStyle
        );

        y += 32f;

        if (synergy.tiers == null || synergy.tiers.Count == 0)
        {
            GUI.Label(
                new Rect(0f, y, contentRect.width, 40f),
                "등록된 시너지 단계가 없습니다.",
                _mutedStyle
            );

            GUI.EndScrollView();
            return;
        }

        for (int i = 0; i < synergy.tiers.Count; i++)
        {
            SynergyTier tier = synergy.tiers[i];

            if (tier == null)
                continue;

            bool isActivated = currentCount >= tier.count;

            Rect tierRect = new Rect(
                0f,
                y,
                contentRect.width - 8f,
                66f
            );

            GUI.Box(
                tierRect,
                "",
                isActivated
                    ? _selectedListCardStyle
                    : _normalListCardStyle
            );

            string tierTitle = isActivated
                ? $"활성  {tier.count}기"
                : $"미활성  {tier.count}기";

            GUI.Label(
                new Rect(
                    tierRect.x + 10f,
                    tierRect.y + 7f,
                    tierRect.width - 20f,
                    22f
                ),
                tierTitle,
                isActivated ? _winStyle : _labelStyle
            );

            string effectText =
                string.IsNullOrWhiteSpace(tier.effectDescription)
                    ? "효과 설명 없음"
                    : tier.effectDescription;

            GUI.Label(
                new Rect(
                    tierRect.x + 10f,
                    tierRect.y + 31f,
                    tierRect.width - 20f,
                    30f
                ),
                effectText,
                _mutedStyle
            );

            y += 74f;
        }

        GUI.EndScrollView();
    }

    /// <summary>
    /// "Bug(4/4)"에서 "Bug" 부분만 추출한다.
    /// 괄호가 없으면 원본 문자열을 그대로 반환한다.
    /// </summary>
    private static string ExtractSynergyKey(string synergyText)
    {
        if (string.IsNullOrWhiteSpace(synergyText))
            return "";

        int openParenthesisIndex = synergyText.IndexOf('(');

        if (openParenthesisIndex < 0)
            return synergyText.Trim();

        return synergyText
            .Substring(0, openParenthesisIndex)
            .Trim();
    }

    /// <summary>
    /// "Bug(4/4)"에서 현재 유닛 수인 4를 추출한다.
    /// 형식이 올바르지 않으면 0을 반환한다.
    /// </summary>
    private static int ExtractSynergyCurrentCount(string synergyText)
    {
        if (string.IsNullOrWhiteSpace(synergyText))
            return 0;

        int openParenthesisIndex = synergyText.IndexOf('(');
        int slashIndex = synergyText.IndexOf('/');

        if (openParenthesisIndex < 0 ||
            slashIndex < 0 ||
            slashIndex <= openParenthesisIndex + 1)
        {
            return 0;
        }

        string countText = synergyText.Substring(
            openParenthesisIndex + 1,
            slashIndex - openParenthesisIndex - 1
        );

        return int.TryParse(countText, out int count)
            ? count
            : 0;
    }

    /// <summary>
    /// 견본덱의 유닛을 한 줄에 최대 4개씩 카드로 표시한다.
    /// </summary>
    private float DrawSampleDeckUnits(int deckId, List<DeckUnitEntry> units, float y)
    {
        if (units == null || units.Count == 0)
        {
            GUI.Box(new Rect(0f, y, 520f, 42f), "");

            GUI.Label(
                new Rect(12f, y + 11f, 420f, 20f),
                "등록된 목표 유닛이 없습니다.",
                _mutedStyle
            );

            return y + 52f;
        }

        const float cardWidth = 150f;
        const float cardHeight = 126f;
        const float gap = 10f;
        const int cardsPerRow = 4;

        var sortedUnits = new List<DeckUnitEntry>(units);

        sortedUnits.Sort((a, b) =>
        {
            int aSlot = a != null ? a.slot : int.MaxValue;
            int bSlot = b != null ? b.slot : int.MaxValue;
            return aSlot.CompareTo(bSlot);
        });

        for (int i = 0; i < sortedUnits.Count; i++)
        {
            DeckUnitEntry entry = sortedUnits[i];
            if (entry == null)
                continue;

            int row = i / cardsPerRow;
            int col = i % cardsPerRow;

            Rect cardRect = new Rect(
                col * (cardWidth + gap),
                y + row * (cardHeight + gap),
                cardWidth,
                cardHeight
            );

            DrawSampleDeckUnitCard(deckId, entry, cardRect);

            Rect itemAreaRect = new Rect(
                cardRect.x + 8f,
                cardRect.y + 83f,
                cardRect.width - 16f,
                38f
            );

            Event currentEvent = Event.current;

            bool clickedCard =
                currentEvent.type == EventType.MouseDown &&
                currentEvent.button == 0 &&
                cardRect.Contains(currentEvent.mousePosition);

            bool clickedItemArea =
                itemAreaRect.Contains(currentEvent.mousePosition);

            if (clickedCard && !clickedItemArea)
            {
                _selectedSampleDeckPokemonId = entry.pokemonId;
                _sampleDeckUnitDetailScroll = Vector2.zero;

                // 다른 유닛을 선택하면 고정된 장비 툴팁을 닫는다.
                _pinnedSampleDeckItemName = "";
                _hoveredSampleDeckItemName = "";

                currentEvent.Use();
            }
        }

        int rowCount = Mathf.CeilToInt(sortedUnits.Count / (float)cardsPerRow);

        return y + rowCount * (cardHeight + gap);
    }

    /// <summary>
    /// 견본덱 유닛 한 마리의 이름, 목표 성급, ID, 코스트와
    /// 임시 추천 장비 2개를 표시한다.
    /// </summary>
    private void DrawSampleDeckUnitCard(int deckId, DeckUnitEntry entry, Rect rect)
    {
        GUI.Box(rect, "", _unitCardStyle);

        PokemonData pokemon = FindPokemonById(entry.pokemonId);

        string pokemonName =
            pokemon != null &&
            !string.IsNullOrWhiteSpace(pokemon.pokemonName)
                ? pokemon.pokemonName
                : $"Pokemon ID {entry.pokemonId}";

        string costText = pokemon != null
            ? $"{pokemon.cost}코스트"
            : "코스트 확인 불가";

        GUI.Label(
            new Rect(
                rect.x + 8f,
                rect.y + 7f,
                rect.width - 16f,
                22f
            ),
            pokemonName,
            _smallStyle
        );

        GUI.Label(
            new Rect(
                rect.x + 8f,
                rect.y + 28f,
                rect.width - 16f,
                28f
            ),
            $"목표 {FormatStars(entry.starLevel)}",
            _winStyle
        );

        GUI.Label(
            new Rect(
                rect.x + 8f,
                rect.y + 55f,
                rect.width - 16f,
                20f
            ),
            $"ID {entry.pokemonId} · {costText}",
            _mutedStyle
        );

        // 카드 내 구분선
        GUI.Box(
            new Rect(
                rect.x + 7f,
                rect.y + 78f,
                rect.width - 14f,
                1f
            ),
            ""
        );

        string[] recommendedItems =
            GetTemporaryRecommendedItems(
                deckId,
                entry.pokemonId
            );

        Rect firstItemRect = new Rect(
            rect.x + 8f,
            rect.y + 83f,
            rect.width - 16f,
            18f
        );

        Rect secondItemRect = new Rect(
            rect.x + 8f,
            rect.y + 102f,
            rect.width - 16f,
            18f
        );

        DrawSampleDeckItemEntry(
            firstItemRect,
            $"추천 1: {recommendedItems[0]}",
            recommendedItems[0]
        );

        DrawSampleDeckItemEntry(
            secondItemRect,
            $"추천 2: {recommendedItems[1]}",
            recommendedItems[1]
        );
    }

    /// <summary>
    /// 견본덱 유닛 카드 안의 추천 장비 한 줄을 표시한다.
    /// Hover 시 임시 툴팁을 열고 클릭하면 고정한다.
    /// 같은 장비를 다시 클릭하면 고정을 해제한다.
    /// </summary>
    private void DrawSampleDeckItemEntry(
    Rect rect,
    string displayText,
    string itemName)
    {
        GUI.Label(
            rect,
            displayText,
            _mutedStyle
        );

        if (string.IsNullOrWhiteSpace(itemName) ||
            itemName == "장비 없음")
        {
            return;
        }

        Event currentEvent = Event.current;

        bool isHovered =
            rect.Contains(currentEvent.mousePosition);

        if (!isHovered)
            return;

        _hoveredSampleDeckItemName = itemName;

        // 고정된 장비가 없을 때만 Hover 위치를 따라간다.
        if (string.IsNullOrWhiteSpace(_pinnedSampleDeckItemName))
        {
            _sampleDeckItemTooltipAnchorScreenPosition =
                GUIUtility.GUIToScreenPoint(
                    new Vector2(
                        rect.xMax,
                        rect.yMin
                    )
                );
        }

        if (currentEvent.type != EventType.MouseDown ||
            currentEvent.button != 0)
        {
            return;
        }

        _sampleDeckItemClickedThisEvent = true;

        if (_pinnedSampleDeckItemName == itemName)
        {
            // 같은 장비 재클릭 → 해제
            _pinnedSampleDeckItemName = "";
        }
        else
        {
            // 새 장비 고정 → 이름과 위치를 같이 저장
            _pinnedSampleDeckItemName = itemName;

            _sampleDeckItemTooltipAnchorScreenPosition =
                GUIUtility.GUIToScreenPoint(
                    new Vector2(
                        rect.xMax,
                        rect.yMin
                    )
                );
        }

        currentEvent.Use();
    }

    /// <summary>
    /// 중앙 DeckDatabase에서 전체 견본덱 목록을 다시 가져온다.
    /// </summary>
    private void RefreshSampleDecks()
    {
        _sampleDecks.Clear();
        _temporaryRecommendedItems.Clear();

        // 새로고침 시 현재 덱 선택은 유지하되,
        // 상세 선택과 모든 견본덱 스크롤 위치는 초기화한다.
        _selectedSampleDeckPokemonId = -1;
        _selectedSampleDeckSynergyIndex = -1;

        _sampleDeckListScroll = Vector2.zero;
        _sampleDeckDetailScroll = Vector2.zero;
        _sampleDeckUnitDetailScroll = Vector2.zero;
        _sampleDeckSynergyDetailScroll = Vector2.zero;

        DeckDatabase db = DeckDatabase.Instance;

        if (db == null || db.decks == null)
        {
            _selectedSampleDeckIndex = 0;
            return;
        }

        for (int i = 0; i < db.decks.Count; i++)
        {
            DeckData deck = db.decks[i];

            if (deck != null)
                _sampleDecks.Add(deck);
        }

        _sampleDecks.Sort((a, b) => a.deckId.CompareTo(b.deckId));

        if (_sampleDecks.Count == 0)
            _selectedSampleDeckIndex = 0;
        else
            _selectedSampleDeckIndex = Mathf.Clamp(
                _selectedSampleDeckIndex,
                0,
                _sampleDecks.Count - 1
            );
    }

    /// <summary>
    /// PokemonDatabase의 전체 목록에서 도감 ID가 일치하는 포켓몬을 찾는다.
    /// 별도 조회 메서드 이름에 의존하지 않도록 현재 all 목록을 직접 검사한다.
    /// </summary>
    private static PokemonData FindPokemonById(int pokemonId)
    {
        PokemonDatabase db = PokemonDatabase.Instance;

        return db != null
            ? db.GetById(pokemonId)
            : null;
    }

    /// <summary>
    /// 덱과 포켓몬 조합별로 실제 ItemDatabase에서 임시 추천 장비 2개를 뽑는다.
    /// 한 번 뽑힌 결과는 Dictionary에 저장되어 OnGUI 호출마다 바뀌지 않는다.
    /// </summary>
    private string[] GetTemporaryRecommendedItems(int deckId, int pokemonId)
    {
        string key = $"{deckId}_{pokemonId}";

        if (_temporaryRecommendedItems.TryGetValue(
            key,
            out string[] cachedItems))
        {
            return cachedItems;
        }

        ItemDatabase db = ItemDatabase.Instance;

        if (db == null || db.all == null || db.all.Count == 0)
        {
            string[] emptyResult = { "장비 없음", "장비 없음" };
            _temporaryRecommendedItems[key] = emptyResult;
            return emptyResult;
        }

        var availableItems = new List<ItemData>();

        for (int i = 0; i < db.all.Count; i++)
        {
            ItemData item = db.all[i];

            if (item != null)
                availableItems.Add(item);
        }

        if (availableItems.Count == 0)
        {
            string[] emptyResult = { "장비 없음", "장비 없음" };
            _temporaryRecommendedItems[key] = emptyResult;
            return emptyResult;
        }

        int firstIndex = UnityEngine.Random.Range(0, availableItems.Count);

        int secondIndex = UnityEngine.Random.Range(0, availableItems.Count);

        string firstName =
            string.IsNullOrWhiteSpace(availableItems[firstIndex].itemName)
                ? availableItems[firstIndex].itemNameEn
                : availableItems[firstIndex].itemName;

        string secondName =
            string.IsNullOrWhiteSpace(availableItems[secondIndex].itemName)
                ? availableItems[secondIndex].itemNameEn
                : availableItems[secondIndex].itemName;

        string[] result =
        {
            firstName,
            secondName
        };

        _temporaryRecommendedItems[key] = result;

        return result;
    }
}
