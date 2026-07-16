using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기물 상점 + 아이템 상점 + 골드 경제 + 레벨/XP 진행 담당.
/// 라운드마다 골드 수입을 지급하고 유닛 상점/아이템 상점을 자동 갱신한다.
/// 유닛 상점은 골드로 구매/리롤, 아이템 상점은 아이템 쿠폰으로 구매한다.
/// 레벨/XP는 ShopManager 내부에서 관리하고,
/// 레벨/XP/배치 가능 기물 수 변경은 GameEvents로 통지한다.
/// 매니저 간 직접 참조 금지 — 상태 변화는 GameEvents로 통지.
/// </summary>
public class ShopManager : MonoBehaviour
{
    [Header("샵 풀")]
    [Tooltip("켜짐(권장): 중앙 PokemonDatabase의 shopBuyable 종으로 풀을 자동 구성. 데이터 구동이라 인스펙터 수동 할당 불필요.\n" +
             "꺼짐: 아래 _pool 인스펙터 배열만 사용(특정 종만 나오게 하는 디버그/제한 테스트용).")]
    [SerializeField] private bool _useDatabasePool = true;

    [Tooltip("_useDatabasePool이 꺼졌을 때만 사용하는 수동 풀. 켜져 있으면 무시되고 DB로 덮어씀.")]
    [SerializeField] private List<PokemonData> _pool = new();

    [Header("유닛 상점 설정")]
    [SerializeField] private int _shopSize = 5;
    [SerializeField] private int _rerollCost = 2;

    [Header("아이템 상점 설정")]
    [SerializeField] private int _itemShopSize = 4;
    [SerializeField] private int _itemPrice = 1;

    [Header("골드 설정 (밸런스 기획서 §7: 라운드 골드는 보상 선지급이 담당)")]
    [Tooltip("⚠️ SerializeField — 인스펙터 값 우선. 선지급 모델에선 0(RW001이 1라운드 골드 제공, §7.5 누적검증도 시작골드 0 기준).")]
    [SerializeField] private int _startingGold = 0;

    [Header("이자 (밸런스 기획서 §7.5)")]
    [Tooltip("보유 골드 10당 지급 이자(원). 기본 1, 경제 증강 시 2. 2라운드부터 라운드 시작 시 지급.")]
    [SerializeField] private int _interestPerTenGold = 1;
    [Tooltip("이자 계산에 인정되는 보유 골드 상한(초과분은 이자 미발생). 기본 50 → 최대 이자 base 5 / 경제 10.")]
    [SerializeField] private int _interestGoldCap = 50;

    [Header("챔피언 풀 설정 (유닛당 카피 수, 밸런스 기획서 §2.3 확정: 30/25/18/10/9)")]
    [Tooltip("⚠️ SerializeField라 씬/프리팹의 인스펙터 값이 우선. 확정값(30/25/18/10/9)으로 인스펙터도 맞춰야 함.")]
    [SerializeField] private int _cost1PoolCount = 30;
    [SerializeField] private int _cost2PoolCount = 25;
    [SerializeField] private int _cost3PoolCount = 18;
    [SerializeField] private int _cost4PoolCount = 10;
    [SerializeField] private int _cost5PoolCount = 9;

    [Header("레벨 확률")]
    [Tooltip("초기 플레이어 레벨. 레벨 변경 시 GameEvents.OnLevelChanged를 통해 ShopManager 내부 레벨이 갱신된다.")]
    [SerializeField] private int _currentLevel = 1;

    [Header("레벨 / XP 설정")]
    [Tooltip("최대 플레이어 레벨. 임시값이며 밸런스 확정 후 조정 가능.")]
    [SerializeField] private int _maxLevel = 10;

    [Tooltip("라운드 종료 시 기본 지급 XP. 임시값이며 밸런스 확정 후 조정 가능.")]
    [SerializeField] private int _roundXpReward = 2;

    [Tooltip("XP 구매 1회에 필요한 골드. 임시값이며 밸런스 확정 후 조정 가능.")]
    [SerializeField] private int _buyXpCostGold = 4;

    [Tooltip("XP 구매 1회에 지급되는 XP. 임시값이며 밸런스 확정 후 조정 가능.")]
    [SerializeField] private int _buyXpAmount = 4;

    [Tooltip("index 0은 사용하지 않음. level 1 필요 XP = _requiredXpByLevel[1]")]
    [SerializeField]
    private int[] _requiredXpByLevel =
    {
        0,  // dummy
        2,  // Lv1 -> Lv2
        2,  // Lv2 -> Lv3
        6,  // Lv3 -> Lv4
        10, // Lv4 -> Lv5
        20, // Lv5 -> Lv6
        36, // Lv6 -> Lv7
        48, // Lv7 -> Lv8
        76, // Lv8 -> Lv9
        80, // Lv9 -> Lv10
        0   // Lv10 max
    };

    [Tooltip("index 0은 사용하지 않음. level 1 unit cap = _unitCapByLevel[1]")]
    [SerializeField]
    private int[] _unitCapByLevel =
    {
        0,  // dummy
        1,  // Lv1
        2,  // Lv2
        3,  // Lv3
        4,  // Lv4
        5,  // Lv5
        6,  // Lv6
        7,  // Lv7
        8,  // Lv8
        9,  // Lv9
        10  // Lv10
    };
    public int Gold { get; private set; }

    /// <summary>무료 리롤 자원 잔여 횟수. 보상(RewardKind.Reroll)으로 누적, 리롤 시 골드보다 우선 소모.</summary>
    public int RerollCount { get; private set; }

    /// <summary>골드 리롤 1회 비용(무료 리롤 소진 후 폴백에 사용). UI 표시용 노출.</summary>
    public int RerollCost => _rerollCost;

    /// <summary>아이템 상점 무료 리롤 자원 잔여 횟수. 보상(RewardKind.ItemShopReroll)으로 누적.</summary>
    public int ItemShopRerollCount { get; private set; }

    public int CurrentLevel => _currentLevel;
    public int CurrentXp { get; private set; }
    public int RequiredXp => GetRequiredXp(_currentLevel);
    public int UnitCap => GetUnitCap(_currentLevel);
    public int BuyXpCostGold => _buyXpCostGold;
    public int BuyXpAmount => _buyXpAmount;
    /// <summary>
    /// 이 플레이어가 4코 상점 오픈 증강을 보유하고 있는지.
    /// MasterClient가 플레이어별 상점 확률을 계산할 때 사용한다.
    /// </summary>
    public bool IsCost4ForceOpen => _cost4ForceOpen;

    public int ShopSize => _shopSize;
    public int ItemShopSize => _itemShopSize;
    public int ItemPrice => _itemPrice;

    /// <summary>현재 유닛 상점에 공개된 후보(읽기 전용). null = 구매됨/빈 슬롯.</summary>
    public IReadOnlyList<PokemonData> CurrentSlots => _slots;

    /// <summary>현재 아이템 상점에 공개된 후보(읽기 전용). null = 구매됨/빈 슬롯.</summary>
    public IReadOnlyList<ScriptableObject> CurrentItemSlots => _itemSlots;

    private PokemonData[] _slots;
    private ScriptableObject[] _itemSlots;

    /// <summary>
    /// 공용 상점 리롤 요청 후 새 상점 정보를 기다리는 중인지.
    /// 응답을 받기 전에 리롤 버튼을 연속으로 누르는 것을 막는다.
    /// </summary>
    private bool _sharedRerollPending;

