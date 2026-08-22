using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 시작 시(1라운드 진입) 플레이어에게 임의의 1코스트 기물을 무료 지급한다.
/// 2인 협동이라 각자 보드가 따로이므로 각 클라이언트가 자기 보드에 지급한다.
/// 지급은 보상/영웅증강과 동일 규칙 — 골드/상점 풀 미차감(무료 지급).
/// 매니저끼리 직접 참조하지 않고 GameEvents.OnRoundChanged만 구독한다.
/// (MatchRecorder처럼 씬 GameObject에 컴포넌트로 붙여 두면 자체 동작한다.)
/// </summary>
public class StartingUnitGranter : MonoBehaviour
{
    [Header("지급 설정")]
    [Tooltip("게임 시작 시 지급할 기물 코스트. 기본 1(1코스트).")]
    [SerializeField] private int _grantCost = 1;

    [Tooltip("게임 시작 시 지급할 기물 수. 기본 1마리.")]
    [SerializeField] private int _grantCount = 1;

    /// <summary>1라운드가 재발행(재접속 등)돼도 중복 지급되지 않도록 1회만 지급.</summary>
    private bool _granted;

    private void OnEnable()  => GameEvents.OnRoundChanged += HandleRoundChanged;
    private void OnDisable() => GameEvents.OnRoundChanged -= HandleRoundChanged;

    private void HandleRoundChanged(int round)
    {
        // 재접속 라운드 캐치업(NetworkManager.ResyncAfterReconnect의 RPC_OnRoundStart 로컬 재호출) 중이면
        // 시작 유닛을 다시 지급하지 않는다 — ShopManager.HandleRoundChanged/RewardManager.HandleStageEntered와
        // 동일 패턴(2026-08 확인: 이 가드가 없어 재접속마다 벤치에 1코스트 유닛이 하나씩 더 생기는 버그가 있었음).
        if (GameManager.TryGet(out var gm) && gm.Network != null && gm.Network.IsReplayingProcessedRound)
            return;

        if (round != 1 || _granted) return;
        _granted = true;
        GrantStartingUnits();
    }

    private void GrantStartingUnits()
    {
        var candidates = GetGrantCandidates();
        if (candidates.Count == 0)
        {
            Debug.LogWarning($"[StartingUnit] {_grantCost}코스트 지급 후보 없음 — PokemonDatabase 임포트/shopBuyable 확인");
            return;
        }

        int granted = 0;
        for (int i = 0; i < _grantCount; i++)
        {
            PokemonData data = candidates[Random.Range(0, candidates.Count)];
            if (GrantOne(data)) granted++;
        }

        Debug.Log($"[StartingUnit] 게임 시작 지급 완료 — {_grantCost}코스트 {granted}/{_grantCount}마리");
    }

    /// <summary>상점 등장 가능한 지정 코스트 종 목록. 무료 지급이라 풀 잔량은 보지 않는다.</summary>
    private List<PokemonData> GetGrantCandidates()
    {
        var db = PokemonDatabase.Instance;
        if (db == null || db.all == null) return new List<PokemonData>();

        return db.all.FindAll(p => p != null && p.shopBuyable && p.cost == _grantCost);
    }

    /// <summary>데이터 1종을 벤치에 무료 지급. 벤치가 가득 차면 실패(false).</summary>
    private bool GrantOne(PokemonData data)
    {
        if (data == null) return false;

        var board = GameManager.Instance != null ? GameManager.Instance.Board : null;
        if (board == null || !board.HasBenchSpace())
        {
            Debug.LogWarning($"[StartingUnit] 벤치 가득/보드 없음 — {data.pokemonName} 지급 유실");
            return false;
        }

        PokemonUnit unit = UnitFactory.Create(data);
        if (unit == null) return false;

        if (!board.TryPlaceInBench(unit))
        {
            Destroy(unit.gameObject); // 방어적 정리(HasBenchSpace 통과 후 실패는 이론상 도달 안 함)
            return false;
        }

        Debug.Log($"[StartingUnit] {data.pokemonName}({_grantCost}코) 벤치 지급");
        return true;
    }
}
