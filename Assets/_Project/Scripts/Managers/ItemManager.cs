using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템/진화의 돌 인벤토리 보관 + 장착·해제 담당.
/// 획득(보상/상점)은 AddItem/AddStone(또는 이름으로 AddItemByNameEn/AddStoneByNameEn)으로 인벤토리에 추가,
/// 장착 단일 입구는 EquipToUnit(드래그&드롭 UI가 호출) — 성공 시 인벤토리에서 제거.
/// 해제 단일 입구는 UnequipFromUnit — 성공 시 인벤토리로 반환.
/// 아이템 쿠폰은 아이템 상점 전용 재화로 여기서 보관/소비한다.
/// 유닛 판매 시 OnUnitSold를 받아 장착 아이템/진화의 돌을 자동 회수한다.
/// </summary>
public class ItemManager : MonoBehaviour
{
    private const int MAX_INVENTORY_SIZE = 20; // PLACEHOLDER(기획확정 전)

    [Header("아이템 쿠폰")]
    [SerializeField] private int _startingItemCoupon = 0;

    private readonly List<ItemData> _items = new();
    private readonly List<EvolutionStoneData> _stones = new();

    public IReadOnlyList<ItemData> Items => _items;
    public IReadOnlyList<EvolutionStoneData> Stones => _stones;

    public int ItemCoupon { get; private set; }

    public int InventoryCount => _items.Count + _stones.Count;
    public bool HasInventorySpace => InventoryCount < MAX_INVENTORY_SIZE;

    private void OnEnable()
    {
        GameEvents.OnUnitSold += HandleUnitSold;
    }

    private void OnDisable()
    {
        GameEvents.OnUnitSold -= HandleUnitSold;
    }

    private void Start()
    {
        ItemCoupon = _startingItemCoupon;
        GameEvents.ItemCouponChanged(ItemCoupon);
        GameEvents.InventoryChanged();
    }

    // ──────────────────────────────────────────
    // 아이템 쿠폰
    // ──────────────────────────────────────────

    /// <summary>아이템 쿠폰 증가/감소. 결과는 0 미만으로 내려가지 않음.</summary>
    public void AddItemCoupon(int amount)
    {
        if (amount == 0) return;

        ItemCoupon = Mathf.Max(0, ItemCoupon + amount);
        GameEvents.ItemCouponChanged(ItemCoupon);

        Debug.Log($"[ItemCoupon] 쿠폰 변경: {ItemCoupon}");
    }

    /// <summary>아이템 쿠폰 사용. 보유량이 부족하면 false.</summary>
    public bool SpendItemCoupon(int amount)
    {
        if (amount <= 0) return true;

        if (ItemCoupon < amount)
        {
            Debug.Log($"[ItemCoupon] 쿠폰 부족 — 필요 {amount}, 보유 {ItemCoupon}");
            return false;
        }

        ItemCoupon -= amount;
        GameEvents.ItemCouponChanged(ItemCoupon);

        Debug.Log($"[ItemCoupon] 쿠폰 사용 -{amount} / 남은 쿠폰 {ItemCoupon}");
        return true;
    }

    // ──────────────────────────────────────────
    // 판매 시 장착물 자동 회수
    // ──────────────────────────────────────────

    /// <summary>
    /// 유닛 판매 시 장착 중인 일반 아이템/진화의 돌을 자동 해제해 인벤토리로 반환.
    /// BoardManager.SellUnit에서 UnitSold 이벤트가 Destroy 전에 발행되므로 여기서 unit 상태를 읽을 수 있다.
    ///
    /// 회수는 인벤토리 캡을 무시한다(RecoverToInventory) — 이미 보유했던 장착물이라
    /// 캡이 가득이어도 유실 없이 무조건 복귀(일시적 20칸 초과 허용)시키기 위함.
    /// (UnequipFromUnit→AddItem 경로는 캡 가득 시 장착물을 유실하므로 회수에는 쓰지 않는다.)
    /// </summary>
    private void HandleUnitSold(PokemonUnit unit)
    {
        if (unit == null) return;

        // 일반 아이템은 제거 중 리스트가 바뀌므로 복사본 기준으로 순회.
        if (unit.items != null && unit.items.Count > 0)
        {
            var equippedItems = new List<ItemData>(unit.items);

            foreach (var item in equippedItems)
            {
                if (item == null) continue;
                RecoverToInventory(unit.RemoveItem(item));
            }
        }

        // 진화의 돌은 제거 시 data가 베이스 포켓몬으로 원복됨.
        if (unit.equippedStone != null)
            RecoverToInventory(unit.RemoveStone());
    }

    /// <summary>판매 회수 전용 — 인벤토리 캡을 무시하고 일반 아이템을 복귀시킨다. null은 무시.</summary>
    private void RecoverToInventory(ItemData item)
    {
        if (item == null) return;
        _items.Add(item);
        GameEvents.InventoryChanged();
    }

