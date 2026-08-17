using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 커서를 올리면 <b>적어둔 문구</b>를 설명창에 띄우는 범용 안내 툴팁.
/// 옵션·견본덱·스테이지 같은 버튼에 붙여 "이게 뭐 하는 버튼인지" 알려줄 때 쓴다.
///
/// 붙이고 <see cref="_text"/>에 문구만 적으면 끝이다 — 오브젝트마다 이벤트를 손으로 엮을 필요가 없다.
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
    [Tooltip("커서를 올렸을 때 보여줄 문구. 줄바꿈도 그대로 나온다.")]
    [TextArea(1, 4)]
    [SerializeField] private string _text;

    [Tooltip("설명창 컨트롤러. 비워두면 씬에서 찾는다 — 보통은 비워둬도 된다.")]
    [SerializeField] private RoleTooltipController _tooltip;

    private void Awake()
    {
        if (_tooltip == null)
            _tooltip = FindFirstObjectByType<RoleTooltipController>(FindObjectsInactive.Include);

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
        if (_tooltip == null || string.IsNullOrWhiteSpace(_text)) return;

        // 소유자를 자신으로 넘긴다 — 여러 버튼을 빠르게 훑어도 남의 툴팁을 끄지 않는다.
        _tooltip.Show(this, _text);
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
