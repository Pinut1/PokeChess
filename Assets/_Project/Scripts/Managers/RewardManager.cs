using UnityEngine;

/// <summary>
/// 스테이지 클리어 보상 지급 담당.
/// 팀 라운드 결과(OnTeamRoundResolved) 확정 시 현재 스테이지의 rewardTableId로 RewardDatabase를 조회해 지급한다.
/// 보상 배수(기획): BothWin=100%, Split(한 명만 패)=50%, BothLose(둘 다 패)=0%.
/// 각 클라이언트가 자기 플레이어 몫을 지급한다(스플릿이면 두 플레이어 모두 절반).
///
/// 스테이지 단일 출처는 RoundPhaseManager.CurrentStage (FSM에 보상 로직을 하드코딩하지 않기 위해 분리).
/// 매니저 간 직접 참조 금지 — 트리거는 GameEvents 구독, 지급은 GameManager.Instance.X pull로만.
///
/// 골드/유닛/아이템/진화의 돌은 실제 지급 연결됨. 소모품/증강은 해당 시스템이 붙을 때까지
/// 훅 + 경고 로그로 남긴다(역기획서 수치도 미확정). grep "[Reward] TODO"로 추적.
/// </summary>
public class RewardManager : MonoBehaviour
{
    private void OnEnable()  => GameEvents.OnTeamRoundResolved += HandleTeamRoundResolved;
    private void OnDisable() => GameEvents.OnTeamRoundResolved -= HandleTeamRoundResolved;

    private void HandleTeamRoundResolved(TeamRoundOutcome outcome)
    {
        // 보상 배수: 둘 다 승=100%, 스플릿=50%, 둘 다 패=0%(지급 없음).
        float mult = outcome switch
        {
            TeamRoundOutcome.BothWin => 1f,
            TeamRoundOutcome.Split   => 0.5f,
            _                        => 0f
        };
        if (mult <= 0f)
        {
            Debug.Log("[Reward] 둘 다 패배 — 보상 없음");
            return;
        }

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

        Debug.Log($"[Reward] '{stage.stageId}' 보상 지급: {table.label} ({table.rewards.Count}항목, 배수 {mult:P0})");
        foreach (var entry in table.rewards)
            GrantEntry(entry, stage, mult);
    }

    /// <summary>보상 한 항목 지급. dropChance로 확률 판정 후 배수 적용 종류별 분기.</summary>
    private void GrantEntry(RewardEntry entry, StageData stage, float mult)
    {
        if (entry == null) return;

        // 확률 보상: 확정(>=1)이 아니면 굴려서 탈락 시 지급 안 함.
        if (entry.dropChance < 1f && Random.value > entry.dropChance)
            return;

        // 스플릿이면 수량 절반(내림). 1개짜리 유닛 보상은 0이 될 수 있음(의도 — 절반).
        int amount = Mathf.FloorToInt(entry.amount * mult);

        switch (entry.kind)
        {
            case RewardKind.Gold:
                if (amount <= 0) break;
                GameManager.Instance.Shop.AddGold(amount); // 구매/판매와 동일 경로 → 골드 동기화도 그대로 탐
                Debug.Log($"[Reward] +{amount}G");
                break;

            case RewardKind.Item:
                if (amount > 0) GrantItem(entry.refNameEn, amount);
                break;

            // ── 아래는 해당 시스템이 붙으면 연결. 지금은 누락 추적용 훅 + 로그. ───────────────
            case RewardKind.Consumable:
                Debug.LogWarning($"[Reward] TODO Consumable '{entry.refNameEn}' ×{amount} — 소모품 시스템 미구현");
                break;
            case RewardKind.EvolutionStone:
                if (amount > 0) GrantStone(entry.refNameEn, amount);
                break;
            case RewardKind.Unit:
                if (amount > 0) GrantUnit(entry.refNameEn, amount);
                break;
            case RewardKind.AugmentChoice:
                Debug.LogWarning("[Reward] TODO AugmentChoice — AugmentManager 미구현(태욱), preReward 흐름과 통합 예정");
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
}
