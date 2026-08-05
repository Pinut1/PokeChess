using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 게임 중 열 수 있는 옵션창.
/// 마스터/배경음/효과음 볼륨, 화면 모드/해상도(적용/취소), 항복 안내, 타이틀 이동, 게임 종료 기능을 제공한다.
///
/// 옵션 본문/확인 모달(ConfirmModal)은 uGUI 오브젝트 참조로 제어한다(IMGUI로 그리지 않음).
/// 옵션 패널은 _optionsPanelRoot.SetActive(true/false)로 여닫는다.
/// 파트너 네트워크 이탈 대기·항복 요청/거절 통지는 옵션창과 무관하게 언제든 떠야 하는 별도 기능이라
/// 이번 전환 범위 밖이며 기존 IMGUI(OnGUI)를 그대로 유지한다.
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

    [Header("옵션 패널 (uGUI)")]
    [Tooltip("옵션 패널 루트. SetActive(true/false)로 여닫는다.")]
    [SerializeField] private GameObject _optionsPanelRoot;
    [Tooltip("설정 버튼. 클릭 시 uGUI 옵션 패널을 연다.")]
    [SerializeField] private Button _openOptionsButton;

    [Header("옵션 패널 — 볼륨")]
    [SerializeField] private Slider _masterVolumeSlider;
    [SerializeField] private Slider _bgmVolumeSlider;
    [SerializeField] private Slider _sfxVolumeSlider;
    [SerializeField] private TMP_Text _masterVolumeValueText;
    [SerializeField] private TMP_Text _bgmVolumeValueText;
    [SerializeField] private TMP_Text _sfxVolumeValueText;

    [Header("옵션 패널 — 화면")]
    [SerializeField] private Toggle _windowedToggle;
    [SerializeField] private Toggle _fullscreenToggle;
    [SerializeField] private TMP_Dropdown _resolutionDropdown;

    [Header("옵션 패널 — 버튼")]
    [SerializeField] private Button _applyButton;
    [SerializeField] private Button _cancelOptionsButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _returnTitleButton;
    [SerializeField] private Button _quitButton;

    [Header("확인 모달 (uGUI)")]
    [Tooltip("타이틀로/게임 종료 확인 팝업의 루트(ConfirmModal). 씬에는 비활성화 상태로 배치해 둘 것 — " +
             "Awake에서도 다시 꺼서 보정한다.")]
    [SerializeField] private GameObject _confirmModal;
    [Tooltip("팝업 안내 문구(MessageText).")]
    [SerializeField] private TMP_Text _confirmMessageText;
    [Tooltip("확인 버튼 — 클릭 시 열려 있던 확인 종류(타이틀로/게임 종료)에 해당하는 기존 로직만 실행.")]
    [SerializeField] private Button _confirmButton;
    [Tooltip("취소 버튼 — 아무 로직도 실행하지 않고 모달만 닫는다.")]
    [SerializeField] private Button _cancelButton;

    private const string PREF_MASTER_VOLUME = "MasterVolume";
    private const string PREF_BGM_VOLUME = "BgmVolume";
    private const string PREF_SFX_VOLUME = "SfxVolume";

    private const string PREF_FULLSCREEN = "FullScreen";
    private const string PREF_RESOLUTION_INDEX = "ResolutionIndex";

    private const float DEFAULT_SUB_VOLUME = 1f;

    private bool _optionsOpen;

    private float _masterVolume;
    private float _bgmVolume;
    private float _sfxVolume;

    // 패널을 열 때의 스냅샷 — 취소 시 이 값으로 되돌린다("취소 시 저장하지 않음" 요구사항).
    private float _masterVolumeBeforeOpen;
    private float _bgmVolumeBeforeOpen;
    private float _sfxVolumeBeforeOpen;

    private Resolution[] _resolutions;
    private string[] _resolutionLabels;

    // 실제로 적용/저장된 값 (Screen에 반영된 상태)
    private bool _appliedFullScreen;
    private int _appliedResolutionIndex;

    // 옵션창에서 편집 중인 임시값 ([적용] 전까지는 Screen/PlayerPrefs에 반영되지 않음)
    private bool _pendingFullScreen;
    private int _pendingResolutionIndex;

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

        if (_openOptionsButton != null) _openOptionsButton.onClick.AddListener(OpenOptionsPanel);
        if (_applyButton != null) _applyButton.onClick.AddListener(HandleApplyButtonClicked);
        if (_cancelOptionsButton != null) _cancelOptionsButton.onClick.AddListener(HandleCancelOptionsButtonClicked);
        if (_closeButton != null) _closeButton.onClick.AddListener(HandleCloseButtonClicked);
        if (_returnTitleButton != null) _returnTitleButton.onClick.AddListener(HandleReturnTitleButtonClicked);
        if (_quitButton != null) _quitButton.onClick.AddListener(HandleQuitButtonClicked);

        ConfigureSlider(_masterVolumeSlider);
        ConfigureSlider(_bgmVolumeSlider);
        ConfigureSlider(_sfxVolumeSlider);

        if (_masterVolumeSlider != null) _masterVolumeSlider.onValueChanged.AddListener(HandleMasterVolumeChanged);
        if (_bgmVolumeSlider != null) _bgmVolumeSlider.onValueChanged.AddListener(HandleBgmVolumeChanged);
        if (_sfxVolumeSlider != null) _sfxVolumeSlider.onValueChanged.AddListener(HandleSfxVolumeChanged);

        if (_windowedToggle != null) _windowedToggle.onValueChanged.AddListener(HandleWindowedToggleChanged);
        if (_fullscreenToggle != null) _fullscreenToggle.onValueChanged.AddListener(HandleFullscreenToggleChanged);

        if (_resolutionDropdown != null) _resolutionDropdown.onValueChanged.AddListener(HandleResolutionDropdownChanged);

        if (_confirmButton != null) _confirmButton.onClick.AddListener(HandleConfirmButtonClicked);
        if (_cancelButton != null) _cancelButton.onClick.AddListener(HandleCancelButtonClicked);

        // Dropdown Caption이 디자인 시점 기본 문구("New Text")로 남지 않도록, 패널을 열기 전에도
        // 한 번 현재 해상도 기준으로 채워둔다.
        RefreshScreenUI();

        // 씬에 켜둔 채 저장했더라도 시작은 닫힌 상태로 맞춘다(다른 컨트롤러들과 동일 관례).
        if (_confirmModal != null) _confirmModal.SetActive(false);
        SetOptionsPanelVisible(false);
    }

    private static void ConfigureSlider(Slider slider)
    {
        if (slider == null) return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
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

    /// <summary>
    /// 옵션 본문/ConfirmModal은 더 이상 여기서 그리지 않는다(uGUI 전환 완료).
    /// 파트너 이탈 대기·항복 요청/거절 통지만 이번 전환 범위 밖이라 IMGUI로 남아 있다.
    /// </summary>
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

        // 파트너 이탈 대기는 옵션창/항복 모달 전부보다 우선한다 — 그 외에는 아무것도 그리지 않는다.
        // ESC도 여기서 소비하지 않고 그냥 무시되므로(HandleEscapeKey 자체를 호출하지 않음) 닫히지 않는다.
        if (_partnerDisconnectModalOpen)
        {
            DrawPartnerDisconnectWaitModal();
            DrawPartnerDisconnectEndConfirmModal();
            return;
        }

        HandleEscapeKey();
        DrawSurrenderRequestModal();
        DrawSurrenderRequestConfirmModal();
        DrawSurrenderRejectedNoticeModal();

        // 항복 요청 버튼: 현재 uGUI Hierarchy에 대응 오브젝트가 없어 이번 전환 범위 밖으로 남긴다.
        // 옵션 패널이 열려 있는 동안에만 우측 상단에 작은 독립 IMGUI로 띄운다(옵션 본문과 겹치지 않음).
        if (_optionsOpen)
            DrawSurrenderFloatingSection();
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
    /// 항복 요청 버튼(및 교차취소 안내). 옵션 패널이 uGUI로 전환되며 본문 내부(IMGUI 창) 자리를
    /// 잃어 독립된 작은 영역으로 옮겼다 — 대응하는 uGUI 오브젝트가 Hierarchy에 없어 이번 범위 밖이다.
    /// </summary>
    private void DrawSurrenderFloatingSection()
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

        const float width = 160f;
        const float height = 80f;

        Rect areaRect = new Rect(Screen.width - width - 16f, 16f, width, height);

        GUILayout.BeginArea(areaRect, GUI.skin.box);

        if (GUILayout.Button("항복"))
        {
            _surrenderNoticeShown = false;
            _surrenderCrossCancelledNoticeShown = false;
            _surrenderRequestModalOpen = true;
            ToggleOptions(false);
        }

        if (_surrenderCrossCancelledNoticeShown)
        {
            GUILayout.Label("항복 요청이 동시에 발생해 취소되었습니다.");
        }

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
            // ConfirmModal이 열려 있으면 모달만 닫는다 — OptionsPanel 본문은 그대로 유지.
            CloseConfirmModal();
        }
        else
        {
            ToggleOptions(!_optionsOpen);
        }

        currentEvent.Use();
    }

    /// <summary>기존 호출부(ESC/항복 모달 취소 등) 호환용 얇은 래퍼 — 실제 동작은 Open/CloseOptionsPanel.</summary>
    private void ToggleOptions(bool open)
    {
        if (open) OpenOptionsPanel();
        else CloseOptionsPanel();
    }

    // ─────────────────────────────────────────
    // 옵션 패널 열기/닫기 (uGUI)
    // ─────────────────────────────────────────

    /// <summary>설정 버튼/ESC로 옵션 패널을 연다. 현재 저장된 값을 스냅샷으로 남기고 uGUI에 반영한다.</summary>
    private void OpenOptionsPanel()
    {
        if (_optionsOpen) return;
        _optionsOpen = true;

        _pendingFullScreen = _appliedFullScreen;
        _pendingResolutionIndex = _appliedResolutionIndex;

        _masterVolumeBeforeOpen = _masterVolume;
        _bgmVolumeBeforeOpen = _bgmVolume;
        _sfxVolumeBeforeOpen = _sfxVolume;

        RefreshVolumeUI();
        RefreshScreenUI();

        SetOptionsPanelVisible(true);
    }

    /// <summary>
    /// Close 버튼 / Cancel 버튼 / ESC가 모두 이 메서드 하나로 모인다.
    /// Apply를 하지 않았으면 화면 설정 임시값과 볼륨을 패널을 열기 전 값으로 되돌린 뒤 패널을 닫는다.
    /// </summary>
    private void CloseOptionsPanel()
    {
        if (!_optionsOpen) return;
        _optionsOpen = false;

        _pendingFullScreen = _appliedFullScreen;
        _pendingResolutionIndex = _appliedResolutionIndex;

        _masterVolume = _masterVolumeBeforeOpen;
        _bgmVolume = _bgmVolumeBeforeOpen;
        _sfxVolume = _sfxVolumeBeforeOpen;
        ApplyMasterVolume(_masterVolume);
        ApplyBgmVolume(_bgmVolume);
        ApplySfxVolume(_sfxVolume);

        RefreshVolumeUI();
        RefreshScreenUI();

        CloseConfirmModal();
        _surrenderCrossCancelledNoticeShown = false;
        PlayerPrefs.Save();

        SetOptionsPanelVisible(false);
    }

    /// <summary>옵션 패널 루트를 SetActive(true/false)로 여닫는다.</summary>
    private void SetOptionsPanelVisible(bool visible)
    {
        if (_optionsPanelRoot != null)
            _optionsPanelRoot.SetActive(visible);
    }

    // ─────────────────────────────────────────
    // 볼륨 (uGUI)
    // ─────────────────────────────────────────

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

    private void HandleMasterVolumeChanged(float value)
    {
        _masterVolume = value;
        ApplyMasterVolume(value);
        UpdateVolumeValueText(_masterVolumeValueText, value);
    }

    private void HandleBgmVolumeChanged(float value)
    {
        _bgmVolume = value;
        ApplyBgmVolume(value);
        UpdateVolumeValueText(_bgmVolumeValueText, value);
    }

    private void HandleSfxVolumeChanged(float value)
    {
        _sfxVolume = value;
        ApplySfxVolume(value);
        UpdateVolumeValueText(_sfxVolumeValueText, value);
    }

    private void RefreshVolumeUI()
    {
        SetSliderValueSilently(_masterVolumeSlider, _masterVolume);
        SetSliderValueSilently(_bgmVolumeSlider, _bgmVolume);
        SetSliderValueSilently(_sfxVolumeSlider, _sfxVolume);

        UpdateVolumeValueText(_masterVolumeValueText, _masterVolume);
        UpdateVolumeValueText(_bgmVolumeValueText, _bgmVolume);
        UpdateVolumeValueText(_sfxVolumeValueText, _sfxVolume);
    }

    private static void SetSliderValueSilently(Slider slider, float value)
    {
        if (slider == null) return;
        slider.SetValueWithoutNotify(value);
    }

    private static void UpdateVolumeValueText(TMP_Text text, float value)
    {
        if (text == null) return;
        text.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }

    // ─────────────────────────────────────────
    // 화면 모드 / 해상도 (uGUI)
    // ─────────────────────────────────────────

    private void HandleWindowedToggleChanged(bool isOn)
    {
        // ToggleGroup이 "최소 1개 선택" 상호 배타를 보장하므로 켜지는 이벤트만 반영한다.
        if (!isOn) return;
        _pendingFullScreen = false;
    }

    private void HandleFullscreenToggleChanged(bool isOn)
    {
        if (!isOn) return;
        _pendingFullScreen = true;
    }

    private void HandleResolutionDropdownChanged(int index)
    {
        _pendingResolutionIndex = index;
    }

    private void RefreshScreenUI()
    {
        if (_windowedToggle != null) _windowedToggle.SetIsOnWithoutNotify(!_pendingFullScreen);
        if (_fullscreenToggle != null) _fullscreenToggle.SetIsOnWithoutNotify(_pendingFullScreen);

        RefreshResolutionDropdownOptions();

        if (_resolutionDropdown != null)
        {
            _resolutionDropdown.SetValueWithoutNotify(_pendingResolutionIndex);
            _resolutionDropdown.RefreshShownValue();
        }
    }

    /// <summary>
    /// 실제 Screen.resolutions 기준으로 Dropdown 옵션을 다시 채운다(ClearOptions → AddOptions).
    /// 중복 해상도/주사율 병합 정책은 BuildResolutionList(기존 로직)를 그대로 재사용한다.
    /// </summary>
    private void RefreshResolutionDropdownOptions()
    {
        if (_resolutionDropdown == null) return;
        if (_resolutionLabels == null) BuildResolutionList();

        _resolutionDropdown.ClearOptions();
        _resolutionDropdown.AddOptions(new List<string>(_resolutionLabels));
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

    // ─────────────────────────────────────────
    // 버튼 (uGUI)
    // ─────────────────────────────────────────

    /// <summary>적용 — 볼륨/화면 설정을 저장·적용한다. 패널은 닫지 않는다(기존 IMGUI 적용 버튼과 동일 정책).</summary>
    private void HandleApplyButtonClicked()
    {
        PlayerPrefs.SetFloat(PREF_MASTER_VOLUME, _masterVolume);
        PlayerPrefs.SetFloat(PREF_BGM_VOLUME, _bgmVolume);
        PlayerPrefs.SetFloat(PREF_SFX_VOLUME, _sfxVolume);

        ApplyScreenSettings();

        PlayerPrefs.Save();

        // 적용 시점 기준으로 취소 스냅샷도 갱신한다 — 적용 직후 취소를 눌러도 방금 적용한 값이 유지된다.
        _masterVolumeBeforeOpen = _masterVolume;
        _bgmVolumeBeforeOpen = _bgmVolume;
        _sfxVolumeBeforeOpen = _sfxVolume;
    }

    /// <summary>옵션 취소 — Close/Cancel/ESC가 공유하는 CloseOptionsPanel()을 그대로 호출한다.</summary>
    private void HandleCancelOptionsButtonClicked() => CloseOptionsPanel();

    /// <summary>닫기 — Close/Cancel/ESC가 공유하는 CloseOptionsPanel()을 그대로 호출한다.</summary>
    private void HandleCloseButtonClicked() => CloseOptionsPanel();

    private void HandleReturnTitleButtonClicked()
    {
        _surrenderNoticeShown = false;
        _surrenderCrossCancelledNoticeShown = false;
        OpenConfirmModal(ConfirmMode.ReturnToTitle, "타이틀 화면으로 이동하시겠습니까?");
    }

    private void HandleQuitButtonClicked()
    {
        _surrenderNoticeShown = false;
        _surrenderCrossCancelledNoticeShown = false;
        OpenConfirmModal(ConfirmMode.QuitGame, "게임을 종료하시겠습니까?");
    }

    // ─────────────────────────────────────────
    // ConfirmModal (uGUI)
    // ─────────────────────────────────────────

    /// <summary>ConfirmModal(uGUI)을 연다 — 옵션 본문(OptionsPanel)은 그대로 둔 채 위에 겹쳐 띄운다.</summary>
    private void OpenConfirmModal(ConfirmMode mode, string message)
    {
        _confirmMode = mode;

        if (_confirmMessageText != null) _confirmMessageText.text = message;
        if (_confirmModal != null) _confirmModal.SetActive(true);
    }

    /// <summary>ConfirmModal만 닫는다. 옵션 본문(OptionsPanel)의 열림 상태는 건드리지 않는다.</summary>
    private void CloseConfirmModal()
    {
        _confirmMode = ConfirmMode.None;

        if (_confirmModal != null) _confirmModal.SetActive(false);
    }

    /// <summary>ConfirmModal의 확인 버튼. 열려 있던 확인 종류에 해당하는 기존 로직만 실행한다.</summary>
    private void HandleConfirmButtonClicked()
    {
        ConfirmMode selectedMode = _confirmMode;
        CloseConfirmModal();

        if (selectedMode == ConfirmMode.ReturnToTitle)
            ConfirmReturnToTitle();
        else if (selectedMode == ConfirmMode.QuitGame)
            QuitGame();
    }

    /// <summary>ConfirmModal의 취소 버튼. 아무 로직도 실행하지 않고 모달만 닫는다.</summary>
    private void HandleCancelButtonClicked()
    {
        CloseConfirmModal();
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

        var widths = new List<int>();
        var heights = new List<int>();
        var refreshRates = new List<RefreshRate>();

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
