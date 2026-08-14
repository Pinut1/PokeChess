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

    /// <summary>OnTakeDamage 단계에서 예약된, 피해가 실제로 적용된 "직후"에 넣어야 할 자가 회복량
    /// (기합의머리띠 healTakenDmgPct 등). 여기서 바로 target.currentHp를 올리면 아직 이번 피해가
    /// 반영되기 전이라 MaxHP 클램프에 막혀 회복분이 소실될 수 있어(2026-08 실측 확인 — 만피 상태에서
    /// 선회복 후 피해를 적용하면 회복분이 그대로 사라짐), ResolveDamage가 피해를 적용한 직후 별도로
    /// 처리한다. 0이면 예약된 회복 없음.</summary>
    public float pendingSelfHeal;

    /// <summary>보호막 흡수 직전(BattleManager.ResolveDamage가 AbsorbShieldDamage를 호출하기 전)의
    /// amount 스냅샷. "실제로 공격받았다"는 사실 자체를 트리거로 쓰는 효과(왕의징표석 gainStackOnTakeDamage
    /// 등)는 보호막이 전부 막아도 발동해야 하므로, 흡수로 줄어드는 amount 대신 이 값을 참조한다.
    /// DamageContext는 매 피해마다 새로 생성되므로(재사용/풀링 없음) 별도 초기화 없이 항상 신선한 값이다.</summary>
    public float preShieldAmount;

    /// <summary>이번 피해에서 BattleUnit.AbsorbShieldDamage가 정확히 0으로 소진시킨 추적 보호막 출처들.
    /// 항상 non-null(없으면 공유 빈 리스트) — DamageContext가 매 피해마다 새로 생성되므로 이전 피해의
    /// 값이 남아있을 수 없다. Apicot 등 "보호막 종료 시 후속 효과"가 이 값만 보고 정확히 1회 판정한다.</summary>
    public System.Collections.Generic.IReadOnlyList<ShieldSource> depletedShieldSources = System.Array.Empty<ShieldSource>();

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
