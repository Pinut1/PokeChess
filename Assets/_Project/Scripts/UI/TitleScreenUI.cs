using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면. 세 단계로 나뉜다.
/// <list type="number">
/// <item><b>PressAnyKey</b> — 로고 + "Press any Button". 아무 입력이나 받으면 다음으로 넘어간다.</item>
/// <item><b>Login</b> — 닉네임(아이디) 입력과 [접속].</item>
/// <item><b>Lobby</b> — 방 목록 + 방 만들기 / 랜덤 입장 / 선택한 방 입장 / 이전 게임으로 들어가기.</item>
/// </list>
///
/// 이전 세션 관련 상태(재접속 대기·실패·확인)는 화면을 따로 만들지 않고 공용
/// <see cref="ModalDialogUI"/>를 띄운다 — GameScene에서 쓰는 것과 같은 프리팹이다.
///
/// 방 목록의 표시·선택은 <see cref="RoomListUI"/>가 맡는다. 여기서는 그 결과(고른 방 이름)를
/// 읽어 NetworkManager에 넘길 뿐이다.
///
/// <b>네트워크 로직은 하나도 갖지 않는다.</b> 전부 NetworkManager를 호출할 뿐이고, 이 클래스는
/// "지금 무엇을 보여줄지"만 정한다. 이전 IMGUI 화면(NetworkConnectionTest)도 같은 원칙이었고,
/// 그 화면의 기능은 전부 이 클래스와 RoomListUI로 옮겨왔다.
/// </summary>
public class TitleScreenUI : MonoBehaviour
{
    /// <summary>지금 어느 화면을 보여주고 있는지.</summary>
    private enum TitlePhase
    {
        PressAnyKey,
        Login,
        Lobby
    }

    /// <summary>모달로 덮어야 할 화면 상태. NetworkManager 폴링 + 로컬 UI 플래그로 판정한다.</summary>
    private enum TitleUiState
    {
        Normal,
        PreviousSessionAvailable,
        Rejoining,
        RejoinFailed,
        ConfirmStartNew,
        AbandoningPreviousSession,
        WaitingForPartner
    }

    [Header("단계 1 — Press any Button")]
    [Tooltip("로고와 안내 문구가 담긴 루트. 입력을 받으면 꺼진다.")]
    [SerializeField] private GameObject _pressAnyKeyPanel;
    [Tooltip("깜빡일 \"Press any Button\" 텍스트. 비워두면 깜빡이지 않는다 — " +
             "TextGlowPulse 같은 별도 연출이 붙어 있으면 비워두는 것이 맞다(두 연출이 겹치지 않도록).")]
    [SerializeField] private Graphic _pressAnyKeyText;
    [Tooltip("한 번 깜빡이는 데 걸리는 시간(초).")]
    [SerializeField, Min(0.1f)] private float _blinkPeriod = 1.4f;
    [Tooltip("깜빡임의 알파 범위. x=가장 흐릴 때, y=가장 진할 때.")]
    [SerializeField] private Vector2 _blinkAlphaRange = new Vector2(0.25f, 1f);

    [Header("단계 2 — 로그인")]
    [Tooltip("아이디 입력줄 루트(LoginRow).")]
    [SerializeField] private GameObject _loginPanel;
    [Tooltip("아이디(닉네임) 입력칸. 마지막으로 쓴 닉네임이 기본값으로 한 번 채워진다.")]
    [SerializeField] private TMP_InputField _nicknameInput;
    [Tooltip("[접속]. 닉네임을 확정하고 로비로 넘어간다 — 이 버튼만으로는 방에 들어가지 않는다.")]
    [SerializeField] private Button _loginButton;
    [Tooltip("닉네임이 비었을 때 등 입력 오류 안내. 비어 있으면 꺼진다.")]
    [SerializeField] private TMP_Text _loginMessageText;

