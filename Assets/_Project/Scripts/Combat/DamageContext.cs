/// <summary>
/// 데미지 타입. 경감은 항상 defense 하나(기획)지만, 아이템·효과 조건 표현을 위해 타입을 구분한다.
/// (예: "스킬 크리만", "물리 관통", True=경감 무시 고정딜)
/// </summary>
public enum DamageType
{
    Physical,  // 평타, ATTACK effectType 스킬
    Magic,     // SPELL effectType 스킬 (spellPower 기반)
    True       // 경감 무시 고정 데미지
}

/// <summary>
/// 한 번의 피해를 파이프라인 단계로 전달하는 가변 컨텍스트.
/// BattleManager.ResolveDamage가 단계별로 amount를 가공한다:
///   공격자 효과(OnDealDamage) → 크리 → 경감 → 피격자 효과(OnTakeDamage) → 적용 → OnKill
/// 효과(ICombatEffect, 기둥B)가 이 컨텍스트를 읽고/수정해 "X일때 Y"를 구현한다.
/// </summary>
public class DamageContext
{
    public BattleUnit source;
    public BattleUnit target;

    public float amount;          // 단계마다 가공되는 피해량 (시작 = 기본 위력)
    public DamageType type = DamageType.Physical;

    public bool isBasicAttack;    // true=평타, false=스킬
    public bool isCrit;           // 크리 발생 여부(효과가 참조 — 보석건틀릿 등). 현재 결정론 모델에선 미사용.

    public DamageContext(BattleUnit source, BattleUnit target, float amount,
                         DamageType type, bool isBasicAttack)
    {
        this.source = source;
        this.target = target;
        this.amount = amount;
        this.type = type;
        this.isBasicAttack = isBasicAttack;
    }
}
