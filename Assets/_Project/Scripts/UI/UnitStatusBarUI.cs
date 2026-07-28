using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 유닛 한 기의 머리 위에 붙는 HP/마나 바(롤토체스식 2단). 값 계산은 하지 않고 받은 비율만 그린다.
/// 화면 좌표로 배치되는 스크린 스페이스 UI라 카메라를 향해 돌릴 필요가 없고, 거리와 무관하게 크기가 일정하다.
/// 풀링해서 재사용하므로 생성/파괴는 UnitStatusBarHud가 관리한다.
/// </summary>
public class UnitStatusBarUI : MonoBehaviour
{
    [Header("채우기")]
    [Tooltip("Image Type을 Filled(Horizontal)로 두면 fillAmount로 줄어든다.")]
    [SerializeField] private Image _hpFill;
    [Tooltip("마나가 없는 유닛(스킬 미보유)이면 자동으로 숨긴다.")]
    [SerializeField] private Image _manaFill;
    [SerializeField] private GameObject _manaRoot;

    [Header("팀 색상")]
    [SerializeField] private Color _allyHpColor  = new(0.35f, 0.85f, 0.35f);
    [SerializeField] private Color _enemyHpColor = new(0.9f, 0.3f, 0.3f);

    private RectTransform _rect;

    public RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

    /// <summary>
    /// 표시값 갱신. 비율은 0~1로 클램프한다.
    /// manaRatio가 음수면 마나 바를 숨긴다(스킬이 없거나 마나 개념이 없는 상태).
    /// </summary>
    public void SetValues(float hpRatio, float manaRatio, bool isAlly)
    {
        if (_hpFill != null)
        {
            _hpFill.fillAmount = Mathf.Clamp01(hpRatio);
            _hpFill.color = isAlly ? _allyHpColor : _enemyHpColor;
        }

        bool showMana = manaRatio >= 0f;
        if (_manaRoot != null) _manaRoot.SetActive(showMana);
        if (showMana && _manaFill != null) _manaFill.fillAmount = Mathf.Clamp01(manaRatio);
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }
}
