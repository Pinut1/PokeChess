using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 확정 증강 7종의 표시 정보(AugmentData) 정의부.
/// 기획 시트가 없어(덱기획 v1 문서가 원본) 코드로 생성한다 — 시트 확정 시 JSON 임포터로 전환.
/// 티어 배정은 기획 미명시 PLACEHOLDER: 영웅증강 = Prismatic, 4코 오픈 = Gold, 나머지 = Silver.
/// </summary>
public static class AugmentCatalog
{
    private static List<AugmentData> _all;

    /// <summary>전체 증강 풀(7종). 최초 접근 시 생성 후 캐시.</summary>
    public static IReadOnlyList<AugmentData> All => _all ??= Build();

    private static List<AugmentData> Build() => new()
    {
        Define(1, AugmentId.GoldInterest, "이자", "GoldInterest", AugmentTier.Silver,
               "즉시 50골드를 얻고, 라운드마다 보유 골드 10당 이자를 1골드 추가로 받는다 (최대 10골드)."),
        Define(2, AugmentId.LevelDiscount, "레벨 할인", "LevelDiscount", AugmentTier.Silver,
               "XP 구매 비용이 2골드 감소한다."),
        Define(3, AugmentId.RerollRefund, "리롤 환급", "RerollRefund", AugmentTier.Silver,
               "상점 리롤 시 45% 확률로 리롤 1회를 돌려받는다."),
        Define(4, AugmentId.PachirisuHero, "영웅증강: 파치리스", "PachirisuHero", AugmentTier.Prismatic,
               "파치리스 1마리를 얻고 리롤 3회를 받는다. 모든 파치리스가 탱커가 되어 날따름(도발)을 사용한다."),
        Define(5, AugmentId.EeveeHero, "영웅증강: 이브이", "EeveeHero", AugmentTier.Prismatic,
               "이브이 1마리와 리롤 3회를 받는다. 이브이는 진화하지 않는 대신 능력치 ×1.4, 3성 달성 시 전투마다 돌연변이 4마리를 소환한다."),
        Define(6, AugmentId.FourCostShopOpen, "4코 상점 오픈", "FourCostShopOpen", AugmentTier.Gold,
               "즉시 상점이 4코스트 5마리로 갱신되고, 이후 레벨과 무관하게 4코스트가 상점에 등장한다."),
        Define(7, AugmentId.Quarry, "채석가", "Quarry", AugmentTier.Silver,
               "매 라운드 진화의 돌 1개를 받는다."),
    };

    private static AugmentData Define(int id, AugmentId augmentId, string name, string nameEn,
                                      AugmentTier tier, string description)
    {
        var data = ScriptableObject.CreateInstance<AugmentData>();
        data.name          = $"Augment_{nameEn}";
        data.id            = id;
        data.augmentId     = augmentId;
        data.augmentName   = name;
        data.augmentNameEn = nameEn;
        data.tier          = tier;
        data.description   = description;
        return data;
    }
}
