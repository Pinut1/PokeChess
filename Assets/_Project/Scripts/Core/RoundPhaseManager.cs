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

    /// <summary>이번 라운드 팀 결과(OnTeamRoundResolved)가 도착했는지. 라운드 시작 시 리셋.
    /// 실제 라이프 소진 여부는 이 플래그가 아니라 NetworkManager.TeamHealth로 직접 판정한다
    /// (outcome 값 자체는 더 이상 이 클래스의 판정에 쓰이지 않음).</summary>
    private bool _teamRoundResolved;

    /// <summary>다음 라운드 시작 전 팀 결과를 최대 이만큼 더 기다린다. 이 대기는 "방장 자신의 전투가 끝난
    /// 시점"부터 재는데, 전투 자체가 최대 35초(BattleManager.MAX_TICKS*TICK_INTERVAL=30s + 연장전
    /// _overtimeDuration=5s)까지 걸릴 수 있어 방장 전투가 아주 빨리(수 초 안에) 끝나도 상대방의 정상적인
    /// 최대 전투 시간을 확실히 덮도록 여유를 크게 잡는다(PLACEHOLDER 안전장치 — RPC 유실 시 영구 정지 방지).</summary>
    private const float TEAM_RESULT_SAFETY_TIMEOUT = 45f;

    /// <summary>이 시점까지도 팀 결과가 안 왔으면 재전송을 한 번 요청한다(NetworkManager.RequestBattleResultResendIfNeeded).</summary>
    private const float TEAM_RESULT_NUDGE_AT = 25f;

    /// <summary>이 시점까지도 안 왔으면 방장 결과로 대체 판정을 시도한다(NetworkManager.TryResolveTeamRoundWithHostFallback).
    /// 40초는 전투 최대 길이(35초)보다 5초 여유를 둔 값이다 — 방장 전투가 극단적으로 빨리 끝나더라도(예:
    /// 0초에 가깝게) 이 시점이면 상대방의 전투는 규칙상 반드시 끝나 있어야 하므로, 아직 안 끝난 정상
    /// 전투 결과를 잘못 무시하고 대체판정해버리는 일이 없다. TEAM_RESULT_SAFETY_TIMEOUT(45s)보다는
    /// 5초 여유를 둬서, 재전송 RPC 왕복 시간을 흡수하면서도 같은 대기 창 안에서 반드시 끝나도록 한다 —
    /// 별도 타이머를 새로 두면 이 루프의 타임아웃과 시점이 어긋나 서로 다른 시각에 판정/포기를 해버리는
    /// 문제가 있었다(2026-08-21 코드리뷰 지적, PR #120 후속. 40s로 상향한 것도 같은 후속 지적).</summary>
    private const float TEAM_RESULT_FALLBACK_AT = 40f;

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
        GameEvents.OnSessionEnded         -= HandleSessionEnded;
        GameEvents.OnGameCleared          -= HandleGameCleared;
        GameEvents.OnTeamRoundResolved    -= HandleTeamRoundResolved;
    }

    private void HandleTeamRoundResolved(TeamRoundOutcome outcome)
    {
        _teamRoundResolved = true;
    }

    // ─────────────────────────────────────────
    // 이벤트 핸들러
    // ─────────────────────────────────────────

    private void HandleRoundChanged(int round)
    {
        // 이미 게임오버로 확정된 뒤 뒤늦게 도착한 다음 라운드 신호는 무시한다(HandleGameCleared와 동일 가드).
        // 게임오버는 라운드 승패 경쟁과 무관하게 항복/접속끊김 포기 등 별도 경로로도 발생하므로,
        // 그 경로로 이미 GameOver에 들어간 클라이언트가 뒤늦은 RoundChanged로 다시 상점 화면에
        // 끌려나오는 걸 막는다(2026-08-21 코드리뷰 지적, PR #120 후속).
        if (CurrentPhase == GamePhase.GameOver) return;

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

    // HandleGracePeriodExpired(구 "유예시간 종료 → 자동 SessionEnded")는 2026-08 파트너 이탈 UX 작업에서 제거됨.
    // 상대 이탈 30초 경과는 이제 자동 패배가 아니라 [포기하기] 버튼 노출 가능 시점일 뿐이며,
    // 이 화면은 OptionsPanelUI가 GameEvents.OnGracePeriodExpired를 직접 구독해 처리한다(RoundPhaseManager가 할 일 없음).
    // 실제 세션 종료는 사용자가 [포기하기]→[타이틀로 이동]/[게임 종료]를 선택했을 때만
    // NetworkManager.ConfirmPartnerDisconnectGiveUp()이 GameEvents.SessionEnded를 발행해 아래 HandleSessionEnded로 흐른다.

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
        // RPC 유실 등으로 영영 안 올 가능성에 대한 안전장치로 최대 대기시간을 두고, 그 안에서 재촉·
        // 대체판정도 같은 시계로 시도한다(별도 타이머를 두면 이 루프의 타임아웃과 서로 어긋난다).
        float waited = 0f;
        bool nudged = false;
        bool fallbackAttempted = false;
        while (!_teamRoundResolved && waited < TEAM_RESULT_SAFETY_TIMEOUT)
        {
            yield return null;
            waited += Time.deltaTime;

            if (!nudged && waited >= TEAM_RESULT_NUDGE_AT)
            {
                nudged = true;
                network.RequestBattleResultResendIfNeeded();
            }
            if (!fallbackAttempted && waited >= TEAM_RESULT_FALLBACK_AT)
            {
                fallbackAttempted = true;
                network.TryResolveTeamRoundWithHostFallback();
            }
        }
        if (!_teamRoundResolved)
            Debug.LogWarning("[Phase] 팀 라운드 결과 미수신(타임아웃) — 안전장치로 다음 라운드 진행");

        // 팀 라이프가 이번 라운드로 완전히 소진됐으면(TeamHealth==0) 여기서는 다음 라운드/완주 어느
        // 쪽도 방송하지 않는다. HP 소진에 따른 게임오버 전환은 NetworkManager의 TeamHP Room 속성
        // 갱신(비동기, 별도 네트워크 메시지)이 처리하는데, 그 전환을 기다리지 않고 여기서 곧장
        // 라운드 번호만으로 완주를 판정하면 게임오버 전환이 아직 도착하기 전에 승리 화면이 먼저
        // 방송되는 레이스가 생길 수 있다(2026-08 코드리뷰 지적).
        // TeamHealth로 직접 판정하는 이유(PlayerHealthManager._maxLives 값에 의존하지 않기 위해):
        // 과거엔 "_lastTeamRoundOutcome != BothWin"을 "라이프 소진"의 대용 신호로 썼는데, 이건
        // _maxLives=1(공용 라이프 1개)일 때만 성립하는 암묵적 전제였다 — BothWin이 아니면 항상 즉시
        // 게임오버였기 때문. 나중에 라이프가 여러 개로 바뀌면, 아직 라이프가 남았는데도 완벽승이
        // 아니었다는 이유만으로 다음 라운드 진행 자체가 영구히 막히는 문제가 있었다(2026-08-21 코드리뷰
        // 지적, PR #120 후속). network.TeamHealth는 ResolveTeamRound()가 이미 라이프 차감을 반영한
        // 뒤의 최신 값을 곧장 읽으므로(Photon 로컬 캐시는 SetCustomProperties 직후 즉시 갱신됨) 별도
        // 대기 없이 라이프가 실제로 남았는지 그대로 판정할 수 있다.
        // (과거 이 조건이 "== BothLose"로만 좁게 걸려 있어서 최종 라운드에서 Split이 나오면 팀 HP가
        // 0이 되는데도 여기서 스킵하지 않고 완주(Victory) 방송을 그대로 내보내는 버그가 있었다 — 게임오버
        // 전환과 완주 방송이 동시에 경쟁하면서 클라이언트마다 승리/패배 모달이 뒤바뀌어 보였다. 2026-08-21
        // QA 리포트 "게임 엔딩 안내창 다름"으로 발견됨.)
        // NetworkManager.DebugInfiniteTeamHealth(QA 무한 HP 토글)가 켜져 있으면 ApplyTeamDamageLocal이
        // 데미지를 무시해 HP가 실제로는 안 깎이므로(NetworkManager.cs의 ApplyTeamDamageLocal 참고),
        // TeamHealth도 0으로 안 내려가 아래 조건이 자연히 스킵되지 않는다 — 명시적으로도 한 번 더
        // 방어해둔다(무한 HP 테스트 중 Result 페이즈에서 영구 정지되는 걸 막기 위함).
        // ⚠️ "<= 0"이 아니라 정확히 "== 0"으로 비교한다 — TeamHealth는 아직 초기화 전이면 -1(sentinel,
        // NetworkManager.TeamHealth 게터 참고)인데, <=0으로 비교하면 "아직 시작도 안 함"과 "라이프 소진"을
        // 구분 못 해 초기화 타이밍이 꼬이면 라운드 1부터 계속 멈춰버린다(2026-08-21 코드리뷰 지적, PR #120 후속).
        if (_teamRoundResolved && network.TeamHealth == 0 && !NetworkManager.DebugInfiniteTeamHealth)
            yield break;

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
