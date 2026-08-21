using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 게임 중 열 수 있는 옵션창.
/// 마스터/배경음/효과음 볼륨, 화면 모드/해상도(적용/취소), 항복 안내, 타이틀 이동, 게임 종료 기능을 제공한다.
///
/// 옵션 본문/확인 모달(ConfirmModal)은 uGUI 오브젝트 참조로 제어한다(IMGUI로 그리지 않음).
/// 옵션 패널은 _optionsPanelRoot.SetActive(true/false)로 여닫는다.
///
/// 항복 3종(요청 수신·거절 안내·교차취소 안내)과 파트너 이탈 3종(재접속 대기·종료 확인·재접속
/// 포기 통지)은 모두 공용 <see cref="ModalDialogUI"/> 한 장을 돌려쓴다. 어느 것을 띄울지는
/// <see cref="PollPartnerDisconnectModals"/>와 <see cref="PollSurrenderModals"/>가 Update에서
/// 상태를 보고 정하며, 앞의 것이 우선한다.
///
/// 남은 IMGUI는 <see cref="OnGUI"/>의 ESC 처리 하나뿐이다. 입력 차단도 EventSystem을 끄는 방식이
/// 아니라 ModalDialog_Pf의 Dim(Raycast Target 켜진 전체화면 Image)이 맡는다.
/// </summary>
public class OptionsPanelUI : MonoBehaviour
{
    private enum ConfirmMode
    {
        None,
        ReturnToTitle,
        QuitGame,
        Surrender
    }

    /// <summary>
    /// 목차(Nav_Panel)의 탭 한 종류. [기본값 복원]이 "현재 보고 있는 페이지만" 되돌리므로
    /// 페이지를 코드가 구분할 수 있어야 한다.
    /// <para>
    /// 인덱스가 아니라 enum인 이유 — 인스펙터에서 탭 순서를 바꿔도 복원 대상이 어긋나지 않는다.
    /// 페이지를 추가할 때는 여기에 항목을 하나 넣고 <see cref="RestorePageDefaults"/>의 switch에
    /// 대응 분기를 추가하면 된다(분기를 빠뜨리면 그 페이지는 복원되지 않을 뿐 다른 곳은 멀쩡하다).
    /// </para>
    /// </summary>
    private enum OptionsPage
    {
        Screen,
        Sound
    }

    /// <summary>목차 토글 하나와 그 토글이 켜졌을 때 보일 페이지의 짝.</summary>
    [System.Serializable]
    private struct OptionsTab
    {
        [Tooltip("Nav_Panel 아래의 목차 토글. Nav_Panel의 ToggleGroup에 묶어둘 것.")]
        public Toggle toggle;

        [Tooltip("이 탭이 켜졌을 때만 활성화될 페이지(Screen_Page / Sound_Page).")]
        public GameObject page;

        [Tooltip("[기본값 복원]이 어느 값들을 되돌릴지 고르는 데 쓴다.")]
        public OptionsPage kind;
    }

    [Header("씬 전환")]
    [Tooltip("타이틀로 버튼 확인 시 로드할 씬 이름입니다. " +
             "비어 있거나 Build Settings에 등록되지 않은 경우 이동하지 않습니다.")]
    [SerializeField] private string _titleSceneName = "";

    [Header("컨텍스트")]
    [Tooltip("true면 공용 설정(볼륨/화면/해상도/적용/기본값복원/닫기)만 쓰는 인스턴스로 동작한다 " +
             "(타이틀 화면용). 게임씬 전용 GameEvents 구독(파트너 이탈/세션 종료/게임 클리어)과 " +
             "그에 딸린 Update 폴링(항복·파트너 이탈 모달)을 전부 건너뛴다 — 항복/타이틀이동/게임종료/" +
             "확인모달/공용 안내모달 필드는 이 인스턴스에서 애초에 연결하지 않는 것이 원칙이다(연결해도 " +
             "이벤트 구독 자체가 없어 무해하지만, Inspector에 게임씬 전용 참조를 남기지 않는 것을 권장). " +
             "게임씬 인스턴스는 반드시 false(기본값)로 두어 기존 동작을 그대로 유지한다.")]
    [SerializeField] private bool _settingsOnly;

    [Header("옵션 패널 (uGUI)")]
    [Tooltip("옵션 패널 루트. SetActive(true/false)로 여닫는다.")]
    [SerializeField] private GameObject _optionsPanelRoot;
    [Tooltip("설정 버튼. 클릭 시 uGUI 옵션 패널을 연다.")]
    [SerializeField] private Button _openOptionsButton;

    [Header("옵션 패널 — 볼륨")]
    [Tooltip("Sound_Page/MasterVolume — VolumeRow_Pf 인스턴스. 슬라이더·% 텍스트는 행이 알아서 다룬다.")]
    [SerializeField] private VolumeRowUI _masterRow;
    [Tooltip("Sound_Page/BgmVolume.")]
    [SerializeField] private VolumeRowUI _bgmRow;
    [Tooltip("Sound_Page/SfxVolume.")]
    [SerializeField] private VolumeRowUI _sfxRow;

    [Header("옵션 패널 — 화면")]
    [SerializeField] private Toggle _windowedToggle;
    [SerializeField] private Toggle _fullscreenToggle;
    [SerializeField] private TMP_Dropdown _resolutionDropdown;

    [Header("옵션 패널 — 목차 탭")]
    [Tooltip("Nav_Panel의 목차 토글과 페이지의 짝. 첫 번째 항목이 패널을 열 때의 기본 탭이다. " +
             "탭을 늘릴 때는 여기에 줄을 추가하면 되고 코드는 건드릴 필요 없다(기본값 복원 분기만 예외).")]
    [SerializeField] private OptionsTab[] _tabs;

    [Header("옵션 패널 — 버튼")]
    [Tooltip("적용 후 패널을 닫는 버튼(표시 문구는 '확인'). 바꾼 값이 없으면 자동으로 비활성화된다.")]
    [SerializeField] private Button _applyButton;
    [SerializeField] private Button _cancelOptionsButton;
    [SerializeField] private Button _closeButton;
    [Tooltip("현재 보고 있는 페이지의 값만 게임 기본값으로 되돌린다. PlayerPrefs에는 쓰지 않으므로 " +
             "[취소]로 되돌릴 수 있다.")]
    [SerializeField] private Button _restoreDefaultsButton;
    [Tooltip("겸용 버튼 — 솔로/1인 방에서는 타이틀 이동, 2인 방에서는 항복 요청. " +
             "버튼 문구는 씬에 적은 그대로 두고, 확인 팝업 문구만 상황에 따라 갈린다.")]
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

    [Header("공용 모달 (uGUI)")]
    [Tooltip("항복·파트너 이탈 관련 안내/선택 팝업(ModalDialog_Pf). " +
             "⚠️ 반드시 Canvas 직계에 둘 것 — OptionsPanel 밑에 두면 옵션창을 닫을 때 같이 꺼져서 " +
             "옵션창과 무관하게 떠야 할 안내가 안 뜬다.")]
    [SerializeField] private ModalDialogUI _modalDialog;

    private const string PREF_MASTER_VOLUME = "MasterVolume";
    private const string PREF_BGM_VOLUME = "BgmVolume";
    private const string PREF_SFX_VOLUME = "SfxVolume";

    private const string PREF_FULLSCREEN = "FullScreen";
    // 해상도는 목록에서의 순번이 아니라 값 자체로 저장한다 — 순번을 저장하면 지원 해상도 목록을
    // 손볼 때(추가·삭제) 같은 순번이 다른 해상도를 가리켜 플레이어 설정이 조용히 바뀐다.
    // 구 키 "ResolutionIndex"는 더 이상 읽지도 쓰지도 않는다(남아 있어도 무시된다).
    private const string PREF_RESOLUTION_WIDTH  = "ResolutionWidth";
    private const string PREF_RESOLUTION_HEIGHT = "ResolutionHeight";

    // ── 게임이 제공하는 기본값 ([기본값 복원]의 기준이자, 저장된 값이 없을 때의 초기값) ──
    // 저장값이 없을 때의 폴백도 반드시 이 상수를 쓴다. 폴백을 "씬에 찍혀 있던 값"으로 두면
    // 처음 켰을 때의 값과 [기본값 복원]을 누른 뒤의 값이 서로 달라진다.
    private const float DEFAULT_MASTER_VOLUME = 1f;
    private const float DEFAULT_SUB_VOLUME = 1f;
    private const bool DEFAULT_FULLSCREEN = true;

    // 모니터 비율을 못 읽었을 때 쓸 비율. CanvasScaler Reference(1920x1080)와 같은 값이어야 한다.
    private const float DEFAULT_ASPECT = 16f / 9f;

    // 종횡비 비교 여유. 같은 비율의 해상도끼리 부동소수 나눗셈 결과가 마지막 자리에서 갈리는 것만
    // 흡수하면 되므로 아주 작아도 된다(16:9와 16:10의 차이는 0.178).
    private const float ASPECT_EPSILON = 0.001f;

    private bool _optionsOpen;

    private float _masterVolume;
    private float _bgmVolume;
    private float _sfxVolume;

    // 마지막으로 [확인]까지 눌러 저장된 볼륨.
    // 두 가지 용도를 겸한다 — [취소] 시 되돌릴 목적지이자, [확인] 버튼 활성화를 판단하는 기준선.
    private float _appliedMasterVolume;
    private float _appliedBgmVolume;
    private float _appliedSfxVolume;

    private Resolution[] _resolutions;
    private string[] _resolutionLabels;

    // 실제로 적용/저장된 값 (Screen에 반영된 상태)
    private bool _appliedFullScreen;

