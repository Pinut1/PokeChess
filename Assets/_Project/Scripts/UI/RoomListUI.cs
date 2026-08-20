using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
using Photon.Realtime;
#endif

/// <summary>
/// 로비 방 목록. 서버에서 받은 방을 한 줄씩 그리고, 사용자가 고른 방 이름을 밖으로 노출한다.
///
/// <b>선택 방식은 옵션창 목차 탭과 같다.</b> 줄 하나가 <see cref="Toggle"/>이고, 전부 Content의
/// ToggleGroup에 묶여 있어 한 번에 하나만 켜진다(상호 배타를 코드로 관리하지 않는다).
/// 실제 입장은 이 클래스가 하지 않는다 — <see cref="TitleScreenUI"/>가 <see cref="SelectedRoomName"/>을
/// 읽어 NetworkManager에 넘긴다.
///
/// 입장 실패(그 사이 방이 차거나 사라짐)는 Photon 콜백을 직접 받을 수 있는 이 클래스가 잡아
/// <see cref="RoomJoinFailed"/>로 올려 보낸다 — TitleScreenUI를 Photon에 의존시키지 않기 위해서다.
///
/// Photon 의존부는 <c>PHOTON_UNITY_NETWORKING</c>으로 감싸고, 그리기는 Photon 타입을 모르는
/// <see cref="RoomRow"/>만 본다(QAManager와 같은 방식). 심볼이 없는 빌드에서는 목록이 늘 비어 있을 뿐
/// 컴파일은 된다.
/// </summary>
public class RoomListUI :
#if PHOTON_UNITY_NETWORKING
    MonoBehaviourPunCallbacks
#else
    MonoBehaviour
