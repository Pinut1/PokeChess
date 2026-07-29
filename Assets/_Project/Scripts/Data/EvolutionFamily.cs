using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 진화 계열(한 개체의 성장 경로) 판정 — 시너지 카운트가 "고유 종"이 아니라
/// "고유 계열" 단위로 세도록 하는 단일 소스.
///
/// 프로젝트에 진화 루트가 세 갈래로 흩어져 있어 여기서 하나로 합친다.
///   ① 3합체(성급) 진화 — PokemonData.evolvesIntoEn
///   ② 진화의 돌         — EvolutionStoneDatabase
///   ③ 통신교환 진화     — TradeEvolutionData
/// 셋 다 "같은 개체의 성장"이므로 같은 계열로 본다. 이상해씨·이상해풀·이상해꽃을
/// 나란히 올려도 풀/독은 각각 1카운트다. (버프는 별개 — 보유 개체 전원이 받는다.)
///
/// 돌연변이(Mutant)는 예외로 종 단위 카운트를 쓴다. 그 분기는 시너지 데이터의
/// SynergyData.countPerSpecies가 담당하며, 이 클래스는 관여하지 않는다.
///
/// 매니저가 아닌 정적 유틸이라 "매니저끼리 직접 참조 금지" 규칙에 걸리지 않는다.
/// </summary>
public static class EvolutionFamily
{
    // 종 id → 계열 루트 종 id. Invalidate()까지 유지되는 지연 빌드 캐시.
    private static Dictionary<int, int> _rootById;

    // 사슬 순환 경고는 1회만(데이터가 꼬여도 매 프레임 로그를 쏟지 않도록).
    private static bool _warnedCycle;

    /// <summary>
    /// 이 종이 속한 진화 계열의 최초 종 id.
    /// 사슬 밖(상점에서 바로 사는 기본종)이면 자기 자신, DB가 없으면 자기 id로 폴백한다.
    /// </summary>
    public static int RootId(PokemonData data)
    {
        if (data == null) return 0;

        EnsureBuilt();
        return _rootById.TryGetValue(data.id, out int root) ? root : data.id;
    }

    /// <summary>
    /// 캐시 폐기. 진화 관련 데이터(Pokemon/EvolutionStone/TradeEvolution)를 다시 임포트하면
    /// 임포터가 호출한다. static이라 도메인 리로드로도 초기화되지만, 리로드 없이
    /// 임포트만 한 직후에는 이 호출이 없으면 옛 계열이 남는다.
    /// </summary>
    public static void Invalidate()
    {
        _rootById = null;
        _warnedCycle = false;
    }

    private static void EnsureBuilt()
    {
        if (_rootById != null) return;

        _rootById = new Dictionary<int, int>();

        var db = PokemonDatabase.Instance;
        if (db == null || db.all == null) return; // 폴백: 전원 자기 자신이 루트

        // 자식 영문명 → 부모 영문명. 먼저 등록된 부모가 이긴다(3합체 사슬 우선).
        var parentOf = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

        foreach (var p in db.all)
            if (p != null) Link(parentOf, p.evolvesIntoEn, p.pokemonNameEn);

        // 돌 1종이 여러 진화 매핑을 갖는다(불꽃의돌 → 이브이/가디/식스테일 …).
        var stones = EvolutionStoneDatabase.Instance;
        if (stones != null && stones.all != null)
            foreach (var s in stones.all)
            {
                if (s == null || s.mappings == null) continue;
                foreach (var m in s.mappings)
                    if (m != null) Link(parentOf, m.evolvedPokemon, m.targetPokemon);
            }

        var trades = TradeEvolutionData.Instance;
        if (trades != null && trades.mappings != null)
            foreach (var m in trades.mappings)
                if (m != null) Link(parentOf, m.evolvedPokemonEn, m.targetPokemonEn);

        foreach (var p in db.all)
            if (p != null) _rootById[p.id] = ResolveRootId(db, parentOf, p);
    }

    /// <summary>자식→부모 등록. 이미 부모가 있으면 유지(첫 등록 우선), 자기 참조는 무시.</summary>
    private static void Link(Dictionary<string, string> parentOf, string child, string parent)
    {
        if (string.IsNullOrEmpty(child) || string.IsNullOrEmpty(parent)) return;
        if (string.Equals(child, parent, System.StringComparison.OrdinalIgnoreCase)) return;

        if (!parentOf.ContainsKey(child)) parentOf[child] = parent;
    }

    /// <summary>부모 사슬을 루트까지 거슬러 올라가 종 id로 굳힌다. 순환은 방문 집합으로 끊는다.</summary>
    private static int ResolveRootId(PokemonDatabase db, Dictionary<string, string> parentOf, PokemonData data)
    {
        string current = data.pokemonNameEn;
        if (string.IsNullOrEmpty(current)) return data.id;

        var visited = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { current };

        while (parentOf.TryGetValue(current, out string parent))
        {
            if (!visited.Add(parent))
            {
                if (!_warnedCycle)
                {
                    _warnedCycle = true;
                    Debug.LogWarning(
                        $"[EvolutionFamily] 진화 사슬 순환 감지 — '{data.pokemonNameEn}' 계열의 '{current}'에서 중단. " +
                        "진화 데이터(evolvesInto/진화의돌/통신교환)에 서로를 가리키는 행이 있는지 확인하세요.");
                }
                break;
            }
            current = parent;
        }

        var rootData = db.GetByNameEn(current);
        return rootData != null ? rootData.id : data.id;
    }
}
