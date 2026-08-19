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

    [Header("SFX")]
    [Tooltip("비어 있으면 GameSceneTest의 'Level Button'을 런타임에 찾습니다. 클릭 시 SoundId.XpBuy 재생.")]
    [SerializeField] private Button _xpPurchaseButton;
    [Tooltip("비어 있으면 GameSceneTest의 'ReRoll Button'을 런타임에 찾습니다. 클릭 시 SoundId.ShopReroll 재생.")]
    [SerializeField] private Button _shopRerollButton;

    [Header("Canvas Shop Controls")]
    [Tooltip("비어 있으면 GameSceneTest의 'BattleRaedy_Button'을 런타임에 찾습니다.")]
    [SerializeField] private Button _battleReadyButton;
    [Tooltip("비어 있으면 UnitStore_Panel 아래의 ShopChange Button을 런타임에 찾습니다.")]
    [SerializeField] private Button _showItemStoreButton;
    [Tooltip("비어 있으면 ItemStore_Panel 아래의 ShopChange Button을 런타임에 찾습니다.")]
    [SerializeField] private Button _showUnitStoreButton;

    [Header("ReRoll Button 텍스트/이미지")]
    [Tooltip("리롤 비용 표시 텍스트 (무료 리롤 있을 때 0원, 없을 때 2원)")]
    [SerializeField] private TextMeshProUGUI _rerollByCoinText;
    [Tooltip("무료 리롤권 개수 표시 텍스트")]
    [SerializeField] private TextMeshProUGUI _rerollCountText;
    [Tooltip("무료 리롤권이 있을 때 보일 이미지 (없으면 숨김, 부드럽게 페이드인/아웃)")]
    [SerializeField] private GameObject _rerollFreeIndicator;
    [Tooltip("리롤 표시 이미지의 페이드 인/아웃 속도 (낮을수록 느림, 기본 1.5)")]
    [SerializeField] private float _rerollIndicatorPulseSpeed = 1.5f;

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

    // 파트너 전체화면 관전 중 상점 HUD 숨김/복원 상태(HandlePartnerSpectateExpandedChanged 전용).
    private bool _shopPanelsHiddenForSpectate;
    private bool _unitStorePanelActiveBeforeSpectate;
    private bool _itemStorePanelActiveBeforeSpectate;
    private bool _battleReadyButtonActiveBeforeSpectate;

    // 파트너 전체화면 관전 상태(SetExpanded(true)로 들어간 전체화면 한정, PIP 제외) 자체를 나타내는
    // 값. _shopPanelsHiddenForSpectate(상점 HUD 숨김/복원 상태)와는 의미가 다르므로 재사용하지
    // 않는다 — UnitDragController/SellZone 같은 3D 물리 입력 쪽이 "지금 관전 중인가"만 확인할 때 쓴다.
    private bool _isPartnerSpectateExpanded;

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

    private int  _rerollCount;          // 직전 잔여 무료 리롤 — 감소를 "소모"로 판정하는 기준

    private int _buyXpCostGold;
    private int _buyXpAmount;

    // ──────────────────────────────────────────
    // 안내 메시지(토스트) 상태
    // ──────────────────────────────────────────

    private string _toastMessage = "";
    private float _toastHideTime;

    private const float ToastDuration = 2.5f;

    // 통신기 골드 전송 UI 상태
    //
    // ⛔ 기능 보류 — 되살릴 때는 [GOLD_TRANSFER_HOLD]로 전체 검색해 7군데를 모두 풀 것.
    //    일부만 풀면 컴파일은 되는데 구독만 살고 그리기는 죽는 식으로 절반만 동작한다
    //    (컴파일 에러로 안 잡히는 종류의 실수라 태그로 묶어 둔다).
    //    ① 상태 필드 ② OnEnable 구독 ③ OnDisable 해제 ④ OnDisable 상태 초기화
    //    ⑤ 결과 핸들러 3개 ⑥ OnGUI 호출 ⑦ 패널 본체(DrawTradeGoldPanel/RequestGoldTransfer)
    // ⛔ 기능 보류(2026-08-18) — 통신기는 유닛 교환만 담당한다. 아래 상태·구독·패널을 통째로 주석 처리해
    //    송신 경로 자체를 없앴다. NetworkManager의 수신(RPC_GoldReceive)·환급 경로는 구버전 클라이언트
    //    호환을 위해 그대로 남겨 뒀다 — 되살릴 때는 여기 주석 블록만 풀면 된다.
    // private bool _isGoldTransferPending;
    // private string _goldTransferResult = "";

    // 플러시와마이농 필드 폼 선택 UI 상태.
    // 신규 유틸 클래스를 만들지 않고 이 클래스 안에서만 쓴다.
    private const int PLUSLE_MINUN_ID = 310;
    private const int PLUSLE_ID       = 311;
    private const int MINUN_ID        = 312;

    private PokemonUnit _pendingFormUnit;
    private PokemonData _plusleFormData;
    private PokemonData _minunFormData;

    // Canvas 선택 패널(PlusleMinunChoicePanel)이 살아 있으면 IMGUI 폴백은 그리지 않는다.
    private bool _formChoicePanelBound;

    // ──────────────────────────────────────────
    // OnGUI 스타일 캐시
    // ──────────────────────────────────────────

    private GUIStyle _titleStyle;
    private GUIStyle _sectionTitleStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _toastStyle;
    private GUIStyle _smallStyle;
    private GUIStyle _mutedStyle;
    private GUIStyle _winStyle;
    private GUIStyle _loseStyle;
    private GUIStyle _badgeStyle;
    private GUIStyle _unitCardStyle;
    private GUIStyle _selectedListCardStyle;
    private GUIStyle _normalListCardStyle;

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

        GameEvents.OnUnitPlacementRejected += HandleUnitPlacementRejected;

        // [GOLD_TRANSFER_HOLD] ② 구독 — 위 상태 필드 주석 참고
        // GameEvents.OnGoldTransferCompleted += HandleGoldTransferCompleted;
        // GameEvents.OnGoldTransferRejected += HandleGoldTransferRejected;
        // GameEvents.OnPartnerGoldReceived += HandlePartnerGoldReceived;

        GameEvents.OnUnitPlaced += HandleUnitPlacedForForm;
        GameEvents.OnUnitBenched += HandleUnitBenchedForForm;
        GameEvents.OnAllPlayersReady += HandleAllPlayersReadyForForm;

        GameEvents.OnPartnerSpectateExpandedChanged += HandlePartnerSpectateExpandedChanged;
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

        GameEvents.OnUnitPlacementRejected -= HandleUnitPlacementRejected;

        // [GOLD_TRANSFER_HOLD] ③ 구독 해제 — 위 상태 필드 주석 참고
        // GameEvents.OnGoldTransferCompleted -= HandleGoldTransferCompleted;
        // GameEvents.OnGoldTransferRejected -= HandleGoldTransferRejected;
        // GameEvents.OnPartnerGoldReceived -= HandlePartnerGoldReceived;

        GameEvents.OnUnitPlaced -= HandleUnitPlacedForForm;
        GameEvents.OnUnitBenched -= HandleUnitBenchedForForm;
        GameEvents.OnAllPlayersReady -= HandleAllPlayersReadyForForm;

        GameEvents.OnPartnerSpectateExpandedChanged -= HandlePartnerSpectateExpandedChanged;

        // [GOLD_TRANSFER_HOLD] ④
        // _isGoldTransferPending = false;

        // 매니저가 꺼지면 선택 UI도 닫아야 한다(패널만 남아 켜져 있으면 조작이 막힌 채 방치된다).
        CloseFormChoice();
    }

    private void Start()
    {
        RefreshMatchHistory();
        SyncProgressState();
        BindCanvasShopControls();
        BindCanvasProgressTexts();
        RefreshCanvasProgressTexts();
        CachePlusleMinunFormData();
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

        // 무료 리롤 표시 이미지의 부드러운 페이드 인/아웃 애니메이션.
        ApplyPulseAlpha(_rerollFreeIndicator,
                        Mathf.PingPong(Time.time * _rerollIndicatorPulseSpeed, 1f));

        TickFormChoice();
    }

    /// <summary>켜져 있는 표시 이미지의 알파만 갈아끼운다(0 → 1 → 0 반복).</summary>
    private static void ApplyPulseAlpha(GameObject indicator, float alpha)
    {
        if (indicator == null || !indicator.activeSelf) return;

        var image = indicator.GetComponent<Image>();
        if (image == null) return;

        Color color = image.color;
        color.a = alpha;
        image.color = color;
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
    ///
    /// 구매 가능 여부는 반드시 ShopManager(gm.Shop.Gold/RequiredXp)의 "지금 이 순간" 실제 값으로만
    /// 계산한다 — UIManager의 _gold/_requiredXp 캐시는 골드/XP 변경이 여러 GameEvents로 나뉘어
    /// 순차 발행될 때 서로 다른 시점의 값을 들고 있을 수 있어(예: 골드는 이미 갱신됐지만 XP는 아직),
    /// 이 조합으로 affordable을 계산하면 같은 클릭 안에서 button.interactable이 실제로 여러 번
    /// 바뀌며 마우스가 버튼 위에 그대로 있는데도 Hover(Highlighted) 표시가 갱신되지 않는 문제가
    /// 있었다(2026-08 확인). ShopManager 쪽 값은 항상 최종 확정 상태 하나뿐이라 이 불일치가 없다.
    /// </summary>
    private void RefreshShopButtonAffordability()
    {
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        if (shop == null) return;

        // 무료 리롤권이 있는지 확인
        bool hasFreeReroll = shop.RerollCount > 0;

        // 리롤 텍스트 업데이트: 무료 있으면 0원, 없으면 2원
        if (_rerollByCoinText != null)
            _rerollByCoinText.text = hasFreeReroll ? "0" : "2";

        // 무료 리롤권 개수 표시
        if (_rerollCountText != null)
            _rerollCountText.text = hasFreeReroll ? $"{shop.RerollCount}" : "";

        // 무료 리롤권 표시 이미지
        if (_rerollFreeIndicator != null)
            _rerollFreeIndicator.SetActive(hasFreeReroll);

        SetButtonAffordable(_shopRerollButton, hasFreeReroll || shop.Gold >= shop.RerollCost);
        SetButtonAffordable(_xpPurchaseButton, shop.RequiredXp > 0 && shop.Gold >= shop.BuyXpCostGold);
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

        // 클릭 시 EventSystem이 이 두 버튼을 "선택(Selected)" 상태로 만들지 않게 한다. Selectable의
        // currentSelectionState는 hasSelection이 isPointerInside보다 우선이라, 클릭 후에도 마우스가
        // 버튼 위에 그대로 있으면 Highlighted가 아니라 Selected로 전환된다. 이 버튼들의 Selected
        // 애니메이션 클립이 Normal과 동일해 "클릭 직후 Hover가 풀린 것처럼 보이는" 버그였다
        // (2026-08 확인, [ShopButtonHoverTrace] 진단 로그로 실측). 이 두 버튼은 키보드/게임패드
        // 내비게이션 대상이 아니므로 Navigation.Mode.None으로 바꿔도 기존 UI 흐름에 영향이 없다 —
        // EventSystem.SetSelectedGameObject(null)을 클릭마다 호출하는 대신, 애초에 선택되지 않게
        // 막는 방식이라 다른 시스템의 선택 상태를 건드리지 않는다.
        DisableSelectionNavigation(_xpPurchaseButton);
        DisableSelectionNavigation(_shopRerollButton);

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

    /// <summary>
    /// Navigation.Mode를 None으로 바꿔 이 버튼이 EventSystem의 "선택(Selected)" 대상이 되지 않게 한다.
    /// Selectable.OnPointerDown은 navigation.mode != None일 때만 EventSystem.SetSelectedGameObject를
    /// 호출하므로, None으로 두면 클릭해도 애초에 선택되지 않아 currentSelectionState가 Selected로
    /// 빠지지 않는다(Hover/Highlighted가 정상적으로 유지됨). 이 두 버튼은 키보드/게임패드 내비게이션을
    /// 쓰지 않는 현재 UI 구조라 안전하다 — 다른 버튼의 Navigation은 건드리지 않는다.
    /// </summary>
    private static void DisableSelectionNavigation(Button button)
    {
        if (button == null) return;

        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
    }

    private void UnbindCanvasShopControls()
    {
        _xpPurchaseButton?.onClick.RemoveListener(HandleXpPurchaseButtonClicked);
        _shopRerollButton?.onClick.RemoveListener(HandleShopRerollButtonClicked);
        _battleReadyButton?.onClick.RemoveListener(HandleBattleReadyButtonClicked);
        _showItemStoreButton?.onClick.RemoveListener(ShowItemStore);
        _showUnitStoreButton?.onClick.RemoveListener(ShowUnitStore);
    }

    private void HandleXpPurchaseButtonClicked()
    {
        if (SoundManager.TryGet(out var sm)) sm.PlaySfx(SoundId.XpBuy);
        GameEvents.RequestXpPurchase();
    }

    private void HandleShopRerollButtonClicked()
    {
        if (SoundManager.TryGet(out var sm)) sm.PlaySfx(SoundId.ShopReroll);
        GameEvents.RequestShopReroll();
    }

    private void HandleBattleReadyButtonClicked()
    {
        if (_currentPhase != GamePhase.Shopping || _readyRequestSubmitted)
            return;

        // 파트너 재접속 대기 중엔 전투 시작 요청을 막는다(2026-08 — 대기 중 입력 차단).
        // 옵션창의 EventSystem 비활성화로도 막히지만, 방어적으로 여기서도 확인한다.
        if (GameManager.TryGet(out var gm) && gm.Network != null && gm.Network.IsAwaitingPartnerReconnect)
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

    /// <summary>
    /// 파트너 전체화면 관전 진입/종료(GameEvents.OnPartnerSpectateExpandedChanged, PIP는 포함 안 함).
    /// 진입 시 상점 HUD 두 루트(_unitStorePanel/_itemStorePanel)와 전투 시작 버튼을 끈다 —
    /// 새로고침·리롤·XP 구매 버튼(ShopButton_Panel)이 UnitStore_Panel의 자식이라 이 SetActive만으로
    /// 함께 꺼진다(버튼을 개별로 SetActive하지 않는다). 진입 전 각 오브젝트의 실제 활성 상태를
    /// 저장해뒀다가 종료 시 그대로 되돌린다 — SetActiveShopPanel(상호 배타 토글)을 쓰면 "둘 다
    /// 꺼져 있던 상태"(예: 전투 중)를 강제로 아이템 상점 활성으로 되돌리는 오류가 생기고,
    /// 전투 시작 버튼도 무조건 true로 복원하면 원래 꺼져 있던 상태(예: 이미 준비 완료로
    /// UpdateBattleReadyButtonState가 비활성 처리한 경우 등)를 무시하고 잘못 되살리므로 두 경우
    /// 모두 저장된 실제 값으로만 되돌린다. ShopManager/전투 준비 네트워크 로직은 건드리지 않는다 —
    /// GameObject 표시 여부만 바꾼다(interactable은 UpdateBattleReadyButtonState가 별도로 관리).
    /// </summary>
    private void HandlePartnerSpectateExpandedChanged(bool expanded)
    {
        // 상점 HUD 숨김/복원의 중복 진입 방지 가드와는 별개로, "지금 전체화면 관전 중인가" 자체는
        // 항상 이벤트 값 그대로 최신화한다(UnitDragController/SellZone의 3D 입력 차단 판단 기준).
        _isPartnerSpectateExpanded = expanded;

        if (expanded)
        {
            if (_shopPanelsHiddenForSpectate) return; // 중복 진입 방지
            _shopPanelsHiddenForSpectate = true;

            _unitStorePanelActiveBeforeSpectate = _unitStorePanel != null && _unitStorePanel.activeSelf;
            _itemStorePanelActiveBeforeSpectate = _itemStorePanel != null && _itemStorePanel.activeSelf;
            _battleReadyButtonActiveBeforeSpectate = _battleReadyButton != null && _battleReadyButton.gameObject.activeSelf;

            if (_unitStorePanel != null) _unitStorePanel.SetActive(false);
            if (_itemStorePanel != null) _itemStorePanel.SetActive(false);
            if (_battleReadyButton != null) _battleReadyButton.gameObject.SetActive(false);
        }
        else
        {
            if (!_shopPanelsHiddenForSpectate) return;
            _shopPanelsHiddenForSpectate = false;

            if (_unitStorePanel != null) _unitStorePanel.SetActive(_unitStorePanelActiveBeforeSpectate);
            if (_itemStorePanel != null) _itemStorePanel.SetActive(_itemStorePanelActiveBeforeSpectate);
            if (_battleReadyButton != null) _battleReadyButton.gameObject.SetActive(_battleReadyButtonActiveBeforeSpectate);
        }
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

    /// <summary>
    /// 무료 리롤 자원 변동. 버튼 활성 여부와 환급 표시를 함께 갱신한다.
    ///
    /// 환급 표시는 "환급분을 쓸 때까지" 유지돼야 하는데, 리롤권은 낱개 구분이 없으므로
    /// <b>잔여 수가 줄어든 시점</b>을 소모로 본다. OnRerollSpent로 끄면 증강의 환급 호출과
    /// 구독 순서 경쟁이 생겨(환급이 먼저 켜면 그 뒤에 꺼버림) 표시가 유실된다.
    /// </summary>
    private void HandleRerollCountChanged(int count)
    {
        _rerollCount = count;
        RefreshShopButtonAffordability();
    }

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

    /// <summary>
    /// 유닛 배치가 거부됐을 때 전달받은 사유를
    /// 화면 중앙에 일정 시간 표시한다.
    /// </summary>
    private void HandleUnitPlacementRejected(string reason)
    {
        _toastMessage = string.IsNullOrWhiteSpace(reason)
            ? "유닛을 배치할 수 없습니다."
            : reason;

        _toastHideTime = Time.unscaledTime + ToastDuration;
    }

    // [GOLD_TRANSFER_HOLD] ⑤ 결과 표시 핸들러 일체 — 위 상태 필드 주석 참고
    //
    // /// <summary>골드 전송 성공 시 대기 상태를 해제하고 결과 문구를 갱신한다.</summary>
    // private void HandleGoldTransferCompleted(int amount)
    // {
    //     _isGoldTransferPending = false;
    //     _goldTransferResult = $"전송 완료: {amount}G";
    // }
    //
    // /// <summary>골드 전송 실패 시 대기 상태를 해제하고 실패 사유를 표시한다.</summary>
    // private void HandleGoldTransferRejected(string reason)
    // {
    //     _isGoldTransferPending = false;
    //     _goldTransferResult = $"전송 실패: {reason}";
    // }
    //
    // /// <summary>파트너에게서 골드를 받은 경우 수령 문구를 표시한다.</summary>
    // private void HandlePartnerGoldReceived(int amount)
    // {
    //     _goldTransferResult = $"파트너에게서 {amount}G 수령";
    // }

    private void OnGUI()
    {
        EnsureStyles();

        DrawOpenButton();

        // 골드/레벨/XP는 Canvas 하단 바(RefreshCanvasProgressTexts)가 표시한다.
        // IMGUI 진행 패널은 같은 값을 중복 표시하며 상점 카드 위를 덮으므로 호출하지 않는다.
        // 메서드는 Canvas 미배선 씬을 위한 폴백으로 남겨둔다.

        // [GOLD_TRANSFER_HOLD] ⑥ — 송신 경로가 여기뿐이라 이 한 줄로 기능이 닫힌다
        // DrawTradeGoldPanel();
        DrawPlusleMinunFormChoicePanel();

        if (_showMatchHistory)
            DrawMatchHistoryWindow();

        // 견본덱 창은 uGUI로 옮겼다 — SampleDeckPanelUI(Canvas)가 그린다.

        DrawToastMessage();
    }

    /// <summary>
    /// 배치 실패 등의 안내 메시지를 화면 상단 중앙에 잠시 표시한다.
    /// </summary>
    private void DrawToastMessage()
    {
        if (string.IsNullOrWhiteSpace(_toastMessage))
            return;

        if (Time.unscaledTime >= _toastHideTime)
        {
            _toastMessage = "";
            return;
        }

        const float width = 420f;
        const float height = 48f;

        float x = (Screen.width - width) * 0.5f;
        float y = 80f;

        GUI.Box(
            new Rect(x, y, width, height),
            GUIContent.none
        );

        GUI.Label(
            new Rect(x + 10f, y + 6f, width - 20f, height - 12f),
            _toastMessage,
            _toastStyle
        );
    }

    /// <summary>
    /// 견본덱 창(uGUI)이 열릴 때 IMGUI 전적창을 닫는다. IMGUI는 Canvas보다 위에 그려져
    /// 그냥 두면 견본덱 창을 덮어버린다. <see cref="SampleDeckPanelUI"/>만 부른다.
    /// </summary>
    public void CloseMatchHistoryWindow() => _showMatchHistory = false;

    // ──────────────────────────────────────────
    // [GOLD_TRANSFER_HOLD] ⑦ 패널 본체 — ⛔ 기능 보류(2026-08-18)
    //
    // 통신기는 유닛 교환만 담당하기로 정리되면서 골드 전송 UI를 통째로 닫았다.
    // GameEvents.RequestGoldTransfer를 발행하는 곳이 아래 패널뿐이라, 이 블록만 주석 처리하면
    // 송신이 성립하지 않는다(NetworkManager.SendGoldToPartner는 호출되지 않는다).
    // 되살릴 때는 이 블록과 위쪽 상태 필드·구독·핸들러 주석을 함께 풀 것.
    // ──────────────────────────────────────────
    // private void DrawTradeGoldPanel()
    // {
    //     const float width = 250f;
    //     const float height = 115f;
    //
    //     float x = 20f;
    //     float y = Screen.height - 360f;
    //
    //     GUI.Box(
    //         new Rect(x, y, width, height),
    //         "통신기 골드 전송"
    //     );
    //
    //     bool previousEnabled = GUI.enabled;
    //
    //     GUI.enabled =
    //         !_isGoldTransferPending &&
    //         _gold >= 1;
    //
    //     if (GUI.Button(
    //             new Rect(x + 15f, y + 30f, 65f, 28f),
    //             "1G"))
    //     {
    //         RequestGoldTransfer(1);
    //     }
    //
    //     GUI.enabled =
    //         !_isGoldTransferPending &&
    //         _gold >= 5;
    //
    //     if (GUI.Button(
    //             new Rect(x + 92f, y + 30f, 65f, 28f),
    //             "5G"))
    //     {
    //         RequestGoldTransfer(5);
    //     }
    //
    //     GUI.enabled =
    //         !_isGoldTransferPending &&
    //         _gold >= 10;
    //
    //     if (GUI.Button(
    //             new Rect(x + 169f, y + 30f, 65f, 28f),
    //             "10G"))
    //     {
    //         RequestGoldTransfer(10);
    //     }
    //
    //     GUI.enabled = previousEnabled;
    //
    //     string statusText =
    //         _isGoldTransferPending
    //             ? "전송 처리 중..."
    //             : _goldTransferResult;
    //
    //     GUI.Label(
    //         new Rect(x + 15f, y + 68f, 220f, 22f),
    //         statusText
    //     );
    // }
    //
    // private void RequestGoldTransfer(int amount)
    // {
    //     if (_isGoldTransferPending)
    //         return;
    //
    //     _isGoldTransferPending = true;
    //     _goldTransferResult = "";
    //
    //     GameEvents.RequestGoldTransfer(amount);
    // }

    // ──────────────────────────────────────────
    // 플러시와마이농 필드 폼 선택 UI
    //
    // 게임 규칙(자동전환/벤치 복원/합체 정규화)은 BoardManager가 담당한다.
    // 여기서는 선택 대기 유닛 참조 보관, 선택 UI 표시, 선택 결과를
    // PokemonUnit.TrySetForm에 전달, OnAllPlayersReady 강제선택, 대기 해제만 담당한다.
    // 매 프레임 GetUnitsOnBoard() 전체를 검색하지 않고 참조 하나만 들고 있는다.
    //
    // 표시는 두 갈래다. 정식 UI는 Canvas의 PlusleMinunChoicePanel이고, 아래 IMGUI
    // (DrawPlusleMinunFormChoicePanel)는 패널이 배선되지 않은 씬을 위한 폴백으로만 남는다.
    // 어느 쪽이 그리든 상태(대기 유닛·블로킹)의 단일 소스는 이 매니저다.
    // ──────────────────────────────────────────

    /// <summary>PokemonDatabase에서 플러시(311)/마이농(312) 데이터를 캐시한다. 이미 캐시됐으면 아무 일도 하지 않는다.</summary>
    private void CachePlusleMinunFormData()
    {
        if (_plusleFormData != null && _minunFormData != null)
            return;

        if (PokemonDatabase.Instance == null)
            return;

        _plusleFormData = PokemonDatabase.Instance.GetById(PLUSLE_ID);
        _minunFormData = PokemonDatabase.Instance.GetById(MINUN_ID);
    }

    /// <summary>플러시(311) 폼 데이터. 선택 UI가 아이콘·이름·스킬 설명을 읽어간다.</summary>
    public PokemonData PlusleFormData => _plusleFormData;

    /// <summary>마이농(312) 폼 데이터.</summary>
    public PokemonData MinunFormData => _minunFormData;

    /// <summary>현재 선택 대기 중인 유닛. 대기 중이 아니면 null.</summary>
    public PokemonUnit PendingFormUnit => IsPendingFormUnitValid() ? _pendingFormUnit : null;

    /// <summary>
    /// Canvas 선택 패널이 자기 존재를 알린다(활성 시 true, 비활성 시 false).
    /// 켜져 있는 동안 IMGUI 폴백은 그리지 않는다 — 같은 창이 두 번 뜨는 걸 막기 위함.
    /// </summary>
    public void SetFormChoicePanelBound(bool bound) => _formChoicePanelBound = bound;

    /// <summary>배치된 유닛이 아직 미결정(310)이면 선택 대기 참조로 보관하고 선택 UI에 알린다.</summary>
    private void HandleUnitPlacedForForm(PokemonUnit unit)
    {
        if (unit == null || unit.data == null || unit.data.id != PLUSLE_MINUN_ID)
            return;

        // 씬 초기화 순서로 Start의 캐시가 비어 있었을 수 있어 여기서 한 번 더 시도한다.
        CachePlusleMinunFormData();

        _pendingFormUnit = unit;

        GameEvents.PlusleMinunChoiceReady(unit);
    }

    /// <summary>선택 대기 중이던 유닛이 벤치로 내려가면(BoardManager가 이미 310으로 복원한 뒤) 대기 상태를 해제한다.</summary>
    private void HandleUnitBenchedForForm(PokemonUnit unit)
    {
        if (unit == _pendingFormUnit)
            CloseFormChoice();
    }

    /// <summary>
    /// 전투 시작 시점까지 미선택이면 보관된 유닛 하나에 한해 무작위로 강제 선택한다.
    /// AugmentManager.AutoSelectRandom과 동일한 트리거 이벤트(OnAllPlayersReady)·
    /// 동일한 "대기 중이던 것 하나를 무작위 자동확정" 패턴을 재사용한다.
    /// 필드 전체를 다시 검색하지 않고 보관된 참조만 사용한다.
    /// </summary>
    private void HandleAllPlayersReadyForForm() => AutoSelectRandomForm();

    /// <summary>미선택 상태의 대기 유닛을 두 폼 중 하나로 무작위 확정한다. 대기 중이 아니면 정리만 한다.</summary>
    private void AutoSelectRandomForm()
    {
        if (!IsPendingFormUnitValid())
        {
            CloseFormChoice();
            return;
        }

        PokemonData picked =
            UnityEngine.Random.value < 0.5f ? _plusleFormData : _minunFormData;

        if (picked != null)
            _pendingFormUnit.TrySetForm(picked, notifyChange: true);

        CloseFormChoice();
    }

    /// <summary>
    /// 선택 UI에서 플레이어가 폼을 골랐을 때 호출한다(Canvas 패널·IMGUI 폴백 공용 진입점).
    /// 대기 상태가 이미 풀렸으면(판매·합체 등) 무시하고 창만 닫는다.
    /// </summary>
    public void SelectPlusleMinunForm(PokemonData formData)
    {
        if (IsPendingFormUnitValid() && formData != null)
            _pendingFormUnit.TrySetForm(formData, notifyChange: true);

        CloseFormChoice();
    }

    /// <summary>대기 상태를 비우고 선택 UI에 닫으라고 알린다. 대기 중이 아니었으면 이벤트를 내지 않는다.</summary>
    private void CloseFormChoice()
    {
        if (_pendingFormUnit == null) return;

        _pendingFormUnit = null;
        GameEvents.PlusleMinunChoiceClosed();
    }

    /// <summary>
    /// 매 프레임 대기 상태를 점검해, 판매·합체 등으로 대상이 사라졌으면 창을 닫는다.
    /// IMGUI는 그리는 김에 검사했지만 Canvas 패널은 스스로 폴링하지 않으므로 여기서 닫아줘야 한다.
    /// 제한 시간은 없다 — 미선택 강제 확정은 전원 Ready(전투 시작) 시점 하나뿐이다.
    /// </summary>
    private void TickFormChoice()
    {
        if (_pendingFormUnit == null) return;

        if (!IsPendingFormUnitValid())
            CloseFormChoice();
    }

    /// <summary>
    /// 선택 대기 참조가 여전히 유효한 선택 대상인지 검증한다(참조/데이터/현재 종/필드 여부).
    /// 판매·합체·벤치 이동 등으로 더 이상 선택 대상이 아니게 됐을 수 있어
    /// 표시 직전·버튼 처리 직전·강제 선택 직전에 매번 다시 확인한다.
    /// </summary>
    private bool IsPendingFormUnitValid()
    {
        if (_pendingFormUnit == null) return false;
        if (_pendingFormUnit.data == null) return false;
        if (_pendingFormUnit.data.id != PLUSLE_MINUN_ID) return false;
        if (!_pendingFormUnit.isOnBoard) return false;

        return true;
    }

    /// <summary>선택 UI가 떠 있는 동안 3D 조작(드래그)을 막기 위해 UnitDragController가 참조한다.</summary>
    public bool IsPlusleMinunChoiceBlocking => IsPendingFormUnitValid();

    /// <summary>
    /// 파트너 전체화면 관전 중인가(PIP 제외, SetExpanded(true) 한정). 관전 오버레이(RawImage)는
    /// Main Camera 기반 Physics 입력을 막지 못하므로, UnitDragController/SellZone처럼 3D 물리
    /// 입력을 직접 다루는 쪽이 이 값을 보고 스스로 차단해야 한다(2026-08 입력 관통 버그 대응).
    /// </summary>
    public bool IsPartnerSpectateExpanded => _isPartnerSpectateExpanded;

    /// <summary>
    /// Canvas 패널이 없는 씬을 위한 IMGUI 폴백. 패널이 배선돼 있으면 그리지 않는다.
    /// 상태 갱신(대기 해제·타이머)은 TickFormChoice가 하므로 여기서는 표시와 입력만 다룬다.
    /// </summary>
    private void DrawPlusleMinunFormChoicePanel()
    {
        if (_formChoicePanelBound) return;
        if (!IsPendingFormUnitValid()) return;

        GUI.depth = -100; // 증강 선택창과 동일하게 다른 OnGUI보다 먼저 입력을 받는다.

        const float width = 260f;
        const float height = 120f;
        float x = (Screen.width - width) * 0.5f;
        float y = (Screen.height - height) * 0.5f;
        var panelRect = new Rect(x, y, width, height);

        GUI.Box(panelRect, $"{_pendingFormUnit.data.pokemonName}의 형태를 선택하세요");

        var plusleRect = new Rect(x + 15f, y + 40f, 100f, 50f);
        var minunRect = new Rect(x + 145f, y + 40f, 100f, 50f);

        if (GUI.Button(plusleRect, "플러시"))
        {
            SelectPlusleMinunForm(_plusleFormData);
            return;
        }

        if (GUI.Button(minunRect, "마이농"))
        {
            SelectPlusleMinunForm(_minunFormData);
            return;
        }
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
        const float shopSlotWidth = 145f;
        const float shopSlotGap = 6f;

        float shopTotalWidth = shopSlotCount * shopSlotWidth + (shopSlotCount - 1) * shopSlotGap;

        float shopStartX = (Screen.width - shopTotalWidth) / 2f;

        // 유닛 상점 왼쪽에 패널 배치.
        // 작은 해상도에서는 화면 밖으로 나가지 않도록 최소 10px을 보장한다.
        float x = Mathf.Max(10f, shopStartX - width - 20f);
        
        float y = Screen.height - 160f;

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

        _toastStyle = new GUIStyle(_labelStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold
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

}
