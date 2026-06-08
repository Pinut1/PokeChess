using System.Collections;
using UnityEngine;

public enum GamePhase
{
    Lobby,     // 매칭 대기
    Shopping,  // 쇼핑 페이즈
    Battle,    // 전투 페이즈
    Result     // 결과 처리
}

/// <summary>
/// 라운드 페이즈 FSM.
/// Lobby → Shopping → Battle → Result → Shopping → ...
/// 페이즈 전환은 이 클래스만 담당. 외부는 GameEvents.OnPhaseChanged 구독.
/// </summary>
public class RoundPhaseManager : MonoBehaviour
{
    [Header("페이즈 시간 (초)")]
    [SerializeField] private float _shoppingDuration = 30f;
    [SerializeField] private float _resultDuration   = 3f;

    public GamePhase CurrentPhase { get; private set; } = GamePhase.Lobby;
    public int       CurrentRound { get; private set; } = 0;

    private Coroutine _phaseTimer;

    // ─────────────────────────────────────────
    // 이벤트 구독
    // ─────────────────────────────────────────

    private void OnEnable()
    {
        GameEvents.OnRoundChanged += HandleRoundChanged;
        GameEvents.OnBattleEnd    += HandleBattleEnd;
    }

    private void OnDisable()
    {
        GameEvents.OnRoundChanged -= HandleRoundChanged;
        GameEvents.OnBattleEnd    -= HandleBattleEnd;
    }

    // ─────────────────────────────────────────
    // 이벤트 핸들러
    // ─────────────────────────────────────────

    private void HandleRoundChanged(int round)
    {
        CurrentRound = round;
        EnterPhase(GamePhase.Shopping);
    }

    private void HandleBattleEnd(bool isWin)
    {
        EnterPhase(GamePhase.Result);
    }

    // ─────────────────────────────────────────
    // 페이즈 전환
    // ─────────────────────────────────────────

    private void EnterPhase(GamePhase phase)
    {
        if (_phaseTimer != null)
            StopCoroutine(_phaseTimer);

        CurrentPhase = phase;
        Debug.Log($"[Phase] {phase} | 라운드 {CurrentRound}");
        GameEvents.PhaseChanged(phase);

        switch (phase)
        {
            case GamePhase.Shopping:
                _phaseTimer = StartCoroutine(ShoppingTimer());
                break;

            case GamePhase.Battle:
                GameEvents.BattleStart();
                break;

            case GamePhase.Result:
                _phaseTimer = StartCoroutine(ResultTimer());
                break;
        }
    }

    // ─────────────────────────────────────────
    // 타이머
    // ─────────────────────────────────────────

    private IEnumerator ShoppingTimer()
    {
        yield return new WaitForSeconds(_shoppingDuration);
        EnterPhase(GamePhase.Battle);
    }

    private IEnumerator ResultTimer()
    {
        yield return new WaitForSeconds(_resultDuration);

        var network = GameManager.Instance.Network;
        if (network.IsMasterClient)
            network.BroadcastRoundStart(CurrentRound + 1);
    }

    // ─────────────────────────────────────────
    // 외부 호출
    // ─────────────────────────────────────────

    /// <summary>
    /// 쇼핑 페이즈에서 준비 완료 버튼 누를 때 호출.
    /// TODO: 2인 모두 준비 시 즉시 전투 돌입 — 기획 확정 후 구현.
    /// </summary>
    public void PlayerReady()
    {
        if (CurrentPhase != GamePhase.Shopping) return;
        EnterPhase(GamePhase.Battle);
    }
}
