using System.Collections.Generic;

/// <summary>
/// 장착 아이템 전체의 "평타스탯형" 보너스(공격/방어/공속/치명타 등)를 전투 시작 시 1회 self에 가산.
/// 유닛 1명당 인스턴스 1개만 만들어 장착 아이템 리스트 전체를 들고 있는다 — 아이템별로 하나씩
/// 순서대로 적용하면 flat/percent가 섞인 조합에서 장착 순서에 따라 최종 스탯이 달라지는 문제가
/// 있어(2026-08 기획 확정), ItemStatFormula.ApplyAll이 전체를 먼저 합산한 뒤 한 번만 계산한다.
/// 조건부/반사/흡혈류(healTakenDmgPct, reflectPhysPct, defSpDefPerAttacker 등)는 여전히 아이템별
/// 매틱 판정이 필요해 ItemConditionalEffect가 아이템 1개당 1개씩 별도로 부착된다(호출측 책임).
/// </summary>
public class ItemStatEffect : ICombatEffect
{
    private readonly IReadOnlyList<ItemData> _items;

    public ItemStatEffect(IReadOnlyList<ItemData> items) => _items = items;

    public void OnCombatStart(BattleUnit self)
    {
        if (_items == null || _items.Count == 0) return;

        // 실제 공식은 Data/ItemStatFormula(공용)에 있다 — 정보창(PokemonUnit.ComputeFinalStats)과
        // 같은 코드를 쓰므로 전투 계산과 정보창 표시가 어긋나지 않는다.
        ItemStatFormula.ApplyAll(
            _items,
            ref self.maxHp, ref self.currentHp,
            ref self.attack, ref self.spellPower, ref self.attackSpeed, ref self.defense,
            ref self.critChance, ref self.critMultiplier,
            ref self.manaRegenBonus,
            ref self.skillDamageAmpPct,
            ref self.damageAmpPct);
    }
}
