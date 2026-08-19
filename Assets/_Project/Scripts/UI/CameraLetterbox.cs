using UnityEngine;

/// <summary>
/// 카메라의 뷰포트를 <b>화면 가운데 16:9 영역</b>으로 고정한다. 화면 비율이 16:9가 아니면
/// 남는 쪽(위아래 또는 좌우)은 이 카메라가 그리지 않으므로, 그 자리는 UI 쪽 검은 띠
/// (LetterboxLayoutTool이 만드는 Letterbox_*)가 덮는다.
///
/// <b>UI 레터박스와 짝이다.</b> SafeArea_16x9는 UI를 16:9로 가두지만 3D 화면까지 가두지는 못한다 —
/// Screen Space - Overlay 캔버스는 카메라와 무관하게 화면 전체를 덮기 때문이다. 이 컴포넌트가
/// 3D 쪽을 같은 영역으로 맞춰, 보드가 검은 띠 뒤로 잘려 들어가는 것을 막는다.
///
/// 두 영역이 정확히 겹치는 이유 — CanvasScaler가 Expand(scale = min(w/1920, h/1080))라
/// SafeArea의 물리 크기가 "화면 안에 들어가는 가장 큰 16:9"가 되고, 아래 계산도 같은 값을 낸다.
/// 예: 1680x1050에서 둘 다 1680x945(위아래 52.5px씩 여백).
///
/// <b>런타임 계산이 필요한 이유</b> — Camera.rect는 정규화 좌표(0~1)라 화면 비율이 바뀌면 다시
/// 구해야 한다. UI 띠처럼 앵커로 고정할 수 없다. 대신 비율이 실제로 바뀐 프레임에만 대입한다.
/// </summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class CameraLetterbox : MonoBehaviour
{
    [Tooltip("고정할 화면 비율. 16:9 = 1.7778 (CanvasScaler Reference 1920x1080과 같은 값이어야 한다).")]
    [SerializeField] private float _targetAspect = 16f / 9f;

    [Tooltip("끌 때 뷰포트를 화면 전체로 되돌린다. 디버그용으로 잠깐 끄고 볼 때 편하다.")]
    [SerializeField] private bool _restoreOnDisable = true;

    private static readonly Rect FullRect = new(0f, 0f, 1f, 1f);

    private Camera _camera;

    // 마지막으로 계산에 쓴 화면 크기. 같으면 다시 계산하지 않는다(매 프레임 Rect 대입 방지).
    private int _lastWidth;
    private int _lastHeight;

    private void Awake() => _camera = GetComponent<Camera>();

    private void OnEnable()
    {
        _lastWidth = 0; // 다음 갱신에서 무조건 다시 계산
        _lastHeight = 0;
        Apply();
    }

    private void OnDisable()
    {
        if (_restoreOnDisable && _camera != null) _camera.rect = FullRect;
    }

    // 해상도 변경은 프레임 중간에 반영되므로 매 프레임 확인한다 — 실제 대입은 값이 바뀔 때만 일어난다.
    private void LateUpdate() => Apply();

    private void Apply()
    {
        if (_camera == null) return;

        // RenderTexture로 그리는 카메라는 건드리지 않는다. 관전용 카메라(PartnerSpectateView)가
        // 여기 해당하는데, 이미 16:9로 보여지는 RT 안에 또 띠를 넣으면 이중 레터박스가 된다.
        if (_camera.targetTexture != null) return;

        if (Screen.width == _lastWidth && Screen.height == _lastHeight) return;
        _lastWidth = Screen.width;
        _lastHeight = Screen.height;

        if (Screen.width <= 0 || Screen.height <= 0 || _targetAspect <= 0f) return;

        float screenAspect = (float)Screen.width / Screen.height;
        float ratio = screenAspect / _targetAspect;

        if (Mathf.Approximately(ratio, 1f))
        {
            _camera.rect = FullRect;
            return;
        }

        if (ratio > 1f)
        {
            // 화면이 더 넓다(울트라와이드 등) → 좌우를 잘라낸다(필러박스).
            float width = 1f / ratio;
            _camera.rect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
        }
        else
        {
            // 화면이 더 높다(16:10 · 4:3) → 위아래를 잘라낸다(레터박스).
            _camera.rect = new Rect(0f, (1f - ratio) * 0.5f, 1f, ratio);
        }
    }
}
