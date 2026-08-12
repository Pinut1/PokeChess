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
    private const string PREF_RESOLUTION_INDEX = "ResolutionIndex";

    // ── 게임이 제공하는 기본값 ([기본값 복원]의 기준이자, 저장된 값이 없을 때의 초기값) ──
    // 저장값이 없을 때의 폴백도 반드시 이 상수를 쓴다. 폴백을 "씬에 찍혀 있던 값"으로 두면
    // 처음 켰을 때의 값과 [기본값 복원]을 누른 뒤의 값이 서로 달라진다.
    private const float DEFAULT_MASTER_VOLUME = 1f;
    private const float DEFAULT_SUB_VOLUME = 1f;
    private const bool DEFAULT_FULLSCREEN = true;

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
    private int _appliedResolutionIndex;

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
        Defeat
    }

    private ModalContent _activeModal = ModalContent.None;

    // 파트너 네트워크 이탈 대기. 항복 관련 무엇보다 우선한다(PollPartnerDisconnectModals).
    private bool _partnerDisconnectModalOpen;
    private bool _partnerGiveUpAvailable;
    private bool _partnerDisconnectEndConfirmOpen;

    // 대기 모달에 [포기하기]를 이미 붙였는지. 유예가 끝난 뒤 매 프레임 다시 붙이지 않으려고 둔다.
    private bool _partnerGiveUpButtonShown;

    // 패배(TeamHpZero) 확인 모달. 한 매치에 한 번만 뜨면 되므로(재오픈 방어) _defeatModalShown으로 막는다.
    private bool _defeatModalShown;
    private Coroutine _defeatModalRoutine;

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
        GameEvents.OnSessionEnded         += HandleSessionEnded;
    }

    private void OnDisable()
    {
        GameEvents.OnOpponentDisconnected -= HandlePartnerDisconnected;
        GameEvents.OnGracePeriodExpired   -= HandlePartnerGiveUpAvailable;
        GameEvents.OnOpponentReconnected  -= HandlePartnerReconnected;
        GameEvents.OnSessionEnded         -= HandleSessionEnded;

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
    /// <item>그 외(PartnerAbandoned/ReconnectFailed/BothDisconnected) — 기존 흐름
    /// (HandlePartnerDisconnectGiveUp/HandlePartnerGaveUpConfirmed)이 이미 처리하므로 여기서는
    /// 손대지 않는다.</item>
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
                ShowDefeatModal();
                break;
        }
    }

    /// <summary>
    /// 패배 확인 모달을 띄운다. 처음 3초는 버튼 없이 안내만(ShowWaiting) 보여주고, 3초 뒤
    /// SetPrimaryAction으로 확인 버튼을 뒤늦게 붙인다 — 패배 즉시 자동으로 타이틀로 이동하지
    /// 않기 위함(요구사항). 한 매치에 한 번만 뜨면 되므로 _defeatModalShown으로 재오픈을 막는다.
    /// </summary>
    private void ShowDefeatModal()
    {
        if (_defeatModalShown || _modalDialog == null) return;
        _defeatModalShown = true;

        _activeModal = ModalContent.Defeat;
        _modalDialog.ShowWaiting("패배했습니다.");

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

    /// <summary>패배 확인 모달의 [확인]. 정상 종료 타이틀 복귀 경로로 나간다.</summary>
    private void HandleDefeatConfirmClicked()
    {
        _activeModal = ModalContent.None;
        ReturnToTitleAfterMatchEnd(SessionEndReason.TeamHpZero);
    }

    /// <summary>
    /// 정상적으로 종료된 매치(항복 합의/패배 확인)에서 타이틀로 복귀한다. 일반 타이틀 이동
    /// (<see cref="ConfirmReturnToTitle"/>)과 씬 이름 검증·PlayerPrefs 저장 절차는 동일하되,
    /// <see cref="NetworkManager.RequestCompletedMatchReturnToTitle"/>를 통해 저장된 재접속
    /// 세션까지 함께 정리한다는 점만 다르다 — 이미 끝난 매치를 타이틀 화면의 [이전 게임으로
    /// 들어가기] 후보로 남기지 않기 위함.
    /// </summary>
    private void ReturnToTitleAfterMatchEnd(SessionEndReason reason)
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
            gameManager.Network.RequestCompletedMatchReturnToTitle(_titleSceneName, reason.ToString());
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
        // 패배(TeamHpZero) 확인 모달이 최우선 — 이미 팀 HP 0으로 매치가 끝난 뒤라 파트너 이탈/항복
        // 관련 모달이 그 위를 덮어써서는 안 된다. 오직 이 모달 자신의 확인 버튼으로만 내려간다.
        if (_activeModal == ModalContent.Defeat)
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
    /// 기존 "타이틀로"/"게임 종료" 흐름을 그대로 재사용한다.
    /// </summary>
    private void HandlePartnerDisconnectGiveUp(bool returnToTitle)
    {
        _activeModal = ModalContent.None;
        _partnerDisconnectEndConfirmOpen = false;
        _partnerDisconnectModalOpen = false;
        _partnerGiveUpAvailable = false;

        if (GameManager.TryGet(out var gameManager) && gameManager.Network != null)
            gameManager.Network.ConfirmPartnerDisconnectGiveUp();

        if (returnToTitle) ConfirmReturnToTitle();
        else QuitGame();
    }

    /// <summary>
    /// 재접속 포기 통지의 [확인]. 기존 패배 처리 흐름을 그대로 탄다 —
    /// SessionEnded 발행(전적 저장) → RequestReturnToTitle → LeaveRoom 완료 → OnLeftRoom에서 씬 전환.
    /// </summary>
    private void HandlePartnerGaveUpConfirmed()
    {
        _activeModal = ModalContent.None;

        if (!GameManager.TryGet(out var gameManager) || gameManager.Network == null)
            return;

        gameManager.Network.AcknowledgePartnerGaveUpReconnect();
        gameManager.Network.ConfirmPartnerDisconnectGiveUp();
        ConfirmReturnToTitle();
    }

    private static bool IsPartnerDisconnectModal(ModalContent content) =>
        content == ModalContent.PartnerDisconnectWait ||
        content == ModalContent.PartnerDisconnectEndConfirm ||
        content == ModalContent.PartnerGaveUp;

    /// <summary>ESC로 넘길 수 없는 모달인가 — 반드시 답해야 하거나, 애초에 고를 게 없는 대기 상태.</summary>
    private static bool IsForcedModal(ModalContent content) =>
        content == ModalContent.IncomingSurrenderRequest ||
        content == ModalContent.Defeat ||
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

    /// <summary>설정 버튼/ESC로 옵션 패널을 연다. 현재 저장된 값을 스냅샷으로 남기고 uGUI에 반영한다.</summary>
    private void OpenOptionsPanel()
    {
        if (_optionsOpen) return;
        _optionsOpen = true;

        if (SoundManager.TryGet(out var sm)) sm.PlaySfx(SoundId.UiClick);

        _pendingFullScreen = _appliedFullScreen;
        _pendingResolutionIndex = _appliedResolutionIndex;

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
        _pendingResolutionIndex = _appliedResolutionIndex;

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
        _pendingResolutionIndex != _appliedResolutionIndex;

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
        RefreshApplyButtonInteractable();
    }

    private void HandleFullscreenToggleChanged(bool isOn)
    {
        if (!isOn) return;
        _pendingFullScreen = true;
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
                _pendingFullScreen = DEFAULT_FULLSCREEN;
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
    /// 기본 해상도 = 지원 목록 중 가장 큰 것.
    /// <para>
    /// Screen.currentResolution을 쓰면 안 된다 — Awake의 LoadAndApplyDisplaySettings()가 저장된
    /// 해상도를 이미 적용한 뒤라, 그 시점에 읽으면 "기본값"이 아니라 "직전에 쓰던 값"이 나온다.
    /// _resolutions는 Screen.resolutions에서 만든 목록이라 모니터가 실제로 지원하는 값이 보장된다.
    /// </para>
    /// </summary>
    private int FindDefaultResolutionIndex()
    {
        if (_resolutions == null || _resolutions.Length == 0) return 0;

        int bestIndex = 0;
        long bestPixels = 0;

        for (int i = 0; i < _resolutions.Length; i++)
        {
            long pixels = (long)_resolutions[i].width * _resolutions[i].height;
            if (pixels <= bestPixels) continue;

            bestPixels = pixels;
            bestIndex = i;
        }

        return bestIndex;
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
