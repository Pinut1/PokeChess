using UnityEngine;

/// <summary>
/// REROLL_TICKET "하이퍼 티켓": 즉시 무료 리롤 5회 + 수동 리롤 1회 소모 시 45% 확률로 무료 리롤 1개 환급.
/// ShopManager.Reroll()이 발행하는 GameEvents.OnRerollSpent 훅 기반. (Augment Table v2 확정)
/// </summary>
public class RerollTicketAugment : Augment
{
    private const int   IMMEDIATE_REROLLS = 5;
    private const float REFUND_CHANCE     = 0.45f;

    public override void Apply()
    {
        var shop = GameManager.Instance.Shop;
        if (shop == null)
        {
            Debug.LogWarning("[Augment] ShopManager 없음 — 하이퍼 티켓 적용 실패");
            return;
        }

        shop.AddReroll(IMMEDIATE_REROLLS);
        Debug.Log($"[Augment] 하이퍼 티켓: 즉시 무료 리롤 +{IMMEDIATE_REROLLS}");
    }

    public override void OnRerollSpent()
    {
        if (Random.value >= REFUND_CHANCE) return;

        var shop = GameManager.Instance.Shop;
        if (shop == null) return;

        shop.AddReroll(1);
        Debug.Log("[Augment] 하이퍼 티켓 환급 발동 (45%) — 무료 리롤 +1");
    }
}
