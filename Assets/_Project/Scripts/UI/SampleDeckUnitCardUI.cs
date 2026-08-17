using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 견본덱 상세(2페이지) 배치도의 유닛 한 칸. 아이콘 위에 성급 뱃지를 얹고,
/// <b>핵심 유닛에만</b> 아래에 아이템 아이콘을 붙인다.
///
/// 아이콘 자체는 게임 안에서 쓰는 <see cref="SynergyTooltipUnitSlot"/>(UnitSlot_Pf)에 맡긴다 —
/// 코스트별 테두리 색 규칙을 견본덱만 따로 갖지 않기 위해서다. 이 컴포넌트가 더하는 것은
/// 성급 뱃지·핵심 아이템 줄·호버 알림 세 가지뿐이다.
///
/// 커서가 올라오면 <see cref="Hovered"/>만 발행하고 설명창은 직접 열지 않는다. 여닫기는 이 칸을
/// 소유한 <see cref="SampleDeckPanelUI"/>가 <see cref="SampleDeckUnitTooltipUI"/>에 넘긴다
/// (인벤토리·아이템 상점이 ItemTooltipController를 쓰는 것과 같은 구조).
///
/// ⚠️ 호버가 잡히려면 <b>이 루트에 Raycast Target이 켜진 Graphic</b>이 하나 있어야 한다
/// (알파 0인 Image면 충분하다) — 자식 아이콘 위에서 들어온 이벤트가 이 루트로 올라온다.
/// </summary>
public class SampleDeckUnitCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("유닛 아이콘 칸(UnitSlot_Pf 인스턴스). 코스트 테두리는 이쪽이 알아서 칠한다.")]
    [SerializeField] private SynergyTooltipUnitSlot _slot;

    [Tooltip("아이콘 위 성급 뱃지. 1~3성 스프라이트를 갈아끼운다.")]
    [SerializeField] private Image _starBadge;

    [Tooltip("성급 스프라이트. 인덱스 0 = 1성 (Art/UI/Info/panel_1~3star). " +
             "칸이 비면 그 성급에서는 뱃지가 꺼진다.")]
    [SerializeField] private Sprite[] _starSprites = new Sprite[3];

    [Tooltip("유닛 이름. 배치도가 좁으면 비워도 된다.")]
    [SerializeField] private TMP_Text _nameText;

    [Header("핵심 아이템")]
    [Tooltip("아이템 아이콘이 깔리는 부모. 핵심 유닛이 아니면 오브젝트째 꺼진다.")]
    [SerializeField] private RectTransform _itemArea;

    [Tooltip("복제할 아이템 칸(배경 + 아이콘). 부모 아래 비활성으로 둘 것.")]
    [SerializeField] private SampleDeckItemSlotUI _itemSlotTemplate;

    /// <summary>이 칸이 그리고 있는 포켓몬. 비었으면 null.</summary>
    public PokemonData Data { get; private set; }

    /// <summary>이 칸에 붙은 핵심 아이템. 핵심 유닛이 아니면 비어 있다.</summary>
    public IReadOnlyList<ScriptableObject> Items => _items;

    /// <summary>커서가 들고 날 때(true=들어옴).</summary>
    public event Action<SampleDeckUnitCardUI, bool> Hovered;

    /// <summary>
    /// 이 칸의 <b>아이템 아이콘</b>에 커서가 들고 날 때. 유닛 설명창과 아이템 설명창은 겹치면
    /// 안 되므로, 여닫기 판단은 두 창을 다 쥐고 있는 <see cref="SampleDeckPanelUI"/>가 한다.
    /// </summary>
    public event Action<SampleDeckUnitCardUI, SampleDeckItemSlotUI, bool> ItemHovered;

    /// <summary>지금 커서가 이 칸 위에 있는지. 아이템에서 커서가 빠졌을 때 유닛 설명창을
    /// 되살릴지 판단하는 데 쓴다(아이템 칸은 이 칸 안에 있어서 둘 다 "들어온" 상태다).</summary>
    public bool IsHovered { get; private set; }

    private readonly List<SampleDeckItemSlotUI> _itemSlots = new();
    private readonly List<ScriptableObject> _items = new();

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
        // 커서를 올린 채 칸이 꺼지면 PointerExit이 오지 않아 설명창만 화면에 남는다.
        IsHovered = false;
        Hovered?.Invoke(this, false);
    }

    /// <summary>
    /// 칸 하나를 채운다. coreItems가 비어 있거나 null이면 아이템 줄이 통째로 꺼진다 —
    /// 핵심 유닛 2~3명만 아이템을 보여주는 규칙이 여기서 갈린다.
    /// </summary>
    public void Bind(PokemonData pokemon, int starLevel, IReadOnlyList<ScriptableObject> coreItems)
    {
        Data = pokemon;

        // placed:true 고정 — 목표 조합이라 "지금 보드에 있나"로 흑백 처리할 일이 없다.
        if (_slot != null) _slot.Bind(pokemon, true);

        if (_nameText != null)
        {
            _nameText.text =
                pokemon != null && !string.IsNullOrWhiteSpace(pokemon.pokemonName)
                    ? pokemon.pokemonName
                    : "";
        }

        BindStar(starLevel);
        BindItems(coreItems);
    }

    private void BindStar(int starLevel)
    {
        if (_starBadge == null) return;

        Sprite sprite = StarSpriteOf(starLevel);

        _starBadge.sprite = sprite;
        _starBadge.gameObject.SetActive(sprite != null);
    }

    private Sprite StarSpriteOf(int starLevel)
    {
        if (_starSprites == null) return null;

        int index = starLevel - 1;
        return index >= 0 && index < _starSprites.Length ? _starSprites[index] : null;
    }

    private void BindItems(IReadOnlyList<ScriptableObject> coreItems)
    {
        _items.Clear();

        if (coreItems != null)
        {
            for (int i = 0; i < coreItems.Count; i++)
                if (coreItems[i] != null) _items.Add(coreItems[i]);
        }

        bool hasItems = _items.Count > 0;

        // 핵심 유닛이 아니면 줄째 끈다 — 빈 칸만 남으면 "아이템을 아직 안 정했다"처럼 보인다.
        if (_itemArea != null) _itemArea.gameObject.SetActive(hasItems);

        if (!hasItems)
        {
            SampleDeckPool.HideExtras(_itemSlots, 0);
            return;
        }

        for (int i = 0; i < _items.Count; i++)
        {
            SampleDeckItemSlotUI slot = SampleDeckPool.Take(
                _itemSlots, _itemSlotTemplate, _itemArea, i,
                created =>
                {
                    // 배치도·목록의 칸만 커서를 받는다(설명창 안의 같은 칸은 켜지 않는다).
                    created.EnableHover();
                    created.Hovered += HandleItemHovered;
                });

            if (slot == null) break;

            slot.Bind(_items[i]);
        }

        SampleDeckPool.HideExtras(_itemSlots, _items.Count);
    }

    private void HandleItemHovered(SampleDeckItemSlotUI slot, bool entered)
        => ItemHovered?.Invoke(this, slot, entered);
}
