/// <summary>
/// HERO_PACHIRISU "기술머신:날따름": 모든 파치리스를 탱커로 전환 + 날따름(도발) 부여 + 스탯 ×1.4(v2 확정).
/// 도발 로직(시전자 중심 r4, 지속 1.0×1.4×성급)은 BattleManager/BattleUnit에 구현 완료 — 여기선 주입만.
/// 자뭉열매(v2)도 구현 완료: ApplyParichisuHeroAugment가 hasHeroBerry 설정 → BattleUnit이 전투당 1회
///   HP 45% 미만 시 언타겟+행동불능 + 매초 maxHP 15% 회복 후 복귀(지속 3s는 PLACEHOLDER).
/// TODO(별도 티켓): 투사체 끌어당김(수치 미확정), SK_PACHIRISU_HERO 스킬행(시트), "가장 강한 파치리스 1마리" 대상 선정.
/// </summary>
public class HeroPachirisuAugment : HeroAugment
{
    // 날따름 마나비용 — 기획 회신(2026-07-16): 우선 30 유지, 엑셀 밸런스 조정 후 확정값 재전달 예정.
    public const int TAUNT_MANA_COST = 30;

    private const float STAT_MULTIPLIER = 1.4f;

    protected override string SpeciesNameEn => "Pachirisu";

    protected override void Tag(PokemonUnit unit)
    {
        if (unit.HasGrantedSkill) return; // 이미 적용됨
        unit.ApplyParichisuHeroAugment(PokemonRole.Tanker, CreateTauntSkill(), TAUNT_MANA_COST, STAT_MULTIPLIER);
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