    [Header("단계 3 — 로비")]
    [SerializeField] private GameObject _lobbyPanel;
    [Tooltip("방 목록. 선택 결과(SelectedRoomName)를 읽어 [선택한 방 입장]에 쓴다.")]
    [SerializeField] private RoomListUI _roomList;
    [Tooltip("목록에서 고른 방으로 입장. 고른 방이 없으면 자동으로 비활성화된다.")]
    [SerializeField] private Button _joinSelectedButton;
    [SerializeField] private Button _createRoomButton;
    [SerializeField] private Button _joinRandomButton;
    [Tooltip("이전 게임으로 복귀. 저장된 세션이 없으면 자동으로 비활성화된다.")]
    [SerializeField] private Button _rejoinButton;
    [Tooltip("로그인 단계로 되돌아간다(아이디 다시 입력).")]
    [SerializeField] private Button _backButton;
    [SerializeField] private Button _quitButton;
    [Tooltip("입장 실패 등 로비 안내 문구. 비어 있으면 꺼진다.")]
    [SerializeField] private TMP_Text _lobbyMessageText;

    [Header("공용 모달")]
    [Tooltip("이전 세션 관련 안내/확인 팝업(ModalDialog_Pf). Canvas 직계에 둘 것.")]
    [SerializeField] private ModalDialogUI _modalDialog;

    [Header("방 이름")]
    [Tooltip("[방 만들기]로 만들 방 이름의 앞부분. 뒤에 네 자리 숫자가 붙어 TestRoom_1234가 된다 — " +
             "고정 이름을 쓰면 모두가 같은 방 하나에만 들어가서 방 목록이 의미가 없어진다.")]
    [SerializeField] private string _createRoomNamePrefix = "TestRoom";

    [Tooltip("QA에서 방 이름을 못박아야 할 때만 채운다. 채우면 숫자를 붙이지 않고 이 이름을 그대로 쓴다.")]
    [SerializeField] private string _createRoomNameOverride = "";

    /// <summary>중복되지 않는 방 이름을 뽑아볼 횟수. 다 실패하면 시각 기반 이름으로 넘어간다.</summary>
    private const int ROOM_NAME_ATTEMPTS = 20;

    private NetworkManager _network;

    private TitlePhase _phase = TitlePhase.PressAnyKey;

    // 사용자가 [새로 시작하기]를 눌러 확인 팝업을 띄운 상태(로컬 UI 전용 — 취소하면 저장값은 그대로).
    private bool _confirmStartNewOpen;

    // AbandonPreviousSession()을 호출했는지. IsRejoining 중 문구를 "재접속 중"과 "포기하는 중"으로
    // 가르는 데만 쓴다.
    private bool _abandonRequested;

    // 공용 모달이 지금 어느 상태를 그리고 있는지. 같은 것을 매 프레임 다시 띄우지 않으려고 기억한다.
    private TitleUiState _shownModalState = TitleUiState.Normal;

    private void Awake()
    {
        _network = FindFirstObjectByType<NetworkManager>();

        if (_network == null)
        {
            Debug.LogError("[TitleScreenUI] 씬에서 NetworkManager를 찾지 못했습니다 — 입장 버튼이 동작하지 않습니다.", this);
        }

        if (_loginButton != null) _loginButton.onClick.AddListener(HandleLoginClicked);

        if (_joinSelectedButton != null) _joinSelectedButton.onClick.AddListener(HandleJoinSelectedClicked);
        if (_createRoomButton != null) _createRoomButton.onClick.AddListener(HandleCreateRoomClicked);
        if (_joinRandomButton != null) _joinRandomButton.onClick.AddListener(HandleJoinRandomClicked);
        if (_rejoinButton != null) _rejoinButton.onClick.AddListener(HandleRejoinClicked);
        if (_backButton != null) _backButton.onClick.AddListener(HandleBackClicked);
        if (_quitButton != null) _quitButton.onClick.AddListener(HandleQuitClicked);

        if (_roomList != null) _roomList.RoomJoinFailed += SetLobbyMessage;

        SetLoginMessage(null);
        SetLobbyMessage(null);
        EnterPressAnyKeyPhase();
    }

    private void OnDestroy()
    {
        if (_roomList != null) _roomList.RoomJoinFailed -= SetLobbyMessage;
    }

    private void Start()
    {
        // 로고를 보여주는 동안 뒤에서 미리 접속해 둔다 — 로비에 도달했을 때 기다리지 않도록.
        if (_network != null) _network.Connect();
    }

