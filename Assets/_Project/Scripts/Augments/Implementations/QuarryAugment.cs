using UnityEngine;

/// <summary>
/// 채석가 증강: 매 라운드 진화의 돌 1개 지급(종류는 EvolutionStoneDatabase에서 무작위 — 기획 미명시 PLACEHOLDER).
/// 선택 즉시 이번 라운드분 1개를 주고, 이후 라운드마다 1개씩 지급한다.
/// 인벤토리 상한 초과 시 지급 실패(유실)는 ItemManager.AddStone이 로그로 남긴다.
/// </summary>
public class QuarryAugment : Augment
{
    public override void Apply() => GrantRandomStone(); // 선택 라운드분 (OnRoundChanged는 이미 지나감)

    public override void OnRoundChanged(int round) => GrantRandomStone();

    private static void GrantRandomStone()
    {
        var item = GameManager.Instance.Item;
        if (item == null)
        {
            Debug.LogWarning("[Augment] ItemManager 없음 — 채석가 지급 실패");
            return;
        }

        var db = EvolutionStoneDatabase.Instance;
        if (db == null || db.all == null || db.all.Count == 0)
        {
            Debug.LogWarning("[Augment] EvolutionStoneDatabase 비어있음 — 채석가 지급 실패");
            return;
        }

        var candidates = db.all.FindAll(s => s != null);
        if (candidates.Count == 0) return;

        var stone = candidates[Random.Range(0, candidates.Count)];
        if (item.AddStone(stone))
            Debug.Log($"[Augment] 채석가: 진화의 돌 '{stone.stoneName}' +1");
    }
}
