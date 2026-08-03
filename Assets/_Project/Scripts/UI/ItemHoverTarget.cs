using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 아이템 아이콘 한 칸의 커서 감지기. 표시할 데이터만 들고 있고 설명창은 직접 열지 않는다 —
/// 여닫기는 이 칸을 소유한 쪽(ItemTooltipController를 아는 쪽)이 <see cref="Hovered"/>를 받아 처리한다.
/// ItemSlotUI(인벤토리)·ItemCardUI(아이템 상점)가 매니저를 직접 참조하지 않는 것과 같은 구조다.
///
/// 이 컴포넌트는 <b>상세창(StatInfoPanelUI)이 런타임에 아이콘마다 붙인다</b> — 프리팹에 미리 달아둘 필요는 없다.
/// 커서를 받으려면 아이콘 Image의 <b>Raycast Target이 켜져 있어야</b> 하며, 붙이는 쪽에서 보장한다.
///
/// 빈 칸에서는 아무 일도 일어나지 않게 <see cref="Data"/>가 null이면 진입 이벤트를 발행하지 않는다.
/// </summary>
public class ItemHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>이 칸이 표시 중인 항목(ItemData·EvolutionStoneData·ConsumableData). 비었으면 null.</summary>
    public ScriptableObject Data { get; private set; }

    /// <summary>지금 커서가 이 칸 위에 있는지. 내용이 바뀔 때 설명창을 다시 띄울지 판단하는 데 쓴다.</summary>
    public bool IsHovered { get; private set; }

    /// <summary>커서가 들고 날 때 발행(true=들어옴).</summary>
    public event Action<ItemHoverTarget, bool> Hovered;

    public void SetData(ScriptableObject data) => Data = data;

    public void OnPointerEnter(PointerEventData eventData)
    {
        IsHovered = true;
        if (Data != null) Hovered?.Invoke(this, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        IsHovered = false;
        Hovered?.Invoke(this, false);
    }

    private void OnDisable()
    {
        // 커서를 올린 채 칸이 꺼지면(상세창이 닫히는 등) PointerExit이 오지 않는다.
        // 화면에 설명창만 남지 않게 직접 나감을 알린다.
        if (!IsHovered) return;

        IsHovered = false;
        Hovered?.Invoke(this, false);
    }
}