    private void Update()
    {
        if (_phase == TitlePhase.PressAnyKey)
        {
            BlinkPressAnyKeyText();

            if (AnyInputThisFrame())
                EnterLoginPhase();

            return;
        }

        RefreshInteractable();
        PollPreviousSessionState();
    }

    // ─────────────────────────────────────────
    // 단계 1 — Press any Button
    // ─────────────────────────────────────────

    private void EnterPressAnyKeyPhase()
    {
        SetPhase(TitlePhase.PressAnyKey);

        if (SoundManager.TryGet(out var sm))
            sm.PlayBgmWithIntro(SoundId.TitleBgmIntro, SoundId.TitleBgm);
    }

    /// <summary>
    /// 키보드·마우스·게임패드 아무거나. Input.anyKeyDown이 마우스 버튼까지 포함하므로 이 하나로 족하다.
    /// <para>
    /// ⚠️ 마우스 <b>이동</b>은 입력으로 치지 않는다 — 커서가 스치기만 해도 넘어가면
    /// 로고를 볼 새가 없다.
    /// </para>
    /// </summary>
    private static bool AnyInputThisFrame()
    {
        return Input.anyKeyDown;
    }

    private void BlinkPressAnyKeyText()
    {
        if (_pressAnyKeyText == null) return;

        float t = (Mathf.Sin(Time.unscaledTime / _blinkPeriod * Mathf.PI * 2f) + 1f) * 0.5f;

        Color color = _pressAnyKeyText.color;
        color.a = Mathf.Lerp(_blinkAlphaRange.x, _blinkAlphaRange.y, t);
        _pressAnyKeyText.color = color;
    }

    // ─────────────────────────────────────────
    // 단계 전환
    // ─────────────────────────────────────────

    /// <summary>세 패널 중 하나만 켠다. 화면 전환은 전부 이 한 곳을 지나므로 상태가 어긋날 여지가 없다.</summary>
    private void SetPhase(TitlePhase phase)
    {
        _phase = phase;

        if (_pressAnyKeyPanel != null) _pressAnyKeyPanel.SetActive(phase == TitlePhase.PressAnyKey);
        if (_loginPanel != null) _loginPanel.SetActive(phase == TitlePhase.Login);
        if (_lobbyPanel != null) _lobbyPanel.SetActive(phase == TitlePhase.Lobby);
    }

    private void EnterLoginPhase()
    {
        SetPhase(TitlePhase.Login);

        // 마지막으로 쓴 닉네임을 기본값으로 채운다. 이후로는 사용자가 자유롭게 고친다.
        if (_nicknameInput != null && _network != null && string.IsNullOrEmpty(_nicknameInput.text))
            _nicknameInput.text = _network.SavedNickname;

        SetLoginMessage(null);
        SetLobbyMessage(null);

        if (_roomList != null) _roomList.ClearSelection();
    }

    private void EnterLobbyPhase()
    {
        SetPhase(TitlePhase.Lobby);

        SetLobbyMessage(null);

        // 로그인 단계에 머무는 동안 방이 생기고 사라졌을 수 있다 — 들어오면서 한 번 새로 받는다.
        if (_roomList != null) _roomList.Refresh();
    }

    // ─────────────────────────────────────────
    // 단계 2 — 로그인
    // ─────────────────────────────────────────

    /// <summary>[접속] — 닉네임만 확정하고 로비로 넘어간다. 방 입장은 로비에서 고른다.</summary>
    private void HandleLoginClicked()
    {
        if (!TryApplyNickname()) return;

        EnterLobbyPhase();
    }

    /// <summary>
    /// 닉네임을 NetworkManager에 넘긴다. 거부되면(빈 문자열 등) 안내만 띄우고 넘어가지 않는다.
    /// 유효성 판정은 TrySetLocalNickname 하나에 맡긴다 — 여기서 따로 검사하면 규칙이 두 곳으로 갈린다.
    /// </summary>
    private bool TryApplyNickname()
    {
        if (_network == null) return false;

        string nickname = _nicknameInput != null ? _nicknameInput.text : "";

        if (_network.TrySetLocalNickname(nickname))
        {
            SetLoginMessage(null);
            return true;
        }

        SetLoginMessage("아이디를 입력해 주세요.");
        return false;
    }

