using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 시너지의 현재 상태. SynergyManager가 재계산할 때마다 갱신됨.
/// </summary>
[System.Serializable]
public class SynergyStatus
{
    public SynergyData data;
    public int uniqueCount;       // 보드 위 고유 계열 수 (진화 계열·같은 포켓몬 중복은 1로 카운트)
    public int activeTierIndex;   // -1 = 비활성, 0부터 = data.tiers 인덱스

    public bool IsActive => activeTierIndex >= 0;
    public SynergyTier ActiveTier => IsActive ? data.tiers[activeTierIndex] : null;
}

/// <summary>
/// 보드 위 유닛 기준 시너지 카운트/활성 티어 계산 담당.
/// OnUnitPlaced/OnUnitBenched/OnUnitSold 트리거 수신 → BoardManager.GetUnitsOnBoard() pull → 전체 재계산.
/// 실제 버프 적용은 BattleManager가 전투 시작 시 GetActiveSynergies()를 pull해서 처리한다.
/// </summary>
public class SynergyManager : MonoBehaviour
{
    [Header("(선택) 수동 오버라이드 — 비우면 중앙 SynergyDatabase 사용")]
    [Tooltip("디버그/테스트용. 평상시엔 비워두고 'Import Synergy JSON'으로 채워진 중앙 DB를 자동 사용.")]
    [SerializeField] private List<SynergyData> _overrideDatabase = new();

    // 실제 사용하는 시너지 목록(중앙 DB 또는 오버라이드). Awake에서 확정.
    private List<SynergyData> _all = new();

    // 한글명/영문명 → SynergyData (PokemonData.synergies가 어느 쪽이든 매칭되도록 양쪽 키 등록)
    private readonly Dictionary<string, SynergyData> _synergyLookup = new();

    // 시너지 id → 현재 상태
    private readonly Dictionary<int, SynergyStatus> _statuses = new();

    // 미등록 시너지 문자열 경고는 종류당 1회만
    private readonly HashSet<string> _warnedUnknown = new();

    private void Awake()
    {
        // 중앙 DB 우선(PokemonDatabase 등과 동일 패턴). 인스펙터 오버라이드가 있으면 그걸 사용(디버그용).
        _all = (_overrideDatabase != null && _overrideDatabase.Count > 0)
            ? _overrideDatabase
            : (SynergyDatabase.Instance != null ? SynergyDatabase.Instance.all : new List<SynergyData>());

        foreach (var data in _all)
        {
            if (data == null) continue;
            if (!string.IsNullOrEmpty(data.synergyName))   _synergyLookup[data.synergyName]   = data;
            if (!string.IsNullOrEmpty(data.synergyNameEn)) _synergyLookup[data.synergyNameEn] = data;
        }

        if (_all.Count == 0)
            Debug.LogWarning("[Synergy] 시너지 데이터 비어있음 — 'Import Synergy JSON' 실행 필요(SynergyDatabase.asset 생성)");
    }

    // ─────────────────────────────────────────
    // 이벤트 구독 — 보드 상태가 바뀔 때마다 재계산
    // ─────────────────────────────────────────

    private void OnEnable()
    {
        GameEvents.OnUnitPlaced  += HandleBoardChanged;
        GameEvents.OnUnitBenched += HandleBoardChanged;
        GameEvents.OnUnitSold    += HandleBoardChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnUnitPlaced  -= HandleBoardChanged;
        GameEvents.OnUnitBenched -= HandleBoardChanged;
        GameEvents.OnUnitSold    -= HandleBoardChanged;
    }

    private void HandleBoardChanged(PokemonUnit _) => RecalculateSynergies();

    // ─────────────────────────────────────────
    // 재계산
    // ─────────────────────────────────────────

    /// <summary>
    /// 보드 전체를 pull해서 시너지 상태를 처음부터 다시 계산.
    /// 보드는 최대 28칸이라 증분 관리 없이 전체 재계산으로 충분.
    /// </summary>
    public void RecalculateSynergies()
    {
        var board = GameManager.Instance.Board;
        if (board == null) return;

        var boardSpecies = new List<PokemonData>();
        foreach (var unit in board.GetUnitsOnBoard())
            if (unit != null && unit.data != null) boardSpecies.Add(unit.data);

        _statuses.Clear();
        foreach (var kv in ComputeSynergyStatuses(boardSpecies))
            _statuses[kv.Key] = kv.Value;

        GameEvents.SynergyUpdated();
    }

