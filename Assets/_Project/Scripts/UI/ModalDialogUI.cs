using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 안내/선택 팝업 한 장. 문구와 버튼 문구·동작을 매번 바깥에서 받아 쓰는 <b>재사용 모달</b>이다.
///
/// 항복·파트너 이탈 관련 IMGUI 모달 6개가 실은 세 모양뿐이라(1버튼 안내 / 2버튼 선택 / 버튼 없는
/// 대기) 계층을 여섯 벌 만들지 않고 이 하나로 돌려쓴다.
///
/// 프리팹 구조(ModalDialog_Pf):
///   ModalDialog_Pf     ← 이 컴포넌트. 여닫기는 이 오브젝트의 SetActive
///     Dim              전체화면 Image, <b>Raycast Target 켤 것</b>
///     Popup            VerticalLayoutGroup + ContentSizeFitter 권장
///       MessageText                                  ← Message Text
///       ButtonRow      HorizontalLayoutGroup
///         PrimaryButton    확인 / 항복하기 / 타이틀로 이동 / 포기하기  ← Primary Button
///           Text (TMP)                                              ← Primary Label
///         SecondaryButton  계속하기 / 게임 종료                       ← Secondary Button
///           Text (TMP)                                              ← Secondary Label
///
/// <b>Dim이 입력 차단을 겸한다.</b> Raycast Target이 켜진 전체화면 Image가 뒤쪽 클릭을 전부
/// 흡수하므로, 예전 IMGUI 모달처럼 EventSystem을 꺼서 막을 필요가 없다 — 그 방식은 이 모달
/// 자신의 버튼까지 죽이기 때문에 애초에 쓸 수 없다.
///
/// <b>Canvas 직계에 둘 것.</b> OptionsPanel 밑에 두면 옵션창을 닫을 때 같이 꺼지는데,
/// 이 모달들은 옵션창과 무관하게 떠야 한다(파트너 이탈 통지 등).
///
/// ESC로 닫을지 말지는 이 컴포넌트가 정하지 않는다 — 강제 선택 모달(이탈 대기 등)이 있어서
/// 부르는 쪽이 상황을 보고 <see cref="Close"/>를 부를지 결정한다.
/// </summary>
public class ModalDialogUI : MonoBehaviour
{
    [Tooltip("안내 문구(MessageText).")]
    [SerializeField] private TMP_Text _messageText;

    [Tooltip("주 버튼. 확인·항복하기·타이틀로 이동·포기하기 자리.")]
    [SerializeField] private Button _primaryButton;
    [Tooltip("주 버튼의 Text (TMP). 문구를 매번 바꾸므로 연결할 것.")]
    [SerializeField] private TMP_Text _primaryLabel;

    [Tooltip("보조 버튼. 계속하기·게임 종료 자리. 1버튼 모달에서는 꺼진다.")]
    [SerializeField] private Button _secondaryButton;
    [Tooltip("보조 버튼의 Text (TMP).")]
    [SerializeField] private TMP_Text _secondaryLabel;

    private Action _onPrimary;
    private Action _onSecondary;

    private bool _initialized;

    // 표시 여부의 단일 기준. activeSelf를 쓰지 않는 이유는 Awake 처리(아래)에 있다.
    private bool _isShowing;

    /// <summary>지금 떠 있는가. 부르는 쪽이 우선순위를 판단할 때 쓴다.</summary>
    public bool IsOpen => _isShowing;

    private void Awake()
    {
        EnsureInitialized();

        // 씬에 켜둔 채 저장했으면 시작은 닫힌 상태로 맞춘다.
        // ⚠️ _isShowing 검사가 필수다 — 씬에 꺼둔 채 저장했다면 Awake는 지금이 아니라
        //    Show*가 SetActive(true)를 부르는 순간 처음 돌기 때문이다. 그때 조건 없이 닫아버리면
        //    방금 연 모달이 곧바로 다시 닫힌다.
        if (!_isShowing) gameObject.SetActive(false);
    }

