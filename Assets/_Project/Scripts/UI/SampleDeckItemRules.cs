using System.Collections.Generic;

/// <summary>
/// 견본덱 추천 아이템 배정 규칙 — 기획 설계 문서(§7 일반 아이템 ↔ 열매/도구)의 6그룹을 그대로 옮긴 표.
///
/// <b>역할군이 곧 그룹이다.</b> 설계 분류와 실제 데이터가 1:1로 맞는 것을 확인했다 —
/// 평타 근접/원거리가 전방/후방과, 스킬 AS_BUFF/SPELL이 AD/AP와 정확히 갈린다.
/// <list type="bullet">
/// <item>Tanker(근접) → 탱커</item>
/// <item>Warrior(근접·AS_BUFF) → 전방 AD 딜러</item>
/// <item>Assassin(근접·SPELL) → 전방 AP 딜러 — 이름과 달리 26마리 전부 근접이다</item>
/// <item>Archer(원거리·AS_BUFF) → 후방 AD 딜러</item>
/// <item>Magician(원거리·SPELL) → 후방 AP 딜러</item>
/// <item>Supporter(원거리) → 서포터</item>
/// </list>
///
/// <b>아이템은 자기 그룹 밖으로 나가지 않는다.</b> 화면에서의 쓰임은 두 가지다.
/// <list type="number">
/// <item>유닛 설명창 — 그룹 <b>전체</b>를 보여준다("이 역할군이 쓸 만한 것들")</item>
/// <item>배치도·목록의 2칸 — 그룹에서 <b>무작위 2개</b>. 진화의 돌이 있으면 1칸을 돌이 먹는다</item>
/// </list>
///
/// 영문명으로 적어둔 이유는 <see cref="ItemDatabase.GetByNameEn"/>이 영문명 키만 등록하기 때문이고,
/// 한글명은 시트에서 바뀔 여지가 있어서다.
/// </summary>
public static class SampleDeckItemRules
{
    // 설계 그룹. 한글명은 주석으로만 남긴다(조회 키는 영문명).
    private static readonly string[] TANKER =
    {
        "Maranga Berry",   // 티라프열매 — 나를 공격 중인 적 1명당 DEF·내구 +10
        "Sitrus Berry",    // 자뭉열매 — HP 500 + 최대체력 18%
        "Leftovers",       // 먹다남은음식 — 매초 잃은 체력 2% 회복
    };

    private static readonly string[] FRONT_AD =
    {
        "Shell Bell",      // 조개껍질방울 — HP 40%에서 보호막
        "Micle Berry",     // 의문열매 — HP 60%에서 보호막, AD 40%
        "Liechi Berry",    // 치리열매 — AD 55%, 피해증폭
        "King's Rock",     // 왕의징표석 — 공격·피격 스택, 최대 시 CC 면역
    };

    private static readonly string[] FRONT_AP =
    {
        "Focus Band",      // 기합의머리띠 — 주문력·흡혈, HP 50% 기준 반전
        "Leek",            // 대파 — AP 35, 치명타 35%, 정밀
        "Apicot Berry",    // 규살열매 — 전투시작 보호막, 소멸 시 주문력 +25%
    };

    private static readonly string[] BACK_AD =
    {
        "Salac Berry",     // 캄라열매 — 매초 공속 7% 누적
        "Quick Claw",      // 선제공격손톱 — 평타 스택, 15스택 시 공속 +15%
        "Lansat Berry",    // 랑사열매 — AD 35%, 치명타 35%, 정밀
    };

    private static readonly string[] BACK_AP =
    {
        "Poké Doll",       // 삐삐인형 — 주문력 획득량 +10%, 마나 +5
        "Petaya Berry",    // 야타비열매 — AP 50, 피해증폭 15%
        "Scope Lens",      // 초점렌즈 — 5초마다 주문력 20% 누적
    };

    private static readonly string[] SUPPORT =
    {
        "Big Root",        // 큰뿌리 — 입힌 피해 20%로 최저 HP 아군 회복
        "Choice Band",     // 구애머리띠 — 2칸 내 같은 열 아군 공속 +30%
        "Razz Berry",      // 라즈열매 — 2칸 내 같은 열 아군 주문력 +25, 마나 +10
    };

    /// <summary>어느 그룹에도 해당하지 않을 때. 엉뚱한 아이템을 보여주느니 아무것도 안 보여준다.</summary>
    private static readonly string[] NONE = System.Array.Empty<string>();

    /// <summary>
    /// <b>영웅증강을 받은 특정 종에만 걸리는 고정 추천.</b> 역할군 그룹에서 무작위로 뽑는 대신
    /// 여기 적힌 순서를 그대로 쓴다. 설명창에서도 이 둘이 맨 앞에 오고 나머지 그룹이 뒤에 붙는다.
    ///
    /// 증강을 받았을 때만 적용한다 — 증강 없는 파치리스(덱 7)는 그냥 서포터라 해당 없다.
    /// 고정 아이템도 <b>그 역할군 그룹 안에서만</b> 고른다(설계 그룹 엄수).
    /// </summary>
    private static readonly Dictionary<string, string[]> AUGMENTED_PINS = new()
    {
        // 기술머신:날따름 파치리스 — 도발을 쓰는 유일한 탱커라 적의 공격 대상이 되는 일이 많다.
        // 티라프(나를 공격 중인 적 1명당 DEF↑)와 자뭉(HP)의 효율이 특히 높다(해인 지정).
        ["Pachirisu"] = new[]
        {
            "Sitrus Berry",   // 자뭉열매
            "Maranga Berry",  // 티라프열매
        },

        // 기술머신:나인이볼부스트 이브이 — 마법사로 전환된다(해인 지정).
        // 삐삐인형은 주문력 획득량 +10%라 다른 AP와 곱해지고, 초점렌즈는 5초마다 주문력 20%를
        // 상한 없이 누적한다 — 둘 다 전투가 길어질수록 커지는 쪽이다.
        ["Eevee"] = new[]
        {
            "Poké Doll",      // 삐삐인형
            "Scope Lens",     // 초점렌즈
        },
    };

