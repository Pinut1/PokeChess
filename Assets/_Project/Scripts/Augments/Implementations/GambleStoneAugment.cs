using UnityEngine;

/// <summary>
/// GAMBLE_STONE "진화 스페셜리스트": 2~5라운드 매 라운드 시작 시 무작위 진화의 돌 1개(최대 4개).
/// 최초 지급 시 재조합기(=Reforger, 재련기) + 2골드 함께 증정(밸런스 조절용 — Augment Table v2).
/// 선택 시점(R2 preReward)은 라운드 시작 직후라 Apply에서 그 라운드분을 지급한다.
/// 인벤토리 상한 초과 시 지급 실패(유실)는 ItemManager.AddStone이 로그로 남긴다.
/// </summary>
public class GambleStoneAugment : Augment
{
    private const int FIRST_ROUND = 2;
    private const int LAST_ROUND  = 5;
    private const int FIRST_BONUS_GOLD = 2;

    private bool _firstGrantDone;

    public override void Apply() => GrantRoundStone(CurrentRound());

    public override void OnRoundChanged(int round) => GrantRoundStone(round);

    private void GrantRoundStone(int round)
    {
        if (round < FIRST_ROUND || round > LAST_ROUND) return;

        var item = GameManager.Instance.Item;
        if (item == null)
        {
            Debug.LogWarning("[Augment] ItemManager 없음 — 진화 스페셜리스트 지급 실패");
            return;
        }

        var db = EvolutionStoneDatabase.Instance;
        if (db == null || db.all == null || db.all.Count == 0)
        {
            Debug.LogWarning("[Augment] EvolutionStoneDatabase 비어있음 — 진화 스페셜리스트 지급 실패");
            return;
        }

        var candidates = db.all.FindAll(s => s != null);
        if (candidates.Count == 0) return;

        var stone = candidates[Random.Range(0, candidates.Count)];
        if (item.AddStone(stone))
            Debug.Log($"[Augment] 진화 스페셜리스트: 진화의 돌 '{stone.stoneName}' +1 ({round}R)");

        // 최초 지급 보너스: 재조합기(Reforger) + 2골드 (v2 밸런스 조절용 추가 보상)
        if (!_firstGrantDone)
        {
            _firstGrantDone = true;
            item.AddReforger(1);
            GameManager.Instance.Shop?.AddGold(FIRST_BONUS_GOLD);
            Debug.Log($"[Augment] 진화 스페셜리스트 최초 보너스: 재조합기 +1, +{FIRST_BONUS_GOLD}G");
        }
    }

    private static int CurrentRound()
    {
        var phase = GameManager.Instance.Phase;
        return phase != null ? phase.CurrentRound : 0;
    }
}
