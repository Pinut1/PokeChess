/// <summary>
/// HERO_EEVEE "기술머신:나인이볼부스트".
///   고정 효과 — 보유한 <b>모든</b> 이브이(보드+벤치)에 진화잠금.
///   이동 효과 — 보드에서 <b>가장 강한 이브이 1마리</b>에 스탯 ×1.4 + 역할 → 마법사(v2 확정).
/// 선정·재선정 규칙은 <see cref="HeroAugment"/> 주석 참고(기획 확정 2026-08-18).
///
/// 잠금만 전 개체 고정인 이유: 잠금이 대상을 따라 움직이면 "이브이인 채로 3성이 된 개체의 잠금이
/// 풀리는" 상태가 생기는데 정상 경로로 만들 수 없는 상태다.
///
/// <b>전투 효과 — 나인이볼부스트</b>(v2 구현 완료 2026-08-19): 이동 효과를 받고 있는 이브이가
/// <b>스킬을 1회 시전할 때마다 진화체 1종을 순서대로 봇 소환</b>하고, 그 종에 대응하는 버프를
/// 이브이 자신에게 건다(봇이 아니라 이브이가 받는다). 8종 전부 스킬 시전으로만 나오며, 성급별
/// 마나코스트(80/60/30)가 몇 종까지 닿는지를 가른다 — 3성만 8종을 다 채운다.
/// 순서·수치·마나는 <see cref="HeroEeveeBoostTable"/>, 전투 로직은 BattleManager.TryCastHeroEeveeBoost.
/// 소환 주체 판정이 <c>heroStatMultiplier &gt; 1</c>(=이동 효과 보유)인 것도 위 전제 위에 있다 —
/// 잠금은 여러 마리가 갖지만 배수는 "가장 강한 1마리"만 갖는다.
///
/// <b>진화의 돌 면역</b>(기획 확정 2026-08-19) — 잠긴 이브이는 진화의 돌을 받지 않는다
/// (<see cref="PokemonUnit.TryEquipStone"/>). 잠금은 합체로 종이 바뀌는 것만 막을 뿐이라, 이게 없으면
/// 돌로 샤미드·쥬피썬더가 되어 증강이 통째로 무의미해진다.
/// 같은 결정으로 <b>돌연변이 시너지도 발동하지 않는다</b>(SynergyManager.SuppressMutantIfHeroEevee) —
/// 이브이를 한 마리에 몰아주는 설계와 진화체를 여러 종 모으는 시너지는 애초에 양립하지 않는다.
///
/// TODO(별도 티켓): skill_table에 `SK_EEVEE_HERO` 행 추가(시트). 지금은 이브이 원본 스킬(Celebrate,
/// 마나 60)의 시전 타이밍만 빌려 쓰고 효과는 코드가 통째로 대체한다 — 전용 VFX는 필요 없다
/// (기획 확정 2026-08-19: 소환되는 진화체 자체가 연출).
/// </summary>
public class HeroEeveeAugment : HeroAugment
{
    private const float STAT_MULTIPLIER = 1.4f;

    protected override string SpeciesNameEn => "Eevee";
    protected override string OverriddenRole => PokemonRole.Magician;

    /// <summary>고정 효과 — 진화잠금. 되돌리지 않는다.</summary>
    protected override void ApplyFixed(PokemonUnit unit) => unit.ApplyEeveeHeroLock();

    protected override void ApplyMobile(PokemonUnit unit)
        => unit.ApplyEeveeHeroAugment(STAT_MULTIPLIER, PokemonRole.Magician);

    protected override void RemoveMobile(PokemonUnit unit) => unit.RemoveEeveeHeroAugment();

    /// <summary>
    /// <b>진화의 돌로 진화시킨 이브이를 한 마리라도 갖고 있으면 오퍼에 뜨지 않는다</b>
    /// (기획 확정 2026-08-19).
    ///
    /// 이 증강은 이브이를 <b>이브이인 채로</b> 한 마리에 몰아주는 설계다. 이미 돌로 샤미드·쥬피썬더
    /// 같은 진화체를 만들어 둔 플레이어에게는, 고른 순간 그 투자분이 증강 대상에서 통째로 빠진다 —
    /// 진화잠금은 앞으로의 진화만 막을 뿐 이미 진화한 개체를 되돌리지 못하기 때문이다.
    /// 고를 수는 있는데 고르면 손해인 선택지는 오퍼에서 빼는 편이 낫다.
    ///
    /// 판정 기준을 "돌 진화"로 잡은 이유 — 돌은 빼기 전까지 유지되는 <b>안정된 상태</b>라 오퍼 시점의
    /// 스냅샷으로 판단해도 뒤집히지 않는다. 반면 돌연변이 시너지 활성 여부는 유닛을 한 마리
    /// 내렸다 올리는 것만으로 바뀌어, 오퍼 필터 기준으로 쓰면 "왜 이번엔 안 뜨지"가 된다.
    ///
    /// ⚠️ 이 필터는 <b>앞문</b>만 막는다. 증강을 고른 뒤에 파트너가 통신교환으로 진화체를 넘겨주는
    /// 경로는 그대로 남으므로, 돌연변이 억제(SynergyManager.SuppressMutantIfHeroEevee)와 돌 면역
    /// (PokemonUnit.TryEquipStone)은 여전히 필요하다. 셋은 서로 대체재가 아니다.
    /// </summary>
    public override bool CanBeOffered() => !HasStoneEvolvedEevee();

    /// <summary>
    /// 보유분(보드+벤치) 중 "돌로 진화한 이브이"가 있는지.
    /// 돌을 끼면 data가 진화체로 바뀌고 원래 종이 preStoneData에 남으므로, 그 둘로 판정한다
    /// (진화체를 상점·통신교환으로 직접 얻은 경우는 preStoneData가 없어 여기 걸리지 않는다 — 의도한 범위).
    /// </summary>
    private static bool HasStoneEvolvedEevee()
    {
        if (!GameManager.TryGet(out var gm) || gm.Board == null) return false;

        foreach (var unit in gm.Board.GetUnitsOnBoard())
            if (IsStoneEvolvedEevee(unit)) return true;

        foreach (var unit in gm.Board.GetUnitsInBench())
            if (IsStoneEvolvedEevee(unit)) return true;

        return false;
    }

    private static bool IsStoneEvolvedEevee(PokemonUnit unit)
        => unit != null && unit.equippedStone != null && unit.preStoneData != null &&
           string.Equals(unit.preStoneData.pokemonNameEn, "Eevee",
                         System.StringComparison.OrdinalIgnoreCase);
}
