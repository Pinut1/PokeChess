using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면. 두 단계로 나뉜다.
/// <list type="number">
/// <item><b>PressAnyKey</b> — 로고 + "Press any Button". 아무 입력이나 받으면 다음으로 넘어간다.</item>
/// <item><b>Login</b> — 닉네임 입력과 입장 버튼들.</item>
/// </list>
///
/// 이전 세션 관련 상태(재접속 대기·실패·확인)는 화면을 따로 만들지 않고 공용
/// <see cref="ModalDialogUI"/>를 띄운다 — GameScene에서 쓰는 것과 같은 프리팹이다.
/// 상태 판정은 <see cref="NetworkConnectionTest"/>의 TitleUiState 로직을 그대로 옮겼다.
///
/// <b>네트워크 로직은 하나도 갖지 않는다.</b> 전부 NetworkManager를 호출할 뿐이고, 이 클래스는
/// "지금 무엇을 보여줄지"만 정한다. 기존 IMGUI 화면도 같은 원칙이었다.
/// </summary>
public class TitleScreenUI : MonoBehaviour
{
    /// <summary>모달로 덮어야 할 화면 상태. NetworkManager 폴링 + 로컬 UI 플래그로 판정한다.</summary>
    private enum TitleUiState
    {
        NormalLogin,
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
    [Tooltip("깜빡일 \"Press any Button\" 텍스트. 비워두면 깜빡이지 않는다.")]
    [SerializeField] private Graphic _pressAnyKeyText;
    [Tooltip("한 번 깜빡이는 데 걸리는 시간(초).")]
    [SerializeField, Min(0.1f)] private float _blinkPeriod = 1.4f;
    [Tooltip("깜빡임의 알파 범위. x=가장 흐릴 때, y=가장 진할 때.")]
    [SerializeField] private Vector2 _blinkAlphaRange = new Vector2(0.25f, 1f);

    [Header("단계 2 — 로그인")]
    [SerializeField] private GameObject _loginPanel;
    [Tooltip("닉네임 입력칸. 마지막으로 쓴 닉네임이 기본값으로 한 번 채워진다.")]
    [SerializeField] private TMP_InputField _nicknameInput;
    [SerializeField] private Button _createRoomButton;
    [SerializeField] private Button _joinRandomButton;
    [Tooltip("이전 게임으로 복귀. 저장된 세션이 없으면 자동으로 비활성화된다.")]
    [SerializeField] private Button _rejoinButton;
    [SerializeField] private Button _quitButton;
    [Tooltip("닉네임이 비었을 때 등 입력 오류 안내. 비어 있으면 꺼진다.")]
    [SerializeField] private TMP_Text _messageText;

    [Header("공용 모달")]
    [Tooltip("이전 세션 관련 안내/확인 팝업(ModalDialog_Pf). Canvas 직계에 둘 것.")]
    [SerializeField] private ModalDialogUI _modalDialog;

    [Header("방 이름")]
    [Tooltip("[방 만들기]로 만들 방 이름. 기존 IMGUI와 같은 값이다.")]
    [SerializeField] private string _createRoomName = "TestRoom";

    private NetworkManager _network;

    private bool _loginPhase;

    // 사용자가 [새로 시작하기]를 눌러 확인 팝업을 띄운 상태(로컬 UI 전용 — 취소하면 저장값은 그대로).
    private bool _confirmStartNewOpen;

    // AbandonPreviousSession()을 호출했는지. IsRejoining 중 문구를 "재접속 중"과 "포기하는 중"으로
    // 가르는 데만 쓴다.
    private bool _abandonRequested;

    // 공용 모달이 지금 어느 상태를 그리고 있는지. 같은 것을 매 프레임 다시 띄우지 않으려고 기억한다.
    private TitleUiState _shownModalState = TitleUiState.NormalLogin;

    private void Awake()
    {
        _network = FindFirstObjectByType<NetworkManager>();

        if (_network == null)
        {
            Debug.LogError("[TitleScreenUI] 씬에서 NetworkManager를 찾지 못했습니다 — 입장 버튼이 동작하지 않습니다.", this);
        }

        if (_createRoomButton != null) _createRoomButton.onClick.AddListener(HandleCreateRoomClicked);
        if (_joinRandomButton != null) _joinRandomButton.onClick.AddListener(HandleJoinRandomClicked);
        if (_rejoinButton != null) _rejoinButton.onClick.AddListener(HandleRejoinClicked);
        if (_quitButton != null) _quitButton.onClick.AddListener(HandleQuitClicked);

        SetMessage(null);
        EnterPressAnyKeyPhase();
    }

    private void Start()
    {
        // 로고를 보여주는 동안 뒤에서 미리 접속해 둔다 — 로그인 화면에 도달했을 때 기다리지 않도록.
        if (_network != null) _network.Connect();
    }

    private void Update()
    {
        if (!_loginPhase)
        {
            BlinkPressAnyKeyText();

            if (AnyInputThisFrame())
                EnterLoginPhase();

            return;
        }

        RefreshLoginInteractable();
        PollPreviousSessionState();
    }

