using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플러시와마이농(310)을 필드에 올렸을 때 화면 아래에서 올라오는 폼 선택창(CheerChoice_Panel).
/// 기존 IMGUI 창(UIManager.DrawPlusleMinunFormChoicePanel)을 대체한다 — 이 스크립트가 살아 있는
/// 동안 UIManager는 IMGUI 폴백을 그리지 않는다(SetFormChoicePanelBound).
///
/// ⚠️ 이 스크립트는 <b>항상 활성인 오브젝트</b>(Canvas 등)에 붙여야 한다.
/// 슬라이드 대상인 CheerChoice_Panel 자신에게 붙이면 안 된다 — 코루틴은 스크립트가 붙은
/// 오브젝트에서 돌기 때문에, 켜고 끄지 않는 구성이어도 참조로 다루는 편이 안전하다.
///
/// 표시 방식: 활성/비활성 토글이 아니라 <b>anchoredPosition.y 슬라이드</b>다.
/// CheerChoice_Panel은 항상 켜져 있고, 대기 없음 = 화면 밖(_hiddenPosY),
/// 선택 대기 = 올라온 위치(_shownPosY). x는 씬에 잡아둔 값을 그대로 둔다.
///
/// 카드 <b>내용은 스크립트가 채우지 않는다</b>. 선택지가 플러시/마이농 둘로 고정된 특수 케이스라
/// 아이콘(Icon_Image)·이름(Name_Text)·설명(what_Text)은 씬에 그려둔 값이 그대로 나온다.
/// 여기서는 버튼 클릭을 어느 폼으로 해석할지만 정한다.
///
/// 씬 구조(GameSceneTest):
///   CheerChoice_Panel                — 슬라이드 대상
///     ChoiceSub_Panel
///       Choice_Text                  — 제목(고정 문구)
///       Choice_Panel                 — HorizontalLayoutGroup
///         CheerChoice_Button_311     — 플러시
///         CheerChoice_Button_312     — 마이농
///
/// 선택 대기 중 3D 배치 입력(드래그) 차단은 UnitDragController가 보는
/// UIManager.IsPlusleMinunChoiceBlocking이 담당한다 — 이미 배선돼 있어 추가 작업이 없다.
/// 화면을 덮는 모달이 아니므로 상점 등 다른 UI는 그대로 눌린다.
///
/// 제한 시간은 없다. 고르지 않은 채 전투가 시작되면(전원 Ready) UIManager가 무작위로 확정한다.
/// </summary>
public class PlusleMinunChoicePanel : MonoBehaviour
{
    [Header("슬라이드 패널")]
    [Tooltip("CheerChoice_Panel의 RectTransform. 이 오브젝트는 항상 켜둔 채 위치만 오르내린다.")]
    [SerializeField] private RectTransform CheerChoice_Panel;

    [Tooltip("숨은 위치의 anchoredPosition.y (화면 밖).")]
    [SerializeField] private float _hiddenPosY = -250f;

    [Tooltip("올라온 위치의 anchoredPosition.y.")]
    [SerializeField] private float _shownPosY = 130f;

    [Tooltip("오르내리는 데 걸리는 시간(초). 0이면 즉시 이동.")]
    [SerializeField] private float _slideDuration = 0.35f;

