using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 아이템 설명창의 표시부. 데이터를 받아 아이콘/이름/효과설명만 채운다.
/// 여닫기와 자리잡기는 <see cref="ItemTooltipController"/>가 맡는다
/// (시너지 툴팁의 UI/Controller 분리와 같은 구조).
///
/// 인벤토리 칸에는 일반 장비 말고 진화의 돌·도구도 올라오므로,
/// 표시에 필요한 세 값만 타입별로 꺼내 쓴다 — ItemSlotUI.Bind와 같은 분기다.
///
/// 프리팹 구조(ItemTooltip_Pf):
///   ItemTooltip_Pf     Image(배경) + VerticalLayoutGroup + ContentSizeFitter
///     Header_Panel     HorizontalLayoutGroup
///       Item_Image     아이콘 배경 프레임
///         Item_Image(1)  실제 아이콘  ← Icon에 물릴 것
///       NameText
///     LineImage
///     ItemInfo_text
/// </summary>
public class ItemTooltipUI : MonoBehaviour
{
    [Tooltip("아이템 일러스트. 배경 프레임이 아니라 그 자식(Item_Image (1))을 물릴 것.")]
    [SerializeField] private Image _icon;

    [Tooltip("아이템 이름(NameText).")]
    [SerializeField] private TextMeshProUGUI _nameText;

    [Tooltip("효과 설명(ItemInfo_text). 설명이 없으면 이 줄은 꺼진다.")]
    [SerializeField] private TextMeshProUGUI _descriptionText;

    /// <summary>지금 그리고 있는 데이터. 컨트롤러가 같은 항목인지 판단할 때 쓴다.</summary>
    public ScriptableObject CurrentData { get; private set; }

    /// <summary>자리를 잡는 쪽(컨트롤러)이 쓰는 루트 RectTransform.</summary>
    public RectTransform Rect { get; private set; }

    private void Awake() => Rect = transform as RectTransform;

    /// <summary>표시할 항목을 채운다. 지원하지 않는 타입이면 false — 컨트롤러가 열지 않는다.</summary>
    public bool Bind(ScriptableObject data)
    {
        CurrentData = data;
        if (data == null) return false;

        string name;
        string description;
        Sprite sprite;

        switch (data)
        {
            case ItemData item:
                name = item.itemName;
                description = item.description;
                sprite = item.icon;
                break;

            case EvolutionStoneData stone:
                name = stone.stoneName;
                description = stone.description;
                sprite = stone.icon;
                break;

            case ConsumableData consumable:
                name = consumable.consumableName;
                description = consumable.description;
                sprite = consumable.icon;
                break;

            default:
                CurrentData = null;
                return false;
        }

        if (_icon != null)
        {
            _icon.sprite = sprite;
            _icon.enabled = sprite != null;
        }

        if (_nameText != null) _nameText.text = name;

        if (_descriptionText != null)
        {
            bool hasDescription = !string.IsNullOrWhiteSpace(description);

            // 줄을 끄면 VerticalLayoutGroup이 자리를 접어 창이 그만큼 짧아진다.
            _descriptionText.gameObject.SetActive(hasDescription);
            if (hasDescription) _descriptionText.text = description;
        }

        return true;
    }
}