#endif
{
    /// <summary>화면에 그릴 한 줄분 정보. Photon 타입을 여기서 끊어낸다.</summary>
    private struct RoomRow
    {
        public string name;
        public string host;
        public string count;
        public string state;
        public bool joinable;

        /// <summary>지금 내가 들어가 있는 방인지. 맨 위로 올리고 "참여 중"으로 표시한다.</summary>
        public bool mine;
    }

    [Header("목록")]
    [Tooltip("줄이 쌓이는 곳. VerticalLayoutGroup + ContentSizeFitter + ToggleGroup을 붙여둘 것.")]
    [SerializeField] private RectTransform _content;

    [Tooltip("복제해서 쓸 줄 원본. 씬에는 비활성 상태로 두고, 이 오브젝트 자체는 목록에 나오지 않는다.")]
    [SerializeField] private RoomEntryUI _entryTemplate;

    [Tooltip("줄들을 묶는 ToggleGroup. 보통 Content에 붙인다. 비우면 Content에서 찾는다.")]
    [SerializeField] private ToggleGroup _toggleGroup;

    [Header("표시")]
    [Tooltip("지금 보이는 방 개수. \"(3)\" 처럼 숫자만 띄운다.")]
    [SerializeField] private TMP_Text _countText;

    [Tooltip("\"불러오는 중\"·\"입장 가능한 방이 없습니다\" 안내. 보여줄 게 없으면 꺼진다.")]
    [SerializeField] private TMP_Text _statusText;

    [Tooltip("방 목록 새로고침 버튼.")]
    [SerializeField] private Button _refreshButton;

    /// <summary>지금 선택된 방 이름. 아무것도 안 골랐으면 빈 문자열.</summary>
    public string SelectedRoomName { get; private set; } = "";

    /// <summary>고른 방이 있는지. [선택 방 입장] 버튼의 활성 조건이다.</summary>
    public bool HasSelection => !string.IsNullOrEmpty(SelectedRoomName);

    /// <summary>방 입장에 실패했을 때(사유 문구). 목록에서 고른 방이 그 사이 차버린 경우 등.</summary>
    public event Action<string> RoomJoinFailed;

    private NetworkManager _network;

    // 지금 화면에 떠 있는 줄들. 다시 그릴 때 통째로 지우고 새로 만든다(방 몇 개 수준이라 부담 없음).
    private readonly List<RoomEntryUI> _entries = new();

    // 이번에 그릴 줄 목록. Photon 캐시에서 매번 새로 만든다.
    private readonly List<RoomRow> _rows = new();

    // 서버 목록을 한 번이라도 받았는지 — 로비 입장 직후의 "아직 목록 없음"과
    // "실제로 방이 없음"을 구분해 표시하는 데 쓴다.
    private bool _receivedAtLeastOnce;

    private void Awake()
    {
        _network = FindFirstObjectByType<NetworkManager>();

        if (_toggleGroup == null && _content != null)
            _toggleGroup = _content.GetComponent<ToggleGroup>();

        // 원본은 화면에 나오면 안 된다. 씬에 켜둔 채 저장했어도 여기서 바로잡는다.
        if (_entryTemplate != null) _entryTemplate.gameObject.SetActive(false);

        if (_refreshButton != null) _refreshButton.onClick.AddListener(Refresh);

        Rebuild();
    }

    // ─────────────────────────────────────────
    // 외부 조작
    // ─────────────────────────────────────────

    /// <summary>
    /// 목록을 비우고 서버에 다시 요청한다. 새로고침이 눈에 보이게 스테일할 수 있는 기존 캐시를
    /// 먼저 버리고 "불러오는 중"으로 되돌린다 — 실제 재조회는 NetworkManager.RefreshRoomList가 맡는다.
    /// </summary>
    public void Refresh()
    {
        ClearRoomCache();
        _receivedAtLeastOnce = false;
        Rebuild();

        if (_network != null) _network.RefreshRoomList();
    }

    /// <summary>고른 방을 해제한다. 로비를 떠날 때(로그인으로 뒤로 가기 등) 부른다.</summary>
    public void ClearSelection()
    {
        SelectedRoomName = "";

        foreach (RoomEntryUI entry in _entries)
            if (entry != null && entry.Toggle != null) entry.Toggle.SetIsOnWithoutNotify(false);
    }

    // ─────────────────────────────────────────
    // Photon 연동
    // ─────────────────────────────────────────

#if PHOTON_UNITY_NETWORKING

    // RoomName 기준 캐시. Photon의 OnRoomListUpdate는 델타 업데이트라 매번 통으로 덮어쓰면 안 된다
    // (RemovedFromList 항목이 유효한 방인 것처럼 남거나, 이번에 언급 안 된 기존 방이 사라짐 — 2026-08 확인).
    private readonly Dictionary<string, RoomInfo> _roomCache = new();

    private void ClearRoomCache() => _roomCache.Clear();

    /// <summary>Photon 룸 리스트는 델타 업데이트다 — RemovedFromList면 캐시에서 제거, 아니면 갱신/추가한다.</summary>
    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        _receivedAtLeastOnce = true;

        foreach (RoomInfo room in roomList)
        {
            if (room.RemovedFromList)
                _roomCache.Remove(room.Name);
            else
                _roomCache[room.Name] = room;
        }

        Rebuild();
    }

    /// <summary>로비를 새로 들어올 때마다 이전 로비의 캐시가 남지 않도록 비운다.</summary>
    public override void OnJoinedLobby()
    {
        _roomCache.Clear();
        _receivedAtLeastOnce = false;
        Rebuild();
    }

    /// <summary>
    /// 방을 만들거나 들어간 직후. 여기서 다시 그려야 방금 만든 내 방이 곧바로 목록에 뜬다
    /// (이 시점부터 로비 목록 갱신은 끊기므로 <see cref="BuildRows"/>가 CurrentRoom을 직접 읽는다).
    /// </summary>
    public override void OnJoinedRoom() => Rebuild();

    /// <summary>파트너가 들어오거나 나가면 인원 표시(1/2 ↔ 2/2)를 따라 갱신한다.</summary>
    public override void OnPlayerEnteredRoom(Player newPlayer) => Rebuild();

    public override void OnPlayerLeftRoom(Player otherPlayer) => Rebuild();

    /// <summary>
    /// 방에서 나오면 내 방 줄을 지우고 서버 목록을 다시 받는다. 방에 있는 동안 로비 갱신이
    /// 끊겨 있었으므로 남아 있는 캐시는 그 사이 낡았다고 봐야 한다.
    /// </summary>
    public override void OnLeftRoom() => Refresh();

    /// <summary>
    /// 목록에서 고른 방으로의 입장이 실패한 경우. 재접속(RejoinRoom) 실패는 NetworkManager가
    /// RejoinFailed 플래그로 따로 알리므로, 여기서는 그 흐름이 아닐 때만 안내를 올린다.
    /// </summary>
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        if (_network != null && _network.IsRejoining) return;

        // 고른 방이 그 사이 차거나 사라진 것이 대부분이다 — 목록을 새로 받아 화면과 실제를 맞춘다.
        Refresh();

        RoomJoinFailed?.Invoke("방에 입장하지 못했습니다. 목록을 새로 불러왔습니다.");
    }

    /// <summary>
    /// 캐시를 화면용 줄 목록으로 옮긴다. 진행 중(2/2)인 방도 목록에는 남긴다 — 재접속 확인용으로
    /// 보여야 하고, 입장 차단은 줄 단위 Toggle 잠금으로 처리한다. MaxPlayers&gt;0 조건은
    /// malformed 0/0 캐시를 거른다.
    /// <para>
    /// <b>지금 내가 들어가 있는 방은 캐시가 아니라 CurrentRoom에서 직접 만든다.</b> 방에 들어가는
    /// 순간 Photon 로비에서 나오게 되어 OnRoomListUpdate가 더 이상 오지 않는다 — 캐시만 믿으면
    /// 방금 만든 내 방이 목록에 아예 안 뜨거나, 뜨더라도 인원이 옛날 값에서 멈춘다.
    /// </para>
    /// </summary>
    private void BuildRows()
    {
        _rows.Clear();

        Room currentRoom = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom : null;

        foreach (RoomInfo room in _roomCache.Values)
        {
            if (!room.IsVisible || room.MaxPlayers <= 0) continue;

            // 내 방이 캐시에도 있으면(남의 방에 들어간 경우) 캐시 쪽은 버린다 — 아래에서 최신 상태로 다시 넣는다.
            if (currentRoom != null && room.Name == currentRoom.Name) continue;

            _rows.Add(MakeRow(room, room.PlayerCount, false));
        }

        // 내 방은 IsVisible을 따지지 않는다 — 방이 잠겨 목록에서 빠진 뒤에도 내가 있는 곳은 보여야 한다.
        // playerCount는 currentRoom(Room 타입)에서 직접 읽어 넘긴다 — MakeRow 안에서 RoomInfo
        // 파라미터로 room.PlayerCount를 읽으면 Room.PlayerCount(실시간 인원, `new`로 가려쓰기됨)가
        // 아니라 RoomInfo.PlayerCount(로비 목록 갱신 때만 채워지는 옛날 값)가 나온다 — 방을 만든
        // 뒤로는 로비 갱신을 더 이상 안 받으니 이 값이 처음 값에 영원히 고정된다(2026-08 실측:
        // 파트너가 들어와도 내 방 줄이 계속 1/2로 보이는 원인이었음).
        if (currentRoom != null) _rows.Add(MakeRow(currentRoom, currentRoom.PlayerCount, true));

        // 서버가 주는 순서는 보장되지 않는다 — 새로고침할 때마다 줄이 뒤바뀌지 않게 고정한다.
        // 내 방만 맨 위로 올리고 나머지는 이름순.
        _rows.Sort(CompareRows);
    }

    /// <summary>
    /// playerCount를 room에서 다시 읽지 않고 파라미터로 받는다 — RoomInfo.PlayerCount와
    /// Room.PlayerCount는 이름은 같아도 서로 다른 멤버(new로 가려쓰기)라, 호출부가 상황에 맞는
    /// 값(캐시된 RoomInfo면 그 값, 내가 들어가 있는 Room이면 실시간 값)을 미리 골라 넘겨야 한다.
    /// </summary>
    private static RoomRow MakeRow(RoomInfo room, int playerCount, bool mine)
    {
        bool inProgress = room.CustomProperties.ContainsKey(NetworkManager.MATCH_GUID_ROOM_KEY);
        bool full = playerCount >= room.MaxPlayers;

        // 표시 전용 보정 — 내 방 행에서만, 로컬 Photon 상태가 아직 나 자신을 반영하기 전이라
        // playerCount가 일시적으로 0으로 읽히는 경우 화면에 0/2로 보이는 것을 막는다.
        int displayCount = mine && playerCount < 1 ? 1 : playerCount;

        return new RoomRow
        {
            name = room.Name,
            host = HostLabelOf(room),
            count = $"{displayCount}/{room.MaxPlayers}",
            state = mine ? "참여 중" : inProgress ? "진행 중" : full ? "가득 참" : "",

            // 이미 들어가 있는 방은 고를 이유가 없다 — 선택해도 [선택 방 입장]이 같은 방을 다시 부를 뿐이다.
            joinable = !mine && room.IsOpen && !full && !inProgress,
            mine = mine
        };
    }

    private static int CompareRows(RoomRow a, RoomRow b)
    {
        if (a.mine != b.mine) return a.mine ? -1 : 1;
        return string.CompareOrdinal(a.name, b.name);
    }

    /// <summary>
    /// 방 생성 시 Room 속성에 실어둔 방장 닉네임. 없으면(구버전 방 등) 표시할 이름이 없다.
    /// <para>
    /// ⚠️ Photon 로비 목록으로는 <b>방 안 사람들의 목록을 볼 수 없다</b> — RoomInfo가 주는 건
    /// 인원 수와 Room 커스텀 속성뿐이다. 그래서 닉네임은 NetworkManager가 방을 만들 때 속성으로
    /// 실어둔 방장 것 하나만 나온다. 들어갈 수 있는 방은 어차피 1/2(방장 혼자)라 실사용에는
    /// 충분하지만, 2/2 방의 두 번째 사람까지 보이게 하려면 NetworkManager가 입장 시 닉네임을
    /// Room 속성에 추가로 실어야 한다(Core/Network 영역 — 담당자 확인 필요).
    /// </para>
    /// </summary>
    private static string HostLabelOf(RoomInfo room)
    {
        return room.CustomProperties.TryGetValue(NetworkManager.HOST_NICKNAME_PROP_KEY, out object host) &&
               host is string hostName && !string.IsNullOrEmpty(hostName)
            ? hostName
            : "알 수 없음";
    }

    /// <summary>이 이름의 방이 지금 로비에 있는지. 새 방 이름을 뽑을 때 중복을 피하는 데 쓴다.</summary>
    public bool ContainsRoom(string roomName) => _roomCache.ContainsKey(roomName);

