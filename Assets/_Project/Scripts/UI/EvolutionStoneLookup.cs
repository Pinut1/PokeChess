using System.Collections.Generic;

/// <summary>
/// 포켓몬 ↔ 진화의 돌 양방향 조회. 견본덱 창과 유닛 상세창이 같이 쓴다.
///
/// <see cref="EvolutionStoneData.mappings"/>는 "진화 전 → 진화 후" 한 방향으로만 적혀 있어
/// 묻는 방향에 따라 훑는 필드가 달라진다.
/// <list type="bullet">
/// <item><see cref="RequiredFor"/> — "이 유닛을 만들려면 무슨 돌이 필요한가"(진화 후 이름으로 역방향)</item>
/// <item><see cref="UsableBy"/> — "이 유닛이 지금 쓸 수 있는 돌은 무엇인가"(진화 전 이름으로 정방향)</item>
/// </list>
/// </summary>
public static class EvolutionStoneLookup
{
    /// <summary>
    /// 이 포켓몬이 <b>진화의 돌로 만들어지는 진화체</b>라면 그 돌. 아니면 null.
    ///
    /// 진화 후 폼은 돌 하나로 특정되지만(라이츄 ← 천둥의돌), 진화 전 폼은 여러 갈래라
    /// (이브이 4종·풀꽃 2종) 이 방향으로만 단일 답이 나온다.
    /// </summary>
    public static EvolutionStoneData RequiredFor(PokemonData pokemon)
    {
        foreach (EvolutionStoneData stone in All())
        {
            foreach (EvolutionMapping mapping in stone.mappings)
            {
                if (Matches(mapping?.evolvedPokemon, pokemon)) return stone;
            }
        }

        return null;
    }

    /// <summary>
    /// 이 포켓몬이 <b>지금 끼울 수 있는</b> 돌 전부. 여러 개일 수 있다(이브이 4종·풀꽃 2종).
    /// 해당 없으면 빈 목록.
    /// </summary>
    public static List<EvolutionStoneData> UsableBy(PokemonData pokemon)
    {
        var result = new List<EvolutionStoneData>();

        foreach (EvolutionStoneData stone in All())
        {
            foreach (EvolutionMapping mapping in stone.mappings)
            {
                if (!Matches(mapping?.targetPokemon, pokemon)) continue;

                result.Add(stone);
                break; // 같은 돌이 같은 종에 두 줄 걸려 있어도 한 번만
            }
        }

        return result;
    }

    private static IEnumerable<EvolutionStoneData> All()
    {
        EvolutionStoneDatabase db = EvolutionStoneDatabase.Instance;
        if (db == null || db.all == null) yield break;

        foreach (EvolutionStoneData stone in db.all)
            if (stone != null && stone.mappings != null) yield return stone;
    }

    /// <summary>시트 간 영문명 표기 차이를 감안해 대소문자 무관 비교한다.</summary>
    private static bool Matches(string nameEn, PokemonData pokemon)
    {
        return pokemon != null &&
               !string.IsNullOrWhiteSpace(nameEn) &&
               !string.IsNullOrWhiteSpace(pokemon.pokemonNameEn) &&
               string.Equals(nameEn, pokemon.pokemonNameEn, System.StringComparison.OrdinalIgnoreCase);
    }
}
