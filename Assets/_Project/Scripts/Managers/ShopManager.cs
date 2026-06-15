using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기물 상점 + 골드 경제 담당.
/// 라운드마다 수입을 지급하고 샵을 자동 리롤한다. 구매 시 유닛을 생성해 벤치에 배치.
/// 매니저 간 직접 참조 금지 — 상태 변화는 GameEvents로 통지.
/// </summary>
public class ShopManager : MonoBehaviour
{
    [Header("샵 풀 (PokemonData 에셋 할당; 비어있으면 ShopDebugTest가 런타임 시드)")]
    [SerializeField] private List<PokemonData> _pool = new();

    [Header("샵 설정")]
    [SerializeField] private int _shopSize = 5;
    [SerializeField] private int _rerollCost = 2;

    [Header("골드 설정")]
    [SerializeField] private int _startingGold = 10;
    [SerializeField] private int _incomePerRound = 5;

    public int Gold { get; private set; }
    public int ShopSize => _shopSize;

    /// <summary>현재 샵에 공개된 후보(읽기 전용). null = 구매됨/빈 슬롯.</summary>
    public IReadOnlyList<PokemonData> CurrentSlots => _slots;

    private PokemonData[] _slots;

    private void Awake()
    {
        _slots = new PokemonData[_shopSize];
        Gold = _startingGold;
    }

    private void OnEnable()
    {
        GameEvents.OnRoundChanged += HandleRoundChanged;
        GameEvents.OnUnitSold     += HandleUnitSold;
    }

    private void OnDisable()
    {
        GameEvents.OnRoundChanged -= HandleRoundChanged;
        GameEvents.OnUnitSold     -= HandleUnitSold;
    }

    private void Start()
    {
        GameEvents.GoldChanged(Gold);
        Roll(); // 초기 샵 공개
    }

    private void HandleRoundChanged(int round)
    {
        AddGold(_incomePerRound);
        Roll();
    }

    /// <summary>BoardManager.SellUnit이 발행한 판매 이벤트 → 환급 골드 지급.</summary>
    private void HandleUnitSold(PokemonUnit unit)
    {
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
        int baseUnits = unit.starLevel switch { 2 => 3, 3 => 9, _ => 1 };
        return unit.data.cost * baseUnits;
    }

    // ──────────────────────────────────────────
    // 샵
    // ──────────────────────────────────────────

    /// <summary>샵 풀에서 무작위로 _shopSize개를 다시 공개.</summary>
    public void Roll()
    {
        if (_pool == null || _pool.Count == 0)
        {
            for (int i = 0; i < _slots.Length; i++) _slots[i] = null;
            GameEvents.ShopRerolled();
            return;
        }

        for (int i = 0; i < _slots.Length; i++)
            _slots[i] = _pool[Random.Range(0, _pool.Count)];

        GameEvents.ShopRerolled();
    }

    /// <summary>골드를 내고 샵을 새로 굴림. 성공 시 true.</summary>
    public bool Reroll()
    {
        if (Gold < _rerollCost)
        {
            Debug.Log("[Shop] 골드 부족 — 리롤 불가");
            return false;
        }
        AddGold(-_rerollCost);
        Roll();
        return true;
    }

    /// <summary>슬롯의 포켓몬을 구매해 벤치에 배치. 성공 시 true.</summary>
    public bool Buy(int slot)
    {
        if (_slots == null || slot < 0 || slot >= _slots.Length) return false;

        PokemonData data = _slots[slot];
        if (data == null) return false;

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

        AddGold(-data.cost);
        _slots[slot] = null;
        GameEvents.ShopRerolled(); // 표시 갱신(슬롯 비움 반영)
        Debug.Log($"[Shop] {data.pokemonName} 구매 (-{data.cost}G)");
        return true;
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
    public void SetPool(List<PokemonData> pool) => _pool = pool;
}