    /// <summary>
    /// 공용 상점 구매 승인 결과를 기다리는 중인지.
    /// 같은 슬롯을 연속 구매하는 것을 막는다.
    /// </summary>
    private bool _sharedPurchasePending;

    /// <summary>구매 승인을 기다리고 있는 상점 슬롯.</summary>
    private int _pendingPurchaseSlot = -1;

    /// <summary>구매 승인을 기다리고 있는 포켓몬 ID.</summary>
    private int _pendingPurchasePokemonId = -1;

    /// <summary>현재 씬의 NetworkManager를 안전하게 가져온다.</summary>
    private NetworkManager Network =>
        GameManager.Instance != null
            ? GameManager.Instance.Network
            : null;

    /// <summary>현재 2인 공용 상점 풀을 사용 중인지.</summary>
    private bool UsesSharedShopPool =>
        Network != null &&
        Network.UsesSharedShopPool;

    /// <summary>
    /// 포켓몬별 남은 풀 수량.
    /// 솔로에서는 구매 시 감소하고,
    /// 2인 공용 풀에서는 상점에 등장하는 순간 예약 차감된다.
    /// 판매 또는 미구매 리롤 시 다시 풀로 돌아온다.
    /// </summary>
    private readonly Dictionary<PokemonData, int> _remainingPool = new();

    /// <summary>진화체 → 기본종(풀 관리 대상) 역매핑. 진화 유닛 판매 시 소비된 기본종 카피를 올바른 풀로 되돌리기 위함.</summary>
    private readonly Dictionary<PokemonData, PokemonData> _evolvedToBase = new();

    private void Awake()
    {
        _slots = new PokemonData[_shopSize];
        _itemSlots = new ScriptableObject[_itemShopSize];

        Gold = _startingGold;
        CurrentXp = 0;

        // 인스펙터에서 잘못된 값이 들어와도 안전하게 보정
        // index 0은 dummy이므로 실제 지원 가능한 최대 레벨은 배열 길이 - 1.
        int xpMaxLevel = _requiredXpByLevel != null && _requiredXpByLevel.Length > 1
            ? _requiredXpByLevel.Length - 1
            : 1;

        int capMaxLevel = _unitCapByLevel != null && _unitCapByLevel.Length > 1
            ? _unitCapByLevel.Length - 1
            : 1;

        int tableMaxLevel = Mathf.Max(1, Mathf.Min(xpMaxLevel, capMaxLevel));

        _maxLevel = Mathf.Clamp(_maxLevel, 1, tableMaxLevel);
        _currentLevel = Mathf.Clamp(_currentLevel, 1, _maxLevel);
    }

    private void OnEnable()
    {
        GameEvents.OnRoundChanged += HandleRoundChanged;
        GameEvents.OnUnitSold += HandleUnitSold;
        GameEvents.OnLevelChanged += HandleLevelChanged;
        GameEvents.OnTeamRoundResolved += HandleTeamRoundResolved;
    }

    private void OnDisable()
    {
        GameEvents.OnRoundChanged -= HandleRoundChanged;
        GameEvents.OnUnitSold -= HandleUnitSold;
        GameEvents.OnLevelChanged -= HandleLevelChanged;
        GameEvents.OnTeamRoundResolved -= HandleTeamRoundResolved;
    }

    private void Start()
    {
        // DB 모드면 인스펙터 배열 무시하고 중앙 PokemonDatabase로 풀을 덮어씀(데이터 구동).
        // 수동 모드(_useDatabasePool=false)인데 풀도 비었으면 ShopDebugTest가 런타임 테스트 풀 시드.
        if (_useDatabasePool)
            SeedPoolFromDatabase();

        InitializeChampionPool();

        // 초기 UI/상점 상태 동기화.
        // LevelChanged는 Roll() 전에 발행해야 첫 상점부터 현재 레벨 확률을 사용한다.
        GameEvents.GoldChanged(Gold);
        GameEvents.LevelChanged(_currentLevel); // HandleLevelChanged에서 UnitCapChanged까지 발행
        GameEvents.XpChanged(CurrentXp, RequiredXp);
        GameEvents.RerollCountChanged(RerollCount); // 무료 리롤 자원 초기 동기화(HUD)
        GameEvents.ItemShopRerollCountChanged(ItemShopRerollCount); // 아이템샵 무료 리롤 자원 초기 동기화

        // MasterClient가 각 플레이어의 레벨별 상점 확률을 계산할 수 있도록
        // 현재 레벨을 Photon Player CustomProperties에 기록한다.
        Network?.SyncLocalShopLevel(_currentLevel);
        // 플레이어별 4코 상점 오픈 상태를 Photon Player 속성에 기록한다.
        Network?.SyncLocalCostFourForceOpen(_cost4ForceOpen);

        if (UsesSharedShopPool)
        {
            // 2인 공용 풀에서는 각 클라이언트가 따로 Roll하지 않는다.
            // 라운드 시작 시 현재 MasterClient가 양쪽 상점을 순서대로 생성한다.
            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = null;

            GameEvents.ShopRerolled();
        }
        else
        {
            // 솔로와 기존 오프라인 테스트는 기존 로컬 상점 방식을 유지한다.
            Roll();
        }

        // 아이템 상점은 공용 풀이 아니므로 기존처럼 각자 생성한다.
        RollItemShop();
    }

    /// <summary>
    /// 샵 풀을 중앙 PokemonDatabase의 shopBuyable 종으로 채운다(인스펙터 배열 덮어씀).
    /// 진화체/통신·돌 전용 종은 shopBuyable=false라 풀에서 제외됨.
    /// DB가 없거나 비면 기존 _pool 유지(빈 풀이면 ShopDebugTest 폴백).
    /// </summary>
    private void SeedPoolFromDatabase()
    {
        var db = PokemonDatabase.Instance;
        if (db == null || db.all == null || db.all.Count == 0)
        {
            Debug.LogWarning("[Shop] PokemonDatabase 비어있음 — 풀 시드 실패(Import Pokemon JSON 확인)");
            return;
        }

        _pool = db.all.FindAll(p => p != null && p.shopBuyable);
        Debug.Log($"[Shop] PokemonDatabase에서 풀 {_pool.Count}종 시드 (shopBuyable)");
    }

    /// <summary>포켓몬별 초기 풀 수량 설정.</summary>
    private void InitializeChampionPool()
    {
        _remainingPool.Clear();

        foreach (var data in _pool)
        {
            if (data == null) continue;

            int count = GetInitialPoolCount(data.cost);
            if (count <= 0) continue;

            _remainingPool[data] = count;
        }

        BuildEvolutionToBaseMap();

        Debug.Log($"[ShopPool] 챔피언 풀 초기화 완료: {_remainingPool.Count}종");
    }

    /// <summary>
    /// 풀에 있는 각 기본종의 진화 사슬(evolvesIntoEn)을 따라가 진화체 → 기본종 매핑을 만든다.
    /// 3합체 시 유닛의 data가 진화체로 스왑되므로, 판매 시 이 맵으로 기본종 풀에 카피를 되돌린다.
    /// (이게 없으면 진화 유닛 판매가 풀에 아무것도 반환하지 않아 상점 풀이 영구 고갈됨.)
    /// </summary>
    private void BuildEvolutionToBaseMap()
    {
        _evolvedToBase.Clear();

        var db = PokemonDatabase.Instance;
        if (db == null) return;

        foreach (var baseData in _remainingPool.Keys)
        {
            var current = baseData;
            // 최종형까지 사슬을 따라가며 각 진화체를 기본종으로 매핑. 사이클 방지용으로 방문 집합 사용.
            var visited = new HashSet<PokemonData> { current };
            while (current != null && !string.IsNullOrEmpty(current.evolvesIntoEn))
            {
                var next = db.GetByNameEn(current.evolvesIntoEn);
                if (next == null || !visited.Add(next)) break;

                if (!_evolvedToBase.ContainsKey(next))
                    _evolvedToBase[next] = baseData;

                current = next;
            }
        }
    }

