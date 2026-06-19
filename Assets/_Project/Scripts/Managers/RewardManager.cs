using UnityEngine;

/// <summary>
/// 스테이지 클리어 보상 지급 담당.
/// 전투 승리(OnBattleEnd(true)) 시 현재 스테이지의 rewardTableId로 RewardDatabase를 조회해 보상을 지급한다.
///
/// 스테이지 단일 출처는 RoundPhaseManager.CurrentStage (FSM에 보상 로직을 하드코딩하지 않기 위해 분리).
/// 매니저 간 직접 참조 금지 — 트리거는 GameEvents 구독, 지급은 GameManager.Instance.X pull로만.
///
/// 골드만 실제 지급 연결됨. 아이템/소모품/진화의 돌/유닛/증강은 해당 시스템(태욱/미구현)이 붙을 때까지
/// 훅 + 경고 로그로 남긴다(역기획서 수치도 미확정). grep "[Reward] TODO"로 추적.
/// </summary>
public class RewardManager : MonoBehaviour
{
    private void OnEnable()  => GameEvents.OnBattleEnd += HandleBattleEnd;
    private void OnDisable() => GameEvents.OnBattleEnd -= HandleBattleEnd;

    private void HandleBattleEnd(bool isWin)
    {
        if (!isWin) return; // 패배 시 보상 없음

        StageData stage = GameManager.Instance.Phase != null ? GameManager.Instance.Phase.CurrentStage : null;
        if (stage == null)
        {
            Debug.LogWarning("[Reward] CurrentStage 없음 — 보상 스킵 (StageDatabase 미임포트/매칭 실패)");
            return;
        }

        var db = RewardDatabase.Instance;
        if (db == null) return; // Instance 접근 시 에러 로그가 이미 출력됨

        RewardData table = db.GetByTableId(stage.rewardTableId);
        if (table == null)
        {
            Debug.LogWarning($"[Reward] '{stage.stageId}'의 rewardTableId={stage.rewardTableId} 테이블을 못 찾음 — 보상 스킵");
            return;
        }

        Debug.Log($"[Reward] '{stage.stageId}' 클리어 보상 지급: {table.label} ({table.rewards.Count}항목)");
        foreach (var entry in table.rewards)
            GrantEntry(entry, stage);
    }

    /// <summary>보상 한 항목 지급. dropChance로 확률 판정 후 종류별 분기.</summary>
    private void GrantEntry(RewardEntry entry, StageData stage)
    {
        if (entry == null) return;

        // 확률 보상: 확정(>=1)이 아니면 굴려서 탈락 시 지급 안 함.
        if (entry.dropChance < 1f && Random.value > entry.dropChance)
            return;

        switch (entry.kind)
        {
            case RewardKind.Gold:
                GameManager.Instance.Shop.AddGold(entry.amount); // 구매/판매와 동일 경로 → 골드 동기화도 그대로 탐
                Debug.Log($"[Reward] +{entry.amount}G");
                break;

            // ── 아래는 해당 시스템이 붙으면 연결. 지금은 누락 추적용 훅 + 로그. ───────────────
            case RewardKind.Item:
                Debug.LogWarning($"[Reward] TODO Item '{entry.refNameEn}' ×{entry.amount} — ItemManager 미구현(태욱)");
                break;
            case RewardKind.Consumable:
                Debug.LogWarning($"[Reward] TODO Consumable '{entry.refNameEn}' ×{entry.amount} — 소모품 시스템 미구현");
                break;
            case RewardKind.EvolutionStone:
                Debug.LogWarning($"[Reward] TODO EvolutionStone '{entry.refNameEn}' ×{entry.amount} — 진화의 돌 시스템 미구현(태욱)");
                break;
            case RewardKind.Unit:
                Debug.LogWarning($"[Reward] TODO Unit '{entry.refNameEn}' ×{entry.amount} — 컴패니언/유닛 지급 미구현");
                break;
            case RewardKind.AugmentChoice:
                Debug.LogWarning("[Reward] TODO AugmentChoice — AugmentManager 미구현(태욱), preReward 흐름과 통합 예정");
                break;
        }
    }
}