    // ─────────────────────────────────────────
    // 단계 3 — 로비
    // ─────────────────────────────────────────

    /// <summary>
    /// 접속 전이거나 재접속 처리 중이면 입장 버튼을 잠근다.
    /// <para>
    /// <b>이미 방에 들어가 있을 때도 잠근다.</b> 방을 만든 뒤 파트너를 기다리는 동안에도 타이틀 화면은
    /// 그대로 떠 있는데(씬 전환은 두 번째 플레이어가 들어와야 일어난다), 이때 버튼이 살아 있으면
    /// 방에 있는 채로 JoinOrCreateRoom을 다시 불러 Photon이 오류를 뱉는다.
    /// </para>
    /// </summary>
    private void RefreshInteractable()
    {
        if (_network == null) return;

        bool ready = _network.IsConnected && !_network.IsRejoining && !_network.IsInRoom;

        if (_loginButton != null) _loginButton.interactable = ready;

        if (_createRoomButton != null) _createRoomButton.interactable = ready;
        if (_joinRandomButton != null) _joinRandomButton.interactable = ready;
        if (_rejoinButton != null) _rejoinButton.interactable = ready && _network.HasSavedSession;
        if (_joinSelectedButton != null)
            _joinSelectedButton.interactable = ready && _roomList != null && _roomList.HasSelection;
    }

    /// <summary>목록에서 고른 방으로 입장. 실패는 RoomListUI가 Photon 콜백으로 받아 안내를 올린다.</summary>
    private void HandleJoinSelectedClicked()
    {
        if (_network == null || _roomList == null || !_roomList.HasSelection) return;

        SetLobbyMessage(null);
        _network.JoinRoom(_roomList.SelectedRoomName);
    }

    private void HandleCreateRoomClicked()
    {
        if (_network == null) return;

        SetLobbyMessage(null);
        _network.JoinOrCreateRoom(ResolveCreateRoomName());
    }

    /// <summary>
    /// 만들 방의 이름 — "TestRoom_1234"처럼 앞부분 + 네 자리 난수.
    ///
    /// 방마다 이름이 달라야 방 목록에 여러 방이 나란히 보인다. 뽑은 이름이 이미 로비에 있으면
    /// 다시 뽑는다 — <see cref="NetworkManager.JoinOrCreateRoom"/>은 같은 이름이 있으면 그 방에
    /// <b>입장</b>해 버리므로, 중복을 그냥 두면 "방 만들기"가 남의 방에 들어가는 동작이 된다.
    ///
    /// 로비 목록에 없는 방(막 만들어져 아직 안 퍼진 방)과 부딪힐 가능성은 남지만, 그 경우에도
    /// 빈자리가 있으면 합류하고 없으면 입장 실패로 안내가 뜬다 — 조용히 깨지지는 않는다.
    /// </summary>
    private string ResolveCreateRoomName()
    {
        if (!string.IsNullOrWhiteSpace(_createRoomNameOverride))
            return _createRoomNameOverride.Trim();

        string prefix = string.IsNullOrWhiteSpace(_createRoomNamePrefix)
            ? "TestRoom"
            : _createRoomNamePrefix.Trim();

        for (int attempt = 0; attempt < ROOM_NAME_ATTEMPTS; attempt++)
        {
            string candidate = $"{prefix}_{Random.Range(1000, 10000)}";

            if (_roomList == null || !_roomList.ContainsRoom(candidate))
                return candidate;
        }

        // 9000개 후보를 20번 뽑아 전부 부딪힐 일은 없지만, 무한 루프를 만들지 않으려고 빠져나갈 길을 둔다.
        return $"{prefix}_{System.DateTime.Now:HHmmss}";
    }

    private void HandleJoinRandomClicked()
    {
        if (_network == null) return;

        SetLobbyMessage(null);
        _network.JoinRandomRoom();
    }

    /// <summary>이전 게임으로 복귀. 저장된 닉네임/방을 그대로 쓰므로 입력칸은 보지 않는다.</summary>
    private void HandleRejoinClicked()
    {
        if (_network == null) return;

        SetLobbyMessage(null);
        _network.AttemptRejoinSavedSession();
    }