    // 이름 → 실제 ItemData. 조회가 잦아 한 번 찾은 것은 들고 있는다.
    private static readonly Dictionary<string, List<ItemData>> _resolved = new();

    /// <summary>
    /// 이 역할군 그룹에 속한 아이템 전부. 설명창이 이 목록을 그대로 보여준다.
    /// 인자는 <b>증강이 반영된</b> 값이어야 한다(<see cref="SampleDeckHeroAugment.ProfileOf"/>) —
    /// 영웅증강 덱의 파치리스는 서포터가 아니라 탱커 아이템을 받아야 한다.
    /// ItemDatabase에서 못 찾은 이름은 조용히 건너뛴다(시트 영문명 표기 차이 대비).
    /// </summary>
    public static IReadOnlyList<ItemData> GroupOf(string role, int attackRange)
    {
        string[] names = NamesFor(role, attackRange);
        return names.Length == 0 ? System.Array.Empty<ItemData>() : Resolve(names[0], names);
    }

    /// <summary>
    /// 역할군만으로 그룹을 찾는다. 사거리를 모르는 곳(역할군 설명창 등)에서 쓴다 —
    /// 데이터 전수 확인 결과 역할군이 근접/원거리를 이미 결정하므로 결과가 같다
    /// (근접: Tanker·Warrior·Assassin / 원거리: Archer·Magician·Supporter).
    /// </summary>
    public static IReadOnlyList<ItemData> GroupOf(string role)
        => GroupOf(role, SampleDeckHeroAugment.RangeOf(role));

    /// <summary>이름 목록을 실제 ItemData로 바꾼다. 못 찾은 이름은 조용히 건너뛰고, 결과는 캐시한다.</summary>
    private static List<ItemData> Resolve(string cacheKey, string[] names)
    {
        if (_resolved.TryGetValue(cacheKey, out List<ItemData> cached)) return cached;

        var result = new List<ItemData>();
        ItemDatabase db = ItemDatabase.Instance;

        if (db != null)
        {
            for (int i = 0; i < names.Length; i++)
            {
                ItemData item = db.GetByNameEn(names[i]);
                if (item != null) result.Add(item);
            }
        }

        _resolved[cacheKey] = result;
        return result;
    }

    /// <summary>
    /// 이 유닛에 <b>고정 지정된</b> 아이템(순서 유지). 해당 없으면 빈 목록.
    /// 카드의 2칸은 이걸 먼저 채우고, 남으면 그룹에서 무작위로 보충한다.
    /// </summary>
    public static IReadOnlyList<ItemData> PinnedOf(PokemonData pokemon, bool augmented)
    {
        if (!augmented || pokemon == null || string.IsNullOrWhiteSpace(pokemon.pokemonNameEn))
            return System.Array.Empty<ItemData>();

        if (!AUGMENTED_PINS.TryGetValue(pokemon.pokemonNameEn, out string[] names))
            return System.Array.Empty<ItemData>();

        return Resolve($"pin:{pokemon.pokemonNameEn}", names);
    }

    /// <summary>
    /// 설명창에 보여줄 순서대로 정렬한 그룹 — <b>고정 지정이 앞, 나머지 그룹이 뒤</b>.
    /// 고정이 없으면 그룹 순서 그대로다.
    /// </summary>
    public static List<ItemData> OrderedGroupOf(
        string role, int attackRange, PokemonData pokemon, bool augmented)
    {
        var result = new List<ItemData>(PinnedOf(pokemon, augmented));

        foreach (ItemData item in GroupOf(role, attackRange))
            if (item != null && !result.Contains(item)) result.Add(item);

        return result;
    }

    /// <summary>임포터를 다시 돌려 ItemDatabase가 바뀌었을 때 캐시를 비운다.</summary>
    public static void InvalidateCache() => _resolved.Clear();

    private static string[] NamesFor(string role, int attackRange)
    {
        if (string.IsNullOrWhiteSpace(role)) return NONE;

        // 근접/원거리로 전방·후방을 가른다 — 역할군 이름만 보면 암살자를 후방으로 착각한다.
        bool front = attackRange <= 1;

        switch (role.ToUpperInvariant())
        {
            case "TANKER":    return TANKER;
            case "WARRIOR":   return FRONT_AD;
            case "ASSASSIN":  return front ? FRONT_AP : BACK_AP;
            case "ARCHER":    return BACK_AD;
            case "MAGICIAN":  return front ? FRONT_AP : BACK_AP;
            case "SUPPORTER": return SUPPORT;

            // 역할군이 아닌 문자열(안내 툴팁의 "옵션" 등)이 들어올 수 있다 —
            // 그때 탱커 아이템을 보여주면 완전히 엉뚱한 화면이 된다.
            default:          return NONE;
        }
    }
}
