using TMPro;
using UnityEngine;

/// <summary>
/// TMP 텍스트 위로 밝은 띠가 왼쪽→오른쪽으로 훑고 지나가는 연출(롤체식).
///
/// 글자마다 정점 색을 갈아끼우는 방식이라 글자 수가 적은 표시용 텍스트에 적합하다
/// (보드 정원 "3/6" 처럼). 머티리얼 Face Speed X로 하는 UV 스크롤과 달리
/// 글자 단위로 끊어 보이고, 훑은 뒤 쉬는 간격을 줄 수 있다.
///
/// 기준색은 매 프레임 <see cref="TMP_Text.color"/>에서 다시 읽는다 —
/// 그래야 다른 스크립트가 색을 바꿔도(정원이 다 찼을 때 등) 따라간다.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class TextShimmer : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;

    [Header("반짝임")]
    [Tooltip("훑고 지나가는 띠의 색.")]
    [SerializeField] private Color _shimmerColor = Color.white;
    [Tooltip("기준색과 얼마나 섞을지. 1이면 완전히 띠 색으로 바뀐다.")]
    [Range(0f, 1f)]
    [SerializeField] private float _intensity = 0.8f;

    [Header("움직임")]
    [Tooltip("훑는 속도. 1이면 약 1초에 텍스트를 한 번 통과한다.")]
    [SerializeField] private float _speed = 1f;
    [Tooltip("띠의 폭(글자 수 대비 비율). 넓을수록 여러 글자가 동시에 밝아진다.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float _bandWidth = 0.35f;
    [Tooltip("한 번 훑은 뒤 쉬는 시간(초). 0이면 쉬지 않고 계속 돈다.")]
    [SerializeField] private float _restSeconds = 1.5f;

    private void Reset() => _text = GetComponent<TMP_Text>();

    private void Awake()
    {
        if (_text == null) _text = GetComponent<TMP_Text>();
    }

    // 다른 스크립트가 text/color를 바꾼 뒤에 덮어써야 하므로 LateUpdate에서 처리한다.
    private void LateUpdate()
    {
        if (_text == null) return;

        TMP_TextInfo info = _text.textInfo;
        if (info == null) return;

        int charCount = info.characterCount;
        if (charCount == 0) return;

        float speed = Mathf.Max(0.01f, _speed);
        float band  = Mathf.Max(0.05f, _bandWidth);

        // 한 주기 = 텍스트를 통과하는 시간 + 쉬는 시간.
        // head가 1을 넘어가면 띠가 글자 밖으로 나가 자연스럽게 사라진다.
        float sweepTime = 1f / speed;
        float cycle     = sweepTime + Mathf.Max(0f, _restSeconds);
        float head      = Mathf.Repeat(Time.time, cycle) * speed;

        Color32 baseColor = _text.color;
        bool touched = false;

        for (int i = 0; i < charCount; i++)
        {
            TMP_CharacterInfo ch = info.characterInfo[i];
            if (!ch.isVisible) continue;

            int matIndex = ch.materialReferenceIndex;
            if (matIndex >= info.meshInfo.Length) continue;

            Color32[] colors = info.meshInfo[matIndex].colors32;
            int vi = ch.vertexIndex;
            if (colors == null || vi + 3 >= colors.Length) continue;

            float pos = charCount > 1 ? i / (float)(charCount - 1) : 0f;

            // 띠 중심에서 멀어질수록 약해진다. 제곱해서 가장자리를 부드럽게.
            float falloff = Mathf.Clamp01(1f - Mathf.Abs(pos - head) / band);
            Color32 c = Color32.Lerp(baseColor, _shimmerColor, falloff * falloff * _intensity);

            colors[vi]     = c;
            colors[vi + 1] = c;
            colors[vi + 2] = c;
            colors[vi + 3] = c;
            touched = true;
        }

        if (touched)
            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }
}
