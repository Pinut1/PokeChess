/// <summary>
/// 장착템의 "평타스탯형" 보너스(공격/방어/공속/치명타 등)를 전투 시작 시 1회 self에 가산.
/// 조건부/반사/흡혈류(healTakenDmgPct, reflectPhysPct, defSpDefPerAttacker 등)는 매 틱 판정이 필요해
/// OnDealDamage/OnTakeDamage 훅을 쓰는 별도 효과로 후속 구현(기둥B 2단계).
/// </summary>
public class ItemStatEffect : ICombatEffect
{
    private readonly ItemData _item;

    public ItemStatEffect(ItemData item) => _item = item;

    public void OnCombatStart(BattleUnit self)
    {
        if (_item == null) return;

        float bonusHp = _item.hpBonus + self.maxHp * _item.maxHpPct;
        self.maxHp += bonusHp;
        self.currentHp += bonusHp;

        self.attack     += _item.attackBonus;
        self.spellPower += self.spellPower * _item.spAtkPct;
        self.attackSpeed += self.attackSpeed * _item.attackSpeedBonus;

        // spDef 폐지(v9) — defenseBonus/spDefBonus 둘 다 단일 defense로 합산.
        self.defense += _item.defenseBonus + _item.spDefBonus;

        self.critChance     += _item.criPct;
        self.critMultiplier  += _item.criDmgPct;
    }
}
