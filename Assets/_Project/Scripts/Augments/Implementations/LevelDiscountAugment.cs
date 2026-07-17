using UnityEngine;

/// <summary>
/// 레벨 할인 증강: XP 구매 골드 비용을 낮춘다.
/// 할인폭은 기획 미확정 — PLACEHOLDER 2 (기본 4G → 2G).
/// </summary>
public class LevelDiscountAugment : Augment
{
    private const int DISCOUNT_GOLD = 2; // PLACEHOLDER — 기획 확정 후 교체

    public override void Apply()
    {
        var shop = GameManager.Instance.Shop;
        if (shop == null)
        {
            Debug.LogWarning("[Augment] ShopManager 없음 — 레벨 할인 적용 실패");
            return;
        }

        shop.AddBuyXpCostDiscount(DISCOUNT_GOLD);
        Debug.Log($"[Augment] 레벨 할인: XP 구매 비용 -{DISCOUNT_GOLD}G => {shop.BuyXpCostGold}G");
    }

    public override void Remove()
    {
        var shop = GameManager.Instance != null ? GameManager.Instance.Shop : null;
        shop?.AddBuyXpCostDiscount(-DISCOUNT_GOLD);
    }
}
