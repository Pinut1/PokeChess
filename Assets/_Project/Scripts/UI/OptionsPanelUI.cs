using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

/// <summary>
/// 게임 중 열 수 있는 옵션창(IMGUI).
/// 마스터/배경음/효과음 볼륨, 화면 모드/해상도(적용/취소), 항복 안내, 타이틀 이동, 게임 종료,
/// 파트너 네트워크 이탈 대기 모달 기능을 제공한다.
/// </summary>
public class OptionsPanelUI : MonoBehaviour
{
    private enum ConfirmMode
    {
        None,
        ReturnToTitle,
        QuitGame
    }

    [Header("씬 전환")]
    [Tooltip("타이틀로 버튼 확인 시 로드할 씬 이름입니다. " +
             "비어 있거나 Build Settings에 등록되지 않은 경우 이동하지 않습니다.")]
    [SerializeField] private string _titleSceneName = "";

    private const string PREF_MASTER_VOLUME = "MasterVolume";
    private const string PREF_BGM_VOLUME = "BgmVolume";
    private const string PREF_SFX_VOLUME = "SfxVolume";

    private const string PREF_FULLSCREEN = "FullScreen";
    private const string PREF_RESOLUTION_INDEX = "ResolutionIndex";

    private const float DEFAULT_SUB_VOLUME = 1f;

    private const float MIN_WIDTH = 380f;
    private const float MIN_HEIGHT = 300f;
    private const float RESIZE_HANDLE_SIZE = 16f;

    private bool _optionsOpen;
    private bool _optionsRectInitialized;

    private Rect _optionsRect = new Rect(0f, 0f, 420f, 360f);

    private float _masterVolume;
    private float _bgmVolume;
    private float _sfxVolume;

    private Resolution[] _resolutions;
    private string[] _resolutionLabels;

    // 실제로 적용/저장된 값 (Screen에 반영된 상태)
    private bool _appliedFullScreen;
    private int _appliedResolutionIndex;

    // 옵션창에서 편집 중인 임시값 ([적용] 전까지는 Screen/PlayerPrefs에 반영되지 않음)
    private bool _pendingFullScreen;
    private int _pendingResolutionIndex;

    private bool _resolutionDropdownOpen;
    private Vector2 _resolutionScrollPosition;
    private Rect _resolutionButtonRect;
    private Rect _resolutionListRect;

    private ConfirmMode _confirmMode = ConfirmMode.None;
    private bool _surrenderNoticeShown;
    private bool _surrenderCrossCancelledNoticeShown;
    private bool _surrenderRequestModalOpen; // 요청자용 확인 모달(옵션창과 독립)

    // 파트너 네트워크 이탈 대기(옵션창/항복 모달보다 우선. OnGUI 최상단에서 처리).
    private bool _partnerDisconnectModalOpen;
    private bool _partnerGiveUpAvailable;
    private bool _partnerDisconnectEndConfirmOpen;
    private static GUIStyle _blockerStyle;

    /// <summary>
    /// BlockAllInput()이 비활성화하기 직전에 캐싱해 두는 EventSystem 참조.
    /// EventSystem.enabled=false는 Unity 내부적으로 OnDisable()을 호출해 그 인스턴스를
    /// EventSystem.current가 참조하는 활성 목록에서 제거한다 — 씬에 EventSystem이 1개뿐이라
    /// 그 순간부터 EventSystem.current는 null을 반환한다. 나중에 재활성화할 때 current를
    /// 다시 조회하면 항상 null이라 절대 켜지지 않으므로, 끄기 직전 참조를 여기 보관해 둔다.
    /// </summary>
    private static EventSystem _blockedEventSystem;

    private void Awake()
    {
        _masterVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(PREF_MASTER_VOLUME, AudioListener.volume));

