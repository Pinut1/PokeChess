using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 보상 지급 담당.
/// 밸런스 기획서 §7: "모든 보상은 해당 라운드 전투 시작 전 선지급"(보상 → 상점 → 덱 구성 → 전투).
/// 따라서 라운드 진입(OnStageEntered) 시 현재 스테이지의 rewardTableId로 RewardDatabase를 조회해 전액 지급한다.
/// 승/패 배수 없음 — 공유 라이프 모델(BothLose만 -1)이라 라운드 시작 시점엔 결과가 없고, 선지급이 곧 상점 예산이 된다.
/// 각 클라이언트가 자기 플레이어 몫을 지급한다(2인 각자 경제).
///
/// 스테이지 단일 출처는 RoundPhaseManager.CurrentStage. StageEntered 인자로 직접 받으므로 pull도 불필요.
/// 매니저 간 직접 참조 금지 — 트리거는 GameEvents 구독, 지급은 GameManager.Instance.X pull로만.
///
/// 골드/리롤/아이템쿠폰/아이템샵리롤/유닛/아이템/진화의 돌/소모품/
/// 재조합기(Reforger)/증강(AugmentChoice) 전부 실제 지급 연결됨.
/// </summary>
public class RewardManager : MonoBehaviour
{
    /// <summary>이미 선지급한 stageId 집합. 선지급 모델이라 같은 스테이지 재진입(재접속/UI 리싱크/중복 발행) 시 보상이 중복 지급되는 것을 막는다.</summary>
    private readonly HashSet<string> _paidStages = new();

    private void OnEnable()  => GameEvents.OnStageEntered += HandleStageEntered;
    private void OnDisable() => GameEvents.OnStageEntered -= HandleStageEntered;

    /// <summary>라운드 진입(전투 전) 시 해당 스테이지 보상을 전액 선지급.</summary>
    private void HandleStageEntered(StageData stage)
    {
        if (stage == null)
        {
            Debug.LogWarning("[Reward] StageEntered stage 없음 — 보상 스킵");
            return;
        }

        // 중복 지급 방지: 같은 stageId는 판당 1회만 선지급한다.
        if (!string.IsNullOrEmpty(stage.stageId) && !_paidStages.Add(stage.stageId))
        {
            Debug.Log($"[Reward] '{stage.stageId}' 이미 지급됨 — 중복 선지급 스킵");
            return;
        }

        // 전투 전 이벤트(preReward): 증강 3택1 트리거. 보상 테이블과 별개 필드라 여기서 분기.
        // (ItemReward/CompanionItem은 기획 미확정 — 사용 스테이지가 생기면 연결.)
        if (stage.preReward == PreStageReward.AugmentChoice)
        {
            if (GameManager.Instance.Augment != null)
            {
                GameManager.Instance.Augment.OfferChoice();
                Debug.Log($"[Reward] '{stage.stageId}' preReward — 증강 3택1 오퍼 트리거");
            }
            else
            {
                Debug.LogWarning("[Reward] AugmentManager 없음 — preReward 증강 3택1 스킵");
            }
        }

        var db = RewardDatabase.Instance;
        if (db == null) return; // Instance 접근 시 에러 로그가 이미 출력됨

        RewardData table = db.GetByTableId(stage.rewardTableId);
        if (table == null)
        {
            Debug.LogWarning($"[Reward] '{stage.stageId}'의 rewardTableId={stage.rewardTableId} 테이블을 못 찾음 — 보상 스킵");
            return;
        }

        Debug.Log($"[Reward] '{stage.stageId}' 보상 선지급: {table.label} ({table.rewards.Count}항목)");
        foreach (var entry in table.rewards)
            GrantEntry(entry, stage);
    }

    /// <summary>보상 한 항목 지급. dropChance로 확률 판정 후 종류별 분기(전액).</summary>
    private void GrantEntry(RewardEntry entry, StageData stage)
    {
        if (entry == null) return;

        // 확률 보상: 확정(>=1)이 아니면 굴려서 탈락 시 지급 안 함.
        if (entry.dropChance < 1f && Random.value > entry.dropChance)
            return;

        int amount = entry.amount;

        switch (entry.kind)
        {
            case RewardKind.Gold:
                if (amount <= 0) break;
                GameManager.Instance.Shop.AddGold(amount); // 구매/판매와 동일 경로 → 골드 동기화도 그대로 탐
                Debug.Log($"[Reward] +{amount}G");
                break;

            case RewardKind.ItemCoupon:
                if (amount <= 0) break;

                if (GameManager.Instance.Item == null)
                {
                    Debug.LogWarning("[Reward] ItemManager 없음 — 아이템 쿠폰 지급 실패");
                    break;
                }

                GameManager.Instance.Item.AddItemCoupon(amount);
                Debug.Log($"[Reward] +{amount} 아이템 쿠폰");
                break;

            case RewardKind.Item:
                if (amount > 0) GrantItem(entry.refNameEn, amount);
                break;

            // ── 종류별 보상 지급 분기 ───────────────
            case RewardKind.Consumable:
                if (amount > 0) GrantConsumable(entry.refNameEn, amount);
                break;
            case RewardKind.EvolutionStone:
                if (amount > 0) GrantStone(entry.refNameEn, amount);
                break;
            case RewardKind.Unit:
                if (amount > 0) GrantUnit(entry.refNameEn, amount);
                break;
            case RewardKind.Reroll:
                // 무료 리롤 자원 지급. ShopManager.AddReroll → GameEvents.RerollCountChanged로 HUD 동기화.
                if (amount > 0)
                {
                    GameManager.Instance.Shop.AddReroll(amount);
                    Debug.Log($"[Reward] +{amount} 무료 리롤");
                }
                break;
            case RewardKind.ItemShopReroll:
                // 폐기(2026-07-28, 기획디렉터 확정). 아이템 상점 리롤이 "공용 자원 라운드당 2개"에서
                // "카드별 라운드 1회, 누적 없음"(ShopManager.RerollItemSlot)으로 바뀌면서 지급할 자원이 없어졌다.
                // reward_data.json에는 기존 itemShopReroll 행이 그대로 남아 있으나 여기서 무시한다
                // — 데이터(구글 시트)는 손대지 않고 호출만 끊는 방식. enum/임포터 매핑도 그대로 둔다
                // (RewardKind는 int로 직렬화돼서 멤버를 지우면 Reforger가 밀려 기존 SO가 깨짐).
                break;

            case RewardKind.Reforger:
                // 재조합기 보유량 지급.
                // 코드 식별자 Reforger는 기존 직렬화 호환을 위해 유지한다.
                if (amount <= 0) break;

                if (GameManager.Instance.Item == null)
                {
                    Debug.LogWarning("[Reward] ItemManager 없음 — 재조합기 지급 실패");
                    break;
                }

                GameManager.Instance.Item.AddReforger(amount);
                Debug.Log($"[Reward] +{amount} 재조합기");
                break;

            case RewardKind.AugmentChoice:
                // 증강 3택1 트리거 — AugmentManager가 풀에서 추첨해 오퍼(GameEvents.AugmentOfferReady) 발행.
                if (GameManager.Instance.Augment == null)
                {
                    Debug.LogWarning("[Reward] AugmentManager 없음 — 증강 3택1 스킵");
                    break;
                }

                GameManager.Instance.Augment.OfferChoice();
                Debug.Log("[Reward] 증강 3택1 오퍼 트리거");
                break;
        }
    }

