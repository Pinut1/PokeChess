using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>스테이지(라운드) 유형.</summary>
public enum StageType
{
    WildCommon,     // 야생 - 일반
    WildRare,       // 야생 - 희귀
    GymBattle,      // 관장전
    ChampionBattle  // 챔피언(최종 보스)전
}

/// <summary>전투 시작 전 발생하는 보상/선택 이벤트.</summary>
public enum PreStageReward
{
    None,
    AugmentChoice,  // 증강 3택1
    ItemReward,     // 아이템 보상
    CompanionItem   // 컴패니언 아이템
}

/// <summary>
/// 적 보드 한 칸에 배치되는 유닛 하나.
/// pokemonNameEn은 플레이어가 쓰는 PokemonData 풀을 그대로 참조한다(보스 전용 종 불필요).
/// 미확정 슬롯은 "DUMMY"로 두고 기획이 나중에 교체 → grep "DUMMY"로 추적.
/// </summary>
[Serializable]
public class EnemyPlacement
{
    public string pokemonNameEn;   // 코드 참조용 영문명 (예: "Magikarp"). 미정이면 "DUMMY".
    public string pokemonNameKr;   // 시트/인스펙터 가독용 한글명 (게임 로직 미사용).
    public int    starLevel = 1;   // 1~3 ★
    public int    q;               // 헥스 좌표 q (적 보드 기준, 임시값 — 기획 확정 필요)
    public int    r;               // 헥스 좌표 r
    public string heldItemEn;      // (옵션) 트레이너 보유 아이템 영문명

    // ── 보스/시그니처 강화 ─────────────────────────
    // 같은 PokemonData를 스테이지마다 다른 강도로 쓰기 위한 배수. ★ 스케일 위에 추가로 곱해짐.
    // 1 = 기본(플레이어와 동일). 예: 꼭두 밀탱크 2.0, 그린 망나뇽 2.5.
    public float statMultiplier = 1f;     // 전 스탯 일괄 배수
    public float hpMultiplier   = 1f;     // (옵션) HP만 추가 배수 — 탱커형 보스용
    public float atkMultiplier  = 1f;     // (옵션) 공격력만 추가 배수
    // 최종 = base × StarMultiplier × statMultiplier × (스탯별 배수)
}

/// <summary>
/// 한 스테이지(예: 1-3)의 적 구성 + 사전 이벤트 정의.
/// 적 종은 확정, 별/좌표/잡몹은 기획이 채우는 칸. BattleManager가 전투 시 이걸 읽어 적 팀 생성.
///
/// SO가 아닌 일반 직렬화 클래스 — 중앙 <see cref="StageDatabase"/>(SO 1개)가 List로 인라인 보유한다.
/// 스테이지마다 .asset을 쪼개지 않아 파일/머지 노이즈를 없앤다(적 참조가 영문명 문자열이라 SO 객체참조 불필요).
/// </summary>
[Serializable]
public class StageData
{
    [Header("식별")]
    public string stageId;          // "1-1"
    public int    chapter = 1;
    public int    round   = 1;
    public int    order;            // 진행 순서(정렬용)

    [Header("유형")]
    public StageType      stageType;
    public PreStageReward preReward = PreStageReward.None; // 전투 전 증강/아이템 이벤트

    [Header("트레이너 (Wild면 비움)")]
    public string trainerName;      // "웅이", "그린" 등

    [Header("적 구성")]
    public List<EnemyPlacement> enemies = new();

    [Header("보상")]
    public int rewardTableId;       // RewardData.rewardTableId 참조 (역기획서 수치 확정 필요)
}
