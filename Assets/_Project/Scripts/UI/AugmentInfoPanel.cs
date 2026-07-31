using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 지금까지 고른 증강을 보여주는 확인창. 3D 오브젝트(AugmentInfoTrigger)를 클릭하면 열리고,
/// 열린 뒤에는 <b>화면 아무 곳이나 한 번 더 누르면</b> 닫힌다.
///
/// 현재 기획은 한 판에 증강을 1개만 고르므로 목록/툴팁을 나누지 않고 한 창에 아이콘·이름·설명을 모두 넣는다.
///
/// 표시 규칙은 하나뿐이다 — <b>보유한 증강이 있으면 그 증강, 없으면 미선택 안내.</b>
/// 라운드를 따로 보지 않는다. 1-1처럼 아직 증강을 못 받은 시점이면 자연히 안내가 뜨고,
/// 1-2에서 고르고 나면 그 증강이 보인다.
///
/// ⚠️ 이 스크립트는 <b>항상 활성인 오브젝트</b>(Canvas 등)에 붙여야 한다.
/// 켜고 끌 대상은 Panel Group 참조로 다룬다 — 꺼진 오브젝트에 붙이면 이벤트를 구독하지 못한다.
/// </summary>
public class AugmentInfoPanel : MonoBehaviour
{
    [Header("패널")]
    [Tooltip("확인창 루트. 열고 닫을 대상이라 이 스크립트를 여기 붙이면 안 된다.")]
    [SerializeField] private GameObject _panelGroup;

    [Header("증강 표시")]
    [Tooltip("Icon_Panel — 증강 아이콘(AugmentData.icon).")]
    [SerializeField] private Image Icon_Panel;

    [Tooltip("NameText — 증강 이름.")]
    [SerializeField] private TextMeshProUGUI NameText;

    [Tooltip("Augment_Text — 증강 설명.")]
    [SerializeField] private TextMeshProUGUI Augment_Text;

    [Header("미선택 표시")]
    // 안내용 오브젝트를 따로 두지 않고 같은 텍스트 자리를 재활용한다.
    [Tooltip("증강을 고르기 전 NameText에 넣을 문구.")]
    [SerializeField] private string _emptyName = "미선택";

    [Tooltip("증강을 고르기 전 Augment_Text에 넣을 문구.")]
    [SerializeField] private string _emptyDescription = "증강 선택 전 입니다.";

    [Header("이름 줄바꿈")]
    [Tooltip("이름이 이 글자 수를 넘으면 콜론(:)을 줄바꿈으로 바꿔 두 줄로 보여준다.\n" +
             "영웅증강은 \"기술머신:○○\" 형태인데 \"기술머신:나인이볼부스트\"(12자)만 한 줄에 안 들어간다. " +
             "\"기술머신:날따름\"(8자)은 그대로 한 줄. 0이면 항상 한 줄로 둔다.")]
    [SerializeField] private int _wrapNameOverLength = 10;

    [Header("닫기 입력")]
    // UnitDragController와 같은 방식 — <Pointer>는 마우스·펜·터치를 모두 포괄한다.
    [SerializeField] private InputAction _dismissAction =
        new InputAction("Dismiss", InputActionType.Button, "<Pointer>/press");

    /// <summary>매니저 조회. Singleton.Instance는 null일 때 LogError를 찍으므로 TryGet을 쓴다.</summary>
    private static AugmentManager Augment =>
        GameManager.TryGet(out var gm) ? gm.Augment : null;

    public bool IsOpen => _panelGroup != null && _panelGroup.activeSelf;

    // 연/닫은 프레임 번호. 창을 연 그 클릭이 곧바로 창을 닫아버리는 것(그리고 그 반대)을 막는다.
    // OnMouseDown과 Update의 호출 순서가 보장되지 않아서 양쪽 모두 기록해 둔다.
    private int _openedFrame = -1;
    private int _closedFrame = -1;

