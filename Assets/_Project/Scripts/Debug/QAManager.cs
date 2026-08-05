#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
#endif

/// <summary>
/// 통합 QA / GM 콘솔(IMGUI). 기존 게임 매니저의 공개 프로퍼티·메서드·이벤트만 조회/호출하는
/// 단방향 전용 도구다 — QAManager → 기존 매니저 방향으로만 의존하며, 어떤 기존 스크립트도
/// QAManager를 참조하지 않는다. 이 오브젝트/스크립트를 삭제해도 기존 게임은 영향받지 않는다.
///
/// 창 구조: (1) 작은 메인 상태창 → (2) 탭 클릭 시 여는 단일 재사용 기능 창 → (3) 유닛 선택 시
/// 여는 단일 재사용 상세 창. 동시에 열릴 수 있는 창은 최대 3개이며 종류별로 항상 1개만 존재한다.
///
/// QA_MainPanel(또는 임의의 오브젝트) 하나에 부착해 사용한다. 씬/프리팹은 이번 작업에서
/// 수정하지 않았으므로, 부착과 Inspector 연결(있다면)은 별도로 진행해야 한다.
/// </summary>
public class QAManager : MonoBehaviour
{
    private enum Tab { Player, Unit, Item, Pool, Network }
    private enum UnitInspectMode { Database, Runtime }

    private const int MAX_LOG_COUNT = 40;

    private static readonly int[] DELTA_STEPS = { 10, 5, 1 };

    // ─────────────────────────────────────────
    // 창 크기 규칙(공통) — 화면 해상도 변경 시 매 프레임 재클램프한다.
    // ─────────────────────────────────────────

    private const float RESIZE_HANDLE_SIZE = 16f;

    private static float MaxWindowWidth => Screen.width - 10f;
    private static float MaxWindowHeight => Screen.height - 10f;

    /// <summary>창을 그리기 직전 매 프레임 호출 — 해상도가 줄어들어도 다음 프레임에 즉시 재제한된다.</summary>
    private static void ClampRectSize(ref Rect rect, float minW, float minH)
    {
        float maxW = Mathf.Max(minW, MaxWindowWidth);
        float maxH = Mathf.Max(minH, MaxWindowHeight);
        rect.width = Mathf.Clamp(rect.width, minW, maxW);
        rect.height = Mathf.Clamp(rect.height, minH, maxH);
    }

    /// <summary>창이 화면 밖으로 완전히 빠지지 않게 최소한의 여백만 남기고 클램프한다.</summary>
    private static void ClampRectToScreen(ref Rect rect)
    {
        const float visibleMargin = 60f;
        rect.x = Mathf.Clamp(rect.x, -(rect.width - visibleMargin), Screen.width - visibleMargin);
        rect.y = Mathf.Clamp(rect.y, 0f, Screen.height - visibleMargin);
    }

    /// <summary>
    /// 창 우하단 리사이즈 핸들. Unity IMGUI에는 리사이즈 가능한 Window가 기본 제공되지 않아
    /// GUI 이벤트를 직접 읽어 드래그 델타만큼 rect.width/height를 늘리고 최소/최대 크기를
    /// 벗어나지 않게 클램프한다(순수 UI 로직 — 게임 계산식과 무관). 최대 크기는 항상 현재
    /// Screen.width/height 기준이라 리사이즈 도중 해상도가 바뀌어도 그 프레임부터 반영된다.
    /// </summary>
    private static void DrawResizeHandle(ref Rect rect, float minWidth, float minHeight)
    {
        var handleRect = new Rect(rect.width - RESIZE_HANDLE_SIZE, rect.height - RESIZE_HANDLE_SIZE,
                                   RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE);
        GUI.Box(handleRect, "◢");

        Event e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);