    private int GetInitialPoolCount(int cost)
    {
        return cost switch
        {
            1 => _cost1PoolCount,
            2 => _cost2PoolCount,
            3 => _cost3PoolCount,
            4 => _cost4PoolCount,
            5 => _cost5PoolCount,
            _ => 0
        };
    }

    private void HandleRoundChanged(int round)
    {
        // 라운드별 고정 골드는 보상 테이블 선지급(RewardManager, OnStageEntered)이 담당.
        // 여기선 보유 골드 기반 이자만 지급(밸런스 기획서 §7.5). 1라운드는 이자 없음(4판 = 2~5라운드).
        if (round >= 2)
        {
            int interest = CalculateInterest();
            if (interest > 0)
            {
                AddGold(interest);
                Debug.Log($"[Shop] 이자 +{interest}G (10G당 {_interestPerTenGold}, 상한 {_interestGoldCap}G)");
            }
        }

        if (UsesSharedShopPool)
        {
            // 양쪽 클라이언트 모두 RoundChanged를 받지만,
            // 현재 MasterClient 한 명만 공용 풀을 수정하고 상점을 생성한다.
            //
            // 생성 순서:
            // 현재 MasterClient 상점 1~5 → 파트너 상점 1~5
            if (Network.IsMasterClient)
                Network.RefreshAllSharedShops();
        }
        else
        {
            // 솔로와 오프라인 테스트는 기존 로컬 상점을 사용한다.
            Roll();
        }

        // 아이템 상점은 플레이어별 독립 상점이므로 기존처럼 각자 갱신한다.
        RollItemShop();
    }

    /// <summary>
    /// 보유 골드 기반 이자 = floor(min(보유, 상한)/10) × 10골드당이자. (롤체식 이자 + 밸런스 기획서 §7.5)
    /// 예) 보유 50↑, 이자율 1 → 5원(롤체 기본 캡). 이자율 2(이자 증강) → 10원.
    /// </summary>
    private int CalculateInterest()
    {
        int eligible = Mathf.Min(Gold, _interestGoldCap);
        return (eligible / 10) * _interestPerTenGold;
    }

    /// <summary>현재 10골드당 이자율(원). base 1, '이자 +1' 증강 스택마다 +1. UI/디버그 표시용.</summary>
    public int InterestPerTenGold => _interestPerTenGold;

    /// <summary>
    /// 10골드당 이자율을 delta만큼 가산. '이자 +1' 증강 seam(덮어쓰기 아님 — 여러 이자 증강이 있으면 스택).
    /// 경제 증강 예: AddInterestPerTenGold(1) → 10골드당 2원(50골드에서 최대 10원) + 별도 AddGold(50) 즉시지급.
    /// </summary>
    public void AddInterestPerTenGold(int delta)
    {
        _interestPerTenGold = Mathf.Max(0, _interestPerTenGold + delta);
        Debug.Log($"[Shop] 이자율 {(delta >= 0 ? "+" : "")}{delta} => 10G당 {_interestPerTenGold}원");
    }

    // ──────────────────────────────────────────
    // 레벨 / XP
    // ──────────────────────────────────────────

    /// <summary>
    /// XP 구매 골드 비용 할인(레벨 할인 증강 seam). 음수 delta로 원복.
    /// 최소 1G 보장(무료 XP 구매로 무한 레벨업 방지). ⚠️ 증강 시스템(영욱 대행) 추가 — 태욱님 확인 필요.
    /// </summary>
    public void AddBuyXpCostDiscount(int discount)
    {
        _buyXpCostGold = Mathf.Max(1, _buyXpCostGold - discount);
        Debug.Log($"[Shop] XP 구매 비용 할인 {(discount >= 0 ? "-" : "+")}{Mathf.Abs(discount)}G => {_buyXpCostGold}G");
    }

    /// <summary>
    /// 팀 라운드 결과 확정 후 기본 XP 지급.
    /// 현재는 승패와 관계없이 라운드 종료 XP를 지급한다.
    /// TODO: 기획 확정 시 outcome에 따라 XP 지급 여부/배율을 다르게 처리할 수 있음.
    /// </summary>
    private void HandleTeamRoundResolved(TeamRoundOutcome outcome)
    {
        AddXp(_roundXpReward);
    }

    /// <summary>
    /// XP를 증가시키고, 필요 XP를 넘으면 자동 레벨업을 처리한다.
    /// 라운드 종료 XP와 XP 구매가 모두 이 함수를 통해 누적된다.
    /// </summary>
    public void AddXp(int amount)
    {
        if (amount <= 0) return;

        if (_currentLevel >= _maxLevel)
        {
            CurrentXp = 0;
            GameEvents.XpChanged(CurrentXp, RequiredXp);
            return;
        }

        CurrentXp += amount;
        Debug.Log($"[LevelXP] XP +{amount} => {CurrentXp}/{RequiredXp}");

        TryLevelUp();

        GameEvents.XpChanged(CurrentXp, RequiredXp);
    }

    /// <summary>
    /// 골드를 사용해 XP를 구매한다.
    /// 성공 시 골드를 차감하고 _buyXpAmount만큼 XP를 지급한다.
    /// </summary>
    public bool BuyXp()
    {
        if (_currentLevel >= _maxLevel)
        {
            Debug.Log("[LevelXP] 최대 레벨 — XP 구매 불가");
            return false;
        }

        if (Gold < _buyXpCostGold)
        {
            Debug.Log($"[LevelXP] 골드 부족 — XP 구매 실패 (필요 {_buyXpCostGold}, 보유 {Gold})");
            return false;
        }

        AddGold(-_buyXpCostGold);
        AddXp(_buyXpAmount);

        Debug.Log($"[LevelXP] XP 구매 완료 (-{_buyXpCostGold}G, +{_buyXpAmount}XP)");
        return true;
    }

    /// <summary>
    /// 현재 XP가 필요 XP 이상이면 레벨업한다.
    /// 여러 레벨을 한 번에 넘길 수 있으므로 while로 처리한다.
    /// </summary>
    private void TryLevelUp()
    {
        while (_currentLevel < _maxLevel && RequiredXp > 0 && CurrentXp >= RequiredXp)
        {
            CurrentXp -= RequiredXp;
            _currentLevel++;

            Debug.Log($"[LevelXP] 레벨업! Lv.{_currentLevel}");

            // ShopManager 자신도 이 이벤트를 구독하고 있으므로,
            // 레벨 변경 이후 상점 확률은 다음 Roll/Reroll부터 새 레벨 기준으로 적용된다.
            GameEvents.LevelChanged(_currentLevel);
        }

        if (_currentLevel >= _maxLevel)
            CurrentXp = 0;
    }

