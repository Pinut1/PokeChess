using UnityEngine;

/// <summary>
/// ECONOMY_SHOP "구독서비스": 서로 다른 4코스트 5마리 상점을 100% 확정으로 총 2회 오픈.
/// ① 선택 즉시 1회 ② 1~3라운드 범위에서 1회 추가(선택이 R2라 실질 R3, 범위 지나면 다음 라운드 폴백).
/// 매 발동 시 4골드 지급. (Augment Table v2 — 구 "15% 확률" 구현은 확정 발동으로 정정됨)
/// </summary>
public class EconomyShopAugment : Augment
{
    private const int GOLD_PER_TRIGGER = 4;
    private const int SECOND_TRIGGER_MAX_ROUND = 3; // triggerTiming ROUND_1-3

    private bool _secondFired;

    public override void Apply() => Fire("선택 즉시");

    public override void OnRoundChanged(int round)
    {
        if (_secondFired) return;

        // 범위(≤3) 안에서 발동. 선택이 3라운드 이후였다면(현재 기획상 없음) 다음 라운드에 폴백 발동.
        if (round > SECOND_TRIGGER_MAX_ROUND)
            Debug.LogWarning($"[Augment] 구독서비스 2차 발동이 기획 범위(1~{SECOND_TRIGGER_MAX_ROUND}R)를 벗어남 — {round}R에 폴백 발동");

        _secondFired = true;
        Fire($"{round}R 추가");
    }

    private static void Fire(string label)
    {
        var shop = GameManager.Instance.Shop;
        if (shop == null)
        {
            Debug.LogWarning("[Augment] ShopManager 없음 — 구독서비스 발동 실패");
            return;
        }

        shop.OpenCostFourShopOnce();
        shop.AddGold(GOLD_PER_TRIGGER);
        Debug.Log($"[Augment] 구독서비스 발동({label}): 4코 상점 오픈 + {GOLD_PER_TRIGGER}G");
    }
}