    private void Awake()
    {
        if (_panelGroup == null)
            Debug.LogError("[AugmentInfoPanel] Panel Group 미배선 — 창이 열리지 않는다", this);

        // 씬에 켜둔 채 저장했더라도 시작은 닫힌 상태로 맞춘다.
        if (_panelGroup != null) _panelGroup.SetActive(false);
    }

    private void OnEnable()
    {
        _dismissAction.Enable();

        // 창을 열어둔 채로 증강을 새로 받는 경우(자동 선택 등)에도 표시가 최신이 되도록.
        GameEvents.OnAugmentSelected += HandleAugmentSelected;
    }

    private void OnDisable()
    {
        _dismissAction.Disable();
        GameEvents.OnAugmentSelected -= HandleAugmentSelected;
    }

    private void Update()
    {
        if (!IsOpen) return;

        // 창을 연 바로 그 클릭으로는 닫지 않는다.
        if (Time.frameCount == _openedFrame) return;

        if (_dismissAction.WasPressedThisFrame()) Close();
    }

    private void HandleAugmentSelected(AugmentData _)
    {
        if (IsOpen) Refresh();
    }

    // ─────────────────────────────────────────
    // 열기 / 닫기
    // ─────────────────────────────────────────

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (_panelGroup == null) return;

        // 같은 프레임에 이미 닫혔으면 다시 열지 않는다 — 열려 있을 때 트리거를 클릭하면
        // "화면 클릭으로 닫힘"과 "트리거로 열림"이 한 프레임에 겹쳐 창이 안 닫히는 것처럼 보인다.
        if (Time.frameCount == _closedFrame) return;

        Refresh(); // 켜기 전에 채워야 한 프레임 빈 창이 보이지 않는다

        _panelGroup.SetActive(true);
        _openedFrame = Time.frameCount;
    }

    public void Close()
    {
        if (_panelGroup != null) _panelGroup.SetActive(false);
        _closedFrame = Time.frameCount;
    }

    // ─────────────────────────────────────────
    // 표시 갱신
    // ─────────────────────────────────────────

    private void Refresh()
    {
        AugmentManager augment = Augment;

        // 매니저가 없으면 "보유 없음"과 같게 취급한다 — 창이 깨진 채 뜨는 것보단 낫다.
        var active = augment != null ? augment.ActiveAugments : null;

        // 증강은 1개만 고르는 기획이라 첫 번째만 본다.
        AugmentData data = active != null && active.Count > 0 ? active[0].Data : null;

        if (Icon_Panel != null)
        {
            // 아이콘은 AugmentCatalog가 AugmentIconDatabase에서 이미 채워 넣는다(없으면 null).
            Sprite icon = data != null ? data.icon : null;
            if (icon != null) Icon_Panel.sprite = icon;

            // 컴포넌트가 아니라 오브젝트를 끈다 — 테두리 같은 자식까지 함께 숨겨야 하기 때문.
            // (미등록 아이콘도 흰 사각형이 남지 않게 같이 처리된다)
            Icon_Panel.gameObject.SetActive(icon != null);
        }

        if (NameText != null)
            NameText.text = data != null ? FormatName(data.augmentName) : _emptyName;

        if (Augment_Text != null)
            Augment_Text.text = data != null ? data.description : _emptyDescription;
    }

    /// <summary>
    /// 긴 이름만 콜론에서 줄을 나눈다. 원본(AugmentCatalog의 augmentName)은 그대로 두는 게 중요하다 —
    /// 그쪽을 고치면 증강 선택 카드·전적 기록·파트너 표시에도 개행이 따라 들어간다.
    /// </summary>
    private string FormatName(string augmentName)
    {
        if (string.IsNullOrEmpty(augmentName)) return "";

        if (_wrapNameOverLength <= 0 || augmentName.Length <= _wrapNameOverLength)
            return augmentName;

        // 콜론은 남기고 그 뒤에서 줄을 넘긴다 — "기술머신:" 이 접두사임이 그대로 보이게.
        return augmentName.Replace(":", ":\n");
    }
}