    /// <summary>
    /// 현재 레벨에서 다음 레벨까지 필요한 XP를 반환한다.
    /// 배열 범위를 벗어나면 0을 반환하여 레벨업을 막는다.
    /// </summary>
    private int GetRequiredXp(int level)
    {
        if (_requiredXpByLevel == null || level <= 0 || level >= _requiredXpByLevel.Length)
            return 0;

        return _requiredXpByLevel[level];
    }

    /// <summary>
    /// 현재 레벨 기준 배치 가능 기물 수를 반환한다.
    /// 배열이 없거나 범위를 벗어나면 임시로 레벨 값을 그대로 사용한다.
    /// </summary>
    private int GetUnitCap(int level)
    {
        if (_unitCapByLevel == null || level <= 0 || level >= _unitCapByLevel.Length)
            return Mathf.Max(1, level);

        return Mathf.Max(1, _unitCapByLevel[level]);
    }

    private void HandleLevelChanged(int level)
    {
        _currentLevel = Mathf.Clamp(level, 1, _maxLevel);

        Debug.Log($"[Shop] 플레이어 레벨 변경 반영: Lv.{_currentLevel}");

        // UnitCap의 단일 소스는 ShopManager.
        // 레벨 변경이 반영될 때마다 현재 레벨 기준 배치 가능 기물 수를 통지한다.
        GameEvents.UnitCapChanged(UnitCap);

        // MasterClient가 다음 상점 생성 때 이 플레이어의 최신 레벨 확률을
        // 사용할 수 있도록 Photon Player 속성에 동기화한다.
        Network?.SyncLocalShopLevel(_currentLevel);
    }

    /// <summary>
    /// BoardManager가 판매 이벤트를 발행하면
    /// 골드를 환급하고 판매한 유닛의 카피를 풀에 돌려놓는다.
    /// </summary>
    private void HandleUnitSold(PokemonUnit unit)
    {
        if (unit == null || unit.data == null)
            return;

        if (UsesSharedShopPool)
        {
            // 진화한 유닛은 현재 진화체 ID가 아니라
            // 합성에 사용된 기본종 풀로 되돌려야 한다.
            PokemonData poolData = GetPoolBaseData(unit.data);
            int amount = GetBaseUnitCount(unit.starLevel);

            if (poolData != null)
            {
                // 공용 풀의 실제 수정은 현재 MasterClient가 담당한다.
                Network.ReturnSharedPoolCopies(poolData.id, amount);
            }
        }
        else
        {
            // 솔로와 오프라인 테스트는 기존 로컬 풀 반환 방식을 사용한다.
            ReturnToChampionPool(unit);
        }

        int refund = SellValue(unit);

        if (refund > 0)
            AddGold(refund);
    }

    /// <summary>
    /// 판매 환급액 = 투자한 골드(코스트 × 합성에 들어간 1성 마리 수). 1성=×1, 2성=×3, 3성=×9.
    /// (= 산 만큼 그대로 돌려줌. TFT는 2코스트+ 고성에 -1 패널티가 있으나 여기선 단순 전액 환급 기본값.)
    /// </summary>
    public int SellValue(PokemonUnit unit)
    {
        if (unit == null || unit.data == null) return 0;
        int baseUnits = GetBaseUnitCount(unit.starLevel);
        return unit.data.cost * baseUnits;
    }

    // ──────────────────────────────────────────
    // 유닛 상점
    // ──────────────────────────────────────────

    /// <summary>샵 풀에서 레벨별 코스트 확률과 남은 풀 수량을 기준으로 _shopSize개를 다시 공개.</summary>
    public void Roll()
    {
        if (_pool == null || _pool.Count == 0)
        {
            for (int i = 0; i < _slots.Length; i++) _slots[i] = null;
            GameEvents.ShopRerolled();
            return;
        }

        for (int i = 0; i < _slots.Length; i++)
            _slots[i] = RollOnePokemon();

        GameEvents.ShopRerolled();
    }

    /// <summary>4코 상점 오픈 증강 활성 여부. 켜지면 레벨 확률표에 4코 등장 확률이 강제 주입된다.</summary>
    private bool _cost4ForceOpen;

    /// <summary>4코 강제 오픈 시 주입되는 4코 등장 확률(%) — 기획 미확정 PLACEHOLDER.</summary>
    private const int COST4_FORCE_OPEN_RATE = 15;

    /// <summary>
    /// 4코 상점 강제 오픈(증강 seam): 즉시 상점을 4코스트 5마리로 무료 갱신하고("5마리 즉시"),
    /// 이후 레벨과 무관하게 4코가 확률표에 등장한다. ⚠️ 증강 시스템(영욱 대행) 추가 — 태욱님 확인 필요.
    /// </summary>
    /// <summary>
    /// 4코 상점 강제 오픈 증강.
    ///
    /// 선택 즉시:
    /// - 솔로: 로컬 상점 5칸을 4코로 갱신
    /// - 2인: MasterClient에게 요청하여 공용 풀에서 4코 5장을 예약 차감
    ///
    /// 이후:
    /// - 이 플레이어의 일반 상점에 4코 등장 가중치가 계속 적용된다.
    /// </summary>
    public void ForceOpenCostFour()
    {
        _cost4ForceOpen = true;

        // MasterClient가 플레이어별 증강 적용 여부를 알 수 있도록 동기화한다.
        Network?.SyncLocalCostFourForceOpen(true);

        if (UsesSharedShopPool)
        {
            // 기존 상점 반환과 4코 5장 예약 차감은
            // 공용 풀 권위자인 MasterClient가 처리한다.
            Network.RequestForceOpenCostFourShop();

            Debug.Log(
                "[Shop] 4코 상점 강제 오픈 요청 — " +
                "MasterClient 공용 풀에서 4코 5슬롯 생성");

            return;
        }

        // 솔로와 오프라인 테스트는 기존 로컬 방식으로 처리한다.
        for (int i = 0; i < _slots.Length; i++)
            _slots[i] = RollOnePokemonOfCost(4);

        GameEvents.ShopRerolled();

        Debug.Log(
            $"[Shop] 4코 상점 강제 오픈 — " +
            $"즉시 4코 {_slots.Length}슬롯 갱신, " +
            $"이후 등장 가중치 {COST4_FORCE_OPEN_RATE}");
    }

    /// <summary>특정 코스트만 굴림(4코 강제 오픈용). 해당 코스트 풀이 비면 일반 굴림으로 폴백.</summary>
    private PokemonData RollOnePokemonOfCost(int cost)
    {
        var candidates = _pool.FindAll(p =>
            p != null &&
            p.cost == cost &&
            _remainingPool.TryGetValue(p, out int remain) &&
            remain > 0);

        if (candidates.Count > 0)
            return candidates[Random.Range(0, candidates.Count)];

        Debug.LogWarning($"[Shop] {cost}코스트 남은 풀 없음 — 일반 굴림으로 폴백");
        return RollOnePokemon();
    }

    private PokemonData RollOnePokemon()
    {
        // 선택된 코스트에 남은 포켓몬이 없을 수 있으니 여러 번 재시도
        for (int attempt = 0; attempt < 10; attempt++)
        {
            int cost = RollCostByLevel(_currentLevel);
            var candidates = _pool.FindAll(p =>
                p != null &&
                p.cost == cost &&
                _remainingPool.TryGetValue(p, out int remain) &&
                remain > 0);

            if (candidates.Count > 0)
                return candidates[Random.Range(0, candidates.Count)];
        }

        // 재시도 실패 시 전체 남은 풀에서 아무거나 선택
        var fallback = _pool.FindAll(p =>
            p != null &&
            _remainingPool.TryGetValue(p, out int remain) &&
            remain > 0);

        if (fallback.Count == 0)
            return null;

        return fallback[Random.Range(0, fallback.Count)];
    }

