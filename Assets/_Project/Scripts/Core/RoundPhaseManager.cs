using System.Collections;
using UnityEngine;

public enum GamePhase
{
    Lobby,     // 매칭 대기
    Shopping,  // 쇼핑 페이즈
    Battle,    // 전투 페이즈
    Result,    // 결과 처리
    Victory,   // 챕터 완주 (최종 라운드 클리어)
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

    [Header("진행 규칙")]
    [Tooltip("켜짐: 쇼핑 제한시간이 지나면 자동으로 전투 시작(테스트/AFK 방지). " +
             "꺼짐: 두 플레이어가 모두 Ready를 눌러야만 전투 시작(정식 동작).")]
    [SerializeField] private bool _autoStartBattleOnTimeout = true;

    public GamePhase CurrentPhase { get; private set; } = GamePhase.Lobby;
    public int       CurrentRound { get; private set; } = 0;

    /// <summary>쇼핑 제한시간 자동 전투 시작 여부. 꺼져 있으면 제한시간이 없어 UI가 카운트다운 대신 별도 표기를 한다.</summary>
    public bool AutoStartBattleOnTimeout => _autoStartBattleOnTimeout;

    /// <summary>
    /// 현재 라운드에 진행 중인 스테이지(중앙 StageDatabase에서 해석). 라운드 변경 시 갱신.
    /// 전투의 적 구성·보상테이블·트레이너·preReward의 단일 출처 — BattleManager 등은 여기서 읽는다.
    /// StageDatabase 미임포트/매칭 실패 시 null(BattleManager는 "내 보드 미러"로 폴백).
    /// </summary>
    public StageData CurrentStage { get; private set; }

    private Coroutine _phaseTimer;

    /// <summary>이번 라운드 팀 결과(OnTeamRoundResolved)가 도착했는지. 라운드 시작 시 리셋.</summary>
    private bool _teamRoundResolved;

    /// <summary>다음 라운드 시작 전 팀 결과를 최대 이만큼 더 기다린다(전투 최대 길이 MAX_TICKS*TICK_INTERVAL=30s와 동일).
    /// 파트너 전투가 아직 진행 중인데 내 쪽만 끝나 먼저 다음 라운드가 시작되는 것을 막기 위함(PLACEHOLDER 안전장치 — RPC 유실 시 영구 정지 방지).</summary>
    private const float TEAM_RESULT_SAFETY_TIMEOUT = 30f;

    // ─────────────────────────────────────────
    // 이벤트 구독
    // ─────────────────────────────────────────

    private void OnEnable()
    {
        GameEvents.OnRoundChanged   += HandleRoundChanged;
        GameEvents.OnBattleEnd      += HandleBattleEnd;
        GameEvents.OnAllPlayersReady += HandleAllPlayersReady;
        GameEvents.OnPlayerReadyRequested += HandlePlayerReadyRequested;
        GameEvents.OnOpponentDisconnected += HandleOpponentDisconnected;
        GameEvents.OnOpponentReconnected  += HandleOpponentReconnected;
        GameEvents.OnGracePeriodExpired   += HandleGracePeriodExpired;
        GameEvents.OnSessionEnded         += HandleSessionEnded;
        GameEvents.OnGameCleared          += HandleGameCleared;
        GameEvents.OnTeamRoundResolved    += HandleTeamRoundResolved;
    }

    private void OnDisable()
    {
        GameEvents.OnRoundChanged   -= HandleRoundChanged;
        GameEvents.OnBattleEnd      -= HandleBattleEnd;
        GameEvents.OnAllPlayersReady -= HandleAllPlayersReady;
        GameEvents.OnPlayerReadyRequested -= HandlePlayerReadyRequested;
        GameEvents.OnOpponentDisconnected -= HandleOpponentDisconnected;
        GameEvents.OnOpponentReconnected  -= HandleOpponentReconnected;
        GameEvents.OnGracePeriodExpired   -= HandleGracePeriodExpired;
        GameEvents.OnSessionEnded         -= HandleSessionEnded;
        GameEvents.OnGameCleared          -= HandleGameCleared;
        GameEvents.OnTeamRoundResolved    -= HandleTeamRoundResolved;
    }

    private void HandleTeamRoundResolved(TeamRoundOutcome outcome) => _teamRoundResolved = true;

    // ─────────────────────────────────────────
    // 이벤트 핸들러
    // ─────────────────────────────────────────