    /// <summary>
    /// 순수 계산 — 보드 위 종(PokemonData) 목록만으로 시너지 상태를 계산한다. RecalculateSynergies가
    /// board.GetUnitsOnBoard()에서 뽑아 쓰는 것과 완전히 같은 로직이며(이 메서드로 추출), PokemonUnit
    /// 인스턴스나 실제 보드 상태를 전혀 요구하지 않는다 — 성급/장착 아이템은 시너지 카운트에 영향이
    /// 없고, unit.data 자체가 이미 진화/돌 진화/통신진화 반영 후의 최종 종이라 speciesId 하나만
    /// 있으면 충분하다. 그래서 파트너 관전(BoardSnapshot.Entry.speciesId, PokemonUnit이 없는 곳)에서도
    /// 그대로 재사용할 수 있다 — 이 인스턴스가 이미 들고 있는 _all/_synergyLookup(SynergyDatabase
    /// 로드 결과)을 그대로 쓰고, 새 DB 접근이나 계산 경로를 만들지 않는다.
    /// 인자로 받은 목록은 호출측이 "보드 위(벤치 제외)"만 추리는 책임을 진다 — 이 메서드는 필터링하지 않는다.
    /// </summary>
    public Dictionary<int, SynergyStatus> ComputeSynergyStatuses(IEnumerable<PokemonData> boardSpecies)
    {
        // 시너지 id → 고유 카운트 키 집합.
        // 키는 기본적으로 진화 계열 루트(EvolutionFamily), countPerSpecies 시너지만 종 id.
        var keysPerSynergy = new Dictionary<int, HashSet<int>>();

        foreach (var data in boardSpecies)
        {
            if (data == null) continue;

            foreach (var synergyKey in data.synergies)
            {
                if (!_synergyLookup.TryGetValue(synergyKey, out var synergyData))
                {
                    if (_warnedUnknown.Add(synergyKey))
                        Debug.LogWarning($"[Synergy] 미등록 시너지 문자열: \"{synergyKey}\" ({data.pokemonName}) — 데이터 오타 확인");
                    continue;
                }

                if (!keysPerSynergy.TryGetValue(synergyData.id, out var keys))
                    keysPerSynergy[synergyData.id] = keys = new HashSet<int>();

                // 기본은 진화 계열 단위 — 이상해씨·이상해풀·이상해꽃을 나란히 올려도 1카운트.
                // 상점에서 한 번 투자한 개체의 성장 과정이지 종류가 늘어난 게 아니기 때문.
                // 돌연변이(countPerSpecies)만 종 단위 — 이브이 진화체 수집이 시너지 설계 자체라
                // 계열로 묶으면 최대 1카운트가 되어 성립하지 않는다.
                keys.Add(synergyData.countPerSpecies
                    ? data.id
                    : EvolutionFamily.RootId(data)); // 중복은 HashSet이 걸러줌
            }
        }

        var result = new Dictionary<int, SynergyStatus>();
        foreach (var data in _all)
        {
            if (data == null) continue;

            int count = keysPerSynergy.TryGetValue(data.id, out var keys) ? keys.Count : 0;
            if (count == 0) continue;

            result[data.id] = new SynergyStatus
            {
                data = data,
                uniqueCount = count,
                activeTierIndex = FindActiveTierIndex(data, count)
            };
        }

        return result;
    }

    /// <summary>충족하는 가장 높은 티어 인덱스. 없으면 -1. (tiers는 임포터가 count 오름차순으로 저장)</summary>
    private static int FindActiveTierIndex(SynergyData data, int count)
    {
        for (int i = data.tiers.Count - 1; i >= 0; i--)
            if (count >= data.tiers[i].count)
                return i;
        return -1;
    }

    // ─────────────────────────────────────────
    // 조회 API (UI / BattleManager가 pull)
    // ─────────────────────────────────────────

    /// <summary>활성화된 시너지만 (티어 충족). 전투 시작 시 버프 적용의 기준.</summary>
    public IReadOnlyList<SynergyStatus> GetActiveSynergies()
    {
        var result = new List<SynergyStatus>();
        foreach (var status in _statuses.Values)
            if (status.IsActive)
                result.Add(status);
        return result;
    }

    /// <summary>카운트가 1 이상인 시너지 전부 (비활성 포함 — UI 회색 표시용).</summary>
    public IReadOnlyList<SynergyStatus> GetAllSynergyStatuses()
    {
        return new List<SynergyStatus>(_statuses.Values);
    }
}
