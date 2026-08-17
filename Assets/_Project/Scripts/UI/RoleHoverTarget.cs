using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 역할군 아이콘 한 칸의 커서 감지기. 설명창은 직접 열지 않고 <see cref="Hovered"/>만 발행한다 —
/// 여닫기는 이 칸을 소유한 쪽(<see cref="RoleTooltipController"/>를 아는 쪽)이 처리한다.
/// 아이템 칸의 <see cref="ItemHoverTarget"/>과 같은 구조다.
///
/// 이 컴포넌트는 <b><see cref="StatInfoPanelUI"/>가 런타임에 붙인다</b> — 프리팹에 미리 달 필요 없다.
/// 커서를 받으려면 대상 Image의 <b>Raycast Target이 켜져 있어야</b> 하며, 붙이는 쪽이 보장한다.
/// </summary>
public class RoleHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>지금 커서가 이 칸 위에 있는지.</summary>
    public bool IsHovered { get; private set; }

    /// <summary>커서가 들고 날 때 발행(true=들어옴).</summary>
    public event Action<RoleHoverTarget, bool> Hovered;

    public void OnPointerEnter(PointerEventData eventData)
    {
        IsHovered = true;
        Hovered?.Invoke(this, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        IsHovered = false;
        Hovered?.Invoke(this, false);
    }

    private void OnDisable()
    {
        // 커서를 올린 채 창이 닫히면 PointerExit이 오지 않는다.
        // 화면에 설명창만 남지 않게 직접 나감을 알린다.
        if (!IsHovered) return;

        IsHovered = false;
        Hovered?.Invoke(this, false);
    }
}