    /// <summary>
    /// 기존 호출 호환용.
    /// 로컬 플레이어의 4코 오픈 상태를 적용한다.
    /// </summary>
    public int[] CreateReservedSharedShop(int level)
    {
        return CreateReservedSharedShop(
            level,
            _cost4ForceOpen);
    }

    /// <summary>
    /// 2인 공용 풀에서 지정 플레이어의 상점 슬롯을 생성한다.
    ///
    /// cost4ForceOpen은 상점을 받는 플레이어의 증강 보유 상태다.
    /// MasterClient 자신의 상태가 아니라 대상 플레이어별 상태를 전달받아야 한다.
    /// </summary>
    public int[] CreateReservedSharedShop(
        int level,
        bool cost4ForceOpen)
    {
        var pokemonIds = new int[_shopSize];

        for (int slot = 0; slot < pokemonIds.Length; slot++)
        {
            PokemonData data =
                RollOnePokemonByRemainingCopies(
                    level,
                    cost4ForceOpen);

            if (data == null)
            {
                pokemonIds[slot] = -1;
                continue;
            }

            // 구매할 때가 아니라 상점에 등장하는 순간 예약 차감한다.
            DecreaseChampionPool(data, 1);
            pokemonIds[slot] = data.id;
        }

        return pokemonIds;
    }

    /// <summary>
    /// 특정 코스트만 사용해 공용 상점 5칸을 생성한다.
    ///
    /// 4코 상점 오픈의 즉시 효과에 사용하며,
    /// 같은 4코 안에서도 남은 카피 수에 비례해 추첨한다.
    /// </summary>
    public int[] CreateReservedSharedShopOfCost(int cost)
    {
        var pokemonIds = new int[_shopSize];

        for (int slot = 0; slot < pokemonIds.Length; slot++)
        {
            PokemonData data = PickByRemainingCopies(cost);

            if (data == null)
            {
                pokemonIds[slot] = -1;
                continue;
            }

            DecreaseChampionPool(data, 1);
            pokemonIds[slot] = data.id;
        }

        return pokemonIds;
    }

    /// <summary>
    /// 지정 플레이어의 레벨 확률과 4코 증강 상태를 적용한 뒤,
    /// 선택된 코스트 안에서 남은 카피 수에 비례해 한 종을 뽑는다.
    /// </summary>
    private PokemonData RollOnePokemonByRemainingCopies(
        int level,
        bool cost4ForceOpen)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            int cost = RollCostByLevel(
                level,
                cost4ForceOpen);

            PokemonData selected =
                PickByRemainingCopies(cost);

