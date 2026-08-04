using UnityEngine;

/// <summary>
/// VfxAimMode.TravelToTarget 전용 — 부착된 오브젝트를 from에서 to까지 duration 동안 실제로
/// 이동시키고, 도착하면 arrivalHold 동안 멈춰 있다가 스스로 파괴한다.
/// 파티클 내부 설정(Start Speed 등)과 무관하게 어떤 프리팹이든 동작한다.
/// </summary>
public class VfxTravelMover : MonoBehaviour
{
    private Vector3 _from;
    private Vector3 _to;
    private float _duration;
    private float _arrivalHold;
    private float _elapsed;
    private bool _arrived;

    /// <summary>이동 시작. BattleVfxPlayer가 Create() 직후 1회만 호출한다.</summary>
    public void Begin(Vector3 from, Vector3 to, float duration, float arrivalHold)
    {
        _from = from;
        _to = to;
        _duration = Mathf.Max(0.01f, duration);
        _arrivalHold = Mathf.Max(0f, arrivalHold);
        _elapsed = 0f;
        _arrived = false;
        transform.position = _from;
    }

    private void Update()
    {
        if (_arrived)
        {
            _arrivalHold -= Time.deltaTime;
            if (_arrivalHold <= 0f) Destroy(gameObject);
            return;
        }

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);
        transform.position = Vector3.Lerp(_from, _to, t);

        if (t >= 1f)
        {
            transform.position = _to;
            _arrived = true;
            if (_arrivalHold <= 0f) Destroy(gameObject); // 유지시간 0이면 도착 즉시 파괴
        }
    }
}
