using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 시너지의 현재 상태. SynergyManager가 재계산할 때마다 갱신됨.
/// </summary>
[System.Serializable]
public class SynergyStatus
{
    public SynergyData data;
    public int uniqueCount;       // 보드 위 고유 종 수 (같은 포켓몬 중복은 1로 카운트)
    public int activeTierIndex;   // -1 = 비활성, 0부터 = data.tiers 인덱스

    public bool IsActive => activeTierIndex >= 0;
    public SynergyTier ActiveTier => IsActive ? data.tiers[activeTierIndex] : null;
}

/// <summary>
/// 보드 위 유닛 기준 시너지 카운트/활성 티어 계산 담당.
/// OnUnitPlaced/OnUnitBenched/OnUnitSold 트리거 수신 → BoardManager.GetUnitsOnBoard() pull → 전체 재계산.
/// 실제 버프 적용은 BattleManager가 전투 시작 시 GetActiveSynergies()를 pull해서 처리 (TODO: BattleManager 구현 시).
/// </summary>
public class SynergyManager : MonoBehaviour
{
    [Header("시너지 데이터베이스 (ScriptableObjects/Synergies 에셋 할당)")]
    [SerializeField] private List<SynergyData> _synergyDatabase = new();

    // 한글명/영문명 → SynergyData (PokemonData.synergies가 어느 쪽이든 매칭되도록 양쪽 키 등록)
    private readonly Dictionary<string, SynergyData> _synergyLookup = new();

    // 시너지 id → 현재 상태
    private readonly Dictionary<int, SynergyStatus> _statuses = new();

    // 미등록 시너지 문자열 경고는 종류당 1회만
    private readonly HashSet<string> _warnedUnknown = new();

    private void Awake()
    {
        foreach (var data in _synergyDatabase)
        {
            if (data == null) continue;
            if (!string.IsNullOrEmpty(data.synergyName))   _synergyLookup[data.synergyName]   = data;
            if (!string.IsNullOrEmpty(data.synergyNameEn)) _synergyLookup[data.synergyNameEn] = data;
        }

        if (_synergyDatabase.Count == 0)
            Debug.LogWarning("[Synergy] 시너지 데이터베이스가 비어있음 — 인스펙터에 Synergies 에셋 할당 필요");
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

        // 시너지 id → 고유 종(pokemon id) 집합
        var speciesPerSynergy = new Dictionary<int, HashSet<int>>();

        foreach (var unit in board.GetUnitsOnBoard())
        {
            if (unit == null || unit.data == null) continue;

            foreach (var synergyKey in unit.data.synergies)
            {
                if (!_synergyLookup.TryGetValue(synergyKey, out var synergyData))
                {
                    if (_warnedUnknown.Add(synergyKey))
                        Debug.LogWarning($"[Synergy] 미등록 시너지 문자열: \"{synergyKey}\" ({unit.data.pokemonName}) — 데이터 오타 확인");
                    continue;
                }

                if (!speciesPerSynergy.TryGetValue(synergyData.id, out var species))
                    speciesPerSynergy[synergyData.id] = species = new HashSet<int>();

                species.Add(unit.data.id); // 같은 종 중복은 HashSet이 걸러줌
            }
        }

        _statuses.Clear();
        foreach (var data in _synergyDatabase)
        {
            if (data == null) continue;

            int count = speciesPerSynergy.TryGetValue(data.id, out var species) ? species.Count : 0;
            if (count == 0) continue;

            _statuses[data.id] = new SynergyStatus
            {
                data = data,
                uniqueCount = count,
                activeTierIndex = FindActiveTierIndex(data, count)
            };
        }

        GameEvents.SynergyUpdated();
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
