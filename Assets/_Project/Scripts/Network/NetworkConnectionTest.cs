#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// 연결 테스트용 임시 스크립트. 검증 후 삭제 예정.
/// </summary>
public class NetworkConnectionTest : MonoBehaviourPunCallbacks
{
    /// <summary>타이틀 로그인 화면 상태(표시 전용 — 실제 네트워크 로직은 전부 NetworkManager가 갖는다).</summary>
    private enum TitleUiState
    {
        NormalLogin,
        PreviousSessionAvailable,
        Rejoining,
        RejoinFailed,
        ConfirmStartNew,
        AbandoningPreviousSession,
        PrevRoomPrompt
    }

    private NetworkManager _network;

    // 재접속 확인용 QA 표시 전용 — NetworkManager의 동일한 private 키를 읽기만 한다(저장/삭제 없음).
    // NetworkManager 코드를 건드리지 않기 위해 문자열을 그대로 미러링.
    private const string DEBUG_PREF_LAST_ROOM_NAME = "LastPhotonRoomName";

    private GUIStyle _titleStyle;
    private GUIStyle _sectionStyle;
    private GUIStyle _infoStyle;

    // RoomName 기준 캐시. Photon의 OnRoomListUpdate는 델타 업데이트라 매번 통으로 덮어쓰면 안 된다
    // (RemovedFromList 항목이 유효한 방인 것처럼 남거나, 이번에 언급 안 된 기존 방이 사라짐 — 2026-08 확인).
    private readonly Dictionary<string, RoomInfo> _roomCache = new();
    private Vector2 _roomListScroll;
    private string _nicknameInput = "";

    // 닉네임 입력칸에 저장된 마지막 닉네임을 기본값으로 한 번만 채우기 위한 가드.
    private bool _nicknameInitialized;

    // OnRoomListUpdate를 한 번이라도 받았는지 — 로비 입장 직후의 "아직 목록 없음"과
    // "실제로 방이 없음"을 구분해 표시하는 데 쓴다.
    private bool _roomListReceivedAtLeastOnce;

    // [새로 시작하기] 확인 팝업이 열려 있는지(로컬 UI 전용 상태 — 취소 시 저장값은 그대로 유지).
    private bool _confirmStartNewOpen;

    // 사용자가 [새로 시작하기]를 확정해 AbandonPreviousSession()을 호출했는지. IsRejoining 상태의
    // 문구를("이전 게임에 다시 접속하는 중입니다..." vs "재접속을 포기하는 중입니다...") 구분하는 데만 쓴다.
    private bool _abandonRequested;

    // [이전 방 입장] 팝업이 열려 있는지(로컬 UI 전용 상태). 입력 닉네임과 실패 메시지도 팝업 전용.
    private bool _prevRoomPopupOpen;
    private string _prevRoomNicknameInput = "";
    private string _prevRoomFailMessage;

    private void Awake()
    {
        _network = GetComponent<NetworkManager>();
        if (_network == null)
            _network = gameObject.AddComponent<NetworkManager>();
    }

    private void Start()
    {
        GameEvents.OnRoundChanged += round =>
            Debug.Log($"[Test] OnRoundChanged → {round}");

        _network.Connect();
    }

    /// <summary>Photon 룸 리스트는 델타 업데이트다 — RemovedFromList면 캐시에서 제거, 아니면 갱신/추가한다.</summary>
    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        Debug.Log($"[RoomList] Received Count : {roomList.Count}");

        foreach (var room in roomList)
        {
            Debug.Log($"[RoomList] RoomName={room.Name} PlayerCount={room.PlayerCount} " +
                      $"MaxPlayers={room.MaxPlayers} IsOpen={room.IsOpen} IsVisible={room.IsVisible} " +
                      $"RemovedFromList={room.RemovedFromList}");
        }

        _roomListReceivedAtLeastOnce = true;