    [Tooltip("이동 가속 곡선. 가로축 0~1이 진행도, 세로축 0~1이 이동 비율.")]
    [SerializeField] private AnimationCurve _slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("선택 버튼")]
    [Tooltip("CheerChoice_Button_311 — 누르면 플러시(311)로 확정. 카드 전체가 눌리도록 자식 텍스트/이미지는 Raycast Target을 끌 것")]
    [SerializeField] private Button CheerChoice_Button_311;

    [Tooltip("CheerChoice_Button_312 — 누르면 마이농(312)으로 확정.")]
    [SerializeField] private Button CheerChoice_Button_312;

    private Coroutine _slideCoroutine;

    /// <summary>매니저 조회. Singleton.Instance는 null일 때 LogError를 찍으므로 TryGet을 쓴다.</summary>
    private static UIManager Ui =>
        GameManager.TryGet(out var gm) ? gm.UI : null;

    private void Awake()
    {
        BindButton(CheerChoice_Button_311, HandlePlusleClicked);
        BindButton(CheerChoice_Button_312, HandleMinunClicked);

        WarnMissingWiring();

        // 씬에 올라온 위치로 저장돼 있어도 시작은 화면 밖이다.
        SnapTo(_hiddenPosY);
    }

    /// <summary>
    /// 비어 있는 참조를 시작 시 한 번에 알린다. 이 UI는 참조가 없으면 조용히 아무 일도 안 하는
    /// 형태라(창이 안 뜨는 것과 구분이 안 된다) 배선 누락을 로그로 드러내야 한다.
    /// </summary>
    private void WarnMissingWiring()
    {
        if (CheerChoice_Panel == null)
            Debug.LogError("[PlusleMinunChoicePanel] CheerChoice_Panel 미배선 — 선택창이 뜨지 않는다", this);

        if (CheerChoice_Button_311 == null)
            Debug.LogError("[PlusleMinunChoicePanel] CheerChoice_Button_311 미배선 — 플러시를 고를 수 없다", this);

        if (CheerChoice_Button_312 == null)
            Debug.LogError("[PlusleMinunChoicePanel] CheerChoice_Button_312 미배선 — 마이농을 고를 수 없다", this);
    }

    private void OnEnable()
    {
        GameEvents.OnPlusleMinunChoiceReady  += HandleChoiceReady;
        GameEvents.OnPlusleMinunChoiceClosed += HandleChoiceClosed;
    }

    private void Start()
    {
        UIManager ui = Ui;
        if (ui == null)
        {
            Debug.LogWarning("[PlusleMinunChoicePanel] UIManager 없음 — 선택창이 동작하지 않는다", this);
            return;
        }

        // 슬라이드 대상이 없으면 이 패널은 아무것도 못 그린다. 이때는 등록하지 않아
        // UIManager의 IMGUI 폴백이 그대로 창을 띄우게 둔다(선택 자체가 막히는 것보단 낫다).
        if (CheerChoice_Panel == null) return;

        // UIManager 등록은 Start에서 한다 — OnEnable 시점엔 GameManager가 아직 매니저를 잡기 전일 수 있다.
        ui.SetFormChoicePanelBound(true);

        // 패널이 대기 시작보다 늦게 켜졌으면 OnPlusleMinunChoiceReady를 놓쳤을 수 있다
        // (씬 재진입·오브젝트 토글). 현재 상태를 한 번 끌어와 그 구멍을 막는다.
        if (ui.PendingFormUnit != null) HandleChoiceReady(ui.PendingFormUnit);
    }

    private void OnDisable()
    {
        GameEvents.OnPlusleMinunChoiceReady  -= HandleChoiceReady;
        GameEvents.OnPlusleMinunChoiceClosed -= HandleChoiceClosed;

        // 컴포넌트가 꺼지면 코루틴도 멈춘다 — 패널이 중간 높이에 걸린 채 남지 않게 즉시 내린다.
        _slideCoroutine = null;
        SnapTo(_hiddenPosY);

        // 패널이 사라지면 IMGUI 폴백이 다시 표시를 맡는다.
        UIManager ui = Ui;
        if (ui != null) ui.SetFormChoicePanelBound(false);
    }

    // ─────────────────────────────────────────
    // 열기 / 닫기
    // ─────────────────────────────────────────

    /// <summary>인자는 받되 쓰지 않는다 — 표시 내용이 씬 고정이라 유닛별로 바뀔 게 없다.</summary>
    private void HandleChoiceReady(PokemonUnit unit)
    {
        // 직전 선택에서 꺼둔 버튼을 되살린다.
        SetButtonsInteractable(true);

        SlideTo(_shownPosY);
    }

    private void HandleChoiceClosed() => SlideTo(_hiddenPosY);

    // ─────────────────────────────────────────
    // 슬라이드 연출
    // ─────────────────────────────────────────

    /// <summary>목표 높이로 이동을 시작한다. 이미 이동 중이면 현재 위치에서 이어받는다.</summary>
    private void SlideTo(float targetY)
    {
        if (CheerChoice_Panel == null) return;

        if (_slideCoroutine != null)
        {
            StopCoroutine(_slideCoroutine);
            _slideCoroutine = null;
        }

        // 시간이 0이거나 코루틴을 돌릴 수 없는 상태면 연출 없이 자리만 맞춘다.
        if (_slideDuration <= 0f || !isActiveAndEnabled)
        {
            SnapTo(targetY);
            return;
        }

        _slideCoroutine = StartCoroutine(SlideRoutine(targetY));
    }

    private IEnumerator SlideRoutine(float targetY)
    {
        Vector2 from = CheerChoice_Panel.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < _slideDuration)
        {
            // 전투 결과 연출 등에서 timeScale이 0이어도 창은 움직여야 하므로 unscaled를 쓴다.
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / _slideDuration);
            float eased = _slideCurve != null ? _slideCurve.Evaluate(t) : t;

            SetPosY(Mathf.LerpUnclamped(from.y, targetY, eased));
            yield return null;
        }

        SetPosY(targetY);
        _slideCoroutine = null;
    }

    private void SnapTo(float y)
    {
        if (_slideCoroutine != null)
        {
            StopCoroutine(_slideCoroutine);
            _slideCoroutine = null;
        }

        SetPosY(y);
    }

    /// <summary>y만 바꾼다 — x는 씬에서 잡아둔 가로 위치를 그대로 유지해야 한다.</summary>
    private void SetPosY(float y)
    {
        if (CheerChoice_Panel == null) return;

        Vector2 pos = CheerChoice_Panel.anchoredPosition;
        pos.y = y;
        CheerChoice_Panel.anchoredPosition = pos;
    }

    // ─────────────────────────────────────────
    // 선택
    // ─────────────────────────────────────────

    private static void BindButton(Button button, UnityEngine.Events.UnityAction onClick)
    {
        if (button == null) return;

        button.onClick.RemoveListener(onClick);
        button.onClick.AddListener(onClick);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (CheerChoice_Button_311 != null) CheerChoice_Button_311.interactable = interactable;
        if (CheerChoice_Button_312 != null) CheerChoice_Button_312.interactable = interactable;
    }

    private void HandlePlusleClicked() => SelectForm(isPlusle: true);
    private void HandleMinunClicked()  => SelectForm(isPlusle: false);

    private void SelectForm(bool isPlusle)
    {
        // 같은 프레임에 두 번 눌리는 것을 막는다(패널이 내려가는 건 이벤트를 타고 한 박자 뒤에 온다).
        SetButtonsInteractable(false);

        UIManager ui = Ui;
        if (ui == null)
        {
            Debug.LogWarning("[PlusleMinunChoicePanel] UIManager 없음 — 선택 무시");
            return;
        }

        // → GameEvents.PlusleMinunChoiceClosed → HandleChoiceClosed(내려감)
        ui.SelectPlusleMinunForm(isPlusle ? ui.PlusleFormData : ui.MinunFormData);
    }
}
