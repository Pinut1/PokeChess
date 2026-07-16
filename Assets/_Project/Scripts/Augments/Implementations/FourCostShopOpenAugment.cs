using UnityEngine;

/// <summary>
/// 4코 상점 오픈 증강: 레벨과 무관하게 상점에 4코스트가 등장하도록 강제 오픈하고,
/// 선택 즉시 상점을 4코스트 5마리로 무료 리롤한다("5마리 즉시" — 덱기획 v1).
/// 등장 확률 수치는 기획 미확정 — ShopManager.ForceOpenCostFour의 PLACEHOLDER 참조.
/// </summary>
public class FourCostShopOpenAugment : Augment
{
    public override void Apply()
    {
        var shop = GameManager.Instance.Shop;
        if (shop == null)
        {
            Debug.LogWarning("[Augment] ShopManager 없음 — 4코 상점 오픈 실패");
            return;
        }

        shop.ForceOpenCostFour();
        Debug.Log("[Augment] 4코 상점 오픈: 즉시 4코 5마리 + 이후 4코 등장 상시 오픈");
    }
}
