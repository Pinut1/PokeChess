using System.Collections.Generic;
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
///     UnitText (TMP)   "이 돌로 진화하는 포켓몬" 같은 고정 문구  ← Unit Label
///     UnitImage_Panel  GridLayoutGroup                        ← Unit Slot Root
///       UnitImage      코스트 테두리(칸 하나)                  ← Unit Slot Prefab
///         Unit_ItemImage  포켓몬 아이콘(PokemonData.unitIcon = 일러스트의 _1 스프라이트)
///
/// 아래 두 줄(UnitText·UnitImage_Panel)은 <b>진화의 돌일 때만</b> 켠다. 다른 아이템에서는
/// 오브젝트째 꺼지므로 VerticalLayoutGroup이 자리를 접어 창이 그만큼 짧아진다.
///
/// 한 칸은 시너지 툴팁과 같은 <see cref="SynergyTooltipUnitSlot"/>을 그대로 쓴다 —
/// "코스트별 테두리 + 유닛 아이콘"이 정확히 같은 표시라 따로 만들 이유가 없다.
/// (SynergyTooltip_Pf는 UnitSlot_Pf를 칸 프리팹으로 물려 쓴다)
/// </summary>
public class ItemTooltipUI : MonoBehaviour
{
    [Tooltip("아이템 일러스트. 배경 프레임이 아니라 그 자식(Item_Image (1))을 물릴 것.")]
    [SerializeField] private Image _icon;

    [Tooltip("아이템 이름(NameText).")]
    [SerializeField] private TextMeshProUGUI _nameText;

    [Tooltip("효과 설명(ItemInfo_text). 설명이 없으면 이 줄은 꺼진다.")]
    [SerializeField] private TextMeshProUGUI _descriptionText;

    [Header("진화의 돌 전용 — 적용 대상 유닛")]
    [Tooltip("아이콘 줄 위의 고정 문구(UnitText). 문구는 프리팹에 적어둔 것을 그대로 쓰고 " +
             "코드는 켜고 끄기만 한다 — 돌마다 문구가 달라질 일이 없어서다.")]
    [SerializeField] private GameObject _unitLabel;

    [Tooltip("아이콘 칸이 담기는 부모(UnitImage_Panel). GridLayoutGroup을 붙여둘 것.")]
    [SerializeField] private RectTransform _unitSlotRoot;

    [Tooltip("칸이 모자랄 때 복제할 칸(UnitImage). 패널 아래 이미 깔아둔 칸을 그대로 물려도 된다 — " +
             "그 칸은 0번으로 재사용되고 복제는 2번째부터 일어난다. 비워두면 깔아둔 칸 수만큼만 표시된다.")]
    [SerializeField] private SynergyTooltipUnitSlot _unitSlotPrefab;

    [Tooltip("만들 수 있는 칸의 상한(폭주 방지). 현재 데이터의 최대는 3칸(불·번개·물·잎)이다.")]
    [SerializeField] private int _maxUnitSlots = 12;

    /// <summary>지금 그리고 있는 데이터. 컨트롤러가 같은 항목인지 판단할 때 쓴다.</summary>
    public ScriptableObject CurrentData { get; private set; }

    /// <summary>자리를 잡는 쪽(컨트롤러)이 쓰는 루트 RectTransform.</summary>
    public RectTransform Rect { get; private set; }

    // 미리 깔아둔 칸 + 필요해서 만든 칸. 한 번 만들면 파괴하지 않고 재사용한다.
    private readonly List<SynergyTooltipUnitSlot> _slots = new();

    // 같은 대상이 두 줄로 들어와도 아이콘은 한 번만(이브이처럼 돌 여러 개가 걸린 종 대비).
    private readonly HashSet<int> _seenIds = new();

    // 그릴 대상을 모았다가 코스트순으로 정렬해서 칸에 넣는다(시트 행 순서 그대로면 코스트가 섞인다).
    private readonly List<PokemonData> _targets = new();

    // 경고는 종류당 1회만(호버할 때마다 로그가 쏟아지지 않게).
    private bool _warnedSlotShortage;

    private void Awake()
    {
        Rect = transform as RectTransform;

        // 프리팹에 미리 깔아둔 칸을 먼저 회수한다(꺼둔 칸도 포함).
        if (_unitSlotRoot != null)
            _slots.AddRange(_unitSlotRoot.GetComponentsInChildren<SynergyTooltipUnitSlot>(true));
    }

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

        // 돌이 아니면 null이 들어가 아래 두 줄이 꺼진다.
        BindEvolutionTargets(data as EvolutionStoneData);

