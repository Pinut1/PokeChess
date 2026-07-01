using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reward v2 보상 지급 담당.
/// 라운드 시작 시 RoundPhaseManager가 CurrentStage를 확정하고 OnStageEntered를 발행하면,
/// 해당 StageData.rewardTableId로 RewardDatabase를 조회해 전투 전 보상을 선지급한다.
///
/// v2 보상 흐름:
/// 보상 선지급 → 상점/리롤/덱 구성 → 전투
///
/// 스테이지 단일 출처는 RoundPhaseManager.CurrentStage / OnStageEntered.
/// 매니저 간 직접 참조 금지 — 트리거는 GameEvents 구독, 지급은 GameManager.Instance.X pull로만 처리.
/// </summary>
public class RewardManager : MonoBehaviour
{
    /// <summary>
    /// 같은 stageId 보상이 중복 지급되는 것을 방지한다.
    /// 예: 이벤트 재발행, 씬 테스트 중 수동 호출, 네트워크 재동기화 등.
    /// </summary>
    private readonly HashSet<string> _grantedStageIds = new();

    private void OnEnable()
    {
        GameEvents.OnStageEntered += HandleStageEntered;
    }

    private void OnDisable()
    {
        GameEvents.OnStageEntered -= HandleStageEntered;
    }

    /// <summary>
    /// 라운드 시작 시 현재 스테이지가 확정되면 전투 전 보상을 선지급한다.
    /// </summary>
    private void HandleStageEntered(StageData stage)
    {
        if (stage == null)
        {
            Debug.LogWarning("[Reward] StageEntered stage 없음 — 보상 스킵");
            return;
        }

        if (string.IsNullOrEmpty(stage.stageId))
        {
            Debug.LogWarning("[Reward] stageId 없음 — 보상 스킵");
            return;
        }

        if (string.IsNullOrEmpty(stage.rewardTableId))
        {
            Debug.LogWarning($"[Reward] '{stage.stageId}' rewardTableId 없음 — 보상 스킵");
            return;
        }

        if (_grantedStageIds.Contains(stage.stageId))
        {
            Debug.LogWarning($"[Reward] '{stage.stageId}' 보상은 이미 지급됨 — 중복 지급 방지");
            return;
        }

        var db = RewardDatabase.Instance;
        if (db == null)
            return; // Instance 접근 시 에러 로그가 이미 출력됨

        RewardData table = db.GetByTableId(stage.rewardTableId);
        if (table == null)
        {
            Debug.LogWarning($"[Reward] '{stage.stageId}'의 rewardTableId={stage.rewardTableId} 테이블을 못 찾음 — 보상 스킵");
            return;
        }

        _grantedStageIds.Add(stage.stageId);

        Debug.Log($"[Reward] '{stage.stageId}' 전투 전 보상 지급: {table.label} ({table.rewards.Count}항목)");

        foreach (var entry in table.rewards)
            GrantEntry(entry, stage, 1f);
    }

    /// <summary>보상 한 항목 지급. dropChance로 확률 판정 후 종류별 분기.</summary>
    private void GrantEntry(RewardEntry entry, StageData stage, float mult)
    {
        if (entry == null) return;

        // 확률 보상: 확정(>=1)이 아니면 굴려서 탈락 시 지급 안 함.
        if (entry.dropChance < 1f && Random.value > entry.dropChance)
            return;

        // v2는 전투 전 선지급이므로 기본 배수는 1f.
        int amount = Mathf.FloorToInt(entry.amount * mult);

        switch (entry.kind)
        {
            case RewardKind.Gold:
                if (amount <= 0) break;

                if (GameManager.Instance.Shop == null)
                {
                    Debug.LogWarning("[Reward] ShopManager 없음 — 골드 지급 실패");
                    break;
                }

                GameManager.Instance.Shop.AddGold(amount);
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

            case RewardKind.Reroll:
                if (amount <= 0) break;

                if (GameManager.Instance.Shop == null)
                {
                    Debug.LogWarning("[Reward] ShopManager 없음 — 유닛상점 리롤권 지급 실패");
                    break;
                }

                GameManager.Instance.Shop.AddUnitShopRerollTickets(amount);
                Debug.Log($"[Reward] +{amount} 유닛상점 리롤권");
                break;

            case RewardKind.ItemShopReroll:
                if (amount <= 0) break;

                if (GameManager.Instance.Shop == null)
                {
                    Debug.LogWarning("[Reward] ShopManager 없음 — 아이템상점 리롤권 지급 실패");
                    break;
                }

                GameManager.Instance.Shop.AddItemShopRerollTickets(amount);
                Debug.Log($"[Reward] +{amount} 아이템상점 리롤권");
                break;

            case RewardKind.Reforger:
                if (amount <= 0) break;

                if (GameManager.Instance.Item == null)
                {
                    Debug.LogWarning("[Reward] ItemManager 없음 — Reforger 지급 실패");
                    break;
                }

                GameManager.Instance.Item.AddReforger(amount);
                Debug.Log($"[Reward] +{amount} Reforger");
                break;

            case RewardKind.Item:
                if (amount > 0) GrantItem(entry.refNameEn, amount);
                break;

            case RewardKind.Consumable:
                if (amount > 0) GrantConsumable(entry.refNameEn, amount);
                break;

            case RewardKind.EvolutionStone:
                if (amount > 0) GrantStone(entry.refNameEn, amount);
                break;

            case RewardKind.Unit:
                if (amount > 0) GrantUnit(entry.refNameEn, amount);
                break;

            case RewardKind.AugmentChoice:
                Debug.LogWarning("[Reward] TODO AugmentChoice — AugmentManager 미구현, preReward 흐름과 통합 예정");
                break;
        }
    }

    /// <summary>
    /// 유닛 보상 지급. refNameEn으로 PokemonDatabase 조회 → amount만큼 벤치에 추가.
    /// 구매(ShopManager.Buy)와 동일 경로(UnitFactory.Create + Board.TryPlaceInBench).
    /// 벤치가 가득 차면 더 못 받고 로그만 남긴다.
    /// </summary>
    private void GrantUnit(string nameEn, int amount)
    {
        if (string.IsNullOrEmpty(nameEn) || nameEn == "DUMMY")
        {
            Debug.LogWarning("[Reward] Unit refNameEn 비어있음/DUMMY — 스킵");
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
        if (board == null)
        {
            Debug.LogWarning("[Reward] BoardManager 없음 — Unit 지급 실패");
            return;
        }

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
                Destroy(unit.gameObject);
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
        if (item == null)
        {
            Debug.LogWarning("[Reward] ItemManager 없음 — Item 지급 실패");
            return;
        }

        int granted = 0;
        for (int i = 0; i < amount; i++)
        {
            if (!item.AddItemByNameEn(nameEn)) break;
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
        if (item == null)
        {
            Debug.LogWarning("[Reward] ItemManager 없음 — EvolutionStone 지급 실패");
            return;
        }

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
        if (item == null)
        {
            Debug.LogWarning("[Reward] ItemManager 없음 — Consumable 지급 실패");
            return;
        }

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