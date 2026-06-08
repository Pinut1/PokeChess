// MOCKUP: AugmentId 확정 후 case 목록 전면 교체. 구조(패턴)는 유지.
using UnityEngine;

/// <summary>
/// AugmentId → 구체 Augment 인스턴스 생성.
/// 새 증강 추가 시 여기에만 case 하나 추가하면 됨.
/// </summary>
public static class AugmentFactory
{
    public static Augment Create(AugmentData data)
    {
        Augment augment = data.augmentId switch
        {
            AugmentId.HpBonus      => new HpBonusAugment(),
            AugmentId.AttackBonus  => new AttackBonusAugment(),
            AugmentId.GoldInterest => new GoldInterestAugment(),
            AugmentId.BattleHeal   => new BattleHealAugment(),
            _ => throw new System.NotImplementedException(
                     $"[AugmentFactory] {data.augmentId} 미구현 — Augment 클래스 추가 필요")
        };

        augment.Initialize(data);
        return augment;
    }
}
