using UnityEngine;

/// <summary>
/// 증강 등급. Augment Table v2 네이밍 매핑: MONSTERBALL=Silver / SUPERBALL=Gold / MASTERBALL=Prismatic.
/// 현재 기획은 전체 SUPERBALL(=Gold) 단일 — 등급 분화는 추후 확장.
/// </summary>
public enum AugmentTier
{
    Silver,    // MONSTERBALL
    Gold,      // SUPERBALL
    Prismatic  // MASTERBALL
}

/// <summary>
/// 확정 증강 6종 (Augment Table v2, 2026-07-16 해인님 회신 반영 — 레벨 할인은 목록에서 제외됨).
/// enum 이름은 v2 augmentId와 1:1 매핑(EconomyGold ↔ ECONOMY_GOLD).
/// 새 증강 추가 시: 여기 enum → Augment 상속 클래스 → AugmentFactory → AugmentCatalog 순으로 등록.
/// </summary>
public enum AugmentId
{
    EconomyGold,    // ECONOMY_GOLD — 골드를 획득했다! (즉시 50G + 이자율 2)
    EconomyShop,    // ECONOMY_SHOP — 구독서비스 (4코 상점 확정 오픈 2회 + 회당 4G)
    HeroPachirisu,  // HERO_PACHIRISU — 기술머신:날따름 (탱커 전환 + 도발 + ×1.4)
    HeroEevee,      // HERO_EEVEE — 기술머신:나인이볼부스트 (진화잠금 + 마법사 전환 + ×1.4)
    RerollTicket,   // REROLL_TICKET — 하이퍼 티켓 (즉시 리롤 5 + 45% 환급)
    GambleStone,    // GAMBLE_STONE — 진화 스페셜리스트 (R2~5 매 라운드 돌 1개 + 최초 재조합기+2G)
}

/// <summary>
/// 증강 표시 정보(이름/설명/등급). 효과 로직은 Augment 구현 클래스가 본체.
/// 기획 시트가 없어(Augment Table v2 md 문서가 원본) AugmentCatalog가 코드로 정의를 생성한다 —
/// 시트가 생기면 임포터 파이프라인(JSON → SO)으로 전환.
/// </summary>
[CreateAssetMenu(menuName = "PokeChess/Augment", fileName = "NewAugment_Data")]
public class AugmentData : ScriptableObject
{
    [Header("기본 정보")]
    public int id;
    public AugmentId augmentId;
    public string augmentName;
    public string augmentNameEn;   // v2 augmentId 문자열 (예: "ECONOMY_GOLD") — 전적 기록/시트 조인 키
    public AugmentTier tier;

    [Header("설명")]
    [TextArea] public string description;

    [Header("에셋 참조")]
    public Sprite icon;
}