        foreach (var room in roomList)
        {
            if (room.RemovedFromList)
                _roomCache.Remove(room.Name);
            else
                _roomCache[room.Name] = room;
        }
    }

    /// <summary>로비를 새로 들어올 때마다 이전 로비의 캐시가 남지 않도록 비운다.</summary>
    public override void OnJoinedLobby()
    {
        _roomCache.Clear();
        _roomListReceivedAtLeastOnce = false;
    }

    private static Rect CenteredRect(float width, float height)
    {
        width = Mathf.Min(width, Screen.width - 20f);
        height = Mathf.Min(height, Screen.height - 20f);
        float x = (Screen.width - width) / 2f;
        float y = (Screen.height - height) / 2f;
        return new Rect(x, y, width, height);
    }

    private void EnsureStyles()
    {
        if (_titleStyle != null) return;

        _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
        _sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
        _infoStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
    }

    private void OnGUI()
    {
        EnsureStyles();

        // 디버그 정보는 화면 크기와 무관하게 항상 좌상단(개발용). QA 확인 편의를 위해 한글 표시 + 글자 크기 확대.
        // 영역 높이만 확대(항목이 잘리던 문제 수정) — 표시 내용/참조 방식은 변경하지 않음.
        GUILayout.BeginArea(new Rect(10, 10, 380, 340));

        GUILayout.Label("===== 네트워크 상태 =====", _titleStyle);
        GUILayout.Label($"포톤 연결 : {(_network.IsConnected ? "연결됨" : "끊김")}", _infoStyle);
        GUILayout.Label($"현재 방 : {(string.IsNullOrEmpty(_network.RoomName) ? "없음" : _network.RoomName)}", _infoStyle);
        GUILayout.Label($"방 인원 : {_network.PlayerCount}/{PhotonNetwork.CurrentRoom?.MaxPlayers ?? 0}", _infoStyle);
        GUILayout.Label($"마스터 여부 : {(_network.IsMasterClient ? "YES" : "NO")}", _infoStyle);
        GUILayout.Label($"자동 씬 동기화 : {(PhotonNetwork.AutomaticallySyncScene ? "ON" : "OFF")}", _infoStyle);
        GUILayout.Label($"현재 씬 : {SceneManager.GetActiveScene().name}", _infoStyle);

        // 닉네임: 현재 세션값(PhotonNetwork.NickName) 대신 마지막으로 정상 저장된 닉네임(SavedNickname —
        // 기존 LastPhotonNickname 구조, NetworkManager가 이미 관리)을 표시 — 타이틀 화면에선 아직 이번
        // 세션 닉네임을 입력하지 않았을 수 있어 "마지막 게임 정보" 취지에 더 맞는다.
        string lastNickname = string.IsNullOrEmpty(_network.SavedNickname) ? "저장 정보 없음" : _network.SavedNickname;
        GUILayout.Label($"닉네임 : {lastNickname}", _infoStyle);

        // 파트너: 현재 방에 실제로 함께 있는 상대(PartnerNickname)를 우선 표시하고, 방에 없으면(타이틀
        // 화면 등) 별도로 저장된 구조가 없으므로 "저장 정보 없음"으로 표시(새 저장 구조 추가하지 않음).
        string partnerDisplay = string.IsNullOrEmpty(_network.PartnerNickname) ? "저장 정보 없음" : _network.PartnerNickname;
        GUILayout.Label($"파트너 : {partnerDisplay}", _infoStyle);

        // 재접속 QA 정보 — 별도 섹션 제목 없이 네트워크 상태 목록에 통합.
        GUILayout.Label($"저장된 방 이름 : {ReadDebugPref(DEBUG_PREF_LAST_ROOM_NAME, "저장 정보 없음")}", _infoStyle);
        GUILayout.Label($"재접속 가능 여부 : {(_network.HasSavedSession ? "가능" : "불가")}", _infoStyle);

        GUILayout.Label("========================", _titleStyle);

        GUILayout.EndArea();

        if (!_network.IsInRoom)
        {
            // 로그인/이전 세션/방 리스트 UI는 화면 중앙에 배치 — 해상도가 바뀌어도 항상 중앙 유지.
            GUILayout.BeginArea(CenteredRect(380, 480), GUI.skin.box);
            DrawTitleLogin();
            GUILayout.EndArea();
        }
        else
        {
            GUILayout.BeginArea(new Rect(10, 360, 320, 90));

            if (GUILayout.Button("Broadcast Round 1"))
                _network.BroadcastRoundStart(1);

            if (GUILayout.Button("Leave Room"))
                _network.LeaveRoom();

            GUILayout.EndArea();
        }
    }

    /// <summary>QA 표시 전용 PlayerPrefs 읽기(저장/삭제 없음) — 값이 없으면 fallback 문자열.</summary>
    private static string ReadDebugPref(string key, string fallback)
    {
        string value = PlayerPrefs.GetString(key, "");
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    /// <summary>현재 표시할 타이틀 상태를 NetworkManager 폴링 + 로컬 UI 전용 플래그로 판정한다.</summary>
    private TitleUiState DetermineTitleUiState()
    {
        if (_confirmStartNewOpen) return TitleUiState.ConfirmStartNew;
        if (_prevRoomPopupOpen) return TitleUiState.PrevRoomPrompt;

        // 포기 처리가 끝나(재입장/RPC/재이탈 완료) 저장된 세션도 같이 사라졌으면 일반 로그인으로 복귀.
        if (_abandonRequested && !_network.IsRejoining && !_network.HasSavedSession)
            _abandonRequested = false;

        if (_abandonRequested && _network.IsRejoining) return TitleUiState.AbandoningPreviousSession;
        if (!_abandonRequested && _network.IsRejoining) return TitleUiState.Rejoining;
        if (_network.RejoinFailed) return TitleUiState.RejoinFailed;
        if (_network.HasSavedSession) return TitleUiState.PreviousSessionAvailable;

        return TitleUiState.NormalLogin;
    }

    private void DrawTitleLogin()
    {
        switch (DetermineTitleUiState())
        {
            case TitleUiState.ConfirmStartNew:
                GUILayout.Label("이전 게임으로 돌아가지 않고 새로 시작하시겠습니까?", _infoStyle);
                GUILayout.Label("파트너와 진행 중이던 게임은 종료됩니다.", _infoStyle);

                if (GUILayout.Button("새로 시작하기"))
                {
                    _confirmStartNewOpen = false;
                    _abandonRequested = true;
                    _network.AbandonPreviousSession();
                }

                if (GUILayout.Button("취소"))
                    _confirmStartNewOpen = false;
                return;

            case TitleUiState.AbandoningPreviousSession:
                GUILayout.Label("재접속을 포기하는 중입니다...", _infoStyle);
                return;

            case TitleUiState.PrevRoomPrompt:
                GUILayout.Label("=====================", _titleStyle);
                GUILayout.Label("이전 닉네임 입력", _sectionStyle);

                _prevRoomNicknameInput = GUILayout.TextField(_prevRoomNicknameInput, 16);

                if (!string.IsNullOrEmpty(_prevRoomFailMessage))
                    GUILayout.Label(_prevRoomFailMessage, _infoStyle);

                if (GUILayout.Button("확인"))
                {
                    // 검증 기준: 입력 닉네임 == NetworkManager가 관리하는 저장 닉네임(SavedNickname),
                    // 그리고 저장된 RoomName/UserId가 모두 존재해야 함 — 이 둘의 존재는 HasSavedSession
                    // 하나로 이미 판정된다(_pendingRejoinRoomName은 Start()에서 둘 다 있을 때만 채워짐).
                    bool nicknameMatches =
                        !string.IsNullOrEmpty(_prevRoomNicknameInput) &&
                        _prevRoomNicknameInput == _network.SavedNickname;

                    if (nicknameMatches && _network.HasSavedSession)
                    {
                        _prevRoomPopupOpen = false;
                        _prevRoomFailMessage = null;
                        // 새 Rejoin 로직이 아니라 기존 AttemptRejoinSavedSession()을 그대로 재사용.
                        _network.AttemptRejoinSavedSession();
                    }
                    else
                    {
                        _prevRoomFailMessage = "이전 게임 정보가 없습니다.";
                    }
                }

                if (GUILayout.Button("취소"))
                {
                    _prevRoomPopupOpen = false;
                    _prevRoomFailMessage = null;
                }

                GUILayout.Label("=====================", _titleStyle);
                return;

            case TitleUiState.Rejoining:
                GUILayout.Label("이전 게임에 다시 접속하는 중입니다...", _infoStyle);
                return;

            case TitleUiState.RejoinFailed:
                GUILayout.Label("이전 게임에 접속할 수 없습니다.", _infoStyle);
                if (GUILayout.Button("확인"))
                    _network.AcknowledgeRejoinFailure();
                return;

            case TitleUiState.PreviousSessionAvailable:
                GUILayout.Label("이전 게임이 정상적으로 종료되지 않았습니다.", _infoStyle);
                GUILayout.Label("파트너가 기다리고 있을 수 있습니다.", _infoStyle);

                if (!string.IsNullOrEmpty(_network.SavedNickname))
                    GUILayout.Label($"이전 닉네임: {_network.SavedNickname}", _infoStyle);

                if (GUILayout.Button("이전 게임으로 들어가기"))
                    _network.AttemptRejoinSavedSession();

                if (GUILayout.Button("새로 시작하기"))
                    _confirmStartNewOpen = true;
                return;

            default:
                DrawNormalLogin();
                return;
        }
    }

    private void DrawNormalLogin()
    {
        // 마지막으로 쓴 닉네임을 입력칸 기본값으로 한 번만 채운다(이후엔 사용자가 자유롭게 수정/삭제 가능).
        if (!_nicknameInitialized)
        {
            _nicknameInput = _network.SavedNickname;
            _nicknameInitialized = true;
        }

        GUILayout.Label("닉네임 :", _infoStyle);
        _nicknameInput = GUILayout.TextField(_nicknameInput, 16);

        GUILayout.Space(6);

        if (GUILayout.Button("방 만들기"))
        {
            if (_network.TrySetLocalNickname(_nicknameInput))
                _network.JoinOrCreateRoom("TestRoom");
        }

        if (GUILayout.Button("랜덤 방 입장"))
        {
            if (_network.TrySetLocalNickname(_nicknameInput))
                _network.JoinRandomRoom();
        }

        if (GUILayout.Button("이전 방 입장"))
        {
            _prevRoomPopupOpen = true;
            _prevRoomNicknameInput = "";
            _prevRoomFailMessage = null;
        }

        GUILayout.Space(10);

        // 저장 상태 표시 — 전부 NetworkManager가 이미 관리하는 값을 읽기만 한다(신규 저장/삭제 없음).
        GUILayout.Label($"저장된 게임 : {(_network.HasSavedSession ? "있음" : "없음")}", _infoStyle);
        GUILayout.Label($"저장된 닉네임 : {(string.IsNullOrEmpty(_network.SavedNickname) ? "없음" : _network.SavedNickname)}", _infoStyle);
        GUILayout.Label($"저장된 방 : {ReadDebugPref(DEBUG_PREF_LAST_ROOM_NAME, "없음")}", _infoStyle);

        GUILayout.Space(10);

        // 재접속 확인용 목록이다(신규 입장 가능 목록이 아님) — 로비에 보이는 방은 진행 중(2/2)이어도
        // 표시한다. IsOpen/PlayerCount<MaxPlayers 조건은 걸지 않는다(2026-08). MaxPlayers>0만 남겨
        // malformed 0/0 캐시 데이터를 걸러낸다. 표시 전용 목록이다 — 여기서 방을 골라 직접 입장하는
        // 기능은 없다(기존 플레이어 복귀는 반드시 [이전 게임으로 들어가기](UserId+RoomName 기반
        // RejoinRoom)로만 가능).
        var joinableRooms = new List<RoomInfo>();
        foreach (var room in _roomCache.Values)
        {
            if (room.IsVisible && room.MaxPlayers > 0)
                joinableRooms.Add(room);
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Room List ({joinableRooms.Count})", _sectionStyle);
        if (GUILayout.Button("방 목록 새로고침", GUILayout.Width(120)))
        {
            Debug.Log("[RoomList] Refresh 요청");

            // 새로고침이 눈에 보이게 최신 상태만 반영하도록, 다시 받을 때까지 기존(스테일할 수 있는)
            // 캐시를 비우고 "불러오는 중" 상태로 되돌린다 — 실제 재조회는 NetworkManager.RefreshRoomList가 담당.
            _roomCache.Clear();
            _roomListReceivedAtLeastOnce = false;

            _network.RefreshRoomList();
        }
        GUILayout.EndHorizontal();

        if (!_roomListReceivedAtLeastOnce)
            GUILayout.Label("방 목록을 불러오는 중입니다...", _infoStyle);
        else if (joinableRooms.Count == 0)
            GUILayout.Label("입장 가능한 방이 없습니다.", _infoStyle);

        _roomListScroll = GUILayout.BeginScrollView(
            _roomListScroll,
            GUILayout.Height(150)
        );

        foreach (var room in joinableRooms)
        {
            string hostLabel =
                room.CustomProperties.TryGetValue(NetworkManager.HOST_NICKNAME_PROP_KEY, out object host) &&
                host is string hostName && !string.IsNullOrEmpty(hostName)
                    ? $"{hostName}의 방"
                    : "알 수 없는 방";

            bool inProgress = room.CustomProperties.ContainsKey(NetworkManager.MATCH_GUID_ROOM_KEY);

            // 읽기 전용 표시 — 클릭해도 입장하지 않는다(진행 중인 다른 방에는 기존 플레이어만
            // [이전 게임으로 들어가기]로 복귀 가능, 신규 사용자는 JoinOrCreate/랜덤 매칭만 사용).
            GUILayout.Box($"{hostLabel} ({room.PlayerCount}/{room.MaxPlayers}){(inProgress ? "  진행 중" : "")}");
        }

        GUILayout.EndScrollView();
    }
}
#endif
