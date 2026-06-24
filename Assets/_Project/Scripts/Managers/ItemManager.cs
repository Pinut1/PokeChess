using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템/진화의 돌 장착·해제 및 전투 스냅샷 스탯 반영 담당. 김태욱 파트.
/// </summary>
public class ItemManager : MonoBehaviour
{
    private const int MAX_INVENTORY_SIZE = 20;

    [Header("Item Inventory")]
    [SerializeField] private List<ItemData> _items = new();
    [SerializeField] private List<EvolutionStoneData> _stones = new();

    public int InventoryCount => _items.Count + _stones.Count;
    public int InventoryMax => MAX_INVENTORY_SIZE;

    public IReadOnlyList<ItemData> Items => _items;
    public IReadOnlyList<EvolutionStoneData> Stones => _stones;

    public bool HasInventorySpace()
    {
        return InventoryCount < MAX_INVENTORY_SIZE;
    }

    /// <summary>
    /// 일반 아이템 획득 시 인벤토리에 추가한다.
    /// Reward / ItemShop 구매 성공 시 호출한다.
    /// </summary>
    public bool AddItem(ItemData item)
    {
        if (item == null)
        {
            Debug.LogWarning("[ItemManager] 아이템 추가 실패: 아이템 데이터가 없습니다.");
            return false;
        }

        if (!HasInventorySpace())
        {
            Debug.LogWarning($"[ItemManager] 아이템 추가 실패: 인벤토리가 가득 찼습니다. ({InventoryCount}/{MAX_INVENTORY_SIZE})");
            return false;
        }

        _items.Add(item);
        Debug.Log($"[ItemManager] 아이템 인벤토리 추가: {item.itemName} ({InventoryCount}/{MAX_INVENTORY_SIZE})");
        return true;
    }

    /// <summary>
    /// 진화의 돌 획득 시 인벤토리에 추가한다.
    /// Reward / ItemShop 구매 성공 시 호출한다.
    /// </summary>
    public bool AddStone(EvolutionStoneData stone)
    {
        if (stone == null)
        {
            Debug.LogWarning("[ItemManager] 진화의 돌 추가 실패: 돌 데이터가 없습니다.");
            return false;
        }

        if (!HasInventorySpace())
        {
            Debug.LogWarning($"[ItemManager] 진화의 돌 추가 실패: 인벤토리가 가득 찼습니다. ({InventoryCount}/{MAX_INVENTORY_SIZE})");
            return false;
        }

        _stones.Add(stone);
        Debug.Log($"[ItemManager] 진화의 돌 인벤토리 추가: {stone.name} ({InventoryCount}/{MAX_INVENTORY_SIZE})");
        return true;
    }

    /// <summary>
    /// 일반 아이템/진화의 돌을 타입에 따라 인벤토리에 추가한다.
    /// </summary>
    public bool AddToInventory(ScriptableObject equippable)
    {
        if (equippable == null)
            return false;

        switch (equippable)
        {
            case ItemData item:
                return AddItem(item);

            case EvolutionStoneData stone:
                return AddStone(stone);

            default:
                Debug.LogWarning($"[ItemManager] 인벤토리 추가 불가 타입: {equippable.GetType().Name}");
                return false;
        }
    }

    private bool ContainsInInventory(ScriptableObject equippable)
    {
        switch (equippable)
        {
            case ItemData item:
                return _items.Contains(item);

            case EvolutionStoneData stone:
                return _stones.Contains(stone);

            default:
                return false;
        }
    }

    private bool RemoveFromInventory(ScriptableObject equippable)
    {
        switch (equippable)
        {
            case ItemData item:
                return _items.Remove(item);

            case EvolutionStoneData stone:
                return _stones.Remove(stone);

            default:
                return false;
        }
    }

    /// <summary>
    /// 장착 단일 입구.
    /// 드래그&드롭 UI가 인벤토리 항목을 유닛에 드롭할 때 호출한다.
    ///
    /// 처리 흐름:
    /// 1. 인벤토리에 있는 아이템인지 확인
    /// 2. 유닛에 장착 시도
    /// 3. 장착 성공 시 인벤토리에서 제거
    /// 4. 장착 실패 시 인벤토리에 그대로 유지
    /// </summary>
    public bool EquipToUnit(ScriptableObject equippable, PokemonUnit unit)
    {
        if (unit == null || equippable == null)
            return false;

        if (!ContainsInInventory(equippable))
        {
            Debug.LogWarning($"[ItemManager] 장착 실패: 인벤토리에 없는 장착물입니다. ({equippable.name})");
            return false;
        }

        bool success;

        switch (equippable)
        {
            case EvolutionStoneData stone:
                success = unit.TryEquipStone(stone);
                break;

            case ItemData item:
                success = TryEquipItem(unit, item);
                break;

            default:
                Debug.LogWarning($"[ItemManager] 장착 불가 타입: {equippable.GetType().Name}");
                return false;
        }

        if (!success)
        {
            Debug.LogWarning("[ItemManager] 장착 실패: 장착물은 인벤토리에 그대로 유지됩니다.");
            return false;
        }

        RemoveFromInventory(equippable);
        Debug.Log($"[ItemManager] 장착 성공: 인벤토리에서 제거 완료 ({InventoryCount}/{MAX_INVENTORY_SIZE})");
        return true;
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
    /// 장착 해제 단일 입구.
    /// 돌은 베이스 종으로 원복하고, 일반 아이템은 제거한다.
    ///
    /// 처리 흐름:
    /// 1. 인벤토리 공간 확인
    /// 2. 유닛에서 장착물 제거
    /// 3. 제거 성공 시 인벤토리로 반환
    /// </summary>
    public ScriptableObject UnequipFromUnit(ScriptableObject equippable, PokemonUnit unit)
    {
        if (unit == null || equippable == null)
            return null;

        if (!HasInventorySpace())
        {
            Debug.LogWarning($"[ItemManager] 해제 실패: 인벤토리가 가득 찼습니다. ({InventoryCount}/{MAX_INVENTORY_SIZE})");
            return null;
        }

        ScriptableObject removed = null;

        switch (equippable)
        {
            case EvolutionStoneData:
                removed = unit.RemoveStone();
                break;

            case ItemData item:
                removed = RemoveItem(unit, item);
                break;

            default:
                Debug.LogWarning($"[ItemManager] 해제 불가 타입: {equippable.GetType().Name}");
                return null;
        }

        if (removed == null)
            return null;

        AddToInventory(removed);
        Debug.Log($"[ItemManager] 해제 성공: 인벤토리로 반환 완료 ({InventoryCount}/{MAX_INVENTORY_SIZE})");

        return removed;
    }

    /// <summary>
    /// 기존 진화의 돌 제거 호출부 호환용.
    /// 제거 성공 시 인벤토리로 반환한다.
    /// </summary>
    public ScriptableObject UnequipStone(PokemonUnit unit)
    {
        if (unit == null)
            return null;

        if (!HasInventorySpace())
        {
            Debug.LogWarning($"[ItemManager] 진화의 돌 해제 실패: 인벤토리가 가득 찼습니다. ({InventoryCount}/{MAX_INVENTORY_SIZE})");
            return null;
        }

        ScriptableObject removed = unit.RemoveStone();

        if (removed == null)
            return null;

        AddToInventory(removed);
        return removed;
    }

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