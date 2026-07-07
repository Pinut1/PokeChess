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

    public int ShopSize => _shopSize;
    public int ItemShopSize => _itemShopSize;
    public int ItemPrice => _itemPrice;

    /// <summary>현재 유닛 상점에 공개된 후보(읽기 전용). null = 구매됨/빈 슬롯.</summary>
    public IReadOnlyList<PokemonData> CurrentSlots => _slots;

    /// <summary>현재 아이템 상점에 공개된 후보(읽기 전용). null = 구매됨/빈 슬롯.</summary>
    public IReadOnlyList<ScriptableObject> CurrentItemSlots => _itemSlots;

    private PokemonData[] _slots;
    private ScriptableObject[] _itemSlots;

    /// <summary>포켓몬별 남은 풀 수량. 구매 시 감소, 판매 시 복귀.</summary>
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
    /// 유닛 상점을 새로 굴림. 무료 리롤 자원을 우선 소모하고, 없으면 골드로 폴백. 성공 시 true.
    /// 실제 소모가 일어나면 GameEvents.RerollSpent를 발행(리롤 환급 증강 등의 훅).
    /// </summary>
    public bool Reroll()
    {
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
        if (_slots == null || slot < 0 || slot >= _slots.Length) return false;

        PokemonData data = _slots[slot];
        if (data == null) return false;

        if (!HasPoolStock(data, 1))
        {
            Debug.Log($"[ShopPool] {data.pokemonName} 남은 풀 수량 없음 — 구매 불가");
            return false;
        }

        if (Gold < data.cost)
        {
            Debug.Log($"[Shop] 골드 부족 — {data.pokemonName} 구매 실패 (필요 {data.cost}, 보유 {Gold})");
            return false;
        }

        var board = GameManager.Instance.Board;
        if (!board.HasBenchSpace())
        {
            Debug.Log("[Shop] 벤치가 가득 참 — 구매 불가");
            return false;
        }

        PokemonUnit unit = UnitFactory.Create(data);
        if (unit == null) return false;

        if (!board.TryPlaceInBench(unit))
        {
            Destroy(unit.gameObject); // 방어적 정리(이론상 도달 안 함)
            return false;
        }

        DecreaseChampionPool(data, 1);

        AddGold(-data.cost);
        _slots[slot] = null;
        GameEvents.ShopRerolled(); // 표시 갱신(슬롯 비움 반영)
        Debug.Log($"[Shop] {data.pokemonName} 구매 (-{data.cost}G)");
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