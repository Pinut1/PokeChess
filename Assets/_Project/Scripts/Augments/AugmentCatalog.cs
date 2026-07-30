using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 확정 증강 6종의 표시 정보(AugmentData) 정의부 — Augment Table v2(2026-07-16) 기준.
/// 기획 시트가 없어(md 문서가 원본) 코드로 생성한다 — 시트 확정 시 JSON 임포터로 전환.
/// 등급은 전체 SUPERBALL(=Gold) 고정(v2 확정, 분화는 추후 확장).
/// </summary>
public static class AugmentCatalog
{
    private static List<AugmentData> _all;

    /// <summary>전체 증강 풀(6종). 최초 접근 시 생성 후 캐시.</summary>
    public static IReadOnlyList<AugmentData> All => _all ??= Build();

    private static List<AugmentData> Build() => new()
    {
        Define(1, AugmentId.EconomyGold, "골드를 획득했다!", "ECONOMY_GOLD",
               "즉시 50골드를 획득합니다. 이자 수익이 2골드로 증가합니다. 이자는 저축한 10골드당 추가로 획득하는 골드입니다."),
        Define(2, AugmentId.EconomyShop, "구독서비스", "ECONOMY_SHOP",
               "100% 확정으로 서로 다른 4단계 포켓몬으로 구성된 상점을 엽니다. 즉시 1회, 1~3라운드 중 1회 추가로 총 2회 발동하며, 매 발동 시 4골드를 획득합니다."),
        Define(3, AugmentId.HeroPachirisu, "기술머신:날따름", "HERO_PACHIRISU",
               "파치리스를 획득합니다. 아군 파치리스가 날따름 스킬을 배우고 탱커로 변하며 능력치가 1.4배가 됩니다."),
        Define(4, AugmentId.HeroEevee, "기술머신:나인이볼부스트", "HERO_EEVEE",
               "이브이를 획득합니다. 이브이가 진화를 참아내고(진화 잠금) 마법사로 변하며 능력치가 1.4배가 됩니다. 3성 달성 시 전투에서 진화한 동료들을 소환합니다."),
        Define(5, AugmentId.RerollTicket, "하이퍼 티켓", "REROLL_TICKET",
               "즉시 무료 상점 새로고침 5회를 얻고, 상점을 새로고침할 때마다 45% 확률로 무료 새로고침을 획득합니다."),
        Define(6, AugmentId.GambleStone, "진화 스페셜리스트", "GAMBLE_STONE",
               "5라운드까지 매 라운드 시작 시 무작위 진화의 돌 1개씩을 받습니다. 최초 지급 시 재조합기와 2골드도 함께 증정합니다."),
    };

    private static AugmentData Define(int id, AugmentId augmentId, string name, string nameEn, string description)
    {
        var data = ScriptableObject.CreateInstance<AugmentData>();
        data.name          = $"Augment_{nameEn}";
        data.id            = id;
        data.augmentId     = augmentId;
        data.augmentName   = name;
        data.augmentNameEn = nameEn;
        data.tier          = AugmentTier.Gold; // 전체 SUPERBALL(=Gold) 고정 — v2 확정
        data.description   = description;
        data.icon          = AugmentIconDatabase.Find(augmentId); // 미지정이면 null(선택 데이터)
        return data;
    }
}