    private void HandleRoundChanged(int round)
    {
        CurrentRound = round;
        _teamRoundResolved = false;
        ResolveCurrentStage(round);
        EnterPhase(GamePhase.Shopping);
    }

    /// <summary>
    /// 현재 라운드의 스테이지를 중앙 StageDatabase에서 확정해 CurrentStage에 보관하고 OnStageEntered 발행.
    /// 이후 BattleManager는 CurrentStage로 적을 생성하고, 연출/보상/증강 담당은 이벤트로 훅을 건다.
    /// </summary>
    private void ResolveCurrentStage(int round)
    {
        CurrentStage = StageDatabase.Instance != null ? StageDatabase.Instance.GetForRound(round) : null;

        if (CurrentStage == null)
        {
            Debug.LogWarning($"[Phase] 라운드 {round} 스테이지 없음 — StageDatabase 미임포트? (전투는 미러 폴백)");
            return;
        }

        Debug.Log($"[Phase] 스테이지 진입: {CurrentStage.stageId} ({CurrentStage.stageType})");
        GameEvents.StageEntered(CurrentStage);

        // TODO(기획 확정 후, 담당자 분배): 전투 전 이벤트 분기. 여기엔 로직을 하드코딩하지 않고
        // OnStageEntered 구독으로 각 파트가 처리한다(증강 풀/보상 수치/연출 모두 기획 미확정).
        //  - preReward 증강 3택1: RewardManager가 OnStageEntered에서 처리(연결 완료, 2026-07-16). 아이템/컴패니언은 기획 미확정.
        //    Ready 전 강제(블로킹) 여부 기획 확정 필요 — 현재는 선택 안 해도 라운드 진행 가능.
        //  - trainerName 존재 시 트레이너 등장 연출 + 전용 BGM/배경(UI 담당).
        // 전투 승리 보상은 RewardManager가 OnBattleEnd(true)에서 CurrentStage.rewardTableId로 지급(연결 완료, 골드만 — 수치는 역기획서 대기).
    }

    private void HandleBattleEnd(BattleEndReason reason)
    {
        // 패배로 체력 0 → PlayerHealthManager가 SessionEnded로 이미 GameOver 전환했을 수 있음.
        // 완주 직후 마지막 전투 결과가 Victory를 덮어쓰는 것도 방지.
        // 구독자 호출 순서와 무관하게 종료 상태를 Result가 덮어쓰지 않도록 가드.
        if (CurrentPhase == GamePhase.GameOver || CurrentPhase == GamePhase.Victory) return;
        EnterPhase(GamePhase.Result);
    }

    /// <summary>챕터 완주(최종 라운드 클리어). 양 클라이언트에서 BroadcastGameCleared로 발행됨.</summary>
    private void HandleGameCleared()
    {
        if (CurrentPhase == GamePhase.GameOver) return;
        Debug.Log($"[Phase] 챕터 완주 — Victory (도달 라운드 {CurrentRound})");
        EnterPhase(GamePhase.Victory);
    }