    /// <summary>
    /// 유닛 보상 지급. refNameEn으로 PokemonDatabase 조회 → amount만큼 벤치에 추가.
    /// 구매(ShopManager.Buy)와 동일 경로(UnitFactory.Create + Board.TryPlaceInBench).
    /// 벤치가 가득 차면 더 못 받고 로그만 남긴다(보상 유실 — TFT도 벤치 풀이면 받지 못함).
    /// </summary>
    private void GrantUnit(string nameEn, int amount)
    {
        if (string.IsNullOrEmpty(nameEn) || nameEn == "DUMMY")
        {
            Debug.LogWarning($"[Reward] Unit refNameEn 비어있음/DUMMY — 스킵");
            return;
        }

        var db = PokemonDatabase.Instance;
        PokemonData data = db != null ? db.GetByNameEn(nameEn) : null;
        if (data == null)
        {
            Debug.LogWarning($"[Reward] Unit '{nameEn}' PokemonDatabase에 없음 — 스킵");
            return;
        }

        var board = GameManager.Instance.Board;
        int granted = 0;
        for (int i = 0; i < Mathf.Max(1, amount); i++)
        {
            if (!board.HasBenchSpace())
            {
                Debug.LogWarning($"[Reward] 벤치 가득 — Unit '{nameEn}' {amount - granted}개 유실");
                break;
            }

            PokemonUnit unit = UnitFactory.Create(data);
            if (unit == null) break;

            if (!board.TryPlaceInBench(unit))
            {
                Destroy(unit.gameObject); // 방어적 정리(HasBenchSpace 통과 후 실패는 이론상 도달 안 함)
                break;
            }
            granted++;
        }

        if (granted > 0)
            Debug.Log($"[Reward] Unit '{nameEn}' ×{granted} 벤치 지급");
    }

    /// <summary>아이템 보상 지급. refNameEn으로 ItemDatabase 조회 → amount만큼 인벤토리에 추가.</summary>
    private void GrantItem(string nameEn, int amount)
    {
        if (string.IsNullOrEmpty(nameEn)) return;

        var item = GameManager.Instance.Item;
        int granted = 0;
        for (int i = 0; i < amount; i++)
        {
            if (!item.AddItemByNameEn(nameEn)) break; // 조회 실패 또는 인벤토리 가득(경고는 ItemManager가 로그)
            granted++;
        }

        if (granted > 0)
            Debug.Log($"[Reward] Item '{nameEn}' ×{granted} 인벤토리 지급");
    }

    /// <summary>진화의 돌 보상 지급. refNameEn으로 EvolutionStoneDatabase 조회 → amount만큼 인벤토리에 추가.</summary>
    private void GrantStone(string nameEn, int amount)
    {
        if (string.IsNullOrEmpty(nameEn)) return;

        var item = GameManager.Instance.Item;
        int granted = 0;
        for (int i = 0; i < amount; i++)
        {
            if (!item.AddStoneByNameEn(nameEn)) break;
            granted++;
        }

        if (granted > 0)
            Debug.Log($"[Reward] EvolutionStone '{nameEn}' ×{granted} 인벤토리 지급");
    }

    /// <summary>소모품 보상 지급. refNameEn으로 ConsumableData 조회 → amount만큼 인벤토리에 추가.</summary>
    private void GrantConsumable(string nameEn, int amount)
    {
        if (string.IsNullOrEmpty(nameEn)) return;

        var item = GameManager.Instance.Item;
        int granted = 0;

        for (int i = 0; i < amount; i++)
        {
            if (!item.AddConsumableByNameEn(nameEn)) break;
            granted++;
        }

        if (granted > 0)
            Debug.Log($"[Reward] Consumable '{nameEn}' ×{granted} 인벤토리 지급");
    }
}
