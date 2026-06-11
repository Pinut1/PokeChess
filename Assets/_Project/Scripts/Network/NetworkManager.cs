using UnityEngine;

#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Photon PUN2 기반 네트워크 매니저.
/// 연결 / 룸 관리 / 라운드 동기화 담당.
/// GameEvents를 통해 다른 매니저와 통신.
/// </summary>
public class NetworkManager : MonoBehaviourPunCallbacks
{
    // ─────────────────────────────────────────
    // 상수
    // ─────────────────────────────────────────

    private const int   MAX_PLAYERS = 2;
    private const float CONNECT_TIMEOUT = 10f;

    /// <summary>연결 끊김 후 재접속 유예 시간(초). Room.PlayerTtl에도 동일하게 적용.</summary>
    private const float RECONNECT_GRACE_PERIOD = 60f;

    /// <summary>JoinRandomRoom 실패(빈 방 없음) 시 생성/입장할 고정 방 이름. 양쪽 클라이언트가 같은 이름을 써야 서로 만날 수 있음.</summary>
    private const string FALLBACK_ROOM_NAME = "PokeChessRoom";

    // ─────────────────────────────────────────
    // 상태 프로퍼티 (읽기 전용)
    // ─────────────────────────────────────────

    public bool IsConnected   => PhotonNetwork.IsConnected;
    public bool IsInRoom      => PhotonNetwork.InRoom;
    public bool IsMasterClient => PhotonNetwork.IsMasterClient;
    public int  PlayerCount   => PhotonNetwork.CurrentRoom?.PlayerCount ?? 0;

    /// <summary>MasterClient에서만 집계되는 "준비 완료" 인원 수</summary>
    private int _readyCount = 0;

    /// <summary>상대방 재접속 유예 타이머</summary>
    private Coroutine _opponentGraceRoutine;

    /// <summary>본인 재접속 시도 타이머</summary>
    private Coroutine _selfReconnectRoutine;

    // ─────────────────────────────────────────
    // 연결
    // ─────────────────────────────────────────

    private void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.NickName = $"Player_{System.Guid.NewGuid().ToString()[..4]}";
    }

    private Coroutine _connectTimeoutRoutine;

    public void Connect()
    {
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
        if (!IsMasterClient) return;
        photonView.RPC(nameof(RPC_OnRoundStart), RpcTarget.All, round);
    }

    /// <summary>MasterClient가 전투 시작을 전체에 알림</summary>
    public void BroadcastBattleStart()
    {
        if (!IsMasterClient) return;
        photonView.RPC(nameof(RPC_OnBattleStart), RpcTarget.All);
    }

    /// <summary>쇼핑 페이즈에서 "준비 완료" 버튼 누를 때 호출. MasterClient에 보고.</summary>
    public void BroadcastPlayerReady()
    {
        photonView.RPC(nameof(RPC_OnPlayerReady), RpcTarget.MasterClient);
    }

    // ─────────────────────────────────────────
    // RPC 수신
    // ─────────────────────────────────────────

    [PunRPC]
    private void RPC_OnRoundStart(int round)
    {
        Debug.Log($"[Network] 라운드 {round} 시작 수신");
        _readyCount = 0;
        GameEvents.RoundChanged(round);
    }

    /// <summary>MasterClient만 처리. 준비 인원이 모이면 전체에 알림.</summary>
    [PunRPC]
    private void RPC_OnPlayerReady()
    {
        if (!IsMasterClient) return;

        _readyCount++;
        Debug.Log($"[Network] 준비 완료 {_readyCount}/{PlayerCount}");

        if (_readyCount >= PlayerCount)
        {
            _readyCount = 0;
            photonView.RPC(nameof(RPC_OnAllPlayersReady), RpcTarget.All);
        }
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
        BroadcastRoundStart(1);
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

    /// <summary>오프라인(1인)에서는 누르는 즉시 "모두 준비"로 처리</summary>
    public void BroadcastPlayerReady()         => GameEvents.AllPlayersReady();
}

#endif
