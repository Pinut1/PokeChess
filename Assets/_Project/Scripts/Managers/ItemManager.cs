using UnityEngine;

/// <summary>
/// 아이템/진화의 돌 장착·해제 및 전투 스냅샷 스탯 반영 담당. 김태욱 파트.
/// </summary>
public class ItemManager : MonoBehaviour
{
    // TODO(태욱): 인벤토리 보관 — 획득(reward/아이템샵) 시 추가, 장착 성공 시 제거.
    // private readonly List<ItemData> _items = new();
    // private readonly List<EvolutionStoneData> _stones = new();

    /// <summary>
    /// 장착 단일 입구. 드래그&드롭 UI가 인벤토리 항목을 유닛에 드롭할 때 호출한다.
    /// 돌/일반 아이템을 타입으로 분기해 PokemonUnit 저수준 장착 로직으로 라우팅한다.
    /// 슬롯 규칙은 PokemonUnit이 처리한다.
    /// </summary>
    public bool EquipToUnit(ScriptableObject equippable, PokemonUnit unit)
    {
        if (unit == null || equippable == null)
            return false;

        switch (equippable)
        {
            case EvolutionStoneData stone:
                return unit.TryEquipStone(stone);

            case ItemData item:
                return TryEquipItem(unit, item);

            default:
                Debug.LogWarning($"[ItemManager] 장착 불가 타입: {equippable.GetType().Name}");
                return false;
        }
    }

    /// <summary>
    /// 일반 장착 아이템을 유닛에 장착한다.
    /// 실제 슬롯 보관은 PokemonUnit.items가 담당한다.
    /// </summary>
    public static bool TryEquipItem(PokemonUnit unit, ItemData item)
    {
        if (unit == null)
        {
            Debug.LogWarning("[ItemManager] 장착 실패: 대상 유닛이 없습니다.");
            return false;
        }

        if (item == null)
        {
            Debug.LogWarning("[ItemManager] 장착 실패: 아이템 데이터가 없습니다.");
            return false;
        }

        bool success = unit.TryEquipItem(item);

        if (!success)
        {
            Debug.LogWarning($"[ItemManager] 장착 실패: 슬롯 부족 또는 잘못된 아이템 ({item.itemNameEn})");
            return false;
        }

        Debug.Log($"[ItemManager] {unit.data?.pokemonName ?? "Unknown"} 에게 {item.itemName} 장착");
        return true;
    }

    /// <summary>
    /// 일반 장착 아이템을 유닛에서 제거한다.
    /// 제거된 아이템의 인벤토리 복귀는 호출측에서 처리한다.
    /// </summary>
    public static ItemData RemoveItem(PokemonUnit unit, ItemData item)
    {
        if (unit == null || item == null)
            return null;

        ItemData removed = unit.RemoveItem(item);

        if (removed == null)
        {
            Debug.LogWarning($"[ItemManager] 제거 실패: 유닛이 해당 아이템을 장착 중이 아닙니다. ({item.itemNameEn})");
            return null;
        }

        Debug.Log($"[ItemManager] {unit.data?.pokemonName ?? "Unknown"} 에서 {item.itemName} 제거");
        return removed;
    }

    /// <summary>
    /// 장착 해제 단일 입구. 돌은 베이스 종으로 원복하고, 일반 아이템은 제거한다.
    /// 반환물의 인벤토리 복귀는 호출측에서 처리한다.
    /// </summary>
    public ScriptableObject UnequipFromUnit(ScriptableObject equippable, PokemonUnit unit)
    {
        if (unit == null || equippable == null)
            return null;

        switch (equippable)
        {
            case EvolutionStoneData:
                return unit.RemoveStone();

            case ItemData item:
                return RemoveItem(unit, item);

            default:
                Debug.LogWarning($"[ItemManager] 해제 불가 타입: {equippable.GetType().Name}");
                return null;
        }
    }

    /// <summary>
    /// 기존 진화의 돌 제거 호출부 호환용.
    /// </summary>
    public ScriptableObject UnequipStone(PokemonUnit unit)
        => unit != null ? unit.RemoveStone() : null;

    /// <summary>
    /// PokemonUnit에 장착된 일반 아이템들의 스탯을 전투용 BattleUnit 스냅샷에 반영한다.
    /// PokemonUnit 원본 스탯은 변경하지 않는다.
    /// </summary>
    public static void ApplyItemStats(PokemonUnit unit, BattleUnit battleUnit)
    {
        if (unit == null || battleUnit == null)
            return;

        if (unit.items == null || unit.items.Count == 0)
            return;

        float hpFlat = 0f;
        float maxHpPct = 0f;

        float attackFlat = 0f;
        float spellPowerPct = 0f;
        float attackSpeedPct = 0f;

        float defenseFlat = 0f;

        float critChancePct = 0f;
        float critDamagePct = 0f;

        // 장착 아이템들의 스탯을 먼저 합산한다.
        // 현재 1차 구현에서는 기본 스탯형 효과만 BattleUnit 스냅샷에 반영한다.
        foreach (var item in unit.items)
        {
            if (item == null)
                continue;

            hpFlat += item.hpBonus;
            maxHpPct += item.maxHpPct;

            attackFlat += item.attackBonus;
            spellPowerPct += item.spAtkPct;
            attackSpeedPct += item.attackSpeedBonus;

            defenseFlat += item.defenseBonus;

            // 현재 BattleUnit은 방어/특수방어가 defense로 통합되어 있어 1차 구현에서는 함께 합산한다.
            defenseFlat += item.spDefBonus;

            critChancePct += item.criPct;
            critDamagePct += item.criDmgPct;
        }

        // PokemonUnit 원본 데이터는 수정하지 않고, 전투용 BattleUnit 스냅샷에만 아이템 효과를 적용한다.
        battleUnit.maxHp = (battleUnit.maxHp + hpFlat) * (1f + maxHpPct * 0.01f);
        battleUnit.currentHp = battleUnit.maxHp;

        battleUnit.attack += attackFlat;
        battleUnit.spellPower *= 1f + spellPowerPct * 0.01f;
        battleUnit.attackSpeed *= 1f + attackSpeedPct * 0.01f;

        battleUnit.defense += defenseFlat;

        battleUnit.critChance = Mathf.Clamp01(battleUnit.critChance + critChancePct * 0.01f);
        battleUnit.critMultiplier += critDamagePct * 0.01f;

#if UNITY_EDITOR
        // 검증용 로그. 빌드에서는 제외된다.
        Debug.Log(
            $"[ItemManager] 아이템 스탯 적용: {unit.data?.pokemonName ?? "Unknown"} " +
            $"items={unit.items.Count}, " +
            $"HP={battleUnit.maxHp:0.##}, ATK={battleUnit.attack:0.##}, " +
            $"DEF={battleUnit.defense:0.##}, SPELL={battleUnit.spellPower:0.##}, " +
            $"CRIT={battleUnit.critChance:0.##}"
        );
#endif
    }
}