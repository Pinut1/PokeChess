using UnityEngine;

#if PHOTON_UNITY_NETWORKING
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Photon PUN2 기반 네트워크 매니저.
/// 연결 / 룸 관리 / 라운드 동기화 담당.
/// GameEvents를 통해 다른 매니저와 통신.
/// </summary>
public class NetworkManager : MonoBehaviourPunCallbacks
{
    [Header("테스트")]
    [Tooltip("체크하면 Photon 없이 1인 오프라인 루프로 동작 (테스트 전용)")]
    [SerializeField] private bool _soloMode;

    // ─────────────────────────────────────────
    // 상수
    // ─────────────────────────────────────────

    private const int   MAX_PLAYERS = 2;
    private const float CONNECT_TIMEOUT = 10f;

    /// <summary>"준비 완료" 여부를 Player CustomProperties에 저장할 때 쓰는 키</summary>
    private const string READY_PROP_KEY = "Ready";

    /// <summary>플레이어 골드를 Player CustomProperties에 저장할 때 쓰는 키(파트너 표시용).</summary>
    private const string GOLD_PROP_KEY = "Gold";

    /// <summary>팀 공통 HP를 Room CustomProperties에 저장할 때 쓰는 키(GDD: 팀 공통 체력).</summary>
    private const string TEAM_HP_PROP_KEY = "TeamHP";

    /// <summary>게임 씬 로드 완료 여부를 Player CustomProperties에 저장할 때 쓰는 키(라운드 시작 핸드셰이크).</summary>
    private const string SCENE_READY_PROP_KEY = "SceneReady";

    /// <summary>이번 라운드 전투 결과를 Player CustomProperties에 저장할 때 쓰는 키. -1=미보고, 0=패, 1=승.</summary>
    private const string BATTLE_RESULT_PROP_KEY = "BattleResult";
    private const int    RESULT_NOT_REPORTED = -1;

    /// <summary>둘 다 패배 시 차감할 라이프(공용 HP 단위 = 라이프 1).</summary>
    private const int    LIFE_LOSS_ON_TEAM_DEFEAT = 1;

    /// <summary>이번 라운드 팀 결과를 이미 판정했는지(MasterClient, 중복 발행 방지). 라운드 시작 시 리셋.</summary>
    private bool _roundResultResolved;

    /// <summary>라운드 1을 한 번만 시작하기 위한 마스터 가드.</summary>
    private bool _gameStarted;

    /// <summary>연결 끊김 후 재접속 유예 시간(초). Room.PlayerTtl에도 동일하게 적용.</summary>
    private const float RECONNECT_GRACE_PERIOD = 60f;

    /// <summary>JoinRandomRoom 실패(빈 방 없음) 시 생성/입장할 고정 방 이름. 양쪽 클라이언트가 같은 이름을 써야 서로 만날 수 있음.</summary>
    private const string FALLBACK_ROOM_NAME = "PokeChessRoom";

    // ─────────────────────────────────────────
    // 상태 프로퍼티 (읽기 전용)
    // ─────────────────────────────────────────

    public bool IsConnected   => PhotonNetwork.IsConnected;
    public bool IsInRoom      => PhotonNetwork.InRoom;
    public bool IsMasterClient => _soloMode || PhotonNetwork.IsMasterClient;
    public int  PlayerCount   => _soloMode ? 1 : PhotonNetwork.CurrentRoom?.PlayerCount ?? 0;

    /// <summary>상대방 재접속 유예 타이머</summary>
    private Coroutine _opponentGraceRoutine;

    /// <summary>본인 재접속 시도 타이머</summary>
    private Coroutine _selfReconnectRoutine;

    // ─────────────────────────────────────────
    // 연결
    // ─────────────────────────────────────────

    private void Awake()
    {
        // 자동 씬 동기화는 "연결 전에" 켜져 있어야 팔로워(비마스터)가 마스터의 LoadLevel을 따라온다.
        // 같은 GameObject의 다른 컴포넌트(NetworkConnectionTest)가 Start에서 Connect()를 부를 수 있어
        // Start 순서 경합을 피하려고 모든 Start보다 먼저인 Awake에서 켠다.
        if (!_soloMode) PhotonNetwork.AutomaticallySyncScene = true;
    }