    // 적용된 해상도는 순번이 아니라 값으로 들고 있는다. 목록(_resolutions)이 화면 모드에 따라
    // 달라지기 때문이다 — 창 모드에서는 모니터와 같은 크기가 빠지므로, 토글을 한 번 누르면 같은
    // 순번이 다른 해상도를 가리킨다. PlayerPrefs를 순번 대신 값으로 저장하기로 한 것과 같은 이유다.
    private Resolution _appliedResolution;

    // 옵션창에서 편집 중인 임시값 ([적용] 전까지는 Screen/PlayerPrefs에 반영되지 않음)
    private bool _pendingFullScreen;
    private int _pendingResolutionIndex;

    private ConfirmMode _confirmMode = ConfirmMode.None;

    // 1회성 안내의 "대기" 플래그. 알림을 받은 것과 실제로 띄운 것은 별개다 —
    // 더 시급한 모달이 떠 있으면 자리가 날 때까지 대기 상태로 들고 있는다.
    private bool _surrenderNoticeShown;
    private bool _surrenderCrossCancelledNoticeShown;

    /// <summary>
    /// 공용 모달(ModalDialogUI)이 지금 무엇을 보여주고 있는지. 같은 것을 매 프레임 다시 띄우지 않으려고 기억한다.
    /// <para>
    /// <see cref="IncomingSurrenderRequest"/>만 성격이 다르다 — 나머지는 확인을 누르면 끝나는 1회성
    /// 안내지만, 이건 네트워크 상태(HasIncomingSurrenderRequest)를 그대로 비추는 창이라 그 상태가
    /// 사라지면 내가 답하지 않았어도 닫아야 한다(교차취소, 요청자 이탈 등).
    /// </para>
    /// </summary>
    private enum ModalContent
    {
        None,
        SurrenderRejected,
        SurrenderCrossCancelled,
        IncomingSurrenderRequest,
        PartnerDisconnectWait,
        PartnerDisconnectEndConfirm,
        PartnerGaveUp,
        Defeat,
        Victory
    }

    private ModalContent _activeModal = ModalContent.None;

    // 파트너 네트워크 이탈 대기. 항복 관련 무엇보다 우선한다(PollPartnerDisconnectModals).
    private bool _partnerDisconnectModalOpen;
    private bool _partnerGiveUpAvailable;
    private bool _partnerDisconnectEndConfirmOpen;

    // 대기 모달에 [포기하기]를 이미 붙였는지. 유예가 끝난 뒤 매 프레임 다시 붙이지 않으려고 둔다.
    private bool _partnerGiveUpButtonShown;

    // 패배 확인 모달(TeamHpZero/ReconnectFailed 공용). 한 매치에 한 번만 뜨면 되므로(재오픈 방어)
    // _defeatModalShown으로 막는다. _defeatModalReason은 확인 버튼을 눌렀을 때 어떤 사유로 타이틀
    // 복귀해야 하는지(ReturnToTitleAfterMatchEnd에 전달할 실제 reason) 기억해두는 용도다
    // (2026-08 코드리뷰 대응 — 이전엔 TeamHpZero로 고정 전달해 ReconnectFailed도 잘못된 사유로
    // 기록될 뻔했다).
    private bool _defeatModalShown;
    private SessionEndReason _defeatModalReason;
    private Coroutine _defeatModalRoutine;

    // 승리(챕터 클리어) 확인 모달. Defeat와 동일한 재오픈 방어 목적(2026-08 코드리뷰 대응).
    private bool _gameClearedModalShown;

    private void Awake()
    {
        _masterVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(PREF_MASTER_VOLUME, DEFAULT_MASTER_VOLUME));