#else

    private void ClearRoomCache() { }

    private void BuildRows() => _rows.Clear();

    public bool ContainsRoom(string roomName) => false;

#endif

    // ─────────────────────────────────────────
    // 그리기
    // ─────────────────────────────────────────

    private void Rebuild()
    {
        if (_content == null || _entryTemplate == null) return;

        BuildRows();
        ClearEntries();

        foreach (RoomRow row in _rows)
            _entries.Add(CreateEntry(row));

        // 다시 그리는 사이 방이 사라졌거나 가득 찼으면 선택도 같이 풀린다.
        if (HasSelection && !TrySelectExisting(SelectedRoomName))
            SelectedRoomName = "";

        RefreshLabels(_rows.Count);
    }

    private RoomEntryUI CreateEntry(RoomRow row)
    {
        RoomEntryUI entry = Instantiate(_entryTemplate, _content);
        entry.gameObject.SetActive(true);

        entry.Bind(row.name, row.host, row.count, row.state, row.joinable);

        if (entry.Toggle != null)
        {
            entry.Toggle.group = _toggleGroup;
            entry.Toggle.SetIsOnWithoutNotify(row.joinable && row.name == SelectedRoomName);

            // 지역 복사본으로 캡처한다 — 루프 변수를 그대로 쓰면 모든 리스너가 마지막 줄을 가리킨다.
            string roomName = row.name;
            entry.Toggle.onValueChanged.AddListener(isOn => HandleEntryToggled(roomName, isOn));
        }

        return entry;
    }

    /// <summary>
    /// ToggleGroup이 나머지를 꺼주므로 켜지는 쪽만 반영하면 된다. 꺼지는 이벤트는 방금 고른 줄이
    /// 아닐 때만 무시한다 — 같은 줄을 다시 눌러 끄면(allowSwitchOff) 선택 해제로 친다.
    /// </summary>
    private void HandleEntryToggled(string roomName, bool isOn)
    {
        if (isOn)
            SelectedRoomName = roomName;
        else if (SelectedRoomName == roomName)
            SelectedRoomName = "";
    }

    private bool TrySelectExisting(string roomName)
    {
        foreach (RoomEntryUI entry in _entries)
        {
            if (entry.RoomName != roomName) continue;
            if (!entry.Joinable) return false;

            if (entry.Toggle != null) entry.Toggle.SetIsOnWithoutNotify(true);
            return true;
        }

        return false;
    }

    private void ClearEntries()
    {
        foreach (RoomEntryUI entry in _entries)
            if (entry != null) Destroy(entry.gameObject);

        _entries.Clear();
    }

    private void RefreshLabels(int roomCount)
    {
        if (_countText != null) _countText.text = $"({roomCount})";

        if (_statusText == null) return;

        // 개수를 먼저 본다 — 서버 목록을 받기 전이라도 내가 만든 방 한 줄은 이미 떠 있을 수 있는데,
        // 그 위에 "불러오는 중"을 덮으면 방금 만든 방이 가려진다.
        string status =
            roomCount > 0 ? null :
            !_receivedAtLeastOnce ? "방 목록을 불러오는 중입니다..." :
            "입장 가능한 방이 없습니다.";

        bool hasStatus = status != null;

        _statusText.gameObject.SetActive(hasStatus);
        if (hasStatus) _statusText.text = status;
    }
}
