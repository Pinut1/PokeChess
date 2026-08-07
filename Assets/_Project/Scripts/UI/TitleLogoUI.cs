using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 로고를 "살아 있게" 만드는 연출. 부유(위아래)와 발광(알파 맥동)을 각각 독립적으로
/// 켜고 끌 수 있다.
///
/// 붙이는 곳은 로고 Image 오브젝트 자신이다. 아무것도 연결하지 않아도 자기 RectTransform으로
/// 부유는 동작한다 — 발광만 대상을 지정해야 한다.
///
/// <b>두 연출의 주기를 일부러 어긋나게 둔다.</b> 같은 속도로 맞추면 기계적으로 보이고, 조금씩
/// 다르면 유기적으로 보인다. 그래서 속도를 하나로 묶지 않고 따로 뒀다.
///
/// 시간은 <see cref="Time.unscaledTime"/>을 쓴다 — 타이틀에서 timeScale이 0이 될 일은 없지만,
/// 연출이 게임 시간에 끌려다닐 이유도 없다.
/// </summary>
public class TitleLogoUI : MonoBehaviour
{
    [Header("부유 (위아래)")]
    [SerializeField] private bool _float = true;
    [Tooltip("위아래 이동 폭(픽셀).")]
    [SerializeField, Range(0f, 60f)] private float _floatAmount = 6f;
    [SerializeField, Min(0.1f)] private float _floatPeriod = 4.7f;

    [Header("발광 (알파 맥동)")]
    [Tooltip("로고 뒤에 깔아둔 흐릿한 발광 Image. 없으면 비워둘 것.")]
    [SerializeField] private Graphic _glow;
    [Tooltip("uGUI Outline 컴포넌트. 발광 이미지 없이 외곽선만으로 빛나게 할 때 쓴다.")]
    [SerializeField] private Outline _outline;
    [Tooltip("알파가 오르내리는 범위. x=가장 어두울 때, y=가장 밝을 때.")]
    [SerializeField] private Vector2 _glowAlphaRange = new Vector2(0.25f, 0.7f);
    [SerializeField, Min(0.1f)] private float _glowPeriod = 2.9f;

    private RectTransform _rect;

    // 연출을 얹기 전의 원래 위치. 매번 여기서 다시 계산해야 반복 재생으로 값이 밀리지 않는다.
    private Vector2 _baseAnchoredPosition;
    private bool _baseCaptured;

    private void Awake()
    {
        _rect = transform as RectTransform;
        CaptureBase();
    }

    private void OnEnable()
    {
        // 꺼진 채로 시작해 Awake가 늦게 도는 경우에도 기준값을 확보한다.
        CaptureBase();
    }

    private void OnDisable()
    {
        // 연출 도중에 꺼지면 어중간한 위치로 굳는다. 원래 값으로 되돌려 둔다.
        if (!_baseCaptured || _rect == null) return;

        _rect.anchoredPosition = _baseAnchoredPosition;
    }

    /// <summary>
    /// 기준 위치는 <b>한 번만</b> 잡는다. 껐다 켤 때마다 다시 잡으면 그 순간의 연출된 값이
    /// 새 기준이 되어 로고가 조금씩 떠내려간다.
    /// </summary>
    private void CaptureBase()
    {
        if (_baseCaptured || _rect == null) return;

        _baseAnchoredPosition = _rect.anchoredPosition;
        _baseCaptured = true;
    }

    private void Update()
    {
        if (!_baseCaptured) return;

        float time = Time.unscaledTime;

        if (_float)
        {
            Vector2 position = _baseAnchoredPosition;
            position.y += Wave(time, _floatPeriod) * _floatAmount;
            _rect.anchoredPosition = position;
        }

        UpdateGlow(time);
    }

    private void UpdateGlow(float time)
    {
        if (_glow == null && _outline == null) return;

        // Wave는 -1~1이므로 0~1로 옮겨 범위에 매핑한다.
        float t = (Wave(time, _glowPeriod) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(_glowAlphaRange.x, _glowAlphaRange.y, t);

        if (_glow != null)
        {
            Color color = _glow.color;
            color.a = alpha;
            _glow.color = color;
        }

        if (_outline != null)
        {
            Color color = _outline.effectColor;
            color.a = alpha;
            _outline.effectColor = color;
        }
    }

    /// <summary>주기(초)를 받아 -1~1 사인파를 돌려준다.</summary>
    private static float Wave(float time, float period)
    {
        return Mathf.Sin(time / period * Mathf.PI * 2f);
    }
}