    public override void OnEnable()
    {
        base.OnEnable();
        GameEvents.OnGoldChanged += HandleGoldChanged;
    }

    public override void OnDisable()
    {
        GameEvents.OnGoldChanged -= HandleGoldChanged;
        base.OnDisable();
    }

    private void HandleGoldChanged(int gold)
    {
        SyncLocalGold(gold);
    }

    private void Start()
    {
        if (_soloMode)
        {
            Debug.LogWarning("[Network] 솔로 모드 — Photon 미사용, 즉시 라운드 1 시작");
            BroadcastRoundStart(1);
            return;
        }

        // 씬 로컬이라 GameManager는 씬마다 새로 생기지만, 이미 연결돼 있으면(로비→게임 전환 후)
        // 닉네임/인증값을 다시 설정하지 않는다(재접속 식별자 보존). 씬 동기화는 Awake에서 이미 켬.
        if (PhotonNetwork.IsConnected) return;

        PhotonNetwork.NickName = $"Player_{System.Guid.NewGuid().ToString()[..4]}";

        // ReconnectAndRejoin이 같은 플레이어로 인식하려면 재연결 시에도 동일한 UserId가 필요함.
        // AuthValues를 미리 고정해두지 않으면 재접속 시 새 UserId가 발급되어
        // "User does not exist in this game" 오류로 입장이 거부됨.
        PhotonNetwork.AuthValues = new AuthenticationValues(System.Guid.NewGuid().ToString());
    }

    private Coroutine _connectTimeoutRoutine;

    public void Connect()
    {
        if (_soloMode) { Debug.Log("[Network] 솔로 모드 — 연결 생략"); return; }
        if (PhotonNetwork.IsConnected) return;
        Debug.Log("[Network] Photon 서버 연결 시도...");
        PhotonNetwork.ConnectUsingSettings();

        if (_connectTimeoutRoutine != null)
            StopCoroutine(_connectTimeoutRoutine);
        _connectTimeoutRoutine = StartCoroutine(ConnectTimeoutRoutine());
    }

    private System.Collections.IEnumerator ConnectTimeoutRoutine()
    {
        yield return new WaitForSeconds(CONNECT_TIMEOUT);

        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Debug.LogWarning($"[Network] 연결 타임아웃 ({CONNECT_TIMEOUT}s) — 연결 시도 중단");
            PhotonNetwork.Disconnect();
        }