    // ─────────────────────────────────────────
    // 단계 1 — Press any Button
    // ─────────────────────────────────────────

    private void EnterPressAnyKeyPhase()
    {
        _loginPhase = false;

        if (_pressAnyKeyPanel != null) _pressAnyKeyPanel.SetActive(true);
        if (_loginPanel != null) _loginPanel.SetActive(false);
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
    // 단계 2 — 로그인
    // ─────────────────────────────────────────

    private void EnterLoginPhase()
    {
        _loginPhase = true;

        if (_pressAnyKeyPanel != null) _pressAnyKeyPanel.SetActive(false);
        if (_loginPanel != null) _loginPanel.SetActive(true);

        // 마지막으로 쓴 닉네임을 기본값으로 채운다. 이후로는 사용자가 자유롭게 고친다.
        if (_nicknameInput != null && _network != null && string.IsNullOrEmpty(_nicknameInput.text))
            _nicknameInput.text = _network.SavedNickname;

        SetMessage(null);
    }

    /// <summary>
    /// 접속 전이거나 재접속 처리 중이면 입장 버튼을 잠근다.
    /// <para>
    /// <b>이미 방에 들어가 있을 때도 잠근다.</b> 방을 만든 뒤 파트너를 기다리는 동안에도 타이틀 화면은
    /// 그대로 떠 있는데(씬 전환은 두 번째 플레이어가 들어와야 일어난다), 이때 버튼이 살아 있으면
    /// 방에 있는 채로 JoinOrCreateRoom을 다시 불러 Photon이 오류를 뱉는다.
    /// </para>
    /// </summary>
    private void RefreshLoginInteractable()
    {
        if (_network == null) return;

        bool ready = _network.IsConnected && !_network.IsRejoining && !_network.IsInRoom;

        if (_createRoomButton != null) _createRoomButton.interactable = ready;
        if (_joinRandomButton != null) _joinRandomButton.interactable = ready;
        if (_rejoinButton != null) _rejoinButton.interactable = ready && _network.HasSavedSession;
    }

    private void HandleCreateRoomClicked()
    {
        if (!TryApplyNickname()) return;

        _network.JoinOrCreateRoom(_createRoomName);
    }

    private void HandleJoinRandomClicked()
    {
        if (!TryApplyNickname()) return;

        _network.JoinRandomRoom();
    }

    /// <summary>이전 게임으로 복귀. 저장된 닉네임/방을 그대로 쓰므로 입력칸은 보지 않는다.</summary>
    private void HandleRejoinClicked()
    {
        if (_network == null) return;

        SetMessage(null);
        _network.AttemptRejoinSavedSession();
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

    /// <summary>
    /// 닉네임을 NetworkManager에 넘긴다. 거부되면(빈 문자열 등) 안내만 띄우고 입장은 하지 않는다.
    /// 유효성 판정은 TrySetLocalNickname 하나에 맡긴다 — 여기서 따로 검사하면 규칙이 두 곳으로 갈린다.
    /// </summary>
    private bool TryApplyNickname()
    {
        if (_network == null) return false;

        string nickname = _nicknameInput != null ? _nicknameInput.text : "";

        if (_network.TrySetLocalNickname(nickname))
        {
            SetMessage(null);
            return true;
        }

        SetMessage("닉네임을 입력해 주세요.");
        return false;
    }

    private void SetMessage(string message)
    {
        if (_messageText == null) return;

        bool has = !string.IsNullOrWhiteSpace(message);

        _messageText.gameObject.SetActive(has);
        if (has) _messageText.text = message;
    }

    // ─────────────────────────────────────────
    // 이전 세션 상태 → 공용 모달
    // ─────────────────────────────────────────

    /// <summary>현재 보여줘야 할 상태. 기존 IMGUI의 DetermineTitleUiState와 같은 판정 순서다.</summary>
    private TitleUiState DetermineState()
    {
        if (_confirmStartNewOpen) return TitleUiState.ConfirmStartNew;

        // 포기 처리가 끝나 저장된 세션까지 사라졌으면 일반 로그인으로 되돌린다.
        if (_abandonRequested && !_network.IsRejoining && !_network.HasSavedSession)
            _abandonRequested = false;

        if (_abandonRequested && _network.IsRejoining) return TitleUiState.AbandoningPreviousSession;
        if (!_abandonRequested && _network.IsRejoining) return TitleUiState.Rejoining;
        if (_network.RejoinFailed) return TitleUiState.RejoinFailed;
        if (_network.HasSavedSession) return TitleUiState.PreviousSessionAvailable;

        // 방에 들어갔지만 아직 씬은 그대로 = 파트너 대기 중. HasSavedSession보다 뒤에 두어도 되는 이유는
        // NetworkManager가 OnJoinedRoom에서 저장된 세션을 지우기 때문이다(둘이 동시에 참이 되지 않는다).
        if (_network.IsInRoom) return TitleUiState.WaitingForPartner;

        return TitleUiState.NormalLogin;
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
