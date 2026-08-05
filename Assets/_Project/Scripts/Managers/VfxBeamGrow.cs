using UnityEngine;

/// <summary>
/// VfxEntry.beamGrowDuration > 0일 때 전용 — Stretched Billboard 파티클의 lengthScale을
/// 0에서 목표 배율까지 duration 동안 애니메이션한다("빔이 뻗어나가는" 연출).
/// 오브젝트 자체는 이동하지 않는다(caster 위치 고정) — StretchBeam과 동일 기준(Stretch 렌더모드)
/// 으로 렌더러를 고른다.
/// </summary>
public class VfxBeamGrow : MonoBehaviour
{
    private ParticleSystemRenderer[] _renderers;
    private float[] _targetLengthScale;
    private float _duration;
    private float _elapsed;

    /// <summary>targetRatio = distance / referenceDistance (SpawnAimed가 계산해서 넘김).</summary>
    public void Begin(float targetRatio, float duration)
    {
        _renderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
        _targetLengthScale = new float[_renderers.Length];

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i].renderMode != ParticleSystemRenderMode.Stretch) continue;
            _targetLengthScale[i] = _renderers[i].lengthScale * targetRatio;
            _renderers[i].lengthScale = 0f; // 길이 0에서 시작
        }

        _duration = Mathf.Max(0.01f, duration);
        _elapsed = 0f;
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null || _renderers[i].renderMode != ParticleSystemRenderMode.Stretch) continue;
            _renderers[i].lengthScale = Mathf.Lerp(0f, _targetLengthScale[i], t);
        }

        if (t >= 1f) Destroy(this); // 다 늘어났으면 컴포넌트만 정리, 오브젝트(빔)는 유지
    }
}
