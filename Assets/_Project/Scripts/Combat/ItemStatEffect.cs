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

        // 실제 공식은 Data/ItemStatFormula(공용)에 있다 — 정보창(PokemonUnit.ComputeFinalStats)과
        // 같은 코드를 쓰므로 전투 계산과 정보창 표시가 어긋나지 않는다.
        ItemStatFormula.Apply(
            _item,
            ref self.maxHp, ref self.currentHp,
            ref self.attack, ref self.spellPower, ref self.attackSpeed, ref self.defense,
            ref self.critChance, ref self.critMultiplier);
    }
}
