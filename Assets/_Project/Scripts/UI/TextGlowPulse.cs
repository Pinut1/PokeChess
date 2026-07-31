using TMPro;
using UnityEngine;

/// <summary>
/// TMP 머티리얼의 Glow Power를 사인파로 오르내려 발광이 숨쉬듯 깜빡이게 한다.
///
/// ⚠️ 머티리얼의 <b>Glow가 켜져 있어야</b> 보인다(GLOW_ON 키워드).
/// PokeChess/UI/TMP 발광 프리셋 만들기로 만든 머티리얼은 이미 켜져 있다.
///
/// 색(_GlowColor)은 건드리지 않으므로 <see cref="BoardCapacityLabel"/>의
/// "가득 찼을 때 색 전환"과 같이 써도 서로 방해하지 않는다.
/// 머티리얼은 인스턴스(TMP_Text.fontMaterial)를 잡아 다른 텍스트에 번지지 않게 한다.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class TextGlowPulse : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;

    [Tooltip("가장 약할 때의 Glow Power.")]
    [Range(0f, 1f)]
    [SerializeField] private float _minPower = 0.1f;

    [Tooltip("가장 강할 때의 Glow Power.")]
    [Range(0f, 1f)]
    [SerializeField] private float _maxPower = 0.5f;

    [Tooltip("한 번 밝아졌다 어두워지는 데 걸리는 시간(초).")]
    [SerializeField] private float _secondsPerCycle = 2f;

    private Material _material;
    private bool _ready;

    private void Reset() => _text = GetComponent<TMP_Text>();

    private void Awake()
    {
        if (_text == null) _text = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        _ready = false;
        if (_text == null) return;

        _material = _text.fontMaterial; // 접근하는 순간 인스턴스가 만들어진다
        if (_material == null) return;

        if (!_material.IsKeywordEnabled("GLOW_ON"))
        {
            Debug.LogWarning(
                $"[TextGlowPulse] '{name}'의 머티리얼에 Glow가 꺼져 있어 " +
                "깜빡임이 보이지 않습니다. 머티리얼 인스펙터의 Glow를 켜세요.",
                this);
            return;
        }

        _ready = true;
    }

    private void OnDisable()
    {
        // 꺼질 때 중간값에서 멈추지 않도록 가장 약한 밝기로 가라앉힌다.
        // (보드 정원이 가득 차면 이 컴포넌트가 꺼지므로, 깜빡임이 멎고 발광이 잦아든다.)
        if (_ready && _material != null)
            _material.SetFloat(ShaderUtilities.ID_GlowPower, _minPower);
    }

    private void Update()
    {
        if (!_ready) return;

        // 삼각파(PingPong)보다 사인이 부드러워 "은은하게 숨쉬는" 느낌에 맞는다.
        float cycle = Mathf.Max(0.01f, _secondsPerCycle);
        float t = (Mathf.Sin(Time.time / cycle * Mathf.PI * 2f) + 1f) * 0.5f;

        _material.SetFloat(ShaderUtilities.ID_GlowPower,
                           Mathf.Lerp(_minPower, _maxPower, t));
    }
}
