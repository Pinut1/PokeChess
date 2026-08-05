using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 일반 장비, 진화의 돌, 제거기, 재조합기 인벤토리를 관리한다.
///
/// 일반 장비와 진화의 돌은 각각 인벤토리 한 칸을 사용한다.
/// 제거기는 게임 시작부터 보유하며 항상 한 칸을 사용한다.
/// 재조합기는 수량이 중첩되며, 한 개 이상 보유하면 한 칸을 사용한다.
///
/// 장착 단일 입구는 EquipToUnit,
/// 개별 해제 단일 입구는 UnequipFromUnit이다.
///
/// 제거기는 유닛의 모든 장착물을 인벤토리로 반환하며,
/// 반환할 공간이 부족하면 사용할 수 없다.
/// </summary>
public class ItemManager : MonoBehaviour
{
    private const int MAX_INVENTORY_SIZE = 20;

    /// <summary>파트너 재접속 대기 중엔 장착/해제를 막는다(2026-08 — 대기 중 입력 차단).</summary>
    private static bool IsAwaitingPartnerReconnect() =>
        GameManager.TryGet(out var gm) && gm.Network != null && gm.Network.IsAwaitingPartnerReconnect;

    [Header("아이템 쿠폰")]
    [SerializeField] private int _startingItemCoupon;

    [Header("제거기")]
    [SerializeField] private bool _hasRemover = true;

    [Header("재조합기")]
    [SerializeField] private int _startingReforger;

    private readonly List<ItemData> _items = new();
    private readonly List<EvolutionStoneData> _stones = new();

    /*
     * 기존 호출부 컴파일 호환용.
     * 현재 기획에서는 소모품을 인벤토리 아이템으로 사용하지 않는다.
     */
    private readonly List<ConsumableData> _consumables = new();

    public IReadOnlyList<ItemData> Items => _items;
    public IReadOnlyList<EvolutionStoneData> Stones => _stones;
    public IReadOnlyList<ConsumableData> Consumables => _consumables;

    public int ItemCoupon { get; private set; }
    public int ReforgerCount { get; private set; }

    public bool HasRemover => _hasRemover;

    /// <summary>
    /// 실제 인벤토리 사용 칸 수.
    ///
    /// 일반 장비와 진화의 돌은 각각 한 칸을 사용한다.
    /// 제거기는 보유 중이면 한 칸을 사용한다.
    /// 재조합기는 개당 한 칸을 사용한다(중첩하지 않고 낱개로 놓인다).
    /// </summary>
    public int InventoryCount =>
        _items.Count
        + _stones.Count
        + (_hasRemover ? 1 : 0)
        + ReforgerCount;

    /// <summary>현재 인벤토리의 남은 빈칸 수.</summary>
    public int AvailableInventorySpace =>
        Mathf.Max(0, MAX_INVENTORY_SIZE - InventoryCount);

    /// <summary>인벤토리에 한 칸 이상 빈 공간이 있는지.</summary>
    public bool HasInventorySpace =>
        AvailableInventorySpace > 0;

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
        ItemCoupon = Mathf.Max(0, _startingItemCoupon);
        ReforgerCount = Mathf.Max(0, _startingReforger);

