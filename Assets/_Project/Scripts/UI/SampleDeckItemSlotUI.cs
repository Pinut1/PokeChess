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

    private Image _resolvedIcon;

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
