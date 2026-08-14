/// <summary>
/// 전투 틱의 훅 지점에 꽂히는 효과(아이템/스킬/시너지 공통 진입점, 기둥B).
/// BattleUnit.effects에 담겨 BattleManager가 각 단계에서 순회 호출한다.
/// </summary>
public interface ICombatEffect
{
    /// <summary>전투 시작 직후(스탯 적용 시점) 1회. 평타스탯형 아이템(공격력+, 치명타% 등)이 여기서 self를 직접 가공.</summary>
    void OnCombatStart(BattleUnit self) { }

    /// <summary>self가 살아있는 동안 매 틱. 도트힐/매틱 재계산형 효과(hpRegenPercent 등).</summary>
    void OnTick(BattleUnit self, float deltaTime) { }

    /// <summary>self가 가해자일 때 데미지 적용 전. ctx.amount/type 가공(관통, 추가데미지 등).</summary>
    void OnDealDamage(BattleUnit self, DamageContext ctx) { }

    /// <summary>self가 피격자일 때 경감 이후, 적용 전. 보호막 흡수·반사 등.</summary>
    void OnTakeDamage(BattleUnit self, DamageContext ctx) { }

    /// <summary>self가 평타를 가했을 때(스킬 제외). 평타 전용 효과(흡혈 등).</summary>
    void OnBasicAttack(BattleUnit self, BattleUnit target) { }

    /// <summary>self가 스킬을 시전했을 때.</summary>
    void OnSkillCast(BattleUnit self) { }

    /// <summary>self가 가해자이고 victim이 이 피해로 사망했을 때.</summary>
    void OnKill(BattleUnit self, BattleUnit victim) { }

    /// <summary>self가 가해자일 때, 피해가 실제로 target의 HP에 반영된 직후(증폭·크리·경감·보호막
    /// 흡수까지 전부 끝난 뒤). actualHpDamage는 target의 currentHp가 실제로 줄어든 양(오버킬 제외,
    /// 보호막이 막은 만큼 제외) — ctx.amount와 다를 수 있으므로 이 값만 신뢰할 것(큰뿌리 등).</summary>
    void OnDealDamageResolved(BattleUnit self, BattleUnit target, float actualHpDamage) { }
}