        _bgmVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(PREF_BGM_VOLUME, DEFAULT_SUB_VOLUME));

        _sfxVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(PREF_SFX_VOLUME, DEFAULT_SUB_VOLUME));

        // 방금 읽은 값이 곧 "적용된 값"이다 — 여기서 기준선을 잡아둬야 패널을 처음 열었을 때
        // [확인]이 비활성으로 시작한다.
        _appliedMasterVolume = _masterVolume;
        _appliedBgmVolume = _bgmVolume;
        _appliedSfxVolume = _sfxVolume;

        AudioListener.volume = _masterVolume;

        LoadAndApplyDisplaySettings();

        if (_openOptionsButton != null) _openOptionsButton.onClick.AddListener(OpenOptionsPanel);
        if (_applyButton != null) _applyButton.onClick.AddListener(HandleApplyButtonClicked);
        if (_cancelOptionsButton != null) _cancelOptionsButton.onClick.AddListener(HandleCancelOptionsButtonClicked);
        if (_closeButton != null) _closeButton.onClick.AddListener(HandleCloseButtonClicked);
        if (_restoreDefaultsButton != null) _restoreDefaultsButton.onClick.AddListener(HandleRestoreDefaultsButtonClicked);
        if (_returnTitleButton != null) _returnTitleButton.onClick.AddListener(HandleReturnTitleButtonClicked);
        if (_quitButton != null) _quitButton.onClick.AddListener(HandleQuitButtonClicked);

        InitializeTabs();

        // 행이 스스로 Awake에서 초기화하지 않고 여기서 불러준다 — Sound_Page가 탭 전환으로 꺼져 있거나
        // OptionsPanel이 닫힌 채 저장돼 있으면 행의 Awake 자체가 돌지 않기 때문(VolumeRowUI.Initialize 참고).
        InitializeVolumeRow(_masterRow, HandleMasterVolumeChanged);
        InitializeVolumeRow(_bgmRow, HandleBgmVolumeChanged);
        InitializeVolumeRow(_sfxRow, HandleSfxVolumeChanged);

        if (_windowedToggle != null) _windowedToggle.onValueChanged.AddListener(HandleWindowedToggleChanged);
        if (_fullscreenToggle != null) _fullscreenToggle.onValueChanged.AddListener(HandleFullscreenToggleChanged);

        if (_resolutionDropdown != null) _resolutionDropdown.onValueChanged.AddListener(HandleResolutionDropdownChanged);

        if (_confirmButton != null) _confirmButton.onClick.AddListener(HandleConfirmButtonClicked);
        if (_cancelButton != null) _cancelButton.onClick.AddListener(HandleCancelButtonClicked);

        // Dropdown Caption이 디자인 시점 기본 문구("New Text")로 남지 않도록, 패널을 열기 전에도
        // 한 번 현재 해상도 기준으로 채워둔다.
        RefreshScreenUI();
        RefreshApplyButtonInteractable();

        // 씬에 켜둔 채 저장했더라도 시작은 닫힌 상태로 맞춘다(다른 컨트롤러들과 동일 관례).
        if (_confirmModal != null) _confirmModal.SetActive(false);
        SetOptionsPanelVisible(false);
    }

    private static void InitializeVolumeRow(VolumeRowUI row, UnityAction<float> onValueChanged)
    {
        if (row == null) return;

        row.Initialize(onValueChanged);
    }

    // ─────────────────────────────────────────
    // 목차 탭 (Nav_Panel)
    // ─────────────────────────────────────────

    /// <summary>
    /// 각 목차 토글에 "켜지면 내 페이지를 보여준다"를 걸어둔다.
    /// 상호 배타(한 번에 하나만 켜짐)는 코드가 아니라 Nav_Panel의 ToggleGroup이 보장한다 —
    /// 그래서 여기서는 켜지는 쪽만 처리하고 꺼지는 이벤트는 무시한다.
    /// </summary>
    private void InitializeTabs()
    {
        if (_tabs == null) return;

        for (int i = 0; i < _tabs.Length; i++)
        {
            OptionsTab tab = _tabs[i];
            if (tab.toggle == null) continue;

            // 지역 복사본으로 캡처한다 — 반복 변수를 그대로 쓰면 모든 리스너가 마지막 탭을 가리킨다.
            GameObject page = tab.page;

            tab.toggle.onValueChanged.AddListener(isOn =>
            {
                if (page != null) page.SetActive(isOn);
            });
        }

        // 씬에 두 페이지가 다 켜진 채 저장돼 있어도 시작부터 하나만 보이게 맞춘다.
        SelectTab(0);
    }

    /// <summary>
    /// index번째 탭을 선택한다. 토글을 켜는 것으로 페이지 전환까지 함께 일어난다
    /// (ToggleGroup이 나머지를 꺼주고, 각 토글의 리스너가 자기 페이지를 껐다 켠다).
    /// </summary>
    private void SelectTab(int index)
    {
        if (_tabs == null || _tabs.Length == 0) return;

        index = Mathf.Clamp(index, 0, _tabs.Length - 1);

        for (int i = 0; i < _tabs.Length; i++)
        {
            OptionsTab tab = _tabs[i];
            bool selected = i == index;

            // 토글이 비어 있어도 페이지 표시는 맞춰준다(배선이 덜 된 상태에서도 화면은 정상).
            if (tab.page != null) tab.page.SetActive(selected);
            if (tab.toggle != null) tab.toggle.SetIsOnWithoutNotify(selected);
        }
    }

    /// <summary>지금 켜져 있는 탭. 하나도 못 찾으면 첫 번째 탭으로 친다.</summary>
    private int CurrentTabIndex
    {
        get
        {
            if (_tabs == null) return 0;

            for (int i = 0; i < _tabs.Length; i++)
                if (_tabs[i].toggle != null && _tabs[i].toggle.isOn)
                    return i;

            return 0;
        }
    }

    private void LoadAndApplyDisplaySettings()
    {
        bool currentFullScreen = Screen.fullScreen;
        _appliedFullScreen = PlayerPrefs.GetInt(
            PREF_FULLSCREEN,
            currentFullScreen ? 1 : 0) == 1;

        BuildResolutionList(windowed: !_appliedFullScreen);
        _appliedResolution = ResolveStartupResolution();

        if (_appliedFullScreen != currentFullScreen)
            ApplyScreenMode();

        // 실제 창 크기와 다를 때만 적용한다 — 같은 값을 다시 넣으면 창이 한 번 깜빡인다.
        if (_appliedResolution.width != Screen.width || _appliedResolution.height != Screen.height)
            ApplyResolution();

        _pendingFullScreen = _appliedFullScreen;
        SyncPendingResolutionToApplied();
    }

    private void OnEnable()
    {
        // 설정 전용(타이틀) 인스턴스는 항복/파트너 이탈/세션 종료/게임 클리어 어느 것도 절대
        // 일어나지 않는 화면이므로 구독 자체를 하지 않는다 — OnDisable의 대응 -=는 구독한 적 없는
        // 핸들러를 떼는 것이라 C# 이벤트상 안전한 no-op이라 그대로 둬도 된다.
        if (_settingsOnly) return;

        GameEvents.OnOpponentDisconnected += HandlePartnerDisconnected;
        GameEvents.OnGracePeriodExpired   += HandlePartnerGiveUpAvailable;
        GameEvents.OnOpponentReconnected  += HandlePartnerReconnected;
        GameEvents.OnSessionEnded         += HandleSessionEnded;
        GameEvents.OnGameCleared          += HandleGameCleared;

        // "파트너 응답 불능"(진짜 이탈은 아님) — 화면은 위 파트너 이탈 대기 모달과 동일하게 재사용하되,
        // RoundPhaseManager/PartnerBattleMirrorController/PartnerSpectateView 등 "진짜 이탈" 전용
        // 부작용을 가진 다른 구독자들과는 별개 이벤트로 분리돼 있다(GameEvents.cs 참고).
        GameEvents.OnPartnerResultUnresponsive    += HandlePartnerResultUnresponsive;
        GameEvents.OnPartnerResultGiveUpAvailable += HandlePartnerResultGiveUpAvailable;
        GameEvents.OnPartnerResultRecovered       += HandlePartnerReconnected;
    }

    private void OnDisable()
    {
        GameEvents.OnOpponentDisconnected -= HandlePartnerDisconnected;
        GameEvents.OnGracePeriodExpired   -= HandlePartnerGiveUpAvailable;
        GameEvents.OnOpponentReconnected  -= HandlePartnerReconnected;
        GameEvents.OnSessionEnded         -= HandleSessionEnded;
        GameEvents.OnGameCleared          -= HandleGameCleared;

        GameEvents.OnPartnerResultUnresponsive    -= HandlePartnerResultUnresponsive;
        GameEvents.OnPartnerResultGiveUpAvailable -= HandlePartnerResultGiveUpAvailable;
        GameEvents.OnPartnerResultRecovered       -= HandlePartnerReconnected;

        if (_defeatModalRoutine != null)
        {
            StopCoroutine(_defeatModalRoutine);
            _defeatModalRoutine = null;
        }

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

    /// <summary>파트너 응답 불능(진짜 이탈 아님) — HandlePartnerDisconnected와 화면 동작은 동일하다.</summary>
    private void HandlePartnerResultUnresponsive()
    {
        _partnerDisconnectModalOpen = true;
        _partnerGiveUpAvailable = false;
        _partnerDisconnectEndConfirmOpen = false;
    }

    /// <summary>위 상태에서 30초 경과 — HandlePartnerGiveUpAvailable과 화면 동작은 동일하다.</summary>
    private void HandlePartnerResultGiveUpAvailable()
    {
        _partnerGiveUpAvailable = true;
    }

    private void HandlePartnerReconnected()
    {
        _partnerDisconnectModalOpen = false;
        _partnerGiveUpAvailable = false;
        _partnerDisconnectEndConfirmOpen = false;

        // 떠 있던 대기 모달은 다음 Update의 PollPartnerDisconnectModals가 내린다.
    }

    // ─────────────────────────────────────────
    // 세션 종료 (GameEvents.OnSessionEnded 구독) — 항복 합의/패배 확인
    // ─────────────────────────────────────────

    /// <summary>
    /// NetworkManager가 이미 발행한 세션 종료 사유를 구독해 "반응"만 한다 — 여기서 SessionEnded를
    /// 다시 발행하지 않는다(MatchRecorder가 같은 이벤트로 전적을 기록하므로 중복 발행하면 중복 기록됨).
    /// <list type="bullet">
    /// <item>Surrender — 이미 두 클라이언트가 옵션창에서 항복을 요청/승인한 상태라 별도 확인 없이
    /// 곧바로 정상 종료 타이틀 복귀 경로를 탄다.</item>
    /// <item>TeamHpZero — 패배 확인 모달을 띄운다(3초 뒤 확인 버튼 노출, ShowDefeatModal).</item>
    /// <item>ReconnectFailed — 본인 재접속(NetworkManager.SelfReconnectRoutine)이 유예시간 내 끝내
    /// 실패한 경우다. PartnerAbandoned와 달리 이 시점 이전에 어떤 모달도 뜬 적이 없으므로(파트너
    /// 이탈 흐름과는 무관 — 그쪽은 "상대방"이 끊겼을 때만 반응한다) 여기서도 반드시 안내해야 한다
    /// (2026-08 코드리뷰 대응 — 예전엔 이 case가 없어 화면이 방치됐다). TeamHpZero와 같은
    /// ShowDefeatModal 흐름을 재사용하되 문구만 원인에 맞게 다르게 준다.</item>
    /// <item>그 외(PartnerAbandoned/BothDisconnected) — PartnerAbandoned는 HandlePartnerDisconnectGiveUp/
    /// HandlePartnerGaveUpConfirmed가 이 이벤트 발행 "이전에" 이미 모달을 보여주고 사용자 선택까지
    /// 받은 뒤이므로 여기서 또 반응할 필요가 없다. BothDisconnected는 현재 어디서도 실제로 발행되지
    /// 않는다.</item>
    /// </list>
    /// </summary>
    private void HandleSessionEnded(SessionEndReason reason)
    {
        switch (reason)
        {
            case SessionEndReason.Surrender:
                ReturnToTitleAfterMatchEnd(reason);
                break;

            case SessionEndReason.TeamHpZero:
                ShowDefeatModal(reason, "패배했습니다.");
                break;

            case SessionEndReason.ReconnectFailed:
                ShowDefeatModal(reason, "연결이 끊겨 재접속에 실패했습니다.");
                break;
        }
    }

    /// <summary>
    /// 패배 확인 모달을 띄운다(TeamHpZero/ReconnectFailed 공용 — 문구만 다르고 흐름은 동일).
    /// 처음 3초는 버튼 없이 안내만(ShowWaiting) 보여주고, 3초 뒤 SetPrimaryAction으로 확인 버튼을
    /// 뒤늦게 붙인다 — 즉시 자동으로 타이틀로 이동하지 않기 위함(요구사항). 한 매치에 한 번만
    /// 뜨면 되므로 _defeatModalShown으로 재오픈을 막는다. reason은 확인 버튼을 눌렀을 때
    /// ReturnToTitleAfterMatchEnd에 그대로 전달해야 실제 종료 사유가 정확히 기록된다(2026-08
    /// 코드리뷰 대응 — 예전엔 TeamHpZero로 고정 전달해 ReconnectFailed도 잘못 기록될 뻔했다).
    /// </summary>
    private void ShowDefeatModal(SessionEndReason reason, string message)
    {
        if (_defeatModalShown || _modalDialog == null) return;
        _defeatModalShown = true;
        _defeatModalReason = reason;

        _activeModal = ModalContent.Defeat;
        _modalDialog.ShowWaiting(message);

        if (_defeatModalRoutine != null) StopCoroutine(_defeatModalRoutine);
        _defeatModalRoutine = StartCoroutine(DefeatConfirmDelayRoutine());
    }

    /// <summary>Time.timeScale 영향을 받지 않는 3초 대기 후 확인 버튼을 붙인다.</summary>
    private System.Collections.IEnumerator DefeatConfirmDelayRoutine()
    {
        yield return new WaitForSecondsRealtime(3f);
        _defeatModalRoutine = null;

        // 대기하는 3초 사이 모달이 이미 닫혔거나 다른 내용으로 바뀌었으면 버튼을 붙이지 않는다.
        if (_modalDialog != null && _activeModal == ModalContent.Defeat)
            _modalDialog.SetPrimaryAction("확인", HandleDefeatConfirmClicked);
    }

    /// <summary>패배 확인 모달의 [확인]. 모달을 띄울 때 저장해둔 실제 종료 사유(_defeatModalReason —
    /// TeamHpZero 또는 ReconnectFailed)로 정상 종료 타이틀 복귀 경로를 탄다.</summary>
    private void HandleDefeatConfirmClicked()
    {
        _activeModal = ModalContent.None;
        ReturnToTitleAfterMatchEnd(_defeatModalReason);
    }

    /// <summary>
    /// GameEvents.OnGameCleared(1-5 최종 라운드 클리어, RoundPhaseManager가 GamePhase.Victory로
    /// 전환할 때 발행) 구독 — 승리 결과 모달을 띄운다. Defeat와 달리 3초 대기 없이 즉시 [타이틀로
    /// 이동] 버튼을 붙인다(패배처럼 "즉시 자동 이동"을 막을 이유가 없음 — 애초에 자동 전환이 없다).
    /// 한 매치에 한 번만 뜨면 되므로 _gameClearedModalShown으로 재오픈을 막는다(Defeat와 동일 목적).
    /// </summary>
    private void HandleGameCleared()
    {
        if (_gameClearedModalShown || _modalDialog == null) return;
        _gameClearedModalShown = true;

        _activeModal = ModalContent.Victory;
        _modalDialog.ShowNotice("게임을 클리어했습니다.", "타이틀로 이동", HandleGameClearedConfirmClicked);
    }

    /// <summary>승리 확인 모달의 [타이틀로 이동]. completed-match 종료 경로로 나간다.
    /// MatchRecorder.HandleGameCleared가 이미 "GameCleared"라는 동일 문자열로 전적을 기록하므로
    /// (Finalize("Victory", "GameCleared")) 새 SessionEndReason을 추가하지 않고 그 표현을 재사용한다.</summary>
    private void HandleGameClearedConfirmClicked()
    {
        _activeModal = ModalContent.None;
        ReturnToTitleAfterMatchEnd("GameCleared");
    }

    /// <summary>
    /// 완전히 종료된 매치(항복 합의/패배 확인/파트너 재접속 포기)에서 타이틀로 복귀한다. 일반 타이틀
    /// 이동(<see cref="ConfirmReturnToTitle"/>)과 씬 이름 검증·PlayerPrefs 저장 절차는 동일하되,
    /// <see cref="NetworkManager.RequestCompletedMatchReturnToTitle"/>를 통해 저장된 재접속
    /// 세션까지 함께 정리한다는 점만 다르다 — 이미 끝난 매치를 타이틀 화면의 [이전 게임으로
    /// 들어가기] 후보로 남기지 않기 위함.
    /// </summary>
    private void ReturnToTitleAfterMatchEnd(SessionEndReason reason) =>
        ReturnToTitleAfterMatchEnd(reason.ToString());

    /// <summary>위 오버로드의 실제 구현. Victory는 SessionEndReason에 대응 값이 없으므로(그 enum은
    /// "세션 종료(패배) 사유" 전용 — GameEvents.cs 참고) 문자열 그대로 받는 이 오버로드를 직접 쓴다.</summary>
    private void ReturnToTitleAfterMatchEnd(string reason)
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

        if (GameManager.TryGet(out var gameManager) && gameManager.Network != null)
            gameManager.Network.RequestCompletedMatchReturnToTitle(_titleSceneName, reason);
        else
            SceneManager.LoadScene(_titleSceneName);
    }

    /// <summary>
    /// ESC 하나만 남았다. 나머지 모달은 전부 uGUI로 옮겨가 Update에서 상태를 보고 띄운다.
    /// </summary>
    private void OnGUI()
    {
        HandleEscapeKey();
    }

    /// <summary>
    /// uGUI로 옮겨간 안내는 OnGUI가 아니라 여기서 다룬다.
    /// OnGUI는 한 프레임에 여러 번(Layout/Repaint 등) 도는 반면 Update는 프레임당 한 번이라,
    /// 알림을 소비(Acknowledge)하고 상태를 바꾸는 일은 이쪽이 맞다.
    /// </summary>
    private void Update()
    {
        // 설정 전용(타이틀) 인스턴스는 항복/파트너 이탈/패배/승리 폴링 자체를 하지 않는다 — 게임씬
        // 상태(_activeModal 등)가 이 인스턴스에서는 애초에 None으로만 머무르므로 OnGUI의 ESC 처리는
        // 손대지 않아도 자연히 "옵션창 토글"로만 수렴한다(별도 분기 불필요).
        if (_settingsOnly) return;

        // 패배(TeamHpZero)/승리(챕터 클리어) 확인 모달이 최우선 — 이미 매치가 완전히 끝난 뒤라
        // 파트너 이탈/항복 관련 모달이 그 위를 덮어써서는 안 된다. 오직 이 모달 자신의 확인
        // 버튼으로만 내려간다.
        if (_activeModal == ModalContent.Defeat || _activeModal == ModalContent.Victory)
            return;

        // 파트너 이탈 관련이 최우선 — 그쪽이 모달을 잡고 있으면 항복 쪽은 손대지 않는다.
        // (IMGUI 시절 OnGUI 최상단의 early-return 두 개가 표현하던 우선순위를 그대로 옮긴 것)
        if (PollPartnerDisconnectModals())
            return;

        PollSurrenderModals();
    }

    // ─────────────────────────────────────────
    // 항복 안내 (uGUI 공용 모달)
    // ─────────────────────────────────────────

    /// <summary>
    /// 항복 관련 1회성 안내를 폴링해 공용 모달로 띄운다.
    /// <para>
    /// <b>수신과 표시를 나눈 이유</b> — 모달 인스턴스가 하나뿐이라 더 시급한 것이 떠 있으면 지금
    /// 띄울 수 없다. 알림은 1회성이라(Acknowledge하면 사라진다) 받는 즉시 대기 플래그로 옮겨두고,
    /// 자리가 나는 프레임에 띄운다. IMGUI 시절 "양보하고 다음 프레임에 다시 시도"와 같은 정책을
    /// 폴링 구조에 맞게 옮긴 것이다.
    /// </para>
    /// </summary>
    private void PollSurrenderModals()
    {
        if (!GameManager.TryGet(out var gameManager) || gameManager.Network == null)
            return;

        // ── 1회성 알림 수신 → 대기 플래그로 옮긴다(표시 여부는 아래에서 우선순위를 보고 정한다).
        if (gameManager.Network.SurrenderRequestRejected)
        {
            gameManager.Network.AcknowledgeSurrenderRejected();
            _surrenderNoticeShown = true;
        }

        if (gameManager.Network.SurrenderRequestCrossCancelled)
        {
            gameManager.Network.AcknowledgeSurrenderCrossCancelled();
            _surrenderCrossCancelledNoticeShown = true;
        }

        if (_modalDialog == null) return;

        // ── 파트너의 항복 요청이 최우선. 다른 게 떠 있어도 밀어내고 띄운다.
        if (gameManager.Network.HasIncomingSurrenderRequest)
        {
            // 내가 보내려던 항복 확인 팝업은 접는다 — 파트너 요청이 우선이라 둘이 겹치면 안 된다.
            if (_confirmMode == ConfirmMode.Surrender)
                CloseConfirmModal();

            if (_activeModal != ModalContent.IncomingSurrenderRequest)
            {
                _activeModal = ModalContent.IncomingSurrenderRequest;

                _modalDialog.ShowChoice(
                    "파트너가 항복을 요청했습니다.\n항복하시겠습니까?",
                    "항복하기", () => RespondToIncomingSurrender(true),
                    "계속하기", () => RespondToIncomingSurrender(false));
            }

            return;
        }

        // 요청 상태가 사라졌는데 창이 남아 있으면 내린다(교차취소·요청자 이탈로 내가 답하기 전에 끝난 경우).
        if (_activeModal == ModalContent.IncomingSurrenderRequest)
        {
            _modalDialog.Close();
            _activeModal = ModalContent.None;
        }

        // ── 안내는 자리가 났을 때만.
        if (_activeModal != ModalContent.None) return;
        if (_confirmMode != ConfirmMode.None) return;
        if (_partnerDisconnectModalOpen || gameManager.Network.PartnerGaveUpReconnect) return;

        if (_surrenderNoticeShown)
        {
            _activeModal = ModalContent.SurrenderRejected;
            _modalDialog.ShowNotice("파트너가 항복 요청을 거절했습니다.", "확인", HandleNoticeConfirmed);
            return;
        }

        if (_surrenderCrossCancelledNoticeShown)
        {
            _activeModal = ModalContent.SurrenderCrossCancelled;
            _modalDialog.ShowNotice("항복 요청이 동시에 발생해 취소되었습니다.", "확인", HandleNoticeConfirmed);
        }
    }

    /// <summary>파트너 요청에 답한다. 모달은 ModalDialogUI가 이미 닫은 뒤라 상태만 정리하면 된다.</summary>
    private void RespondToIncomingSurrender(bool accepted)
    {
        _activeModal = ModalContent.None;

        if (GameManager.TryGet(out var gameManager) && gameManager.Network != null)
            gameManager.Network.RespondToSurrender(accepted);
    }

    /// <summary>안내의 [확인]을 눌렀을 때. 모달은 ModalDialogUI가 이미 닫았고 대기 플래그만 내린다.</summary>
    private void HandleNoticeConfirmed()
    {
        switch (_activeModal)
        {
            case ModalContent.SurrenderRejected:
                _surrenderNoticeShown = false;
                break;

            case ModalContent.SurrenderCrossCancelled:
                _surrenderCrossCancelledNoticeShown = false;
                break;
        }

        _activeModal = ModalContent.None;
    }

    /// <summary>
    /// 지금 떠 있는 안내만 ESC로 닫는다. 대기 중인 다른 안내는 그대로 두므로 다음 프레임에 이어서 뜬다
    /// (확인 버튼을 누른 것과 같은 처리 + 모달 닫기 — Close는 콜백을 부르지 않으므로 직접 정리한다).
    /// </summary>
    private void DismissActiveNotice()
    {
        if (_modalDialog != null) _modalDialog.Close();

        HandleNoticeConfirmed();
    }

    /// <summary>
    /// 안내를 전부 취소한다. 사용자가 항복/타이틀 이동 같은 다른 선택을 하러 갈 때 —
    /// 지나간 알림이 그 위에 겹쳐 뜨지 않게 한다.
    /// </summary>
    private void ClearSurrenderNotices()
    {
        if (_activeModal != ModalContent.None && _modalDialog != null)
            _modalDialog.Close();

        _activeModal = ModalContent.None;
        _surrenderNoticeShown = false;
        _surrenderCrossCancelledNoticeShown = false;
    }

    // ─────────────────────────────────────────
    // 파트너 이탈 (uGUI 공용 모달)
    // ─────────────────────────────────────────

    /// <summary>
    /// 파트너 이탈 관련 모달 3종을 상태에 따라 띄운다. 하나라도 잡고 있으면 true —
    /// 그러면 <see cref="Update"/>가 항복 쪽 폴링을 건너뛴다.
    /// <para>
    /// 셋 다 <b>ESC로 닫을 수 없고</b> 뒤쪽 입력도 막혀야 한다. 예전에는 BlockAllInput()이
    /// EventSystem을 통째로 꺼서 막았지만, 지금은 ModalDialog_Pf의 Dim(Raycast Target 켜진
    /// 전체화면 Image)이 그 역할을 한다 — uGUI 모달에서 EventSystem을 끄면 모달 자신의 버튼까지
    /// 죽으므로 애초에 쓸 수 없는 방식이다. ESC는 <see cref="IsForcedModal"/>이 막는다.
    /// </para>
    /// </summary>
    private bool PollPartnerDisconnectModals()
    {
        if (_modalDialog == null) return false;

        NetworkManager network =
            GameManager.TryGet(out var gameManager) ? gameManager.Network : null;

        // ① 재접속 포기 통지가 최우선. 대기/종료확인 상태를 정리하고 이것만 띄운다.
        //    전투/타이머 재개 이벤트는 발행하지 않는다(재개하면 안 되므로).
        if (network != null && network.PartnerGaveUpReconnect)
        {
            _partnerDisconnectModalOpen = false;
            _partnerGiveUpAvailable = false;
            _partnerDisconnectEndConfirmOpen = false;

            if (_activeModal != ModalContent.PartnerGaveUp)
            {
                _activeModal = ModalContent.PartnerGaveUp;
                _modalDialog.ShowNotice("상대방이 재접속을 포기했습니다.", "확인", HandlePartnerGaveUpConfirmed);
            }

            return true;
        }

        // ② [포기하기] 이후 종료 확인. 닫기/취소 없이 반드시 둘 중 하나를 골라야 한다.
        if (_partnerDisconnectEndConfirmOpen)
        {
            if (_activeModal != ModalContent.PartnerDisconnectEndConfirm)
            {
                _activeModal = ModalContent.PartnerDisconnectEndConfirm;

                _modalDialog.ShowChoice(
                    "게임을 종료하시겠습니까?",
                    "타이틀로 이동", () => HandlePartnerDisconnectGiveUp(returnToTitle: true),
                    "게임 종료", () => HandlePartnerDisconnectGiveUp(returnToTitle: false));
            }

            return true;
        }

        // ③ 재접속 대기. 유예가 끝나기 전까지는 고를 게 없으므로 버튼 없는 대기 상태다.
        if (_partnerDisconnectModalOpen)
        {
            if (_activeModal != ModalContent.PartnerDisconnectWait)
            {
                _activeModal = ModalContent.PartnerDisconnectWait;
                _partnerGiveUpButtonShown = false;

                _modalDialog.ShowWaiting("팀원이 연결 끊김\n재접속을 기다리는 중입니다...");
            }

            // 유예가 끝나면 [포기하기]가 뒤늦게 붙는다(문구는 그대로 둔다).
            if (_partnerGiveUpAvailable && !_partnerGiveUpButtonShown)
            {
                _partnerGiveUpButtonShown = true;
                _modalDialog.SetPrimaryAction("포기하기", HandlePartnerGiveUpClicked);
            }

            return true;
        }

        // 이탈 상태가 풀렸는데(파트너 재접속) 창이 남아 있으면 내린다.
        if (IsPartnerDisconnectModal(_activeModal))
        {
            _modalDialog.Close();
            _activeModal = ModalContent.None;
        }

        return false;
    }

    /// <summary>대기 모달의 [포기하기]. 다음 단계인 종료 확인으로 넘어간다.</summary>
    private void HandlePartnerGiveUpClicked()
    {
        _activeModal = ModalContent.None;
        _partnerDisconnectEndConfirmOpen = true;

        // 곧바로 다시 폴링한다 — Update를 기다리면 한 프레임 동안 모달도 Dim도 사라져 화면이 깜빡인다.
        PollPartnerDisconnectModals();
    }

    /// <summary>
    /// 종료 확인의 두 선택지. 패배 기록(SessionEnded)은 여기서 실제로 고른 시점에만 발행된다.
    /// "타이틀로"는 완전히 끝난 매치이므로 ReturnToTitleAfterMatchEnd(완료 매치 전용 경로 — 저장된
    /// 재접속 세션까지 정리)를 탄다(2026-08 코드리뷰 대응). 일반 ConfirmReturnToTitle을 쓰면 재접속
    /// 세션이 남아 타이틀의 [이전 게임으로 들어가기]에 이미 끝난 매치가 잘못 노출된다.
    /// 이 모달은 "진짜 이탈"과 "파트너 응답 불능" 양쪽에서 공유하는데, 사유는
    /// gameManager.Network.ConfirmPartnerDisconnectGiveUp() 내부에서 결정되는 것과 반드시 같아야
    /// 한다 — 여기서 따로 PartnerAbandoned로 고정하면 전적 기록과 재접속 세션 정리 로그가 서로 다른
    /// 사유를 가리키게 된다(2026-08-22 코드리뷰 지적).
    /// </summary>
    private void HandlePartnerDisconnectGiveUp(bool returnToTitle)
    {
        _activeModal = ModalContent.None;
        _partnerDisconnectEndConfirmOpen = false;
        _partnerDisconnectModalOpen = false;
        _partnerGiveUpAvailable = false;

        bool isResultUnresponsive = false;
        if (GameManager.TryGet(out var gameManager) && gameManager.Network != null)
        {
            isResultUnresponsive = gameManager.Network.IsPartnerResultUnresponsive;
            gameManager.Network.ConfirmPartnerDisconnectGiveUp();
        }

        SessionEndReason reason = isResultUnresponsive
            ? SessionEndReason.PartnerResultUnresponsive
            : SessionEndReason.PartnerAbandoned;

        if (returnToTitle) ReturnToTitleAfterMatchEnd(reason);
        else QuitGame();
    }

    /// <summary>
    /// 재접속 포기 통지의 [확인]. 기존 패배 처리 흐름을 그대로 탄다 —
    /// SessionEnded 발행(전적 저장) → RequestCompletedMatchReturnToTitle(저장된 재접속 세션 정리 포함)
    /// → LeaveRoom 완료 → OnLeftRoom에서 씬 전환(2026-08 코드리뷰 대응 — 기존 ConfirmReturnToTitle은
    /// 세션을 지우지 않아 이미 끝난 매치가 [이전 게임으로 들어가기]에 남는 문제가 있었다).
    /// </summary>
    private void HandlePartnerGaveUpConfirmed()
    {
        _activeModal = ModalContent.None;

        if (!GameManager.TryGet(out var gameManager) || gameManager.Network == null)
            return;

        gameManager.Network.AcknowledgePartnerGaveUpReconnect();
        gameManager.Network.ConfirmPartnerDisconnectGiveUp();
        ReturnToTitleAfterMatchEnd(SessionEndReason.PartnerAbandoned);
    }

    private static bool IsPartnerDisconnectModal(ModalContent content) =>
        content == ModalContent.PartnerDisconnectWait ||
        content == ModalContent.PartnerDisconnectEndConfirm ||
        content == ModalContent.PartnerGaveUp;

    /// <summary>ESC로 넘길 수 없는 모달인가 — 반드시 답해야 하거나, 애초에 고를 게 없는 대기 상태.</summary>
    private static bool IsForcedModal(ModalContent content) =>
        content == ModalContent.IncomingSurrenderRequest ||
        content == ModalContent.Defeat ||
        content == ModalContent.Victory ||
        IsPartnerDisconnectModal(content);

    private void HandleEscapeKey()
    {
        Event currentEvent = Event.current;

        if (currentEvent.type != EventType.KeyDown ||
            currentEvent.keyCode != KeyCode.Escape)
        {
            return;
        }

        if (IsForcedModal(_activeModal))
        {
            // 반드시 답해야 하는 모달(파트너 요청·이탈 관련)은 ESC로 넘길 수 없다.
            // 옵션창 토글로도 새어나가지 않게 여기서 이벤트를 삼킨다 — IMGUI 시절엔 OnGUI 최상단의
            // early-return이 HandleEscapeKey 호출 자체를 막아 우연히 지켜지던 규칙이다.
        }
        else if (_activeModal != ModalContent.None)
        {
            // 떠 있는 안내를 ESC로 닫는다. 대기 중인 다른 안내가 남아 있으면 다음 프레임에 이어서 뜬다.
            DismissActiveNotice();
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

    /// <summary>
    /// 설정 버튼/ESC로 옵션 패널을 연다. 현재 저장된 값을 스냅샷으로 남기고 uGUI에 반영한다.
    /// public인 이유: 타이틀 씬의 SettingsButton(TitleScreenUI)이 다른 클래스에서 직접 호출한다.
    /// </summary>
    public void OpenOptionsPanel()
    {
        if (_optionsOpen) return;
        _optionsOpen = true;

        if (SoundManager.TryGet(out var sm)) sm.PlaySfx(SoundId.UiClick);

        _pendingFullScreen = _appliedFullScreen;
        SyncPendingResolutionToApplied();

        // 열 때는 항상 첫 탭부터 — 지난번 닫을 때 보던 탭이 남지 않게 한다.
        SelectTab(0);

        RefreshVolumeUI();
        RefreshScreenUI();

        // 아직 아무것도 안 바꿨으니 [확인]은 꺼진 채로 열린다.
        RefreshApplyButtonInteractable();

        SetOptionsPanelVisible(true);
    }

    /// <summary>
    /// Close 버튼 / Cancel 버튼 / ESC가 모두 이 메서드 하나로 모인다.
    /// 화면 설정 임시값과 볼륨을 마지막으로 저장된 값으로 되돌린 뒤 패널을 닫는다.
    /// [확인]도 이 메서드로 닫지만, 그쪽은 되돌리기 직전에 기준선을 갱신해 두므로 여기서 아무것도
    /// 잃지 않는다(HandleApplyButtonClicked 참고).
    /// </summary>
    private void CloseOptionsPanel()
    {
        if (!_optionsOpen) return;
        _optionsOpen = false;

        _pendingFullScreen = _appliedFullScreen;
        SyncPendingResolutionToApplied();

        _masterVolume = _appliedMasterVolume;
        _bgmVolume = _appliedBgmVolume;
        _sfxVolume = _appliedSfxVolume;
        ApplyMasterVolume(_masterVolume);
        ApplyBgmVolume(_bgmVolume);
        ApplySfxVolume(_sfxVolume);

        RefreshVolumeUI();
        RefreshScreenUI();

        CloseConfirmModal();
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
    // 변경 감지 — [확인] 버튼 활성화
    // ─────────────────────────────────────────

    /// <summary>
    /// 아직 저장되지 않은 변경이 하나라도 있는가. 기준선은 "마지막으로 [확인]까지 눌러 저장된 값"이다.
    /// <para>
    /// 볼륨은 슬라이더를 움직이는 즉시 들리지만(라이브 프리뷰) 저장은 [확인] 시점이라, 소리가 바뀐
    /// 것과 저장된 것은 별개다 — 그래서 편집값(_masterVolume)과 저장값(_appliedMasterVolume)을
    /// 따로 들고 비교한다. 값을 바꿨다가 원래대로 되돌려놓으면 다시 false가 되어 버튼이 꺼진다.
    /// </para>
    /// </summary>
    private bool HasUnappliedChanges =>
        !Mathf.Approximately(_masterVolume, _appliedMasterVolume) ||
        !Mathf.Approximately(_bgmVolume, _appliedBgmVolume) ||
        !Mathf.Approximately(_sfxVolume, _appliedSfxVolume) ||
        _pendingFullScreen != _appliedFullScreen ||
        PendingResolution.width != _appliedResolution.width ||
        PendingResolution.height != _appliedResolution.height;

    /// <summary>
    /// 드롭다운에서 고르고 있는 해상도. 순번이 아니라 값으로 비교해야 하는 곳에서 쓴다 —
    /// 목록이 화면 모드에 따라 달라지므로 순번끼리 비교하면 모드를 바꿨다 되돌린 것만으로도
    /// "바뀐 것 없음"이 "바뀜"으로 잡힌다.
    /// </summary>
    private Resolution PendingResolution =>
        _resolutions == null || _resolutions.Length == 0
            ? default
            : _resolutions[Mathf.Clamp(_pendingResolutionIndex, 0, _resolutions.Length - 1)];

    /// <summary>값이 바뀔 수 있는 모든 경로(슬라이더·토글·드롭다운·기본값 복원) 끝에서 부른다.</summary>
    private void RefreshApplyButtonInteractable()
    {
        if (_applyButton == null) return;

        _applyButton.interactable = HasUnappliedChanges;
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

    // % 텍스트 갱신은 행이 자기 슬라이더를 구독해 알아서 한다 — 여기서는 채널 동작만 처리한다.

    private void HandleMasterVolumeChanged(float value)
    {
        _masterVolume = value;
        ApplyMasterVolume(value);
        RefreshApplyButtonInteractable();
    }

    private void HandleBgmVolumeChanged(float value)
    {
        _bgmVolume = value;
        ApplyBgmVolume(value);
        RefreshApplyButtonInteractable();
    }

    private void HandleSfxVolumeChanged(float value)
    {
        _sfxVolume = value;
        ApplySfxVolume(value);
        RefreshApplyButtonInteractable();
    }

    /// <summary>
    /// 저장된 값을 슬라이더에 다시 밀어넣는다(패널 열기·취소로 되돌리기).
    /// 콜백을 타지 않으므로 여기서 Apply*Volume이 다시 불리지는 않는다.
    /// </summary>
    private void RefreshVolumeUI()
    {
        SetVolumeRowSilently(_masterRow, _masterVolume);
        SetVolumeRowSilently(_bgmRow, _bgmVolume);
        SetVolumeRowSilently(_sfxRow, _sfxVolume);
    }

    private static void SetVolumeRowSilently(VolumeRowUI row, float value)
    {
        if (row == null) return;

        row.SetValueWithoutNotify(value);
    }

    // ─────────────────────────────────────────
    // 화면 모드 / 해상도 (uGUI)
    // ─────────────────────────────────────────

    private void HandleWindowedToggleChanged(bool isOn)
    {
        // ToggleGroup이 "최소 1개 선택" 상호 배타를 보장하므로 켜지는 이벤트만 반영한다.
        if (!isOn) return;
        _pendingFullScreen = false;
        HandlePendingScreenModeChanged();
    }

    private void HandleFullscreenToggleChanged(bool isOn)
    {
        if (!isOn) return;
        _pendingFullScreen = true;
        HandlePendingScreenModeChanged();
    }

    /// <summary>
    /// 창/전체화면 토글 뒤처리. 모드가 바뀌면 <b>고를 수 있는 해상도 목록 자체가 달라지므로</b>
    /// (창 모드는 모니터와 같은 크기를 뺀다) 목록을 다시 만들고 드롭다운을 새로 그린다.
    /// RefreshScreenUI는 토글을 SetIsOnWithoutNotify로 되쓰므로 여기서 다시 불려도 재귀하지 않는다.
    /// </summary>
    private void HandlePendingScreenModeChanged()
    {
        RebuildResolutionListForPendingMode();
        RefreshScreenUI();
        RefreshApplyButtonInteractable();
    }

    private void HandleResolutionDropdownChanged(int index)
    {
        _pendingResolutionIndex = index;
        RefreshApplyButtonInteractable();
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
    /// 지원 해상도 목록으로 Dropdown 옵션을 다시 채운다(ClearOptions → AddOptions).
    /// 목록을 만드는 규칙은 BuildResolutionList 한 곳에만 둔다.
    /// </summary>
    private void RefreshResolutionDropdownOptions()
    {
        if (_resolutionDropdown == null) return;
        if (_resolutionLabels == null) BuildResolutionList(windowed: !_pendingFullScreen);

        _resolutionDropdown.ClearOptions();
        _resolutionDropdown.AddOptions(new List<string>(_resolutionLabels));
    }

    private void ApplyScreenSettings()
    {
        _appliedFullScreen = _pendingFullScreen;
        _appliedResolution = PendingResolution;

        ApplyScreenMode();
        ApplyResolution();

        PlayerPrefs.SetInt(PREF_FULLSCREEN, _appliedFullScreen ? 1 : 0);
        PlayerPrefs.SetInt(PREF_RESOLUTION_WIDTH,  _appliedResolution.width);
        PlayerPrefs.SetInt(PREF_RESOLUTION_HEIGHT, _appliedResolution.height);
        PlayerPrefs.Save();
    }

    // ─────────────────────────────────────────
    // 버튼 (uGUI)
    // ─────────────────────────────────────────

    /// <summary>
    /// 확인 — 볼륨/화면 설정을 저장·적용하고 패널을 닫는다.
    /// 바꾼 값이 없으면 버튼 자체가 비활성이라 여기까지 오지 않는다.
    /// </summary>
    private void HandleApplyButtonClicked()
    {
        if (SoundManager.TryGet(out var sm)) sm.PlaySfx(SoundId.SettingApply);

        PlayerPrefs.SetFloat(PREF_MASTER_VOLUME, _masterVolume);
        PlayerPrefs.SetFloat(PREF_BGM_VOLUME, _bgmVolume);
        PlayerPrefs.SetFloat(PREF_SFX_VOLUME, _sfxVolume);

        ApplyScreenSettings();

        PlayerPrefs.Save();

        // ⚠️ 반드시 CloseOptionsPanel()보다 먼저. 닫기는 볼륨을 _applied*Volume으로 되돌리는데,
        // 여기서 기준선을 갱신하지 않으면 방금 저장한 값이 곧바로 이전 값으로 덮여 사라진다.
        _appliedMasterVolume = _masterVolume;
        _appliedBgmVolume = _bgmVolume;
        _appliedSfxVolume = _sfxVolume;

        // 저장했으면 창은 닫는다 — [닫기]를 따로 누를 필요가 없다.
        CloseOptionsPanel();
    }

    /// <summary>
    /// 기본값 복원 — <b>지금 보고 있는 페이지의 값만</b> 게임 기본값으로 되돌린다.
    /// <para>
    /// PlayerPrefs에는 쓰지 않는다. 볼륨은 즉시 들려주고(라이브 프리뷰) 화면 설정은 pending에만
    /// 넣으므로, 실수로 눌렀더라도 [취소]를 누르면 원래대로 돌아간다 — 그래서 확인 모달이 없다.
    /// </para>
    /// </summary>
    private void HandleRestoreDefaultsButtonClicked()
    {
        if (_tabs == null || _tabs.Length == 0) return;

        if (SoundManager.TryGet(out var sm)) sm.PlaySfx(SoundId.UiClick);

        RestorePageDefaults(_tabs[CurrentTabIndex].kind);

        RefreshVolumeUI();
        RefreshScreenUI();
        RefreshApplyButtonInteractable();
    }

    /// <summary>페이지를 추가하면 여기에 분기를 하나 더 만든다.</summary>
    private void RestorePageDefaults(OptionsPage page)
    {
        switch (page)
        {
            case OptionsPage.Screen:
                // 모드부터 되돌린 뒤 목록을 다시 만든다 — 목록이 모드에 따라 달라지므로,
                // 순서를 바꾸면 이전 모드 기준 목록에서 기본값을 고르게 된다.
                _pendingFullScreen = DEFAULT_FULLSCREEN;
                BuildResolutionList(windowed: !_pendingFullScreen);
                _pendingResolutionIndex = FindDefaultResolutionIndex();
                break;

            case OptionsPage.Sound:
                _masterVolume = DEFAULT_MASTER_VOLUME;
                _bgmVolume = DEFAULT_SUB_VOLUME;
                _sfxVolume = DEFAULT_SUB_VOLUME;
                ApplyMasterVolume(_masterVolume);
                ApplyBgmVolume(_bgmVolume);
                ApplySfxVolume(_sfxVolume);
                break;
        }
    }

    /// <summary>
    /// 기본 해상도 = 목록 중 <b>모니터 종횡비에 가장 가까운 것</b>, 같은 비율이 여럿이면 그중 가장 큰 것.
    /// [기본값 복원]의 기준이자, 저장된 설정이 없거나 그 해상도가 목록에서 빠졌을 때의 폴백이다
    /// (<see cref="ResolveStartupResolution"/>).
    /// <para>
    /// <b>단순히 "가장 큰 것"을 고르면 안 된다</b> — 목록의 마지막은 1920x1200(16:10)이라, 16:9
    /// 모니터를 쓰는 사람이 처음 게임을 켜거나 [기본값 복원]을 누르면 16:10으로 렌더된다. 그러면
    /// CameraLetterbox가 위아래에 검은 띠를 붙여, 이 프로젝트가 없애려던 그림이 기본값이 된다.
    /// 비율을 맞춰 주면 16:9 모니터는 1920x1080, 16:10 모니터는 1920x1200을 받아 띠가 없어진다.
    /// </para>
    /// <para>
    /// Screen.currentResolution을 쓰면 안 된다 — Awake의 LoadAndApplyDisplaySettings()가 저장된
    /// 해상도를 이미 적용한 뒤라, 그 시점에 읽으면 "기본값"이 아니라 "직전에 쓰던 값"이 나온다.
    /// 그래서 비율은 <see cref="GetDisplayAspect"/>(모니터 모드 목록 기준)에서 가져온다.
    /// </para>
    /// </summary>
    private int FindDefaultResolutionIndex()
    {
        if (_resolutions == null || _resolutions.Length == 0) return 0;

        float displayAspect = GetDisplayAspect();

        int bestIndex = 0;
        float bestDelta = float.MaxValue;

        for (int i = 0; i < _resolutions.Length; i++)
        {
            float aspect = (float)_resolutions[i].width / _resolutions[i].height;
            float delta = Mathf.Abs(aspect - displayAspect);

            // 목록이 작은 것부터라, 비율이 같으면 나중에 오는(=더 큰) 쪽이 이기도록 <= 로 둔다.
            // 여유(ASPECT_EPSILON)를 두는 이유는 1280x720·1600x900·1920x1080이 같은 16:9인데도
            // 부동소수 나눗셈 결과가 마지막 자리에서 갈릴 수 있어서다. 16:9와 16:10의 차이는
            // 0.178이라 이 정도 여유로 두 비율이 섞일 일은 없다.
            if (delta <= bestDelta + ASPECT_EPSILON)
            {
                bestDelta = Mathf.Min(bestDelta, delta);
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    /// <summary>
    /// 모니터의 종횡비. <b>모드 목록에서 가장 큰 모드(=네이티브 해상도)</b>의 비율로 본다 —
    /// 모드 목록은 모니터가 정하는 값이라 우리가 SetResolution으로 무엇을 넣었든 영향받지 않는다.
    /// <para>
    /// 폭의 최대값과 높이의 최대값을 따로 뽑지 않는 이유는 <see cref="FitsDisplay"/>와 같다 —
    /// 서로 다른 모드에서 나온 값이 합쳐지면 실제로는 없는 비율이 만들어진다. 픽셀 수가 가장 큰
    /// 모드 하나를 통째로 골라 그 폭과 높이를 함께 쓴다.
    /// </para>
    /// </summary>
    private static float GetDisplayAspect()
    {
        Resolution[] modes = Screen.resolutions;

        int bestWidth = 0;
        int bestHeight = 0;
        long bestPixels = 0;

        for (int i = 0; i < modes.Length; i++)
        {
            long pixels = (long)modes[i].width * modes[i].height;
            if (pixels <= bestPixels) continue;

            bestPixels = pixels;
            bestWidth = modes[i].width;
            bestHeight = modes[i].height;
        }

        // 모드 목록이 비는 환경(일부 에디터·헤드리스)에서는 현재 해상도밖에 기댈 것이 없다.
        if (bestWidth <= 0 || bestHeight <= 0)
        {
            bestWidth = Screen.currentResolution.width;
            bestHeight = Screen.currentResolution.height;
        }

        return bestHeight > 0 ? (float)bestWidth / bestHeight : DEFAULT_ASPECT;
    }

    /// <summary>옵션 취소 — Close/Cancel/ESC가 공유하는 CloseOptionsPanel()을 그대로 호출한다.</summary>
    private void HandleCancelOptionsButtonClicked()
    {
        if (SoundManager.TryGet(out var sm)) sm.PlaySfx(SoundId.UiClick);
        CloseOptionsPanel();
    }

    /// <summary>닫기 — Close/Cancel/ESC가 공유하는 CloseOptionsPanel()을 그대로 호출한다.</summary>
    private void HandleCloseButtonClicked()
    {
        if (SoundManager.TryGet(out var sm)) sm.PlaySfx(SoundId.UiClick);
        CloseOptionsPanel();
    }

    /// <summary>
    /// 방 인원에 따라 역할이 갈리는 겸용 버튼.
    /// <list type="bullet">
    /// <item>솔로/1인 방 — 항복이 성립하지 않으므로 <b>타이틀로 이동</b>.</item>
    /// <item>2인 방 — 혼자 나가면 파트너가 남으므로 <b>항복 요청</b>(파트너 동의가 있어야 종료).</item>
    /// </list>
    /// 버튼에 적힌 문구는 바꾸지 않는다 — 갈리는 것은 확인 팝업(ConfirmModal)의 안내 문구뿐이다.
    /// </summary>
    private void HandleReturnTitleButtonClicked()
    {
        if (SoundManager.TryGet(out var sm)) sm.PlaySfx(SoundId.UiClick);

        ClearSurrenderNotices();

        if (IsSurrenderAvailable())
        {
            OpenConfirmModal(ConfirmMode.Surrender, "파트너에게 항복을 요청하시겠습니까?");
            return;
        }

        OpenConfirmModal(ConfirmMode.ReturnToTitle, "타이틀 화면으로 이동하시겠습니까?");
    }

    /// <summary>
    /// 항복이 성립하는 상황인가(= 2인 방인가).
    /// PlayerCount는 솔로 모드에서 1을 반환하므로 이 한 줄로 솔로까지 걸러진다.
    /// <see cref="NetworkManager.RequestSurrender"/>의 자체 가드와 같은 조건이라, 여기서 통과한
    /// 요청이 네트워크 쪽에서 조용히 무시되는 일은 없다.
    /// </summary>
    private static bool IsSurrenderAvailable()
    {
        if (!GameManager.TryGet(out var gameManager) || gameManager.Network == null)
            return false;

        return gameManager.Network.IsInRoom && gameManager.Network.PlayerCount >= 2;
    }

    private void HandleQuitButtonClicked()
    {
        if (SoundManager.TryGet(out var sm)) sm.PlaySfx(SoundId.UiClick);

        ClearSurrenderNotices();
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
        if (SoundManager.TryGet(out var sm)) sm.PlaySfx(SoundId.UiClick);

        ConfirmMode selectedMode = _confirmMode;
        CloseConfirmModal();

        if (selectedMode == ConfirmMode.ReturnToTitle)
        {
            ConfirmReturnToTitle();
        }
        else if (selectedMode == ConfirmMode.QuitGame)
        {
            QuitGame();
        }
        else if (selectedMode == ConfirmMode.Surrender)
        {
            ConfirmRequestSurrender();

            // 요청을 보냈으면 파트너의 응답을 기다려야 하므로 옵션창은 닫아둔다.
            CloseOptionsPanel();
        }
    }

    /// <summary>ConfirmModal의 취소 버튼. 아무 로직도 실행하지 않고 모달만 닫는다.</summary>
    private void HandleCancelButtonClicked()
    {
        if (SoundManager.TryGet(out var sm)) sm.PlaySfx(SoundId.UiClick);
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

    // ── 지원 해상도 목록(기획 확정 2026-08-19) ──────────────────────────────
    // Screen.resolutions를 그대로 쓰지 않고 아래 고정 목록에서 고른다.
    //
    // 왜 고정 목록인가 — Screen.resolutions는 모니터가 보고하는 모드를 전부 준다(800x600·
    // 1024x768·1280x1024 같은 4:3/5:4 레거시 포함). CanvasScaler Reference가 1920x1080(16:9)이라
    // 다른 비율에서는 논리 캔버스 크기가 달라져 UI가 어긋나고, 목록이 길수록 고르기도 나쁘다.
    // 실제로 쓸 몇 개만 노출하는 편이 확실하다.
    //
    // 모니터가 보고하지 않는 값을 넣어도 되는 이유 — 전체화면이 FullScreenWindow(테두리 없는 창)라
    // 여기 값은 "모니터 모드"가 아니라 "렌더 해상도"로 쓰인다. 다만 모니터보다 큰 값은 창 모드에서
    // 화면 밖으로 넘치므로 BuildResolutionList가 걸러낸다.
    //
    // 16:10을 함께 두는 이유 — 16:10 모니터에서 16:9만 고를 수 있으면, 유니티가 16:9 렌더 결과를
    // 16:10 화면에 맞춰 늘리는 경로(FullScreenWindow의 업스케일)를 타게 된다. 네이티브 비율을
    // 고를 수 있으면 검은 띠를 CameraLetterbox가 직접 그리므로 늘어날 여지가 없다.
    //
    // 정렬은 작은 것부터 — 드롭다운에 이 순서대로 나가고, FindDefaultResolutionIndex가 "같은 비율이
    // 여럿이면 나중에 오는 것이 더 크다"는 이 정렬에 기대어 동률을 가른다. 항목을 추가할 때는 크기
    // 순서를 지킬 것.
    private static readonly Vector2Int[] SupportedResolutions =
    {
        new(1280,  720),   // 16:9   HD
        new(1280,  800),   // 16:10
        new(1600,  900),   // 16:9
        new(1680, 1050),   // 16:10
        new(1920, 1080),   // 16:9   FHD
        new(1920, 1200),   // 16:10
    };

    /// <summary>
    /// 드롭다운 목록을 만든다. <see cref="SupportedResolutions"/> 중 <b>모니터에 들어가는 것</b>만 남긴다.
    /// 전부 걸러지면(목록 최소보다 작은 화면) 가장 작은 것 하나는 남긴다 — 드롭다운이 비면
    /// 아무것도 고를 수 없다.
    /// <para>
    /// <paramref name="windowed"/>가 켜지면 모니터와 <b>같은 크기</b>도 뺀다. 창은 그림 영역 바깥에
    /// 타이틀바와 테두리가 더 붙고 작업표시줄도 자리를 차지하므로, 모니터와 같은 크기를 요청하면
    /// 창이 화면에 들어가지 못한다. 그러면 OS/유니티가 창을 줄여버려 드롭다운 표시와 실제 크기가
    /// 어긋난 채로 남는다([확인] 버튼도 이미 꺼져 있어 되돌릴 방법이 없다).
    /// </para>
    /// <para>
    /// 목록이 모드에 따라 달라지므로 <b>순번은 모드가 바뀌는 순간 의미를 잃는다</b>.
    /// 목록을 다시 만드는 곳은 반드시 <see cref="RebuildResolutionListForPendingMode"/>나
    /// <see cref="SyncPendingResolutionToApplied"/>를 거쳐 순번을 값 기준으로 다시 맞출 것.
    /// </para>
    /// </summary>
    private void BuildResolutionList(bool windowed)
    {
        var picked = new List<Vector2Int>();
        foreach (Vector2Int candidate in SupportedResolutions)
        {
            if (FitsDisplay(candidate, windowed)) picked.Add(candidate);
        }

        if (picked.Count == 0) picked.Add(SupportedResolutions[0]);

        _resolutions = new Resolution[picked.Count];
        _resolutionLabels = new string[picked.Count];

        for (int i = 0; i < picked.Count; i++)
        {
            RefreshRate refreshRate = ResolveRefreshRate(picked[i].x, picked[i].y);

            _resolutions[i] = new Resolution
            {
                width = picked[i].x,
                height = picked[i].y,
                refreshRateRatio = refreshRate
            };

            _resolutionLabels[i] = $"{picked[i].x}x{picked[i].y} @{refreshRate.value:0}Hz";
        }
    }

    /// <summary>
    /// 이 후보를 담을 수 있는 디스플레이 모드가 <b>하나라도</b> 있는가.
    /// <para>
    /// 폭의 최대값과 높이의 최대값을 따로 구해 비교하면 안 된다 — 두 값이 서로 다른 모드에서 나올 수
    /// 있기 때문이다. 1920x1080과 1600x1200을 함께 보고하는 모니터에서 그렇게 하면 최대 폭 1920과
    /// 최대 높이 1200이 합쳐져 "1920x1200"이라는, 이 모니터에 실제로는 없는 크기가 상한이 된다.
    /// 모드 하나하나와 대보면 그런 조합이 만들어지지 않는다.
    /// </para>
    /// <para>
    /// Screen.currentResolution을 기준으로 쓰지 않는 이유 — 우리가 SetResolution으로 바꿔 놓은
    /// 값일 수 있다. 다만 모드 목록이 비는 환경(일부 에디터·헤드리스)에서는 그것밖에 없다.
    /// </para>
    /// </summary>
    private static bool FitsDisplay(Vector2Int candidate, bool windowed)
    {
        Resolution[] modes = Screen.resolutions;

        if (modes.Length == 0)
        {
            return FitsMode(candidate,
                Screen.currentResolution.width, Screen.currentResolution.height, windowed);
        }

        for (int i = 0; i < modes.Length; i++)
        {
            if (FitsMode(candidate, modes[i].width, modes[i].height, windowed)) return true;
        }

        return false;
    }

    /// <summary>
    /// 모드 하나에 후보가 들어가는가. 창 모드에서는 <b>같은 크기를 허용하지 않는다</b>(&lt; 비교) —
    /// 창은 그림 영역 바깥에 테두리가 더 붙기 때문이다.
    /// <para>
    /// 이 판정은 테두리·타이틀바·작업표시줄의 실제 두께까지 계산하지는 않는다(유니티에 작업 영역을
    /// 알려주는 크로스플랫폼 API가 없다). 그래서 모니터보다 조금 작은 정도의 크기는 여전히 OS가
    /// 창을 줄일 수 있다. 여유분을 빼려면 여기에 상수를 더할 것.
    /// </para>
    /// </summary>
    private static bool FitsMode(Vector2Int candidate, int modeWidth, int modeHeight, bool windowed)
        => windowed
            ? candidate.x <  modeWidth && candidate.y <  modeHeight
            : candidate.x <= modeWidth && candidate.y <= modeHeight;

    /// <summary>
    /// 고정 목록의 해상도에 붙일 주사율. 모니터가 그 해상도를 모드로 보고하면 그중 가장 높은 것을,
    /// 보고하지 않으면(FullScreenWindow라 모드가 없어도 되므로 정상 상황) 현재 주사율을 쓴다.
    /// </summary>
    private static RefreshRate ResolveRefreshRate(int width, int height)
    {
        RefreshRate best = default;
        bool found = false;

        Resolution[] modes = Screen.resolutions;
        for (int i = 0; i < modes.Length; i++)
        {
            if (modes[i].width != width || modes[i].height != height) continue;
            if (found && modes[i].refreshRateRatio.value <= best.value) continue;

            best = modes[i].refreshRateRatio;
            found = true;
        }

        return found ? best : Screen.currentResolution.refreshRateRatio;
    }

    /// <summary>
    /// 시작할 때 쓸 해상도. 저장된 값을 <b>순번이 아니라 해상도(width/height)</b>로 읽는다 —
    /// 순번으로 저장하면 지원 목록을 손볼 때 같은 순번이 다른 해상도를 가리켜 설정이 조용히 바뀐다.
    /// 저장된 값이 없거나(첫 실행) 그 해상도가 목록에서 빠졌으면 FindDefaultResolutionIndex로
    /// 떨어진다 — 목록 안의 값이므로 드롭다운 표시와 실제 해상도가 어긋난 채 시작하지 않는다.
    /// </summary>
    private Resolution ResolveStartupResolution()
    {
        int savedWidth  = PlayerPrefs.GetInt(PREF_RESOLUTION_WIDTH,  0);
        int savedHeight = PlayerPrefs.GetInt(PREF_RESOLUTION_HEIGHT, 0);

        if (savedWidth > 0 && savedHeight > 0)
        {
            int savedIndex = FindResolutionIndex(savedWidth, savedHeight);
            if (savedIndex >= 0) return _resolutions[savedIndex];
        }

        return _resolutions[FindDefaultResolutionIndex()];
    }

    /// <summary>
    /// 편집 중인 화면 모드(<see cref="_pendingFullScreen"/>) 기준으로 목록을 다시 만들고,
    /// <b>고르고 있던 해상도를 값으로 다시 찾아</b> 순번을 맞춘다. 창↔전체화면을 오갈 때 쓴다.
    /// 그 해상도가 새 목록에서 빠졌으면(창 모드로 바꿔 모니터와 같은 크기가 제외된 경우)
    /// 기본값으로 떨어진다 — 드롭다운이 목록에 없는 값을 가리킨 채 남지 않게.
    /// </summary>
    private void RebuildResolutionListForPendingMode()
    {
        Resolution previous = PendingResolution;

        BuildResolutionList(windowed: !_pendingFullScreen);

        int restored = FindResolutionIndex(previous.width, previous.height);
        _pendingResolutionIndex = restored >= 0 ? restored : FindDefaultResolutionIndex();
    }

    /// <summary>
    /// 편집값을 마지막으로 저장된 값으로 되돌린다(패널 열기·취소·시작 시).
    /// <b>호출 전에 <see cref="_pendingFullScreen"/>을 먼저 맞춰 둘 것</b> — 목록이 모드에 따라
    /// 달라지므로, 모드를 맞추지 않으면 엉뚱한 목록에서 순번을 찾게 된다.
    /// </summary>
    private void SyncPendingResolutionToApplied()
    {
        BuildResolutionList(windowed: !_pendingFullScreen);

        int index = FindResolutionIndex(_appliedResolution.width, _appliedResolution.height);
        _pendingResolutionIndex = index >= 0 ? index : FindDefaultResolutionIndex();
    }

    /// <summary>저장된 해상도가 목록의 몇 번째인지. 목록에 없으면 -1.</summary>
    private int FindResolutionIndex(int width, int height)
    {
        if (_resolutions == null) return -1;

        for (int i = 0; i < _resolutions.Length; i++)
        {
            if (_resolutions[i].width == width && _resolutions[i].height == height) return i;
        }

        return -1;
    }

    private void ApplyScreenMode()
    {
        Screen.fullScreenMode = _appliedFullScreen
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;
    }

    private void ApplyResolution()
    {
        Screen.SetResolution(
            _appliedResolution.width,
            _appliedResolution.height,
            _appliedFullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed,
            _appliedResolution.refreshRateRatio);
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
