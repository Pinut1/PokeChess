using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 상점 카드 4장. 카드 내용 동기화는 ShopBarUIBase가 담당하고,
/// 여기선 "아이템 상점 갱신 이벤트/데이터/구매 호출이 무엇인지"만 정의한다(UnitShopBarUI와 동일 구조).
/// 슬롯 규칙(0=진화의 돌, 1~3=일반 아이템)은 ShopManager.RollItemShop이 정하므로
/// UI는 슬롯 인덱스를 그대로 따라가기만 하면 된다.
/// 유닛 상점과 다른 점은 (1) 카드마다 개별 리롤 버튼이 있고 (2) 구매 불가 시 블로커를 덮는다는 것이라,
/// 베이스의 구매 배선에 더해 두 가지를 여기서 추가로 연결한다.
/// </summary>
public class ItemShopBarUI : ShopBarUIBase<ScriptableObject, ItemCardUI>
{
    [Header("설명창")]
    [Tooltip("카드에 커서를 올리면 띄울 아이템 설명창 컨트롤러. 비우면 설명창이 뜨지 않는다. " +
             "인벤토리와 같은 컨트롤러를 물려도 된다 — 소유자로 구분하므로 서로 꺼뜨리지 않는다.")]
    [SerializeField] private ItemTooltipController _tooltip;

    protected override void Awake()
    {
        base.Awake(); // 구매 클릭 배선

        for (int i = 0; i < _cards.Length; i++)
        {
            int slotIndex = i; // 클로저 캡처용 로컬 복사
            _cards[i].RerollClicked += () => RerollSlot(slotIndex);
            _cards[i].Hovered += HandleHovered;
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable(); // Unsubscribe

        // 상점을 닫는데 설명창만 남는 일이 없도록(유닛 상점으로 전환할 때).
        if (_tooltip != null) _tooltip.HideAll();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _cards.Length; i++)
            if (_cards[i] != null) _cards[i].Hovered -= HandleHovered;
    }

    /// <summary>카드에 커서가 들고 날 때 설명창을 여닫는다.</summary>
    private void HandleHovered(ItemCardUI card, bool entered)
    {
        if (_tooltip == null) return;

        if (entered) _tooltip.Show(card, card.CurrentData);
        else         _tooltip.Hide(card);
    }

    protected override void OnEnable()
    {
        base.OnEnable(); // Subscribe + Refresh
        RefreshRerollButtons();
        RefreshPurchasable();
    }

    // 리롤/구매/라운드 갱신은 OnItemShopRerolled로, 구매 가능 여부는 쿠폰·인벤토리 변동으로 갱신된다.
    protected override void Subscribe()
    {
        GameEvents.OnItemShopRerolled += RefreshAll;
        GameEvents.OnItemCouponChanged += HandleCouponChanged;
        GameEvents.OnInventoryChanged += RefreshPurchasable;
    }

    protected override void Unsubscribe()
    {
        GameEvents.OnItemShopRerolled -= RefreshAll;
        GameEvents.OnItemCouponChanged -= HandleCouponChanged;
        GameEvents.OnInventoryChanged -= RefreshPurchasable;
    }

    protected override IReadOnlyList<ScriptableObject> GetSlots()
    {
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        return shop != null ? shop.CurrentItemSlots : null;
    }

    protected override void Buy(int slotIndex)
    {
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        shop?.BuyItem(slotIndex);
    }

    private void RerollSlot(int slotIndex)
    {
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        shop?.RerollItemSlot(slotIndex);
    }

    private void HandleCouponChanged(int _) => RefreshPurchasable();

    private void RefreshAll()
    {
        Refresh(); // 카드 내용 바인딩 / Sold 처리
        RefreshRerollButtons();
        RefreshPurchasable();
    }

    /// <summary>라운드 갱신 시 전부 다시 켜지고, 리롤을 쓴 슬롯만 꺼진다.</summary>
    private void RefreshRerollButtons()
    {
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;

        for (int i = 0; i < _cards.Length; i++)
            _cards[i].SetRerollAvailable(shop != null && shop.CanRerollItemSlot(i));
    }

    /// <summary>
    /// 쿠폰 부족·인벤토리 만석이면 카드를 블로커로 덮는다.
    /// 사유는 ShopManager.BuyItem의 실패 조건과 같은 순서로 판정해 실제 동작과 어긋나지 않게 한다.
    /// 빈 슬롯(구매됨)은 SetSold가 처리하므로 건너뛴다.
    /// </summary>
    private void RefreshPurchasable()
    {
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        if (shop == null) return;

        var slots = GetSlots();
        string reason = GetBlockReason(shop, gm.Item);

        for (int i = 0; i < _cards.Length; i++)
        {
            bool hasProduct = slots != null && i < slots.Count && slots[i] != null;
            if (!hasProduct) continue;

            _cards[i].SetPurchasable(reason == null, reason);
        }
    }

    /// <summary>구매를 막는 사유. 없으면 null.</summary>
    private static string GetBlockReason(ShopManager shop, ItemManager item)
    {
        if (item == null) return "아이템 정보를 불러올 수 없습니다";

        if (!item.HasInventorySpace) return "인벤토리가 가득 찼습니다";

        if (item.ItemCoupon < shop.ItemPrice)
            return $"아이템 쿠폰이 부족합니다 (필요 {shop.ItemPrice} / 보유 {item.ItemCoupon})";

        return null;
    }
}