        GameEvents.ItemCouponChanged(ItemCoupon);
        GameEvents.InventoryChanged();
    }

    // ──────────────────────────────────────────
    // 아이템 쿠폰
    // ──────────────────────────────────────────

    /// <summary>
    /// 아이템 쿠폰을 증가 또는 감소시킨다.
    /// 결과는 0 미만으로 내려가지 않는다.
    /// </summary>
    public void AddItemCoupon(int amount)
    {
        if (amount == 0)
            return;

        ItemCoupon = Mathf.Max(0, ItemCoupon + amount);
        GameEvents.ItemCouponChanged(ItemCoupon);

        Debug.Log($"[아이템 쿠폰] 보유량 변경: {ItemCoupon}");
    }

    /// <summary>
    /// 아이템 쿠폰을 사용한다.
    /// 보유량이 부족하면 false를 반환한다.
    /// </summary>
    public bool SpendItemCoupon(int amount)
    {
        if (amount <= 0)
            return true;

        if (ItemCoupon < amount)
        {
            Debug.Log(
                $"[아이템 쿠폰] 수량 부족 — " +
                $"필요 {amount}, 보유 {ItemCoupon}");

            return false;
        }

        ItemCoupon -= amount;
        GameEvents.ItemCouponChanged(ItemCoupon);

        Debug.Log(
            $"[아이템 쿠폰] -{amount} / " +
            $"남은 수량 {ItemCoupon}");

        return true;
    }

    // ──────────────────────────────────────────
    // 재조합기
    // ──────────────────────────────────────────

    /// <summary>
    /// 재조합기를 획득한다.
    ///
    /// 낱개로 칸을 차지하므로(중첩 없음) 개수만큼 빈칸이 필요하다.
    /// 빈칸이 모자라면 들어갈 수 있는 만큼만 받고 나머지는 유실된다
    /// (유닛 보상이 벤치 만석에서 유실되는 것과 같은 규칙).
    /// </summary>
    public bool AddReforger(int amount)
    {
        if (amount <= 0)
            return false;

        int space = AvailableInventorySpace;

        if (space <= 0)
        {
            Debug.LogWarning(
                $"[재조합기] 인벤토리 가득 " +
                $"({InventoryCount}/{MAX_INVENTORY_SIZE}) — 획득 실패");

            return false;
        }

        int granted = Mathf.Min(amount, space);

        ReforgerCount += granted;
        GameEvents.InventoryChanged();

        if (granted < amount)
        {
            Debug.LogWarning(
                $"[재조합기] 빈칸 부족 — {amount}개 중 {granted}개만 획득 " +
                $"({amount - granted}개 유실)");
        }

        Debug.Log(
            $"[재조합기] +{granted} / " +
            $"보유 {ReforgerCount}개 / " +
            $"인벤토리 {InventoryCount}/{MAX_INVENTORY_SIZE}");

        return true;
    }

    /// <summary>
    /// 재조합기를 사용한다.
    /// 보유량이 부족하면 false를 반환한다.
    /// </summary>
    public bool SpendReforger(int amount)
    {
        if (amount <= 0)
            return true;

        if (ReforgerCount < amount)
        {
            Debug.Log(
                $"[재조합기] 수량 부족 — " +
                $"필요 {amount}, 보유 {ReforgerCount}");

            return false;
        }

        ReforgerCount -= amount;
        GameEvents.InventoryChanged();

        Debug.Log(
            $"[재조합기] -{amount} / " +
            $"남은 수량 {ReforgerCount}개 / " +
            $"인벤토리 {InventoryCount}/{MAX_INVENTORY_SIZE}");

        return true;
    }

    // ──────────────────────────────────────────
    // 판매 및 성급 합체 시 장착물 회수
    // ──────────────────────────────────────────

    /// <summary>
    /// 유닛 판매 시 장착 중인 일반 장비와 진화의 돌을 회수한다.
    ///
    /// 판매 시에는 이미 플레이어가 보유하던 장착물을 돌려주는 것이므로
    /// 인벤토리 제한을 무시하고 복귀시킨다.
    /// </summary>
    private void HandleUnitSold(PokemonUnit unit)
    {
        if (unit == null)
            return;

        bool inventoryChanged = false;

        if (unit.items != null && unit.items.Count > 0)
        {
            var equippedItems = new List<ItemData>(unit.items);

            foreach (ItemData item in equippedItems)
            {
                if (item == null)
                    continue;

                ItemData removed = unit.RemoveItem(item);

                if (removed == null)
                    continue;

                _items.Add(removed);
                inventoryChanged = true;
            }
        }

        if (unit.equippedStone != null)
        {
            EvolutionStoneData removedStone = unit.RemoveStone();

            if (removedStone != null)
            {
                _stones.Add(removedStone);
                inventoryChanged = true;
            }
        }

        if (inventoryChanged)
            GameEvents.InventoryChanged();
    }

    /// <summary>
    /// 판매 등 강제 회수 상황에서 일반 장비를 인벤토리 제한 없이 복귀시킨다.
    /// </summary>
    private void RecoverToInventory(ItemData item)
    {
        if (item == null)
            return;

        _items.Add(item);
        GameEvents.InventoryChanged();
    }

    /// <summary>
    /// 판매 등 강제 회수 상황에서 진화의 돌을 인벤토리 제한 없이 복귀시킨다.
    /// </summary>
    private void RecoverToInventory(EvolutionStoneData stone)
    {
        if (stone == null)
            return;

        _stones.Add(stone);
        GameEvents.InventoryChanged();
    }

    /// <summary>
    /// 성급 합체 후 남은 일반 장비를 인벤토리 제한 없이 반환한다.
    /// </summary>
    public void RecoverMergedItem(ItemData item)
    {
        RecoverToInventory(item);
    }

    /// <summary>
    /// 성급 합체 후 중복된 진화의 돌을 인벤토리 제한 없이 반환한다.
    /// </summary>
    public void RecoverMergedStone(EvolutionStoneData stone)
    {
        RecoverToInventory(stone);
    }

    // ──────────────────────────────────────────
    // 인벤토리 획득
    // ──────────────────────────────────────────

    /// <summary>
    /// 일반 장비를 획득한다.
    /// 인벤토리가 가득 차 있으면 false를 반환한다.
    /// </summary>
    public bool AddItem(ItemData item)
    {
        if (item == null)
            return false;

        if (!HasInventorySpace)
        {
            Debug.LogWarning(
                $"[아이템] 인벤토리 가득 " +
                $"({InventoryCount}/{MAX_INVENTORY_SIZE}) — " +
                $"'{item.itemName}' 획득 실패");

            return false;
        }

        _items.Add(item);
        GameEvents.InventoryChanged();

        return true;
    }

    /// <summary>
    /// 진화의 돌을 획득한다.
    /// 인벤토리가 가득 차 있으면 false를 반환한다.
    /// </summary>
    public bool AddStone(EvolutionStoneData stone)
    {
        if (stone == null)
            return false;

        if (!HasInventorySpace)
        {
            Debug.LogWarning(
                $"[진화의 돌] 인벤토리 가득 " +
                $"({InventoryCount}/{MAX_INVENTORY_SIZE}) — " +
                $"'{stone.stoneName}' 획득 실패");

            return false;
        }

        _stones.Add(stone);
        GameEvents.InventoryChanged();

        return true;
    }

    /// <summary>
    /// 영문명으로 일반 장비를 조회해 획득한다.
    /// </summary>
    public bool AddItemByNameEn(string nameEn)
    {
        if (string.IsNullOrEmpty(nameEn))
            return false;

        ItemDatabase db = ItemDatabase.Instance;
        ItemData item = db != null ? db.GetByNameEn(nameEn) : null;

        if (item == null)
        {
            Debug.LogWarning(
                $"[아이템] '{nameEn}'을 ItemDatabase에서 찾지 못했습니다.");

            return false;
        }

        return AddItem(item);
    }

    /// <summary>
    /// 영문명으로 진화의 돌을 조회해 획득한다.
    /// </summary>
    public bool AddStoneByNameEn(string nameEn)
    {
        if (string.IsNullOrEmpty(nameEn))
            return false;

        EvolutionStoneDatabase db = EvolutionStoneDatabase.Instance;
        EvolutionStoneData stone =
            db != null ? db.GetByNameEn(nameEn) : null;

        if (stone == null)
        {
            Debug.LogWarning(
                $"[진화의 돌] '{nameEn}'을 " +
                "EvolutionStoneDatabase에서 찾지 못했습니다.");

            return false;
        }

        return AddStone(stone);
    }

    /// <summary>
    /// 현재 기획에서는 별도 소모품을 사용하지 않는다.
    /// 제거기/재조합기는 _hasRemover/ReforgerCount로 따로 관리한다.
    /// 기존 호출부 컴파일 호환을 위해 메서드만 유지한다.
    /// </summary>
    public bool AddConsumable(ConsumableData consumable)
    {
        Debug.LogWarning(
            "[소모품] 현재 기획에서는 별도 소모품을 사용하지 않습니다.");

        return false;
    }

    /// <summary>
    /// 현재 기획에서는 별도 소모품을 사용하지 않는다.
    /// 기존 호출부 컴파일 호환을 위해 메서드만 유지한다.
    /// </summary>
    public bool AddConsumableByNameEn(string nameEn)
    {
        Debug.LogWarning(
            "[소모품] 현재 기획에서는 별도 소모품을 사용하지 않습니다.");

        return false;
    }

    // ──────────────────────────────────────────
    // 장착 및 개별 해제
    // ──────────────────────────────────────────

    /// <summary>
    /// 일반 장비 또는 진화의 돌을 유닛에게 장착한다.
    ///
    /// 성공하면 해당 장착물을 인벤토리에서 제거한다.
    /// 전투 중 필드 유닛에게는 장착할 수 없다.
    /// 전투 중 벤치 유닛에게는 장착할 수 있다.
    /// </summary>
    public bool EquipToUnit(
        ScriptableObject equippable,
        PokemonUnit unit)
    {
        if (IsAwaitingPartnerReconnect()) return false;

        if (unit == null || equippable == null)
            return false;

        if (IsBattlePhaseAndUnitOnBoard(unit))
        {
            Debug.Log(
                $"[아이템] 전투 중 필드 유닛 " +
                $"'{unit.data?.pokemonName}'에게는 장착할 수 없습니다.");

            return false;
        }

        switch (equippable)
        {
            case EvolutionStoneData stone:
                if (!_stones.Contains(stone))
                    return false;

                if (!unit.TryEquipStone(stone))
                    return false;

                _stones.Remove(stone);
                GameEvents.InventoryChanged();

                return true;

            case ItemData item:
                if (!_items.Contains(item))
                    return false;

                if (!unit.TryEquipItem(item))
                    return false;

                _items.Remove(item);
                GameEvents.InventoryChanged();

                return true;

            default:
                Debug.LogWarning(
                    $"[아이템] 장착할 수 없는 타입: " +
                    $"{equippable.GetType().Name}");

                return false;
        }
    }

    /// <summary>
    /// 유닛에게 장착된 항목 하나를 해제해 인벤토리로 반환한다.
    ///
    /// 인벤토리에 빈칸이 없으면 해제하지 않는다.
    /// </summary>
    public ScriptableObject UnequipFromUnit(
        ScriptableObject equippable,
        PokemonUnit unit)
    {
        if (IsAwaitingPartnerReconnect()) return null;

        if (unit == null || equippable == null)
            return null;

        if (!HasInventorySpace)
        {
            Debug.Log(
                $"[아이템 해제] 인벤토리 공간 부족 — " +
                $"현재 {InventoryCount}/{MAX_INVENTORY_SIZE}");

            return null;
        }

        switch (equippable)
        {
            case EvolutionStoneData:
                {
                    EvolutionStoneData removed = unit.RemoveStone();

                    if (removed == null)
                        return null;

                    _stones.Add(removed);
                    GameEvents.InventoryChanged();

                    return removed;
                }

            case ItemData item:
                {
                    ItemData removed = unit.RemoveItem(item);

                    if (removed == null)
                        return null;

                    _items.Add(removed);
                    GameEvents.InventoryChanged();

                    return removed;
                }

            default:
                Debug.LogWarning(
                    $"[아이템] 해제할 수 없는 타입: " +
                    $"{equippable.GetType().Name}");

                return null;
        }
    }

    /// <summary>
    /// 기존 진화의 돌 제거 호출부 호환용 메서드.
    /// </summary>
    public ScriptableObject UnequipStone(PokemonUnit unit)
    {
        if (unit == null)
            return null;

        return UnequipFromUnit(unit.equippedStone, unit);
    }

    // ──────────────────────────────────────────
    // 제거기
    // ──────────────────────────────────────────

    /// <summary>
    /// 제거기를 사용해 대상 유닛의 일반 장비와 진화의 돌을
    /// 모두 인벤토리로 반환한다.
    ///
    /// 제거기는 무제한 도구이므로 사용해도 사라지지 않는다.
    /// 반환할 장착물 개수만큼 빈칸이 없으면 사용할 수 없다.
    /// 전투 중 필드 유닛에게는 사용할 수 없다.
    /// </summary>
    public bool UseRemover(PokemonUnit unit)
    {
        if (!_hasRemover)
        {
            Debug.LogWarning(
                "[제거기] 제거기를 보유하고 있지 않습니다.");

            return false;
        }

        if (unit == null)
            return false;

        if (IsBattlePhaseAndUnitOnBoard(unit))
        {
            Debug.Log(
                $"[제거기] 전투 중 필드 유닛 " +
                $"'{unit.data?.pokemonName}'에게는 사용할 수 없습니다.");

            return false;
        }

        if (!HasSpaceForEquippedObjects(unit, "제거기"))
            return false;

        int removedItemCount = 0;
        bool removedStone = false;

        if (unit.items != null && unit.items.Count > 0)
        {
            var equippedItems = new List<ItemData>(unit.items);

            foreach (ItemData item in equippedItems)
            {
                if (item == null)
                    continue;

                ItemData removed = unit.RemoveItem(item);

                if (removed == null)
                    continue;

                _items.Add(removed);
                removedItemCount++;
            }
        }

        if (unit.equippedStone != null)
        {
            EvolutionStoneData removed = unit.RemoveStone();

            if (removed != null)
            {
                _stones.Add(removed);
                removedStone = true;
            }
        }

        bool succeeded =
            removedItemCount > 0 || removedStone;

        if (!succeeded)
        {
            Debug.LogWarning(
                $"[제거기] '{unit.data?.pokemonName}'의 " +
                "장착물 제거에 실패했습니다.");

            return false;
        }

        GameEvents.InventoryChanged();

        Debug.Log(
            $"[제거기] '{unit.data?.pokemonName}' 장착물 반환 완료 — " +
            $"일반 장비 {removedItemCount}개, " +
            $"진화의 돌 {(removedStone ? 1 : 0)}개 / " +
            $"인벤토리 {InventoryCount}/{MAX_INVENTORY_SIZE}");

        return true;
    }

    /// <summary>
    /// 재조합기를 사용해 대상 유닛의 모든 장착물을
    /// 같은 종류의 무작위 장착물로 변경한 뒤 인벤토리로 반환한다.
    ///
    /// 일반 장비는 일반 장비로,
    /// 진화의 돌은 진화의 돌로 재조합한다.
    ///
    /// 같은 아이템이 다시 등장할 수 있으며,
    /// 모든 처리가 성공한 경우에만 재조합기 1개를 소모한다.
    /// </summary>
    public bool UseReforger(PokemonUnit unit)
    {
        if (ReforgerCount <= 0)
        {
            Debug.LogWarning(
                "[재조합기] 재조합기를 보유하고 있지 않습니다."
            );

            return false;
        }

        if (unit == null)
            return false;

        if (IsBattlePhaseAndUnitOnBoard(unit))
        {
            Debug.Log(
                $"[재조합기] 전투 중 필드 유닛 " +
                $"'{unit.data?.pokemonName}'에게는 사용할 수 없습니다."
            );

            return false;
        }

        if (!HasSpaceForEquippedObjects(unit, "재조합기"))
            return false;

        bool hasItems =
            unit.items != null &&
            unit.items.Count > 0;

        bool hasStone =
            unit.equippedStone != null;

        // 실제 변경 전에 기존 장착물과 결과를 모두 준비한다.
        var originalItems =
            hasItems
                ? new List<ItemData>(unit.items)
                : new List<ItemData>();

        var reforgedItems =
            new List<ItemData>();

        EvolutionStoneData originalStone =
            unit.equippedStone;

        EvolutionStoneData reforgedStone =
            null;

        // ─────────────────────────────
        // 일반 장비 결과 미리 결정
        // ─────────────────────────────

        if (originalItems.Count > 0)
        {
            ItemDatabase itemDatabase =
                ItemDatabase.Instance;

            if (itemDatabase == null ||
                itemDatabase.all == null)
            {
                Debug.LogWarning(
                    "[재조합기] ItemDatabase를 불러올 수 없습니다."
                );

                return false;
            }

            List<ItemData> itemCandidates =
                GetValidItemCandidates(
                    itemDatabase.all
                );

            if (itemCandidates.Count <= 0)
            {
                Debug.LogWarning(
                    "[재조합기] 재조합 가능한 일반 장비가 없습니다."
                );

                return false;
            }

            for (int i = 0; i < originalItems.Count; i++)
            {
                ItemData result =
                    itemCandidates[
                        Random.Range(
                            0,
                            itemCandidates.Count
                        )
                    ];

                reforgedItems.Add(result);
            }
        }

        // ─────────────────────────────
        // 진화의 돌 결과 미리 결정
        // ─────────────────────────────

        if (hasStone)
        {
            EvolutionStoneDatabase stoneDatabase =
                EvolutionStoneDatabase.Instance;

            if (stoneDatabase == null ||
                stoneDatabase.all == null)
            {
                Debug.LogWarning(
                    "[재조합기] EvolutionStoneDatabase를 불러올 수 없습니다."
                );

                return false;
            }

            List<EvolutionStoneData> stoneCandidates =
                GetValidStoneCandidates(
                    stoneDatabase.all
                );

            if (stoneCandidates.Count <= 0)
            {
                Debug.LogWarning(
                    "[재조합기] 재조합 가능한 진화의 돌이 없습니다."
                );

                return false;
            }

            reforgedStone =
                stoneCandidates[
                    Random.Range(
                        0,
                        stoneCandidates.Count
                    )
                ];
        }

        // 결과 개수가 기존 장비 개수와 맞는지 최종 확인한다.
        if (reforgedItems.Count != originalItems.Count)
        {
            Debug.LogWarning(
                "[재조합기] 일반 장비 결과 개수가 일치하지 않습니다."
            );

            return false;
        }

        // 실제 적용 직전에 기존 장착 상태를 다시 확인한다.
        foreach (ItemData originalItem in originalItems)
        {
            if (originalItem == null ||
                unit.items == null ||
                !unit.items.Contains(originalItem))
            {
                Debug.LogWarning(
                    "[재조합기] 처리 중 유닛의 장착 상태가 변경되었습니다."
                );

                return false;
            }
        }

        if (hasStone &&
            unit.equippedStone != originalStone)
        {
            Debug.LogWarning(
                "[재조합기] 처리 중 진화의 돌 장착 상태가 변경되었습니다."
            );

            return false;
        }

        // ─────────────────────────────
        // 기존 일반 장비 제거
        // ─────────────────────────────

        foreach (ItemData originalItem in originalItems)
        {
            ItemData removed =
                unit.RemoveItem(originalItem);

            if (removed == null)
            {
                Debug.LogError(
                    $"[재조합기] 일반 장비 제거 실패: " +
                    $"{originalItem?.itemName}"
                );

                return false;
            }
        }

        // ─────────────────────────────
        // 기존 진화의 돌 제거
        // ─────────────────────────────

        if (hasStone)
        {
            EvolutionStoneData removedStone =
                unit.RemoveStone();

            if (removedStone == null)
            {
                Debug.LogError(
                    $"[재조합기] 진화의 돌 제거 실패: " +
                    $"{originalStone?.stoneName}"
                );

                return false;
            }
        }

        // ─────────────────────────────
        // 재조합 결과 인벤토리 반환
        // ─────────────────────────────

        foreach (ItemData resultItem in reforgedItems)
        {
            _items.Add(resultItem);
        }

        if (reforgedStone != null)
        {
            _stones.Add(reforgedStone);
        }

        /*
         * 모든 변경이 완료된 후에만 재조합기를 소모한다.
         * SpendReforger 내부에서 InventoryChanged 이벤트를 호출한다.
         */
        if (!SpendReforger(1))
        {
            Debug.LogError(
                "[재조합기] 결과 적용 후 재조합기 소모에 실패했습니다."
            );

            return false;
        }

        Debug.Log(
            $"[재조합기] '{unit.data?.pokemonName}' 재조합 완료 — " +
            $"일반 장비 {reforgedItems.Count}개, " +
            $"진화의 돌 {(reforgedStone != null ? 1 : 0)}개 반환 / " +
            $"남은 재조합기 {ReforgerCount}개 / " +
            $"인벤토리 {InventoryCount}/{MAX_INVENTORY_SIZE}"
        );

        return true;
    }

    /// <summary>
    /// 유닛 없이, 인벤토리에 있는 진화의 돌 하나를 재조합기로 직접 재조합한다.
    ///
    /// 장착할 유닛이 없어 장착 경로(UseReforger)를 쓸 수 없는 진화의 돌을 위한 보조 경로 —
    /// 인벤토리 UI에서 재조합기를 다른 슬롯의 진화의 돌 위로 끌어다 놓는 조작에서 호출된다.
    /// 대상 돌은 결과 후보에서 반드시 제외한다(같은 돌 재당첨 없음). UseReforger(유닛 대상)는
    /// 이 제외 규칙을 적용하지 않으므로 그대로 둔다 — 이번 변경 범위 밖이다.
    ///
    /// 모든 검증을 통과했을 때만 대상 돌을 결과로 교체하고 재조합기 1개를 소모한다.
    /// </summary>
    public bool ReforgeStoneInInventory(EvolutionStoneData targetStone)
    {
        if (ReforgerCount <= 0)
        {
            Debug.LogWarning(
                "[재조합기] 재조합기를 보유하고 있지 않습니다."
            );

            return false;
        }

        if (targetStone == null || !_stones.Contains(targetStone))
        {
            Debug.LogWarning(
                "[재조합기] 대상 진화의 돌이 인벤토리에 없습니다."
            );

            return false;
        }

        EvolutionStoneDatabase stoneDatabase =
            EvolutionStoneDatabase.Instance;

        if (stoneDatabase == null ||
            stoneDatabase.all == null)
        {
            Debug.LogWarning(
                "[재조합기] EvolutionStoneDatabase를 불러올 수 없습니다."
            );

            return false;
        }

        List<EvolutionStoneData> candidates =
            GetValidStoneCandidates(
                stoneDatabase.all,
                targetStone
            );

        if (candidates.Count <= 0)
        {
            Debug.LogWarning(
                "[재조합기] 재조합 가능한 진화의 돌이 없습니다."
            );

            return false;
        }

        EvolutionStoneData result =
            candidates[
                Random.Range(
                    0,
                    candidates.Count
                )
            ];

        // 실제 적용 직전에 대상이 여전히 같은 자리에 있는지 다시 확인한다(UseReforger와 동일한 순서).
        int index = _stones.IndexOf(targetStone);

        if (index < 0)
        {
            Debug.LogWarning(
                "[재조합기] 처리 중 대상 진화의 돌이 사라졌습니다."
            );

            return false;
        }

        // 제거와 추가를 한 번의 대입으로 처리해 중간에 "돌이 없는" 상태가 생기지 않게 한다.
        _stones[index] = result;

        /*
         * 모든 변경이 완료된 후에만 재조합기를 소모한다.
         * SpendReforger 내부에서 InventoryChanged 이벤트를 호출한다.
         */
        if (!SpendReforger(1))
        {
            Debug.LogError(
                "[재조합기] 결과 적용 후 재조합기 소모에 실패했습니다."
            );

            return false;
        }

        Debug.Log(
            $"[재조합기] 인벤토리 직접 재조합 완료 — " +
            $"'{targetStone.stoneName}' → '{result.stoneName}' / " +
            $"남은 재조합기 {ReforgerCount}개 / " +
            $"인벤토리 {InventoryCount}/{MAX_INVENTORY_SIZE}"
        );

        return true;
    }

    /// <summary>
    /// ItemDatabase의 전체 목록에서 null이 아닌 일반 장비만 반환한다.
    /// 동일한 아이템이 다시 등장하는 것은 허용한다.
    /// </summary>
    private static List<ItemData> GetValidItemCandidates(
        List<ItemData> allItems)
    {
        var candidates =
            new List<ItemData>();

        if (allItems == null)
            return candidates;

        foreach (ItemData item in allItems)
        {
            if (item != null)
                candidates.Add(item);
        }

        return candidates;
    }

    /// <summary>
    /// EvolutionStoneDatabase의 전체 목록에서
    /// null이 아닌 진화의 돌만 반환한다.
    ///
    /// excludedStone이 null이면(기존 유닛 대상 재조합) 동일한 돌이 다시 등장하는 것도 허용한다.
    /// excludedStone을 넘기면(인벤토리 직접 재조합) 그 돌만 후보에서 제외한다.
    /// </summary>
    private static List<EvolutionStoneData> GetValidStoneCandidates(
        List<EvolutionStoneData> allStones,
        EvolutionStoneData excludedStone = null)
    {
        var candidates =
            new List<EvolutionStoneData>();

        if (allStones == null)
            return candidates;

        foreach (EvolutionStoneData stone in allStones)
        {
            if (stone == null)
                continue;

            if (excludedStone != null && stone == excludedStone)
                continue;

            candidates.Add(stone);
        }

        return candidates;
    }

    // ──────────────────────────────────────────
    // 공통 검사
    // ──────────────────────────────────────────

    /// <summary>
    /// 전투 페이즈이고 대상 유닛이 필드에 있는지 확인한다.
    /// </summary>
    private static bool IsBattlePhaseAndUnitOnBoard(
        PokemonUnit unit)
    {
        if (!GameManager.TryGet(out var gm) || unit == null)
            return false;

        bool isBattlePhase =
            gm.Phase != null
            && gm.Phase.CurrentPhase == GamePhase.Battle;

        if (!isBattlePhase)
            return false;

        return gm.Board != null
            && gm.Board.GetUnitsOnBoard().Contains(unit);
    }

    /// <summary>
    /// 유닛에게 장착된 일반 장비와 진화의 돌의 총개수를 반환한다.
    /// </summary>
    private static int GetEquippedObjectCount(
        PokemonUnit unit)
    {
        if (unit == null)
            return 0;

        int count = unit.items?.Count ?? 0;

        if (unit.equippedStone != null)
            count++;

        return count;
    }

    /// <summary>
    /// 유닛의 장착물을 모두 인벤토리로 반환할 공간이 있는지 확인한다.
    ///
    /// 제거기 슬롯과 재조합기 슬롯은 반환 공간으로 계산하지 않는다.
    /// 재조합기를 소모하며 생길 슬롯도 미리 빈칸으로 계산하지 않는다.
    /// </summary>
    private bool HasSpaceForEquippedObjects(
        PokemonUnit unit,
        string toolName)
    {
        int requiredSpace =
            GetEquippedObjectCount(unit);

        if (requiredSpace <= 0)
        {
            Debug.Log(
                $"[{toolName}] '{unit?.data?.pokemonName}'에게 " +
                "장착된 아이템이 없습니다.");

            return false;
        }

        if (AvailableInventorySpace < requiredSpace)
        {
            Debug.Log(
                $"[{toolName}] 인벤토리 공간 부족 — " +
                $"필요 {requiredSpace}칸, " +
                $"빈칸 {AvailableInventorySpace}칸, " +
                $"현재 {InventoryCount}/{MAX_INVENTORY_SIZE}");

            return false;
        }

        return true;
    }
}