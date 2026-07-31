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
    [Tooltip("내려둔 상태에서 버튼을 띄울 높이(px). 하단 패널과 겹치지 않도록 조절.")]
    [SerializeField] private float _minimizedButtonRiseY;
    [SerializeField] private float _minimizedIconRotationZ = 180f;

    [Header("텍스트(선택)")]
    [Tooltip("남은 시간. 안내 문구는 씬에 고정 텍스트로 넣어두므로 스크립트가 건드리지 않는다.")]
    [SerializeField] private TextMeshProUGUI AugmentHold_TimerText;

    private readonly List<AugmentCardUI> _cards = new();

    private bool _isMinimized;
    private Quaternion _iconBaseRotation;
    private RectTransform _holdButtonRect;
    private Vector2 _holdButtonBasePos;
    private int _shownSeconds = -1; // 타이머 텍스트 중복 대입 방지(TMP는 같은 값이어도 재빌드한다)

    private void Awake()
    {
        // 카드는 비활성 상태로 있을 수 있어 includeInactive: true 가 필요하다.
        if (Augment_ChoiceCard != null)
            Augment_ChoiceCard.GetComponentsInChildren(true, _cards);

        if (_cards.Count != AugmentManager.OFFER_COUNT)
            Debug.LogWarning($"[AugmentOfferPanel] 카드 {_cards.Count}장 — {AugmentManager.OFFER_COUNT}장이어야 함");

        if (HoldIcon_Image != null)
            _iconBaseRotation = HoldIcon_Image.localRotation;

        if (AugmentHold_Button != null)
        {
            // 버튼의 RectTransform은 Button에서 바로 얻는다 — 인스펙터 필드를 늘리지 않기 위해.
            _holdButtonRect = AugmentHold_Button.transform as RectTransform;
            if (_holdButtonRect != null)
                _holdButtonBasePos = _holdButtonRect.anchoredPosition;

            AugmentHold_Button.onClick.AddListener(ToggleMinimized);
        }

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
        for (int i = 0; i < _cards.Count; i++)
        {
            if (i < offer.Count)
                _cards[i].SetAugment(offer[i], HandleCardClicked);
            else
                _cards[i].gameObject.SetActive(false); // 풀 소진으로 3장 미만인 경우
        }

        _isMinimized = false;
        _shownSeconds = -1; // 새 오퍼 — 타이머 캐시 초기화
        if (Center_PanelGroup != null) Center_PanelGroup.SetActive(true);
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

        // 내려둔 동안엔 버튼 자체를 띄운다(하단 패널과 겹치지 않게).
        if (_holdButtonRect != null)
        {
            Vector2 pos = _holdButtonBasePos;
            if (_isMinimized) pos.y += _minimizedButtonRiseY;
            _holdButtonRect.anchoredPosition = pos;
        }
    }
}
