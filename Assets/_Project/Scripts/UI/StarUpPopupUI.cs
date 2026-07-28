using UnityEngine;

/// <summary>
/// 성급 진화 순간에 잠깐 떴다 사라지는 팝업 한 개. 2성/3성을 다른 아트로 구분한다.
/// 표시만 담당하고 등장·상승·페이드 진행은 StarUpPopupHud가 준다(위치 계산과 같은 곳에서 다뤄야
/// 유닛을 따라다니면서 사라지는 연출이 어긋나지 않는다).
///
/// HP바의 상시 성급 표식(UnitStatusBarUI._star2/_star3)과는 별개다. 이쪽은 "방금 진화했다"는 순간 연출.
/// </summary>
public class StarUpPopupUI : MonoBehaviour
{
    [SerializeField] private GameObject _star2;
    [SerializeField] private GameObject _star3;

    [Tooltip("페이드용. 없으면 알파 처리 없이 표시/숨김만 한다.")]
    [SerializeField] private CanvasGroup _canvasGroup;

    private RectTransform _rect;

    public RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

    /// <summary>2성/3성 표식 선택. 그 외 값이면 둘 다 끈다.</summary>
    public void SetStar(int starLevel)
    {
        if (_star2 != null) _star2.SetActive(starLevel == 2);
        if (_star3 != null) _star3.SetActive(starLevel >= 3);
    }

    public void SetAlpha(float alpha)
    {
        if (_canvasGroup != null) _canvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }
}
