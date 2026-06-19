using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>보상 한 항목의 종류.</summary>
public enum RewardKind
{
    Gold,            // 골드 (amount = 골드량)
    Item,            // 완성 아이템 (refNameEn = ItemData 영문명, amount = 개수)
    Consumable,      // 소모품 (refNameEn = ConsumableData 영문명)
    EvolutionStone,  // 진화의 돌 (refNameEn = EvolutionStoneData 영문명)
    Unit,            // 유닛 지급 (refNameEn = PokemonData 영문명, 컴패니언 등)
    AugmentChoice    // 증강 3택1 트리거 (refNameEn 무시 — AugmentManager가 처리)
}

/// <summary>
/// 보상 테이블 한 줄. 확정 보상은 dropChance = 1, 확률 보상은 0~1.
/// 미정 슬롯은 refNameEn을 "DUMMY"로 두고 역기획서 확정 후 교체 → grep "DUMMY"로 추적.
/// </summary>
[Serializable]
public class RewardEntry
{
    public RewardKind kind;
    public int    amount = 1;          // Gold면 골드량, 그 외엔 지급 개수
    public string refNameEn;           // 참조 영문명 (Gold/AugmentChoice면 무시)
    [Range(0f, 1f)]
    public float  dropChance = 1f;     // 1 = 확정, <1 = 확률 보상
}

/// <summary>
/// 스테이지 클리어 보상 테이블 한 개. StageData.rewardTableId가 이 rewardTableId를 참조한다.
/// 적 구성과 분리해 여러 스테이지가 같은 보상 테이블을 재사용할 수 있게 한다.
///
/// SO가 아닌 일반 직렬화 클래스 — 중앙 <see cref="RewardDatabase"/>(SO 1개)가 List로 인라인 보유한다.
/// (Reward_{id}.asset을 쪼개지 않아 파일/머지 노이즈 제거 + 런타임 단일 로드. <see cref="StageData"/>와 동일 패턴.)
/// 실제 수치는 역기획서 확정 후 채움.
/// </summary>
[Serializable]
public class RewardData
{
    [Header("식별")]
    public int    rewardTableId;       // StageData.rewardTableId가 참조하는 키
    public string label;               // 가독용 (예: "1-3 클리어 보상")

    [Header("보상 목록")]
    public List<RewardEntry> rewards = new();
}
