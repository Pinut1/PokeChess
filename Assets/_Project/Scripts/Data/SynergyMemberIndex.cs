using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "이 시너지 효과를 받는 포켓몬 목록" 인덱스 — 시너지 툴팁의 유닛 아이콘 줄이 쓰는 단일 소스.
///
/// 묶는 단위를 <b>SynergyManager의 카운트 키와 똑같이</b> 맞춘 것이 핵심이다.
/// 매니저는 진화 계열 루트(EvolutionFamily.RootId)로 세고, 돌연변이처럼 countPerSpecies가
/// 켜진 시너지만 종 id로 센다. 툴팁도 같은 키로 한 칸을 만들기 때문에 "아이콘 칸 수 = 최대 카운트"가
/// 되고, 이상해씨·이상해풀·이상해꽃이 3칸으로 늘어나 실제 카운트(1)와 어긋나는 일이 없다.
///
/// 대표 포켓몬은 <b>그 시너지를 실제로 들고 있는</b> 멤버 중 코스트 → 진화단계 → 도감번호 순으로 고른다.
/// 계열 루트를 그대로 쓰면 안 되는 이유: 캐터피에는 비행이 없고 버터플만 비행이라
/// 루트를 쓰면 비행 툴팁에 캐터피가 뜬다. 같은 상황이 3건 더 있다 —
/// 별가사리→아쿠스타(정령), 롱스톤→강철톤·스라크→핫삼(파괴).
///
/// 매니저가 아닌 정적 유틸이라 "매니저끼리 직접 참조 금지" 규칙에 걸리지 않는다.
/// 캐시는 EvolutionFamily와 같은 시점(임포트 직후)에 Invalidate()로 버린다.
/// </summary>
public static class SynergyMemberIndex
{
    /// <summary>툴팁 아이콘 한 칸.</summary>
    public class Member
    {
        /// <summary>아이콘·코스트에 쓸 대표 포켓몬.</summary>
        public PokemonData data;

        /// <summary>SynergyManager와 같은 카운트 키(계열 루트 id, countPerSpecies면 종 id).</summary>
        public int countKey;
    }

    private static readonly List<Member> Empty = new();

    // 시너지 id → 표시 순서로 정렬된 멤버들. Invalidate()까지 유지되는 지연 빌드 캐시.
    private static Dictionary<int, List<Member>> _bySynergyId;

    /// <summary>이 시너지 효과를 받는 포켓몬들(코스트 오름차순). 없으면 빈 목록.</summary>
    public static IReadOnlyList<Member> MembersOf(SynergyData synergy)
    {
        if (synergy == null) return Empty;

        EnsureBuilt();
        return _bySynergyId.TryGetValue(synergy.id, out var members) ? members : Empty;
    }

    /// <summary>
    /// 캐시 폐기. 포켓몬/시너지/진화 데이터를 다시 임포트하면 임포터가 호출한다.
    /// (EvolutionFamily.Invalidate와 같은 자리에서 함께 부른다 — 계열이 바뀌면 카운트 키도 바뀌므로)
    /// </summary>
    public static void Invalidate() => _bySynergyId = null;

    private static void EnsureBuilt()
    {
        if (_bySynergyId != null) return;

        _bySynergyId = new Dictionary<int, List<Member>>();

        var pokemonDb = PokemonDatabase.Instance;
        var synergyDb = SynergyDatabase.Instance;
        if (pokemonDb == null || pokemonDb.all == null || synergyDb == null) return;

        // 시너지 id → (카운트 키 → 대표 후보). 같은 키에 여러 멤버가 걸리면 더 좋은 쪽만 남긴다.
        var picked = new Dictionary<int, Dictionary<int, PokemonData>>();

        foreach (var pokemon in pokemonDb.all)
        {
            if (pokemon == null || pokemon.synergies == null) continue;

            // 적(봇) 전용은 뺀다 — 잉어킹(obtainBy=wild)은 플레이어가 얻을 수 없으니
            // "이 시너지를 채울 수 있는 유닛" 목록에 있으면 안 된다.
            // 상점에 안 나올 뿐인 진화체(evolution/stone/trade/synergy)는 그대로 남는다.
            if (!pokemon.IsPlayerObtainable) continue;

            foreach (var key in pokemon.synergies)
            {
                var synergy = synergyDb.GetByKey(key);
                if (synergy == null) continue; // 오타/미등록은 SynergyManager가 경고를 남기므로 여기선 조용히 무시

                if (!picked.TryGetValue(synergy.id, out var byCountKey))
                    picked[synergy.id] = byCountKey = new Dictionary<int, PokemonData>();

                int countKey = synergy.countPerSpecies ? pokemon.id : EvolutionFamily.RootId(pokemon);

                if (!byCountKey.TryGetValue(countKey, out var current) || IsBetterRepresentative(pokemon, current))
                    byCountKey[countKey] = pokemon;
            }
        }

        foreach (var pair in picked)
        {
            var members = new List<Member>(pair.Value.Count);
            foreach (var entry in pair.Value)
                members.Add(new Member { data = entry.Value, countKey = entry.Key });

            members.Sort(CompareForDisplay);
            _bySynergyId[pair.Key] = members;
        }
    }

    /// <summary>
    /// 같은 계열에서 대표로 세울 포켓몬 고르기. 코스트가 낮은 쪽 → 진화 단계가 이른 쪽 → 도감번호 순.
    /// (PokemonData.range는 사거리가 아니라 진화 단계다 — 시트 컬럼명 탓에 헷갈리기 쉬움)
    /// </summary>
    private static bool IsBetterRepresentative(PokemonData candidate, PokemonData current)
    {
        if (current == null) return true;
        if (candidate.cost  != current.cost)  return candidate.cost  < current.cost;
        if (candidate.range != current.range) return candidate.range < current.range;
        return candidate.id < current.id;
    }

    /// <summary>표시 순서: 코스트 오름차순 → 진화 단계 → 도감번호(같은 코스트끼리 순서가 흔들리지 않게).</summary>
    private static int CompareForDisplay(Member a, Member b)
    {
        if (a.data.cost  != b.data.cost)  return a.data.cost.CompareTo(b.data.cost);
        if (a.data.range != b.data.range) return a.data.range.CompareTo(b.data.range);
        return a.data.id.CompareTo(b.data.id);
    }
}
