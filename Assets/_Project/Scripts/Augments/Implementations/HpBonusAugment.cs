// MOCKUP: 수치/효과 기획 확정 전. 패턴 예시용.
using UnityEngine;

/// <summary>[Silver] 모든 유닛 최대 체력 +150</summary>
public class HpBonusAugment : Augment
{
    private const float BONUS = 150f;

    public override void Apply()
    {
        // TODO: BoardManager에서 전체 유닛 목록 받아서 적용
        // foreach unit: unit.data.hp += BONUS (기욱이 BoardManager 완성 후 연결)
        Debug.Log($"[Augment] HP +{BONUS} 적용");
    }

    public override void Remove()
    {
        // TODO: 보너스 롤백
    }
}
