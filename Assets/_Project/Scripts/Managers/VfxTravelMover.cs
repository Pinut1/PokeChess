using UnityEngine;

/// <summary>
/// VfxAimMode.TravelToTarget 전용 — 부착된 오브젝트를 from에서 to까지 duration 페이스로 이동시킨다.
/// 대상 도달 후 동작은 travelPastTarget으로 갈린다.
///   false(기본) — 도달 즉시 파괴("맞고 사라짐").
///   true         — 멈추지 않고 같은 속도로 계속 직진하다가 화면 밖으로 나가면 파괴("관통해서 날아감").
/// 파티클 내부 설정(Start Speed 등)과 무관하게 어떤 프리팹이든 동작한다.
/// </summary>
public class VfxTravelMover : MonoBehaviour
{
    /// <summary>카메라를 못 찾는 등 화면 밖 판정이 안 될 때를 대비한 최대 생존 시간(초). travelPastTarget 전용.</summary>
    private const float MAX_LIFETIME = 5f;

    /// <summary>뷰포트 좌표 기준 여유 마진. 화면 경계에 딱 걸쳐 파괴되면 눈에 띄게 잘려 보여서 살짝 더 나간 뒤 지운다.</summary>
    private const float VIEWPORT_MARGIN = 0.1f;

    private Vector3 _from;
    private Vector3 _to;
    private float _duration;
    private float _elapsed;
    private bool _arrived;
    private bool _travelPastTarget;

    // 도착 후 관통 비행 단계에서만 쓴다.
    private Vector3 _velocity;
    private float _safetyTimer;
    private Camera _camera;

    /// <summary>이동 시작. BattleVfxPlayer가 Create() 직후 1회만 호출한다.</summary>
    public void Begin(Vector3 from, Vector3 to, float duration, bool travelPastTarget)
    {
        _from = from;
        _to = to;
        _duration = Mathf.Max(0.01f, duration);
        _travelPastTarget = travelPastTarget;
        _elapsed = 0f;
        _arrived = false;
        transform.position = _from;
    }

    private void Update()
    {
        if (_arrived)
        {
            UpdatePastTarget();
            return;
        }

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);
        transform.position = Vector3.Lerp(_from, _to, t);

        if (t >= 1f)
        {
            transform.position = _to;
            _arrived = true;

            if (!_travelPastTarget)
            {
                Destroy(gameObject);
                return;
            }

            float speed = Vector3.Distance(_from, _to) / _duration;
            _velocity = (_to - _from).normalized * speed;
            _safetyTimer = MAX_LIFETIME;
            _camera = Camera.main;
        }
    }

    private void UpdatePastTarget()
    {
        transform.position += _velocity * Time.deltaTime;

        if (IsOffScreen())
        {
            Destroy(gameObject);
            return;
        }

        _safetyTimer -= Time.deltaTime;
        if (_safetyTimer <= 0f) Destroy(gameObject);
    }

    private bool IsOffScreen()
    {
        if (_camera == null) return false;

        Vector3 viewport = _camera.WorldToViewportPoint(transform.position);
        if (viewport.z < 0f) return true; // 카메라 뒤로 지나감

        return viewport.x < -VIEWPORT_MARGIN || viewport.x > 1f + VIEWPORT_MARGIN
            || viewport.y < -VIEWPORT_MARGIN || viewport.y > 1f + VIEWPORT_MARGIN;
    }
}
