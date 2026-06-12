using System.Collections;
using UnityEngine;

public enum GamePhase
{
    Lobby,     // 매칭 대기
    Shopping,  // 쇼핑 페이즈
    Battle,    // 전투 페이즈
    Result,    // 결과 처리
    GameOver   // 세션 종료 (연결 끊김 등으로 인한 패배 처리)
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
        GameEvents.OnRoundChanged   += HandleRoundChanged;
        GameEvents.OnBattleEnd      += HandleBattleEnd;
        GameEvents.OnAllPlayersReady += HandleAllPlayersReady;
        GameEvents.OnOpponentDisconnected += HandleOpponentDisconnected;
        GameEvents.OnOpponentReconnected  += HandleOpponentReconnected;
        GameEvents.OnGracePeriodExpired   += HandleGracePeriodExpired;
        GameEvents.OnSessionEnded         += HandleSessionEnded;
    }

    private void OnDisable()
    {
        GameEvents.OnRoundChanged   -= HandleRoundChanged;
        GameEvents.OnBattleEnd      -= HandleBattleEnd;
        GameEvents.OnAllPlayersReady -= HandleAllPlayersReady;
        GameEvents.OnOpponentDisconnected -= HandleOpponentDisconnected;
        GameEvents.OnOpponentReconnected  -= HandleOpponentReconnected;
        GameEvents.OnGracePeriodExpired   -= HandleGracePeriodExpired;
        GameEvents.OnSessionEnded         -= HandleSessionEnded;
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

    private void HandleAllPlayersReady()
    {
        if (CurrentPhase != GamePhase.Shopping) return;
        EnterPhase(GamePhase.Battle);
    }

    /// <summary>상대 연결 끊김 — 유예시간 동안 페이즈 타이머 일시정지</summary>
    private void HandleOpponentDisconnected(float graceSeconds)
    {
        Debug.LogWarning($"[Phase] 상대 연결 끊김 — {graceSeconds}초 유예, 페이즈 일시정지");

        if (_phaseTimer != null)
        {
            StopCoroutine(_phaseTimer);
            _phaseTimer = null;
        }
    }

    /// <summary>상대 재접속 성공 — 페이즈 타이머 재개(처음부터 재시작)</summary>
    private void HandleOpponentReconnected()
    {
        Debug.Log("[Phase] 상대 재접속 — 페이즈 재개");

        switch (CurrentPhase)
        {
            case GamePhase.Shopping:
                _phaseTimer = StartCoroutine(ShoppingTimer());
                break;
            case GamePhase.Result:
                _phaseTimer = StartCoroutine(ResultTimer());
                break;
        }
    }

    /// <summary>재접속 유예시간 종료. 둘 다 끊겼으면 세션 종료, 한 명만 남았으면 항복/나가기 선택 필요</summary>
    private void HandleGracePeriodExpired(bool bothDisconnected)
    {
        if (bothDisconnected)
        {
            HandleSessionEnded();
            return;
        }

        Debug.LogWarning("[Phase] 유예시간 종료 — 남은 플레이어 항복/나가기 선택 필요 (UI 미구현)");
        // TODO(UIManager): 항복/나가기 선택 UI 연결
    }

    /// <summary>세션 종료(패배 처리). 전적 기록 시스템 미구현 — 로그만 출력</summary>
    private void HandleSessionEnded()
    {
        if (_phaseTimer != null)
        {
            StopCoroutine(_phaseTimer);
            _phaseTimer = null;
        }

        EnterPhase(GamePhase.GameOver);
        Debug.LogWarning("[Phase] 세션 종료 — 패배 처리 (전적 기록 미구현, 로그만 출력)");
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
    /// 2인 모두 준비되면 GameEvents.OnAllPlayersReady를 통해 전투 페이즈로 전환됨.
    /// </summary>
    public void PlayerReady()
    {
        if (CurrentPhase != GamePhase.Shopping) return;
        GameManager.Instance.Network.BroadcastPlayerReady();
    }

    // ─────────────────────────────────────────
    // 임시 디버그 UI
    // ─────────────────────────────────────────

    private void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.button) { fontSize = 36 };

        if (CurrentPhase == GamePhase.Shopping)
        {
            var readyRect = new Rect(Screen.width / 2f - 150f, Screen.height - 150f, 300f, 100f);
            if (GUI.Button(readyRect, "Ready", style))
                PlayerReady();
        }
    }
}
