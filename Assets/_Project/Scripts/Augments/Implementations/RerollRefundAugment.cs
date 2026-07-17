using UnityEngine;

/// <summary>
/// 리롤 환급 증강: 수동 리롤 1회 소모 시 45% 확률로 무료 리롤 1개 환급.
/// ShopManager.Reroll()이 발행하는 GameEvents.OnRerollSpent 훅 기반(리롤 자원화 선행 완료).
/// </summary>
public class RerollRefundAugment : Augment
{
    private const float REFUND_CHANCE = 0.45f;

    public override void OnRerollSpent()
    {
        if (Random.value >= REFUND_CHANCE) return;

        var shop = GameManager.Instance.Shop;
        if (shop == null) return;

        shop.AddReroll(1);
        Debug.Log("[Augment] 리롤 환급 발동 (45%) — 무료 리롤 +1");
    }
}