        _connectTimeoutRoutine = null;
    }

    public void Disconnect()
    {
        if (!PhotonNetwork.IsConnected) return;
        PhotonNetwork.Disconnect();
    }

    // ─────────────────────────────────────────
    // 룸 관리
    // ─────────────────────────────────────────

    public void CreateRoom(string roomName)
    {
        var options = new RoomOptions { MaxPlayers = MAX_PLAYERS, IsVisible = true, PlayerTtl = (int)(RECONNECT_GRACE_PERIOD * 1000) };
        PhotonNetwork.CreateRoom(roomName, options);
    }

    public void JoinRoom(string roomName)
    {
        PhotonNetwork.JoinRoom(roomName);
    }

    public void JoinOrCreateRoom(string roomName)
    {
        var options = new RoomOptions { MaxPlayers = MAX_PLAYERS, PlayerTtl = (int)(RECONNECT_GRACE_PERIOD * 1000) };
        PhotonNetwork.JoinOrCreateRoom(roomName, options, TypedLobby.Default);
    }

    /// <summary>빈 자리 있는 방 매칭. 없으면 OnJoinRandomFailed에서 새 방 생성.</summary>
    public void JoinRandomRoom()
    {
        PhotonNetwork.JoinRandomRoom();
    }

    public void LeaveRoom()
    {
        PhotonNetwork.LeaveRoom();
    }

    // ─────────────────────────────────────────
    // 라운드 동기화 (MasterClient 전용)
    // ─────────────────────────────────────────

    /// <summary>MasterClient가 다음 라운드 시작을 전체에 알림</summary>
    public void BroadcastRoundStart(int round)
    {
        if (_soloMode) { GameEvents.RoundChanged(round); return; }

        if (!IsMasterClient) return;
        photonView.RPC(nameof(RPC_OnRoundStart), RpcTarget.All, round);
    }

    /// <summary>MasterClient가 전투 시작을 전체에 알림</summary>
    public void BroadcastBattleStart()
    {
        if (_soloMode) { GameEvents.BattleStart(); return; }

        if (!IsMasterClient) return;
        photonView.RPC(nameof(RPC_OnBattleStart), RpcTarget.All);
    }

    /// <summary>MasterClient가 챕터 완주(최종 라운드 클리어)를 전체에 알림. 다음 라운드 대신 호출.</summary>
    public void BroadcastGameCleared()
    {
        if (_soloMode) { GameEvents.GameCleared(); return; }

        if (!IsMasterClient) return;
        photonView.RPC(nameof(RPC_OnGameCleared), RpcTarget.All);
    }

    /// <summary>쇼핑 페이즈에서 "준비 완료" 버튼 누를 때 호출. 자신의 준비 상태를 CustomProperties에 기록.</summary>
    public void BroadcastPlayerReady()
    {
        // 솔로 모드: 1인 = 전원 준비 완료
        if (_soloMode) { GameEvents.AllPlayersReady(); return; }

        var props = new Hashtable { { READY_PROP_KEY, true } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    /// <summary>
    /// 자기 보드 전투 결과(승/패)를 팀에 보고. PlayerHealthManager가 OnBattleEnd에서 호출.
    /// 두 플레이어가 모두 보고하면 MasterClient가 팀 결과를 판정한다(OnPlayerPropertiesUpdate).
    /// 솔로 모드는 1인=팀이므로 즉시 판정.
    /// </summary>
    public void ReportBattleResult(bool isWin)
    {
        if (_soloMode)
        {
            // 1인 = 팀. 승=BothWin, 패=BothLose(Split 불가).
            ResolveSoloRound(isWin);
            return;
        }

        var props = new Hashtable { { BATTLE_RESULT_PROP_KEY, isWin ? 1 : 0 } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    // ─────────────────────────────────────────
    // 통신교환 (전송 트랜잭션 — 모델A: 1마리 핸드오버 → 받는 쪽 진화/벤치)
    // ─────────────────────────────────────────

    /// <summary>전송 ack 대기 중인 보낸 유닛. 성공 ack 시 내 벤치에서 제거(취소 불가).</summary>
    private PokemonUnit _pendingTradeUnit;

    /// <summary>
    /// 내 벤치 유닛 1마리를 파트너에게 전송. 받는 쪽(A)이 자기 권위로 (매핑되면)진화체를 벤치에 생성.
    /// 상대 벤치가 가득이면 거부(유닛 그대로). 전송은 베이스 종+성급만 넘기고 인스턴스화는 A가 함.
    /// </summary>
    public void SendTradeUnit(PokemonUnit unit)
    {
        if (unit == null || unit.data == null) return;
        if (_soloMode || !PhotonNetwork.InRoom) { Debug.LogWarning("[Trade] 파트너 없음 — 전송 불가"); return; }
        if (_pendingTradeUnit != null) { Debug.LogWarning("[Trade] 이전 전송 처리 중"); return; }

        _pendingTradeUnit = unit;
        Debug.Log($"[Trade] 전송 요청: {unit.data.pokemonNameEn} ★{unit.starLevel} → 파트너");
        photonView.RPC(nameof(RPC_TradeReceive), RpcTarget.Others, unit.data.pokemonNameEn, unit.starLevel);
    }

    [PunRPC]
    private void RPC_TradeReceive(string baseNameEn, int starLevel)
    {
        var board = GameManager.Instance.Board;
        if (board == null || !board.HasBenchSpace())
        {
            Debug.LogWarning("[Trade] 내 벤치 가득 — 전송 거부");
            photonView.RPC(nameof(RPC_TradeAck), RpcTarget.Others, false);
            return;
        }

        // 통신진화 매핑 있으면 진화체로, 없으면 그대로 핸드오버(데이터 미입력 폴백).
        var te = TradeEvolutionData.Instance;
        string evolved = te != null ? te.GetEvolved(baseNameEn) : null;
        string targetName = string.IsNullOrEmpty(evolved) ? baseNameEn : evolved;

        var data = PokemonDatabase.Instance != null ? PokemonDatabase.Instance.GetByNameEn(targetName) : null;
        if (data == null)
        {
            Debug.LogWarning($"[Trade] '{targetName}' PokemonDatabase에 없음 — 거부");
            photonView.RPC(nameof(RPC_TradeAck), RpcTarget.Others, false);
            return;
        }

        var unit = UnitFactory.Create(data, Mathf.Clamp(starLevel, 1, 3));
        if (unit == null || !board.TryPlaceInBench(unit))
        {
            if (unit != null) Destroy(unit.gameObject);
            photonView.RPC(nameof(RPC_TradeAck), RpcTarget.Others, false);
            return;
        }

        Debug.Log($"[Trade] 수신: {baseNameEn} → {targetName} ★{unit.starLevel} 벤치 배치");
        GameEvents.TradeUnitReceived(unit);
        photonView.RPC(nameof(RPC_TradeAck), RpcTarget.Others, true);
    }

    [PunRPC]
    private void RPC_TradeAck(bool success)
    {
        if (_pendingTradeUnit == null) return;
        var unit = _pendingTradeUnit;
        _pendingTradeUnit = null;

        if (!success)
        {
            Debug.LogWarning("[Trade] 전송 실패(상대 벤치 가득) — 유닛 유지");
            GameEvents.TradeRejected();
            return;
        }

        // 성공 — 보낸 유닛을 내 벤치에서 제거(환급 없음, 취소 불가). Destroy로 시각도 정리.
        var board = GameManager.Instance.Board;
        var bench = board.GetBenchSnapshot();
        for (int i = 0; i < bench.Count; i++)
            if (bench[i] == unit) { board.RemoveFromBench(i); break; }
        Destroy(unit.gameObject);
        Debug.Log("[Trade] 전송 완료 — 보낸 유닛 제거");
    }

    // ─────────────────────────────────────────
    // 상태 동기화 (보드 미러 / 골드 / 팀 HP)
    // ─────────────────────────────────────────

    /// <summary>현재 팀 공통 HP. Room CustomProperties에서 읽음. 미설정이면 -1.</summary>
    public int TeamHealth
    {
        get
        {
            if (_soloMode || !PhotonNetwork.InRoom) return _soloTeamHp;
            var props = PhotonNetwork.CurrentRoom?.CustomProperties;
            if (props != null && props.TryGetValue(TEAM_HP_PROP_KEY, out object hp))
                return (int)hp;
            return -1;
        }
    }

    private int _soloTeamHp = -1; // 솔로 모드용 로컬 팀 HP 저장

    /// <summary>
    /// 내 보드 배치 스냅샷을 상대 클라이언트에게 송출(미러 렌더용). 상대에게만 전송.
    /// 보드/벤치가 바뀔 때 BoardSyncBroadcaster가 호출한다.
    /// </summary>
    public void BroadcastBoardSnapshot(int[] data)
    {
        if (_soloMode) return; // 파트너 없음 — 송출 불필요
        if (!PhotonNetwork.InRoom) return;
        photonView.RPC(nameof(RPC_OnBoardSnapshot), RpcTarget.Others, data);
    }

    /// <summary>
    /// 게임 씬 로드 완료를 알림(라운드 시작 핸드셰이크). GameSceneBootstrap이 GameScene 진입 시 호출.
    /// 두 클라가 모두 준비되면 MasterClient가 라운드 1을 시작한다.
    /// (로비→게임 전환 중 라운드 시작 RPC가 유실되는 레이스 방지)
    /// </summary>
    public void NotifySceneReady()
    {
        if (_soloMode) { BroadcastRoundStart(1); return; }
        if (!PhotonNetwork.InRoom) return;
        var props = new Hashtable { { SCENE_READY_PROP_KEY, true } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    /// <summary>내 골드를 Player CustomProperties에 기록 → 파트너 클라가 표시.</summary>
    public void SyncLocalGold(int gold)
    {
        if (_soloMode || !PhotonNetwork.InRoom) return;
        var props = new Hashtable { { GOLD_PROP_KEY, gold } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    /// <summary>팀 공통 HP 초기화(MasterClient만, 아직 미설정일 때). PlayerHealthManager가 게임 시작 시 호출.</summary>
    public void InitTeamHealth(int hp)
    {
        if (_soloMode) { _soloTeamHp = hp; GameEvents.HealthChanged(hp); return; }
        if (!IsMasterClient || !PhotonNetwork.InRoom) return;
        if (TeamHealth >= 0) return; // 이미 설정됨(재접속 등)
        SetTeamHealthProp(hp);
    }

    /// <summary>
    /// 전투 패배 데미지를 팀 공통 HP에 반영 요청. 단일 기록자(MasterClient) 권위로 갱신.
    /// 비마스터는 RPC로 마스터에게 위임 → 두 클라가 동시에 써서 생기는 경합 방지.
    /// </summary>
    public void ReportBattleLoss(int damage)
    {
        if (_soloMode) { ApplyTeamDamageLocal(damage); return; }
        if (!PhotonNetwork.InRoom) return;

        if (IsMasterClient) ApplyTeamDamageLocal(damage);
        else photonView.RPC(nameof(RPC_ReportBattleLoss), RpcTarget.MasterClient, damage);
    }

    /// <summary>MasterClient에서만 실행: 현재 팀 HP를 읽어 데미지만큼 깎아 Room 속성에 기록.</summary>
    private void ApplyTeamDamageLocal(int damage)
    {
        if (_soloMode)
        {
            _soloTeamHp = Mathf.Max(0, _soloTeamHp - damage);
            GameEvents.HealthChanged(_soloTeamHp);
            if (_soloTeamHp <= 0) GameEvents.SessionEnded();
            return;
        }

        int current = TeamHealth;
        if (current < 0) return; // 아직 초기화 안 됨
        SetTeamHealthProp(Mathf.Max(0, current - damage));
    }

    private void SetTeamHealthProp(int hp)
    {
        var props = new Hashtable { { TEAM_HP_PROP_KEY, hp } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    // ─────────────────────────────────────────
    // RPC 수신
    // ─────────────────────────────────────────

    /// <summary>상대 보드 스냅샷 수신 → 미러 렌더용 이벤트 발행.</summary>
    [PunRPC]
    private void RPC_OnBoardSnapshot(int[] data)
    {
        GameEvents.OpponentBoardChanged(BoardSnapshot.Decode(data));
    }

    /// <summary>비마스터의 패배 데미지 요청을 마스터가 수신 → 팀 HP에 반영.</summary>
    [PunRPC]
    private void RPC_ReportBattleLoss(int damage)
    {
        if (!IsMasterClient) return;
        ApplyTeamDamageLocal(damage);
    }

    [PunRPC]
    private void RPC_OnRoundStart(int round)
    {
        Debug.Log($"[Network] 라운드 {round} 시작 수신");

        // 각 클라이언트가 자기 준비 상태 + 이번 라운드 전투 결과를 리셋
        var props = new Hashtable
        {
            { READY_PROP_KEY, false },
            { BATTLE_RESULT_PROP_KEY, RESULT_NOT_REPORTED }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        _roundResultResolved = false; // (MasterClient 집계 가드 리셋)

        GameEvents.RoundChanged(round);
    }

    [PunRPC]
    private void RPC_OnAllPlayersReady()
    {
        Debug.Log("[Network] 2인 모두 준비 완료");
        GameEvents.AllPlayersReady();
    }

    [PunRPC]
    private void RPC_OnBattleStart()
    {
        Debug.Log("[Network] 전투 시작 수신");
        GameEvents.BattleStart();
    }

    [PunRPC]
    private void RPC_OnGameCleared()
    {
        Debug.Log("[Network] 챕터 완주 수신");
        GameEvents.GameCleared();
    }

    [PunRPC]
    private void RPC_OnTeamRoundResolved(int outcome)
    {
        GameEvents.TeamRoundResolved((TeamRoundOutcome)outcome);
    }

    /// <summary>솔로 모드(1인=팀) 즉시 판정. 승=BothWin, 패=BothLose(라이프 -1).</summary>
    private void ResolveSoloRound(bool isWin)
    {
        TeamRoundOutcome outcome = isWin ? TeamRoundOutcome.BothWin : TeamRoundOutcome.BothLose;
        if (!isWin) ApplyTeamDamageLocal(LIFE_LOSS_ON_TEAM_DEFEAT);
        GameEvents.TeamRoundResolved(outcome);
    }

    // ─────────────────────────────────────────
    // Photon 콜백
    // ─────────────────────────────────────────

    public override void OnConnectedToMaster()
    {
        if (_connectTimeoutRoutine != null)
        {
            StopCoroutine(_connectTimeoutRoutine);
            _connectTimeoutRoutine = null;
        }

        Debug.Log("[Network] Photon Master 서버 연결 완료");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("[Network] 로비 입장");
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"[Network] 룸 입장 완료 | 인원: {PlayerCount}/{MAX_PLAYERS}");
        if (PlayerCount == MAX_PLAYERS)
            OnRoomFull();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[Network] 룸 생성 실패 ({returnCode}): {message}");

        // 그 사이 다른 클라이언트가 같은 이름의 방을 먼저 만든 경우 → 그 방으로 입장
        if (returnCode == ErrorCode.GameIdAlreadyExists)
        {
            Debug.Log($"[Network] '{FALLBACK_ROOM_NAME}' 방이 이미 존재 → 입장 시도");
            JoinRoom(FALLBACK_ROOM_NAME);
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[Network] 룸 입장 실패 ({returnCode}): {message}");
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"[Network] 랜덤 룸 없음 → '{FALLBACK_ROOM_NAME}' 생성/입장 시도");
        CreateRoom(FALLBACK_ROOM_NAME);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"[Network] {newPlayer.NickName} 입장 | 인원: {PlayerCount}/{MAX_PLAYERS}");

        if (_opponentGraceRoutine != null)
        {
            StopCoroutine(_opponentGraceRoutine);
            _opponentGraceRoutine = null;
            Debug.Log("[Network] 상대 재접속 성공 — 유예 타이머 취소");
            GameEvents.OpponentReconnected();
        }

        if (PlayerCount == MAX_PLAYERS)
            OnRoomFull();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"[Network] {otherPlayer.NickName} 퇴장 (Inactive: {otherPlayer.IsInactive})");

        if (!otherPlayer.IsInactive)
        {
            // 자진 퇴장(룸 영구 이탈) — 유예 없이 바로 처리
            GameEvents.GracePeriodExpired(false);
            return;
        }

        if (_opponentGraceRoutine != null)
            StopCoroutine(_opponentGraceRoutine);
        _opponentGraceRoutine = StartCoroutine(OpponentGraceRoutine());

        GameEvents.OpponentDisconnected(RECONNECT_GRACE_PERIOD);
    }

    /// <summary>상대방 연결 끊김 후 재접속 유예 타이머. 시간 내 재접속하면 OnPlayerEnteredRoom에서 취소됨.</summary>
    private System.Collections.IEnumerator OpponentGraceRoutine()
    {
        yield return new WaitForSeconds(RECONNECT_GRACE_PERIOD);
        _opponentGraceRoutine = null;

        bool bothDisconnected = !PhotonNetwork.IsConnectedAndReady;
        Debug.LogWarning($"[Network] 상대 재접속 유예시간 종료 (둘 다 끊김: {bothDisconnected})");
        GameEvents.GracePeriodExpired(bothDisconnected);
    }

    private void OnRoomFull()
    {
        // 2인 모두 입장 → 방 닫고 게임 시작
        Debug.Log("[Network] 2인 모두 입장 — 게임 시작");

        if (!IsMasterClient) return;

        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.LoadLevel("GameSceneTest");
        // 라운드 1 시작은 여기서 하지 않는다 — 씬 전환 중 RPC 유실 방지를 위해
        // 두 클라가 GameScene 로드를 마치고 SceneReady를 올리면(OnPlayerPropertiesUpdate) 그때 시작.
    }

    /// <summary>
    /// 플레이어의 CustomProperties(준비 상태)가 바뀔 때마다 호출됨.
    /// MasterClient만 검사 — 모든 플레이어가 준비 완료면 전체에 알림.
    /// _readyCount(로컬 변수) 대신 Player CustomProperties를 직접 조회하므로
    /// MasterClient가 교체돼도 준비 상태가 유실되지 않음.
    /// </summary>
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        // 파트너 골드 표시: 다른 플레이어의 Gold가 바뀌면 UI 갱신용 이벤트 발행(모든 클라).
        if (targetPlayer != PhotonNetwork.LocalPlayer &&
            changedProps.TryGetValue(GOLD_PROP_KEY, out object gold))
        {
            GameEvents.PartnerGoldChanged((int)gold);
        }

        // 이하 집계는 MasterClient만.
        if (!IsMasterClient) return;

        // 게임 씬 로드 핸드셰이크: 두 클라가 모두 SceneReady면 라운드 1 시작(1회만).
        if (!_gameStarted && changedProps.ContainsKey(SCENE_READY_PROP_KEY) && AllPlayersHaveFlag(SCENE_READY_PROP_KEY))
        {
            _gameStarted = true;
            BroadcastRoundStart(1);
            return;
        }

        // 준비 완료 집계.
        if (changedProps.ContainsKey(READY_PROP_KEY) && AllPlayersHaveFlag(READY_PROP_KEY))
            photonView.RPC(nameof(RPC_OnAllPlayersReady), RpcTarget.All);

        // 전투 결과 집계: 두 플레이어가 모두 보고했으면 팀 결과 1회 판정.
        if (changedProps.ContainsKey(BATTLE_RESULT_PROP_KEY) && !_roundResultResolved && AllPlayersReportedResult())
            ResolveTeamRound();
    }

    /// <summary>모든 플레이어가 이번 라운드 전투 결과를 보고했는지(-1=미보고).</summary>
    private bool AllPlayersReportedResult()
    {
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (!player.CustomProperties.TryGetValue(BATTLE_RESULT_PROP_KEY, out object v) ||
                (int)v == RESULT_NOT_REPORTED)
                return false;
        }
        return true;
    }

    /// <summary>
    /// MasterClient: 두 플레이어 승패를 집계해 팀 결과 판정 → 라이프 차감(둘 다 패) + 전체 브로드캐스트.
    /// 승리 수: 2=BothWin, 1=Split, 0=BothLose.
    /// </summary>
    private void ResolveTeamRound()
    {
        _roundResultResolved = true;

        int wins = 0;
        foreach (var player in PhotonNetwork.PlayerList)
            if (player.CustomProperties.TryGetValue(BATTLE_RESULT_PROP_KEY, out object v) && (int)v == 1)
                wins++;

        TeamRoundOutcome outcome = wins >= 2 ? TeamRoundOutcome.BothWin
                                 : wins == 1 ? TeamRoundOutcome.Split
                                 : TeamRoundOutcome.BothLose;

        if (outcome == TeamRoundOutcome.BothLose)
            ApplyTeamDamageLocal(LIFE_LOSS_ON_TEAM_DEFEAT); // 라이프 -1 (마스터 권위)

        Debug.Log($"[Network] 팀 라운드 결과: {outcome} (승 {wins}명)");
        photonView.RPC(nameof(RPC_OnTeamRoundResolved), RpcTarget.All, (int)outcome);
    }

    /// <summary>모든 플레이어의 해당 bool CustomProperty가 true인지.</summary>
    private bool AllPlayersHaveFlag(string key)
    {
        foreach (var player in PhotonNetwork.PlayerList)
        {
            bool on = player.CustomProperties.TryGetValue(key, out object v) && (bool)v;
            if (!on) return false;
        }
        return true;
    }

    /// <summary>팀 공통 HP(Room 속성) 변경 수신 → 모든 클라가 UI 갱신, 0 이하면 세션 종료.</summary>
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (!propertiesThatChanged.TryGetValue(TEAM_HP_PROP_KEY, out object hp)) return;

        int health = (int)hp;
        GameEvents.HealthChanged(health);
        if (health <= 0)
        {
            Debug.LogWarning("[Network] 팀 공통 HP 0 — 세션 종료(게임오버)");
            GameEvents.SessionEnded();
        }
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        Debug.Log($"[Network] 마스터 클라이언트 변경 → {newMasterClient.NickName}");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[Network] 연결 끊김: {cause}");

        // 자진 퇴장(LeaveRoom/Disconnect 직접 호출)은 재접속 시도 안 함
        if (cause == DisconnectCause.DisconnectByClientLogic) return;

        if (_selfReconnectRoutine != null)
            StopCoroutine(_selfReconnectRoutine);
        _selfReconnectRoutine = StartCoroutine(SelfReconnectRoutine());
    }

    /// <summary>본인 연결이 끊겼을 때 유예시간 동안 재접속 시도. 실패 시 세션 종료(패배 처리).</summary>
    private System.Collections.IEnumerator SelfReconnectRoutine()
    {
        Debug.Log($"[Network] 재접속 시도 시작 (유예 {RECONNECT_GRACE_PERIOD}초)");
        PhotonNetwork.ReconnectAndRejoin();

        float elapsed = 0f;
        while (elapsed < RECONNECT_GRACE_PERIOD)
        {
            if (PhotonNetwork.InRoom)
            {
                Debug.Log("[Network] 재접속 성공");
                _selfReconnectRoutine = null;
                yield break;
            }

            yield return new WaitForSeconds(1f);
            elapsed += 1f;
        }

        Debug.LogWarning("[Network] 재접속 실패 — 세션 종료(패배 처리)");
        _selfReconnectRoutine = null;
        GameEvents.SessionEnded();
    }
}

#else

/// <summary>
/// PUN2 미설치 시 오프라인 스텁.
/// Window > Package Manager > Asset Store에서 PUN2 설치 후 자동으로 실제 구현으로 교체됨.
/// </summary>
public class NetworkManager : MonoBehaviour
{
    public bool IsConnected    => false;
    public bool IsInRoom       => false;
    public bool IsMasterClient => true;   // 오프라인에서는 항상 호스트 취급
    public int  PlayerCount    => 1;

    private int _teamHp = -1;
    public int  TeamHealth     => _teamHp;

    private void Start()
    {
        Debug.LogWarning("[Network] PUN2 미설치 — 오프라인 모드로 실행 중");
    }

    public void Connect()           => Debug.Log("[Network] 오프라인 모드");
    public void Disconnect()        { }
    public void CreateRoom(string _) => Debug.Log("[Network] 오프라인 모드");
    public void JoinRoom(string _)   => Debug.Log("[Network] 오프라인 모드");
    public void JoinOrCreateRoom(string _) => Debug.Log("[Network] 오프라인 모드");
    public void JoinRandomRoom()    => Debug.Log("[Network] 오프라인 모드");
    public void LeaveRoom()         { }
    public void BroadcastRoundStart(int round) => GameEvents.RoundChanged(round);
    public void BroadcastBattleStart()         => GameEvents.BattleStart();
    public void BroadcastGameCleared()         => GameEvents.GameCleared();

    /// <summary>오프라인(1인)에서는 누르는 즉시 "모두 준비"로 처리</summary>
    public void BroadcastPlayerReady()         => GameEvents.AllPlayersReady();

    // 상태 동기화 — 오프라인은 파트너가 없으므로 보드 미러/골드는 no-op, 팀 HP만 로컬 처리.
    public void BroadcastBoardSnapshot(int[] _) { }
    public void SyncLocalGold(int _)            { }

    /// <summary>오프라인(1인)은 씬 로드 즉시 라운드 1 시작.</summary>
    public void NotifySceneReady()              => BroadcastRoundStart(1);

    public void InitTeamHealth(int hp)
    {
        if (_teamHp >= 0) return;
        _teamHp = hp;
        GameEvents.HealthChanged(_teamHp);
    }

    public void ReportBattleLoss(int damage)
    {
        if (_teamHp < 0) return;
        _teamHp = Mathf.Max(0, _teamHp - damage);
        GameEvents.HealthChanged(_teamHp);
        if (_teamHp <= 0) GameEvents.SessionEnded();
    }

    /// <summary>오프라인(1인=팀): 승=BothWin, 패=BothLose(라이프 -1). 즉시 판정.</summary>
    public void ReportBattleResult(bool isWin)
    {
        if (!isWin) ReportBattleLoss(1);   // 라이프 -1
        GameEvents.TeamRoundResolved(isWin ? TeamRoundOutcome.BothWin : TeamRoundOutcome.BothLose);
    }

    /// <summary>오프라인은 파트너가 없어 통신교환 불가.</summary>
    public void SendTradeUnit(PokemonUnit unit) => Debug.LogWarning("[Trade] 오프라인 — 파트너 없음, 전송 불가");
}

#endif
