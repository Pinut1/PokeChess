using TMPro;
using UnityEngine;

/// <summary>
/// 닉네임 길이에 맞춰 뒤에 깔린 판(NicknamePanel_My / NicknamePanel_Partner)의 <b>가로 폭만</b>
/// 늘였다 줄였다 한다. 롤체 플레이어 목록처럼 짧은 닉네임엔 짧은 판, 긴 닉네임엔 긴 판이 붙는다.
///
/// HorizontalLayoutGroup + ContentSizeFitter를 쓰지 않는 이유 — 지금 줄 구성이
/// <c>NicknamePanel → Profile_Image → (Nickname_Text, 프로필 버튼)</c>으로 <b>손으로 겹쳐 놓은</b>
/// 구조라, 레이아웃 그룹을 걸면 직속 자식만 다루면서 배치를 통째로 다시 잡아버린다. 지금 배치를
/// 그대로 두고 폭만 따라가게 하는 쪽이 안전해서 폭 계산만 하는 작은 컴포넌트로 뒀다.
///
/// <b>붙이기 전에 인스펙터에서 맞춰둘 것</b> (한 번만 하면 된다):
///   1. 판(NicknamePanel_*)의 Pivot X = 1 — 폭이 늘 때 <b>왼쪽으로</b> 자라고 오른쪽 가장자리가
///      제자리에 남는다. Pivot을 0.5로 두고 싶으면 <see cref="_keepRightEdge"/>를 켜면 같은 결과가
///      나온다(스크립트가 위치를 보정한다).
///   2. Profile_Image의 Anchor를 오른쪽(Min=Max=(1, 0.5))으로 — 판의 <b>오른쪽 가장자리 기준</b>이
///      되어야 판이 늘어나도 아이콘이 제자리에 남는다. 가운데 앵커(0.5)로 두면 판 폭이 바뀔 때마다
///      아이콘이 같이 밀린다.
///   3. (선택) Nickname_Text의 정렬. 글자 박스를 글자 길이에 딱 맞춰 잡아주므로 박스 안 정렬은
///      보이는 결과에 영향이 없다 — 왼쪽 정렬로 둬도 된다. 박스가 자라는 방향은 Pivot과 무관하게
///      항상 왼쪽이다(오른쪽 끝 고정).
///
/// 줄바꿈은 Awake에서 꺼준다 — 켜져 있으면 긴 닉네임이 두 줄로 접혀 preferredWidth가 판 폭이
/// 아니라 접힌 폭으로 잡힌다(폭이 안 늘어나는 것처럼 보이는 원인).
/// </summary>
public class NicknamePanelFitter : MonoBehaviour
{
    [Tooltip("길이를 잴 닉네임 텍스트. 비워두면 자식에서 찾는다.")]
    [SerializeField] private TMP_Text _nicknameText;

    [Tooltip("폭을 늘였다 줄일 판. 비워두면 이 컴포넌트가 붙은 오브젝트의 RectTransform.")]
    [SerializeField] private RectTransform _panel;

    [Tooltip("글자 폭에 항상 더할 폭. 프로필 아이콘이 차지하는 자리 + 좌우 여백을 합친 값이다.\n" +
             "판을 원하는 모양으로 맞춰 놓고, (지금 판 폭 − 지금 글자 폭)을 넣으면 그 모양이 유지된다.")]
    [SerializeField] private float _extraWidth = 140f;

    [Tooltip("판이 이보다 좁아지지 않는다.\n" +
             "0(기본)이면 인스펙터에 잡아둔 시작 폭을 그대로 최소폭으로 쓴다 — 닉네임이 짧아도 지금 " +
             "모양보다 좁아지지 않고, 길 때만 늘어난다.")]
    [SerializeField] private float _minWidth;

    [Tooltip("판이 이보다 넓어지지 않는다. 0이면 제한 없음. 닉네임은 16자로 이미 제한돼 있어(NetworkManager) " +
             "보통은 안 걸리지만, 화면 밖으로 나가는 것만 막아두는 안전장치.")]
    [SerializeField] private float _maxWidth = 600f;

    [Tooltip("폭이 바뀌어도 판의 오른쪽 가장자리를 제자리에 두고 왼쪽으로만 자라게 한다.\n" +
             "Pivot X가 이미 1이면 켜도 꺼도 결과가 같다.")]
    [SerializeField] private bool _keepRightEdge = true;

