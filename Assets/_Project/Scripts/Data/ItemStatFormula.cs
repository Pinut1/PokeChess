using System.Collections.Generic;

/// <summary>
/// 장착 아이템 전체의 스탯 보너스를 계산하는 단일 계산 공식.
///
/// Combat/ItemStatEffect(전투 시작 1회, BattleUnit 대상)와 PokemonUnit(정보창 표시용 실시간 계산,
/// 쇼핑 단계)이 이 메서드 하나를 공유한다 — 전투 계산식과 정보창 계산식이 서로 달라지는 것을 막기
/// 위한 단일 소스. Data 계층에 둬서 Core(PokemonUnit)와 Combat(ItemStatEffect) 양쪽에서
/// 참조 방향 문제 없이 쓸 수 있게 했다.
///
/// ItemData의 *Pct/*Percent류는 0~100 정수 퍼센트로 저장됨(예: maxHpPct=18 → +18%) — /100 필요.
///
/// 아이템을 하나씩 순서대로 적용(누적)하지 않는다 — flat/percent가 섞인 아이템을 순차 적용하면
/// 장착 순서에 따라 최종 스탯이 달라지는 문제가 있어(2026-08 기획 확정), 스탯별로 전체 아이템의
/// flat 합계·percent 합계를 각각 먼저 구한 뒤 Final = (Base + SumFlat) × (1 + SumPercent) 공식을
/// 한 번만 적용한다. 이 방식은 아이템 리스트 순서와 결과가 무관하다.
/// </summary>
public static class ItemStatFormula
{
    public static void ApplyAll(
        IReadOnlyList<ItemData> items,
        ref float maxHp, ref float currentHp,
        ref float attack, ref float spellPower, ref float attackSpeed, ref float defense,
        ref float critChance, ref float critMultiplier,
        ref float manaRegenBonus,
        ref float skillDamageAmpPct,
        ref float damageAmpPct)
    {
        if (items == null || items.Count == 0) return;

        float sumHpFlat = 0f, sumHpPct = 0f;
        float sumAtkFlat = 0f, sumAtkPct = 0f;
        float sumApFlat = 0f, sumApPct = 0f;
        float sumAsPct = 0f;
        float sumDefFlat = 0f, sumDefPct = 0f;
        float sumCritChancePct = 0f, sumCritDmgPct = 0f;
        float sumManaRegen = 0f;
        float sumSkillDamageAmp = 0f, sumDamageAmp = 0f;

        foreach (ItemData item in items)
        {
            if (item == null) continue;

            sumHpFlat  += item.hpBonus;
            sumHpPct   += item.maxHpPct;
            sumAtkFlat += item.attackBonus;
            sumAtkPct  += item.attackPct;
            sumApFlat  += item.spellPowerBonus;
            sumApPct   += item.spAtkPct;
            sumAsPct   += item.attackSpeedBonus;
            // spDef 폐지(v9) — defenseBonus/spDefBonus 둘 다 단일 defense로 합산. (둘 다 flat 보너스, % 아님)
            sumDefFlat += item.defenseBonus + item.spDefBonus;
            sumDefPct  += item.defensePct;
            sumCritChancePct += item.criPct;
            sumCritDmgPct    += item.criDmgPct;
            sumManaRegen     += item.manaRegenBonus;
            sumSkillDamageAmp += item.skillDamageAmpPct;
            sumDamageAmp      += item.damageAmpPct;
        }

        float newMaxHp = (maxHp + sumHpFlat) * (1f + sumHpPct * 0.01f);
        currentHp += newMaxHp - maxHp; // 호출 시점 currentHp==maxHp 전제(전투 시작 풀피) — 늘어난 만큼 그대로 채움.
        maxHp = newMaxHp;

        attack      = (attack + sumAtkFlat) * (1f + sumAtkPct * 0.01f);
        spellPower  = (spellPower + sumApFlat) * (1f + sumApPct * 0.01f);
        attackSpeed = attackSpeed * (1f + sumAsPct * 0.01f);
        defense     = (defense + sumDefFlat) * (1f + sumDefPct * 0.01f);

        critChance     += sumCritChancePct * 0.01f;
        critMultiplier += sumCritDmgPct * 0.01f;

        // 초당 마나 충전량에 더할 flat 가산분(배수는 BattleManager.GainMana가 별도로 곱함).
        manaRegenBonus += sumManaRegen;

        // 상시(전투 시작 1회) 피해 증폭. 이후 전투 중 ItemConditionalEffect가 동적으로 가산하는 분
        // (초점렌즈/규살열매/왕의징표석 등)은 이 값 위에 += 로만 더해지므로 서로 덮어쓰지 않는다
        // (같은 계열은 가산 후 1회 곱 — BattleManager.ResolveDamage 참고).
        skillDamageAmpPct += sumSkillDamageAmp;
        damageAmpPct      += sumDamageAmp;
    }
}
