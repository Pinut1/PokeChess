using UnityEngine;

/// <summary>
/// ECONOMY_GOLD "골드를 획득했다!": 즉시 50골드 + 10골드당 이자 +1(기본 1 → 2, 최대 이자 5 → 10).
/// 이자 계산/지급 본체는 ShopManager(§7.5) — 여기선 이자율 가산 seam만 호출. (Augment Table v2 확정)
/// </summary>
public class EconomyGoldAugment : Augment
{
    private const int INTEREST_DELTA = 1;
    private const int IMMEDIATE_GOLD = 50;

    public override void Apply()
    {
        var shop = GameManager.Instance.Shop;
        if (shop == null)
        {
            Debug.LogWarning("[Augment] ShopManager 없음 — 골드를 획득했다! 적용 실패");
            return;
        }

        shop.AddInterestPerTenGold(INTEREST_DELTA);
        shop.AddGold(IMMEDIATE_GOLD);
        Debug.Log($"[Augment] 골드를 획득했다!: 이자율 +{INTEREST_DELTA}, 즉시 +{IMMEDIATE_GOLD}G");
    }

    public override void Remove()
    {
        var shop = GameManager.Instance != null ? GameManager.Instance.Shop : null;
        shop?.AddInterestPerTenGold(-INTEREST_DELTA); // 즉시지급 골드는 회수하지 않음
    }

    /// <summary>
    /// 재접속 복원 — 이자율 가산(ShopManager는 씬마다 새로 생성되어 유실됨)만 재적용.
    /// 즉시 지급 골드는 재지급하지 않는다(이미 복원된 Gold 값에 반영되어 있음 — 재지급 시 중복 지급됨).
    /// </summary>
    public override void Restore()
    {
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        if (shop == null)
        {
            Debug.LogWarning("[Augment] ShopManager 없음 — 골드를 획득했다! 복원 실패");
            return;
        }

        shop.AddInterestPerTenGold(INTEREST_DELTA);
        Debug.Log($"[Augment] 골드를 획득했다! 복원: 이자율 +{INTEREST_DELTA}(즉시 골드 재지급 없음)");
    }
}
