using UnityEngine;

/// <summary>
/// ECONOMY_SHOP "구독서비스".
///
/// 증강 선택 즉시 현재 상점을
/// 서로 다른 4코스트 5마리 상점으로 한 번 교체하고
/// 4골드를 지급한다.
///
/// 효과는 선택 순간 한 번만 발동한다.
/// 이후 수동 리롤과 다음 라운드 자동 갱신은
/// 현재 플레이어 레벨의 기본 상점 확률을 사용한다.
/// </summary>
public class EconomyShopAugment : Augment
{
    private const int GOLD_REWARD = 4;

    public override void Apply()
    {
        ShopManager shop =
            GameManager.Instance != null
                ? GameManager.Instance.Shop
                : null;

        if (shop == null)
        {
            Debug.LogWarning(
                "[Augment] ShopManager 없음 — 구독서비스 적용 실패"
            );

            return;
        }

        shop.OpenCostFourShopOnce();
        shop.AddGold(GOLD_REWARD);

        Debug.Log(
            $"[Augment] 구독서비스 적용: " +
            $"현재 상점을 4코스트 상점으로 1회 교체 + " +
            $"{GOLD_REWARD}G"
        );
    }
}