    /// <summary>
    /// 버튼 리스너를 건다. 꺼진 채로 시작하면 Awake가 늦게 돌므로 <see cref="Open"/>에서도 부른다
    /// (VolumeRowUI.Initialize와 같은 이유 — 비활성 오브젝트는 Awake가 돌지 않는다).
    /// </summary>
    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        if (_primaryButton != null) _primaryButton.onClick.AddListener(HandlePrimaryClicked);
        if (_secondaryButton != null) _secondaryButton.onClick.AddListener(HandleSecondaryClicked);
    }

    // ─────────────────────────────────────────
    // 띄우기
    // ─────────────────────────────────────────

    /// <summary>1버튼 안내. 거절 안내·교차취소 안내·재접속 포기 통지에 쓴다.</summary>
    public void ShowNotice(string message, string okText, Action onOk = null)
    {
        Open(message);

        SetButton(_primaryButton, _primaryLabel, okText, true);
        SetButton(_secondaryButton, _secondaryLabel, null, false);

        _onPrimary = onOk;
        _onSecondary = null;
    }

    /// <summary>2버튼 선택. 항복 요청 수신·이탈 후 종료 확인에 쓴다.</summary>
    public void ShowChoice(
        string message,
        string primaryText, Action onPrimary,
        string secondaryText, Action onSecondary)
    {
        Open(message);

        SetButton(_primaryButton, _primaryLabel, primaryText, true);
        SetButton(_secondaryButton, _secondaryLabel, secondaryText, true);

        _onPrimary = onPrimary;
        _onSecondary = onSecondary;
    }

    /// <summary>
    /// 버튼 없는 대기 안내. 파트너 이탈 대기처럼 아직 고를 게 없는 상태에 쓴다.
    /// 나중에 선택지가 생기면(유예 만료 → [포기하기]) <see cref="SetPrimaryAction"/>으로 버튼을 붙인다.
    /// </summary>
    public void ShowWaiting(string message)
    {
        Open(message);

        SetButton(_primaryButton, _primaryLabel, null, false);
        SetButton(_secondaryButton, _secondaryLabel, null, false);

        _onPrimary = null;
        _onSecondary = null;
    }

    /// <summary>대기 중인 모달에 주 버튼을 뒤늦게 붙인다. 이미 떠 있는 문구는 건드리지 않는다.</summary>
    public void SetPrimaryAction(string label, Action onPrimary)
    {
        SetButton(_primaryButton, _primaryLabel, label, true);
        _onPrimary = onPrimary;
    }

    /// <summary>문구만 갈아끼운다(대기 상태에서 안내 문구가 바뀌는 경우).</summary>
    public void SetMessage(string message)
    {
        if (_messageText != null) _messageText.text = message;
    }

    /// <summary>닫는다. 걸려 있던 동작도 같이 버려서 다음에 뜰 때 옛 콜백이 남지 않게 한다.</summary>
    public void Close()
    {
        _onPrimary = null;
        _onSecondary = null;
        _isShowing = false;

        gameObject.SetActive(false);
    }

    private void Open(string message)
    {
        EnsureInitialized();

        SetMessage(message);

        // SetActive(true)보다 먼저 세워야 한다 — 이 호출이 Awake를 처음 돌리는 경우
        // Awake의 방어적 닫기가 이 값을 보고 비켜간다.
        _isShowing = true;

        gameObject.SetActive(true);
    }

    /// <summary>
    /// 버튼을 껐다 켜고 문구를 채운다. 둘 다 끄면 ButtonRow가 비므로,
    /// Popup에 ContentSizeFitter를 달아두면 팝업 높이가 알아서 줄어든다.
    /// </summary>
    private static void SetButton(Button button, TMP_Text label, string text, bool visible)
    {
        if (button == null) return;

        button.gameObject.SetActive(visible);

        if (visible && label != null && !string.IsNullOrEmpty(text))
            label.text = text;
    }

    // 콜백보다 먼저 닫는다 — 그래야 콜백이 곧바로 다음 모달을 띄워도(포기하기 → 종료 확인)
    // 방금 연 모달이 이 Close에 다시 닫히지 않는다.

    private void HandlePrimaryClicked()
    {
        Action action = _onPrimary;
        Close();
        action?.Invoke();
    }

    private void HandleSecondaryClicked()
    {
        Action action = _onSecondary;
        Close();
        action?.Invoke();
    }
}