    /// <summary>판매 회수 전용 — 인벤토리 캡을 무시하고 진화의 돌을 복귀시킨다. null은 무시.</summary>
    private void RecoverToInventory(EvolutionStoneData stone)
    {
        if (stone == null) return;
        _stones.Add(stone);
        GameEvents.InventoryChanged();
    }

    // ──────────────────────────────────────────
    // 인벤토리 획득
    // ──────────────────────────────────────────

    /// <summary>일반 아이템 획득(보상/상점 구매 성공 시 호출). 인벤토리 가득이면 false.</summary>
    public bool AddItem(ItemData item)
    {
        if (item == null) return false;

        if (!HasInventorySpace)
        {
            Debug.LogWarning($"[Item] 인벤토리 가득({InventoryCount}/{MAX_INVENTORY_SIZE}) — '{item.itemName}' 획득 실패");
            return false;
        }

        _items.Add(item);
        GameEvents.InventoryChanged();
        return true;
    }

    /// <summary>진화의 돌 획득. 인벤토리 가득이면 false.</summary>
    public bool AddStone(EvolutionStoneData stone)
    {
        if (stone == null) return false;

        if (!HasInventorySpace)
        {
            Debug.LogWarning($"[Item] 인벤토리 가득({InventoryCount}/{MAX_INVENTORY_SIZE}) — '{stone.stoneName}' 획득 실패");
            return false;
        }

        _stones.Add(stone);
        GameEvents.InventoryChanged();
        return true;
    }

    /// <summary>영문명으로 ItemDatabase 조회 후 획득(RewardManager 등 refNameEn 기반 호출용).</summary>
    public bool AddItemByNameEn(string nameEn)
    {
        var db = ItemDatabase.Instance;
        ItemData item = db != null ? db.GetByNameEn(nameEn) : null;

        if (item == null)
        {
            Debug.LogWarning($"[Item] '{nameEn}' ItemDatabase에 없음 — 획득 실패");
            return false;
        }

        return AddItem(item);
    }

    /// <summary>영문명으로 EvolutionStoneDatabase 조회 후 획득(RewardManager 등 refNameEn 기반 호출용).</summary>
    public bool AddStoneByNameEn(string nameEn)
    {
        var db = EvolutionStoneDatabase.Instance;
        EvolutionStoneData stone = db != null ? db.GetByNameEn(nameEn) : null;

        if (stone == null)
        {
            Debug.LogWarning($"[Item] '{nameEn}' EvolutionStoneDatabase에 없음 — 획득 실패");
            return false;
        }

        return AddStone(stone);
    }

    // ──────────────────────────────────────────
    // 장착 / 해제
    // ──────────────────────────────────────────

    /// <summary>
    /// 장착 단일 입구. 드래그&드롭 UI가 인벤토리 항목을 유닛에 드롭할 때 호출.
    /// 인벤토리에 있는 항목만 장착 가능. 성공 시 인벤토리에서 제거(슬롯 규칙은 PokemonUnit이 강제).
    /// </summary>
    public bool EquipToUnit(ScriptableObject equippable, PokemonUnit unit)
    {
        if (unit == null || equippable == null) return false;

        switch (equippable)
        {
            case EvolutionStoneData stone:
                if (!_stones.Contains(stone)) return false;
                if (!unit.TryEquipStone(stone)) return false;

                _stones.Remove(stone);
                GameEvents.InventoryChanged();
                return true;

            case ItemData item:
                if (!_items.Contains(item)) return false;
                if (!unit.TryEquipItem(item)) return false;

                _items.Remove(item);
                GameEvents.InventoryChanged();
                return true;

            default:
                Debug.LogWarning($"[Item] 장착 불가 타입: {equippable.GetType().Name}");
                return false;
        }
    }

    /// <summary>장착 해제 단일 입구(되돌리기). 돌은 베이스로 원복, 일반템은 제거. 성공 시 인벤토리로 반환.</summary>
    public ScriptableObject UnequipFromUnit(ScriptableObject equippable, PokemonUnit unit)
    {
        if (unit == null || equippable == null) return null;

        switch (equippable)
        {
            case EvolutionStoneData:
                {
                    var removed = unit.RemoveStone();
                    if (removed != null) AddStone(removed);
                    return removed;
                }

            case ItemData item:
                {
                    var removed = unit.RemoveItem(item);
                    if (removed != null) AddItem(removed);
                    return removed;
                }

            default:
                Debug.LogWarning($"[Item] 해제 불가 타입: {equippable.GetType().Name}");
                return null;
        }
    }

    /// <summary>기존 진화의 돌 제거 호출부 호환용 — 인벤토리로 반환까지 처리.</summary>
    public ScriptableObject UnequipStone(PokemonUnit unit)
        => unit != null ? UnequipFromUnit(unit.equippedStone, unit) : null;
}