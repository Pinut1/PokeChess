using UnityEngine;

/// <summary>
/// UI 이미지를 제자리에서 둥실 떠 있게 만드는 연출(타이틀 아이콘 등).
///
/// 세 가지를 겹쳐 쓴다. 각각 <b>주기가 따로</b>라 서로 딱 맞아떨어지지 않고, 그래서 같은 궤적을
/// 반복하는 대신 계속 조금씩 다른 길로 흐르는 것처럼 보인다(리사주 도형).
///  - <b>위아래</b>: 부유감의 본체.
///  - <b>좌우</b>: 위아래보다 폭을 훨씬 작게 주는 게 자연스럽다. 0이면 끔.
///  - <b>기울임</b>: 살짝 갸웃거리는 회전. 0이면 끔.
///
/// 여러 개를 겹쳐 놓을 때는 <b>주기와 시작 위상을 서로 다르게</b> 줘야 한 몸처럼 움직이지 않는다.
/// 주기만 다르게 해도 시작 순간(sin 0)에는 같이 출발하므로, 시작 위상까지 흩어 두는 걸 권장.
///
/// 배치 값(anchoredPosition·회전)은 <b>건드리지 않고 기준으로만</b> 쓴다 — 인스펙터에서 잡아둔
/// 위치가 곧 궤도의 중심이다. 회전은 원래 회전 위에 화면 기준으로 얹으므로, Y를 180 돌려
/// 뒤집어 놓은 이미지(BackTitle_Icon)도 기울어지는 방향이 화면에서 동일하다.
///
/// 시간은 <see cref="Time.unscaledTime"/>을 쓴다 — 연출이 게임 시간에 끌려다닐 이유가 없다.
///
/// 로고(Title_Logo)의 부유는 발광과 묶여 있어 <see cref="TitleLogoUI"/>가 따로 담당한다.
/// </summary>
[DisallowMultipleComponent]
public class FloatingIcon : MonoBehaviour
{
    [Header("위아래")]
    [Tooltip("중심에서 위아래로 움직이는 폭(픽셀). 이 값의 ±만큼 오간다.")]
    [SerializeField, Range(0f, 120f)] private float _floatAmount = 12f;

    [Tooltip("한 번 왕복하는 데 걸리는 시간(초). 클수록 느리고 무겁게 뜬다.")]
    [SerializeField, Min(0.1f)] private float _floatPeriod = 3.6f;

    [Header("좌우 (선택)")]
    [Tooltip("좌우로 흔들리는 폭(픽셀). 0이면 좌우 흔들림 없음. 위아래 폭의 1/3 이하를 권장.")]
    [SerializeField, Range(0f, 120f)] private float _swayAmount = 4f;

    [Tooltip("좌우 왕복 주기(초). 위아래 주기와 나누어떨어지지 않게 둬야 궤적이 반복돼 보이지 않는다.")]
    [SerializeField, Min(0.1f)] private float _swayPeriod = 5.3f;

    [Header("기울임 (선택)")]
    [Tooltip("원래 회전에서 좌우로 갸웃거리는 각도(도). 0이면 기울임 없음. 2~5도면 충분하다.")]
    [SerializeField, Range(0f, 30f)] private float _tiltAngle = 2.5f;

    [Tooltip("기울임 왕복 주기(초).")]
    [SerializeField, Min(0.1f)] private float _tiltPeriod = 4.9f;

    [Header("시작 위상")]
    [Tooltip("궤도 어디에서 시작할지(0~1이 한 바퀴). 여러 개를 놓을 때 서로 다르게 줘서 같은 박자로 뜨는 걸 막는다.")]
    [SerializeField, Range(0f, 1f)] private float _phase;

    private RectTransform _rect;

    // 연출을 얹기 전의 배치 값. 매 프레임 여기서 다시 계산해야 값이 누적돼 떠내려가지 않는다.
    private Vector2 _basePosition;
    private Quaternion _baseRotation;
    private bool _baseCaptured;

    private void Awake()
    {
        _rect = transform as RectTransform;
        if (_rect == null)
            Debug.LogWarning("[FloatingIcon] RectTransform이 아니다 — UI 오브젝트에 붙일 것", this);

        CaptureBase();
    }

    // 꺼진 채로 시작해 Awake가 늦게 도는 경우에도 기준값을 확보한다.
    private void OnEnable() => CaptureBase();

    private void OnDisable()
    {
        // 연출 도중에 꺼지면 어중간한 위치·각도로 굳는다. 원래 배치로 되돌려 둔다.
        if (!_baseCaptured || _rect == null) return;

        _rect.anchoredPosition = _basePosition;
        _rect.localRotation = _baseRotation;
    }

    /// <summary>
    /// 기준 배치는 <b>한 번만</b> 잡는다. 껐다 켤 때마다 다시 잡으면 그 순간의 연출된 값이
    /// 새 기준이 되어 아이콘이 조금씩 떠내려간다.
    /// </summary>
    private void CaptureBase()
    {
        if (_baseCaptured || _rect == null) return;

        _basePosition = _rect.anchoredPosition;
        _baseRotation = _rect.localRotation;
        _baseCaptured = true;
    }

    private void Update()
    {
        if (!_baseCaptured) return;

        float time = Time.unscaledTime;

        Vector2 position = _basePosition;
        position.y += Wave(time, _floatPeriod) * _floatAmount;
        if (_swayAmount > 0f) position.x += Wave(time, _swayPeriod) * _swayAmount;
        _rect.anchoredPosition = position;

        if (_tiltAngle <= 0f) return;

        // 원래 회전에 왼쪽에서 곱한다 = 부모(화면) 기준으로 기울인다.
        // 오른쪽에서 곱하면 자기 축 기준이라, Y가 180 돌아간 이미지는 기우는 방향이 반대로 뒤집힌다.
        float tilt = Wave(time, _tiltPeriod) * _tiltAngle;
        _rect.localRotation = Quaternion.Euler(0f, 0f, tilt) * _baseRotation;
    }

    /// <summary>주기(초)와 시작 위상을 반영한 -1~1 사인파.</summary>
    private float Wave(float time, float period)
    {
        return Mathf.Sin((time / period + _phase) * Mathf.PI * 2f);
    }
}
