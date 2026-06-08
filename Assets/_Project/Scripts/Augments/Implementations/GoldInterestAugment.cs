// MOCKUP: 수치/효과 기획 확정 전. 패턴 예시용.
using UnityEngine;

/// <summary>[Silver] 라운드 시작마다 보유 골드의 10% 추가 지급 (최대 5골드)</summary>
public class GoldInterestAugment : Augment
{
    private const float RATE    = 0.1f;
    private const int   MAX_INT = 5;

    public override void OnRoundChanged(int round)
    {
        // TODO: ShopManager에서 현재 골드 읽어서 계산
        // int current = GameManager.Instance.Shop.Gold;
        // int interest = Mathf.Min(Mathf.FloorToInt(current * RATE), MAX_INT);
        // GameEvents.GoldChanged(current + interest);
        Debug.Log($"[Augment] 이자 지급 (라운드 {round})");
    }
}
