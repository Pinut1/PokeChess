/// <summary>
/// 파치리스 영웅증강: 모든 파치리스를 서포터→탱커로 전환하고 날따름(도발) 스킬을 부여.
/// 도발 로직(시전자 중심 r4, 지속 1.0×1.4×성급)은 BattleManager/BattleUnit에 구현 완료 — 여기선 주입만.
/// </summary>
public class PachirisuHeroAugment : HeroAugment
{
    // 날따름 마나비용 — PLACEHOLDER(기획 미확정). 스킬 스펙 자체는 기획 확정(2026-07-10): ENEMY_AREA r4.
    // 30 = 전투 시작 ~3초 시전(초당 마나 10). EeveeHeroDebugTest도 이 값을 공유한다.
    public const int TAUNT_MANA_COST = 30;

    protected override string SpeciesNameEn => "Pachirisu";

    protected override void Tag(PokemonUnit unit)
    {
        if (unit.HasGrantedSkill) return; // 이미 적용됨
        unit.ApplyParichisuHeroAugment(PokemonRole.Tanker, CreateTauntSkill(), TAUNT_MANA_COST);
    }

    /// <summary>날따름 스킬 정의(기획 확정 2026-07-10: ENEMY_AREA r4, 시전자 중심 타겟팅은 BattleManager 전용 처리).</summary>
    public static PokemonSkillData CreateTauntSkill() => new PokemonSkillData
    {
        skillId    = "PACHIRISU_TAUNT",
        skillName  = "날따름",
        effectType = SkillEffectType.Taunt,
        targetType = SkillTargetType.EnemyArea,
        areaRadius = 4,
        vfxId      = ""
    };
}
