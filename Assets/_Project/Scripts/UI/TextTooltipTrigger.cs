using System.Collections.Generic;
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
///
/// ── 실행 중 문구 갱신(<see cref="_id"/>) ──
/// 이 컴포넌트의 문구는 기본적으로 인스펙터에 적어둔 고정값이지만, 밸런스 수치를 바꾸는 다른
/// 코드(예: 경제 증강이 이자율을 바꾸는 경우)가 실행 중에 본문을 갈아끼워야 할 때가 있다.
/// 그런 자리에는 <see cref="_id"/>에 식별자를 적어두면, 다른 코드가
/// <see cref="TrySetBodyText"/>로 이 인스턴스를 찾아 <see cref="SetBodyText"/>를 호출할 수 있다.
/// 안 쓰는 툴팁은 <see cref="_id"/>를 비워두면 그만이다(기존 동작과 완전히 동일).
/// </summary>
public class TextTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("다른 코드가 실행 중에 본문을 갈아끼우려 할 때 찾는 식별자(선택). 비워두면 고정 문구만 쓰는 " +
             "기존 동작 그대로다. 쓰는 쪽 예: 경제 증강이 이자율 툴팁을 갱신할 때 \"GoldInterest\".")]
    [SerializeField] private string _id;

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

    // _id로 이 인스턴스를 찾을 수 있게 등록하는 레지스트리. 씬 전환 시 컴포넌트가 통째로 파괴/재생성되므로
    // OnEnable/OnDisable로 등록·해제한다(Awake/OnDestroy가 아님 — 오브젝트가 비활성화된 동안엔
    // TrySetBodyText가 이 인스턴스를 찾지 못하는 게 맞다. 어차피 안 보이는 툴팁 문구를 갱신해도 의미 없다).
    private static readonly Dictionary<string, TextTooltipTrigger> _registry = new();

    // 지금 이 툴팁이 화면에 떠 있는지. SetBodyText가 호출됐을 때 이미 열려 있는 툴팁이면 즉시
    // 다시 그려서(재호출), 커서를 올린 채로 값이 바뀌어도 다음에 뗐다 올릴 때까지 기다리지 않는다.
    private bool _isOpen;

    private void Awake()
    {
        // 씬에 설명창이 둘(상점용/스탯창용)이라 아무거나 잡으면 모양이 뒤바뀐다.
        // 여러 개면 경고를 남기는 공용 탐색을 쓴다.
        if (_hotkey == null) _hotkey = GetComponent<ButtonHotkey>();

        if (_tooltip == null) _tooltip = RoleTooltipController.FindInScene(this);

        EnsureRaycastTarget();
    }

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(_id)) return;

        if (_registry.TryGetValue(_id, out var existing) && existing != this)
            Debug.LogWarning($"[TextTooltipTrigger] id \"{_id}\" 중복 — 이전 등록({existing.name})을 덮어씁니다. " +
                              "TrySetBodyText는 항상 마지막으로 활성화된 인스턴스만 찾습니다.", this);

        _registry[_id] = this;
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
        _isOpen = true;
    }

    /// <summary>
    /// 제목·본문·꼬리를 빈 줄로 이어 붙인다. 비어 있는 칸은 통째로 건너뛰므로,
    /// 제목과 꼬리를 안 쓰면 결과가 <see cref="_text"/> 그대로다(기존 배선과 같은 출력).
    /// </summary>
    private string Compose()
    {
        if (string.IsNullOrWhiteSpace(_title) && string.IsNullOrWhiteSpace(_footer))
            return FillKey(_text);

        string separator = TooltipText.Separator(_blankLines);

        var sb = new System.Text.StringBuilder();

        TooltipText.Append(sb, FillKey(_title), separator);
        TooltipText.Append(sb, FillKey(_text), separator);
        TooltipText.Append(sb, FillKey(_footer), separator);

        return sb.ToString();
    }

    /// <summary>
    /// {key}를 단축키 표기("[D]")로 바꾼다. 단축키가 안 붙어 있으면 빈 문자열로 지운다 —
    /// 없는 키를 "[]"로 남겨두면 오히려 눈에 걸린다.
    /// </summary>
    private string FillKey(string part)
    {
        if (string.IsNullOrEmpty(part)) return part;

        string key = _hotkey != null ? _hotkey.KeyLabel : string.Empty;

        return part.Replace("{key}", TooltipText.KeyLabel(key, _keyLabelFormat));
    }

    public void OnPointerExit(PointerEventData eventData) => Close();

    private void OnDisable()
    {
        // 커서를 올린 채 버튼이 꺼지면 PointerExit이 오지 않아 설명창만 화면에 남는다.
        Close();

        // 비활성 인스턴스는 TrySetBodyText가 찾지 못하게 등록 해제한다(안 보이는 툴팁 문구를
        // 갱신해도 의미가 없다 — 클래스 doc 참고). OnEnable에서 다시 등록된다.
        if (!string.IsNullOrEmpty(_id) && _registry.TryGetValue(_id, out var existing) && existing == this)
            _registry.Remove(_id);
    }

    private void Close()
    {
        _isOpen = false;
        if (_tooltip != null) _tooltip.Hide(this);
    }

    /// <summary>
    /// 본문(<see cref="_text"/>)을 실행 중에 갈아끼운다. 이 툴팁이 지금 화면에 떠 있으면(<see cref="_isOpen"/>)
    /// 즉시 다시 그려 반영한다 — 아니면 다음에 커서를 올릴 때 새 문구로 뜬다.
    /// </summary>
    public void SetBodyText(string text)
    {
        _text = text;

        if (!_isOpen || _tooltip == null) return;

        string composed = Compose();
        if (string.IsNullOrWhiteSpace(composed))
        {
            Close();
            return;
        }

        _tooltip.Show(this, composed);
    }

    /// <summary>
    /// <see cref="_id"/>가 일치하는 인스턴스를 찾아 <see cref="SetBodyText"/>를 호출한다. 씬에 없거나
    /// 비활성이면(등록 해제됨) false — 조용히 실패한다(씬 전환 중 등, 크래시할 일이 아니다).
    /// </summary>
    public static bool TrySetBodyText(string id, string text)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (!_registry.TryGetValue(id, out var target) || target == null) return false;

        target.SetBodyText(text);
        return true;
    }
}
