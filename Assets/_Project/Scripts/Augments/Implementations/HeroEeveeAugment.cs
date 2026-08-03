/// <summary>
/// HERO_EEVEE "기술머신:나인이볼부스트": 모든 이브이에 진화잠금 + 스탯 ×1.4 + 역할 → 마법사(v2 확정).
/// 3성 이브이가 보드에 있으면 전투 시작 시 진화체 봇 소환(현재 4마리 — BattleManager.HasHeroEeveeThreeStar).
/// TODO(별도 티켓): v2 나인이볼부스트 정식 스펙 — 진화체 8종 소환 + 종별 버프 전달, SK_EEVEE_HERO 스킬화,
///   진화의 돌 면역, 돌연변이 시너지 → 고유 시너지 전환, "가장 강한 이브이 1마리" 대상 선정.
/// </summary>
public class HeroEeveeAugment : HeroAugment
{
    private const float STAT_MULTIPLIER = 1.4f;

    protected override string SpeciesNameEn => "Eevee";
    protected override string OverriddenRole => PokemonRole.Magician;

    protected override void Tag(PokemonUnit unit)
    {
        if (unit.evolutionLocked) return; // 이미 적용됨(합체 생존자는 플래그 보존)
        unit.ApplyEeveeHeroAugment(STAT_MULTIPLIER, PokemonRole.Magician);
    }
}
