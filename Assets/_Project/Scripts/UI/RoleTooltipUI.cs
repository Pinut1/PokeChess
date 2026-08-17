using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 유닛의 역할군만 한 줄로 보여주는 작은 설명창의 표시부.
/// 여닫기와 자리잡기는 <see cref="RoleTooltipController"/>가 맡는다
/// (시너지·아이템 툴팁의 UI/Controller 분리와 같은 구조).
///
/// 프리팹 구조(RoleTooltip_Pf):
///   RoleTooltip_Pf   Image(배경)
///     Role_Text      TextMeshProUGUI
///     ItemArea       HorizontalLayoutGroup  ← (선택) 역할군 추천 아이템 줄
///       ItemSlot_Template  SampleDeckItemSlotUI (비활성 템플릿)
///
/// 아이템 줄은 <b>선택</b>이다 — <see cref="_itemArea"/>를 안 물리면 예전처럼 역할 이름만 나온다.
///
/// 역할 한글 대응표는 StatInfoPanelUI와 같은 내용을 여기에도 둔다.
/// 표가 두 벌이 되지만 스탯창의 검증된 인스펙터 배선을 건드리지 않는 쪽을 택했다
/// — 표기를 바꿀 일이 생기면 <b>두 곳을 같이</b> 고쳐야 한다.
/// </summary>
public class RoleTooltipUI : MonoBehaviour
{
    /// <summary>역할 영문명과 표시용 한글명 한 쌍. 데이터에는 영문(Archer 등)만 들어 있다.</summary>
    [System.Serializable]
    public class RoleLabel
    {
        public string role;    // 데이터의 role 문자열 (Archer, Tanker …)
        public string korean;  // 표시용 (원거리딜러, 탱커 …)
    }

    [Tooltip("Role_Text.")]
    [SerializeField] private TextMeshProUGUI _roleText;

    [Tooltip("역할 표기 형식. {0}=데이터의 영문 역할, {1}=아래 표에서 찾은 한글 이름.\n" +
             "한글 이름을 못 찾으면 영문만 표시한다.")]
    [SerializeField] private string _roleFormat = "{0}({1})";

    [Header("역할군 추천 아이템 (선택)")]
    [Tooltip("아이템 칸이 깔리는 부모. 비워두면 아이템 줄 없이 역할 이름만 나온다.")]
    [SerializeField] private RectTransform _itemArea;

    [Tooltip("복제할 아이템 칸(배경 + 아이콘). 부모 아래 비활성으로 둘 것.")]
    [SerializeField] private SampleDeckItemSlotUI _itemSlotTemplate;

    [Tooltip("아이템 줄 위의 고정 문구. 없으면 비워둬도 된다.")]
    [SerializeField] private GameObject _itemLabel;

    [Tooltip("역할 영문 → 한글 대응표. 데이터의 role 문자열과 대소문자 무관하게 맞춘다. " +
             "StatInfoPanelUI의 Role Labels와 같은 내용으로 유지할 것.")]
    [SerializeField] private RoleLabel[] _roleLabels =
    {
        new() { role = "Tanker",    korean = "탱커" },
        new() { role = "Warrior",   korean = "전사" },
        new() { role = "Assassin",  korean = "암살자" },
        new() { role = "Magician",  korean = "마법사" },
        new() { role = "Archer",    korean = "원거리딜러" },
        new() { role = "Supporter", korean = "서포터" },
    };

    /// <summary>자리를 잡는 쪽(컨트롤러)이 쓰는 루트 RectTransform.</summary>
    public RectTransform Rect { get; private set; }

    private readonly List<SampleDeckItemSlotUI> _itemSlots = new();

    // 이번에 그릴 목록(장비 + 진화의 돌). 매번 새 리스트를 만들지 않으려고 재사용한다.
    private readonly List<ScriptableObject> _shown = new();

    private void Awake()
    {
        Rect = transform as RectTransform;
        SampleDeckPool.HideTemplate(_itemSlotTemplate);
    }

    /// <summary>
    /// 역할 문자열을 받아 표시한다. 비어 있으면 false — 컨트롤러가 열지 않는다.
    /// </summary>
    /// <param name="pokemon">
    /// (선택) 지금 보고 있는 유닛. 넘기면 <b>그 유닛이 쓸 수 있는 진화의 돌</b>을 목록 맨 뒤에 붙인다.
    /// 상점 카드처럼 유닛이 특정되지 않는 곳에서는 null을 넘기면 된다.
    /// </param>
    /// <param name="evolutionLocked">
    /// 진화잠금 상태(나인이볼부스트 이브이 등)면 돌을 붙이지 않는다 — 끼워도 진화하지 않는다.
    /// </param>
    public bool Bind(string role, PokemonData pokemon = null, bool evolutionLocked = false)
    {
        if (string.IsNullOrEmpty(role)) return false;

        if (_roleText != null) _roleText.text = FormatRole(role);

        BindItems(role, pokemon, evolutionLocked);
        return true;
    }

    /// <summary>
    /// 이 역할군이 쓰는 아이템을 전부 보여준다(<see cref="SampleDeckItemRules"/>).
    /// 기획 설계 문서의 역할군별 아이템 분배를 그대로 따르므로 견본덱 화면과 같은 목록이 나온다.
    /// </summary>
    private void BindItems(string role, PokemonData pokemon, bool evolutionLocked)
    {
        if (_itemArea == null || _itemSlotTemplate == null) return;

        _shown.Clear();

        IReadOnlyList<ItemData> group = SampleDeckItemRules.GroupOf(role);

        if (group != null)
        {
            foreach (ItemData item in group)
                if (item != null) _shown.Add(item);
        }

        // 쓸 수 있는 진화의 돌은 맨 뒤 — 끼우는 순간 다른 유닛이 되므로 장비보다 뒤에 둔다.
        // 견본덱 유닛 설명창과 같은 규칙이다.
        if (pokemon != null && !evolutionLocked)
        {
            foreach (EvolutionStoneData stone in EvolutionStoneLookup.UsableBy(pokemon))
                _shown.Add(stone);
        }

        for (int i = 0; i < _shown.Count; i++)
        {
            SampleDeckItemSlotUI slot =
                SampleDeckPool.Take(_itemSlots, _itemSlotTemplate, _itemArea, i);

            if (slot == null) break;
            slot.Bind(_shown[i]);
        }

        SampleDeckPool.HideExtras(_itemSlots, _shown.Count);

        if (_itemLabel != null) _itemLabel.SetActive(_shown.Count > 0);
    }

    /// <summary>"Archer" → "Archer(원거리딜러)". 대응표에 없으면 영문만 그대로 쓴다.</summary>
    private string FormatRole(string role)
    {
        string korean = null;

        if (_roleLabels != null)
        {
            foreach (RoleLabel label in _roleLabels)
            {
                if (label == null || string.IsNullOrEmpty(label.role)) continue;
                if (!string.Equals(label.role, role, System.StringComparison.OrdinalIgnoreCase)) continue;

                korean = label.korean;
                break;
            }
        }

        return string.IsNullOrEmpty(korean) ? role : string.Format(_roleFormat, role, korean);
    }
}