        _bgmVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(PREF_BGM_VOLUME, DEFAULT_SUB_VOLUME));

        _sfxVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(PREF_SFX_VOLUME, DEFAULT_SUB_VOLUME));

        AudioListener.volume = _masterVolume;

        LoadAndApplyDisplaySettings();
    }

    private void LoadAndApplyDisplaySettings()
    {
        bool currentFullScreen = Screen.fullScreen;
        _appliedFullScreen = PlayerPrefs.GetInt(
            PREF_FULLSCREEN,
            currentFullScreen ? 1 : 0) == 1;

        BuildResolutionList();
        int currentResolutionIndex = FindCurrentResolutionIndex();
        _appliedResolutionIndex = Mathf.Clamp(
            PlayerPrefs.GetInt(PREF_RESOLUTION_INDEX, currentResolutionIndex),
            0,
            _resolutionLabels.Length - 1);

        if (_appliedFullScreen != currentFullScreen)
            ApplyScreenMode();

        if (_appliedResolutionIndex != currentResolutionIndex)
            ApplyResolution();

        _pendingFullScreen = _appliedFullScreen;
        _pendingResolutionIndex = _appliedResolutionIndex;
    }

    private void OnEnable()
    {
        GameEvents.OnOpponentDisconnected += HandlePartnerDisconnected;
        GameEvents.OnGracePeriodExpired   += HandlePartnerGiveUpAvailable;
        GameEvents.OnOpponentReconnected  += HandlePartnerReconnected;
    }

    private void OnDisable()
    {
        GameEvents.OnOpponentDisconnected -= HandlePartnerDisconnected;
        GameEvents.OnGracePeriodExpired   -= HandlePartnerGiveUpAvailable;
        GameEvents.OnOpponentReconnected  -= HandlePartnerReconnected;

        // 오브젝트가 비활성화/파괴되는 시점에도 대기 모달 때문에 꺼둔 EventSystem이 있다면 복구한다
        // (씬 전환이면 새 EventSystem이 생성되므로 실질적 위험은 낮지만, 방어적으로 처리).
        RestoreBlockedEventSystem();

        PlayerPrefs.Save();
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }

    private void HandlePartnerDisconnected(float graceSeconds)
    {
        _partnerDisconnectModalOpen = true;
        _partnerGiveUpAvailable = false;
        _partnerDisconnectEndConfirmOpen = false;
    }

    private void HandlePartnerGiveUpAvailable(bool bothDisconnected)
    {
        _partnerGiveUpAvailable = true;
    }

    private void HandlePartnerReconnected()
    {
        _partnerDisconnectModalOpen = false;
        _partnerGiveUpAvailable = false;
        _partnerDisconnectEndConfirmOpen = false;

        // BlockAllInput()이 매 프레임 꺼뒀던 EventSystem을 되돌린다.
        RestoreBlockedEventSystem();
    }

    private void OnGUI()
    {
        // 파트너가 재접속을 포기했다는 통지가 최우선이다 — 대기/확인 모달 상태를 정리하고 통지만 그린다.
        // 전투/타이머 재개 이벤트는 여기서 발행하지 않는다(재개하면 안 되므로).
        if (GameManager.TryGet(out var gm) && gm.Network != null && gm.Network.PartnerGaveUpReconnect)
        {
            _partnerDisconnectModalOpen = false;
            _partnerGiveUpAvailable = false;
            _partnerDisconnectEndConfirmOpen = false;

            DrawPartnerGaveUpNoticeModal();
            return;
        }

        // 파트너 이탈 대기는 설정 버튼/옵션창/항복 모달 전부보다 우선한다 — 그 외에는 아무것도 그리지 않는다.
        // ESC도 여기서 소비하지 않고 그냥 무시되므로(HandleEscapeKey 자체를 호출하지 않음) 닫히지 않는다.
        if (_partnerDisconnectModalOpen)
        {
            DrawPartnerDisconnectWaitModal();
            DrawPartnerDisconnectEndConfirmModal();
            return;
        }

        HandleEscapeKey();
        DrawOpenButton();
        DrawSurrenderRequestModal();
        DrawSurrenderRequestConfirmModal();
        DrawSurrenderRejectedNoticeModal();

        if (!_optionsOpen)
            return;

        if (!_optionsRectInitialized)
        {
            _optionsRect.x = (Screen.width - _optionsRect.width) * 0.5f;
            _optionsRect.y = (Screen.height - _optionsRect.height) * 0.5f;
            _optionsRectInitialized = true;
        }

        ClampRectSize(ref _optionsRect, MIN_WIDTH, MIN_HEIGHT);

        _optionsRect = GUILayout.Window(
            GetInstanceID(),
            _optionsRect,
            DrawOptionsWindow,
            "옵션");

        ClampRectToScreen(ref _optionsRect);
    }

    private void DrawOpenButton()
    {
        const float width = 70f;
        const float height = 28f;

        Rect buttonRect = new Rect(
            8f,
            Screen.height - height - 8f,
            width,
            height);

        if (GUI.Button(buttonRect, "설정"))
            ToggleOptions(!_optionsOpen);
    }

    /// <summary>
    /// 파트너의 항복 요청 전용 모달. 옵션창(_optionsOpen) 여부와 무관하게 항상 그려진다.
    /// </summary>
    private void DrawSurrenderRequestModal()
    {
        if (!GameManager.TryGet(out var gameManager) || gameManager.Network == null)
            return;

        if (!gameManager.Network.HasIncomingSurrenderRequest)
            return;

        const float width = 320f;
        const float height = 110f;

        Rect modalRect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        GUILayout.BeginArea(modalRect, GUI.skin.box);

        GUILayout.Label("파트너가 항복을 요청했습니다. 항복하시겠습니까?");

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("항복하기"))
            gameManager.Network.RespondToSurrender(true);

        if (GUILayout.Button("계속하기"))
            gameManager.Network.RespondToSurrender(false);

        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    /// <summary>
    /// 요청자용 항복 확인 모달. 옵션창과 독립적으로 그려지며, [항복] 클릭 시 옵션창을 닫고 이 모달을 연다.
    /// </summary>
    private void DrawSurrenderRequestConfirmModal()
    {
        if (!_surrenderRequestModalOpen)
            return;

        // 파트너 수신 모달과 동시에 뜨지 않도록 양보(파트너 요청이 더 우선).
        if (GameManager.TryGet(out var gameManager) && gameManager.Network != null &&
            gameManager.Network.HasIncomingSurrenderRequest)
        {
            _surrenderRequestModalOpen = false;
            return;
        }

        const float width = 320f;
        const float height = 110f;

        Rect modalRect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        GUILayout.BeginArea(modalRect, GUI.skin.box);

        GUILayout.Label("파트너에게 항복을 요청하시겠습니까?");

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("요청하기"))
        {
            ConfirmRequestSurrender();
            _surrenderRequestModalOpen = false;
        }

        if (GUILayout.Button("취소"))
        {
            _surrenderRequestModalOpen = false;
            ToggleOptions(true);
        }

        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    /// <summary>
    /// 내가 보낸 항복 요청이 거절됐을 때 표시하는 전용 모달. 옵션창(_optionsOpen) 여부와 무관하게 항상 그려진다.
    /// </summary>
    private void DrawSurrenderRejectedNoticeModal()
    {
        if (!GameManager.TryGet(out var gameManager) || gameManager.Network == null)
            return;

        if (gameManager.Network.SurrenderRequestRejected)
        {
            gameManager.Network.AcknowledgeSurrenderRejected();
            _surrenderNoticeShown = true;
        }

        if (!_surrenderNoticeShown)
            return;

        // 더 시급한 모달(파트너 수신 요청, 내 요청 확인)이 떠 있으면 양보하고 다음 프레임에 다시 시도.
        if (gameManager.Network.HasIncomingSurrenderRequest || _surrenderRequestModalOpen)
            return;

        const float width = 320f;
        const float height = 110f;

        Rect modalRect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        GUILayout.BeginArea(modalRect, GUI.skin.box);

        GUILayout.Label("파트너가 항복 요청을 거절했습니다.");

        if (GUILayout.Button("확인"))
            _surrenderNoticeShown = false;

        GUILayout.EndArea();
    }

    /// <summary>
    /// 파트너 네트워크 이탈 대기 모달. 화면 Dim + 전체 입력 차단, ESC로 닫히지 않는다.
    /// [포기하기]가 나타나기 전까지는 안내 문구만 표시(무한 대기, 타이머 없음).
    /// </summary>
    private void DrawPartnerDisconnectWaitModal()
    {
        // 종료 확인 모달로 넘어갔으면 대기 모달 대신 그쪽만 그린다.
        if (_partnerDisconnectEndConfirmOpen)
            return;

        DrawFullScreenDim();

        const float width = 360f;
        const float height = 150f;

        Rect modalRect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        GUILayout.BeginArea(modalRect, GUI.skin.box);

        GUILayout.Label("팀원이 연결 끊김");
        GUILayout.Label("재접속을 기다리는 중입니다...");

        if (_partnerGiveUpAvailable)
        {
            GUILayout.Space(10f);

            if (GUILayout.Button("포기하기"))
                _partnerDisconnectEndConfirmOpen = true;
        }

        GUILayout.EndArea();

        // 모달 자체 버튼이 클릭을 먼저 처리하도록 반드시 실제 컨트롤을 그린 다음에 차단한다.
        // (전체화면 블로커를 버튼보다 먼저 그리면 AugmentOfferHud가 피하려던 것과 같은 문제로
        // 블로커가 버튼 클릭 자체를 가로채 버린다.)
        BlockAllInput();
    }

    /// <summary>
    /// [포기하기] 이후 종료 확인 모달. 닫기/취소 없이 반드시 둘 중 하나를 선택해야 한다.
    /// 패배 기록(SessionEnded)은 여기서 실제로 선택한 시점에만 발행된다.
    /// </summary>
    private void DrawPartnerDisconnectEndConfirmModal()
    {
        if (!_partnerDisconnectEndConfirmOpen)
            return;

        DrawFullScreenDim();

        const float width = 320f;
        const float height = 130f;

        Rect modalRect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        GUILayout.BeginArea(modalRect, GUI.skin.box);

        GUILayout.Label("게임을 종료하시겠습니까?");

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("타이틀로 이동"))
        {
            if (GameManager.TryGet(out var gameManager) && gameManager.Network != null)
                gameManager.Network.ConfirmPartnerDisconnectGiveUp();

            // 이 모달도 BlockAllInput()으로 EventSystem을 꺼둔 채였다 — 씬 전환 전 방어적으로 되돌린다.
            RestoreBlockedEventSystem();

            // 기존 "타이틀로" 흐름을 그대로 재사용(LeaveRoom + 씬 전환) — 이 메서드 자체는 수정하지 않는다.
            ConfirmReturnToTitle();
        }

        if (GUILayout.Button("게임 종료"))
        {
            if (GameManager.TryGet(out var gameManager) && gameManager.Network != null)
                gameManager.Network.ConfirmPartnerDisconnectGiveUp();

            RestoreBlockedEventSystem();

            // 기존 "게임 종료" 흐름을 그대로 재사용 — 이 메서드 자체는 수정하지 않는다.
            QuitGame();
        }

        GUILayout.EndHorizontal();

        GUILayout.EndArea();

        // 모달 자체 버튼이 클릭을 먼저 처리하도록 반드시 실제 컨트롤을 그린 다음에 차단한다.
        BlockAllInput();
    }

    /// <summary>
    /// 파트너가 재접속을 포기했다는 통지 전용 모달. ESC/바깥 클릭으로 닫히지 않으며([확인]만 유효),
    /// 이 모달이 떠 있는 동안은 OnGUI가 다른 무엇도 그리지 않으므로 옵션창/항복 모달보다 우선한다.
    /// </summary>
    private void DrawPartnerGaveUpNoticeModal()
    {
        DrawFullScreenDim();

        const float width = 340f;
        const float height = 130f;

        Rect modalRect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        GUILayout.BeginArea(modalRect, GUI.skin.box);

        GUILayout.Label("상대방이 재접속을 포기했습니다.");

        if (GUILayout.Button("확인"))
        {
            if (GameManager.TryGet(out var gameManager) && gameManager.Network != null)
            {
                gameManager.Network.AcknowledgePartnerGaveUpReconnect();

                // 이 모달도 BlockAllInput()으로 EventSystem을 꺼둔 채였다 — 씬 전환(ConfirmReturnToTitle)이
                // 끝나면 새 EventSystem이 생기긴 하지만, LeaveRoom 완료를 기다리는 동안 현재 씬에 남아있는
                // 시간이 있으므로 방어적으로 여기서도 되돌린다.
                RestoreBlockedEventSystem();

                // 기존 패배 처리 흐름 재사용: SessionEnded 발행(전적 저장) → 기존 "타이틀로" 경로
                // (RequestReturnToTitle → LeaveRoom 완료 → OnLeftRoom에서 씬 전환)를 그대로 탄다.
                gameManager.Network.ConfirmPartnerDisconnectGiveUp();
                ConfirmReturnToTitle();
            }
        }

        GUILayout.EndArea();

        BlockAllInput();
    }

    /// <summary>화면 전체를 반투명 검은색으로 덮는다(Dim). 새 텍스처 없이 Texture2D.whiteTexture만 사용.</summary>
    private static void DrawFullScreenDim()
    {
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    /// <summary>
    /// 전체 입력 차단. AugmentOfferHud의 DrawClickBlocker/AbsorbClicksOutside와 같은 방식(투명 GUI.Button +
    /// Event.Use())으로 IMGUI 입력(마우스/키보드)을 흡수하고, uGUI(Canvas 버튼)는 IMGUI 블로커로 막히지
    /// 않으므로 EventSystem 자체를 잠시 꺼서 막는다(HandlePartnerReconnected에서 다시 켠다).
    /// </summary>
    private static void BlockAllInput()
    {
        _blockerStyle ??= new GUIStyle();

        GUI.Button(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none, _blockerStyle);

        Event currentEvent = Event.current;
        if (currentEvent != null && (currentEvent.isMouse || currentEvent.isKey))
            currentEvent.Use();

        var eventSystem = EventSystem.current;
        if (eventSystem != null && eventSystem.enabled)
        {
            // 끄기 직전 참조를 캐싱해 둔다 — 끄고 나면 EventSystem.current가 null이 되어
            // 나중에 다시 조회하는 방식으로는 되돌릴 수 없다(클래스 상단 필드 설명 참고).
            _blockedEventSystem = eventSystem;
            eventSystem.enabled = false;
        }
    }

    /// <summary>
    /// BlockAllInput()이 꺼둔 EventSystem을 되돌린다. EventSystem.current를 다시 조회하지 않고
    /// 끄기 직전에 캐싱해 둔 참조를 그대로 사용한다(current는 비활성화된 순간 null이 되므로).
    /// </summary>
    private static void RestoreBlockedEventSystem()
    {
        if (_blockedEventSystem == null)
            return;

        _blockedEventSystem.enabled = true;
        Debug.Log("[OptionsPanelUI] 파트너 이탈 대기 종료 — 캐싱된 EventSystem 재활성화");
        _blockedEventSystem = null;
    }

    private void HandleEscapeKey()
    {
        Event currentEvent = Event.current;

        if (currentEvent.type != EventType.KeyDown ||
            currentEvent.keyCode != KeyCode.Escape)
        {
            return;
        }

        if (_surrenderRequestModalOpen)
        {
            _surrenderRequestModalOpen = false;
            ToggleOptions(true);
        }
        else if (_surrenderNoticeShown)
        {
            _surrenderNoticeShown = false;
        }
        else if (_confirmMode != ConfirmMode.None)
        {
            _confirmMode = ConfirmMode.None;
        }
        else
        {
            ToggleOptions(!_optionsOpen);
        }

        currentEvent.Use();
    }

    private void ToggleOptions(bool open)
    {
        bool wasOpen = _optionsOpen;
        _optionsOpen = open;

        if (_optionsOpen && !wasOpen)
        {
            // 창을 열 때: 현재 실제 적용 중인 값으로 편집 상태 초기화
            _pendingFullScreen = _appliedFullScreen;
            _pendingResolutionIndex = _appliedResolutionIndex;
            _resolutionDropdownOpen = false;
        }

        if (!_optionsOpen)
        {
            // 닫기/ESC/취소 공통: 미적용 임시값 폐기 후 실제 적용값으로 복구
            _pendingFullScreen = _appliedFullScreen;
            _pendingResolutionIndex = _appliedResolutionIndex;
            _resolutionDropdownOpen = false;

            _confirmMode = ConfirmMode.None;
            _surrenderCrossCancelledNoticeShown = false;
            PlayerPrefs.Save();
        }
    }

    private static void ClampRectSize(
        ref Rect rect,
        float minimumWidth,
        float minimumHeight)
    {
        float maximumWidth = Mathf.Max(
            minimumWidth,
            Screen.width - 10f);

        float maximumHeight = Mathf.Max(
            minimumHeight,
            Screen.height - 10f);

        rect.width = Mathf.Clamp(
            rect.width,
            minimumWidth,
            maximumWidth);

        rect.height = Mathf.Clamp(
            rect.height,
            minimumHeight,
            maximumHeight);
    }

    private static void ClampRectToScreen(ref Rect rect)
    {
        const float visibleMargin = 60f;

        rect.x = Mathf.Clamp(
            rect.x,
            -(rect.width - visibleMargin),
            Screen.width - visibleMargin);

        rect.y = Mathf.Clamp(
            rect.y,
            0f,
            Screen.height - visibleMargin);
    }

    private static void DrawResizeHandle(
        ref Rect rect,
        float minimumWidth,
        float minimumHeight)
    {
        Rect handleRect = new Rect(
            rect.width - RESIZE_HANDLE_SIZE,
            rect.height - RESIZE_HANDLE_SIZE,
            RESIZE_HANDLE_SIZE,
            RESIZE_HANDLE_SIZE);

        GUI.Box(handleRect, "◢");

        Event currentEvent = Event.current;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);

        switch (currentEvent.GetTypeForControl(controlId))
        {
            case EventType.MouseDown:
                if (handleRect.Contains(currentEvent.mousePosition))
                {
                    GUIUtility.hotControl = controlId;
                    currentEvent.Use();
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == controlId)
                {
                    float maximumWidth = Mathf.Max(
                        minimumWidth,
                        Screen.width - 10f);

                    float maximumHeight = Mathf.Max(
                        minimumHeight,
                        Screen.height - 10f);

                    rect.width = Mathf.Clamp(
                        rect.width + currentEvent.delta.x,
                        minimumWidth,
                        maximumWidth);

                    rect.height = Mathf.Clamp(
                        rect.height + currentEvent.delta.y,
                        minimumHeight,
                        maximumHeight);

                    currentEvent.Use();
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == controlId)
                {
                    GUIUtility.hotControl = 0;
                    currentEvent.Use();
                }
                break;
        }
    }

    private void DrawOptionsWindow(int windowId)
    {
        GUILayout.BeginVertical();

        DrawHeader();

        GUILayout.Space(6f);
        DrawVolumeSection();

        GUILayout.Space(12f);
        DrawScreenSection();

        GUILayout.Space(12f);

        if (_confirmMode == ConfirmMode.None)
        {
            DrawNormalButtons();
        }
        else
        {
            DrawConfirmSection();
        }

        GUILayout.EndVertical();

        DrawResizeHandle(
            ref _optionsRect,
            MIN_WIDTH,
            MIN_HEIGHT);

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
    }

    private void DrawHeader()
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label("옵션", GUILayout.Width(200f));
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("닫기", GUILayout.Width(60f)))
            ToggleOptions(false);

        GUILayout.EndHorizontal();
    }

    private void DrawVolumeSection()
    {
        GUILayout.Label("── 볼륨 ──");

        DrawVolumeRow(
            "마스터 볼륨",
            ref _masterVolume,
            PREF_MASTER_VOLUME,
            ApplyMasterVolume);

        DrawVolumeRow(
            "배경음",
            ref _bgmVolume,
            PREF_BGM_VOLUME,
            ApplyBgmVolume);

        DrawVolumeRow(
            "효과음",
            ref _sfxVolume,
            PREF_SFX_VOLUME,
            ApplySfxVolume);
    }

    /// <summary>SoundManager가 씬에 없을 때도(아직 배치 전 등) 기존 마스터 볼륨 동작이 그대로 유지되도록 폴백한다.</summary>
    private static void ApplyMasterVolume(float value)
    {
        if (SoundManager.TryGet(out var soundManager))
            soundManager.SetMasterVolume(value);
        else
            AudioListener.volume = value;
    }

    private static void ApplyBgmVolume(float value)
    {
        if (SoundManager.TryGet(out var soundManager))
            soundManager.SetBgmVolume(value);
    }

    private static void ApplySfxVolume(float value)
    {
        if (SoundManager.TryGet(out var soundManager))
            soundManager.SetSfxVolume(value);
    }

    private static void DrawVolumeRow(
        string label,
        ref float value,
        string preferenceKey,
        System.Action<float> applyImmediately)
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label(label, GUILayout.Width(100f));

        float newValue = GUILayout.HorizontalSlider(
            value,
            0f,
            1f,
            GUILayout.Width(160f));

        GUILayout.Label(
            $"{Mathf.RoundToInt(newValue * 100f)}%",
            GUILayout.Width(50f));

        GUILayout.EndHorizontal();

        if (Mathf.Approximately(newValue, value))
            return;

        value = newValue;

        PlayerPrefs.SetFloat(preferenceKey, value);
        applyImmediately?.Invoke(value);
    }

    private void DrawScreenSection()
    {
        GUILayout.Label("── 화면 ──");

        DrawFullScreenModeRow();
        DrawResolutionDropdown();

        GUILayout.Space(6f);
        DrawScreenApplyCancelRow();
    }

    private void DrawFullScreenModeRow()
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label("화면 모드", GUILayout.Width(100f));

        bool wasPendingFullScreen = _pendingFullScreen;

        bool windowedChecked = GUILayout.Toggle(
            !wasPendingFullScreen, "창모드", GUILayout.Width(100f));

        bool fullScreenChecked = GUILayout.Toggle(
            wasPendingFullScreen, "전체화면", GUILayout.Width(100f));

        GUILayout.EndHorizontal();

        // 라디오 버튼처럼 상호 배타 처리: 실제로 클릭된 쪽의 값만 반영한다.
        // (같은 프레임에 두 Toggle을 모두 그리지만, 실제 클릭 이벤트는 마우스 아래
        // 컨트롤 하나만 소비하므로 클릭되지 않은 쪽은 항상 이전 값 그대로 반환된다.)
        if (windowedChecked && wasPendingFullScreen)
            _pendingFullScreen = false;
        else if (fullScreenChecked && !wasPendingFullScreen)
            _pendingFullScreen = true;
    }

    private void DrawResolutionDropdown()
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label("해상도", GUILayout.Width(100f));

        if (GUILayout.Button(
                _resolutionLabels[_pendingResolutionIndex],
                GUILayout.Width(220f)))
        {
            _resolutionDropdownOpen = !_resolutionDropdownOpen;
        }

        if (Event.current.type == EventType.Repaint)
            _resolutionButtonRect = GUILayoutUtility.GetLastRect();

        GUILayout.EndHorizontal();

        if (_resolutionDropdownOpen)
            DrawResolutionDropdownList();

        CloseResolutionDropdownOnOutsideClick();
    }

    private void DrawResolutionDropdownList()
    {
        GUILayout.BeginVertical(GUI.skin.box);

        float listHeight = Mathf.Min(140f, _resolutionLabels.Length * 24f);

        _resolutionScrollPosition = GUILayout.BeginScrollView(
            _resolutionScrollPosition,
            GUILayout.Height(listHeight));

        for (int i = 0; i < _resolutionLabels.Length; i++)
        {
            if (GUILayout.Button(_resolutionLabels[i]))
            {
                _pendingResolutionIndex = i;
                _resolutionDropdownOpen = false;
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        if (Event.current.type == EventType.Repaint)
            _resolutionListRect = GUILayoutUtility.GetLastRect();
    }

    private void CloseResolutionDropdownOnOutsideClick()
    {
        if (!_resolutionDropdownOpen)
            return;

        if (Event.current.type != EventType.MouseDown)
            return;

        bool insideButton = _resolutionButtonRect.Contains(Event.current.mousePosition);
        bool insideList = _resolutionListRect.Contains(Event.current.mousePosition);

        if (!insideButton && !insideList)
            _resolutionDropdownOpen = false;
    }

    private void DrawScreenApplyCancelRow()
    {
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("적용", GUILayout.Width(80f)))
            ApplyScreenSettings();

        if (GUILayout.Button("취소", GUILayout.Width(80f)))
            ToggleOptions(false);

        GUILayout.EndHorizontal();
    }

    private void ApplyScreenSettings()
    {
        _appliedFullScreen = _pendingFullScreen;
        _appliedResolutionIndex = _pendingResolutionIndex;

        ApplyScreenMode();
        ApplyResolution();

        PlayerPrefs.SetInt(PREF_FULLSCREEN, _appliedFullScreen ? 1 : 0);
        PlayerPrefs.SetInt(PREF_RESOLUTION_INDEX, _appliedResolutionIndex);
        PlayerPrefs.Save();
    }

    private void DrawNormalButtons()
    {
        DrawSurrenderSection();

        GUILayout.Space(10f);

        if (GUILayout.Button("타이틀로"))
        {
            _surrenderNoticeShown = false;
            _surrenderCrossCancelledNoticeShown = false;
            _confirmMode = ConfirmMode.ReturnToTitle;
        }

        GUILayout.Space(10f);

        if (GUILayout.Button("게임 종료"))
        {
            _surrenderNoticeShown = false;
            _surrenderCrossCancelledNoticeShown = false;
            _confirmMode = ConfirmMode.QuitGame;
        }
    }

    private void DrawSurrenderSection()
    {
        if (!GameManager.TryGet(out var gameManager) || gameManager.Network == null)
            return;

        if (gameManager.Network.SurrenderRequestCrossCancelled)
        {
            gameManager.Network.AcknowledgeSurrenderCrossCancelled();
            _surrenderCrossCancelledNoticeShown = true;
        }

        // 파트너가 없는 솔로/1인 방에서는 항복이 성립할 수 없으므로 버튼을 숨긴다.
        if (!gameManager.Network.IsInRoom || gameManager.Network.PlayerCount < 2)
            return;

        if (GUILayout.Button("항복"))
        {
            _surrenderNoticeShown = false;
            _surrenderCrossCancelledNoticeShown = false;
            _surrenderRequestModalOpen = true;
            ToggleOptions(false);
        }

        if (_surrenderCrossCancelledNoticeShown)
        {
            GUILayout.Label(
                "항복 요청이 동시에 발생해 취소되었습니다. 다시 요청해주세요.");
        }
    }

    private void DrawConfirmSection()
    {
        string message = _confirmMode switch
        {
            ConfirmMode.ReturnToTitle =>
                "타이틀 화면으로 이동하시겠습니까?",

            ConfirmMode.QuitGame =>
                "게임을 종료하시겠습니까?",

            _ => ""
        };

        GUILayout.Label(message);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("확인", GUILayout.Width(80f)))
        {
            ConfirmMode selectedMode = _confirmMode;
            _confirmMode = ConfirmMode.None;

            if (selectedMode == ConfirmMode.ReturnToTitle)
                ConfirmReturnToTitle();
            else if (selectedMode == ConfirmMode.QuitGame)
                QuitGame();
        }

        if (GUILayout.Button("취소", GUILayout.Width(80f)))
            _confirmMode = ConfirmMode.None;

        GUILayout.EndHorizontal();
    }

    private void ConfirmReturnToTitle()
    {
        if (string.IsNullOrWhiteSpace(_titleSceneName))
        {
            Debug.LogWarning(
                "[OptionsPanelUI] 타이틀 씬 이름이 설정되지 않았습니다.");

            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(_titleSceneName))
        {
            Debug.LogWarning(
                $"[OptionsPanelUI] 빌드에 등록되지 않은 씬입니다: {_titleSceneName}");

            return;
        }

        PlayerPrefs.Save();
        Time.timeScale = 1f;

        // NetworkManager가 LeaveRoom 완료(Photon 확인) 후에만 씬을 전환한다 — 여기서 직접
        // LeaveRoom+LoadScene을 연달아 하면 "Leaving" 상태에서 걸린 SetProperties 호출이
        // 오류를 낼 수 있다(2026-08). 정상 타이틀 이동과 파트너 이탈 포기 후 타이틀 이동 모두
        // 이 메서드 하나를 공유하므로 양쪽 다 안전한 경로를 탄다.
        if (GameManager.TryGet(out var gameManager) && gameManager.Network != null)
            gameManager.Network.RequestReturnToTitle(_titleSceneName);
        else
            SceneManager.LoadScene(_titleSceneName);
    }

    private void BuildResolutionList()
    {
        Resolution[] rawResolutions = Screen.resolutions;

        var widths = new System.Collections.Generic.List<int>();
        var heights = new System.Collections.Generic.List<int>();
        var refreshRates = new System.Collections.Generic.List<RefreshRate>();

        for (int i = 0; i < rawResolutions.Length; i++)
        {
            Resolution candidate = rawResolutions[i];

            int existingIndex = -1;
            for (int j = 0; j < widths.Count; j++)
            {
                if (widths[j] == candidate.width && heights[j] == candidate.height)
                {
                    existingIndex = j;
                    break;
                }
            }

            if (existingIndex < 0)
            {
                widths.Add(candidate.width);
                heights.Add(candidate.height);
                refreshRates.Add(candidate.refreshRateRatio);
            }
            else if (candidate.refreshRateRatio.value > refreshRates[existingIndex].value)
            {
                refreshRates[existingIndex] = candidate.refreshRateRatio;
            }
        }

        if (widths.Count == 0)
        {
            widths.Add(Screen.currentResolution.width);
            heights.Add(Screen.currentResolution.height);
            refreshRates.Add(Screen.currentResolution.refreshRateRatio);
        }

        _resolutions = new Resolution[widths.Count];
        _resolutionLabels = new string[widths.Count];

        for (int i = 0; i < widths.Count; i++)
        {
            _resolutions[i] = new Resolution
            {
                width = widths[i],
                height = heights[i],
                refreshRateRatio = refreshRates[i]
            };

            _resolutionLabels[i] =
                $"{widths[i]}x{heights[i]} @{refreshRates[i].value:0}Hz";
        }
    }

    private int FindCurrentResolutionIndex()
    {
        for (int i = 0; i < _resolutions.Length; i++)
        {
            if (_resolutions[i].width == Screen.currentResolution.width &&
                _resolutions[i].height == Screen.currentResolution.height)
            {
                return i;
            }
        }

        return 0;
    }

    private void ApplyScreenMode()
    {
        Screen.fullScreenMode = _appliedFullScreen
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;
    }

    private void ApplyResolution()
    {
        Resolution resolution = _resolutions[_appliedResolutionIndex];

        Screen.SetResolution(
            resolution.width,
            resolution.height,
            _appliedFullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed,
            resolution.refreshRateRatio);
    }

    private static void ConfirmRequestSurrender()
    {
        if (GameManager.TryGet(out var gameManager) && gameManager.Network != null)
            gameManager.Network.RequestSurrender();
    }

    private static void QuitGame()
    {
        PlayerPrefs.Save();
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}