        return true;
    }

    // ─────────────────────────────────────────
    // 진화의 돌 — 이 돌을 쓸 수 있는 포켓몬
    // ─────────────────────────────────────────

    /// <summary>
    /// 돌의 진화 매핑(EvolutionMap 시트)에서 <b>진화 전</b> 포켓몬만 뽑아 아이콘 줄을 채운다.
    /// stone이 null이거나 한 마리도 못 찾으면 문구·아이콘 줄을 통째로 끈다.
    /// </summary>
    private void BindEvolutionTargets(EvolutionStoneData stone)
    {
        int shown = FillUnitSlots(stone);
        bool show = shown > 0;

        // 두 줄 다 오브젝트째 껐다 켠다 — VerticalLayoutGroup이 자리를 접어야
        // 일반 아이템 툴팁이 예전 높이 그대로 남지 않는다.
        if (_unitLabel != null) _unitLabel.SetActive(show);
        if (_unitSlotRoot != null) _unitSlotRoot.gameObject.SetActive(show);
    }

    /// <summary>
    /// 채운 칸 수. 남는 칸은 꺼서 그리드가 자리를 접게 한다.
    /// 표시 순서는 <b>저코스트 → 고코스트</b>다 — 시트(EvolutionMap) 행 순서를 그대로 쓰면
    /// 코스트가 뒤섞여 나온다. 같은 코스트끼리는 id순이라 매번 같은 자리에 온다.
    /// </summary>
    private int FillUnitSlots(EvolutionStoneData stone)
    {
        if (_unitSlotRoot == null) return 0;

        int shown = 0;
        _seenIds.Clear();
        _targets.Clear();

        // 돌이 아닌 아이템도 이 함수를 지나간다 — 이땐 조회 없이 전 칸을 끄기만 하면 된다.
        var db = stone != null ? PokemonDatabase.Instance : null;

        if (db != null && stone.mappings != null)
        {
            foreach (var mapping in stone.mappings)
            {
                if (mapping == null || string.IsNullOrWhiteSpace(mapping.targetPokemon)) continue;

                PokemonData target = db.GetByNameEn(mapping.targetPokemon);
                if (target == null) continue; // 시트 표기 불일치. 조용히 건너뛴다(QAManager가 따로 검증한다)

                // 이브이처럼 한 돌에 두 줄이 걸릴 여지가 있어 종 기준으로 한 번만 그린다.
                if (!_seenIds.Add(target.id)) continue;

                _targets.Add(target);
            }

            // 정렬을 칸에 넣기 전에 한다 — 넣으면서 정렬하면 상한에 걸렸을 때
            // 잘려나가는 쪽이 "고코스트"가 아니라 "시트 뒷줄"이 된다.
            _targets.Sort(CompareByCostThenId);

            foreach (var target in _targets)
            {
                var slot = SlotAt(shown);
                if (slot == null) break; // 칸 상한 도달 — 아래에서 한 번만 경고

                // placed:true 고정 — 시너지 툴팁과 달리 "지금 보드에 있나"는 상관없는 목록이라
                // 흑백 처리를 하지 않는다. 테두리 색(코스트)만 살린다.
                slot.Bind(target, true);
                shown++;
            }
        }

        for (int i = shown; i < _slots.Count; i++)
            if (_slots[i] != null) _slots[i].SetEmpty();

        if (stone != null && shown < _targets.Count && !_warnedSlotShortage)
        {
            _warnedSlotShortage = true;
            Debug.LogWarning($"[ItemTooltip] '{stone.stoneName}' 대상 {_targets.Count}종 중 {shown}종만 표시됨 — " +
                             "Unit Slot Prefab을 물리거나 Max Unit Slots를 늘릴 것", this);
        }

        return shown;
    }

    /// <summary>코스트 오름차순, 같으면 id순(표시 순서를 고정해 호버할 때마다 자리가 바뀌지 않게).</summary>
    private static int CompareByCostThenId(PokemonData a, PokemonData b)
    {
        int byCost = a.cost.CompareTo(b.cost);
        return byCost != 0 ? byCost : a.id.CompareTo(b.id);
    }

    /// <summary>i번째 칸. 없으면 프리팹에서 만들어 붙인다. 상한을 넘거나 프리팹이 없으면 null.</summary>
    private SynergyTooltipUnitSlot SlotAt(int index)
    {
        if (index < _slots.Count) return _slots[index];
        if (_unitSlotPrefab == null || index >= _maxUnitSlots) return null;

        // worldPositionStays: false — 켜두면 프리팹에 잡아둔 RectTransform 값이 틀어진다.
        var slot = Instantiate(_unitSlotPrefab, _unitSlotRoot, false);
        _slots.Add(slot);
        return slot;
    }
}
