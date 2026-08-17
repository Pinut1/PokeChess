using System.Collections.Generic;

/// <summary>
/// 견본덱 표시용 <b>영웅증강 반영</b>. 증강을 실제로 고르지 않아도, 견본덱은 그 증강을 전제로 짠
/// 조합이므로 <b>바뀐 모습</b>(역할군·사거리)으로 보여줘야 한다.
///
/// 대상 덱은 <b>덱 이름으로</b> 가려낸다 — 기획이 덱 이름에 증강 이름을 그대로 넣어뒀다.
/// <list type="bullet">
/// <item>덱 2 「[기술머신:날따름] 전기 파치리스」 → 파치리스가 <b>탱커</b>가 되고 근접이 된다</item>
/// <item>덱 4 「[기술머신:나인이볼부스트] 물 이브이」 → 이브이가 <b>마법사</b>가 된다</item>
/// </list>
/// 덱 7에도 파치리스가 있지만 증강 덱이 아니라(「진화 스페셜리스트」) 원본 서포터 그대로 둔다.
///
/// 역할군은 <see cref="Augment.TryGetRoleOverride"/>를 그대로 쓴다 — 주석에 "상점 카드처럼 아직
/// 유닛이 없는 곳에서 쓴다"고 적힌, 정확히 이 용도의 API다. 표를 따로 만들면 증강이 바뀔 때 어긋난다.
///
/// 사거리는 바뀐 역할군에서 파생한다. 실제 게임도 그렇게 동작한다 —
/// <c>HeroPachirisuAugment.TANKER_ATTACK_RANGE = 1</c>("Tanker 역할은 전부 근접")이고
/// 평타 이펙트도 근접(<c>VFX_Electric_S</c>)으로 갈아끼운다.
/// </summary>
public static class SampleDeckHeroAugment
{
    /// <summary>견본덱이 보여줄 유닛의 실제 모습. 증강 덱이 아니면 원본 값이 그대로 들어온다.</summary>
    public readonly struct Profile
    {
        public readonly string role;
        public readonly int attackRange;

        /// <summary>증강으로 바뀐 모습인지. 설명창이 표시를 달리할 때 쓸 수 있다.</summary>
        public readonly bool augmented;

        public Profile(string role, int attackRange, bool augmented)
        {
            this.role = role;
            this.attackRange = attackRange;
            this.augmented = augmented;
        }
    }

    /// <summary>근접으로 보는 사거리. 규약상 _S(근거리)=1, _L(원거리)=4.</summary>
    private const int MELEE_RANGE = 1;
    private const int RANGED_RANGE = 4;

    // 덱 이름 → 그 덱에 걸린 영웅증강. 덱마다 매번 카탈로그를 훑지 않게 한 번만 찾는다.
    private static readonly Dictionary<string, Augment> _byDeckName = new();

    /// <summary>
    /// 이 덱에서 이 포켓몬이 실제로 어떤 모습인지. 증강 대상이 아니면 원본을 그대로 돌려준다.
    /// </summary>
    public static Profile ProfileOf(DeckData deck, PokemonData pokemon)
    {
        if (pokemon == null) return new Profile(null, RANGED_RANGE, false);

        Profile original = new(pokemon.role, pokemon.attackRange, false);

        if (deck == null || string.IsNullOrWhiteSpace(deck.deckName)) return original;

        Augment augment = AugmentOf(deck.deckName);
        if (augment == null) return original;

        if (!augment.TryGetRoleOverride(pokemon, out string role)) return original;

        return new Profile(role, RangeOf(role), true);
    }

    /// <summary>
    /// 덱 이름에 증강 이름이 들어 있으면 그 증강. 없으면 null(캐시에 null도 남긴다).
    ///
    /// ⚠️ <b>부분 문자열 매칭이라 근본적으로 취약하다.</b> 시트의 덱 이름이 바뀌면 조용히 안 걸린다.
    /// 제대로 고치려면 deck_data 시트에 증강 id 컬럼이 필요하다(기획 요청 대기).
    /// 그때까지의 방어는 둘이다.
    /// <list type="bullet">
    /// <item>여러 증강이 걸리면 <b>가장 긴 이름</b>을 고른다 — 한 이름이 다른 이름을 품고 있어도
    ///       (「나인이볼」 ⊂ 「나인이볼부스트」) 순회 순서에 결과가 좌우되지 않는다</item>
    /// <item>둘 이상 걸리면 경고를 남긴다 — 조용한 오작동만은 막는다</item>
    /// </list>
    /// </summary>
    private static Augment AugmentOf(string deckName)
    {
        if (_byDeckName.TryGetValue(deckName, out Augment cached)) return cached;

        AugmentData best = null;
        int matches = 0;

        foreach (AugmentData data in AugmentCatalog.All)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.augmentName)) continue;
            if (!deckName.Contains(data.augmentName)) continue;

            matches++;

            if (best == null || data.augmentName.Length > best.augmentName.Length)
                best = data;
        }

        if (matches > 1)
        {
            UnityEngine.Debug.LogWarning(
                $"[SampleDeckHeroAugment] 덱 이름 \"{deckName}\"에 증강 이름이 {matches}개 걸립니다 — " +
                $"가장 긴 \"{best.augmentName}\"을 씁니다. 덱 이름이나 증강 이름을 손봐야 합니다.");
        }

        Augment found = best != null ? AugmentFactory.Create(best) : null;

        _byDeckName[deckName] = found;
        return found;
    }

    /// <summary>
    /// 역할군에서 평타 사거리를 파생한다. 데이터 전수 확인 결과 예외가 없다 —
    /// 근접(Tanker·Warrior·Assassin) 80마리 전부 1, 원거리(Archer·Magician·Supporter) 60마리 전부 4.
    ///
    /// <b>이 판정의 단일 소스다.</b> <see cref="SampleDeckItemRules"/>도 여기를 부른다 —
    /// 양쪽에 같은 표를 두면 역할이 늘 때 한쪽만 고쳐져 배치도와 아이템 그룹이 어긋난다.
    /// </summary>
    public static int RangeOf(string role)
    {
        if (string.IsNullOrWhiteSpace(role)) return RANGED_RANGE;

        switch (role.ToUpperInvariant())
        {
            case "TANKER":
            case "WARRIOR":
            case "ASSASSIN":
                return MELEE_RANGE;

            default:
                return RANGED_RANGE;
        }
    }
}
