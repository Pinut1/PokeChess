using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 증강 3택1 오퍼 정식 UI 컨트롤러.
///
/// ⚠️ 이 스크립트는 <b>항상 활성인 오브젝트</b>(Canvas 등)에 붙여야 한다.
/// 유니티는 비활성 오브젝트의 Awake/Start를 실행하지 않으므로, Center_PanelGroup 위에 두면
/// 자기를 켜줄 OnAugmentOfferReady를 구독조차 못 해 영원히 안 켜진다.
/// 그래서 패널 그룹은 "붙는 위치"가 아니라 "참조로 켜고 끄는 대상"이다.
///
/// 표시 구조:
///   Center_PanelGroup — 오퍼 동안만 활성
///     Augment_Panel      — 카드 3장. 내려두면 비활성
///     AugmentHold_Button — 내려두기 토글. Augment_Panel 밖이라 내려둔 상태에서도 남는다
///   Bottom_Panel      — Augment_Panel과 반대로 동작(카드 보일 때 숨김)
/// </summary>
public class AugmentOfferPanel : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private GameObject Center_PanelGroup;
    [SerializeField] private GameObject Augment_Panel;
    [SerializeField] private GameObject Bottom_Panel;

    [Header("카드")]
    [Tooltip("Augment_ChoiceCard — AugmentCardUI를 가진 카드들의 부모")]
    [SerializeField] private Transform Augment_ChoiceCard;

    [Header("내려두기")]
    [SerializeField] private Button AugmentHold_Button;
    [Tooltip("내려두면 180° 뒤집히는 화살표. Pivot이 중앙이라 회전만으로 제자리에서 뒤집힌다.")]
    [SerializeField] private RectTransform HoldIcon_Image;

    [Tooltip("위로 띄울 대상. 비워두면 버튼과 아이콘 중 <b>바깥쪽(부모)</b>을 자동으로 고른다 — " +
             "둘의 부모자식 관계를 뒤집어도 그대로 동작한다.\n" +
             "안쪽(자식)만 띄우면 부모에 붙은 그림은 제자리에 있고 클릭 영역만 어긋난다.")]
    [SerializeField] private RectTransform Hold_RiseTarget;

    [Tooltip("내려둔 상태에서 버튼을 띄울 높이(px). 하단 패널과 겹치지 않도록 조절.")]
    [SerializeField] private float _minimizedButtonRiseY;
    [SerializeField] private float _minimizedIconRotationZ = 180f;

    [Header("내려두기 — 아이콘 색")]
    [Tooltip("색을 바꿀 아이콘 이미지. 비워두면 Hold Icon Image에 붙은 Image를 쓰고, " +
             "거기 없으면 그 자식에서 찾는다.")]
    [SerializeField] private Image HoldIcon_Graphic;

    [Tooltip("내려둔(180° 뒤집힌) 상태의 아이콘 색. 평상시 색은 씬/프리팹에 잡아둔 Image 색을 그대로 쓴다.")]
    [SerializeField] private Color _minimizedIconColor = new(1f, 0.82f, 0.35f, 1f);

    [Tooltip("끄면 색은 그대로 두고 회전·위치만 바뀐다.")]
    [SerializeField] private bool _tintIconWhenMinimized = true;

    [Header("텍스트(선택)")]
    [Tooltip("남은 시간. 안내 문구는 씬에 고정 텍스트로 넣어두므로 스크립트가 건드리지 않는다.")]
    [SerializeField] private TextMeshProUGUI AugmentHold_TimerText;

    private readonly List<AugmentCardUI> _cards = new();

    private bool _isMinimized;
    private Quaternion _iconBaseRotation;
    private Color _iconBaseColor = Color.white;

    // 실제로 띄우는 대상과 그 원래 자리. 버튼이 아니라 "바깥쪽 오브젝트"일 수 있다.
    private RectTransform _riseRect;
    private Vector2 _riseBasePos;
    private int _shownSeconds = -1; // 타이머 텍스트 중복 대입 방지(TMP는 같은 값이어도 재빌드한다)

    private void Awake()
    {
        // 카드는 비활성 상태로 있을 수 있어 includeInactive: true 가 필요하다.
        if (Augment_ChoiceCard != null)
            Augment_ChoiceCard.GetComponentsInChildren(true, _cards);

        if (_cards.Count != AugmentManager.OFFER_COUNT)
            Debug.LogWarning($"[AugmentOfferPanel] 카드 {_cards.Count}장 — {AugmentManager.OFFER_COUNT}장이어야 함");

        if (HoldIcon_Image != null)
        {
            _iconBaseRotation = HoldIcon_Image.localRotation;

            // GetComponentInChildren은 자기 자신부터 본다 — 아이콘에 Image가 붙어 있으면 그게 잡힌다.
            if (HoldIcon_Graphic == null)
                HoldIcon_Graphic = HoldIcon_Image.GetComponentInChildren<Image>(true);
        }

        // 평상시 색은 씬에 잡아둔 값을 그대로 쓴다(회전 기준값·버튼 원위치와 같은 방식).
        if (HoldIcon_Graphic != null)
            _iconBaseColor = HoldIcon_Graphic.color;

        _riseRect = ResolveRiseTarget();
        if (_riseRect != null)
            _riseBasePos = _riseRect.anchoredPosition;

        if (AugmentHold_Button != null)
            AugmentHold_Button.onClick.AddListener(ToggleMinimized);

        // 오퍼가 오기 전까지는 닫아둔다(씬에 켜둔 채 저장했더라도 정리).
        if (Center_PanelGroup != null)
            Center_PanelGroup.SetActive(false);
    }

    private void OnEnable()
    {
        GameEvents.OnAugmentOfferReady += HandleOfferReady;
        GameEvents.OnAugmentSelected   += HandleOfferClosed;
    }

    private void OnDisable()
    {
        GameEvents.OnAugmentOfferReady -= HandleOfferReady;
        GameEvents.OnAugmentSelected   -= HandleOfferClosed;
    }

    private void Update()
    {
        if (AugmentHold_TimerText == null) return;
        if (Center_PanelGroup == null || !Center_PanelGroup.activeSelf) return;

        AugmentManager augment = Augment;
        if (augment == null) return;

        // 초가 바뀔 때만 대입한다. 매 프레임 넣으면 값이 같아도 TMP가 메시를 다시 만든다.
        int seconds = Mathf.CeilToInt(augment.OfferTimeRemaining);
        if (seconds == _shownSeconds) return;

        _shownSeconds = seconds;
        AugmentHold_TimerText.text = seconds > 0 ? $"{seconds}" : "";
    }

    /// <summary>매니저 조회. Singleton.Instance는 null일 때 LogError를 찍으므로 TryGet을 쓴다.</summary>
    private static AugmentManager Augment =>
        GameManager.TryGet(out var gm) ? gm.Augment : null;

    // ─────────────────────────────────────────
    // 오퍼 열기 / 닫기
    // ─────────────────────────────────────────

    private void HandleOfferReady(IReadOnlyList<AugmentData> offer)
    {
        if (SoundManager.TryGet(out var sm)) sm.PlaySfx(SoundId.AugmentOpen);

        for (int i = 0; i < _cards.Count; i++)
        {
            if (i < offer.Count)
                _cards[i].SetAugment(offer[i], HandleCardClicked);
            else
                _cards[i].gameObject.SetActive(false); // 풀 소진으로 3장 미만인 경우
        }

        _isMinimized = false;
        _shownSeconds = -1; // 새 오퍼 — 타이머 캐시 초기화
        if (Center_PanelGroup != null)
        {
            Center_PanelGroup.SetActive(true);
        }
        ApplyMinimizedState();
    }

    private void HandleOfferClosed(AugmentData _)
    {
        _isMinimized = false;
        if (Center_PanelGroup != null) Center_PanelGroup.SetActive(false);
        if (Bottom_Panel != null)      Bottom_Panel.SetActive(true);
    }

    private void HandleCardClicked(AugmentData data)
    {
        AugmentManager augment = Augment;
        if (augment == null)
        {
            Debug.LogWarning("[AugmentOfferPanel] AugmentManager 없음 — 선택 무시");
            return;
        }

        augment.SelectAugment(data); // → GameEvents.AugmentSelected → HandleOfferClosed
    }

    // ─────────────────────────────────────────
    // 내려두기
    // ─────────────────────────────────────────

    private void ToggleMinimized()
    {
        _isMinimized = !_isMinimized;

        // 블로킹 해제/재개는 매니저가 단일 소스(IsChoiceBlocking) — UnitDragController가 이걸 본다.
        AugmentManager augment = Augment;
        if (augment != null) augment.SetOfferMinimized(_isMinimized);

        ApplyMinimizedState();
    }

    /// <summary>내려둠 여부에 따라 카드/하단바/버튼 아이콘을 한 번에 맞춘다.</summary>
    private void ApplyMinimizedState()
    {
        if (Augment_Panel != null) Augment_Panel.SetActive(!_isMinimized);

        // 카드가 보이는 동안엔 하단바를 숨긴다(내려두면 되돌려 조작 가능).
        if (Bottom_Panel != null) Bottom_Panel.SetActive(_isMinimized);

        // 아이콘은 회전만 — Pivot이 중앙이라 제자리에서 뒤집힌다.
        if (HoldIcon_Image != null)
            HoldIcon_Image.localRotation = _isMinimized
                ? _iconBaseRotation * Quaternion.Euler(0f, 0f, _minimizedIconRotationZ)
                : _iconBaseRotation;

        // 뒤집힌 상태를 색으로도 구분한다. 스프라이트는 그대로 두고 틴트만 바꾼다.
        if (_tintIconWhenMinimized && HoldIcon_Graphic != null)
            HoldIcon_Graphic.color = _isMinimized ? _minimizedIconColor : _iconBaseColor;

        // 내려둔 동안엔 버튼을 띄운다(하단 패널과 겹치지 않게).
        if (_riseRect != null)
        {
            Vector2 pos = _riseBasePos;
            if (_isMinimized) pos.y += _minimizedButtonRiseY;
            _riseRect.anchoredPosition = pos;
        }
    }

    /// <summary>
    /// 띄울 대상을 정한다. 인스펙터에 지정했으면 그것, 아니면
    /// <b>Center_PanelGroup의 직속 자식 중 버튼을 품고 있는 것</b>.
    ///
    /// 버튼이나 아이콘을 곧바로 쓰지 않는 이유: 둘 중 <b>안쪽</b>을 띄우면 바깥 오브젝트에 붙은
    /// 그림은 제자리에 남고 클릭 판정만 위로 뜬다. 어느 쪽이 부모인지, 사이에 래퍼 오브젝트를
    /// 하나 더 끼웠는지에 따라 답이 달라지므로, 계층을 어떻게 짜든 변하지 않는 기준
    /// (= 오퍼 패널 그룹 바로 아래 한 덩어리)으로 잡는다.
    ///
    /// 이 규칙이 성립하려면 내려두기 버튼 덩어리가 Augment_Panel <b>밖</b>,
    /// Center_PanelGroup 바로 아래에 있어야 한다(클래스 주석의 표시 구조 그대로).
    /// </summary>
    private RectTransform ResolveRiseTarget()
    {
        if (Hold_RiseTarget != null) return Hold_RiseTarget;

        RectTransform button = AugmentHold_Button != null
            ? AugmentHold_Button.transform as RectTransform
            : null;

        RectTransform fallback = button != null ? button : HoldIcon_Image;
        if (fallback == null) return null;

        Transform group = Center_PanelGroup != null ? Center_PanelGroup.transform : null;
        if (group == null) return fallback;

        // 버튼에서 위로 올라가며 부모가 패널 그룹인 지점을 찾는다.
        for (Transform t = fallback; t != null; t = t.parent)
            if (t.parent == group)
                return t as RectTransform ?? fallback;

        // 패널 그룹 아래가 아니면(구조 변경) 원래 방식대로 버튼을 띄운다.
        Debug.LogWarning(
            $"[AugmentOfferPanel] '{fallback.name}'이(가) Center_PanelGroup 아래에 없습니다 — " +
            "띄울 대상을 Hold Rise Target에 직접 지정하세요.", this);

        return fallback;
    }
}
