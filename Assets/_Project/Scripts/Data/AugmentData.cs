// MOCKUP: 기획 확정 전 목업. AugmentId 목록 / AugmentTier 등급명 변경 가능.
using UnityEngine;

public enum AugmentTier
{
    Silver,
    Gold,
    Prismatic
}

// MOCKUP: 아래 항목은 예시. 기획팀 증강 목록 확정 후 전면 교체 예정.
public enum AugmentId
{
    HpBonus,
    AttackBonus,
    GoldInterest,
    BattleHeal,
}

[CreateAssetMenu(menuName = "PokeChess/Augment", fileName = "NewAugment_Data")]
public class AugmentData : ScriptableObject
{
    [Header("기본 정보")]
    public int id;
    public AugmentId augmentId;
    public string augmentName;
    public string augmentNameEn;
    public AugmentTier tier;

    [Header("설명")]
    [TextArea] public string description;

    [Header("에셋 참조")]
    public Sprite icon;
}
