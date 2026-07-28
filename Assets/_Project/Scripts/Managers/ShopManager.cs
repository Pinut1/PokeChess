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

    public int CurrentLevel => _currentLevel;
    public int CurrentXp { get; private set; }
    public int RequiredXp => GetRequiredXp(_currentLevel);
    public int UnitCap => GetUnitCap(_currentLevel);
    public int BuyXpCostGold => _buyXpCostGold;
    public int BuyXpAmount => _buyXpAmount;

    public int ShopSize => _shopSize;
    public int ItemShopSize => _itemShopSize;
    public int ItemPrice => _itemPrice;

    /// <summary>현재 유닛 상점에 공개된 후보(읽기 전용). null = 구매됨/빈 슬롯.</summary>
    public IReadOnlyList<PokemonData> CurrentSlots => _slots;

    /// <summary>현재 아이템 상점에 공개된 후보(읽기 전용). null = 구매됨/빈 슬롯.</summary>
    public IReadOnlyList<ScriptableObject> CurrentItemSlots => _itemSlots;

    /// <summary>
    /// 해당 아이템 슬롯을 이번 라운드에 리롤할 수 있는지.
    /// 슬롯당 1회이며 미사용분은 다음 라운드로 누적되지 않는다.
    /// </summary>
    public bool CanRerollItemSlot(int slot)
    {
        if (_itemSlotRerollUsed == null || slot < 0 || slot >= _itemSlotRerollUsed.Length) return false;
        return !_itemSlotRerollUsed[slot];
    }

    private PokemonData[] _slots;
    private ScriptableObject[] _itemSlots;

    /// <summary>슬롯별 이번 라운드 리롤 사용 여부. RollItemShop(매 라운드 갱신)마다 전부 false로 되돌린다.</summary>
    private bool[] _itemSlotRerollUsed;

    /// <summary>포켓몬별 남은 풀 수량. 구매 시 감소, 판매 시 복귀.</summary>
    private readonly Dictionary<PokemonData, int> _remainingPool = new();

    private sealed class SharedShopReservation
    {
        public int revision;
        public int[] pokemonIds;
    }

    /// <summary>MasterClient 권위의 플레이어별 상점 예약. 비마스터도 마스터 교체 대비 미러를 유지한다.</summary>
    private readonly Dictionary<int, SharedShopReservation> _sharedReservations = new();
    private bool _poolInitialized;
    private bool _sharedRollPending;
    private bool _sharedPurchasePending;
    private int _shopRevision;

    /// <summary>진화체 → 기본종(풀 관리 대상) 역매핑. 진화 유닛 판매 시 소비된 기본종 카피를 올바른 풀로 되돌리기 위함.</summary>
    private readonly Dictionary<PokemonData, PokemonData> _evolvedToBase = new();

    /// <summary>
    /// 현재 플레이어가 통신진화를 해금한 원본 ID → 통신진화체 ID.
    /// 플레이어별 로컬 상태이며, 파트너에게는 적용되지 않는다.
    /// </summary>
    private readonly Dictionary<int, int> _activeTradeEvolutions = new();

    /// <summary>
    /// 현재 상점 슬롯이 실제 공용 풀에서 예약한 원본 포켓몬 ID.
    /// 화면에는 통신진화체가 표시되더라도 풀 계산과 구매 승인은 이 ID를 사용한다.
    /// 0 = 구매됨/빈 슬롯.
    /// </summary>
    private int[] _slotPoolPokemonIds;

    private void Awake()
    {
        _slots = new PokemonData[_shopSize];
        _slotPoolPokemonIds = new int[_shopSize];
        _itemSlots = new ScriptableObject[_itemShopSize];
        _itemSlotRerollUsed = new bool[_itemShopSize];

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
        GameEvents.OnXpPurchaseRequested += HandleXpPurchaseRequested;
        GameEvents.OnShopRerollRequested += HandleShopRerollRequested;
    }

    private void OnDisable()
    {
        GameEvents.OnRoundChanged -= HandleRoundChanged;
        GameEvents.OnUnitSold -= HandleUnitSold;
        GameEvents.OnLevelChanged -= HandleLevelChanged;
        GameEvents.OnTeamRoundResolved -= HandleTeamRoundResolved;
        GameEvents.OnXpPurchaseRequested -= HandleXpPurchaseRequested;
        GameEvents.OnShopRerollRequested -= HandleShopRerollRequested;
    }

    private void HandleXpPurchaseRequested() => BuyXp();

    private void HandleShopRerollRequested() => Reroll();

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

        Roll();         // 초기 유닛 상점 공개
        RollItemShop(); // 초기 아이템 상점 공개
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

        _poolInitialized = true;

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

        Roll();         // 유닛 상점은 매 라운드 자동 갱신
        RollItemShop(); // 아이템 상점도 매 라운드 자동 갱신
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
    }

    /// <summary>BoardManager.SellUnit이 발행한 판매 이벤트 → 환급 골드 지급 + 챔피언 풀 복귀.</summary>
    private void HandleUnitSold(PokemonUnit unit)
    {
        ReturnToChampionPool(unit);

        int refund = SellValue(unit);
        if (refund > 0) AddGold(refund);
    }

    /// <summary>
    /// 판매 환급액 = 투자한 골드(코스트 × 합성에 들어간 1성 마리 수). 1성=×1, 2성=×3, 3성=×9.
    /// (= 산 만큼 그대로 돌려줌. TFT는 2코스트+ 고성에 -1 패널티가 있으나 여기선 단순 전액 환급 기본값.)
    /// </summary>
    public int SellValue(PokemonUnit unit)
    {
        if (unit == null || unit.data == null)
            return 0;

        // 통신진화/일반 진화 유닛은 공용 풀의 원본 종 가격으로 환급한다.
        PokemonData priceData =
            _evolvedToBase.TryGetValue(
                unit.data,
                out PokemonData baseData)
                ? baseData
                : unit.data;

        int baseUnits = GetBaseUnitCount(unit.starLevel);

        return priceData.cost * baseUnits;
    }

    // ──────────────────────────────────────────
    // 유닛 상점
    // ──────────────────────────────────────────

    /// <summary>샵 풀에서 레벨별 코스트 확률과 남은 풀 수량을 기준으로 _shopSize개를 다시 공개.</summary>
    public void Roll()
    {
        var network = GameManager.Instance != null ? GameManager.Instance.Network : null;
        if (network != null && network.UsesSharedShopPool)
        {
            if (_sharedRollPending || _sharedPurchasePending) return;
            _sharedRollPending = true;
            if (!network.RequestSharedShopRoll(_currentLevel, _cost4ForceOpen))
                _sharedRollPending = false;
            return;
        }

        if (_pool == null || _pool.Count == 0)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = null;
                _slotPoolPokemonIds[i] = 0;
            }
            GameEvents.ShopRerolled();
            return;
        }

        ReturnLocalShopSlotsToPool();
        for (int i = 0; i < _slots.Length; i++)
        {
            // 실제 공용 풀에서 예약되는 원본 데이터.
            PokemonData poolData =
                RollOnePokemonWeighted(
                    _currentLevel,
                    _cost4ForceOpen
                );

            _slotPoolPokemonIds[i] =
                poolData != null ? poolData.id : 0;

            // 화면에는 활성화된 통신진화체를 표시.
            _slots[i] =
                ResolveTradeEvolutionShopData(poolData);

            if (poolData != null)
                DecreaseChampionPool(poolData, 1);
        }

        GameEvents.ShopRerolled();
    }

    /// <summary>
    /// (구 4코 상시 오픈 플래그) Augment Table v2에서 "지속 확률 등장"이 제거되어 항상 false.
    /// 공유 상점 RPC 시그니처(forceCostFour) 호환용으로만 유지 — 공유 상점 작업 정리 시 제거 가능.
    /// </summary>
    private bool _cost4ForceOpen;

    /// <summary>
    /// ECONOMY_SHOP 증강 효과.
    /// 증강 선택 즉시 현재 상점을 서로 다른 4코스트 유닛으로
    /// 한 번만 무료 갱신한다.
    ///
    /// 호출 완료와 동시에 효과가 종료되며,
    /// 이후 수동 리롤과 다음 라운드 자동 갱신은
    /// 현재 레벨의 기본 상점 확률을 사용한다.
    /// </summary>
    public void OpenCostFourShopOnce()
    {
        var network = GameManager.Instance != null ? GameManager.Instance.Network : null;
        if (network != null && network.UsesSharedShopPool)
        {
            if (_sharedRollPending || _sharedPurchasePending) return;
            _sharedRollPending = true;
            if (!network.RequestSharedShopRoll(_currentLevel, false, true))
                _sharedRollPending = false;
            return;
        }

        ReturnLocalShopSlotsToPool();

        // 서로 다른 4코스트 원본 유닛을 우선 선택한다.
        var distinct = new List<PokemonData>();

        foreach (var pair in _remainingPool)
        {
            if (pair.Key != null &&
                pair.Key.cost == 4 &&
                pair.Value > 0)
            {
                distinct.Add(pair.Key);
            }
        }

        for (int i = 0; i < _slots.Length; i++)
        {
            PokemonData poolData;

            if (distinct.Count > 0)
            {
                int pick =
                    Random.Range(0, distinct.Count);

                poolData = distinct[pick];
                distinct.RemoveAt(pick);
            }
            else
            {
                poolData =
                    RollOnePokemonOfCostWeighted(
                        4,
                        _currentLevel,
                        false
                    );
            }

            _slotPoolPokemonIds[i] =
                poolData != null ? poolData.id : 0;

            _slots[i] =
                ResolveTradeEvolutionShopData(poolData);

            if (poolData != null)
                DecreaseChampionPool(poolData, 1);
        }

        GameEvents.ShopRerolled();
        Debug.Log($"[Shop] 구독서비스 — 서로 다른 4코 {_slots.Length}슬롯 무료 갱신");
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

    private int RollCostByLevel(int level)
    {
        int[] rates = GetCostRates(level);
        int total = 0;

        for (int i = 0; i < rates.Length; i++)
            total += rates[i];

        if (total <= 0)
            return 1;

        int roll = Random.Range(0, total);
        int cumulative = 0;

        for (int i = 0; i < rates.Length; i++)
        {
            cumulative += rates[i];
            if (roll < cumulative)
                return i + 1; // index 0 = 1코스트
        }

        return 1;
    }

    /// <summary>
    /// 현재 레벨의 코스트별 등장 확률(1~5코스트 순, 합 100). UI 표시용 읽기 전용 사본.
    /// 내부 배열을 그대로 넘기면 호출부가 확률표를 변경할 수 있으므로 복사해서 반환한다.
    /// </summary>
    public int[] GetCurrentCostRates()
    {
        return (int[])GetCostRates(CurrentLevel).Clone();
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

    /// <summary>
    /// 받는 플레이어가 통신진화 유닛을 실제로 수령했을 때 호출한다.
    /// 이후 상점 갱신부터 원본 대신 통신진화체를 표시하고,
    /// 현재 상점의 원본 카드도 구매 시 진화체로 생성한다.
    /// </summary>
    public void ActivateTradeEvolution(int basePokemonId, int evolvedPokemonId)
    {
        if (basePokemonId <= 0 || evolvedPokemonId <= 0)
            return;

        PokemonDatabase db = PokemonDatabase.Instance;

        PokemonData baseData =
            db != null ? db.GetById(basePokemonId) : null;

        PokemonData evolvedData =
            db != null ? db.GetById(evolvedPokemonId) : null;

        if (baseData == null || evolvedData == null)
        {
            Debug.LogWarning(
                $"[Shop][TradeEvolution] 데이터 조회 실패: " +
                $"{basePokemonId} → {evolvedPokemonId}"
            );
            return;
        }

        _activeTradeEvolutions[basePokemonId] = evolvedPokemonId;

        // 핫삼 판매 시 스라크 공용 풀로 반환하기 위한 역매핑.
        _evolvedToBase[evolvedData] = baseData;

        Debug.Log(
            $"[Shop][TradeEvolution] 상점 변환 활성화: " +
            $"{baseData.pokemonName} → {evolvedData.pokemonName}"
        );
    }

    /// <summary>
    /// 통신진화체 → 원본 종 역매핑만 등록한다(판매 시 풀 반환/가격 정산용).
    /// ActivateTradeEvolution과 달리 상점 카드 변환(해금)은 발생시키지 않는다.
    /// 이미 완성된 통신진화체를 재전송받은 경우처럼,
    /// 진화 "사건" 없이 진화체 유닛만 소유하게 됐을 때 사용.
    /// </summary>
    public void RegisterEvolvedToBase(int evolvedPokemonId, int basePokemonId)
    {
        if (evolvedPokemonId <= 0 || basePokemonId <= 0) return;

        var db = PokemonDatabase.Instance;
        PokemonData evolvedData = db != null ? db.GetById(evolvedPokemonId) : null;
        PokemonData baseData    = db != null ? db.GetById(basePokemonId)    : null;
        if (evolvedData == null || baseData == null)
        {
            Debug.LogWarning(
                $"[Shop][TradeEvolution] 역매핑 등록 실패: {evolvedPokemonId} → {basePokemonId}");
            return;
        }

        if (!_evolvedToBase.ContainsKey(evolvedData))
        {
            _evolvedToBase[evolvedData] = baseData;
            Debug.Log(
                $"[Shop][TradeEvolution] 판매 역매핑 등록: " +
                $"{evolvedData.pokemonName} → {baseData.pokemonName}");
        }
    }

    /// <summary>
    /// 공용 풀에서는 원본 ID를 유지하되,
    /// 현재 플레이어가 통신진화를 해금했다면 화면·구매 생성용 데이터만 진화체로 바꾼다.
    /// </summary>
    private PokemonData ResolveTradeEvolutionShopData(PokemonData poolData)
    {
        if (poolData == null)
            return null;

        if (!_activeTradeEvolutions.TryGetValue(
                poolData.id,
                out int evolvedPokemonId))
        {
            return poolData;
        }

        PokemonData evolvedData =
            PokemonDatabase.Instance != null
                ? PokemonDatabase.Instance.GetById(evolvedPokemonId)
                : null;

        if (evolvedData == null)
        {
            Debug.LogWarning(
                $"[Shop][TradeEvolution] 진화체 ID {evolvedPokemonId} 조회 실패 — " +
                $"{poolData.pokemonName} 그대로 표시"
            );

            return poolData;
        }

        return evolvedData;
    }

    // ──────────────────────────────────────────
    // 공용 풀 / 상점 확률 UI 조회 API
    // ──────────────────────────────────────────

    /// <summary>
    /// 현재 플레이어 레벨의 1~5코스트 등장 확률을 반환한다.
    /// index 0 = 1코스트, index 4 = 5코스트.
    /// </summary>
    public int[] GetCurrentCostRatesForDebug()
    {
        return GetCostRates(_currentLevel);
    }

    /// <summary>
    /// 상점 카드에 표시할 포켓몬별 풀 잔여 수량과
    /// 다음 상점 한 슬롯에서의 등장 확률을 계산한다.
    ///
    /// 등장 확률 =
    /// 해당 코스트 등장 확률
    /// × 해당 포켓몬 남은 수량
    /// ÷ 같은 코스트 전체 남은 수량
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

        // 진화체가 전달되면 실제 풀 관리 대상인 기본종으로 변환한다.
        PokemonData poolData =
            _evolvedToBase.TryGetValue(data, out PokemonData baseData)
                ? baseData
                : data;

        if (poolData == null ||
            !_remainingPool.TryGetValue(poolData, out remaining))
        {
            return false;
        }

        initial = GetInitialPoolCount(poolData.cost);

        // 같은 코스트 포켓몬들의 현재 남은 카피 수 합계.
        foreach (var pair in _remainingPool)
        {
            PokemonData candidate = pair.Key;

            if (candidate == null ||
                candidate.cost != poolData.cost)
            {
                continue;
            }

            sameCostRemaining += Mathf.Max(0, pair.Value);
        }

        int[] rates = GetCostRates(_currentLevel);
        int rateIndex = poolData.cost - 1;

        if (rateIndex < 0 || rateIndex >= rates.Length)
            return true;

        int totalRateWeight = 0;

        for (int i = 0; i < rates.Length; i++)
            totalRateWeight += Mathf.Max(0, rates[i]);

        if (totalRateWeight <= 0)
            return true;

        costRatePercent =
            100f *
            Mathf.Max(0, rates[rateIndex]) /
            totalRateWeight;

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
    /// 유닛 상점을 새로 굴림. 무료 리롤 자원을 우선 소모하고, 없으면 골드로 폴백. 성공 시 true.
    /// 실제 소모가 일어나면 GameEvents.RerollSpent를 발행(리롤 환급 증강 등의 훅).
    /// </summary>
    public bool Reroll()
    {
        if (_sharedRollPending || _sharedPurchasePending)
        {
            Debug.Log("[Shop] 이전 공유 풀 요청 처리 중 — 리롤 대기");
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

        Roll(); // 수동 리롤은 유닛 상점만 갱신. 아이템 상점은 갱신하지 않음.
        GameEvents.RerollSpent(); // 리롤 환급 증강(45%) 등이 구독 → 확률 판정 후 AddReroll로 환급
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

    /// <summary>슬롯의 포켓몬을 구매해 벤치에 배치. 성공 시 true.</summary>
    public bool Buy(int slot)
    {
        if (_slots == null || slot < 0 || slot >= _slots.Length)
            return false;

        // 화면에 표시되는 데이터.
        // 통신진화가 활성화됐다면 진화체일 수 있다.
        PokemonData displayedData = _slots[slot];

        if (displayedData == null)
            return false;

        // 실제 공용 풀에서 예약된 원본 데이터.
        // 예: 화면은 핫삼이지만 풀 기준은 스라크.
        PokemonData poolData =
            _slotPoolPokemonIds != null &&
            slot < _slotPoolPokemonIds.Length &&
            _slotPoolPokemonIds[slot] > 0 &&
            PokemonDatabase.Instance != null
                ? PokemonDatabase.Instance.GetById(
                    _slotPoolPokemonIds[slot]
                )
                : displayedData;

        if (poolData == null)
            return false;

        // 구매 시 실제 생성될 데이터.
        // 통신진화가 활성화됐다면 원본 대신 진화체 생성.
        PokemonData purchaseData =
            ResolveTradeEvolutionShopData(poolData);

        if (purchaseData == null)
            return false;

        NetworkManager network =
            GameManager.Instance != null
                ? GameManager.Instance.Network
                : null;

        bool usesSharedPool =
            network != null &&
            network.UsesSharedShopPool;

        if (_sharedRollPending || _sharedPurchasePending)
            return false;

        // 가격은 공용 풀 원본 데이터 기준.
        if (Gold < poolData.cost)
        {
            Debug.Log(
                $"[Shop] 골드 부족 — " +
                $"{purchaseData.pokemonName} 구매 실패 " +
                $"(필요 {poolData.cost}, 보유 {Gold})"
            );

            return false;
        }

        BoardManager board =
            GameManager.Instance != null
                ? GameManager.Instance.Board
                : null;

        if (board == null)
        {
            Debug.LogWarning("[Shop] BoardManager 없음 — 구매 불가");
            return false;
        }

        if (!board.HasBenchSpace())
        {
            Debug.Log("[Shop] 벤치가 가득 참 — 구매 불가");
            return false;
        }

        // 공유 풀 사용 시 MasterClient에게 구매 승인을 먼저 요청.
        // 실제 생성은 ResolveSharedShopPurchase에서 처리한다.
        if (usesSharedPool)
        {
            _sharedPurchasePending = true;

            if (!network.RequestSharedShopPurchase(
                    _shopRevision,
                    slot))
            {
                _sharedPurchasePending = false;
                return false;
            }

            return true;
        }

        // 로컬 풀 모드에서는 바로 구매 유닛 생성.
        PokemonUnit unit =
            UnitFactory.Create(purchaseData);

        if (unit == null)
            return false;

        // 상점에서 통신진화체를 구매한 경우
        // 특수진화 배율이 적용되도록 상태 표시.
        unit.isTradeEvolved =
            purchaseData != poolData;

        unit.ResetForBattle();

        if (!board.TryPlaceInBench(unit))
        {
            Destroy(unit.gameObject);
            return false;
        }

        AddGold(-poolData.cost);

        ClearShopSlot(slot);

        GameEvents.ShopRerolled();

        Debug.Log(
            $"[Shop] {purchaseData.pokemonName} 구매 " +
            $"(-{poolData.cost}G, 풀 기준 {poolData.pokemonName})"
        );

        return true;
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

    private void ReturnToChampionPool(PokemonUnit unit)
    {
        if (unit == null || unit.data == null) return;

        // 진화 유닛(data가 진화체로 스왑됨)이면 소비된 기본종 풀로 되돌린다.
        PokemonData data = _evolvedToBase.TryGetValue(unit.data, out var baseData) ? baseData : unit.data;

        if (!_remainingPool.ContainsKey(data))
            return;

        int amount = GetBaseUnitCount(unit.starLevel);

        var network = GameManager.Instance != null ? GameManager.Instance.Network : null;
        if (network != null && network.UsesSharedShopPool)
        {
            network.RequestSharedShopReturn(data.id, amount);
            return;
        }

        int maxCount = GetInitialPoolCount(data.cost);

        _remainingPool[data] = Mathf.Min(maxCount, _remainingPool[data] + amount);

        Debug.Log($"[ShopPool] {data.pokemonName} 풀 복귀 +{amount} / 남은 수량: {_remainingPool[data]}");
    }

    // ──────────────────────────────────────────
    // 공유 챔피언 풀 (MasterClient 권위)
    // ──────────────────────────────────────────

    /// <summary>기존 미구매 예약을 반환하고 새 상점 5칸을 풀에서 예약한다. MasterClient만 호출.</summary>
    public bool TryAuthorityRollSharedShop(
        int actorNumber, int level, bool forceCostFour, bool onlyCostFour,
        out int revision, out int[] pokemonIds)
    {
        EnsureChampionPoolInitialized();
        revision = 0;
        pokemonIds = null;
        if (!_poolInitialized || actorNumber <= 0) return false;

        if (_sharedReservations.TryGetValue(actorNumber, out var oldReservation))
            ReturnReservationToPool(oldReservation);

        int nextRevision = oldReservation != null ? oldReservation.revision + 1 : 1;
        var slots = new int[_shopSize];

        for (int i = 0; i < slots.Length; i++)
        {
            PokemonData selected = onlyCostFour
                ? RollOnePokemonOfCostWeighted(4, level, forceCostFour)
                : RollOnePokemonWeighted(level, forceCostFour);
            if (selected == null) continue;

            slots[i] = selected.id;
            DecreaseChampionPool(selected, 1); // 상점 노출 즉시 예약 차감
        }

        _sharedReservations[actorNumber] = new SharedShopReservation
        {
            revision = nextRevision,
            pokemonIds = slots
        };
        revision = nextRevision;
        pokemonIds = (int[])slots.Clone();
        Debug.Log($"[SharedShopPool] actor={actorNumber} 상점 예약 rev={revision}");
        return true;
    }

    /// <summary>예약 슬롯을 소유 상태로 전환한다. 풀은 노출 때 이미 차감됐으므로 추가 차감하지 않는다.</summary>
    public bool TryAuthorityPurchaseSharedShop(int actorNumber, int revision, int slot, out int pokemonId)
    {
        pokemonId = 0;
        if (!_sharedReservations.TryGetValue(actorNumber, out var reservation) ||
            reservation.revision != revision || reservation.pokemonIds == null ||
            slot < 0 || slot >= reservation.pokemonIds.Length)
            return false;

        pokemonId = reservation.pokemonIds[slot];
        if (pokemonId <= 0) return false;
        reservation.pokemonIds[slot] = 0;
        return true;
    }

    /// <summary>판매 또는 구매 커밋 실패 카피를 공유 풀에 반환한다. MasterClient만 호출.</summary>
    public void AuthorityReturnSharedShopCopy(int pokemonId, int amount)
    {
        if (amount <= 0) return;
        EnsureChampionPoolInitialized();
        PokemonData data = PokemonDatabase.Instance != null ? PokemonDatabase.Instance.GetById(pokemonId) : null;
        if (data == null || !_remainingPool.TryGetValue(data, out int remain)) return;

        _remainingPool[data] = Mathf.Min(GetInitialPoolCount(data.cost), remain + amount);
        Debug.Log($"[SharedShopPool] {data.pokemonName} 풀 복귀 +{amount} / 남은 {_remainingPool[data]}");
    }

    /// <summary>
    /// MasterClient 권위로 현재 남아 있는 공용 풀에서
    /// 지정 코스트 유닛 카피 1장을 실제로 선택하고 차감한다.
    ///
    /// 상점 슬롯 예약과는 별개이며 골드는 소비하지 않는다.
    /// 같은 코스트 내에서는 남은 카피 수에 비례해 선택한다.
    /// </summary>
    public bool TryAuthorityTakeDebugUnitByCost(
        int cost,
        out int pokemonId)
    {
        pokemonId = 0;

        if (cost < 1 || cost > 5)
        {
            Debug.LogWarning(
                $"[SharedShopPool][QA] 잘못된 코스트 요청: {cost}"
            );

            return false;
        }

        EnsureChampionPoolInitialized();

        if (!_poolInitialized)
        {
            Debug.LogWarning(
                "[SharedShopPool][QA] 챔피언 풀이 초기화되지 않음"
            );

            return false;
        }

        PokemonData selected =
            PickWeightedByRemaining(cost);

        if (selected == null)
        {
            Debug.LogWarning(
                $"[SharedShopPool][QA] {cost}코스트 남은 공용 풀 없음"
            );

            return false;
        }

        if (!HasPoolStock(selected, 1))
        {
            Debug.LogWarning(
                $"[SharedShopPool][QA] {selected.pokemonName} 재고 부족"
            );

            return false;
        }

        DecreaseChampionPool(selected, 1);

        pokemonId = selected.id;

        Debug.Log(
            $"[SharedShopPool][QA] {cost}코스트 선택: " +
            $"{selected.pokemonName}, pokemonId={pokemonId}"
        );

        return true;
    }

    /// <summary>
    /// MasterClient가 승인한 QA 유닛을 요청자의 벤치에 생성한다.
    ///
    /// 생성 또는 벤치 배치에 실패하면 이미 차감된 공용 풀 카피를
    /// 기존 RequestSharedShopReturn을 통해 다시 반환한다.
    /// </summary>
    public void ResolveSharedDebugUnitByCost(
        int cost,
        int pokemonId,
        bool success)
    {
        if (!success || pokemonId <= 0)
        {
            Debug.LogWarning(
                $"[SharedShopPool][QA] {cost}코스트 지급 요청 실패"
            );

            return;
        }

        PokemonData poolData =
            PokemonDatabase.Instance != null
                ? PokemonDatabase.Instance.GetById(pokemonId)
                : null;

        if (poolData == null)
        {
            Debug.LogError(
                $"[SharedShopPool][QA] Pokemon ID {pokemonId} 조회 실패"
            );

            GameManager.Instance?.Network?
                .RequestSharedShopReturn(pokemonId, 1);

            return;
        }

        if (poolData.cost != cost)
        {
            Debug.LogWarning(
                $"[SharedShopPool][QA] 요청 코스트와 승인 유닛 불일치: " +
                $"요청={cost}, 실제={poolData.cost}, " +
                $"pokemonId={pokemonId}"
            );

            GameManager.Instance?.Network?
                .RequestSharedShopReturn(pokemonId, 1);

            return;
        }

        BoardManager board =
            GameManager.Instance != null
                ? GameManager.Instance.Board
                : null;

        if (board == null)
        {
            Debug.LogError(
                "[SharedShopPool][QA] BoardManager 없음 — 지급 실패"
            );

            GameManager.Instance?.Network?
                .RequestSharedShopReturn(pokemonId, 1);

            return;
        }

        if (!board.HasBenchSpace())
        {
            Debug.LogWarning(
                "[SharedShopPool][QA] 벤치가 가득 참 — 지급 취소"
            );

            GameManager.Instance?.Network?
                .RequestSharedShopReturn(pokemonId, 1);

            return;
        }

        /*
         * 현재 플레이어가 해당 원본 종의 통신진화를 해금했다면
         * 일반 상점 구매와 동일하게 진화체로 생성한다.
         *
         * 공용 풀 차감과 판매 반환 기준은 여전히 poolData 원본이다.
         */
        PokemonData grantData =
            ResolveTradeEvolutionShopData(poolData);

        if (grantData == null)
        {
            Debug.LogError(
                $"[SharedShopPool][QA] 생성 데이터 조회 실패: " +
                $"{poolData.pokemonName}"
            );

            GameManager.Instance?.Network?
                .RequestSharedShopReturn(pokemonId, 1);

            return;
        }

        PokemonUnit unit =
            UnitFactory.Create(grantData);

        if (unit == null)
        {
            Debug.LogError(
                $"[SharedShopPool][QA] {grantData.pokemonName} 생성 실패"
            );

            GameManager.Instance?.Network?
                .RequestSharedShopReturn(pokemonId, 1);

            return;
        }

        unit.isTradeEvolved =
            grantData != poolData;

        unit.ResetForBattle();

        if (!board.TryPlaceInBench(unit))
        {
            Destroy(unit.gameObject);

            GameManager.Instance?.Network?
                .RequestSharedShopReturn(pokemonId, 1);

            Debug.LogWarning(
                $"[SharedShopPool][QA] {grantData.pokemonName} " +
                "벤치 배치 실패 — 공용 풀 반환 요청"
            );

            return;
        }

        /*
         * 골드는 차감하지 않는다.
         * 유닛은 일반 유닛과 완전히 동일하므로 판매하면
         * HandleUnitSold → ReturnToChampionPool을 통해 풀에 복귀한다.
         */
        Debug.Log(
            $"[SharedShopPool][QA] {grantData.pokemonName} " +
            $"{cost}코스트 무료 지급 완료 " +
            $"(풀 기준 {poolData.pokemonName})"
        );
    }

    /// <summary>
    /// QA 패널의 1~5코 유닛 획득 버튼이 호출하는 공개 진입점.
    ///
    /// 공유 풀 사용 중이면 NetworkManager를 통해 MasterClient에 요청한다.
    /// 솔로 또는 오프라인이면 현재 로컬 풀에서 직접 차감 후 생성한다.
    /// </summary>
    public bool DebugGrantUnitByCost(int cost)
    {
        if (cost < 1 || cost > 5)
        {
            Debug.LogWarning(
                $"[Shop][QA] 잘못된 코스트 요청: {cost}"
            );

            return false;
        }

        BoardManager board =
            GameManager.Instance != null
                ? GameManager.Instance.Board
                : null;

        if (board == null)
        {
            Debug.LogWarning(
                "[Shop][QA] BoardManager 없음 — 지급 불가"
            );

            return false;
        }

        if (!board.HasBenchSpace())
        {
            Debug.LogWarning(
                "[Shop][QA] 벤치가 가득 참 — 지급 불가"
            );

            return false;
        }

        NetworkManager network =
            GameManager.Instance != null
                ? GameManager.Instance.Network
                : null;

        /*
         * 멀티플레이:
         * MasterClient가 실제 공용 풀에서 선택 및 차감하고
         * ResolveSharedDebugUnitByCost로 결과를 돌려준다.
         */
        if (network != null &&
            network.UsesSharedShopPool)
        {
            bool requested =
                network.RequestSharedDebugUnitByCost(cost);

            if (!requested)
            {
                Debug.LogWarning(
                    $"[Shop][QA] {cost}코스트 공유 풀 요청 실패"
                );
            }

            return requested;
        }

        /*
         * 솔로/오프라인:
         * 로컬 _remainingPool이 권위 풀이므로
         * 이곳에서 직접 선택·차감 후 생성한다.
         */
        EnsureChampionPoolInitialized();

        PokemonData selected =
            PickWeightedByRemaining(cost);

        if (selected == null)
        {
            Debug.LogWarning(
                $"[Shop][QA] {cost}코스트 남은 로컬 풀 없음"
            );

            return false;
        }

        if (!HasPoolStock(selected, 1))
            return false;

        DecreaseChampionPool(selected, 1);

        PokemonData grantData =
            ResolveTradeEvolutionShopData(selected);

        if (grantData == null)
        {
            AuthorityReturnSharedShopCopy(
                selected.id,
                1
            );

            return false;
        }

        PokemonUnit unit =
            UnitFactory.Create(grantData);

        if (unit == null)
        {
            AuthorityReturnSharedShopCopy(
                selected.id,
                1
            );

            return false;
        }

        unit.isTradeEvolved =
            grantData != selected;

        unit.ResetForBattle();

        if (!board.TryPlaceInBench(unit))
        {
            Destroy(unit.gameObject);

            AuthorityReturnSharedShopCopy(
                selected.id,
                1
            );

            Debug.LogWarning(
                $"[Shop][QA] {grantData.pokemonName} " +
                "벤치 배치 실패 — 로컬 풀 복귀"
            );

            return false;
        }

        Debug.Log(
            $"[Shop][QA] {cost}코스트 무료 지급: " +
            $"{grantData.pokemonName}, " +
            $"풀 기준 {selected.pokemonName}"
        );

        return true;
    }

    /// <summary>
    /// 현재 공용 풀 또는 로컬 풀에 남아 있는
    /// 지정 코스트 전체 카피 수를 반환한다.
    ///
    /// 예:
    /// 주뱃 28장 + 꼬렛 30장 + 캐터피 29장
    /// → 1코스트 남은 수량 87장.
    /// </summary>
    public int GetRemainingPoolCountByCost(int cost)
    {
        if (cost < 1 || cost > 5)
            return 0;

        EnsureChampionPoolInitialized();

        int total = 0;

        foreach (var pair in _remainingPool)
        {
            PokemonData data = pair.Key;

            if (data == null ||
                data.cost != cost)
            {
                continue;
            }

            total += Mathf.Max(0, pair.Value);
        }

        return total;
    }


    public void ApplySharedShopSnapshot(int revision, int[] pokemonIds)
    {
        _sharedRollPending = false;
        if (revision <= 0 || pokemonIds == null)
        {
            Debug.LogWarning("[SharedShopPool] 상점 갱신 요청 실패");
            return;
        }

        if (_slots == null || _slots.Length != pokemonIds.Length)
            _slots = new PokemonData[pokemonIds.Length];

        if (_slotPoolPokemonIds == null ||
            _slotPoolPokemonIds.Length != pokemonIds.Length)
        {
            _slotPoolPokemonIds = new int[pokemonIds.Length];
        }

        var db = PokemonDatabase.Instance;
        for (int i = 0; i < pokemonIds.Length; i++)
        {
            int poolPokemonId = pokemonIds[i];

            _slotPoolPokemonIds[i] = poolPokemonId;

            PokemonData poolData =
                poolPokemonId > 0 && db != null
                    ? db.GetById(poolPokemonId)
                    : null;

            _slots[i] =
                ResolveTradeEvolutionShopData(poolData);
        }

        _shopRevision = revision;
        GameEvents.ShopRerolled();
    }

    /// <summary>공유 상점 구매 승인 후 로컬 골드/벤치 상태를 커밋한다.</summary>
    public void ResolveSharedShopPurchase(
        int revision,
        int slot,
        int pokemonId,
        bool success)
    {
        _sharedPurchasePending = false;

        if (!success ||
            revision != _shopRevision ||
            _slots == null ||
            slot < 0 ||
            slot >= _slots.Length ||
            _slots[slot] == null)
        {
            Debug.LogWarning(
                "[SharedShopPool] 구매 승인 실패 또는 오래된 응답"
            );
            return;
        }

        // MasterClient가 승인한 ID는 항상 공용 풀의 원본 ID.
        PokemonData poolData =
            PokemonDatabase.Instance != null
                ? PokemonDatabase.Instance.GetById(pokemonId)
                : null;

        if (poolData == null)
        {
            Debug.LogWarning(
                $"[SharedShopPool] 승인된 원본 ID {pokemonId} 조회 실패"
            );
            return;
        }

        // 현재 슬롯이 예약한 원본과 응답 ID가 같은지 검증.
        if (_slotPoolPokemonIds == null ||
            slot >= _slotPoolPokemonIds.Length ||
            _slotPoolPokemonIds[slot] != pokemonId)
        {
            Debug.LogWarning(
                "[SharedShopPool] 슬롯 원본 ID와 구매 승인 ID가 일치하지 않음"
            );
            return;
        }

        // 통신진화가 활성화됐다면 실제 생성은 진화체로 한다.
        PokemonData purchaseData =
            ResolveTradeEvolutionShopData(poolData);

        if (purchaseData == null)
        {
            GameManager.Instance?.Network?
                .RequestSharedShopReturn(pokemonId, 1);

            ClearShopSlot(slot);
            GameEvents.ShopRerolled();
            return;
        }

        BoardManager board =
            GameManager.Instance != null
                ? GameManager.Instance.Board
                : null;

        if (board == null ||
            Gold < poolData.cost ||
            !board.HasBenchSpace())
        {
            GameManager.Instance?.Network?
                .RequestSharedShopReturn(pokemonId, 1);

            ClearShopSlot(slot);
            GameEvents.ShopRerolled();

            Debug.LogWarning(
                "[SharedShopPool] 구매 커밋 실패 — 예약 카피 풀 반환"
            );
            return;
        }

        PokemonUnit unit =
            UnitFactory.Create(purchaseData);

        if (unit == null)
        {
            GameManager.Instance?.Network?
                .RequestSharedShopReturn(pokemonId, 1);

            ClearShopSlot(slot);
            GameEvents.ShopRerolled();
            return;
        }

        unit.isTradeEvolved =
            purchaseData != poolData;

        unit.ResetForBattle();

        if (!board.TryPlaceInBench(unit))
        {
            Destroy(unit.gameObject);

            GameManager.Instance?.Network?
                .RequestSharedShopReturn(pokemonId, 1);

            ClearShopSlot(slot);
            GameEvents.ShopRerolled();
            return;
        }

        AddGold(-poolData.cost);

        ClearShopSlot(slot);
        GameEvents.ShopRerolled();

        Debug.Log(
            $"[SharedShopPool] {purchaseData.pokemonName} 구매 확정 " +
            $"(-{poolData.cost}G, 풀 기준 {poolData.pokemonName})"
        );
    }

    /// <summary>유닛 상점의 표시 데이터와 풀 원본 ID를 함께 비운다.</summary>
    private void ClearShopSlot(int slot)
    {
        if (_slots != null &&
            slot >= 0 &&
            slot < _slots.Length)
        {
            _slots[slot] = null;
        }

        if (_slotPoolPokemonIds != null &&
            slot >= 0 &&
            slot < _slotPoolPokemonIds.Length)
        {
            _slotPoolPokemonIds[slot] = 0;
        }
    }

    /// <summary>모든 클라이언트가 권위 풀/예약 미러를 유지해 MasterClient 교체에 대비한다.</summary>
    public void ApplySharedPoolMirror(int[] pokemonIds, int[] remaining, int actorNumber, int revision, int[] slots)
    {
        EnsureChampionPoolInitialized();
        var db = PokemonDatabase.Instance;
        if (pokemonIds != null && remaining != null && db != null)
        {
            int count = Mathf.Min(pokemonIds.Length, remaining.Length);
            for (int i = 0; i < count; i++)
            {
                PokemonData data = db.GetById(pokemonIds[i]);
                if (data != null && _remainingPool.ContainsKey(data))
                    _remainingPool[data] = Mathf.Max(0, remaining[i]);
            }
        }

        if (actorNumber > 0 && slots != null)
        {
            _sharedReservations[actorNumber] = new SharedShopReservation
            {
                revision = revision,
                pokemonIds = (int[])slots.Clone()
            };
        }
    }

    public void GetSharedPoolMirror(out int[] pokemonIds, out int[] remaining)
    {
        EnsureChampionPoolInitialized();
        pokemonIds = new int[_remainingPool.Count];
        remaining = new int[_remainingPool.Count];
        int index = 0;
        foreach (var pair in _remainingPool)
        {
            pokemonIds[index] = pair.Key.id;
            remaining[index] = pair.Value;
            index++;
        }
    }

    public IEnumerable<(int actorNumber, int revision, int[] slots)> GetSharedReservationsMirror()
    {
        foreach (var pair in _sharedReservations)
            yield return (pair.Key, pair.Value.revision, (int[])pair.Value.pokemonIds.Clone());
    }

    private void EnsureChampionPoolInitialized()
    {
        if (_poolInitialized) return;
        if (_useDatabasePool) SeedPoolFromDatabase();
        InitializeChampionPool();
    }

    private void ReturnReservationToPool(SharedShopReservation reservation)
    {
        if (reservation?.pokemonIds == null) return;
        foreach (int id in reservation.pokemonIds)
            if (id > 0) AuthorityReturnSharedShopCopy(id, 1);
    }

    private void ReturnLocalShopSlotsToPool()
    {
        if (_slotPoolPokemonIds == null)
            return;

        PokemonDatabase db =
            PokemonDatabase.Instance;

        for (int i = 0; i < _slotPoolPokemonIds.Length; i++)
        {
            int poolPokemonId =
                _slotPoolPokemonIds[i];

            if (poolPokemonId <= 0 || db == null)
                continue;

            PokemonData poolData =
                db.GetById(poolPokemonId);

            if (poolData == null ||
                !_remainingPool.TryGetValue(
                    poolData,
                    out int remain))
            {
                continue;
            }

            int maxCount =
                GetInitialPoolCount(poolData.cost);

            _remainingPool[poolData] =
                Mathf.Min(maxCount, remain + 1);

            _slotPoolPokemonIds[i] = 0;
        }
    }

    private PokemonData RollOnePokemonWeighted(int level, bool forceCostFour)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            PokemonData selected = PickWeightedByRemaining(RollCostByLevel(level, forceCostFour));
            if (selected != null) return selected;
        }
        return PickWeightedByRemaining(0);
    }

    private PokemonData RollOnePokemonOfCostWeighted(int cost, int fallbackLevel, bool forceCostFour)
    {
        return PickWeightedByRemaining(cost) ?? RollOnePokemonWeighted(fallbackLevel, forceCostFour);
    }

    /// <summary>같은 코스트에서는 종 균등이 아니라 남은 카피 수 비례로 뽑는다.</summary>
    private PokemonData PickWeightedByRemaining(int cost)
    {
        int totalCopies = 0;
        foreach (var pair in _remainingPool)
            if (pair.Key != null && pair.Value > 0 && (cost <= 0 || pair.Key.cost == cost))
                totalCopies += pair.Value;
        if (totalCopies <= 0) return null;

        int roll = Random.Range(0, totalCopies);
        foreach (var pair in _remainingPool)
        {
            if (pair.Key == null || pair.Value <= 0 || (cost > 0 && pair.Key.cost != cost)) continue;
            if (roll < pair.Value) return pair.Key;
            roll -= pair.Value;
        }
        return null;
    }

    private int RollCostByLevel(int level, bool forceCostFour)
    {
        bool previous = _cost4ForceOpen;
        _cost4ForceOpen = forceCostFour;
        int result = RollCostByLevel(level);
        _cost4ForceOpen = previous;
        return result;
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

        if (_itemSlotRerollUsed == null || _itemSlotRerollUsed.Length != _itemShopSize)
            _itemSlotRerollUsed = new bool[_itemShopSize];

        for (int i = 0; i < _itemSlots.Length; i++)
        {
            if (i == 0)
                _itemSlots[i] = RollOneStone();
            else
                _itemSlots[i] = RollOneItem();

            _itemSlotRerollUsed[i] = false; // 지난 라운드 사용 여부와 무관하게 리롤 권한 복구(누적 없음)
        }

        GameEvents.ItemShopRerolled();
    }

    /// <summary>
    /// 아이템 상점 슬롯 1칸만 다시 굴린다(카드에 붙은 개별 리롤 버튼용). 비용 없음, 슬롯당 라운드 1회.
    /// 슬롯 규칙은 RollItemShop과 동일 — 0번은 진화의 돌, 1~3번은 일반 아이템만 나온다.
    /// 구매로 비워진 슬롯도 리롤 대상이다. 성공 시 true.
    /// </summary>
    public bool RerollItemSlot(int slot)
    {
        if (_itemSlots == null || slot < 0 || slot >= _itemSlots.Length) return false;

        if (!CanRerollItemSlot(slot))
        {
            Debug.Log($"[ItemShop] {slot}번 슬롯은 이번 라운드 리롤을 이미 사용함");
            return false;
        }

        _itemSlots[slot] = slot == 0 ? (ScriptableObject)RollOneStone() : RollOneItem();
        _itemSlotRerollUsed[slot] = true;

        GameEvents.ItemShopRerolled();
        Debug.Log($"[ItemShop] {slot}번 슬롯 리롤 완료 — 이번 라운드 재리롤 불가");
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
