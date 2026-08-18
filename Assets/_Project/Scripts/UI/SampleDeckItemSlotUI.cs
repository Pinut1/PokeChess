using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 견본덱 화면의 아이템 한 칸. 배경 패널(루트의 Image)과 아이템 일러스트(자식)를 분리해,
/// 아이콘 뒤에 테두리·판을 깔 수 있게 한다.
///
/// 인벤토리 칸(<see cref="ItemSlotUI"/>)이 <c>_icon</c>과 <c>_itemView</c>를 나눠 갖는 것과 같은
/// 이유다 — uGUI는 자식이 항상 부모 위에 그려지므로, 배경을 두려면 <b>루트가 배경이고 아이콘이
/// 자식</b>이어야 한다.
///
/// <see cref="_icon"/>을 비워두면 루트의 Image를 아이콘으로 쓴다. 배경 패널을 아직 안 넣은
/// 상태(아이콘만 있는 칸)에서도 그대로 동작하라고 남겨둔 폴백이다.
/// ⚠️ 배경을 넣은 뒤에는 반드시 <see cref="_icon"/>을 물릴 것 — 안 물리면 배경 스프라이트가
/// 아이템 그림으로 덮어써진다(바로 눈에 띈다).
/// </summary>
public class SampleDeckItemSlotUI : MonoBehaviour
{
    [Tooltip("아이템 일러스트. 스프라이트는 코드가 갈아끼운다. " +
             "비워두면 루트의 Image를 아이콘으로 쓴다(배경 패널이 없는 칸).")]
    [SerializeField] private Image _icon;

    /// <summary>이 칸이 그리고 있는 항목(ItemData·EvolutionStoneData·ConsumableData). 비었으면 null.</summary>
    public ScriptableObject CurrentData { get; private set; }

    /// <summary>커서가 들고 날 때(true=들어옴). 설명창은 이 칸을 소유한 쪽이 연다.</summary>
    public event Action<SampleDeckItemSlotUI, bool> Hovered;

    private Image _resolvedIcon;

    // 커서 감지기. 인벤토리·상점 칸과 같은 컴포넌트를 런타임에 붙여 프리팹 배선을 늘리지 않는다
    // (StatInfoPanelUI.SetupItemHovers와 같은 방식).
    private ItemHoverTarget _hover;

    // Graphic이 없어 감지기를 못 붙인 경우에도 한 번만 경고하도록 시도 여부를 따로 둔다.
    private bool _hoverResolved;

    /// <summary>
    /// 이 칸이 커서를 받도록 켠다. <b>켠 칸만</b> <see cref="Hovered"/>를 발행한다.
    ///
    /// 기본이 꺼짐인 이유 — 설명창(<see cref="SampleDeckUnitTooltipUI"/>) 안에도 같은 칸이 쓰인다.
    /// 설명창은 커서를 따라다니므로 거기까지 Raycast Target을 켜면 설명창이 자기 커서를 가로채
    /// 열렸다 닫혔다를 반복할 수 있다. 실제로 손이 닿는 건 배치도·목록의 칸뿐이다.
    /// </summary>
    public void EnableHover()
    {
        EnsureHover();
        if (_hover != null) _hover.SetData(CurrentData);
    }

    /// <summary>
    /// 커서 감지기를 준비한다. 커서 이벤트가 오려면 <b>이 오브젝트에 Raycast Target이 켜진
    /// Graphic</b>이 있어야 한다 — 배경 패널이 있으면 그게 받고, 없으면 아이콘 Image가 받는다.
    /// 둘 다 없으면 감지기를 붙여봐야 아무 일도 일어나지 않으므로 알려준다.
    /// </summary>
    private void EnsureHover()
    {
        if (_hoverResolved) return;
        _hoverResolved = true;

        var graphic = GetComponent<Graphic>();

        if (graphic == null)
        {
            Debug.LogWarning(
                "[SampleDeckItemSlotUI] 이 칸에 Image가 없어 커서를 받을 수 없습니다 — " +
                "아이템 설명창이 뜨지 않습니다. 배경 패널(Image)을 루트에 두세요.", this);
            return;
        }

        graphic.raycastTarget = true;

        _hover = GetComponent<ItemHoverTarget>();
        if (_hover == null) _hover = gameObject.AddComponent<ItemHoverTarget>();

        _hover.Hovered += HandleHovered;
    }

    private void HandleHovered(ItemHoverTarget target, bool entered) => Hovered?.Invoke(this, entered);

    private void OnDestroy()
    {
        if (_hover != null) _hover.Hovered -= HandleHovered;
    }

    private Image Icon
    {
        get
        {
            if (_resolvedIcon == null)
                _resolvedIcon = _icon != null ? _icon : GetComponent<Image>();

            return _resolvedIcon;
        }
    }

    public void Bind(ScriptableObject data)
    {
        CurrentData = data;

        // 커서를 켜둔 칸만 감지기를 들고 있다(EnableHover). 안 켰으면 그냥 그림만 바꾼다.
        if (_hover != null) _hover.SetData(data);

        Image icon = Icon;
        if (icon == null) return;

        Sprite sprite = IconOf(data);

        icon.sprite = sprite;
        // 그림이 아직 없는 항목은 흰 사각형 대신 빈 칸으로 둔다(배경 패널은 그대로 남는다).
        icon.enabled = sprite != null;
    }

    /// <summary>
    /// 항목 종류별 아이콘. 견본덱 추천 칸에는 일반 장비뿐 아니라 <b>진화의 돌</b>도 올라온다
    /// (돌로 진화하는 유닛은 그 돌이 1순위 추천이라서). <see cref="ItemTooltipUI"/>와 같은 분기다.
    /// </summary>
    public static Sprite IconOf(ScriptableObject data)
    {
        switch (data)
        {
            case ItemData item: return item.icon;
            case EvolutionStoneData stone: return stone.icon;
            case ConsumableData consumable: return consumable.icon;
            default: return null;
        }
    }
}