    private void HandleBackClicked()
    {
        EnterLoginPhase();
    }

    private void HandleQuitClicked()
    {
        PlayerPrefs.Save();
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ─────────────────────────────────────────
    // 안내 문구
    // ─────────────────────────────────────────

    private void SetLoginMessage(string message) => SetMessage(_loginMessageText, message);

    private void SetLobbyMessage(string message) => SetMessage(_lobbyMessageText, message);

    private static void SetMessage(TMP_Text target, string message)
    {
        if (target == null) return;

        bool has = !string.IsNullOrWhiteSpace(message);

        target.gameObject.SetActive(has);
        if (has) target.text = message;
    }

    // ─────────────────────────────────────────
    // 이전 세션 상태 → 공용 모달
    // ─────────────────────────────────────────

    /// <summary>현재 보여줘야 할 상태. 기존 IMGUI의 DetermineTitleUiState와 같은 판정 순서다.</summary>
    private TitleUiState DetermineState()
    {
        if (_confirmStartNewOpen) return TitleUiState.ConfirmStartNew;

        // 포기 처리가 끝나 저장된 세션까지 사라졌으면 일반 상태로 되돌린다.
        if (_abandonRequested && !_network.IsRejoining && !_network.HasSavedSession)
            _abandonRequested = false;

        if (_abandonRequested && _network.IsRejoining) return TitleUiState.AbandoningPreviousSession;
        if (!_abandonRequested && _network.IsRejoining) return TitleUiState.Rejoining;
        if (_network.RejoinFailed) return TitleUiState.RejoinFailed;
        if (_network.HasSavedSession) return TitleUiState.PreviousSessionAvailable;

        // 방에 들어갔지만 아직 씬은 그대로 = 파트너 대기 중. HasSavedSession보다 뒤에 두어도 되는 이유는
        // NetworkManager가 OnJoinedRoom에서 저장된 세션을 지우기 때문이다(둘이 동시에 참이 되지 않는다).
        if (_network.IsInRoom) return TitleUiState.WaitingForPartner;

        return TitleUiState.Normal;
    }

    private void PollPreviousSessionState()
    {
        if (_network == null || _modalDialog == null) return;

        TitleUiState state = DetermineState();

        if (state == _shownModalState) return;
        _shownModalState = state;

        switch (state)
        {
            case TitleUiState.PreviousSessionAvailable:
                _modalDialog.ShowChoice(
                    "이전 게임이 정상적으로 종료되지 않았습니다.\n파트너가 기다리고 있을 수 있습니다.",
                    "이전 게임으로 들어가기", HandleRejoinClicked,
                    "새로 시작하기", () => _confirmStartNewOpen = true);
                break;

            case TitleUiState.ConfirmStartNew:
                _modalDialog.ShowChoice(
                    "이전 게임으로 돌아가지 않고 새로 시작하시겠습니까?\n파트너와 진행 중이던 게임은 종료됩니다.",
                    "새로 시작하기", HandleConfirmStartNew,
                    "취소", () => _confirmStartNewOpen = false);
                break;

            case TitleUiState.Rejoining:
                _modalDialog.ShowWaiting("이전 게임에 다시 접속하는 중입니다...");
                break;

            case TitleUiState.AbandoningPreviousSession:
                _modalDialog.ShowWaiting("재접속을 포기하는 중입니다...");
                break;

            // 버튼 없는 대기 화면으로 두면 파트너가 영영 안 올 때 빠져나갈 길이 없다 — 나가기를 준다.
            // 아직 혼자 있는 방이라 나가도 남는 사람이 없다.
            case TitleUiState.WaitingForPartner:
                _modalDialog.ShowNotice(
                    "파트너를 기다리는 중입니다...", "나가기",
                    () => _network.LeaveRoom());
                break;

            case TitleUiState.RejoinFailed:
                _modalDialog.ShowNotice(
                    "이전 게임에 접속할 수 없습니다.", "확인",
                    () => _network.AcknowledgeRejoinFailure());
                break;

            default:
                _modalDialog.Close();
                break;
        }
    }

    private void HandleConfirmStartNew()
    {
        _confirmStartNewOpen = false;
        _abandonRequested = true;

        _network.AbandonPreviousSession();
    }
}
