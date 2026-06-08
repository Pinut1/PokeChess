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

    // ─────────────────────────────────────────
    // 상태 프로퍼티 (읽기 전용)
    // ─────────────────────────────────────────

    public bool IsConnected   => PhotonNetwork.IsConnected;
    public bool IsInRoom      => PhotonNetwork.InRoom;
    public bool IsMasterClient => PhotonNetwork.IsMasterClient;
    public int  PlayerCount   => PhotonNetwork.CurrentRoom?.PlayerCount ?? 0;

    // ─────────────────────────────────────────
    // 연결
    // ─────────────────────────────────────────

    private void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = false;
        PhotonNetwork.NickName = $"Player_{System.Guid.NewGuid().ToString()[..4]}";
    }

    public void Connect()
    {
        if (PhotonNetwork.IsConnected) return;
        Debug.Log("[Network] Photon 서버 연결 시도...");
        PhotonNetwork.ConnectUsingSettings();
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
        var options = new RoomOptions { MaxPlayers = MAX_PLAYERS, IsVisible = true };
        PhotonNetwork.CreateRoom(roomName, options);
    }

    public void JoinRoom(string roomName)
    {
        PhotonNetwork.JoinRoom(roomName);
    }

    public void JoinOrCreateRoom(string roomName)
    {
        var options = new RoomOptions { MaxPlayers = MAX_PLAYERS };
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

    // ─────────────────────────────────────────
    // RPC 수신
    // ─────────────────────────────────────────

    [PunRPC]
    private void RPC_OnRoundStart(int round)
    {
        Debug.Log($"[Network] 라운드 {round} 시작 수신");
        GameEvents.RoundChanged(round);
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
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[Network] 룸 입장 실패 ({returnCode}): {message}");
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.LogWarning("[Network] 랜덤 룸 없음 → 새 룸 생성");
        CreateRoom($"Room_{Random.Range(1000, 9999)}");
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"[Network] {newPlayer.NickName} 입장 | 인원: {PlayerCount}/{MAX_PLAYERS}");
        if (PlayerCount == MAX_PLAYERS)
            OnRoomFull();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"[Network] {otherPlayer.NickName} 퇴장");
    }

    private void OnRoomFull()
    {
        // 2인 모두 입장 → 방 닫고 게임 시작
        if (IsMasterClient)
            PhotonNetwork.CurrentRoom.IsOpen = false;

        Debug.Log("[Network] 2인 모두 입장 — 게임 시작");
        GameEvents.RoundChanged(1);
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        Debug.Log($"[Network] 마스터 클라이언트 변경 → {newMasterClient.NickName}");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[Network] 연결 끊김: {cause}");
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
}

#endif