        switch (e.GetTypeForControl(id))
        {
            case EventType.MouseDown:
                if (handleRect.Contains(e.mousePosition))
                {
                    GUIUtility.hotControl = id;
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == id)
                {
                    float maxW = Mathf.Max(minWidth, MaxWindowWidth);
                    float maxH = Mathf.Max(minHeight, MaxWindowHeight);
                    rect.width = Mathf.Clamp(rect.width + e.delta.x, minWidth, maxW);
                    rect.height = Mathf.Clamp(rect.height + e.delta.y, minHeight, maxH);
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == id)
                {
                    GUIUtility.hotControl = 0;
                    e.Use();
                }
                break;
        }
    }

    // ─────────────────────────────────────────
    // ① 메인 상태창 — 상태 정보 + 탭 버튼만. 실제 기능은 여기 없음(작게 유지).
    // ─────────────────────────────────────────

    private const float MIN_MAIN_WIDTH = 430f;
    private const float MIN_MAIN_HEIGHT = 170f;

    private bool _mainOpen;
    private Rect _mainRect = new Rect(0f, 0f, 460f, 230f);
    private bool _mainRectInitialized;

    // ─────────────────────────────────────────
    // ② 기능 창(단일 재사용) — 탭 버튼을 누르면 열리고, 다른 탭을 누르면 내용만 교체된다.
    // ─────────────────────────────────────────

    private const float MIN_FEATURE_WIDTH = 600f;
    private const float MIN_FEATURE_HEIGHT = 400f;

    private bool _featureWindowOpen;
    private Tab _currentTab = Tab.Player;
    private Rect _featureWindowRect = new Rect(0f, 0f, 700f, 520f);
    private bool _featureWindowRectInitialized;
    private Vector2 _featureWindowScroll;

    // ─────────────────────────────────────────
    // ③ 상세 창(단일 재사용) — 유닛 목록 클릭 시 열리고, 새 선택 시 내용만 교체된다.
    // ─────────────────────────────────────────

    private const float MIN_DETAIL_WIDTH = 360f;
    private const float MIN_DETAIL_HEIGHT = 300f;

    private bool _detailWindowOpen;
    private bool _detailWindowRectInitialized;
    private Rect _detailWindowRect = new Rect(0f, 0f, 440f, 520f);
    private Vector2 _detailWindowScroll;

    // ─────────────────────────────────────────
    // GlobalStatus 캐시 (GameEvents 구독으로 갱신)
    // ─────────────────────────────────────────

    private GamePhase _phase;
    private int _round;
    private string _stageId = "-";
    private bool _isBattle;
    private bool _isOvertime;

    private int _gold;
    private int _itemCoupon;
    private int _inventoryCount;
    private int _inventoryMax;

    // null = 아직 한 번도 동기화 이벤트를 못 받음(=동기화 대기).
    private int? _partnerGold;

    // ─────────────────────────────────────────
    // 로그
    // ─────────────────────────────────────────

    private readonly List<string> _logs = new();
    private Vector2 _logScroll;

    // ─────────────────────────────────────────
    // 유닛 탭 — 공통 선택 상태
    // ─────────────────────────────────────────

    private UnitInspectMode _unitInspectMode = UnitInspectMode.Database;

    // DB 조회 모드 선택
    private PokemonData _selectedUnitData;
    private bool _selectedUnitPoolQueried;
    private bool _selectedUnitPoolFound;
    private int _selectedUnitRemaining;
    private int _selectedUnitInitial;

    // 실제 유닛 조회 모드 선택
    private PokemonUnit _selectedRuntimeUnit;

    private Vector2 _unitListScroll;

    // ─────────────────────────────────────────
    // 유닛 탭 — DB 검색
    // ─────────────────────────────────────────

    private struct UnitSearchEntry
    {
        public PokemonData data;
        public bool poolFound;
        public int poolRemaining;
        public int poolInitial;
    }

    private enum UnitSortKey { Name, Id, Cost, Pool }

    // 랜덤 획득 버튼 행과 풀 수량 행이 공유하는 셀 폭. 기능 창 최소 폭(600)에 5칸이 들어가야 하므로 110으로 유지.
    private const float COST_CELL_WIDTH = 110f;

    private string _unitQuery = "";
    private string _unitSearchedQuery; // 마지막으로 실제 검색을 수행한 질의(재검색 트리거 감지용)
    private int _unitCostFilter; // 0=전체, 1~5=해당 코스트만
    private UnitSortKey _unitSortKey = UnitSortKey.Id;
    private bool _unitSortDescending;
    private readonly List<UnitSearchEntry> _unitResults = new();

    // ─────────────────────────────────────────
    // 아이템 탭
    // ─────────────────────────────────────────

    private enum ItemCategory { All, Equipment, Stone, Tool }

    [Header("아이템 탭 — 도구 지급용 데이터")]
    [Tooltip("재조합기 표시/지급용 실제 ConsumableData. ItemInventoryUI의 같은 필드와 동일한 에셋" +
             "(Reforger_Consumable)을 연결할 것 — 여기서 새로 만들거나 이름으로 검색하지 않는다.\n" +
             "제거기는 시작부터 보유하는 무제한 도구라(소비되지 않음) QA 지급 대상에서 제외한다.")]
    [SerializeField] private ConsumableData _reforgerData;

    /// <summary>
    /// 검색 결과 한 줄 + 보조 설명 캐시. UnitSearchEntry(풀 정보)와 같은 패턴 —
    /// RunItemSearch가 실행될 때 한 번만 채우고, DrawItemResultRow는 매 프레임 그대로 읽기만 한다.
    /// </summary>
    private struct ItemSearchEntry
    {
        public ScriptableObject data;
        public string description; // 장비/도구=ItemData.description, 돌=설명+진화 가능 대상(BuildStoneDescription)
    }

    private ItemCategory _itemCategory = ItemCategory.All;
    private string _itemQuery = "";
    private string _itemSearchedQuery;
    private readonly List<ItemSearchEntry> _itemResults = new();
    private Vector2 _itemListScroll;
    private Vector2 _myInventoryScroll;
    private Vector2 _equippedScroll;
    private GUIStyle _wrapLabelStyle; // 지급 목록 보조 설명용 줄바꿈 라벨(OnGUI 중 1회 생성 후 재사용)

    // 도구 데이터 미배선 경고가 검색할 때마다(키 입력마다) 반복 출력되지 않도록 세션당 1회만 남긴다.
    private bool _toolDataMissingWarned;

    // ─────────────────────────────────────────
    // 공용 풀 탭
    // ─────────────────────────────────────────

    private struct PoolEntry
    {
        public PokemonData data;
        public int initial;
        public int remaining;
    }

    private const float POOL_REFRESH_DELAY = 0.6f; // 공유 풀 RPC 왕복을 기다리는 지연 갱신 시간(초)

    private bool _poolComputed;
    private bool _poolPending; // 랜덤 획득 요청 후 지연 갱신 코루틴이 대기 중인지("계산 중" 표시용)
    private readonly List<PoolEntry> _poolEntries = new();
    private readonly int[] _poolInitialByCost = new int[6]; // index 1~5 사용
    private readonly int[] _poolRemainingByCost = new int[6];
    private Vector2 _poolScroll;

    // ─────────────────────────────────────────
    // 이벤트 구독
    // ─────────────────────────────────────────

    private void OnEnable()
    {
        GameEvents.OnGoldChanged        += HandleGoldChanged;
        GameEvents.OnItemCouponChanged  += HandleItemCouponChanged;
        GameEvents.OnInventoryChanged   += HandleInventoryChanged;
        GameEvents.OnPhaseChanged       += HandlePhaseChanged;
        GameEvents.OnRoundChanged       += HandleRoundChanged;
        GameEvents.OnStageEntered       += HandleStageEntered;
        GameEvents.OnBattleStart        += HandleBattleStart;
        GameEvents.OnBattleEnd          += HandleBattleEnd;
        GameEvents.OnOvertimeStarted    += HandleOvertimeStarted;
        GameEvents.OnPartnerGoldChanged += HandlePartnerGoldChanged;

        SyncInitialState();
    }

    private void OnDisable()
    {
        GameEvents.OnGoldChanged        -= HandleGoldChanged;
        GameEvents.OnItemCouponChanged  -= HandleItemCouponChanged;
        GameEvents.OnInventoryChanged   -= HandleInventoryChanged;
        GameEvents.OnPhaseChanged       -= HandlePhaseChanged;
        GameEvents.OnRoundChanged       -= HandleRoundChanged;
        GameEvents.OnStageEntered       -= HandleStageEntered;
        GameEvents.OnBattleStart        -= HandleBattleStart;
        GameEvents.OnBattleEnd          -= HandleBattleEnd;
        GameEvents.OnOvertimeStarted    -= HandleOvertimeStarted;
        GameEvents.OnPartnerGoldChanged -= HandlePartnerGoldChanged;
    }

    /// <summary>QAManager가 늦게 활성화돼 이전에 발행된 이벤트를 놓쳤을 경우를 보완하는 1회 동기화.</summary>
    private void SyncInitialState()
    {
        if (!GameManager.TryGet(out var gm)) return;

        if (gm.Phase != null)
        {
            _phase = gm.Phase.CurrentPhase;
            _round = gm.Phase.CurrentRound;
            _stageId = gm.Phase.CurrentStage != null ? gm.Phase.CurrentStage.stageId : "-";
            _isBattle = _phase == GamePhase.Battle;
        }

        if (gm.Shop != null) _gold = gm.Shop.Gold;

        if (gm.Item != null)
        {
            _itemCoupon = gm.Item.ItemCoupon;
            _inventoryCount = gm.Item.InventoryCount;
            _inventoryMax = gm.Item.InventoryCount + gm.Item.AvailableInventorySpace;
        }
    }

    private void HandleGoldChanged(int gold) => _gold = gold;
    private void HandleItemCouponChanged(int coupon) => _itemCoupon = coupon;

    private void HandleInventoryChanged()
    {
        if (!GameManager.TryGet(out var gm) || gm.Item == null) return;
        _inventoryCount = gm.Item.InventoryCount;
        _inventoryMax = gm.Item.InventoryCount + gm.Item.AvailableInventorySpace;
    }

    private void HandlePhaseChanged(GamePhase phase)
    {
        _phase = phase;
        _isBattle = phase == GamePhase.Battle;
        if (!_isBattle) _isOvertime = false; // 전투가 아니면 오버타임도 아님(방어적 리셋)
    }

    private void HandleRoundChanged(int round) => _round = round;
    private void HandleStageEntered(StageData stage) => _stageId = stage != null ? stage.stageId : "-";
    private void HandleBattleStart() => _isOvertime = false;
    private void HandleBattleEnd(BattleEndReason reason) => _isOvertime = false;
    private void HandleOvertimeStarted(float duration) => _isOvertime = true;
    private void HandlePartnerGoldChanged(int gold) => _partnerGold = gold;

    // ─────────────────────────────────────────
    // 로그
    // ─────────────────────────────────────────

    private void LogSuccess(string action, string detail) => AddLog("성공", action, detail);
    private void LogFailure(string action, string reason) => AddLog("실패", action, reason);

    private void AddLog(string tag, string action, string detail)
    {
        string time = System.DateTime.Now.ToString("HH:mm:ss");
        _logs.Add($"[{time}][{tag}] {action}: {detail}");

        while (_logs.Count > MAX_LOG_COUNT)
            _logs.RemoveAt(0);

        _logScroll.y = float.MaxValue; // 새 로그로 자동 스크롤
    }

    // ─────────────────────────────────────────
    // OnGUI 진입점 — 세 창을 각각 프레임당 정확히 한 번씩만 그린다.
    // ─────────────────────────────────────────

    private void OnGUI()
    {
        DrawOpenButton();

        if (_mainOpen)
        {
            if (!_mainRectInitialized)
            {
                _mainRect.x = Screen.width - _mainRect.width - 16f;
                _mainRect.y = 16f;
                _mainRectInitialized = true;
            }

            ClampRectSize(ref _mainRect, MIN_MAIN_WIDTH, MIN_MAIN_HEIGHT);
            _mainRect = GUILayout.Window(GetInstanceID(), _mainRect, DrawMainStatusWindow, "QA / GM 콘솔");
            ClampRectToScreen(ref _mainRect);
        }

        if (_featureWindowOpen)
        {
            if (!_featureWindowRectInitialized)
            {
                _featureWindowRect.x = Mathf.Max(0f, _mainRect.x - _featureWindowRect.width - 10f);
                _featureWindowRect.y = _mainRect.y;
                _featureWindowRectInitialized = true;
            }

            ClampRectSize(ref _featureWindowRect, MIN_FEATURE_WIDTH, MIN_FEATURE_HEIGHT);
            _featureWindowRect = GUILayout.Window(GetInstanceID() + 1, _featureWindowRect, DrawFeatureWindow, FeatureTitle());
            ClampRectToScreen(ref _featureWindowRect);
        }

        if (_detailWindowOpen)
        {
            if (!_detailWindowRectInitialized)
            {
                _detailWindowRect.x = Mathf.Min(_featureWindowRect.x + _featureWindowRect.width + 10f,
                                                 Screen.width - _detailWindowRect.width);
                _detailWindowRect.y = _featureWindowRect.y;
                _detailWindowRectInitialized = true;
            }

            ClampRectSize(ref _detailWindowRect, MIN_DETAIL_WIDTH, MIN_DETAIL_HEIGHT);
            _detailWindowRect = GUILayout.Window(GetInstanceID() + 2, _detailWindowRect, DrawDetailWindow,
                                                  _unitInspectMode == UnitInspectMode.Database ? "DB 정보 상세" : "실제 유닛 상세");
            ClampRectToScreen(ref _detailWindowRect);
        }
    }

    private void DrawOpenButton()
    {
        const float w = 70f, h = 28f;
        var rect = new Rect(Screen.width - w - 8f, Screen.height - h - 8f, w, h);
        if (GUI.Button(rect, _mainOpen ? "QA ▼" : "QA ▲"))
            _mainOpen = !_mainOpen;
    }

    // ─────────────────────────────────────────
    // ① 메인 상태창
    // ─────────────────────────────────────────

    private void DrawMainStatusWindow(int windowId)
    {
        GUILayout.BeginVertical();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("새로고침", GUILayout.Width(80f)))
        {
            SyncInitialState();
            TryRefreshPoolSummary();
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("닫기", GUILayout.Width(60f)))
            _mainOpen = false;
        GUILayout.EndHorizontal();

        DrawGlobalStatus();
        DrawTabBar();

        GUILayout.EndVertical();

        DrawResizeHandle(ref _mainRect, MIN_MAIN_WIDTH, MIN_MAIN_HEIGHT);
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
    }

    private void DrawGlobalStatus()
    {
        GUILayout.BeginVertical(GUI.skin.box);

        if (!GameManager.TryGet(out var gm))
        {
            GUILayout.Label("GameManager 연결 안 됨");
            GUILayout.EndVertical();
            return;
        }

        string battleLabel = _isBattle ? (_isOvertime ? "전투 중(오버타임)" : "전투 중") : "전투 아님";
        GUILayout.Label($"Phase: {PhaseLabel(_phase)} │ Round: {_round} │ Stage: {_stageId} │ {battleLabel}");

        NetworkManager net = gm.Network;
        string role = net == null ? "확인 불가" : (net.IsMasterClient ? "Master" : "Guest");
        string connState = net == null ? "확인 불가" : (net.IsConnected ? "연결됨" : "연결 안 됨");
        string roomState = net == null ? "확인 불가" : (net.IsInRoom ? "룸 입장" : "룸 미입장");
        int playerCount = net != null ? net.PlayerCount : 0;
        GUILayout.Label($"역할: {role} │ 네트워크: {connState} │ {roomState} │ 인원: {playerCount}");

        string invLabel = _inventoryMax > 0 ? $"{_inventoryCount}/{_inventoryMax}" : $"{_inventoryCount}/-";
        GUILayout.Label($"내 골드 {_gold} │ 아이템 쿠폰 {_itemCoupon} │ 인벤토리 {invLabel}");

        string partnerGoldLabel = (net == null || playerCount < 2)
            ? "확인 불가"
            : (_partnerGold.HasValue ? _partnerGold.Value.ToString() : "동기화 대기");
        GUILayout.Label($"파트너 골드 {partnerGoldLabel}");

        GUILayout.EndVertical();
    }

    private static string PhaseLabel(GamePhase phase) => phase switch
    {
        GamePhase.Lobby => "Lobby",
        GamePhase.Shopping => "Shopping",
        GamePhase.Battle => "Battle",
        GamePhase.Result => "Result",
        GamePhase.Victory => "Victory",
        GamePhase.GameOver => "GameOver",
        _ => phase.ToString()
    };

    // ─────────────────────────────────────────
    // TabBar — 클릭 시 기능 창을 열거나(닫혀 있으면) 내용만 교체한다(열려 있으면).
    // ─────────────────────────────────────────

    private void DrawTabBar()
    {
        GUILayout.BeginHorizontal();
        DrawTabButton(Tab.Player,  "플레이어");
        DrawTabButton(Tab.Unit,    "유닛");
        DrawTabButton(Tab.Item,    "아이템");
        DrawTabButton(Tab.Pool,    "공용 풀");
        DrawTabButton(Tab.Network, "네트워크");
        GUILayout.EndHorizontal();
    }

    private void DrawTabButton(Tab tab, string label)
    {
        bool selected = _featureWindowOpen && _currentTab == tab;
        if (GUILayout.Toggle(selected, label, "Button") && (!_featureWindowOpen || _currentTab != tab))
        {
            _currentTab = tab;
            _featureWindowOpen = true;
            _featureWindowScroll = Vector2.zero;
            if (tab == Tab.Unit) TryRefreshPoolSummary();
        }
    }

    private string FeatureTitle() => _currentTab switch
    {
        Tab.Player  => "플레이어 QA 기능",
        Tab.Unit    => "유닛 QA 기능",
        Tab.Item    => "아이템 QA 기능",
        Tab.Pool    => "공용 풀 기능",
        Tab.Network => "네트워크 기능",
        _ => "QA 기능"
    };

    // ─────────────────────────────────────────
    // ② 기능 창(단일 재사용)
    // ─────────────────────────────────────────

    private void DrawFeatureWindow(int windowId)
    {
        GUILayout.BeginVertical();

        if (GUILayout.Button("닫기", GUILayout.Width(60f)))
            _featureWindowOpen = false;

        _featureWindowScroll = GUILayout.BeginScrollView(_featureWindowScroll, GUI.skin.box);

        switch (_currentTab)
        {
            case Tab.Player:  DrawPlayerTab();  break;
            case Tab.Unit:    DrawUnitTab();    break;
            case Tab.Item:    DrawItemTab();    break;
            case Tab.Pool:    DrawPoolTab();    break;
            case Tab.Network: DrawNetworkTab(); break;
        }

        GUILayout.EndScrollView();

        DrawActionLog();

        GUILayout.EndVertical();

        DrawResizeHandle(ref _featureWindowRect, MIN_FEATURE_WIDTH, MIN_FEATURE_HEIGHT);
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
    }

    // ─────────────────────────────────────────
    // 플레이어 탭
    // ─────────────────────────────────────────

    private void DrawPlayerTab()
    {
        if (!GameManager.TryGet(out var gm))
        {
            GUILayout.Label("GameManager 연결 안 됨");
            return;
        }

        DrawGoldSection(gm);
        GUILayout.Space(8f);
        DrawCouponSection(gm);

        GUILayout.Space(8f);
        GUILayout.Label("파트너 골드/쿠폰/인벤토리 조작은 지원하지 않습니다(파트너 매니저에 접근 가능한 공개 API 없음).");
    }

    private void DrawGoldSection(GameManager gm)
    {
        ShopManager shop = gm.Shop;
        GUILayout.Label($"내 골드: {(shop != null ? shop.Gold.ToString() : "ShopManager 연결 안 됨")}");

        if (shop == null) return;

        GUILayout.BeginHorizontal();
        foreach (int step in DELTA_STEPS)
            if (GUILayout.Button($"-{step}", GUILayout.Width(50f)))
                ApplyGoldDelta(shop, -step);
        foreach (int step in ReverseSteps())
            if (GUILayout.Button($"+{step}", GUILayout.Width(50f)))
                ApplyGoldDelta(shop, step);
        GUILayout.EndHorizontal();
    }

    private void ApplyGoldDelta(ShopManager shop, int delta)
    {
        int before = shop.Gold;
        shop.AddGold(delta); // AddGold 내부에서 Mathf.Max(0, ...)로 자체 클램프됨(ShopManager.cs 확인됨)
        int after = shop.Gold;

        if (after == before && delta != 0)
            LogFailure($"내 골드 {SignedLabel(delta)}", $"변경 없음(현재 {before})");
        else
            LogSuccess($"내 골드 {SignedLabel(delta)}", $"{before} → {after}");
    }

    private void DrawCouponSection(GameManager gm)
    {
        ItemManager item = gm.Item;
        GUILayout.Label($"내 아이템 쿠폰: {(item != null ? item.ItemCoupon.ToString() : "ItemManager 연결 안 됨")}");

        if (item == null) return;

        GUILayout.BeginHorizontal();
        foreach (int step in DELTA_STEPS)
            if (GUILayout.Button($"-{step}", GUILayout.Width(50f)))
                ApplyCouponDelta(item, -step);
        foreach (int step in ReverseSteps())
            if (GUILayout.Button($"+{step}", GUILayout.Width(50f)))
                ApplyCouponDelta(item, step);
        GUILayout.EndHorizontal();
    }

    private void ApplyCouponDelta(ItemManager item, int delta)
    {
        int before = item.ItemCoupon;
        item.AddItemCoupon(delta); // 내부에서 Mathf.Max(0, ...)로 자체 클램프됨(ItemManager.cs 확인됨)
        int after = item.ItemCoupon;

        if (after == before && delta != 0)
            LogFailure($"내 아이템 쿠폰 {SignedLabel(delta)}", $"변경 없음(현재 {before})");
        else
            LogSuccess($"내 아이템 쿠폰 {SignedLabel(delta)}", $"{before} → {after}");
    }

    private static IEnumerable<int> ReverseSteps()
    {
        for (int i = DELTA_STEPS.Length - 1; i >= 0; i--)
            yield return DELTA_STEPS[i];
    }

    private static string SignedLabel(int amount) => amount >= 0 ? $"+{amount}" : amount.ToString();

    // ─────────────────────────────────────────
    // 유닛 탭
    // ─────────────────────────────────────────

    /// <summary>
    /// 기능 창에는 A(랜덤 획득) / B(조회 모드 전환) / C(목록)만 표시한다. 로그(D)는 기능 창
    /// 공통 하단(DrawFeatureWindow의 DrawActionLog)에서 모든 탭에 공통으로 표시된다.
    /// 유닛 상세는 여기 임베드하지 않고, 목록 행을 클릭하면 단일 재사용 상세 창이 자동으로 열린다.
    /// </summary>
    private void DrawUnitTab()
    {
        GUILayout.Label("A. 코스트별 랜덤 1성 실제 획득 (내 벤치)");
        DrawUnitGrantButtons();
        DrawCostPoolSummary();
        DrawBenchQuickSellButton();

        GUILayout.Space(10f);
        GUILayout.Label("B. 조회 모드");
        DrawUnitInspectModeToggle();

        GUILayout.Space(6f);
        GUILayout.Label("C. 조회 목록");
        if (_unitInspectMode == UnitInspectMode.Database)
        {
            DrawUnitSearch();
            DrawUnitResultList();
        }
        else
        {
            DrawRuntimeUnitList();
        }
    }

    private void DrawUnitGrantButtons()
    {
        GUILayout.BeginHorizontal();
        for (int cost = 1; cost <= 5; cost++)
        {
            int c = cost;
            if (GUILayout.Button($"{c}코 랜덤 획득", GUILayout.Width(COST_CELL_WIDTH)))
                HandleGrantByCost(c);
        }
        GUILayout.EndHorizontal();
    }

    /// <summary>
    /// 랜덤 획득 버튼 바로 아래, 버튼과 정확히 같은 셀 폭(COST_CELL_WIDTH)으로 5칸을 한 줄에 배치한다.
    /// 공용 풀 탭(RecomputePool)이 채우는 _poolInitialByCost/_poolRemainingByCost 캐시를
    /// 그대로 읽기만 한다 — 별도 계산식을 만들지 않는다.
    /// </summary>
    private void DrawCostPoolSummary()
    {
        if (!GameManager.TryGet(out var gm) || gm.Shop == null)
        {
            GUILayout.Label("공용 풀 요약: 확인 불가(ShopManager 연결 안 됨)");
            return;
        }

        if (_poolPending)
        {
            GUILayout.Label("공용 풀 요약: 계산 중...");
            return;
        }

        if (!_poolComputed)
        {
            GUILayout.Label("공용 풀 요약: 새로고침 필요");
            return;
        }

        GUILayout.BeginHorizontal();
        for (int cost = 1; cost <= 5; cost++)
        {
            string text = $"{cost}코 {_poolRemainingByCost[cost]}/{_poolInitialByCost[cost]}";
            GUILayout.Label(text, GUILayout.Width(COST_CELL_WIDTH));
        }
        GUILayout.EndHorizontal();
    }

    /// <summary>
    /// 공용 풀 탭의 RecomputePool과 완전히 동일한 캐시를 채우는 유일한 진입점.
    /// 유닛 탭 요약(DrawCostPoolSummary)도 이 메서드가 채운 같은 배열을 읽으므로
    /// 두 탭의 표시값이 항상 일치한다(중복 계산식 없음).
    /// </summary>
    private void TryRefreshPoolSummary()
    {
        if (!GameManager.TryGet(out var gm) || gm.Shop == null) return;
        RecomputePool(gm.Shop);
    }

    /// <summary>
    /// ShopManager.DebugGrantUnitByCost(cost)를 그대로 호출한다 — UnitFactory.Create(data, 1)
    /// 기본 성급(1성)으로 생성하고 BoardManager.TryPlaceInBench로 배치하며 공용 풀을 정상
    /// 차감하는 기존 QA 전용 API다(ShopManager.cs 확인). 새 생성 경로를 만들지 않는다.
    /// 이 메서드는 어떤 종이 지급됐는지 직접 알려주지 않으므로(bool만 반환, 공유 풀 모드는
    /// RPC 비동기 처리), 로그에 종명을 넣기 위해 호출 전/후 벤치 스냅샷을 비교한다.
    /// </summary>
    private void HandleGrantByCost(int cost)
    {
        string action = $"{cost}코 랜덤 획득";

        if (!GameManager.TryGet(out var gm) || gm.Shop == null)
        {
            LogFailure(action, "ShopManager 연결 안 됨");
            return;
        }

        if (gm.Board == null)
        {
            LogFailure(action, "BoardManager 연결 안 됨");
            return;
        }

        if (!gm.Board.HasBenchSpace())
        {
            LogFailure(action, "벤치가 가득 참");
            return;
        }

        var benchBefore = new HashSet<PokemonUnit>(gm.Board.GetUnitsInBench());

        bool success = gm.Shop.DebugGrantUnitByCost(cost);

        if (!success)
        {
            LogFailure(action, "요청 실패(해당 코스트 공용 풀 부족 또는 네트워크 요청 거부 — 콘솔 로그 참고)");
            return;
        }

        string grantedName = FindNewBenchUnitName(gm.Board, benchBefore);
        if (grantedName != null)
            LogSuccess(action, $"{grantedName} 1성 벤치 지급 완료");
        else
            LogSuccess(action, "요청 완료(공유 풀 모드는 지급까지 잠시 걸릴 수 있음 — 벤치에서 확인)");

        // 공유 풀 모드는 RPC 왕복이 비동기라 이 시점엔 아직 반영 전일 수 있다. 즉시 표시를
        // 바꾸지 않고, 잠시 뒤 ShopManager.TryGetPoolDebugInfo를 다시 읽어(RecomputePool)
        // 실제 값으로 갱신한다. 그동안 유닛 탭 요약은 "계산 중"으로 표시된다.
        _poolComputed = false;
        _poolPending = true;
        StartCoroutine(DelayedPoolRefresh());
    }

    private IEnumerator DelayedPoolRefresh()
    {
        yield return new WaitForSeconds(POOL_REFRESH_DELAY);

        _poolPending = false;
        TryRefreshPoolSummary();
        if (_unitSearchedQuery != null) RunUnitSearch(); // 검색 결과 풀 수량도 같은 시점에 갱신
    }

    private static string FindNewBenchUnitName(BoardManager board, HashSet<PokemonUnit> before)
    {
        foreach (PokemonUnit unit in board.GetUnitsInBench())
        {
            if (unit != null && unit.data != null && !before.Contains(unit))
                return unit.data.pokemonName;
        }
        return null;
    }

    /// <summary>
    /// 벤치 슬롯 번호가 가장 앞인 유닛 1마리만 판매하는 QA 단축 버튼. 필드 유닛은 GetBenchSnapshot()에
    /// 애초에 포함되지 않으므로 절대 대상이 되지 않는다. 판매/풀 갱신이 끝나기 전(=_poolPending)에는
    /// 버튼을 비활성화해 같은 유닛이 중복 판매되지 않게 막는다(랜덤 획득 버튼과 동일한 대기 플래그 재사용).
    /// </summary>
    private void DrawBenchQuickSellButton()
    {
        bool prevEnabled = GUI.enabled;
        GUI.enabled = !_poolPending;
        if (GUILayout.Button("벤치 첫 유닛 판매", GUILayout.Width(COST_CELL_WIDTH * 2f)))
            HandleSellFirstBenchUnit();
        GUI.enabled = prevEnabled;
    }

    /// <summary>
    /// SellZone.cs(OnDropUnit → SellUnit)가 실제로 쓰는 것과 완전히 동일한 정식 판매 경로,
    /// BoardManager.SellUnit(공개 메서드)만 호출한다. 이 한 호출이 GameEvents.OnUnitSold를
    /// 발행해 ShopManager.HandleUnitSold(골드 지급 + 챔피언 풀 반환)와 ItemManager.HandleUnitSold
    /// (PokemonUnit.PrepareForSell()로 장착 아이템·진화의 돌 회수)를 자동으로 트리거하고,
    /// BoardSyncBroadcaster/SynergyManager/BoardView 등 다른 구독자도 함께 동작한다.
    /// QAManager는 Destroy·AddGold·풀 증가·아이템 제거·리스트 Remove를 직접 하지 않는다.
    /// </summary>
    private void HandleSellFirstBenchUnit()
    {
        const string action = "벤치 첫 유닛 판매";

        if (_poolPending) return; // 처리/갱신 중 중복 클릭 방어

        if (!GameManager.TryGet(out var gm) || gm.Board == null)
        {
            LogFailure(action, "BoardManager 연결 안 됨");
            return;
        }

        IReadOnlyList<PokemonUnit> bench = gm.Board.GetBenchSnapshot();
        PokemonUnit target = null;
        int targetSlot = -1;

        if (bench != null)
        {
            for (int i = 0; i < bench.Count; i++)
            {
                if (bench[i] != null && bench[i].data != null) // Unity 오버로드 null 체크(파괴된 참조 방어)
                {
                    target = bench[i];
                    targetSlot = i;
                    break;
                }
            }
        }

        if (target == null)
        {
            LogFailure(action, "판매할 유닛 없음");
            return;
        }

        string unitName = target.data.pokemonName;
        int starLevel = target.starLevel;
        int goldBefore = gm.Shop != null ? gm.Shop.Gold : 0;

        bool success = gm.Board.SellUnit(target);

        if (!success)
        {
            LogFailure(action, "판매 실패(BoardManager.SellUnit이 false 반환)");
            return;
        }

        int goldAfter = gm.Shop != null ? gm.Shop.Gold : goldBefore;
        LogSuccess(action, $"{unitName} {starLevel}성 / 벤치 {targetSlot + 1} / 골드 {goldBefore} → {goldAfter}");

        // 공유 풀 반환이 RPC 왕복으로 비동기 처리될 수 있어, 코스트별 랜덤 획득과 완전히 같은
        // 지연 갱신 코루틴(DelayedPoolRefresh)을 그대로 재사용한다 — 새 코루틴을 만들지 않는다.
        // 이 코루틴이 끝나면 _poolPending도 false로 돌아가 판매 버튼이 다시 활성화된다.
        // 내 보유 유닛 목록은 DrawRuntimeUnitList가 매 프레임 GetBenchSnapshot/GetUnitsOnBoard를
        // 직접 그리므로 별도 캐시 없이 항상 최신 상태다.
        _poolComputed = false;
        _poolPending = true;
        StartCoroutine(DelayedPoolRefresh());
    }

    private void DrawUnitInspectModeToggle()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(_unitInspectMode == UnitInspectMode.Database, "DB 유닛 검색", "Button") &&
            _unitInspectMode != UnitInspectMode.Database)
            _unitInspectMode = UnitInspectMode.Database;

        if (GUILayout.Toggle(_unitInspectMode == UnitInspectMode.Runtime, "내 보유 유닛", "Button") &&
            _unitInspectMode != UnitInspectMode.Runtime)
            _unitInspectMode = UnitInspectMode.Runtime;
        GUILayout.EndHorizontal();
    }

    // ── DB 유닛 검색 ──

    private void DrawUnitSearch()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("검색(이름/ID 일부):", GUILayout.Width(140f));
        _unitQuery = GUILayout.TextField(_unitQuery, GUILayout.Width(200f));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("코스트:", GUILayout.Width(60f));
        DrawCostFilterToggle(0, "전체");
        for (int c = 1; c <= 5; c++)
            DrawCostFilterToggle(c, $"{c}코");
        GUILayout.EndHorizontal();

        if (_unitQuery != _unitSearchedQuery)
            RunUnitSearch();
    }

    private void DrawCostFilterToggle(int cost, string label)
    {
        bool selected = _unitCostFilter == cost;
        if (GUILayout.Toggle(selected, label, "Button", GUILayout.Width(50f)) && !selected)
        {
            _unitCostFilter = cost;
            RunUnitSearch();
        }
    }

    /// <summary>
    /// 검색/필터 조건이 바뀔 때만 실행되며(질의·코스트 필터 변경, 지급 후 지연 갱신), 풀 수량도
    /// 이때 한 번만 TryGetPoolDebugInfo로 조회해 결과와 함께 캐시한다 — OnGUI가 매 프레임 다시 그릴
    /// 때 반복 호출하지 않기 위함. 검색 결과 제한은 두지 않고 PokemonDatabase.all 전체를 대상으로 한다.
    /// </summary>
    private void RunUnitSearch()
    {
        _unitSearchedQuery = _unitQuery;
        _unitResults.Clear();

        PokemonDatabase db = PokemonDatabase.Instance;
        if (db == null || db.all == null) return;

        GameManager.TryGet(out var gm);
        ShopManager shop = gm != null ? gm.Shop : null;

        string query = (_unitQuery ?? "").Trim();

        foreach (PokemonData data in db.all)
        {
            if (data == null) continue;
            if (_unitCostFilter != 0 && data.cost != _unitCostFilter) continue;
            if (!MatchesQuery(query, data)) continue;

            var entry = new UnitSearchEntry { data = data };
            if (shop != null)
                entry.poolFound = shop.TryGetPoolDebugInfo(
                    data, out entry.poolRemaining, out entry.poolInitial, out _, out _, out _);

            _unitResults.Add(entry);
        }

        SortUnitResults();
    }

    /// <summary>
    /// 정렬 버튼 클릭 시에도 다시 호출되어 _unitResults 자체의 순서를 List.Sort로 실제 변경한다
    /// (UI 표시만 바뀌는 가짜 정렬 금지 요구사항 반영).
    /// </summary>
    private void SortUnitResults()
    {
        _unitResults.Sort((a, b) =>
        {
            int cmp = _unitSortKey switch
            {
                UnitSortKey.Name => string.Compare(a.data.pokemonName, b.data.pokemonName, System.StringComparison.Ordinal),
                UnitSortKey.Id => a.data.id.CompareTo(b.data.id),
                UnitSortKey.Cost => a.data.cost.CompareTo(b.data.cost),
                UnitSortKey.Pool => a.poolRemaining.CompareTo(b.poolRemaining),
                _ => 0
            };
            return _unitSortDescending ? -cmp : cmp;
        });
    }

    private static bool MatchesQuery(string query, PokemonData data)
    {
        if (string.IsNullOrEmpty(query)) return true;

        if (int.TryParse(query, out int id) && data.id == id) return true;

        return (!string.IsNullOrEmpty(data.pokemonName) && data.pokemonName.Contains(query)) ||
               (!string.IsNullOrEmpty(data.pokemonNameEn) &&
                data.pokemonNameEn.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private void DrawUnitResultList()
    {
        GUILayout.Label($"검색 결과 ({_unitResults.Count}개)");

        GUILayout.BeginHorizontal();
        GUILayout.Label("정렬:", GUILayout.Width(40f));
        DrawUnitSortButton(UnitSortKey.Name, "이름");
        DrawUnitSortButton(UnitSortKey.Id, "ID");
        DrawUnitSortButton(UnitSortKey.Cost, "코스트");
        DrawUnitSortButton(UnitSortKey.Pool, "풀잔여");
        GUILayout.EndHorizontal();

        GUILayout.Label("이름 | ID | 코스트 | 시너지 | 풀");

        _unitListScroll = GUILayout.BeginScrollView(_unitListScroll, GUI.skin.box, GUILayout.Height(300f));

        if (_unitResults.Count == 0)
        {
            GUILayout.Label("검색 결과 없음");
        }
        else
        {
            foreach (UnitSearchEntry entry in _unitResults)
            {
                string synergyText = FormatSynergyShort(entry.data.synergies);
                string poolLabel = !entry.poolFound
                    ? "대상 아님"
                    : (entry.poolRemaining <= 0
                        ? $"품절 0/{entry.poolInitial}"
                        : $"{entry.poolRemaining}/{entry.poolInitial}");

                string line = $"{entry.data.pokemonName} | ID {entry.data.id} | {entry.data.cost}코 | {synergyText} | {poolLabel}";
                if (GUILayout.Button(line))
                    SelectDatabaseUnit(entry.data);
            }
        }

        GUILayout.EndScrollView();
    }

    private void DrawUnitSortButton(UnitSortKey key, string label)
    {
        string arrow = _unitSortKey == key ? (_unitSortDescending ? " ▼" : " ▲") : "";
        if (GUILayout.Button(label + arrow, GUILayout.Width(90f)))
        {
            if (_unitSortKey == key) _unitSortDescending = !_unitSortDescending;
            else { _unitSortKey = key; _unitSortDescending = false; }
            SortUnitResults();
        }
    }

    /// <summary>목록용 축약 표시. 전체 시너지는 상세 창에서 FormatSynergyFull로 표시한다.</summary>
    private static string FormatSynergyShort(List<string> synergies)
    {
        if (synergies == null || synergies.Count == 0) return "-";
        string joined = string.Join(", ", synergies);
        const int maxLen = 24;
        return joined.Length > maxLen ? joined.Substring(0, maxLen) + "…" : joined;
    }

    private static string FormatSynergyFull(List<string> synergies)
        => (synergies != null && synergies.Count > 0) ? string.Join(", ", synergies) : "-";

    /// <summary>DB 결과 선택 → Database 모드로 전환하고 단일 상세 창을 열거나(이미 열려 있으면) 내용만 교체한다.</summary>
    private void SelectDatabaseUnit(PokemonData data)
    {
        _selectedUnitData = data;
        _selectedRuntimeUnit = null;
        _unitInspectMode = UnitInspectMode.Database;
        _selectedUnitPoolQueried = false;
        _detailWindowScroll = Vector2.zero;
        _detailWindowOpen = true;
    }

    // ── 실제 유닛(내 보유) 목록 ──

    /// <summary>
    /// 벤치는 슬롯 번호를 표시해야 하므로(요구사항: "벤치 1"/"벤치 2") null 슬롯도 포함하는
    /// GetBenchSnapshot()(인덱스=슬롯)을 쓴다. 필드는 GetUnitsOnBoard()(이미 null 제거된 목록)를 쓴다.
    /// 둘 다 BoardManager의 기존 공개 API 그대로다.
    /// </summary>
    private void DrawRuntimeUnitList()
    {
        if (!GameManager.TryGet(out var gm) || gm.Board == null)
        {
            GUILayout.Label("BoardManager 연결 안 됨");
            return;
        }

        GUILayout.Label("위치 | 이름 | 성급 | 코스트 | 시너지 | 진화 | 장비 수");

        _unitListScroll = GUILayout.BeginScrollView(_unitListScroll, GUI.skin.box, GUILayout.Height(300f));

        IReadOnlyList<PokemonUnit> bench = gm.Board.GetBenchSnapshot();
        int shown = 0;

        if (bench != null)
        {
            for (int i = 0; i < bench.Count; i++)
            {
                PokemonUnit unit = bench[i];
                if (unit == null || unit.data == null) continue;
                DrawRuntimeUnitRow(unit, $"벤치 {i + 1}");
                shown++;
            }
        }

        foreach (PokemonUnit unit in gm.Board.GetUnitsOnBoard())
        {
            if (unit == null || unit.data == null) continue;
            DrawRuntimeUnitRow(unit, "필드");
            shown++;
        }

        if (shown == 0)
            GUILayout.Label("보유 중인 유닛 없음");

        GUILayout.EndScrollView();
    }

    private void DrawRuntimeUnitRow(PokemonUnit unit, string positionLabel)
    {
        string evoState = ClassifyEvolution(unit);
        string synergyText = FormatSynergyShort(unit.data.synergies);
        int itemCount = unit.items != null ? unit.items.Count : 0;
        string line = $"{positionLabel} | {unit.data.pokemonName} | {unit.starLevel}성 | {unit.data.cost}코 | " +
                       $"{synergyText} | {evoState} | 장비 {itemCount}";
        if (GUILayout.Button(line))
            SelectRuntimeUnit(unit);
    }

    /// <summary>실제 유닛 선택 → Runtime 모드로 전환하고 단일 상세 창을 열거나(이미 열려 있으면) 내용만 교체한다.</summary>
    private void SelectRuntimeUnit(PokemonUnit unit)
    {
        _selectedRuntimeUnit = unit;
        _selectedUnitData = unit != null ? unit.data : null;
        _unitInspectMode = UnitInspectMode.Runtime;
        _detailWindowScroll = Vector2.zero;
        _detailWindowOpen = true;
    }

    // ── 상세 창 공통 진입점(기능 창에는 더 이상 임베드하지 않음, 단일 재사용 창 전용) ──

    /// <summary>DB 모드/실제 유닛 모드 공통 진입점. 단일 재사용 상세 창(DrawDetailWindow)이 호출한다.</summary>
    private void DrawUnitDetailContent()
    {
        if (_unitInspectMode == UnitInspectMode.Database)
            DrawDatabaseDetailContent();
        else
            DrawRuntimeDetailContent();
    }

    // ── DB 조회 모드 상세 ──

    private void DrawDatabaseDetailContent()
    {
        if (_selectedUnitData == null)
        {
            GUILayout.Label("선택된 유닛 없음");
            return;
        }

        PokemonData data = _selectedUnitData;

        GUILayout.Label("조회 모드: DB 정보");
        GUILayout.Label($"이름: {data.pokemonName}");
        GUILayout.Label($"영문명: {data.pokemonNameEn}");
        GUILayout.Label($"ID: {data.id}   코스트: {data.cost}");
        GUILayout.Label($"역할: {(string.IsNullOrEmpty(data.role) ? "-" : data.role)}");
        GUILayout.Label($"시너지: {FormatSynergyFull(data.synergies)}");

        if (data.skill != null && data.skill.HasSkill)
            GUILayout.Label($"스킬: {data.skill.skillName}({data.skill.skillId}) / {data.skill.effectType} / {data.skill.targetType}");
        else
            GUILayout.Label("스킬: 없음(평타만)");

        GUILayout.Label($"마나 비용: {data.manaCost}");

        GUILayout.Space(6f);
        DrawUnitPoolDetail(data);

        GUILayout.Space(6f);
        DrawEvolutionTechTree(data);

        GUILayout.Space(6f);
        DrawDatabaseStatTable(data);
    }

    private void DrawUnitPoolDetail(PokemonData data)
    {
        GUILayout.Label("── 공용 풀 ──");

        if (!GameManager.TryGet(out var gm) || gm.Shop == null)
        {
            GUILayout.Label("ShopManager 연결 안 됨");
            return;
        }

        if (!_selectedUnitPoolQueried)
        {
            _selectedUnitPoolFound = gm.Shop.TryGetPoolDebugInfo(
                data, out _selectedUnitRemaining, out _selectedUnitInitial, out _, out _, out _);
            _selectedUnitPoolQueried = true;
        }

        if (!_selectedUnitPoolFound)
        {
            GUILayout.Label("공용 풀 대상 아님");
            return;
        }

        int deducted = _selectedUnitInitial - _selectedUnitRemaining;
        GUILayout.Label($"초기 수량: {_selectedUnitInitial}");
        GUILayout.Label($"현재 잔여: {_selectedUnitRemaining}");
        GUILayout.Label($"현재 차감: {deducted} (상점 예약분 포함 가능 — '구매된 수량' 아님)");

        if (GUILayout.Button("풀 정보 새로고침", GUILayout.Width(140f)))
        {
            _selectedUnitPoolQueried = false;
            TryRefreshPoolSummary(); // 코스트별 요약(DrawCostPoolSummary)도 같이 최신화
        }
    }

    // ── 진화 테크트리 ──
    //
    // 조사 결과: PokemonData에는 요구된 "obtainBy"(shop/evolution/stone/trade/synergy/wild)
    // 필드가 존재하지 않는다(PokemonData.cs 전체 필드 확인 완료 — id/pokemonName/pokemonNameEn/
    // cost/hp/attack/defense/attackSpeed/range/attackRange/spellPower/manaCost/synergies/role/
    // skillId/skill/attackVfxId/evolvesIntoEn/shopBuyable/modelPrefab/icon 뿐, obtainBy 없음).
    // 따라서 obtainBy 기준 분류는 실제 코드에 존재하지 않는 개념이라 그대로 적용할 수 없다.
    // 대신 이미 공개된 데이터만으로 동등한 기능을 구성한다:
    //   - 일반 성급진화 체인: PokemonData.evolvesIntoEn(공개)을 정방향으로 따라가고,
    //     "evolvesIntoEn == 현재 종"인 다른 종을 PokemonDatabase.all에서 찾아 역방향으로
    //     루트(1성)까지 거슬러 올라간다(계산이 아니라 데이터 추적).
    //   - 진화의 돌: EvolutionStoneDatabase.all + EvolutionStoneData.GetEvolvedPokemon(정방향),
    //     mappings(공개 리스트)를 직접 순회해 역방향(원본 종 + 돌)도 조회한다.
    //   - 통신진화: TradeEvolutionData.GetEvolved(정방향)/GetBaseOf(역방향) — 둘 다 공개 메서드.
    //   - 시너지 진화: 이 종이 시너지 진화 결과인지 여부를 판별할 공개 데이터/필드 자체가 없어
    //     "공개 API 없음"으로 고정 표시한다(추측 금지).

    private static void DrawEvolutionTechTree(PokemonData data)
    {
        GUILayout.Label("── 진화 테크트리 ──");
        GUILayout.Label("(참고: PokemonData에 obtainBy 필드 없음 — evolvesIntoEn 역추적 + 각 진화 DB 정/역방향 조회로 구성)");

        DrawNormalEvolutionTechTree(data);
        GUILayout.Space(4f);
        DrawStoneEvolutionTechTree(data);
        GUILayout.Space(4f);
        DrawTradeEvolutionTechTree(data);
        GUILayout.Space(4f);
        DrawSynergyEvolutionNote();
    }

    /// <summary>
    /// evolvesIntoEn을 정방향으로 따라가 전체 체인을 구성하되, 먼저 역방향(evolvesIntoEn이
    /// 현재 종을 가리키는 다른 종을 찾는 식)으로 체인의 시작(1성) 종까지 거슬러 올라간 뒤
    /// 다시 정방향으로 내려오며 그린다. 이렇게 하면 진화 결과 종을 선택해도(예: 피죤투) 전체
    /// 체인에서 그 종의 정확한 위치(2성/3성)가 표시된다. 순환 데이터를 대비해 방문 집합으로 무한루프를 막는다.
    /// </summary>
    private static void DrawNormalEvolutionTechTree(PokemonData data)
    {
        GUILayout.Label("── 일반 성급진화 ──");

        PokemonDatabase db = PokemonDatabase.Instance;
        if (db == null || db.all == null)
        {
            GUILayout.Label("공개 데이터 없음");
            return;
        }

        PokemonData root = FindEvolutionRoot(data, db);

        var chain = new List<PokemonData> { root };
        var visited = new HashSet<PokemonData> { root };
        PokemonData cursor = root;
        while (!string.IsNullOrEmpty(cursor.evolvesIntoEn))
        {
            PokemonData next = db.GetByNameEn(cursor.evolvesIntoEn);
            if (next == null || !visited.Add(next)) break; // 다음 데이터 없음 또는 순환 방지
            chain.Add(next);
            cursor = next;
        }

        for (int i = 0; i < chain.Count; i++)
        {
            PokemonData node = chain[i];
            string marker = (node == data) ? "  ← 현재 선택" : "";
            GUILayout.Label($"{node.pokemonName} {i + 1}성{marker}");
            if (i < chain.Count - 1)
                GUILayout.Label("  │ 동일 유닛 3마리\n  ▼");
        }

        if (chain.Count == 1 && string.IsNullOrEmpty(root.evolvesIntoEn) && root == data)
            GUILayout.Label("(추가 일반 성급진화 없음)");
    }

    /// <summary>evolvesIntoEn 역방향 탐색으로 진화 체인의 시작(1성) 종을 찾는다. 순환 방지 포함.</summary>
    private static PokemonData FindEvolutionRoot(PokemonData data, PokemonDatabase db)
    {
        PokemonData cursor = data;
        var visited = new HashSet<PokemonData> { data };
        while (true)
        {
            PokemonData preceding = FindPrecedingByEvolvesInto(cursor, db);
            if (preceding == null || !visited.Add(preceding)) return cursor;
            cursor = preceding;
        }
    }

    private static PokemonData FindPrecedingByEvolvesInto(PokemonData data, PokemonDatabase db)
    {
        foreach (PokemonData p in db.all)
        {
            if (p == null || p == data) continue;
            if (!string.IsNullOrEmpty(p.evolvesIntoEn) &&
                string.Equals(p.evolvesIntoEn, data.pokemonNameEn, System.StringComparison.OrdinalIgnoreCase))
                return p;
        }
        return null;
    }

    /// <summary>
    /// 정방향(이 종 → 돌 → 결과)과 역방향(원본 종 + 돌 → 이 종)을 모두 조회한다. 정방향은
    /// EvolutionStoneData.GetEvolvedPokemon(공개 메서드)을, 역방향은 EvolutionMapping의
    /// targetPokemon/evolvedPokemon(공개 필드)을 직접 순회한다 — 역방향 전용 공개 메서드가
    /// 없어 리스트를 직접 읽지만 private 필드 접근이 아니다. 여러 경로가 있으면 전부 나열한다.
    /// </summary>
    private static void DrawStoneEvolutionTechTree(PokemonData data)
    {
        GUILayout.Label("── 진화의 돌 ──");

        EvolutionStoneDatabase stoneDb = EvolutionStoneDatabase.Instance;
        PokemonDatabase pokeDb = PokemonDatabase.Instance;
        if (stoneDb == null || stoneDb.all == null)
        {
            GUILayout.Label("공개 데이터 없음");
            return;
        }

        int shown = 0;

        // 정방향: 이 종에서 추가로 돌진화 가능한 경로
        foreach (EvolutionStoneData stone in stoneDb.all)
        {
            if (stone == null) continue;
            string evolvedEn = stone.GetEvolvedPokemon(data.pokemonNameEn);
            if (string.IsNullOrEmpty(evolvedEn)) continue;

            PokemonData evolved = pokeDb != null ? pokeDb.GetByNameEn(evolvedEn) : null;
            GUILayout.Label($"{data.pokemonName} 1~3성  ← 현재 선택");
            GUILayout.Label($"  │ {stone.stoneName}\n  ▼");
            GUILayout.Label($"{(evolved != null ? evolved.pokemonName : evolvedEn)} — 성급 유지");
            shown++;
        }

        // 역방향: 이 종을 결과로 만드는 원본 종 + 돌 (여러 경로 가능)
        foreach (EvolutionStoneData stone in stoneDb.all)
        {
            if (stone == null || stone.mappings == null) continue;
            foreach (EvolutionMapping mapping in stone.mappings)
            {
                if (mapping == null) continue;
                if (!string.Equals(mapping.evolvedPokemon, data.pokemonNameEn, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                PokemonData origin = pokeDb != null ? pokeDb.GetByNameEn(mapping.targetPokemon) : null;
                GUILayout.Label($"{(origin != null ? origin.pokemonName : mapping.targetPokemon)} 1~3성");
                GUILayout.Label($"  │ {stone.stoneName}\n  ▼");
                GUILayout.Label($"{data.pokemonName}  ← 현재 선택 — 성급 유지");
                shown++;
            }
        }

        if (shown == 0)
            GUILayout.Label("돌진화 경로 없음(추가 돌진화 불가 / 돌진화 결과물 아님)");
    }

    /// <summary>TradeEvolutionData.Instance(공개 static)의 공개 조회 메서드(GetEvolved/GetBaseOf)만 사용한다.</summary>
    private static void DrawTradeEvolutionTechTree(PokemonData data)
    {
        GUILayout.Label("── 통신진화 ──");

        TradeEvolutionData db = TradeEvolutionData.Instance;
        if (db == null)
        {
            GUILayout.Label("공개 데이터 없음");
            return;
        }

        PokemonDatabase pokeDb = PokemonDatabase.Instance;
        bool shown = false;

        string evolvedEn = db.GetEvolved(data.pokemonNameEn);
        if (!string.IsNullOrEmpty(evolvedEn))
        {
            PokemonData evolved = pokeDb != null ? pokeDb.GetByNameEn(evolvedEn) : null;
            GUILayout.Label("1~3성  ← 현재 선택");
            GUILayout.Label("  │ 통신교환\n  ▼");
            GUILayout.Label($"{(evolved != null ? evolved.pokemonName : evolvedEn)} — 성급 유지");
            shown = true;
        }

        string baseEn = db.GetBaseOf(data.pokemonNameEn);
        if (!string.IsNullOrEmpty(baseEn))
        {
            PokemonData baseData = pokeDb != null ? pokeDb.GetByNameEn(baseEn) : null;
            GUILayout.Label($"{(baseData != null ? baseData.pokemonName : baseEn)} 1~3성");
            GUILayout.Label("  │ 통신교환\n  ▼");
            GUILayout.Label($"{data.pokemonName}  ← 현재 선택 — 성급 유지");
            shown = true;
        }

        if (!shown)
            GUILayout.Label("불가능(통신진화 대상도 결과물도 아님)");
    }

    /// <summary>
    /// 조사 결과: 이 종이 "시너지 진화"의 결과물인지 여부를 나타내는 공개 필드/메서드가
    /// PokemonData/PokemonDatabase 어디에도 없다(플러시·마이농 등 폼 변환은 별도 런타임
    /// 로직으로 추정되나 이를 조회할 공개 API가 확인되지 않음). 원작 지식으로 추측하지 않고
    /// 공개 API 부재 사실만 고정 표시한다.
    /// </summary>
    private static void DrawSynergyEvolutionNote()
    {
        GUILayout.Label("── 시너지 진화 ──");
        GUILayout.Label("시너지 진화 결과 종 여부: 공개 API 없음");
        GUILayout.Label("상세 조건: 공개 API 없음");
    }

    private static readonly string[] STAT_TABLE_HEADERS =
    {
        "스탯", "DB기본", "일반1성", "일반2성", "일반3성", "특수1성", "특수2성", "특수3성"
    };

    /// <summary>
    /// 일반진화 1~3성만 PokemonUnit.StarMultiplierFor(공개 static)를 그대로 곱해서 계산한다 —
    /// 공식을 복사하지 않고 실제 메서드를 재사용. 방어력/공격속도는 PokemonUnit.Defense/AttackSpeed가
    /// 성급과 무관하게 원본을 그대로 반환하므로(코드 확인됨) 배율을 곱하지 않는다.
    /// 특수진화 1~3성(SPECIAL_STAR_MULTIPLIER)은 private이라 공개 API가 없어 '-'로 표시한다.
    /// 장비 관련 열은 DB 조회 대상에 장착 상태 자체가 없으므로 표에서 제거했다.
    /// </summary>
    private static void DrawDatabaseStatTable(PokemonData data)
    {
        GUILayout.Label("── 능력치 예상 ──");
        GUILayout.Label("특수진화 1~3성 = 공개 API 없음('-' 표시). 장비 관련 열은 DB 모드에 없음(장착 상태 자체가 없음).");

        float m1 = PokemonUnit.StarMultiplierFor(1);
        float m2 = PokemonUnit.StarMultiplierFor(2);
        float m3 = PokemonUnit.StarMultiplierFor(3);

        const float labelW = 72f, cellW = 66f;

        GUILayout.BeginHorizontal();
        foreach (string h in STAT_TABLE_HEADERS)
            GUILayout.Box(h, GUILayout.Width(h == "스탯" ? labelW : cellW));
        GUILayout.EndHorizontal();

        DrawStatRow("HP", data.hp, m1, m2, m3, true, labelW, cellW);
        DrawStatRow("공격력", data.attack, m1, m2, m3, true, labelW, cellW);
        DrawStatRow("방어력", data.defense, m1, m2, m3, false, labelW, cellW);
        DrawStatRow("공격속도", data.attackSpeed, m1, m2, m3, false, labelW, cellW);
        DrawStatRow("스킬위력", data.spellPower, m1, m2, m3, true, labelW, cellW);
    }

    private static void DrawStatRow(string label, float baseValue, float m1, float m2, float m3,
                                     bool scalesWithStar, float labelW, float cellW)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Box(label, GUILayout.Width(labelW));
        GUILayout.Box(baseValue.ToString("0.##"), GUILayout.Width(cellW));

        if (scalesWithStar)
        {
            GUILayout.Box((baseValue * m1).ToString("0.##"), GUILayout.Width(cellW));
            GUILayout.Box((baseValue * m2).ToString("0.##"), GUILayout.Width(cellW));
            GUILayout.Box((baseValue * m3).ToString("0.##"), GUILayout.Width(cellW));
        }
        else
        {
            // 방어력/공격속도는 성급과 무관(PokemonUnit.Defense/AttackSpeed가 원본을 그대로 반환).
            GUILayout.Box(baseValue.ToString("0.##"), GUILayout.Width(cellW));
            GUILayout.Box(baseValue.ToString("0.##"), GUILayout.Width(cellW));
            GUILayout.Box(baseValue.ToString("0.##"), GUILayout.Width(cellW));
        }

        GUILayout.Box("-", GUILayout.Width(cellW)); // 특수1성
        GUILayout.Box("-", GUILayout.Width(cellW)); // 특수2성
        GUILayout.Box("-", GUILayout.Width(cellW)); // 특수3성
        GUILayout.EndHorizontal();
    }

    // ── 실제 유닛 조회 모드 상세 ──

    /// <summary>
    /// ItemInventoryHud에는 실제 PokemonUnit을 선택/조회하는 public 메서드가 전혀 없어(전부
    /// private) 재사용할 수 없었다. 대신 PokemonUnit 자체의 공개 프로퍼티(MaxHp/Attack/Defense/
    /// AttackSpeed/SpellPower/Range, StarMultiplier, IsSpecialEvolved, EffectiveSkill 등)와
    /// TradeEvolutionData.GetBaseOf(공개)를 직접 조회한다. 장비 적용 후 최종 수치만 공개 API가 없어 제외.
    /// </summary>
    private void DrawRuntimeDetailContent()
    {
        if (_selectedRuntimeUnit == null) // Unity 오버로드 — 판매/합체 등으로 파괴된 참조도 null로 판정됨
        {
            GUILayout.Label("선택된 실제 유닛 없음");
            return;
        }

        PokemonUnit unit = _selectedRuntimeUnit;
        if (unit.data == null)
        {
            GUILayout.Label("선택된 실제 유닛 없음");
            return;
        }

        GameManager.TryGet(out var gm);

        GUILayout.Label("조회 모드: 실제 유닛");
        GUILayout.Label($"위치: {FindRuntimePositionLabel(gm != null ? gm.Board : null, unit)}");
        GUILayout.Label($"현재 종: {unit.data.pokemonName}");
        GUILayout.Label($"ID: {unit.data.id}   코스트: {unit.data.cost}");
        GUILayout.Label($"성급: {unit.starLevel}성");
        GUILayout.Label($"역할: {unit.Role}");
        GUILayout.Label($"시너지: {FormatSynergyFull(unit.data.synergies)}");

        PokemonSkillData skill = unit.EffectiveSkill;
        GUILayout.Label(skill != null && skill.HasSkill
            ? $"현재 스킬: {skill.skillName}({skill.skillId}) / {skill.effectType} / {skill.targetType}"
            : "현재 스킬: 없음(평타만)");
        GUILayout.Label($"현재 마나 비용: {unit.EffectiveManaCost}");

        GUILayout.Space(6f);
        DrawRuntimeEvolutionState(unit);

        GUILayout.Space(6f);
        DrawRuntimeHeroAugmentState(unit);

        GUILayout.Space(6f);
        DrawRuntimeEquipment(unit);

        GUILayout.Space(6f);
        DrawRuntimeStatTable(unit);
    }

    /// <summary>벤치 슬롯 번호는 unit.isOnBoard가 false일 때 GetBenchSnapshot()에서 매 프레임 다시
    /// 찾는다(선택 시점 캐시가 아님) — 자동 승격 등으로 위치가 바뀌어도 항상 정확하다.</summary>
    private static string FindRuntimePositionLabel(BoardManager board, PokemonUnit unit)
    {
        if (unit.isOnBoard) return "필드";
        if (board == null) return "벤치(위치 확인 불가)";

        IReadOnlyList<PokemonUnit> bench = board.GetBenchSnapshot();
        if (bench != null)
            for (int i = 0; i < bench.Count; i++)
                if (bench[i] == unit) return $"벤치 {i + 1}";

        return "벤치(위치 확인 불가)";
    }

    /// <summary>
    /// 미진화/일반진화/진화의돌/통신진화 분류 + 돌 이름 + 통신진화 여부 + 진화 전 원본 종 + 진화 잠금
    /// 여부를 전부 공개 필드/프로퍼티로만 구성한다. 진화 전 원본 종은 돌 진화면 preStoneData(공개),
    /// 통신진화면 TradeEvolutionData.GetBaseOf(공개)로 조회한다.
    /// </summary>
    private static void DrawRuntimeEvolutionState(PokemonUnit unit)
    {
        GUILayout.Label("── 진화 상태 ──");
        GUILayout.Label($"분류: {ClassifyEvolution(unit)}");
        GUILayout.Label($"진화의 돌: {(unit.equippedStone != null ? unit.equippedStone.stoneName : "없음")}");
        GUILayout.Label($"통신진화 여부: {(unit.isTradeEvolved ? "예" : "아니오")}");

        string originalSpecies = "-";
        if (unit.equippedStone != null && unit.preStoneData != null)
            originalSpecies = unit.preStoneData.pokemonName;
        else if (unit.isTradeEvolved)
        {
            TradeEvolutionData db = TradeEvolutionData.Instance;
            string baseEn = db != null ? db.GetBaseOf(unit.data.pokemonNameEn) : null;
            if (!string.IsNullOrEmpty(baseEn))
            {
                PokemonData baseData = PokemonDatabase.Instance != null ? PokemonDatabase.Instance.GetByNameEn(baseEn) : null;
                originalSpecies = baseData != null ? baseData.pokemonName : baseEn;
            }
            else
            {
                originalSpecies = "공개 데이터 없음";
            }
        }
        GUILayout.Label($"진화 전 원본 종: {originalSpecies}");
        GUILayout.Label($"진화 잠금 여부: {(unit.evolutionLocked ? "예" : "아니오")}");
    }

    private static string ClassifyEvolution(PokemonUnit unit)
    {
        if (unit.equippedStone != null) return "진화의 돌";
        if (unit.isTradeEvolved) return "통신진화";
        if (unit.starLevel > 1) return "일반진화";
        return "미진화";
    }

    /// <summary>
    /// 영웅증강 적용 여부와 heroStatMultiplier/roleOverride/HasGrantedSkill/주입 스킬을 전부
    /// PokemonUnit 공개 필드·프로퍼티에서만 읽는다(PokemonUnit.cs 클래스 주석: "이브이 영웅증강 =
    /// evolutionLocked + heroStatMultiplier, 파치리스 영웅증강 = roleOverride + grantedSkill").
    /// </summary>
    private static void DrawRuntimeHeroAugmentState(PokemonUnit unit)
    {
        GUILayout.Label("── 영웅증강 ──");

        bool applied = unit.evolutionLocked ||
                        !Mathf.Approximately(unit.heroStatMultiplier, 1f) ||
                        !string.IsNullOrEmpty(unit.roleOverride) ||
                        unit.HasGrantedSkill;
        GUILayout.Label($"적용 여부: {(applied ? "예" : "아니오")}");
        GUILayout.Label($"heroStatMultiplier: ×{unit.heroStatMultiplier:0.##}");
        GUILayout.Label($"roleOverride: {(string.IsNullOrEmpty(unit.roleOverride) ? "없음" : unit.roleOverride)}");
        GUILayout.Label($"HasGrantedSkill: {(unit.HasGrantedSkill ? "예" : "아니오")}");
        GUILayout.Label(unit.HasGrantedSkill
            ? $"주입된 스킬: {unit.grantedSkill.skillName}({unit.grantedSkill.skillId})"
            : "주입된 스킬: 없음");
    }

    /// <summary>장착 아이템 이름+설명(ItemData.description, 공개 필드)을 나열하고, 진화의 돌은 별도 표시한다.</summary>
    private static void DrawRuntimeEquipment(PokemonUnit unit)
    {
        GUILayout.Label("── 장비 ──");

        int itemCount = unit.items != null ? unit.items.Count : 0;
        GUILayout.Label($"장착 아이템 수: {itemCount}");

        if (itemCount == 0)
        {
            GUILayout.Label("장착 아이템 없음");
        }
        else
        {
            foreach (ItemData it in unit.items)
            {
                if (it == null) continue;
                GUILayout.Label($"• {it.itemName}");
                GUILayout.Label($"  설명: {(string.IsNullOrEmpty(it.description) ? "-" : it.description)}");
            }
        }

        GUILayout.Label(unit.equippedStone != null
            ? $"진화의 돌(별도): {unit.equippedStone.stoneName} — {(string.IsNullOrEmpty(unit.equippedStone.description) ? "-" : unit.equippedStone.description)}"
            : "진화의 돌(별도): 없음");
    }

    /// <summary>
    /// 조사 결과 반영: PokemonUnit.ApplyEffectiveStatMultiplier(private, 소스 주석으로 확인)의 실제
    /// 계산은 "DB기본 × heroStatMultiplier × StarMultiplier" 순서다. 곱셈은 교환법칙이 성립하므로
    /// "DB기본 × 진화배율(먼저) × 영웅증강배율(나중)"으로 순서를 바꿔 계산해도 최종값은 항상
    /// unit.MaxHp 등 공개 프로퍼티와 정확히 일치한다 — 이 사실을 이용해 "영웅증강 단계 결과" 칸에
    /// 별도 계산 없이 공개 프로퍼티(unit.MaxHp 등) 값을 그대로 재사용한다(계산식 복사가 아니라
    /// 이미 검증된 공개 값 재사용). 일반진화/특수진화 칸의 "단계 결과"만 DB기본 × 공개 배율
    /// (StarMultiplierFor 또는 unit.StarMultiplier) 단일 곱셈으로 구한다. 방어력/공격속도는
    /// PokemonUnit.Defense/AttackSpeed가 성급·영웅증강과 무관하게 원본을 그대로 반환하므로
    /// (코드 확인됨) 배율 칸을 전부 "해당없음"으로 둔다.
    /// </summary>
    private static void DrawRuntimeStatTable(PokemonUnit unit)
    {
        GUILayout.Label("── 능력치(배율/단계 결과, 장비 적용 전) ──");
        GUILayout.Label("시너지 증가 / 장비 증가 = 공개 API 없음. 최종값은 진화·영웅증강 반영, 장비·전투 시너지 미포함.");

        const float labelW = 68f, cellW = 72f;
        string[] headers = { "스탯", "DB기본", "일반진화", "특수진화", "시너지", "영웅증강", "장비", "최종적용" };

        GUILayout.BeginHorizontal();
        foreach (string h in headers)
            GUILayout.Box(h, GUILayout.Width(h == "스탯" ? labelW : cellW));
        GUILayout.EndHorizontal();

        DrawRuntimeStatRow("HP", unit.data.hp, unit, unit.MaxHp, true, labelW, cellW);
        DrawRuntimeStatRow("공격력", unit.data.attack, unit, unit.Attack, true, labelW, cellW);
        DrawRuntimeStatRow("방어력", unit.data.defense, unit, unit.Defense, false, labelW, cellW);
        DrawRuntimeStatRow("공격속도", unit.data.attackSpeed, unit, unit.AttackSpeed, false, labelW, cellW);
        DrawRuntimeStatRow("스킬위력", unit.data.spellPower, unit, unit.SpellPower, true, labelW, cellW);
    }

    /// <summary>
    /// scales=true(HP/공격력/스킬위력)일 때만 일반·특수진화 배율(StarMultiplierFor/StarMultiplier,
    /// 둘 다 공개)을 하드코딩 없이 그대로 곱해 "×배율\n단계결과" 두 줄로 표시한다. 1성 미진화
    /// 유닛도 ×1.0을 생략하지 않는다(StarMultiplierFor(1)이 그대로 1.0을 반환하므로 자연히 표시됨).
    /// 영웅증강 칸은 배율(unit.heroStatMultiplier)은 항상 표시하고, 단계 결과는 finalValue(=
    /// unit.MaxHp 등, 이미 진화+영웅증강까지 반영된 공개 프로퍼티)를 그대로 써서 항상 정확히
    /// 분리된다 — "분리 API 없음" 케이스가 발생하지 않는 유일한 이유는 finalValue 자체가 이미
    /// 그 단계의 정답이기 때문이다.
    /// </summary>
    private static void DrawRuntimeStatRow(string label, float baseValue, PokemonUnit unit, float finalValue,
                                            bool scales, float labelW, float cellW)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Box(label, GUILayout.Width(labelW));
        GUILayout.Box(baseValue.ToString("0.##"), GUILayout.Width(cellW));

        if (scales)
        {
            string normalCell = !unit.IsSpecialEvolved
                ? FormatRatioResult(PokemonUnit.StarMultiplierFor(unit.starLevel), baseValue * PokemonUnit.StarMultiplierFor(unit.starLevel))
                : "해당없음";
            string specialCell = unit.IsSpecialEvolved
                ? FormatRatioResult(unit.StarMultiplier, baseValue * unit.StarMultiplier)
                : "해당없음";
            string heroCell = FormatRatioResult(unit.heroStatMultiplier, finalValue); // finalValue = 진화+영웅증강 반영된 공개 프로퍼티 그대로

            GUILayout.Box(normalCell, GUILayout.Width(cellW));
            GUILayout.Box(specialCell, GUILayout.Width(cellW));
            GUILayout.Box("API\n없음", GUILayout.Width(cellW)); // 시너지
            GUILayout.Box(heroCell, GUILayout.Width(cellW));
        }
        else
        {
            // 방어력/공격속도는 성급·영웅증강과 무관(PokemonUnit.Defense/AttackSpeed가 원본을 그대로 반환).
            GUILayout.Box("해당없음", GUILayout.Width(cellW));
            GUILayout.Box("해당없음", GUILayout.Width(cellW));
            GUILayout.Box("API\n없음", GUILayout.Width(cellW)); // 시너지
            GUILayout.Box("해당없음", GUILayout.Width(cellW));
        }

        GUILayout.Box("API\n없음", GUILayout.Width(cellW)); // 장비
        GUILayout.Box(finalValue.ToString("0.##"), GUILayout.Width(cellW)); // 최종 적용값(장비 제외)
        GUILayout.EndHorizontal();
    }

    /// <summary>"×배율\n결과값" 두 줄 포맷. 셀 폭을 넓히지 않고도 두 값을 함께 표시하기 위함.</summary>
    private static string FormatRatioResult(float ratio, float result) => $"×{ratio:0.##}\n{result:0}";

    // ─────────────────────────────────────────
    // ③ 상세 창(단일 재사용)
    // ─────────────────────────────────────────

    /// <summary>
    /// DB 결과/실제 유닛 목록 어느 쪽을 클릭해도 항상 이 창 하나만 열고 내용만 교체한다
    /// (SelectDatabaseUnit/SelectRuntimeUnit이 공통으로 _detailWindowOpen=true만 세팅하므로
    /// 이미 열려 있으면 새 창을 만들지 않고 같은 창이 다음 프레임에 새 선택 상태를 그린다).
    /// </summary>
    private void DrawDetailWindow(int windowId)
    {
        GUILayout.BeginVertical();

        if (GUILayout.Button("닫기", GUILayout.Width(60f)))
            _detailWindowOpen = false;

        _detailWindowScroll = GUILayout.BeginScrollView(_detailWindowScroll, GUI.skin.box);
        DrawUnitDetailContent();
        GUILayout.EndScrollView();

        GUILayout.EndVertical();

        DrawResizeHandle(ref _detailWindowRect, MIN_DETAIL_WIDTH, MIN_DETAIL_HEIGHT);
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
    }

    // ─────────────────────────────────────────
    // 아이템 탭
    // ─────────────────────────────────────────

    private void DrawItemTab()
    {
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(280f));
        DrawMyInventory();
        GUILayout.EndVertical();

        GUILayout.BeginVertical();
        DrawItemSearch();
        DrawItemResultList();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.Label("장착 중 아이템(조회 전용 — 삭제/변경 불가):");
        DrawEquippedItems();
    }

    private void DrawMyInventory()
    {
        GUILayout.Label("내 인벤토리");

        if (!GameManager.TryGet(out var gm) || gm.Item == null)
        {
            GUILayout.Label("ItemManager 연결 안 됨");
            return;
        }

        ItemManager item = gm.Item;
        GUILayout.Label($"사용량: {item.InventoryCount}/{item.InventoryCount + item.AvailableInventorySpace}");

        _myInventoryScroll = GUILayout.BeginScrollView(_myInventoryScroll, GUI.skin.box, GUILayout.Height(260f));

        if (item.HasRemover)
            GUILayout.Label("[도구] 제거기");
        if (item.ReforgerCount > 0)
            GUILayout.Label($"[도구] 재조합기 x{item.ReforgerCount}");

        foreach (ItemData it in item.Items)
            if (it != null) GUILayout.Label($"[장비] {it.itemName}");

        foreach (EvolutionStoneData stone in item.Stones)
            if (stone != null) GUILayout.Label($"[돌] {stone.stoneName}");

        if (item.InventoryCount == 0)
            GUILayout.Label("인벤토리 비어 있음");

        GUILayout.EndScrollView();
    }

    private void DrawItemSearch()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("분류:", GUILayout.Width(50f));
        DrawItemCategoryToggle(ItemCategory.All, "전체");
        DrawItemCategoryToggle(ItemCategory.Equipment, "일반 장비");
        DrawItemCategoryToggle(ItemCategory.Stone, "진화의 돌");
        DrawItemCategoryToggle(ItemCategory.Tool, "도구");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("검색(이름/ID 일부):", GUILayout.Width(140f));
        _itemQuery = GUILayout.TextField(_itemQuery, GUILayout.Width(200f));
        GUILayout.EndHorizontal();

        if (_itemQuery != _itemSearchedQuery)
            RunItemSearch();
    }

    private void DrawItemCategoryToggle(ItemCategory category, string label)
    {
        bool selected = _itemCategory == category;
        if (GUILayout.Toggle(selected, label, "Button") && !selected)
        {
            _itemCategory = category;
            RunItemSearch();
        }
    }

    private void RunItemSearch()
    {
        _itemSearchedQuery = _itemQuery;
        _itemResults.Clear();

        string query = (_itemQuery ?? "").Trim();

        // 분류가 2종일 때는 "다른 한쪽이 아니면 포함" 방식(!=)도 성립했지만, Tool이 늘어난
        // 뒤로는 분류별로 명시적으로 포함 조건을 검사해야 한다.
        bool includeEquipment = _itemCategory == ItemCategory.All || _itemCategory == ItemCategory.Equipment;
        bool includeStone     = _itemCategory == ItemCategory.All || _itemCategory == ItemCategory.Stone;
        bool includeTool      = _itemCategory == ItemCategory.All || _itemCategory == ItemCategory.Tool;

        if (includeEquipment)
        {
            ItemDatabase db = ItemDatabase.Instance;
            if (db != null && db.all != null)
                foreach (ItemData data in db.all)
                {
                    if (data == null) continue;
                    if (!MatchesItemQuery(query, data.id, data.itemName, data.itemNameEn)) continue;
                    // 기존 아이템 툴팁(ItemTooltipUI)이 읽는 것과 같은 필드를 그대로 재사용한다.
                    _itemResults.Add(new ItemSearchEntry { data = data, description = data.description });
                }
        }

        if (includeStone)
        {
            EvolutionStoneDatabase db = EvolutionStoneDatabase.Instance;
            if (db != null && db.all != null)
                foreach (EvolutionStoneData data in db.all)
                {
                    if (data == null) continue;
                    if (!MatchesItemQuery(query, data.id, data.stoneName, data.stoneNameEn)) continue;
                    _itemResults.Add(new ItemSearchEntry { data = data, description = BuildStoneDescription(data) });
                }
        }

        if (includeTool)
        {
            if (_reforgerData != null && MatchesItemQuery(query, _reforgerData.id, _reforgerData.consumableName, _reforgerData.consumableNameEn))
                _itemResults.Add(new ItemSearchEntry { data = _reforgerData, description = _reforgerData.description });

            // 참조 누락 시 도구 분류가 조용히 비어 보이면 원인 파악이 어려우므로 한 번은 알려준다
            // (RunItemSearch는 매 프레임이 아니라 검색어/분류가 바뀔 때만 호출되지만, 그마저도 세션당 1회로 더 제한한다).
            if (!_toolDataMissingWarned && _reforgerData == null)
            {
                Debug.LogWarning("[QAManager] 재조합기 지급용 ConsumableData(_reforgerData)가 Inspector에 연결되지 않았습니다 — 도구 분류에 표시되지 않습니다.");
                _toolDataMissingWarned = true;
            }
        }
    }

    private static bool MatchesItemQuery(string query, int id, string nameKr, string nameEn)
    {
        if (string.IsNullOrEmpty(query)) return true;
        if (int.TryParse(query, out int qid) && id == qid) return true;

        return (!string.IsNullOrEmpty(nameKr) && nameKr.Contains(query)) ||
               (!string.IsNullOrEmpty(nameEn) && nameEn.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    /// <summary>돌 자체 설명(있으면) + "진화 가능: 유닛명…" 두 줄을 합친다. 돌 설명이 없으면 진화 대상 줄만 반환.</summary>
    private static string BuildStoneDescription(EvolutionStoneData stone)
    {
        string evolutionLine = BuildStoneEvolutionTargets(stone);

        if (string.IsNullOrWhiteSpace(stone.description))
            return evolutionLine;

        return $"{stone.description}\n{evolutionLine}";
    }

    /// <summary>
    /// EvolutionStoneData.mappings의 targetPokemon(진화 전 종, 영문명)을 PokemonDatabase.GetByNameEn으로
    /// 한글명(pokemonName)으로 바꿔 매핑 순서 그대로 나열한다. 중복 이름만 제거한다.
    /// PokemonDatabase에 없는 영문명은 대상을 알 수 없으므로 추측하지 않고 결과에서 제외한다.
    /// </summary>
    private static string BuildStoneEvolutionTargets(EvolutionStoneData stone)
    {
        if (stone.mappings == null || stone.mappings.Count == 0)
            return "진화 가능 대상 없음";

        PokemonDatabase db = PokemonDatabase.Instance;
        if (db == null) return "진화 가능 대상 없음";

        var names = new List<string>();

        foreach (EvolutionMapping mapping in stone.mappings)
        {
            if (mapping == null || string.IsNullOrEmpty(mapping.targetPokemon)) continue;

            PokemonData target = db.GetByNameEn(mapping.targetPokemon);
            if (target == null || string.IsNullOrEmpty(target.pokemonName)) continue;

            if (!names.Contains(target.pokemonName))
                names.Add(target.pokemonName);
        }

        return names.Count > 0 ? $"진화 가능: {string.Join(", ", names)}" : "진화 가능 대상 없음";
    }

    private void DrawItemResultList()
    {
        GUILayout.Label($"검색 결과 ({_itemResults.Count}개)");

        _itemListScroll = GUILayout.BeginScrollView(_itemListScroll, GUI.skin.box, GUILayout.Height(260f));

        if (_itemResults.Count == 0)
        {
            GUILayout.Label("검색 결과 없음");
        }
        else
        {
            foreach (ItemSearchEntry entry in _itemResults)
                DrawItemResultRow(entry);
        }

        GUILayout.EndScrollView();
    }

    private void DrawItemResultRow(ItemSearchEntry entry)
    {
        ScriptableObject obj = entry.data;
        string label = obj switch
        {
            ItemData it => $"[장비] {it.itemName} (ID {it.id})",
            EvolutionStoneData st => $"[돌] {st.stoneName} (ID {st.id})",
            ConsumableData tool => $"[도구] {tool.consumableName} (ID {tool.id})",
            _ => obj.name
        };

        GUILayout.BeginHorizontal(GUI.skin.box);

        GUILayout.BeginVertical();
        GUILayout.Label(label, GUILayout.ExpandWidth(true));
        if (!string.IsNullOrWhiteSpace(entry.description))
            GUILayout.Label(entry.description, WrapLabelStyle, GUILayout.ExpandWidth(true));
        GUILayout.EndVertical();

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("지급", GUILayout.Width(60f), GUILayout.Height(24f)))
            HandleGrantItem(obj);

        GUILayout.EndHorizontal();
    }

    /// <summary>보조 설명 줄바꿈용 라벨 스타일. GUI.skin은 OnGUI 중에만 유효해 최초 사용 시 1회만 생성한다.</summary>
    private GUIStyle WrapLabelStyle => _wrapLabelStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true };

    private void HandleGrantItem(ScriptableObject obj)
    {
        if (!GameManager.TryGet(out var gm) || gm.Item == null)
        {
            LogFailure("아이템 지급", "ItemManager 연결 안 됨");
            return;
        }

        ItemManager item = gm.Item;

        if (!item.HasInventorySpace)
        {
            string label = obj switch
            {
                ItemData it => it.itemName,
                EvolutionStoneData st => st.stoneName,
                ConsumableData tool => tool.consumableName,
                _ => obj.name
            };
            LogFailure($"아이템 지급 - {label}", "인벤토리가 가득 참");
            return;
        }

        int before = item.InventoryCount;

        switch (obj)
        {
            case ItemData it:
            {
                bool success = item.AddItem(it);
                LogGrantResult($"아이템 지급 - {it.itemName}", success, before, item.InventoryCount, item.InventoryCount + item.AvailableInventorySpace);
                break;
            }
            case EvolutionStoneData st:
            {
                bool success = item.AddStone(st);
                LogGrantResult($"아이템 지급 - {st.stoneName}", success, before, item.InventoryCount, item.InventoryCount + item.AvailableInventorySpace);
                break;
            }
            case ConsumableData tool when tool == _reforgerData:
            {
                bool success = item.AddReforger(1);
                LogGrantResult($"아이템 지급 - {tool.consumableName}", success, before, item.InventoryCount, item.InventoryCount + item.AvailableInventorySpace);
                break;
            }
        }
    }

    private void LogGrantResult(string action, bool success, int before, int after, int max)
    {
        if (success)
            LogSuccess(action, $"인벤토리 {before}/{max} → {after}/{max}");
        else
            LogFailure(action, "지급 실패(인벤토리 가득 참 또는 유효하지 않은 항목)");
    }

    /// <summary>보드+벤치 전 유닛을 순회해 장착 중인 장비/돌만 조회용으로 표시(삭제/변경 없음).</summary>
    private void DrawEquippedItems()
    {
        if (!GameManager.TryGet(out var gm) || gm.Board == null)
        {
            GUILayout.Label("BoardManager 연결 안 됨");
            return;
        }

        _equippedScroll = GUILayout.BeginScrollView(_equippedScroll, GUI.skin.box, GUILayout.Height(100f));

        int shown = 0;
        foreach (PokemonUnit unit in CombinedUnits(gm.Board))
        {
            if (unit == null || unit.data == null) continue;
            bool hasItems = unit.items != null && unit.items.Count > 0;
            bool hasStone = unit.equippedStone != null;
            if (!hasItems && !hasStone) continue;

            var parts = new List<string>();
            if (hasItems)
                foreach (ItemData it in unit.items)
                    if (it != null) parts.Add(it.itemName);
            if (hasStone)
                parts.Add($"[돌]{unit.equippedStone.stoneName}");

            GUILayout.Label($"{unit.data.pokemonName} ★{unit.starLevel} ({(unit.isOnBoard ? "필드" : "벤치")}): {string.Join(", ", parts)}");
            shown++;
        }

        if (shown == 0)
            GUILayout.Label("장착 중인 장비/돌 없음");

        GUILayout.EndScrollView();
    }

    private static IEnumerable<PokemonUnit> CombinedUnits(BoardManager board)
    {
        foreach (PokemonUnit unit in board.GetUnitsOnBoard()) yield return unit;
        foreach (PokemonUnit unit in board.GetUnitsInBench()) yield return unit;
    }

    // ─────────────────────────────────────────
    // 공용 풀 탭
    // ─────────────────────────────────────────

    private void DrawPoolTab()
    {
        if (!GameManager.TryGet(out var gm) || gm.Shop == null)
        {
            GUILayout.Label("ShopManager 연결 안 됨");
            return;
        }

        if (!_poolComputed && GUILayout.Button("공용 풀 조회(느릴 수 있음)"))
            TryRefreshPoolSummary();

        if (GUILayout.Button("새로고침", GUILayout.Width(100f)))
            TryRefreshPoolSummary();

        if (_poolPending)
        {
            GUILayout.Label("계산 중... (랜덤 획득 요청의 공유 풀 반영 대기)");
            return;
        }

        if (!_poolComputed)
        {
            GUILayout.Label("아직 조회하지 않음 — 버튼을 눌러 조회하세요.");
            return;
        }

        GUILayout.Label("── 코스트별 합계 ──");
        for (int cost = 1; cost <= 5; cost++)
            GUILayout.Label($"{cost}코: 초기 {_poolInitialByCost[cost]} / 잔여 {_poolRemainingByCost[cost]}");

        GUILayout.Space(6f);
        GUILayout.Label($"── 유닛별 ({_poolEntries.Count}종) ──");

        _poolScroll = GUILayout.BeginScrollView(_poolScroll, GUI.skin.box, GUILayout.Height(200f));
        foreach (PoolEntry entry in _poolEntries)
        {
            string status = entry.remaining <= 0 ? "품절" : "";
            GUILayout.Label($"{entry.data.pokemonName} | ID {entry.data.id} | {entry.data.cost}코 | " +
                             $"초기 {entry.initial} / 잔여 {entry.remaining} / 차감 {entry.initial - entry.remaining} {status}");
        }
        GUILayout.EndScrollView();
    }

    /// <summary>
    /// 코스트별 합계 전용 API가 없어(ShopManager 조사 결과) PokemonDatabase.all을 순회하며
    /// shopBuyable 종마다 ShopManager.TryGetPoolDebugInfo를 호출해 QAManager 내부에서 직접 합산한다.
    /// 진화체(shopBuyable=false)는 순회에서 애초에 제외되므로 같은 풀이 중복 합산되지 않는다.
    /// 매 프레임 호출하면 비용이 커서(TryGetPoolDebugInfo 내부가 O(n) 순회) 버튼/트리거 시점에만 갱신한다.
    /// </summary>
    private void RecomputePool(ShopManager shop)
    {
        _poolEntries.Clear();
        for (int i = 0; i < _poolInitialByCost.Length; i++)
        {
            _poolInitialByCost[i] = 0;
            _poolRemainingByCost[i] = 0;
        }

        PokemonDatabase db = PokemonDatabase.Instance;
        if (db == null || db.all == null)
        {
            _poolComputed = true;
            return;
        }

        foreach (PokemonData data in db.all)
        {
            if (data == null || !data.shopBuyable) continue;
            if (!shop.TryGetPoolDebugInfo(data, out int remaining, out int initial, out _, out _, out _)) continue;

            _poolEntries.Add(new PoolEntry { data = data, initial = initial, remaining = remaining });

            int cost = Mathf.Clamp(data.cost, 1, 5);
            _poolInitialByCost[cost] += initial;
            _poolRemainingByCost[cost] += remaining;
        }

        _poolComputed = true;
    }

    // ─────────────────────────────────────────
    // 네트워크 탭
    // ─────────────────────────────────────────

    private void DrawNetworkTab()
    {
        if (!GameManager.TryGet(out var gm) || gm.Network == null)
        {
            GUILayout.Label("NetworkManager 연결 안 됨");
            return;
        }

        NetworkManager net = gm.Network;

        GUILayout.Label($"역할: {(net.IsMasterClient ? "Master" : "Guest")}");
        GUILayout.Label($"IsConnected: {net.IsConnected}");
        GUILayout.Label($"IsInRoom: {net.IsInRoom}");
        GUILayout.Label($"PlayerCount: {net.PlayerCount}");
        GUILayout.Label($"내 닉네임: {net.LocalNickname}");
        GUILayout.Label($"파트너 닉네임: {(string.IsNullOrEmpty(net.PartnerNickname) ? "확인 불가" : net.PartnerNickname)}");
        GUILayout.Label($"파트너 연결 여부: {(net.PlayerCount >= 2 ? "연결됨" : "미접속")}");
        GUILayout.Label($"파트너 골드(동기화): {(net.PlayerCount < 2 ? "확인 불가" : (_partnerGold.HasValue ? _partnerGold.Value.ToString() : "동기화 대기"))}");

        GUILayout.Space(6f);
        DrawActorNumbers();

        GUILayout.Space(6f);
        GUILayout.Label("네트워크 조작(강제 연결/해제, RPC 실행, 파트너 데이터 변경 등)은 지원하지 않습니다.");
    }

    private static void DrawActorNumbers()
    {
#if PHOTON_UNITY_NETWORKING
        string myActor = PhotonNetwork.LocalPlayer != null
            ? PhotonNetwork.LocalPlayer.ActorNumber.ToString()
            : "확인 불가";

        string partnerActor = "확인 불가";
        var others = PhotonNetwork.PlayerListOthers;
        if (others != null && others.Length > 0 && others[0] != null)
            partnerActor = others[0].ActorNumber.ToString();

        GUILayout.Label($"내 ActorNumber: {myActor}");
        GUILayout.Label($"파트너 ActorNumber: {partnerActor}");
#else
        GUILayout.Label("내 ActorNumber: 확인 불가(PHOTON_UNITY_NETWORKING 미정의 빌드)");
        GUILayout.Label("파트너 ActorNumber: 확인 불가(PHOTON_UNITY_NETWORKING 미정의 빌드)");
#endif
    }

    // ─────────────────────────────────────────
    // ActionLog — 기능 창 하단에 모든 탭 공통으로 표시된다.
    // ─────────────────────────────────────────

    private void DrawActionLog()
    {
        GUILayout.Label($"최근 실행 로그 (최대 {MAX_LOG_COUNT}개)");

        _logScroll = GUILayout.BeginScrollView(_logScroll, GUI.skin.box, GUILayout.Height(100f));

        if (_logs.Count == 0)
            GUILayout.Label("아직 실행 기록 없음");
        else
            foreach (string line in _logs)
                GUILayout.Label(line);

        GUILayout.EndScrollView();
    }
}
#endif