    [Tooltip("글자 박스 폭도 글자 길이에 맞춘다. 끄면 판만 늘어나고 글자 박스는 인스펙터 값 그대로 둔다.")]
    [SerializeField] private bool _resizeTextBox = true;

    // 마지막으로 폭을 계산했을 때의 문자열. 같은 문자열이면 preferredWidth를 다시 재지 않는다
    // (TMP는 이 값을 읽을 때마다 레이아웃을 다시 계산한다).
    private string _measuredText;

    // 실제로 쓰는 최소폭. _minWidth가 0이면 폭을 처음 건드리기 직전에 시작 폭을 여기에 담아
    // "짧아져도 지금보다는 좁아지지 않는다"를 만든다 — 인스펙터에 숫자를 따로 적어둘 필요가 없다.
    private float _resolvedMinWidth;

    private void Awake()
    {
        if (_nicknameText == null) _nicknameText = GetComponentInChildren<TMP_Text>(true);
        if (_panel == null) _panel = transform as RectTransform;

        if (_nicknameText == null || _panel == null)
        {
            Debug.LogWarning("[NicknamePanelFitter] 닉네임 텍스트나 판을 못 찾았습니다 — 폭 조절을 건너뜁니다.", this);
            enabled = false;
            return;
        }

        // 줄바꿈이 켜져 있으면 긴 닉네임이 접혀서 폭이 안 늘어난다(클래스 주석 참고).
        _nicknameText.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void OnEnable()
    {
        // 켜질 때는 문자열이 그대로여도 한 번은 다시 맞춘다(비활성 중에 닉네임이 바뀌었을 수 있다).
        _measuredText = null;
    }

    // 닉네임은 UserInfoPanelUI가 자기 Update에서 채운다. 실행 순서에 상관없이 같은 프레임에
    // 반영되도록 LateUpdate에서 확인한다 — 문자열이 그대로면 즉시 빠져나가므로 비용은 비교 한 번뿐이다.
    private void LateUpdate()
    {
        string current = _nicknameText.text;
        if (current == _measuredText) return;

        _measuredText = current;
        Fit();
    }

    /// <summary>인스펙터 값을 만지면서 결과를 바로 보고 싶을 때(플레이 중) 쓰는 수동 갱신.</summary>
    [ContextMenu("지금 폭 맞추기")]
    private void Fit()
    {
        if (_nicknameText == null || _panel == null) return;

        // 최소폭은 폭을 처음 건드리기 직전에 한 번만 정한다 — 그래야 인스펙터에 잡아둔 시작 폭이
        // 잡힌다. (에디터에서 "지금 폭 맞추기"로 먼저 부르는 경우까지 같은 경로로 처리된다.)
        if (_resolvedMinWidth <= 0f)
            _resolvedMinWidth = _minWidth > 0f ? _minWidth : _panel.rect.width;

        float textWidth = _nicknameText.preferredWidth;

        if (_resizeTextBox)
            ApplyWidth(_nicknameText.rectTransform, textWidth, keepRightEdge: true);

        // 닉네임이 짧으면 판을 줄이지 않고 시작 폭을 유지한다(_resolvedMinWidth) — 길 때만 늘어난다.
        float panelWidth = Mathf.Max(textWidth + _extraWidth, _resolvedMinWidth);
        if (_maxWidth > 0f) panelWidth = Mathf.Min(panelWidth, _maxWidth);

        ApplyWidth(_panel, panelWidth, _keepRightEdge);
    }

    /// <summary>
    /// RectTransform 폭을 바꾼다. keepRightEdge면 늘어난 만큼 위치를 왼쪽으로 밀어
    /// 오른쪽 가장자리를 제자리에 둔다(Pivot X가 1이면 보정값이 0이라 저절로 맞는다).
    ///
    /// 가로로 늘어난(Anchor Min.x ≠ Max.x) RectTransform은 sizeDelta가 폭이 아니라 여백이라
    /// 이 계산이 성립하지 않는다 — 그 경우는 건드리지 않고 넘어간다.
    /// </summary>
    private static void ApplyWidth(RectTransform rect, float width, bool keepRightEdge)
    {
        if (rect == null) return;
        if (!Mathf.Approximately(rect.anchorMin.x, rect.anchorMax.x)) return;

        float delta = width - rect.rect.width;
        if (Mathf.Approximately(delta, 0f)) return;

        rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);

        if (!keepRightEdge) return;

        var pos = rect.anchoredPosition;
        pos.x -= delta * (1f - rect.pivot.x);
        rect.anchoredPosition = pos;
    }
}
