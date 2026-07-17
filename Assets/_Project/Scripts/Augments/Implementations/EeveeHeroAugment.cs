/// <summary>
/// 이브이 영웅증강: 모든 이브이에 진화잠금 + 스탯 ×1.4.
/// 3성 이브이가 보드에 있으면 전투 시작 시 돌연변이 봇 4마리 전원소환
/// (판정은 BattleManager.HasHeroEeveeThreeStar — evolutionLocked 플래그 기반 자동).
/// 전투/유닛 seam은 구현 완료(HANDOFF_2026-07-01_hero-augment-seam.md), 여기선 태그만.
/// </summary>
public class EeveeHeroAugment : HeroAugment
{
    private const float STAT_MULTIPLIER = 1.4f;

    protected override string SpeciesNameEn => "Eevee";

    protected override void Tag(PokemonUnit unit)
    {
        if (unit.evolutionLocked) return; // 이미 적용됨(합체 생존자는 플래그 보존)
        unit.ApplyEeveeHeroAugment(STAT_MULTIPLIER);
    }
}