            if (selected != null)
                return selected;
        }

        // 선택된 코스트가 모두 고갈된 경우
        // 현재 남아 있는 전체 공용 풀에서 가중 추첨한다.
        return PickByRemainingCopies(0);
    }

    /// <summary>
    /// cost가 1~5면 해당 코스트 안에서 뽑고,
    /// cost가 0이면 모든 코스트의 남은 카피를 대상으로 뽑는다.
    /// </summary>
    private PokemonData PickByRemainingCopies(int cost)
    {
        int totalCopies = 0;

        // 먼저 뽑을 수 있는 전체 카피 수를 계산한다.
        foreach (PokemonData data in _pool)
        {
            if (data == null)
                continue;

            if (cost > 0 && data.cost != cost)
                continue;

            if (_remainingPool.TryGetValue(data, out int remain) &&
                remain > 0)
            {
                totalCopies += remain;
            }
        }

        if (totalCopies <= 0)
            return null;

        // 남은 전체 카피 수 범위에서 숫자 하나를 뽑는다.
        int roll = Random.Range(0, totalCopies);
        int cumulative = 0;

        // 각 포켓몬의 남은 장수만큼 구간을 배정한다.
        foreach (PokemonData data in _pool)
        {
            if (data == null)
                continue;

            if (cost > 0 && data.cost != cost)
                continue;

            if (!_remainingPool.TryGetValue(data, out int remain) ||
                remain <= 0)
            {
                continue;
            }

            cumulative += remain;

            if (roll < cumulative)
                return data;
        }

        return null;
    }

    /// <summary>
    /// 리롤 또는 라운드 갱신으로 사라진 미구매 상점 유닛을
    /// 공용 풀에 다시 돌려놓는다.
    /// -1은 이미 구매했거나 비어 있는 슬롯이므로 무시한다.
    /// </summary>
    public void ReturnReservedSharedShop(int[] pokemonIds)
    {
        if (pokemonIds == null)
            return;

        for (int i = 0; i < pokemonIds.Length; i++)
        {
            int pokemonId = pokemonIds[i];

            if (pokemonId < 0)
                continue;

            AddCopiesToSharedPool(pokemonId, 1);
        }
    }

    /// <summary>
    /// MasterClient가 보내준 포켓몬 ID 배열을
    /// 이 플레이어의 실제 상점 슬롯에 적용한다.
    /// </summary>
    public void ApplySharedShop(int[] pokemonIds)
    {
        var db = PokemonDatabase.Instance;

        if (_slots == null || _slots.Length != _shopSize)
            _slots = new PokemonData[_shopSize];

        for (int slot = 0; slot < _slots.Length; slot++)
        {
            int pokemonId =
                pokemonIds != null && slot < pokemonIds.Length
                    ? pokemonIds[slot]
                    : -1;

            _slots[slot] =
                pokemonId >= 0 && db != null
                    ? db.GetById(pokemonId)
                    : null;
        }

        // 새 상점을 받았으므로 이전 요청 대기 상태를 해제한다.
        _sharedRerollPending = false;
        _sharedPurchasePending = false;
        _pendingPurchaseSlot = -1;
        _pendingPurchasePokemonId = -1;

        GameEvents.ShopRerolled();
    }

    /// <summary>
    /// 판매 또는 구매 실패로 반환되는 카피를 공용 풀 복사본에 더한다.
    /// 실제 호출 권위는 NetworkManager의 현재 MasterClient가 가진다.
    /// </summary>
    public void AddCopiesToSharedPool(int pokemonId, int amount)
    {
        if (amount <= 0)
            return;

        var db = PokemonDatabase.Instance;
        PokemonData data = db != null ? db.GetById(pokemonId) : null;

        if (data == null || !_remainingPool.ContainsKey(data))
        {
            Debug.LogWarning(
                $"[SharedShop] 풀에 없는 포켓몬 반환 요청 — ID {pokemonId}");

            return;
        }

        int maxCount = GetInitialPoolCount(data.cost);

        _remainingPool[data] = Mathf.Min(
            maxCount,
            _remainingPool[data] + amount);

        Debug.Log(
            $"[SharedShop] {data.pokemonName} 풀 반환 +{amount} / " +
            $"남은 수량: {_remainingPool[data]}");
    }

    /// <summary>
    /// 현재 공용 풀의 포켓몬 ID와 남은 수량을 배열로 만든다.
    ///
    /// 저장 형식:
    /// 포켓몬ID, 남은 수량, 포켓몬ID, 남은 수량...
    /// </summary>
    public int[] ExportSharedPoolSnapshot()
    {
        var pokemonList = new List<PokemonData>(_remainingPool.Keys);

        // 클라이언트마다 Dictionary 순서가 달라도
        // 항상 동일한 결과가 나오도록 포켓몬 ID 순으로 정렬한다.
        pokemonList.Sort((a, b) => a.id.CompareTo(b.id));

        var snapshot = new int[pokemonList.Count * 2];
        int index = 0;

        foreach (PokemonData data in pokemonList)
        {
            snapshot[index++] = data.id;
            snapshot[index++] = _remainingPool[data];
        }

        return snapshot;
    }

    /// <summary>
    /// Room CustomProperties에 저장된 공용 풀 상태를 로컬 복사본에 적용한다.
    /// 새 MasterClient가 되면 이 복사본이 새로운 관리 원본이 된다.
    /// </summary>
    public void ImportSharedPoolSnapshot(int[] snapshot)
    {
        if (snapshot == null || snapshot.Length == 0)
            return;

        // ID와 수량이 한 쌍이므로 배열 길이는 반드시 짝수여야 한다.
        if (snapshot.Length % 2 != 0)
        {
            Debug.LogWarning(
                "[SharedShop] 저장된 공용 풀 정보가 올바르지 않습니다.");

            return;
        }

        var db = PokemonDatabase.Instance;

        if (db == null)
            return;

        // 저장된 값에 없는 포켓몬이 이전 수량으로 남지 않도록
        // 현재 풀 수량을 먼저 0으로 초기화한다.
        var poolPokemon = new List<PokemonData>(_remainingPool.Keys);

        foreach (PokemonData data in poolPokemon)
            _remainingPool[data] = 0;

        for (int index = 0; index < snapshot.Length; index += 2)
        {
            int pokemonId = snapshot[index];
            int remain = snapshot[index + 1];

            PokemonData data = db.GetById(pokemonId);

            if (data == null || !_remainingPool.ContainsKey(data))
            {
                Debug.LogWarning(
                    $"[SharedShop] 공용 풀 복원 대상 없음 — ID {pokemonId}");

                continue;
            }

            int maxCount = GetInitialPoolCount(data.cost);

            _remainingPool[data] = Mathf.Clamp(
                remain,
                0,
                maxCount);
        }

        Debug.Log("[SharedShop] 공용 풀 상태 동기화 완료");
    }

    /// <summary>
    /// 로컬 플레이어의 증강 상태를 적용하는 기존 상점용 오버로드.
    /// </summary>
    private int RollCostByLevel(int level)
    {
        return RollCostByLevel(
            level,
            _cost4ForceOpen);
    }

    /// <summary>
    /// 지정 플레이어의 4코 오픈 상태를 적용해 코스트를 추첨한다.
    /// </summary>
    private int RollCostByLevel(
        int level,
        bool cost4ForceOpen)
    {
        int[] rates =
            GetEffectiveCostRates(
                level,
                cost4ForceOpen);

        int total = 0;

        for (int i = 0; i < rates.Length; i++)
            total += Mathf.Max(0, rates[i]);

        if (total <= 0)
            return 1;

        int roll = Random.Range(0, total);
        int cumulative = 0;

        for (int i = 0; i < rates.Length; i++)
        {
            cumulative += Mathf.Max(0, rates[i]);

            if (roll < cumulative)
                return i + 1;
        }

        return 1;
    }

    /// <summary>
    /// 기본 레벨 확률표에 플레이어별 4코 증강 효과를 적용한다.
    ///
    /// 현재 15는 정확한 15%가 아니라 가중치다.
    /// 예를 들어 Lv1은 1코 100 + 4코 15이므로
    /// 실제 표시 확률은 약 13.04%가 된다.
    /// </summary>
    private int[] GetEffectiveCostRates(
        int level,
        bool cost4ForceOpen)
    {
        int[] rates = GetCostRates(level);

        if (cost4ForceOpen)
        {
            // 원래 4코 확률이 15보다 낮으면 최소 15 가중치를 보장한다.
            rates[3] = Mathf.Max(
                rates[3],
                COST4_FORCE_OPEN_RATE);
        }

        return rates;
    }

    /// <summary>
    /// 레벨별 코스트 등장 확률. Lv5~10은 밸런스 기획서 §2.4 확정값.
    /// Lv1~4는 문서 미명시라 초반 진입 곡선용 임시값 유지.
    /// </summary>
    private int[] GetCostRates(int level)
    {
        return level switch
        {
            1 => new[] { 100, 0, 0, 0, 0 },
            2 => new[] { 80, 20, 0, 0, 0 },
            3 => new[] { 65, 30, 5, 0, 0 },
            4 => new[] { 50, 35, 15, 0, 0 },
            // ── 밸런스 기획서 §2.4 확정 ──
            5 => new[] { 45, 33, 20, 2, 0 },
            6 => new[] { 30, 40, 25, 5, 0 },
            7 => new[] { 19, 30, 40, 10, 1 },
            8 => new[] { 15, 20, 32, 30, 3 },
            9 => new[] { 10, 17, 25, 33, 15 },
            _ => new[] { 5, 10, 20, 40, 25 } // Lv10 이상
        };
    }

    // ──────────────────────────────────────────
    // 공용 풀 / 상점 확률 디버그 UI 조회 API
    // ──────────────────────────────────────────

    /// <summary>
    /// 현재 플레이어 레벨의 1~5코스트 등장 확률을 반환한다.
    /// 반환 배열 index 0 = 1코스트, index 4 = 5코스트.
    /// 프로토타입 HUD의 확률 표시용이다.
    /// </summary>
    public int[] GetCurrentCostRatesForDebug()
    {
        return GetEffectiveCostRates(
            _currentLevel,
            _cost4ForceOpen);
    }

    /// <summary>
    /// 프로토타입 HUD에서 포켓몬별 공용 풀 수량과
    /// 다음 상점 슬롯 등장 확률을 표시하기 위한 정보를 반환한다.
    ///
    /// 등장 확률:
    /// 현재 레벨의 해당 코스트 확률
    /// × 해당 포켓몬 남은 수량
    /// ÷ 같은 코스트 전체 포켓몬 남은 수량
    /// </summary>
    public bool TryGetPoolDebugInfo(
        PokemonData data,
        out int remaining,
        out int initial,
        out int sameCostRemaining,
        out float costRatePercent,
        out float appearancePercent)
    {
        remaining = 0;
        initial = 0;
        sameCostRemaining = 0;
        costRatePercent = 0f;
        appearancePercent = 0f;

        if (data == null)
            return false;

        // 진화체가 들어오더라도 실제 풀 관리 대상인 기본종으로 변환한다.
        PokemonData poolData = GetPoolBaseData(data);

        if (poolData == null ||
            !_remainingPool.TryGetValue(poolData, out remaining))
        {
            return false;
        }

        initial = GetInitialPoolCount(poolData.cost);

        // 같은 코스트에 속한 모든 포켓몬의 현재 남은 카피 수를 합산한다.
        foreach (var pair in _remainingPool)
        {
            PokemonData candidate = pair.Key;

            if (candidate == null || candidate.cost != poolData.cost)
                continue;

            sameCostRemaining += Mathf.Max(0, pair.Value);
        }

        int[] rates = GetEffectiveCostRates(_currentLevel, _cost4ForceOpen);

        int totalRateWeight = 0;

        for (int i = 0; i < rates.Length; i++)
            totalRateWeight += Mathf.Max(0, rates[i]);

        int rateIndex = poolData.cost - 1;

        if (rateIndex < 0 ||
            rateIndex >= rates.Length ||
            totalRateWeight <= 0)
        {
            return true;
        }

        // 현재 확률표의 합이 100이 아니더라도 정상적으로 백분율로 환산한다.
        costRatePercent =
            100f * Mathf.Max(0, rates[rateIndex]) / totalRateWeight;

        if (sameCostRemaining > 0)
        {
            appearancePercent =
                costRatePercent *
                remaining /
                sameCostRemaining;
        }

        return true;
    }

    /// <summary>
    /// 유닛 상점을 새로 굴린다.
    /// 무료 리롤을 우선 사용하고 없으면 골드를 사용한다.
    /// </summary>
    public bool Reroll()
    {
        if (UsesSharedShopPool &&
            (_sharedRerollPending || _sharedPurchasePending))
        {
            Debug.Log("[SharedShop] 이전 상점 요청 처리 중 — 리롤 불가");
            return false;
        }

        if (RerollCount > 0)
        {
            RerollCount--;
            GameEvents.RerollCountChanged(RerollCount);
        }
        else if (Gold >= _rerollCost)
        {
            AddGold(-_rerollCost);
        }
        else
        {
            Debug.Log("[Shop] 리롤 불가 — 무료 리롤/골드 부족");
            return false;
        }

        if (UsesSharedShopPool)
        {
            // 현재 상점 반환과 새 상점 생성은
            // 공용 풀 권위자인 MasterClient에게 요청한다.
            _sharedRerollPending = true;
            Network.RequestSharedShopReroll(_currentLevel);
        }
        else
        {
            // 솔로와 오프라인은 기존 로컬 리롤을 사용한다.
            Roll();
        }

        // 리롤 환급 증강 등이 이 이벤트를 받아 처리한다.
        GameEvents.RerollSpent();
        return true;
    }

    /// <summary>무료 리롤 자원 지급(보상/증강 환급). 결과는 GameEvents.RerollCountChanged로 통지.</summary>
    public void AddReroll(int amount)
    {
        if (amount <= 0) return;
        RerollCount += amount;
        GameEvents.RerollCountChanged(RerollCount);
        Debug.Log($"[Shop] 무료 리롤 +{amount} => {RerollCount}");
    }

    /// <summary>
    /// 슬롯의 포켓몬을 구매해 벤치에 배치한다.
    /// 2인 공용 풀에서는 MasterClient의 예약 슬롯 승인을 먼저 받는다.
    /// </summary>
    public bool Buy(int slot)
    {
        if (_slots == null || slot < 0 || slot >= _slots.Length)
            return false;

        PokemonData data = _slots[slot];

        if (data == null)
            return false;

        if (Gold < data.cost)
        {
            Debug.Log(
                $"[Shop] 골드 부족 — {data.pokemonName} 구매 실패 " +
                $"(필요 {data.cost}, 보유 {Gold})");

            return false;
        }

        var board = GameManager.Instance != null
            ? GameManager.Instance.Board
            : null;

        if (board == null || !board.HasBenchSpace())
        {
            Debug.Log("[Shop] 벤치가 가득 참 — 구매 불가");
            return false;
        }

        if (UsesSharedShopPool)
        {
            if (_sharedPurchasePending || _sharedRerollPending)
            {
                Debug.Log("[SharedShop] 이전 상점 요청 처리 중 — 구매 불가");
                return false;
            }

            // 상점에 등장할 때 이미 풀에서 한 장 예약됐으므로
            // 여기서는 풀을 다시 차감하지 않는다.
            _sharedPurchasePending = true;
            _pendingPurchaseSlot = slot;
            _pendingPurchasePokemonId = data.id;

            Network.RequestSharedShopPurchase(slot, data.id);
            return true;
        }

        // 이하 솔로와 기존 오프라인 구매 방식.
        if (!HasPoolStock(data, 1))
        {
            Debug.Log(
                $"[ShopPool] {data.pokemonName} 남은 풀 수량 없음 — 구매 불가");

            return false;
        }

        PokemonUnit unit = UnitFactory.Create(data);

        if (unit == null)
            return false;

        if (!board.TryPlaceInBench(unit))
        {
            Destroy(unit.gameObject);
            return false;
        }

        DecreaseChampionPool(data, 1);

        AddGold(-data.cost);
        _slots[slot] = null;

        GameEvents.ShopRerolled();

        Debug.Log($"[Shop] {data.pokemonName} 구매 (-{data.cost}G)");
        return true;
    }

    /// <summary>
    /// MasterClient가 보낸 공용 상점 구매 승인 결과를 처리한다.
    /// 승인된 슬롯은 이미 공용 풀에서 예약 차감된 상태다.
    /// </summary>
    public void HandleSharedPurchaseResult(
        int slot,
        int pokemonId,
        bool approved)
    {
        bool matchesPendingRequest =
            _sharedPurchasePending &&
            _pendingPurchaseSlot == slot &&
            _pendingPurchasePokemonId == pokemonId;

        // 결과를 받았으므로 구매 대기 상태를 먼저 해제한다.
        _sharedPurchasePending = false;
        _pendingPurchaseSlot = -1;
        _pendingPurchasePokemonId = -1;

        if (!approved)
        {
            Debug.LogWarning(
                "[SharedShop] 상점 정보가 달라 구매가 승인되지 않았습니다.");

            return;
        }

        // 승인 결과가 현재 대기 중인 요청과 다르면
        // 잘못 소비된 한 장을 공용 풀에 되돌린다.
        if (!matchesPendingRequest)
        {
            Network?.ReturnSharedPoolCopies(pokemonId, 1);

            Debug.LogWarning(
                "[SharedShop] 구매 요청과 승인 결과가 달라 풀에 반환합니다.");

            return;
        }

        PokemonData data =
            _slots != null &&
            slot >= 0 &&
            slot < _slots.Length
                ? _slots[slot]
                : null;

        // 승인받은 ID와 현재 슬롯 정보가 다르면
        // 예약된 한 장을 반환하고 해당 슬롯을 비운다.
        if (data == null || data.id != pokemonId)
        {
            Network?.ReturnSharedPoolCopies(pokemonId, 1);

            if (_slots != null && slot >= 0 && slot < _slots.Length)
                _slots[slot] = null;

            GameEvents.ShopRerolled();

            Debug.LogWarning(
                "[SharedShop] 승인된 포켓몬과 현재 슬롯이 달라 구매를 취소합니다.");

            return;
        }

        var board = GameManager.Instance != null
            ? GameManager.Instance.Board
            : null;

        // 요청 후 골드 또는 벤치 상태가 달라졌다면
        // 구매하지 못한 예약 카피를 공용 풀에 반환한다.
        if (board == null ||
            !board.HasBenchSpace() ||
            Gold < data.cost)
        {
            Network?.ReturnSharedPoolCopies(pokemonId, 1);
            _slots[slot] = null;
            GameEvents.ShopRerolled();

            Debug.LogWarning(
                "[SharedShop] 구매 조건이 달라져 예약 카피를 풀에 반환합니다.");

            return;
        }

        PokemonUnit unit = UnitFactory.Create(data);

        if (unit == null || !board.TryPlaceInBench(unit))
        {
            if (unit != null)
                Destroy(unit.gameObject);

            Network?.ReturnSharedPoolCopies(pokemonId, 1);
            _slots[slot] = null;
            GameEvents.ShopRerolled();

            Debug.LogWarning(
                "[SharedShop] 유닛 배치 실패 — 예약 카피를 풀에 반환합니다.");

            return;
        }

        // 상점 등장 시 이미 풀에서 차감됐으므로
        // DecreaseChampionPool은 다시 호출하지 않는다.
        AddGold(-data.cost);
        _slots[slot] = null;

        GameEvents.ShopRerolled();

        Debug.Log(
            $"[SharedShop] {data.pokemonName} 구매 완료 (-{data.cost}G)");
    }

    private bool HasPoolStock(PokemonData data, int amount)
    {
        if (data == null) return false;
        return _remainingPool.TryGetValue(data, out int remain) && remain >= amount;
    }

    private void DecreaseChampionPool(PokemonData data, int amount)
    {
        if (data == null) return;
        if (!_remainingPool.ContainsKey(data)) return;

        _remainingPool[data] = Mathf.Max(0, _remainingPool[data] - amount);

        Debug.Log($"[ShopPool] {data.pokemonName} 풀 감소 -{amount} / 남은 수량: {_remainingPool[data]}");
    }

    /// <summary>
    /// 진화체를 판매하거나 반환할 때 실제 풀에 들어 있는 기본종을 찾는다.
    /// 기본종이면 입력받은 데이터를 그대로 반환한다.
    /// </summary>
    private PokemonData GetPoolBaseData(PokemonData data)
    {
        if (data == null)
            return null;

        return _evolvedToBase.TryGetValue(data, out PokemonData baseData)
            ? baseData
            : data;
    }

    private void ReturnToChampionPool(PokemonUnit unit)
    {
        if (unit == null || unit.data == null) return;

        // 진화 유닛(data가 진화체로 스왑됨)이면 소비된 기본종 풀로 되돌린다.
        PokemonData data = GetPoolBaseData(unit.data);

        if (!_remainingPool.ContainsKey(data))
            return;

        int amount = GetBaseUnitCount(unit.starLevel);
        int maxCount = GetInitialPoolCount(data.cost);

        _remainingPool[data] = Mathf.Min(maxCount, _remainingPool[data] + amount);

        Debug.Log($"[ShopPool] {data.pokemonName} 풀 복귀 +{amount} / 남은 수량: {_remainingPool[data]}");
    }

    private int GetBaseUnitCount(int starLevel)
    {
        return starLevel switch
        {
            2 => 3,
            3 => 9,
            _ => 1
        };
    }

    // ──────────────────────────────────────────
    // 아이템 상점
    // ──────────────────────────────────────────

    /// <summary>
    /// 아이템 상점 자동 갱신.
    /// 0번 슬롯 = 진화의 돌, 1~3번 슬롯 = 일반 아이템.
    /// </summary>
    public void RollItemShop()
    {
        if (_itemSlots == null || _itemSlots.Length != _itemShopSize)
            _itemSlots = new ScriptableObject[_itemShopSize];

        for (int i = 0; i < _itemSlots.Length; i++)
        {
            if (i == 0)
                _itemSlots[i] = RollOneStone();
            else
                _itemSlots[i] = RollOneItem();
        }

        GameEvents.ItemShopRerolled();
    }

    /// <summary>아이템 상점 무료 리롤 자원 지급(보상). 결과는 GameEvents.ItemShopRerollCountChanged로 통지.</summary>
    public void AddItemShopReroll(int amount)
    {
        if (amount <= 0) return;
        ItemShopRerollCount += amount;
        GameEvents.ItemShopRerollCountChanged(ItemShopRerollCount);
        Debug.Log($"[ItemShop] 무료 리롤 +{amount} => {ItemShopRerollCount}");
    }

    /// <summary>아이템 상점을 무료 리롤 자원으로 새로 굴림(수동). 자원이 없으면 실패. 성공 시 true.</summary>
    public bool RerollItemShop()
    {
        if (ItemShopRerollCount <= 0)
        {
            Debug.Log("[ItemShop] 무료 리롤 없음 — 아이템샵 리롤 불가");
            return false;
        }

        ItemShopRerollCount--;
        GameEvents.ItemShopRerollCountChanged(ItemShopRerollCount);
        RollItemShop();
        return true;
    }

    private EvolutionStoneData RollOneStone()
    {
        var db = EvolutionStoneDatabase.Instance;
        if (db == null || db.all == null || db.all.Count == 0)
        {
            Debug.LogWarning("[ItemShop] EvolutionStoneDatabase 비어있음 — 진화의 돌 슬롯 비움");
            return null;
        }

        var candidates = db.all.FindAll(s => s != null);
        if (candidates.Count == 0) return null;

        return candidates[Random.Range(0, candidates.Count)];
    }

    private ItemData RollOneItem()
    {
        var db = ItemDatabase.Instance;
        if (db == null || db.all == null || db.all.Count == 0)
        {
            Debug.LogWarning("[ItemShop] ItemDatabase 비어있음 — 아이템 슬롯 비움");
            return null;
        }

        var candidates = db.all.FindAll(i => i != null);
        if (candidates.Count == 0) return null;

        return candidates[Random.Range(0, candidates.Count)];
    }

    /// <summary>아이템 상점 슬롯의 상품을 아이템 쿠폰으로 구매해 인벤토리에 추가. 성공 시 true.</summary>
    public bool BuyItem(int slot)
    {
        if (_itemSlots == null || slot < 0 || slot >= _itemSlots.Length) return false;

        ScriptableObject product = _itemSlots[slot];
        if (product == null) return false;

        var itemManager = GameManager.Instance.Item;
        if (itemManager == null)
        {
            Debug.LogWarning("[ItemShop] ItemManager 없음 — 구매 불가");
            return false;
        }

        if (!itemManager.HasInventorySpace)
        {
            Debug.LogWarning("[ItemShop] 인벤토리 가득 참 — 구매 불가");
            return false;
        }

        if (itemManager.ItemCoupon < _itemPrice)
        {
            Debug.Log($"[ItemShop] 아이템 쿠폰 부족 — 필요 {_itemPrice}, 보유 {itemManager.ItemCoupon}");
            return false;
        }

        if (!itemManager.SpendItemCoupon(_itemPrice))
            return false;

        bool added = product switch
        {
            ItemData item => itemManager.AddItem(item),
            EvolutionStoneData stone => itemManager.AddStone(stone),
            _ => false
        };

        if (!added)
        {
            // 방어적 환불
            itemManager.AddItemCoupon(_itemPrice);
            Debug.LogWarning("[ItemShop] 인벤토리 추가 실패 — 쿠폰 환불");
            return false;
        }

        _itemSlots[slot] = null;

        GameEvents.ItemPurchased(product);
        GameEvents.ItemShopRerolled();

        Debug.Log($"[ItemShop] {GetItemShopName(product)} 구매 완료 (-{_itemPrice} 쿠폰)");
        return true;
    }

    private string GetItemShopName(ScriptableObject product)
    {
        return product switch
        {
            ItemData item => item.itemName,
            EvolutionStoneData stone => stone.stoneName,
            _ => product != null ? product.name : "(null)"
        };
    }

    // ──────────────────────────────────────────
    // 골드
    // ──────────────────────────────────────────

    /// <summary>골드 증감(보상/소비 등). 결과는 0 미만으로 내려가지 않음.</summary>
    public void AddGold(int amount)
    {
        Gold = Mathf.Max(0, Gold + amount);
        GameEvents.GoldChanged(Gold);
    }

    /// <summary>디버그/시드용: 런타임 풀 주입.</summary>
    public void SetPool(List<PokemonData> pool)
    {
        _pool = pool;
        InitializeChampionPool();
    }
}