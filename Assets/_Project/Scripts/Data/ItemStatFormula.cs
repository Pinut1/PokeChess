/// <summary>
/// 장착 아이템 1개의 스탯 보너스를 누적 적용하는 단일 계산 공식.
///
/// Combat/ItemStatEffect(전투 시작 1회, BattleUnit 대상)와 PokemonUnit(정보창 표시용 실시간 계산,
/// 쇼핑 단계)이 이 메서드 하나를 공유한다 — 전투 계산식과 정보창 계산식이 서로 달라지는 것을 막기
/// 위한 단일 소스. Data 계층에 둬서 Core(PokemonUnit)와 Combat(ItemStatEffect) 양쪽에서
/// 참조 방향 문제 없이 쓸 수 있게 했다.
///
/// ItemData의 *Pct/*Percent류는 0~100 정수 퍼센트로 저장됨(예: maxHpPct=18 → +18%) — /100 필요.
/// 여러 아이템을 순서대로 적용하면 뒤 아이템의 %보너스는 앞 아이템까지 반영된 값 기준으로 계산된다
/// (누적 방식 — 기존 ItemStatEffect 동작 그대로 유지, 전투 계산 결과를 바꾸지 않는다).
/// </summary>
public static class ItemStatFormula
{
    public static void Apply(
        ItemData item,
        ref float maxHp, ref float currentHp,
        ref float attack, ref float spellPower, ref float attackSpeed, ref float defense,
        ref float critChance, ref float critMultiplier)
    {
        if (item == null) return;

        float bonusHp = item.hpBonus + maxHp * (item.maxHpPct * 0.01f);
        maxHp += bonusHp;
        currentHp += bonusHp;

        attack      += item.attackBonus;
        spellPower  += spellPower * (item.spAtkPct * 0.01f);
        attackSpeed += attackSpeed * (item.attackSpeedBonus * 0.01f);

        // spDef 폐지(v9) — defenseBonus/spDefBonus 둘 다 단일 defense로 합산. (둘 다 flat 보너스, % 아님)
        defense += item.defenseBonus + item.spDefBonus;

        critChance     += item.criPct * 0.01f;
        critMultiplier += item.criDmgPct * 0.01f;
    }
}
