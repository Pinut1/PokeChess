using System.Collections.Generic;
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

    private const int   MAX_PLAYERS         = 2;
    private const float CONNECT_TIMEOUT     = 10f;
    // Photon 플레이어 닉네임 최대 길이.
    private const int   MAX_NICKNAME_LENGTH = 16;

    /// <summary>"준비 완료" 여부를 Player CustomProperties에 저장할 때 쓰는 키</summary>
    private const string READY_PROP_KEY = "Ready";

    /// <summary>플레이어 골드를 Player CustomProperties에 저장할 때 쓰는 키(파트너 표시용).</summary>
    private const string GOLD_PROP_KEY = "Gold";

    /// <summary>플레이어의 누적 증강 영문명 배열을 Player CustomProperties에 저장할 때 쓰는 키.
    /// RPC와 달리 유예(비활성) 중에도 서버에 보존돼 재접속·마스터 교체에 안전하다.</summary>
    private const string AUGMENTS_PROP_KEY = "Augments";

    /// <summary>팀 공통 HP를 Room CustomProperties에 저장할 때 쓰는 키(GDD: 팀 공통 체력).</summary>
    private const string TEAM_HP_PROP_KEY = "TeamHP";

    /// <summary>게임 씬 로드 완료 여부를 Player CustomProperties에 저장할 때 쓰는 키(라운드 시작 핸드셰이크).</summary>
    private const string SCENE_READY_PROP_KEY = "SceneReady";

    /// <summary>이번 라운드 전투 결과를 Player CustomProperties에 저장할 때 쓰는 키. -1=미보고, 0=패, 1=승.</summary>
    private const string BATTLE_RESULT_PROP_KEY = "BattleResult";
    private const int    RESULT_NOT_REPORTED = -1;

    /// <summary>현재 진행 라운드를 Room CustomProperties에 저장할 때 쓰는 키.
    /// RPC_OnRoundStart는 비접속자에게 유실되므로, 재접속 클라이언트는 이 속성으로 라운드를 복구한다.</summary>
    private const string ROUND_PROP_KEY = "Round";

    /// <summary>내가 마지막으로 수신/적용한 라운드. 재접속 시 Room 속성의 현재 라운드와 비교해 유실분을 복구.</summary>
    private int _lastKnownRound;

    /// <summary>내 보드 스냅샷 송출 revision(단조 증가). 수신 측은 과거 revision을 무시해 순서 역전을 막는다.</summary>
    private int _localBoardRevision;

    /// <summary>상대 보드 스냅샷의 마지막 적용 revision. 새 판(라운드 1) 시작 시 리셋.</summary>
    private int _lastOpponentBoardRevision = -1;

    /// <summary>둘 다 패배 시 차감할 라이프(공용 HP 단위 = 라이프 1).</summary>
    private const int    LIFE_LOSS_ON_TEAM_DEFEAT = 1;

    /// <summary>디버그: 켜지면 팀 공통 HP가 절대 깎이지 않음(무한 HP). PrototypeHud에서 토글. 빌드/검증 편의용.</summary>
    public static bool DebugInfiniteTeamHealth = false;

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
    public bool UsesSharedShopPool => !_soloMode && PhotonNetwork.InRoom;

    // 전적 기록(MatchRecorder) 등이 Photon 타입에 직접 의존하지 않도록 문자열로 노출.
    public string RoomName       => _soloMode ? "solo" : PhotonNetwork.CurrentRoom?.Name ?? "";
    public string LocalNickname  => _soloMode ? "SoloPlayer" : PhotonNetwork.NickName;

    public override void OnEnable()
    {
        base.OnEnable();

        GameEvents.OnPhaseChanged += HandlePhaseChanged;
        GameEvents.OnGoldTransferRequested += HandleGoldTransferRequested;
        GameEvents.OnPlayerReadyApproved += BroadcastPlayerReady;
    }

    public override void OnDisable()
    {
        GameEvents.OnPhaseChanged -= HandlePhaseChanged;
        GameEvents.OnGoldTransferRequested -= HandleGoldTransferRequested;
        GameEvents.OnPlayerReadyApproved -= BroadcastPlayerReady;

        base.OnDisable();
    }

    private void HandlePhaseChanged(GamePhase phase)
    {
        _isBattlePhase = phase == GamePhase.Battle;

        if (phase == GamePhase.Shopping)
        {
            ApplyPendingTradeEvolutions();
        }
    }

    /// <summary>
    /// 현재 판의 고유 ID(GUID). 협동에선 MasterClient가 방 잠금 시점에 Room 커스텀 속성으로
    /// 배포해 두 클라이언트가 같은 값을 갖는다 — 전적 matchId의 "방이름+분단위 시각" 방식이
    /// 분 경계에서 어긋날 수 있던 문제의 교체분(Phase 2). 솔로는 라운드 1 시작마다 재발급.
    /// 아직 미배포/미입장이면 ""(호출부가 폴백 처리).
    /// </summary>
    public string MatchGuid
    {
        get
        {
            if (_soloMode) return _soloMatchGuid;
            if (PhotonNetwork.InRoom &&
                PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(MATCH_GUID_ROOM_KEY, out object g))
                return g as string ?? "";
            return "";
        }
    }
    private string _soloMatchGuid = "";
    private const string MATCH_GUID_ROOM_KEY = "MatchGuid";
    public string PartnerNickname
    {
        get
        {
            if (_soloMode || !PhotonNetwork.InRoom) return "";
            var others = PhotonNetwork.PlayerListOthers;
            return others != null && others.Length > 0 ? others[0].NickName : "";
        }
    }

    /// <summary>상대방 재접속 유예 타이머</summary>
    private Coroutine _opponentGraceRoutine;

    /// <summary>GracePeriodExpired 중복 발행 방지. 로컬 유예 코루틴(60초)과 Photon PlayerTtl(동일 60초) 만료로 인한
    /// OnPlayerLeftRoom(IsInactive=false) 재호출이 거의 동시에 도착해 같은 이탈 건을 두 번 발행할 수 있다.</summary>
    private bool _gracePeriodExpiredFired;

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

        // 실제 로비에서 닉네임이 전달되지 않은 테스트 상황을 대비한 임시값.
        // 이미 로그인/로비 UI에서 닉네임을 설정했다면 해당 값을 덮어쓰지 않는다.
        if (string.IsNullOrWhiteSpace(PhotonNetwork.NickName))
            PhotonNetwork.NickName = $"Player_{System.Guid.NewGuid().ToString()[..4]}";

        // ReconnectAndRejoin이 같은 플레이어로 인식하려면 재연결 시에도 동일한 UserId가 필요함.
        // AuthValues를 미리 고정해두지 않으면 재접속 시 새 UserId가 발급되어
        // "User does not exist in this game" 오류로 입장이 거부됨.
        PhotonNetwork.AuthValues = new AuthenticationValues(System.Guid.NewGuid().ToString());
    }

    private Coroutine _connectTimeoutRoutine;

    /// <summary>
    /// 로그인 또는 로비 UI에서 입력받은 닉네임을
    /// 현재 Photon 로컬 플레이어의 닉네임으로 적용한다.
    ///
    /// 설정된 닉네임은 방 입장 후 다른 플레이어에게 공유되며,
    /// MatchRecorder가 LocalNickname / PartnerNickname을 통해
    /// 전적 기록에 자동으로 저장한다.
    /// </summary>
    /// <param name="nickname">사용자가 입력한 닉네임</param>
    /// <returns>닉네임 적용에 성공하면 true, 적용할 수 없으면 false</returns>
    public bool TrySetLocalNickname(string nickname)
    {
        // 솔로 모드는 Photon 플레이어 정보를 사용하지 않는다.
        if (_soloMode)
        {
            Debug.LogWarning("[Network] 솔로 모드에서는 Photon 닉네임을 사용하지 않습니다.");
            return false;
        }

        // 현재 UI에는 방 입장 후 닉네임 변경 경로가 없지만,
        // 다른 스크립트에서 이 공개 메서드를 잘못 호출하는 경우를 방지한다.
        // 한 판 도중 닉네임이 바뀌어 파트너 표시와 전적 기록이 어긋나지 않도록 차단한다.
        if (PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[Network] 방 입장 후에는 닉네임을 변경할 수 없습니다.");
            return false;
        }

        // 입력 앞뒤의 불필요한 공백을 제거한다.
        string trimmedNickname = nickname?.Trim();

        // 공백 또는 빈 문자열은 유효한 닉네임으로 인정하지 않는다.
        if (string.IsNullOrEmpty(trimmedNickname))
        {
            Debug.LogWarning("[Network] 닉네임이 비어 있습니다.");
            return false;
        }

        // 로비 UI와 네트워크 데이터의 닉네임 길이를 일정하게 제한한다.
        if (trimmedNickname.Length > MAX_NICKNAME_LENGTH)
            trimmedNickname = trimmedNickname[..MAX_NICKNAME_LENGTH];

        // Photon 플레이어 정보에 적용.
        // 방에 들어가면 파트너가 Player.NickName으로 이 값을 조회할 수 있다.
        PhotonNetwork.NickName = trimmedNickname;

        Debug.Log($"[Network] 닉네임 설정 완료: {PhotonNetwork.NickName}");
        return true;
    }

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
        if (_soloMode)
        {
            // 솔로: 새 판(라운드 1)마다 matchId용 GUID 재발급
            if (round == 1) _soloMatchGuid = System.Guid.NewGuid().ToString("N");
            GameEvents.RoundChanged(round);
            return;
        }

        if (!IsMasterClient) return;
        // 재접속 클라이언트의 라운드 복구용으로 Room 속성에도 기록(RPC는 비접속자에게 유실됨).
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { ROUND_PROP_KEY, round } });
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

    // ─────────────────────────────────────────
    // 공유 챔피언 풀 / 상점 예약 (MasterClient 권위)
    // ─────────────────────────────────────────

    public bool RequestSharedShopRoll(int level, bool forceCostFour, bool onlyCostFour = false)
    {
        if (!UsesSharedShopPool) return false;
        if (IsMasterClient)
            ProcessSharedShopRoll(PhotonNetwork.LocalPlayer.ActorNumber, level, forceCostFour, onlyCostFour);
        else
            photonView.RPC(nameof(RPC_RequestSharedShopRoll), RpcTarget.MasterClient, level, forceCostFour, onlyCostFour);
        return true;
    }

    public bool RequestSharedShopPurchase(int revision, int slot)
    {
        if (!UsesSharedShopPool) return false;
        if (IsMasterClient)
            ProcessSharedShopPurchase(PhotonNetwork.LocalPlayer.ActorNumber, revision, slot);
        else
            photonView.RPC(nameof(RPC_RequestSharedShopPurchase), RpcTarget.MasterClient, revision, slot);
        return true;
    }

    /// <summary>
    /// 구매 합체 예외(2슬롯 동시 구매)를 하나의 요청으로 보낸다.
    /// 마스터 클라이언트가 두 슬롯을 모두 검증한 뒤 동시에 승인/거절한다(부분 성공 없음).
    /// </summary>
    public bool RequestSharedShopMergePurchase(int revision, int slotA, int slotB)
    {
        if (!UsesSharedShopPool) return false;
        if (IsMasterClient)
            ProcessSharedShopMergePurchase(PhotonNetwork.LocalPlayer.ActorNumber, revision, slotA, slotB);
        else
            photonView.RPC(nameof(RPC_RequestSharedShopMergePurchase), RpcTarget.MasterClient, revision, slotA, slotB);
        return true;
    }

    public void RequestSharedShopReturn(int pokemonId, int amount)
    {
        if (!UsesSharedShopPool || pokemonId <= 0 || amount <= 0) return;
        if (IsMasterClient) ProcessSharedShopReturn(pokemonId, amount);
        else photonView.RPC(nameof(RPC_RequestSharedShopReturn), RpcTarget.MasterClient, pokemonId, amount);
    }

    /// <summary>
    /// QA 패널에서 지정한 코스트의 유닛 1마리를
    /// 현재 남아 있는 공용 풀에서 실제로 차감해 요청한다.
    ///
    /// 상점 슬롯 예약과는 무관하며 골드는 소비하지 않는다.
    /// 실제 유닛 선택과 공용 풀 차감은 MasterClient가 담당한다.
    /// </summary>
    public bool RequestSharedDebugUnitByCost(int cost)
    {
        if (!UsesSharedShopPool)
            return false;

        if (cost < 1 || cost > 5)
        {
            Debug.LogWarning(
                $"[SharedShopPool][QA] 잘못된 코스트 요청: {cost}"
            );

            return false;
        }

        int actorNumber =
            PhotonNetwork.LocalPlayer != null
                ? PhotonNetwork.LocalPlayer.ActorNumber
                : 0;

        if (actorNumber <= 0)
            return false;

        if (IsMasterClient)
        {
            ProcessSharedDebugUnitByCost(
                actorNumber,
                cost
            );
        }
        else
        {
            photonView.RPC(
                nameof(RPC_RequestSharedDebugUnitByCost),
                RpcTarget.MasterClient,
                cost
            );
        }

        return true;
    }

    [PunRPC]
    private void RPC_RequestSharedShopRoll(int level, bool forceCostFour, bool onlyCostFour, PhotonMessageInfo info)
    {
        if (!IsMasterClient || info.Sender == null) return;
        ProcessSharedShopRoll(info.Sender.ActorNumber, level, forceCostFour, onlyCostFour);
    }

    private void ProcessSharedShopRoll(int actorNumber, int level, bool forceCostFour, bool onlyCostFour)
    {
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        if (shop == null || !shop.TryAuthorityRollSharedShop(
                actorNumber, level, forceCostFour, onlyCostFour, out int revision, out int[] slots))
        {
            SendSharedShopSnapshot(actorNumber, 0, System.Array.Empty<int>());
            return;
        }

        BroadcastSharedPoolMirror(actorNumber, revision, slots);
        SendSharedShopSnapshot(actorNumber, revision, slots);
    }

    private void SendSharedShopSnapshot(int actorNumber, int revision, int[] slots)
    {
        Player target = PhotonNetwork.CurrentRoom?.GetPlayer(actorNumber);
        if (target != null)
            photonView.RPC(nameof(RPC_ApplySharedShopSnapshot), target, revision, slots ?? System.Array.Empty<int>());
    }

    [PunRPC]
    private void RPC_ApplySharedShopSnapshot(int revision, int[] slots)
    {
        GameManager.TryGet(out var gm);
        gm?.Shop?.ApplySharedShopSnapshot(revision, slots);
    }

    [PunRPC]
    private void RPC_RequestSharedShopPurchase(int revision, int slot, PhotonMessageInfo info)
    {
        if (!IsMasterClient || info.Sender == null) return;
        ProcessSharedShopPurchase(info.Sender.ActorNumber, revision, slot);
    }

    private void ProcessSharedShopPurchase(int actorNumber, int revision, int slot)
    {
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        int pokemonId = 0;
        bool success = shop != null && shop.TryAuthorityPurchaseSharedShop(
            actorNumber, revision, slot, out pokemonId);

        Player target = PhotonNetwork.CurrentRoom?.GetPlayer(actorNumber);
        if (target != null)
            photonView.RPC(nameof(RPC_ResolveSharedShopPurchase), target,
                revision, slot, success ? pokemonId : 0, success);

        if (success && shop != null)
        {
            foreach (var state in shop.GetSharedReservationsMirror())
            {
                if (state.actorNumber != actorNumber) continue;
                BroadcastSharedPoolMirror(actorNumber, state.revision, state.slots);
                break;
            }
        }
    }

    [PunRPC]
    private void RPC_ResolveSharedShopPurchase(int revision, int slot, int pokemonId, bool success)
    {
        GameManager.TryGet(out var gm);
        gm?.Shop?.ResolveSharedShopPurchase(revision, slot, pokemonId, success);
    }

    [PunRPC]
    private void RPC_RequestSharedShopMergePurchase(int revision, int slotA, int slotB, PhotonMessageInfo info)
    {
        if (!IsMasterClient || info.Sender == null) return;
        ProcessSharedShopMergePurchase(info.Sender.ActorNumber, revision, slotA, slotB);
    }

    /// <summary>
    /// 마스터 권위로 두 슬롯을 한 번에 검증·소비한다(전체 성공 또는 전체 실패).
    /// 슬롯별로 나눠 RPC를 두 번 보내지 않고, 이 한 번의 처리 안에서 두 예약을 동시에 소비한다.
    /// </summary>
    private void ProcessSharedShopMergePurchase(int actorNumber, int revision, int slotA, int slotB)
    {
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        int pokemonId = 0;
        bool success = shop != null && shop.TryAuthorityPurchaseMergeSharedShop(
            actorNumber, revision, slotA, slotB, out pokemonId);

        Player target = PhotonNetwork.CurrentRoom?.GetPlayer(actorNumber);
        if (target != null)
            photonView.RPC(nameof(RPC_ResolveSharedShopMergePurchase), target,
                revision, slotA, slotB, success ? pokemonId : 0, success);

        if (success && shop != null)
        {
            foreach (var state in shop.GetSharedReservationsMirror())
            {
                if (state.actorNumber != actorNumber) continue;
                BroadcastSharedPoolMirror(actorNumber, state.revision, state.slots);
                break;
            }
        }
    }

    [PunRPC]
    private void RPC_ResolveSharedShopMergePurchase(
        int revision, int slotA, int slotB, int pokemonId, bool success)
    {
        GameManager.TryGet(out var gm);
        gm?.Shop?.ResolveSharedShopMergePurchase(revision, slotA, slotB, pokemonId, success);
    }

    [PunRPC]
    private void RPC_RequestSharedShopReturn(int pokemonId, int amount)
    {
        if (IsMasterClient) ProcessSharedShopReturn(pokemonId, amount);
    }

    /// <summary>
    /// 비마스터 클라이언트의 QA 코스트 유닛 획득 요청을
    /// MasterClient가 수신한다.
    /// </summary>
    [PunRPC]
    private void RPC_RequestSharedDebugUnitByCost(
        int cost,
        PhotonMessageInfo info)
    {
        if (!IsMasterClient ||
            info.Sender == null)
        {
            return;
        }

        ProcessSharedDebugUnitByCost(
            info.Sender.ActorNumber,
            cost
        );
    }

    private void ProcessSharedShopReturn(int pokemonId, int amount)
    {
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        if (shop == null) return;
        shop.AuthorityReturnSharedShopCopy(pokemonId, amount);
        BroadcastSharedPoolMirror(-1, 0, System.Array.Empty<int>());
    }

    /// <summary>
    /// MasterClient가 현재 남아 있는 공용 풀에서
    /// 지정 코스트의 유닛 카피 1장을 선택하고 차감한다.
    ///
    /// 선택 확률은 ShopManager에서 남은 카피 수에 비례해 계산한다.
    /// 성공하면 요청자에게 선택된 원본 pokemonId를 전달한다.
    /// </summary>
    private void ProcessSharedDebugUnitByCost(
        int actorNumber,
        int cost)
    {
        if (!IsMasterClient)
            return;

        Player target =
            PhotonNetwork.CurrentRoom != null
                ? PhotonNetwork.CurrentRoom.GetPlayer(actorNumber)
                : null;

        if (target == null)
        {
            Debug.LogWarning(
                $"[SharedShopPool][QA] 요청 플레이어를 찾을 수 없음: " +
                $"actor={actorNumber}"
            );

            return;
        }

        if (cost < 1 || cost > 5)
        {
            photonView.RPC(
                nameof(RPC_ResolveSharedDebugUnitByCost),
                target,
                cost,
                0,
                false
            );

            return;
        }

        ShopManager shop =
            GameManager.TryGet(out var gm) ? gm.Shop : null;

        int pokemonId = 0;

        bool success =
            shop != null &&
            shop.TryAuthorityTakeDebugUnitByCost(
                cost,
                out pokemonId
            );

        /*
         * 먼저 요청자에게 결과를 전달한다.
         *
         * 요청자는 ShopManager에서 실제 벤치 생성을 시도하고,
         * 생성이나 배치에 실패하면 기존
         * RequestSharedShopReturn(pokemonId, 1)을 호출해
         * 카피를 다시 공용 풀로 돌려보낸다.
         */
        photonView.RPC(
            nameof(RPC_ResolveSharedDebugUnitByCost),
            target,
            cost,
            success ? pokemonId : 0,
            success
        );

        if (!success)
        {
            Debug.LogWarning(
                $"[SharedShopPool][QA] {cost}코스트 획득 실패 — " +
                "남은 공용 풀 없음 또는 ShopManager 처리 실패"
            );

            return;
        }

        /*
         * 공용 풀에서 실제로 1장이 빠졌으므로
         * 모든 클라이언트의 풀 미러를 갱신한다.
         *
         * actorNumber=-1:
         * 특정 상점 예약 변경이 아니라 풀 수량만 변경됐다는 의미.
         */
        BroadcastSharedPoolMirror(
            -1,
            0,
            System.Array.Empty<int>()
        );

        Debug.Log(
            $"[SharedShopPool][QA] actor={actorNumber}, " +
            $"{cost}코스트 유닛 지급 승인, pokemonId={pokemonId}"
        );
    }

    /// <summary>
    /// MasterClient가 선택·차감한 QA 유닛의 원본 ID를
    /// 요청한 클라이언트가 수신한다.
    ///
    /// 실제 생성과 벤치 배치는 로컬 ShopManager가 처리한다.
    /// </summary>
    [PunRPC]
    private void RPC_ResolveSharedDebugUnitByCost(
        int cost,
        int pokemonId,
        bool success)
    {
        ShopManager shop =
            GameManager.TryGet(out var gm) ? gm.Shop : null;

        if (shop == null)
        {
            /*
             * 정상적인 게임 씬에서는 발생하면 안 되지만,
             * ShopManager가 없는 상태에서 이미 풀이 차감됐다면
             * MasterClient에 카피 반환을 요청한다.
             */
            if (success && pokemonId > 0)
            {
                RequestSharedShopReturn(
                    pokemonId,
                    1
                );
            }

            Debug.LogError(
                "[SharedShopPool][QA] ShopManager 없음 — " +
                "QA 유닛 생성 불가"
            );

            return;
        }

        shop.ResolveSharedDebugUnitByCost(
            cost,
            pokemonId,
            success
        );
    }

    private void BroadcastSharedPoolMirror(int actorNumber, int revision, int[] slots)
    {
        if (!IsMasterClient) return;
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        if (shop == null) return;

        shop.GetSharedPoolMirror(out int[] pokemonIds, out int[] remaining);
        photonView.RPC(nameof(RPC_ApplySharedPoolMirror), RpcTarget.All,
            pokemonIds, remaining, actorNumber, revision, slots ?? System.Array.Empty<int>());
    }

    [PunRPC]
    private void RPC_ApplySharedPoolMirror(
        int[] pokemonIds, int[] remaining, int actorNumber, int revision, int[] slots)
    {
        GameManager.TryGet(out var gm);
        gm?.Shop?.ApplySharedPoolMirror(
            pokemonIds, remaining, actorNumber, revision, slots);
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
    // 통신교환
    // 유닛 즉시 전송 → 상대 통신기 대기열 저장 → 수동 수령
    // ─────────────────────────────────────────
    private sealed class TradeUnitPacket
    {
        /// <summary>송신자가 발급한 거래 고유번호.</summary>
        public int tradeId;

        public int pokemonId;
        public int starLevel;
        public int[] itemIds;
        public int stoneId;
        public int preStonePokemonId;

        /// <summary>송신 당시 필드에 배치돼 있던 유닛인지.</summary>
        public bool wasOnBoard;

        /// <summary>이미 통신진화가 완료된 유닛인지(재전송 시 ×1.4·역매핑 보존용).</summary>
        public bool isTradeEvolved;
    }

    /// <summary>
    /// 송신 후 상대방의 실제 수령 ACK를 기다리는 거래 정보.
    /// 유닛 GameObject가 없어져도 패킷만으로 유닛을 복원할 수 있어야 한다.
    /// </summary>
    private sealed class PendingOutgoingTrade
    {
        public TradeUnitPacket packet;
        public int sentRound;
        public string pokemonName;
    }

    /// <summary>파트너에게서 도착해 통신기에 대기 중인 유닛들.</summary>
    private readonly Queue<TradeUnitPacket> _incomingTradeQueue = new();

    /// <summary>
    /// 내가 전송했지만 상대방이 아직 실제로 수령하지 않은 유닛들.
    /// key = tradeId
    /// </summary>
    private readonly Dictionary<int, PendingOutgoingTrade>
        _pendingOutgoingTrades = new();

    /// <summary>로컬 거래 번호 발급용 순번.</summary>
    private int _nextTradeSequence = 1;

    /// <summary>마지막으로 유닛을 전송한 라운드.</summary>
    private int _lastTradeSentRound = -1;

    /// <summary>현재 내 통신기에 대기 중인 수신 유닛 수.</summary>
    public int PendingTradeUnitCount => _incomingTradeQueue.Count;

    /// <summary>파트너가 아직 수령하지 않은 내 송신 유닛 수.</summary>
    public int PendingOutgoingTradeCount => _pendingOutgoingTrades.Count;

    /// <summary>
    /// 플레이어별 거래 고유번호를 발급한다.
    /// 두 플레이어가 각각 같은 순번을 사용해도 ActorNumber가 달라 충돌하지 않는다.
    /// </summary>
    private int CreateTradeId()
    {
        int actorNumber =
            PhotonNetwork.LocalPlayer != null
                ? PhotonNetwork.LocalPlayer.ActorNumber
                : 0;

        int sequence = _nextTradeSequence++;

        if (_nextTradeSequence >= 1000000)
            _nextTradeSequence = 1;

        return actorNumber * 1000000 + sequence;
    }

    /// <summary>
    /// 유닛을 파트너 통신기로 전송한다.
    ///
    /// 전송 시점에 유닛은 내 보드/벤치에서 제거하지만,
    /// 판매가 아니므로 공용 상점 풀이나 아이템 인벤토리에는 반환하지 않는다.
    ///
    /// 유닛의 전체 정보는 tradeId별 송신 대기 저장소에 보존되며,
    /// 상대가 실제로 벤치에 수령한 뒤 ACK를 보내야 제거된다.
    /// </summary>
    public void SendTradeUnit(PokemonUnit unit)
    {
        if (unit == null || unit.data == null)
            return;

        if (_soloMode || !PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[Trade] 파트너 없음 — 전송 불가");
            GameEvents.TradeRejected();
            return;
        }

        if (_lastKnownRound <= 0)
        {
            Debug.LogWarning("[Trade] 라운드 시작 전에는 전송할 수 없습니다.");
            GameEvents.TradeRejected();
            return;
        }

        if (_lastTradeSentRound == _lastKnownRound)
        {
            Debug.LogWarning(
                $"[Trade] {_lastKnownRound}라운드 전송 횟수를 이미 사용했습니다."
            );

            GameEvents.TradeRejected();
            return;
        }

        Player[] others = PhotonNetwork.PlayerListOthers;

        if (others == null ||
            others.Length == 0 ||
            others[0].IsInactive)
        {
            Debug.LogWarning("[Trade] 파트너 연결 끊김 — 전송 불가");
            GameEvents.TradeRejected();
            return;
        }

        BoardManager board =
            GameManager.TryGet(out var gm) ? gm.Board : null;

        if (board == null)
        {
            Debug.LogError("[Trade] BoardManager 없음 — 전송 불가");
            GameEvents.TradeRejected();
            return;
        }

        int[] itemIds;

        if (unit.items == null || unit.items.Count == 0)
        {
            itemIds = System.Array.Empty<int>();
        }
        else
        {
            var ids = new List<int>();

            foreach (ItemData item in unit.items)
            {
                if (item != null)
                    ids.Add(item.id);
            }

            itemIds = ids.ToArray();
        }

        int stoneId =
            unit.equippedStone != null
                ? unit.equippedStone.id
                : 0;

        int preStonePokemonId =
            unit.preStoneData != null
                ? unit.preStoneData.id
                : 0;

        bool wasOnBoard = IsUnitOnBoard(board, unit);
        int tradeId = CreateTradeId();

        var packet = new TradeUnitPacket
        {
            tradeId = tradeId,
            pokemonId = unit.data.id,
            starLevel = Mathf.Clamp(unit.starLevel, 1, 3),
            itemIds = itemIds,
            stoneId = stoneId,
            preStonePokemonId = preStonePokemonId,
            wasOnBoard = wasOnBoard,
            isTradeEvolved = unit.isTradeEvolved
        };

        var pendingTrade = new PendingOutgoingTrade
        {
            packet = packet,
            sentRound = _lastKnownRound,
            pokemonName = unit.data.pokemonName
        };

        /*
         * 매우 중요:
         * RemoveUnitForTrade는 판매 처리와 달라야 한다.
         *
         * 여기서는 다음 작업이 일어나면 안 된다.
         * - 공용 상점 풀 반환
         * - 일반 장비 인벤토리 반환
         * - 진화의 돌 인벤토리 반환
         *
         * 오직 보드/벤치 점유 해제만 수행해야 한다.
         */
        if (!board.RemoveUnitForTrade(unit))
        {
            Debug.LogError(
                $"[Trade] {unit.data.pokemonName} 보드/벤치 제거 실패"
            );

            GameEvents.TradeRejected();
            return;
        }

        // GameObject를 제거하기 전에 복구 가능한 패킷을 저장한다.
        _pendingOutgoingTrades.Add(tradeId, pendingTrade);

        // 전송 요청이 정상 등록된 시점에 이번 라운드 사용 횟수를 소모한다.
        _lastTradeSentRound = _lastKnownRound;

        Destroy(unit.gameObject);

        Debug.Log(
            $"[Trade] 전송 등록: tradeId={tradeId}, " +
            $"{pendingTrade.pokemonName} ★{packet.starLevel}, " +
            $"장비 {packet.itemIds.Length}개, 돌 ID {packet.stoneId}, " +
            $"위치 {(packet.wasOnBoard ? "필드" : "벤치")}, " +
            $"ACK 대기 {_pendingOutgoingTrades.Count}마리"
        );

        photonView.RPC(
            nameof(RPC_TradeQueueReceive),
            RpcTarget.Others,
            packet.tradeId,
            packet.pokemonId,
            packet.starLevel,
            packet.itemIds,
            packet.stoneId,
            packet.preStonePokemonId,
            packet.wasOnBoard,
            packet.isTradeEvolved
        );
    }

    private static bool IsUnitOnBoard(
        BoardManager board,
        PokemonUnit targetUnit)
        {
            if (board == null || targetUnit == null)
                return false;

            foreach (PokemonUnit unit in board.GetUnitsOnBoard())
            {
                if (unit == targetUnit)
                    return true;
            }

            return false;
        }

    /// <summary>
    /// 현재 전투 페이즈 여부.
    /// 전투 중 필드 유닛의 통신진화를 다음 쇼핑 페이즈까지 지연하는 데 사용.
    /// </summary>
    private bool _isBattlePhase;

    /// <summary>
    /// 전투 중 발생해 다음 쇼핑 페이즈까지 대기 중인 통신진화 정보.
    /// 유닛 참조를 직접 저장하지 않고 원본/진화체 ID를 저장한다.
    /// 대기 중 유닛이 판매되거나 이동해도 안전하게 다시 조회하기 위함.
    /// </summary>
    private readonly List<PendingTradeEvolution> _pendingTradeEvolutions = new();

    private sealed class PendingTradeEvolution
    {
        public int originalPokemonId;
        public int evolvedPokemonId;
    }

    /// <summary>
    /// 파트너가 보낸 유닛을 통신기 FIFO 대기열에 저장한다.
    /// 이 시점에는 아직 실제 수령이 아니므로 ACK를 보내지 않는다.
    /// </summary>
    [PunRPC]
    private void RPC_TradeQueueReceive(
        int tradeId,
        int pokemonId,
        int starLevel,
        int[] itemIds,
        int stoneId,
        int preStonePokemonId,
        bool wasOnBoard,
        bool isTradeEvolved)
    {
        PokemonData data =
            PokemonDatabase.Instance != null
                ? PokemonDatabase.Instance.GetById(pokemonId)
                : null;

        if (data == null)
        {
            /*
             * ACK를 보내지 않는다.
             * 송신자의 PendingOutgoingTrade가 유지되어 소유권 데이터가
             * 사라지지 않게 한다.
             */
            Debug.LogError(
                $"[Trade] 수신 패킷 저장 실패 — " +
                $"tradeId={tradeId}, Pokemon ID {pokemonId} 조회 불가"
            );

            return;
        }

        var packet = new TradeUnitPacket
        {
            tradeId = tradeId,
            pokemonId = pokemonId,
            starLevel = Mathf.Clamp(starLevel, 1, 3),
            itemIds = itemIds ?? System.Array.Empty<int>(),
            stoneId = stoneId,
            preStonePokemonId = preStonePokemonId,
            wasOnBoard = wasOnBoard,
            isTradeEvolved = isTradeEvolved
        };

        _incomingTradeQueue.Enqueue(packet);
        GameEvents.TradeQueueChanged(_incomingTradeQueue.Count);

        Debug.Log(
            $"[Trade] 통신기 도착: tradeId={tradeId}, " +
            $"{data.pokemonName} ★{packet.starLevel}, " +
            $"현재 대기 {_incomingTradeQueue.Count}마리"
        );
    }

    /// <summary>
    /// 상대가 유닛을 실제로 벤치에 배치한 뒤 보내는 수령 완료 ACK.
    /// 해당 tradeId의 송신 대기 데이터만 제거한다.
    /// </summary>
    [PunRPC]
    private void RPC_TradeQueueAck(int tradeId)
    {
        if (!_pendingOutgoingTrades.TryGetValue(
                tradeId,
                out PendingOutgoingTrade pendingTrade))
        {
            Debug.LogWarning(
                $"[Trade] 알 수 없거나 이미 처리된 ACK 수신: tradeId={tradeId}"
            );

            return;
        }

        _pendingOutgoingTrades.Remove(tradeId);

        Debug.Log(
            $"[Trade] 수령 ACK 완료: tradeId={tradeId}, " +
            $"{pendingTrade.pokemonName} ★{pendingTrade.packet.starLevel}, " +
            $"남은 ACK 대기 {_pendingOutgoingTrades.Count}마리"
        );
    }

    /// <summary>
    /// 통신기를 클릭하면 FIFO 순서로 빈 벤치 칸만큼 연속 수령한다.
    ///
    /// 첫 번째 유닛의 복원에 실패하면 그 유닛을 Queue 맨 앞에 유지하고
    /// 뒤의 유닛을 건너뛰지 않는다.
    /// </summary>
    public bool TryReceiveNextTradeUnit()
    {
        if (_incomingTradeQueue.Count == 0)
        {
            Debug.Log("[Trade] 통신기에 대기 중인 유닛이 없습니다.");
            return false;
        }

        BoardManager board =
            GameManager.TryGet(out var gm) ? gm.Board : null;

        if (board == null)
        {
            Debug.LogError("[Trade] BoardManager 없음 — 수령 불가");
            return false;
        }

        if (!board.HasBenchSpace())
        {
            Debug.LogWarning("[Trade] 벤치가 가득 차 수령할 수 없습니다.");
            return false;
        }

        int receivedCount = 0;

        while (_incomingTradeQueue.Count > 0 &&
               board.HasBenchSpace())
        {
            /*
             * FIFO 맨 앞 유닛 수령에 실패하면 반복을 즉시 종료한다.
             * 실패 유닛을 건너뛰고 다음 유닛을 받으면 안 된다.
             */
            if (!TryReceiveFrontTradeUnit(board))
                break;

            receivedCount++;
        }

        if (receivedCount > 0)
        {
            Debug.Log(
                $"[Trade] 통신기 일괄 수령 완료: {receivedCount}마리, " +
                $"남은 대기 {_incomingTradeQueue.Count}마리"
            );
        }

        return receivedCount > 0;
    }

    /// <summary>
    /// FIFO Queue 맨 앞의 유닛 한 마리를 복원한다.
    /// 성공한 경우에만 Queue에서 제거하고 송신자에게 ACK를 보낸다.
    /// 수령 유닛은 항상 수신자의 벤치에 생성되고 벤치 유닛은 전투에 영향을 주지 않으므로,
    /// 송신자가 필드/벤치 어디서 보냈는지와 무관하게 통신진화 대상이면 무조건 즉시 진화체로 생성한다.
    /// </summary>
    private bool TryReceiveFrontTradeUnit(BoardManager board)
    {
        if (board == null ||
            _incomingTradeQueue.Count == 0 ||
            !board.HasBenchSpace())
        {
            return false;
        }

        TradeUnitPacket packet = _incomingTradeQueue.Peek();

        PokemonData originalData =
            PokemonDatabase.Instance != null
                ? PokemonDatabase.Instance.GetById(packet.pokemonId)
                : null;

        if (originalData == null)
        {
            Debug.LogError(
                $"[Trade] 대기 유닛 조회 실패 — " +
                $"tradeId={packet.tradeId}, Pokemon ID {packet.pokemonId}"
            );

            return false;
        }

        TradeEvolutionData tradeEvolution = TradeEvolutionData.Instance;

        string evolvedNameEn =
            tradeEvolution != null
                ? tradeEvolution.GetEvolved(originalData.pokemonNameEn)
                : null;

        bool isTradeEvolution =
            !string.IsNullOrEmpty(evolvedNameEn);

        PokemonData evolvedData = null;

        if (isTradeEvolution)
        {
            evolvedData =
                PokemonDatabase.Instance.GetByNameEn(evolvedNameEn);

            if (evolvedData == null)
            {
                Debug.LogError(
                    $"[Trade] 통신진화체 '{evolvedNameEn}' 조회 실패 — " +
                    $"tradeId={packet.tradeId}"
                );

                return false;
            }
        }

        // 통신진화 대상이면 지연 없이 항상 진화체로 생성한다.
        PokemonData receivedData = evolvedData ?? originalData;

        PokemonUnit receivedUnit =
            UnitFactory.Create(receivedData, packet.starLevel);

        if (receivedUnit == null)
        {
            Debug.LogError(
                $"[Trade] 유닛 생성 실패 — tradeId={packet.tradeId}"
            );

            return false;
        }

        // 일반 장비 복원
        foreach (int itemId in packet.itemIds)
        {
            ItemData item =
                ItemDatabase.Instance != null
                    ? ItemDatabase.Instance.GetById(itemId)
                    : null;

            if (item == null || !receivedUnit.TryEquipItem(item))
            {
                Destroy(receivedUnit.gameObject);

                Debug.LogError(
                    $"[Trade] 일반 장비 복원 실패 — " +
                    $"tradeId={packet.tradeId}, itemId={itemId}"
                );

                return false;
            }
        }

        // 진화의 돌 장착 상태 복원
        if (packet.stoneId > 0)
        {
            EvolutionStoneData stone =
                EvolutionStoneDatabase.Instance != null
                    ? EvolutionStoneDatabase.Instance.GetById(packet.stoneId)
                    : null;

            PokemonData preStoneData =
                packet.preStonePokemonId > 0 &&
                PokemonDatabase.Instance != null
                    ? PokemonDatabase.Instance.GetById(
                        packet.preStonePokemonId
                    )
                    : null;

            if (stone == null || preStoneData == null)
            {
                Destroy(receivedUnit.gameObject);

                Debug.LogError(
                    $"[Trade] 진화의 돌 상태 복원 실패 — " +
                    $"tradeId={packet.tradeId}, " +
                    $"stone={packet.stoneId}, " +
                    $"base={packet.preStonePokemonId}"
                );

                return false;
            }

            receivedUnit.equippedStone = stone;
            receivedUnit.preStoneData = preStoneData;
        }

        // 이미 통신진화 완료 상태로 배송된 유닛은 그 상태를 그대로 복원하고,
        // 이번 수령으로 새로 통신진화 대상이 된 경우도 즉시 반영한다(지연 없음).
        receivedUnit.isTradeEvolved =
            packet.isTradeEvolved || isTradeEvolution;

        // 진화체로 받은 경우에만 연출. 베이스 핸드오버는 진화가 아니다.
        if (receivedUnit.isTradeEvolved) GameEvents.UnitEvolved(receivedUnit, false);

        receivedUnit.ResetForBattle();

        if (!board.TryPlaceInBench(receivedUnit))
        {
            Destroy(receivedUnit.gameObject);

            Debug.LogWarning(
                $"[Trade] 벤치 배치 실패 — tradeId={packet.tradeId}"
            );

            return false;
        }

        /*
         * 여기까지 성공해야 유닛 소유권이 수신자에게 실제로 이동한 것이다.
         *
         * 상점 풀:
         * - 새 구매가 아니므로 추가 차감 없음
         * - 판매가 아니므로 반환 없음
         *
         * 아이템:
         * - 인벤토리에서 새로 꺼내지 않음
         * - 송신자의 인벤토리로 반환하지 않음
         * - 패킷에 귀속된 상태 그대로 복원
         */

        _incomingTradeQueue.Dequeue();
        GameEvents.TradeQueueChanged(_incomingTradeQueue.Count);

        GameManager.TryGet(out var gm);

        if (isTradeEvolution && evolvedData != null)
        {
            // 수령 유닛은 벤치에만 생성되고 전투에 영향을 주지 않으므로 지연 없이 즉시 진화 처리한다.
            ApplyTradeEvolutionToOwnedUnits(
                originalData,
                evolvedData,
                receivedUnit
            );

            gm?.Shop?.ActivateTradeEvolution(
                originalData.id,
                evolvedData.id
            );

            board.RecheckEvolution(receivedUnit);
        }
        else if (packet.isTradeEvolved)
        {
            /*
             * 이미 통신진화가 완료된 유닛을 재전송받은 경우:
             * 새 진화 사건이 아니므로 해금/광역진화는 발생시키지 않고,
             * 판매 시 원본 종 풀 반환을 위한 역매핑만 등록한다.
             */
            string baseNameEn =
                tradeEvolution != null
                    ? tradeEvolution.GetBaseOf(originalData.pokemonNameEn)
                    : null;

            PokemonData tradeBaseData =
                !string.IsNullOrEmpty(baseNameEn) &&
                PokemonDatabase.Instance != null
                    ? PokemonDatabase.Instance.GetByNameEn(baseNameEn)
                    : null;

            if (tradeBaseData != null)
            {
                gm?.Shop?.RegisterEvolvedToBase(
                    originalData.id,
                    tradeBaseData.id
                );
            }
            else
            {
                Debug.LogWarning(
                    $"[Trade] 통신진화체 '{originalData.pokemonNameEn}'의 " +
                    "원본 종 역조회 실패 — 판매 풀 반환이 어긋날 수 있음"
                );
            }
        }

        /*
         * 유닛 생성, 장비 복원, 진화 상태 복원, 벤치 배치가
         * 전부 성공한 뒤에만 해당 tradeId ACK를 보낸다.
         */
        photonView.RPC(
            nameof(RPC_TradeQueueAck),
            RpcTarget.Others,
            packet.tradeId
        );

        string evolutionText =
            isTradeEvolution &&
            evolvedData != null
                ? " → " + evolvedData.pokemonName
                : "";

        // 진화 판정에는 더 이상 사용하지 않고, 수령 로그 기록용으로만 남긴다.
        string originText =
            packet.wasOnBoard
                ? " (필드 출신 유닛 수령)"
                : "";

        Debug.Log(
            $"[Trade] 수령 완료: tradeId={packet.tradeId}, " +
            $"{originalData.pokemonName}{evolutionText} " +
            $"★{receivedUnit.starLevel}{originText}, " +
            $"남은 대기 {_incomingTradeQueue.Count}마리"
        );

        GameEvents.TradeUnitReceived(receivedUnit);

        return true;
    }

    /// <summary>
    /// 통신진화 수령 시 받는 플레이어가 보유한 동일 원본 유닛을 진화시킨다.
    ///
    /// 쇼핑 상태:
    /// - 필드 유닛 즉시 진화
    /// - 벤치 유닛 즉시 진화
    ///
    /// 전투 상태:
    /// - 필드 유닛은 다음 쇼핑 상태까지 진화 대기
    /// - 벤치 유닛은 즉시 진화
    /// </summary>
    private void ApplyTradeEvolutionToOwnedUnits(
        PokemonData originalData,
        PokemonData evolvedData,
        PokemonUnit receivedUnit)
    {
        BoardManager board =
            GameManager.TryGet(out var gm) ? gm.Board : null;

        if (board == null ||
            originalData == null ||
            evolvedData == null)
        {
            return;
        }

        int changedCount = 0;
        int pendingCount = 0;

        // 필드 유닛
        foreach (PokemonUnit unit in board.GetUnitsOnBoard())
        {
            if (unit == null ||
                unit == receivedUnit ||
                unit.data == null ||
                unit.data.id != originalData.id)
            {
                continue;
            }

            if (_isBattlePhase)
            {
                pendingCount++;
                continue;
            }

            if (TryApplyTradeEvolution(
                    unit,
                    receivedUnit,
                    originalData,
                    evolvedData))
            {
                changedCount++;
                GameEvents.UnitPlaced(unit);
            }
        }



        // 벤치 유닛은 전투 중에도 즉시 진화
        foreach (PokemonUnit unit in board.GetUnitsInBench())
        {
            if (TryApplyTradeEvolution(
                    unit,
                    receivedUnit,
                    originalData,
                    evolvedData))
            {
                changedCount++;
                GameEvents.UnitBenched(unit);
            }
        }

        if (_isBattlePhase && pendingCount > 0)
        {
            AddPendingTradeEvolution(
                originalData.id,
                evolvedData.id
            );
        }

        Debug.Log(
            $"[TradeEvolution] {originalData.pokemonName} → " +
            $"{evolvedData.pokemonName} 즉시 진화 {changedCount}마리, " +
            $"필드 진화 대기 {pendingCount}마리"
        );
    }

    private static bool TryApplyTradeEvolution(
        PokemonUnit unit,
        PokemonUnit receivedUnit,
        PokemonData originalData,
        PokemonData evolvedData)
    {
        if (unit == null ||
            unit == receivedUnit ||
            unit.data == null ||
            unit.data.id != originalData.id)
        {
            return false;
        }

        unit.data = evolvedData;
        unit.isTradeEvolved = true;
        unit.ResetForBattle();

        return true;
    }

    /// <summary>
    /// 전투 중 지연된 통신진화 정보를 등록한다.
    /// 같은 원본/진화체 조합은 중복 저장하지 않는다.
    /// </summary>
    private void AddPendingTradeEvolution(
        int originalPokemonId,
        int evolvedPokemonId)
    {
        foreach (PendingTradeEvolution pending
                 in _pendingTradeEvolutions)
        {
            if (pending.originalPokemonId == originalPokemonId &&
                pending.evolvedPokemonId == evolvedPokemonId)
            {
                return;
            }
        }

        _pendingTradeEvolutions.Add(
            new PendingTradeEvolution
            {
                originalPokemonId = originalPokemonId,
                evolvedPokemonId = evolvedPokemonId
            }
        );

        Debug.Log(
            $"[TradeEvolution] 진화 대기 등록: " +
            $"{originalPokemonId} → {evolvedPokemonId}"
        );
    }

    /// <summary>
    /// 쇼핑 페이즈 진입 시 전투 중 지연된 통신진화를 적용한다.
    /// </summary>
    private void ApplyPendingTradeEvolutions()
    {
        if (_pendingTradeEvolutions.Count == 0)
            return;

        BoardManager board =
            GameManager.TryGet(out var gm) ? gm.Board : null;

        PokemonDatabase database = PokemonDatabase.Instance;

        if (board == null || database == null)
        {
            Debug.LogWarning(
                "[TradeEvolution] 대기 진화 적용 실패 — " +
                "BoardManager 또는 PokemonDatabase 없음"
            );

            return;
        }

        int changedCount = 0;
        var changedUnits = new List<PokemonUnit>();

        foreach (PendingTradeEvolution pending
                 in _pendingTradeEvolutions)
        {
            PokemonData originalData =
                database.GetById(pending.originalPokemonId);

            PokemonData evolvedData =
                database.GetById(pending.evolvedPokemonId);

            if (originalData == null || evolvedData == null)
            {
                Debug.LogError(
                    $"[TradeEvolution] 대기 진화 데이터 조회 실패 " +
                    $"({pending.originalPokemonId} → " +
                    $"{pending.evolvedPokemonId})"
                );

                continue;
            }

            foreach (PokemonUnit unit in board.GetUnitsOnBoard())
            {
                if (!TryApplyTradeEvolution(
                        unit,
                        null,
                        originalData,
                        evolvedData))
                {
                    continue;
                }

                changedCount++;
                changedUnits.Add(unit);
                GameEvents.UnitPlaced(unit);
            }

            foreach (PokemonUnit unit in board.GetUnitsInBench())
            {
                if (!TryApplyTradeEvolution(
                        unit,
                        null,
                        originalData,
                        evolvedData))
                {
                    continue;
                }

                changedCount++;
                changedUnits.Add(unit);
                GameEvents.UnitBenched(unit);
            }
        }

        _pendingTradeEvolutions.Clear();

        foreach (PokemonUnit unit in changedUnits)
        {
            if (unit != null)
                board.RecheckEvolution(unit);
        }

        Debug.Log(
            $"[TradeEvolution] 쇼핑 페이즈 진입 — " +
            $"대기 유닛 {changedCount}마리 진화 완료"
        );
    }

    // ─────────────────────────────────────────
    // 통신기 골드 전송 (선차감 → 상대 수신 → ack, 실패 시 환급)
    // ─────────────────────────────────────────

    /// <summary>ack 대기 중인 송금액. 0 = 대기 없음. 실패 ack 시 이만큼 환급.</summary>
    private int _pendingGoldTransfer;

    /// <summary>
    /// 내 골드를 파트너에게 전송. 골드는 각자 로컬 권위(ShopManager)라 통신교환과 같은
    /// 피어 ack 모델을 쓴다 — 보내는 쪽이 자기 잔액을 검증·선차감하고, 받는 쪽이 자기 권위로 가산.
    /// 선차감이라 ack 대기 중 이중 송금/초과 지출이 불가능하고, 실패 ack 시 전액 환급된다.
    /// </summary>
    public void SendGoldToPartner(int amount)
    {
        if (amount <= 0) return;
        if (_soloMode || !PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[GoldTransfer] 파트너 없음 — 전송 불가");
            GameEvents.GoldTransferRejected("파트너 없음");
            return;
        }
        if (_pendingGoldTransfer > 0)
        {
            Debug.LogWarning("[GoldTransfer] 이전 전송 처리 중");
            GameEvents.GoldTransferRejected("이전 전송 처리 중");
            return;
        }

        // 유예시간(재접속 대기) 중인 파트너에게 보낸 RPC는 버퍼되지 않고 유실됨 — 골드 증발 방지.
        var others = PhotonNetwork.PlayerListOthers;
        if (others == null || others.Length == 0 || others[0].IsInactive)
        {
            Debug.LogWarning("[GoldTransfer] 파트너 연결 끊김 — 전송 불가");
            GameEvents.GoldTransferRejected("파트너 연결 끊김");
            return;
        }

        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        if (shop == null || shop.Gold < amount)
        {
            Debug.LogWarning($"[GoldTransfer] 골드 부족 — 보유 {(shop != null ? shop.Gold : 0)} < 요청 {amount}");
            GameEvents.GoldTransferRejected("골드 부족");
            return;
        }

        shop.AddGold(-amount); // 선차감 (OnGoldChanged → BoardSyncBroadcaster가 파트너 표시 동기화)
        _pendingGoldTransfer = amount;
        Debug.Log($"[GoldTransfer] 전송 요청: {amount}G → 파트너");
        photonView.RPC(nameof(RPC_GoldReceive), RpcTarget.Others, amount);
    }

    private void HandleGoldTransferRequested(int amount)
    {
        SendGoldToPartner(amount);
    }

    [PunRPC]
    private void RPC_GoldReceive(int amount)
    {
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        if (amount <= 0 || shop == null)
        {
            Debug.LogWarning($"[GoldTransfer] 수신 거부 (amount={amount}, shop={(shop != null)})");
            photonView.RPC(nameof(RPC_GoldTransferAck), RpcTarget.Others, false);
            return;
        }

        shop.AddGold(amount);
        Debug.Log($"[GoldTransfer] 수신: 파트너에게서 {amount}G");
        GameEvents.PartnerGoldReceived(amount);
        photonView.RPC(nameof(RPC_GoldTransferAck), RpcTarget.Others, true);
    }

    [PunRPC]
    private void RPC_GoldTransferAck(bool success)
    {
        if (_pendingGoldTransfer <= 0) return;
        int amount = _pendingGoldTransfer;
        _pendingGoldTransfer = 0;

        if (!success)
        {
            var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
            shop?.AddGold(amount); // 환급
            Debug.LogWarning($"[GoldTransfer] 전송 실패 — {amount}G 환급");
            GameEvents.GoldTransferRejected("상대 수신 실패 — 환급됨");
            return;
        }

        Debug.Log($"[GoldTransfer] 전송 완료: {amount}G");
        GameEvents.GoldTransferCompleted(amount);
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
        photonView.RPC(nameof(RPC_OnBoardSnapshot), RpcTarget.Others, ++_localBoardRevision, data);
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

    /// <summary>내 누적 증강 목록(영문명, 선택 순)을 Player CustomProperties에 기록 → 파트너 클라가 표시/전적 기록.</summary>
    public void SyncLocalAugments(string[] augmentNamesEn)
    {
        if (_soloMode || !PhotonNetwork.InRoom) return;
        var props = new Hashtable { { AUGMENTS_PROP_KEY, augmentNamesEn ?? System.Array.Empty<string>() } };
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
        if (DebugInfiniteTeamHealth)
        {
            Debug.Log("[Network] 디버그 무한 HP — 팀 데미지 무시");
            return;
        }

        if (_soloMode)
        {
            _soloTeamHp = Mathf.Max(0, _soloTeamHp - damage);
            GameEvents.HealthChanged(_soloTeamHp);
            if (_soloTeamHp <= 0) GameEvents.SessionEnded(SessionEndReason.TeamHpZero);
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

    /// <summary>상대 보드 스냅샷 수신 → 미러 렌더용 이벤트 발행.
    /// 재접속 직후 재송출과 일반 송출이 교차 도착할 수 있어, 과거 revision은 버려 최신 미러를 보존한다.</summary>
    [PunRPC]
    private void RPC_OnBoardSnapshot(int revision, int[] data)
    {
        if (revision <= _lastOpponentBoardRevision)
        {
            Debug.Log($"[Network] 과거 보드 스냅샷 무시 (rev {revision} ≤ {_lastOpponentBoardRevision})");
            return;
        }
        _lastOpponentBoardRevision = revision;
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
        if (round == 1) props[AUGMENTS_PROP_KEY] = System.Array.Empty<string>(); // 새 판 — 이전 판 증강 잔존 방지
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        _roundResultResolved = false; // (MasterClient 집계 가드 리셋)
        _lastKnownRound = round;      // 재접속 라운드 복구 기준점

        if (round == 1)
        {
            // 새 판 — 보드 스냅샷 revision 리셋(이전 판의 revision과 비교되지 않도록)
            _localBoardRevision = 0;
            _lastOpponentBoardRevision = -1;
        }

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

        _gracePeriodExpiredFired = false;

        if (_opponentGraceRoutine != null)
        {
            StopCoroutine(_opponentGraceRoutine);
            _opponentGraceRoutine = null;
            Debug.Log("[Network] 상대 재접속 성공 — 유예 타이머 취소");
            GameEvents.OpponentReconnected();
        }

        // 진행 중 매치에 파트너가 재입장 — 파트너가 자리를 비운 사이 보낸 내 스냅샷 RPC는
        // 유실됐으므로 현재 보드를 재송출해 파트너 쪽 미러를 복구시킨다.
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(MATCH_GUID_ROOM_KEY))
            GameEvents.BoardResyncRequested();

        if (PlayerCount == MAX_PLAYERS)
            OnRoomFull();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"[Network] {otherPlayer.NickName} 퇴장 (Inactive: {otherPlayer.IsInactive})");

        if (!otherPlayer.IsInactive)
        {
            // 자진 퇴장(룸 영구 이탈) 또는 PlayerTtl 만료로 인한 서버측 제거 — 유예 없이 바로 처리.
            // 후자는 로컬 유예 코루틴과 같은 이탈 건이므로 FireGracePeriodExpired가 중복 발행을 막는다.
            if (_opponentGraceRoutine != null)
            {
                StopCoroutine(_opponentGraceRoutine);
                _opponentGraceRoutine = null;
            }
            FireGracePeriodExpired(false);
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
        FireGracePeriodExpired(bothDisconnected);
    }

    /// <summary>같은 이탈 건에 대해 GracePeriodExpired를 한 번만 발행한다. 파트너 재입장 시 리셋.</summary>
    private void FireGracePeriodExpired(bool bothDisconnected)
    {
        if (_gracePeriodExpiredFired) return;
        _gracePeriodExpiredFired = true;
        GameEvents.GracePeriodExpired(bothDisconnected);
    }

    private void OnRoomFull()
    {
        // 2인 모두 입장 → 방 닫고 게임 시작
        Debug.Log("[Network] 2인 모두 입장 — 게임 시작");

        if (!IsMasterClient) return;

        // 재접속 시 OnJoinedRoom/OnPlayerEnteredRoom이 다시 호출될 수 있다.
        // 이미 생성된 매치라면 씬을 다시 로드하지 않아 공유 상점 풀과 예약을 보존한다.
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(MATCH_GUID_ROOM_KEY))
        {
            Debug.Log("[Network] 기존 매치 재입장 — 게임 씬 재로드 생략");
            return;
        }

        PhotonNetwork.CurrentRoom.IsOpen = false;

        // 이번 판의 matchId(GUID)를 Room 속성으로 배포 — 두 클라이언트가 같은 값으로 전적을 묶는다.
        // 씬 로드 전에 설정해 게임 시작 시점엔 양쪽 모두 동기화돼 있음.
        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable { { MATCH_GUID_ROOM_KEY, System.Guid.NewGuid().ToString("N") } });

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

        // 파트너 증강 목록: 다른 플레이어의 Augments가 바뀌면 이벤트 발행(모든 클라).
        if (targetPlayer != PhotonNetwork.LocalPlayer &&
            changedProps.TryGetValue(AUGMENTS_PROP_KEY, out object augments) &&
            augments is string[] augmentNames)
        {
            GameEvents.PartnerAugmentsChanged(augmentNames);
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
            GameEvents.SessionEnded(SessionEndReason.TeamHpZero);
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
                ResyncAfterReconnect();
                yield break;
            }

            yield return new WaitForSeconds(1f);
            elapsed += 1f;
        }

        Debug.LogWarning("[Network] 재접속 실패 — 세션 종료(패배 처리)");
        _selfReconnectRoutine = null;
        GameEvents.SessionEnded(SessionEndReason.ReconnectFailed);
    }

    /// <summary>
    /// 본인 재접속 성공 직후 상태 복구.
    /// - 내 보드 스냅샷 재송출: 끊긴 동안의 내 보드 변경은 송출이 막혀 파트너 미러가 낡아 있다.
    /// - 파트너 골드/증강/팀 HP 재발행: 속성 자체는 최신이지만 변경 "이벤트"를 못 받아 UI가 낡아 있다.
    /// - 라운드 복구: 끊긴 동안 라운드가 진행됐으면 Room 속성 기준으로 현재 라운드에 쇼핑부터 재진입.
    /// (상대 보드 미러 복구는 상대 클라이언트가 OnPlayerEnteredRoom에서 재송출해 처리된다.)
    /// </summary>
    private void ResyncAfterReconnect()
    {
        GameEvents.BoardResyncRequested();

        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player == PhotonNetwork.LocalPlayer) continue;

            if (player.CustomProperties.TryGetValue(GOLD_PROP_KEY, out object gold))
                GameEvents.PartnerGoldChanged((int)gold);

            if (player.CustomProperties.TryGetValue(AUGMENTS_PROP_KEY, out object augments) &&
                augments is string[] augmentNames)
                GameEvents.PartnerAugmentsChanged(augmentNames);
        }

        int teamHp = TeamHealth;
        if (teamHp >= 0) GameEvents.HealthChanged(teamHp);

        // 끊긴 동안 라운드가 진행됐으면 현재 라운드로 재진입.
        // RPC_OnRoundStart를 로컬 직접 호출해 일반 라운드 시작과 같은 리셋 절차(준비/전투결과 속성 초기화)를 탄다.
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROUND_PROP_KEY, out object roundObj))
        {
            int currentRound = (int)roundObj;
            if (currentRound > _lastKnownRound)
            {
                Debug.Log($"[Network] 재접속 라운드 복구: {_lastKnownRound} → {currentRound}");
                RPC_OnRoundStart(currentRound);
            }
        }
    }

    // ─────────────────────────────────────────
    // 테스트용 게임 재시작
    // 마스터가 전체 클라이언트에 재시작 RPC를 보내고,
    // 각 클라이언트가 자기 로컬 게임 씬을 직접 다시 로드한다.
    // ─────────────────────────────────────────

    public void RestartGame()
    {
        if (_soloMode)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
            );
            return;
        }

        if (!PhotonNetwork.InRoom || !IsMasterClient)
        {
            Debug.LogWarning("[Restart] 마스터 클라이언트만 재시작할 수 있습니다.");
            return;
        }

        string sceneName =
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        Debug.Log("[Restart] 전체 클라이언트 새 판 재시작 요청");

        // 이전 판 Room 상태 초기화
        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable
            {
            { ROUND_PROP_KEY, 0 },
            { TEAM_HP_PROP_KEY, -1 },
            {
                MATCH_GUID_ROOM_KEY,
                System.Guid.NewGuid().ToString("N")
            }
            }
        );

        // 같은 씬을 PhotonNetwork.LoadLevel로 다시 불러오면
        // 파트너가 씬 변경으로 인식하지 않을 수 있으므로
        // 양쪽 클라이언트가 직접 로컬 씬을 재로드한다.
        photonView.RPC(
            nameof(RPC_RestartGame),
            RpcTarget.AllViaServer,
            sceneName
        );
    }

    [PunRPC]
    private void RPC_RestartGame(string sceneName)
    {
        Debug.Log($"[Restart] 재시작 명령 수신: {sceneName}");

        // 이전 판 Player 상태 초기화
        PhotonNetwork.LocalPlayer.SetCustomProperties(
            new Hashtable
            {
            { SCENE_READY_PROP_KEY, false },
            { READY_PROP_KEY, false },
            { BATTLE_RESULT_PROP_KEY, RESULT_NOT_REPORTED },
            { GOLD_PROP_KEY, 0 },
            { AUGMENTS_PROP_KEY, System.Array.Empty<string>() }
            }
        );

        // 현재 NetworkManager 로컬 상태 초기화
        _gameStarted = false;
        _roundResultResolved = false;

        _lastKnownRound = 0;
        _localBoardRevision = 0;
        _lastOpponentBoardRevision = -1;

        // 통신교환 상태 초기화
        _pendingOutgoingTrades.Clear();
        _nextTradeSequence = 1;
        _lastTradeSentRound = -1;

        _incomingTradeQueue.Clear();
        _pendingTradeEvolutions.Clear();
        _isBattlePhase = false;

        GameEvents.TradeQueueChanged(0);

        // 골드 전송 대기 상태 초기화
        _pendingGoldTransfer = 0;

        StartCoroutine(ReloadGameSceneRoutine(sceneName));
    }

    private System.Collections.IEnumerator ReloadGameSceneRoutine(
        string sceneName)
    {
        // CustomProperties가 서버에 전달될 시간을 잠깐 확보
        yield return new WaitForSecondsRealtime(0.3f);

        Debug.Log($"[Restart] 로컬 씬 재로드: {sceneName}");

        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
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
    public bool UsesSharedShopPool => false;

    // 실구현과 동일한 표면(전적 기록 등에서 사용).
    public string RoomName        => "offline";
    public string LocalNickname   => "OfflinePlayer";
    public string PartnerNickname => "";
    public string MatchGuid       => ""; // 오프라인은 GUID 미발급 — MatchRecorder가 구형 방식으로 폴백

    private int _teamHp = -1;
    public int  TeamHealth     => _teamHp;

    private void OnEnable()
    {
        GameEvents.OnPlayerReadyApproved += BroadcastPlayerReady;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerReadyApproved -= BroadcastPlayerReady;
    }

    /// <summary>디버그: 켜지면 팀 공통 HP가 절대 깎이지 않음(무한 HP). PrototypeHud에서 토글.</summary>
    public static bool DebugInfiniteTeamHealth = false;

    private void Start()
    {
        Debug.LogWarning("[Network] PUN2 미설치 — 오프라인 모드로 실행 중");
    }

    /// <summary>
    /// PUN2 미설치 환경에서는 Photon 닉네임을 설정하지 않는다.
    /// 실제 구현부와 동일한 공개 API를 유지하기 위한 오프라인 스텁.
    /// </summary>
    public bool TrySetLocalNickname(string nickname)
    {
        Debug.LogWarning("[Network] 오프라인 모드에서는 Photon 닉네임을 설정할 수 없습니다.");
        return false;
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
    public void SyncLocalAugments(string[] _)   { }
    public bool RequestSharedShopRoll(int level, bool forceCostFour, bool onlyCostFour = false) => false;
    public bool RequestSharedShopPurchase(int revision, int slot) => false;
    public bool RequestSharedShopMergePurchase(int revision, int slotA, int slotB) => false;
    public void RequestSharedShopReturn(int pokemonId, int amount) { }

    /// <summary>
    /// 오프라인에서는 공유 풀이 없으므로 false.
    /// ShopManager가 로컬 풀 직접 획득 방식으로 처리한다.
    /// </summary>
    public bool RequestSharedDebugUnitByCost(int cost) => false;

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
        if (DebugInfiniteTeamHealth)
        {
            Debug.Log("[Network] 디버그 무한 HP — 팀 데미지 무시");
            return;
        }
        if (_teamHp < 0) return;
        _teamHp = Mathf.Max(0, _teamHp - damage);
        GameEvents.HealthChanged(_teamHp);
        if (_teamHp <= 0) GameEvents.SessionEnded(SessionEndReason.TeamHpZero);
    }

    /// <summary>오프라인(1인=팀): 승=BothWin, 패=BothLose(라이프 -1). 즉시 판정.</summary>
    public void ReportBattleResult(bool isWin)
    {
        if (!isWin) ReportBattleLoss(1);   // 라이프 -1
        GameEvents.TeamRoundResolved(isWin ? TeamRoundOutcome.BothWin : TeamRoundOutcome.BothLose);
    }

    /// <summary>오프라인은 파트너가 없어 통신교환 불가.</summary>
    public void SendTradeUnit(PokemonUnit unit) => Debug.LogWarning("[Trade] 오프라인 — 파트너 없음, 전송 불가");

    /// <summary>오프라인은 파트너가 없어 골드 전송 불가.</summary>
    public void SendGoldToPartner(int amount)
    {
        Debug.LogWarning("[GoldTransfer] 오프라인 — 파트너 없음, 전송 불가");
        GameEvents.GoldTransferRejected("파트너 없음");
    }

    public int PendingTradeUnitCount => 0;

    public bool TryReceiveNextTradeUnit()
    {
        Debug.LogWarning("[Trade] 오프라인 — 수령할 파트너 유닛 없음");
        return false;
    }

    public void RestartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
        );
    }
}

#endif
