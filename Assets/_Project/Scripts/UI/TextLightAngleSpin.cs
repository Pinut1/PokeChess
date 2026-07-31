using TMPro;
using UnityEngine;

/// <summary>
/// TMP 머티리얼의 Local Lighting → Light Angle을 계속 회전시킨다.
/// 글자 입체면에 닿는 빛의 방향이 돌아가면서 광택이 훑고 지나가는 연출이 된다.
///
/// ⚠️ 머티리얼의 <b>Bevel이 켜져 있어야</b> 보인다 — TMP_SDF 셰이더의 조명 계산이
/// 통째로 <c>#if BEVEL_ON</c> 안에 있어서, 꺼져 있으면 각도를 돌려도 화면이 그대로다.
/// PokeChess/UI/TMP 발광 프리셋 만들기로 만든 머티리얼은 이미 켜져 있다.
///
/// 머티리얼은 공유본이 아니라 인스턴스(TMP_Text.fontMaterial)를 잡는다 —
/// 같은 프리셋을 쓰는 다른 텍스트까지 같이 도는 것을 막기 위해서다.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class TextLightAngleSpin : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;

    [Tooltip("빛이 한 바퀴 도는 데 걸리는 시간(초). 작을수록 빠르다.")]
    [SerializeField] private float _secondsPerTurn = 4f;

    [Tooltip("시계 방향으로 돌릴지.")]
    [SerializeField] private bool _clockwise = true;

    private Material _material;

    private void Reset() => _text = GetComponent<TMP_Text>();

    private void Awake()
    {
        if (_text == null) _text = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (_text == null) return;

        _material = _text.fontMaterial; // 접근하는 순간 인스턴스가 만들어진다

        if (_material != null && !_material.IsKeywordEnabled("BEVEL_ON"))
        {
            Debug.LogWarning(
                $"[TextLightAngleSpin] '{name}'의 머티리얼에 Bevel이 꺼져 있어 " +
                "Light Angle을 돌려도 보이지 않습니다. 머티리얼 인스펙터의 Bevel을 켜세요.",
                this);
        }
    }

    private void Update()
    {
        if (_material == null) return;

        float turns = _secondsPerTurn > 0f ? Time.time / _secondsPerTurn : 0f;
        if (!_clockwise) turns = -turns;

        // 셰이더의 _LightAngle은 0~2π 라디안 범위다.
        float angle = Mathf.Repeat(turns, 1f) * Mathf.PI * 2f;

        _material.SetFloat(ShaderUtilities.ID_LightAngle, angle);
    }
}
