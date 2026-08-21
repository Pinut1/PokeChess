using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 커서를 올리면 <b>적어둔 문구</b>를 설명창에 띄우는 범용 안내 툴팁.
/// 옵션·견본덱·스테이지 같은 버튼에 붙여 "이게 뭐 하는 버튼인지" 알려줄 때 쓴다.
///
/// 붙이고 <see cref="_text"/>에 문구만 적으면 끝이다 — 오브젝트마다 이벤트를 손으로 엮을 필요가 없다.
///
/// 문구 어디에나 <b>{key}</b>를 쓰면 같은 오브젝트의 <see cref="ButtonHotkey"/>가 들고 있는
/// 단축키가 "[D]" 꼴로 들어간다. 인스펙터에서 키를 바꾸면 툴팁 문구도 같이 따라가므로
/// "[D]"라고 적어두고 키만 바꿔서 어긋나는 일이 없다. 단축키가 없으면 대괄호까지 통째로 빠진다.
///
/// 제목/꼬리 줄(<see cref="_title"/>·<see cref="_footer"/>)은 <b>선택</b>이다. 비워두면 예전처럼
/// 본문 한 덩어리만 나가고, 채우면 사이에 빈 줄을 넣어 이어 붙인다 — 새로고침 버튼 설명처럼
/// "제목 + 설명" 두 단으로 보여줘야 하는 곳에 쓴다. 숫자가 들어가서 값이 바뀌는 설명이라면
/// 이 컴포넌트 말고 <see cref="XpPurchaseTooltipTrigger"/>처럼 값을 읽어 오는 쪽을 쓸 것 —
/// 여기 적은 문구는 <b>고정</b>이라 밸런스 표가 바뀌어도 따라가지 않는다.
///
/// 표시는 <see cref="RoleTooltipController"/>를 그대로 빌려 쓴다. 그쪽은 역할군 대응표에 없는
/// 문자열이 오면 형식을 입히지 않고 원문 그대로 내보내므로, 아무 문구나 넣어도 그대로 나온다.
///
/// <see cref="ItemHoverTarget"/>·<see cref="RoleHoverTarget"/>과 달리 이벤트만 올리지 않고
/// 직접 설명창을 여닫는다 — 소유자가 따로 없는 고정 문구라 중개할 쪽이 없기 때문이다.
///
/// ⚠️ 커서를 받으려면 <b>Raycast Target이 켜진 Graphic</b>이 이 오브젝트에 있어야 한다.
/// 있으면 Awake에서 자동으로 켜주고, 하나도 없으면 경고를 남긴다.
/// </summary>
public class TextTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("첫 줄(제목). 비우면 제목 줄 없이 본문만 나온다. TMP 리치텍스트(<b> 등)를 그대로 쓸 수 있다.")]
    [TextArea(1, 2)]
    [SerializeField] private string _title;

    [Tooltip("커서를 올렸을 때 보여줄 문구. 줄바꿈도 그대로 나온다.")]
    [TextArea(1, 4)]
    [SerializeField] private string _text;

    [Tooltip("마지막 줄. 비우면 이 줄은 나오지 않는다.")]
    [TextArea(1, 2)]
    [SerializeField] private string _footer;

    [Tooltip("제목·본문·꼬리 사이에 넣을 줄바꿈 수. 1이면 바로 다음 줄, 2면 한 줄 띄운다. " +
             "제목과 꼬리를 모두 비워두면 쓰이지 않는다.")]
    [Range(1, 3)]
    [SerializeField] private int _blankLines = 2;

    [Tooltip("{key}에 쓸 단축키. 비우면 같은 오브젝트의 ButtonHotkey를 찾는다 — 보통은 비워두면 된다.")]
    [SerializeField] private ButtonHotkey _hotkey;

    [Tooltip("{key}를 감쌀 서식. {0}=키 이름. 예: \"[{0}]\", \"({0})\"")]
    [SerializeField] private string _keyLabelFormat = "[{0}]";

    [Tooltip("설명창 컨트롤러. 비워두면 씬에서 찾는다 — 보통은 비워둬도 된다.")]
    [SerializeField] private RoleTooltipController _tooltip;

    private void Awake()
    {
        // 씬에 설명창이 둘(상점용/스탯창용)이라 아무거나 잡으면 모양이 뒤바뀐다.
        // 여러 개면 경고를 남기는 공용 탐색을 쓴다.
        if (_hotkey == null) _hotkey = GetComponent<ButtonHotkey>();

        if (_tooltip == null) _tooltip = RoleTooltipController.FindInScene(this);

        EnsureRaycastTarget();
    }

    /// <summary>
    /// Raycast Target이 꺼져 있으면 커서 이벤트가 아예 오지 않는다 — 여기서 보장한다.
    /// Graphic이 하나도 없으면 아무리 붙여도 동작하지 않으므로 알려준다.
    /// </summary>
    private void EnsureRaycastTarget()
    {
        var graphic = GetComponent<Graphic>();

        if (graphic == null)
        {
            Debug.LogWarning(
                "[TextTooltipTrigger] 이 오브젝트에 Image·Text 같은 Graphic이 없어 커서 이벤트를 받을 수 없습니다. " +
                "그림이 있는 오브젝트에 붙이거나, 알파 0인 Image를 하나 추가하세요.", this);
            return;
        }

        graphic.raycastTarget = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_tooltip == null) return;

        string text = Compose();
        if (string.IsNullOrWhiteSpace(text)) return;

        // 소유자를 자신으로 넘긴다 — 여러 버튼을 빠르게 훑어도 남의 툴팁을 끄지 않는다.
        _tooltip.Show(this, text);
    }

    /// <summary>
    /// 제목·본문·꼬리를 빈 줄로 이어 붙인다. 비어 있는 칸은 통째로 건너뛰므로,
    /// 제목과 꼬리를 안 쓰면 결과가 <see cref="_text"/> 그대로다(기존 배선과 같은 출력).
    /// </summary>
    private string Compose()
    {
        if (string.IsNullOrWhiteSpace(_title) && string.IsNullOrWhiteSpace(_footer))
            return FillKey(_text);

        string separator = new('\n', Mathf.Clamp(_blankLines, 1, 3));

        var sb = new System.Text.StringBuilder();

        Append(sb, FillKey(_title), separator);
        Append(sb, FillKey(_text), separator);
        Append(sb, FillKey(_footer), separator);

        return sb.ToString();
    }

    /// <summary>
    /// {key}를 단축키 표기("[D]")로 바꾼다. 단축키가 안 붙어 있으면 빈 문자열로 지운다 —
    /// 없는 키를 "[]"로 남겨두면 오히려 눈에 걸린다.
    /// </summary>
    private string FillKey(string part)
    {
        if (string.IsNullOrEmpty(part)) return part;

        string label = _hotkey != null ? _hotkey.KeyLabel : string.Empty;

        string replacement =
            string.IsNullOrEmpty(label)           ? string.Empty :
            string.IsNullOrEmpty(_keyLabelFormat) ? label :
                                                    string.Format(_keyLabelFormat, label);

        return part.Replace("{key}", replacement);
    }

    private static void Append(System.Text.StringBuilder sb, string part, string separator)
    {
        if (string.IsNullOrWhiteSpace(part)) return;

        if (sb.Length > 0) sb.Append(separator);

        // 단축키가 빠지면 "새로고침 " 처럼 꼬리 공백이 남는다.
        sb.Append(part.TrimEnd());
    }

    public void OnPointerExit(PointerEventData eventData) => Close();

    private void OnDisable()
    {
        // 커서를 올린 채 버튼이 꺼지면 PointerExit이 오지 않아 설명창만 화면에 남는다.
        Close();
    }

    private void Close()
    {
        if (_tooltip != null) _tooltip.Hide(this);
    }
}
