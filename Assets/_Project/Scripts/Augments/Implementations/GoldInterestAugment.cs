using UnityEngine;

/// <summary>
/// 이자 증강: 10골드당 이자 +1(기본 1 → 2, 최대 이자 5 → 10) + 즉시 50골드 지급.
/// 이자 계산/지급 본체는 ShopManager(§7.5) — 여기선 이자율 가산 seam만 호출.
/// </summary>
public class GoldInterestAugment : Augment
{
    private const int INTEREST_DELTA   = 1;
    private const int IMMEDIATE_GOLD   = 50;

    public override void Apply()
    {
        var shop = GameManager.Instance.Shop;
        if (shop == null)
        {
            Debug.LogWarning("[Augment] ShopManager 없음 — 이자 증강 적용 실패");
            return;
        }

        shop.AddInterestPerTenGold(INTEREST_DELTA);
        shop.AddGold(IMMEDIATE_GOLD);
        Debug.Log($"[Augment] 이자 증강: 이자율 +{INTEREST_DELTA}, 즉시 +{IMMEDIATE_GOLD}G");
    }

    public override void Remove()
    {
        var shop = GameManager.Instance != null ? GameManager.Instance.Shop : null;
        shop?.AddInterestPerTenGold(-INTEREST_DELTA); // 즉시지급 골드는 회수하지 않음
    }
}