    private void HandleAllPlayersReady()
    {
        if (CurrentPhase != GamePhase.Shopping)
            return;

        AugmentManager augment =
            GameManager.TryGet(out var gm) ? gm.Augment : null;

        if (augment != null && augment.HasPendingChoice)
        {
            Debug.LogWarning(
                "[Phase] 증강 선택 대기 중 — 전투 시작 보류"
            );
            return;
        }

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

    /// <summary>
    /// 재접속 유예시간 종료. 2인 협동 PVE라 둘 다 끊겼든 한 명만 남았든 공략이 불가능하므로
    /// 어느 경우든 세션 종료(패배)로 처리한다. (남은 플레이어 솔로 전환/항복 선택지는 기획상 두지 않음.)
    /// 자기 핸들러 직접 호출이 아니라 이벤트로 발행해야 다른 구독자(MatchRecorder 등)도 종료를 인지한다.
    /// </summary>
    private void HandleGracePeriodExpired(bool bothDisconnected)
    {
        if (!bothDisconnected)
            Debug.LogWarning("[Phase] 유예시간 종료 — 상대 미재접속(협동 불가) → 세션 종료");

        GameEvents.SessionEnded(bothDisconnected
            ? SessionEndReason.BothDisconnected
            : SessionEndReason.PartnerAbandoned);
    }

    /// <summary>세션 종료(패배 처리). 전적 기록은 MatchRecorder가 같은 이벤트를 구독해 담당.</summary>
    private void HandleSessionEnded(SessionEndReason reason)
    {
        if (_phaseTimer != null)
        {
            StopCoroutine(_phaseTimer);
            _phaseTimer = null;
        }

        EnterPhase(GamePhase.GameOver);
        string stageId = CurrentStage != null ? CurrentStage.stageId : "(없음)";
        Debug.LogWarning($"[Phase] 세션 종료 — 패배 처리 (사유 {reason}, 도달 라운드 {CurrentRound}, 스테이지 {stageId})");
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
        // 자동 시작이 꺼져 있으면 두 플레이어 Ready로만 전투가 시작된다.
        if (!_autoStartBattleOnTimeout)
            yield break;

        float elapsed = 0f;

        while (elapsed < _shoppingDuration)
        {
            // 증강 선택 대기 중에는 쇼핑 타이머를 진행시키지 않는다.
            AugmentManager augment =
                GameManager.TryGet(out var gm) ? gm.Augment : null;

            if (augment == null || !augment.HasPendingChoice)
                elapsed += Time.deltaTime;

            yield return null;
        }

        // 타이머 종료 직전에 증강 오퍼가 생겼을 가능성까지 방어한다.
        while (GameManager.TryGet(out var gmPending)
               && gmPending.Augment != null
               && gmPending.Augment.HasPendingChoice)
        {
            yield return null;
        }

        if (CurrentPhase == GamePhase.Shopping)
            EnterPhase(GamePhase.Battle);
    }

    private IEnumerator ResultTimer()
    {
        yield return new WaitForSeconds(_resultDuration);

        if (!GameManager.TryGet(out var gm))
            yield break;

        var network = gm.Network;
        if (!network.IsMasterClient) yield break;

        // 파트너 전투가 아직 진행 중일 수 있음(각자 보드 따로 시뮬레이션) — 팀 결과(OnTeamRoundResolved)가
        // 도착할 때까지 추가로 기다려서 한쪽만 끝났는데 다음 라운드가 먼저 시작되는 걸 막는다.
        // RPC 유실 등으로 영영 안 올 가능성에 대한 안전장치로 최대 대기시간을 둔다.
        float waited = 0f;
        while (!_teamRoundResolved && waited < TEAM_RESULT_SAFETY_TIMEOUT)
        {
            yield return null;
            waited += Time.deltaTime;
        }
        if (!_teamRoundResolved)
            Debug.LogWarning("[Phase] 팀 라운드 결과 미수신(타임아웃) — 안전장치로 다음 라운드 진행");

        // 최종 라운드를 클리어했으면 다음 라운드 대신 완주(Victory)를 알린다.
        // (StageDatabase.GetForRound는 stages가 비지 않으면 항상 클램프해 null을 안 주므로
        //  LastRound로 끝을 판정해야 한다 — 안 그러면 11 이후 1-11이 무한 반복됨.)
        int last = StageDatabase.Instance != null ? StageDatabase.Instance.LastRound : 0;
        if (last > 0 && CurrentRound >= last)
            network.BroadcastGameCleared();
        else
            network.BroadcastRoundStart(CurrentRound + 1);
    }

    // ─────────────────────────────────────────
    // 외부 호출
    // ─────────────────────────────────────────

    /// <summary>
    /// 쇼핑 페이즈에서 UI의 준비 요청을 검증한다.
    /// 2인 모두 준비되면 GameEvents.OnAllPlayersReady를 통해 전투 페이즈로 전환됨.
    /// </summary>
    private void HandlePlayerReadyRequested()
    {
        if (CurrentPhase != GamePhase.Shopping) return;

        // 증강 3택1이 떠 있는 동안은 준비를 막는다(블로킹 UX).
        AugmentManager augment = GameManager.TryGet(out var gm) ? gm.Augment : null;
        if (augment != null && augment.HasPendingChoice)
        {
            Debug.LogWarning("[Phase] 증강을 먼저 선택해야 준비할 수 있습니다.");
            return;
        }

        // 실제 브로드캐스트는 이벤트를 구독한 NetworkManager가 담당한다.
        GameEvents.ApprovePlayerReady();
    }

    // Ready 입력은 UIManager, Victory 표시는 PrototypeHud가 담당한다.
}
