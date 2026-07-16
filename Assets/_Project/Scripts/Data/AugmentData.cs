using UnityEngine;

public enum AugmentTier
{
    Silver,
    Gold,
    Prismatic
}

/// <summary>
/// 확정 증강 7종 (GAP_2026-07-01_slice-1-5.md §2.B — 덱기획 v1 유도 증강).
/// 새 증강 추가 시: 여기 enum → Augment 상속 클래스 → AugmentFactory → AugmentCatalog 순으로 등록.
/// </summary>
public enum AugmentId
{
    GoldInterest,      // 이자 +1 (10골드당) + 즉시 50골드
    LevelDiscount,     // XP 구매 비용 할인
    RerollRefund,      // 리롤 소모 시 45% 확률 환급
    PachirisuHero,     // 파치리스 영웅증강 (탱커 전환 + 도발 부여 + 즉시지급 1 + 리롤 3)
    EeveeHero,         // 이브이 영웅증강 (진화잠금 + ×1.4 + 3성 봇소환 + 즉시지급 1 + 리롤 3)
    FourCostShopOpen,  // 4코스트 상점 강제 오픈 (즉시 4코 5마리)
    Quarry,            // 채석가 — 매 라운드 진화의 돌 1개
}

/// <summary>
/// 증강 표시 정보(이름/설명/티어). 효과 로직은 Augment 구현 클래스가 본체.
/// 기획 시트가 없어(덱기획 v1 문서 기반) AugmentCatalog가 코드로 정의를 생성한다 —
/// 시트가 생기면 임포터 파이프라인(JSON → SO)으로 전환.
/// </summary>
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
