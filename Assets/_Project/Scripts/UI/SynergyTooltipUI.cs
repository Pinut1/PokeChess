using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 시너지 아이콘에 마우스를 올렸을 때 뜨는 설명창(롤체 특성 툴팁과 같은 구성).
///
///   시너지 심볼 / 시너지 이름
///   (2) 단계별 효과 설명
///   (4) 단계별 효과 설명
///   효과를 받는 유닛 아이콘들 — 코스트별 테두리, 필드에 배치돼 있으면 채색·아니면 흑백
///
/// 여닫기와 위치는 SynergyTooltipController가 맡고, 이 컴포넌트는 <b>내용만</b> 그린다.
/// 스크립트는 툴팁 루트(껐다 켜지는 오브젝트)에 붙인다.
///
/// ⚠️ 프리팹 조건 — 이게 맞아야 "위는 고정, 아래로만 늘어남"이 성립한다.
///   · 루트 RectTransform 피벗 Y = 1 (위쪽 기준으로 자란다)
///   · 루트에 VerticalLayoutGroup + ContentSizeFitter(Vertical = Preferred Size)
///   · 유닛 아이콘 칸의 부모(Unit Slot Root)에 GridLayoutGroup + ContentSizeFitter(Vertical = Preferred Size)
///   · 단계 줄 텍스트는 Auto Size 대신 고정 크기 + Wrapping 권장(줄 수가 늘면 패널이 그만큼 길어진다)
/// </summary>
public class SynergyTooltipUI : MonoBehaviour
{
    [Header("머리말")]
    [Tooltip("시너지 심볼. SynergyData.icon을 그대로 쓴다.")]
    [SerializeField] private Image _symbol;

    [Tooltip("심볼 색. 심볼 PNG가 흰색 제작이라 곱셈 틴트로 색을 낸다(시너지 행과 같은 방식).")]
    [SerializeField] private Color _symbolColor = Color.white;

    [Tooltip("시너지 이름.")]
    [SerializeField] private TextMeshProUGUI _nameText;

    [Tooltip("(선택) 현재 보유 수. 안 쓸 거면 비워둔다.")]
    [SerializeField] private TextMeshProUGUI _countText;

    [Tooltip("보유 수 표기 형식. {0}=현재 계열 수.")]
    [SerializeField] private string _countFormat = "{0}";

    [Header("단계별 효과 줄")]
    [Tooltip("단계 줄 텍스트. 현재 데이터의 최대 단계 수는 4개(풀·독·물·돌연변이)라 4칸이면 충분하다. " +
             "데이터의 단계가 더 많아지면 남는 단계는 표시되지 않고 경고가 한 번 찍힌다.")]
    [SerializeField] private TextMeshProUGUI[] _tierLines = new TextMeshProUGUI[4];

    [Tooltip("줄 표기 형식. {0}=발동에 필요한 수, {1}=효과 설명.")]
    [SerializeField] private string _tierFormat = "({0}) {1}";

    [Tooltip("지금 발동 중인 단계의 색.")]
    [SerializeField] private Color _activeTierColor = Color.white;

    [Tooltip("아직 못 채웠거나 이미 지나간 단계의 색(시너지 행의 문턱 표기와 같은 규칙).")]
    [SerializeField] private Color _inactiveTierColor = new(0.62f, 0.62f, 0.62f);

    [Header("효과를 받는 유닛 아이콘")]
    [Tooltip("아이콘 칸이 담기는 부모. GridLayoutGroup을 붙여둘 것.")]
    [SerializeField] private RectTransform _unitSlotRoot;

    [Tooltip("칸이 모자랄 때 복제할 칸 프리팹. Unit Slot Root 아래에 미리 칸을 깔아뒀다면 " +
             "그것부터 쓰고, 부족한 만큼만 이걸로 만든다. 둘 다 없으면 아이콘 줄이 비어 보인다.")]
    [SerializeField] private SynergyTooltipUnitSlot _unitSlotPrefab;

    [Tooltip("만들 수 있는 칸의 상한(폭주 방지). 현재 데이터의 최대는 10칸(독)이다.")]
    [SerializeField] private int _maxUnitSlots = 24;

    /// <summary>지금 그리고 있는 시너지. 컨트롤러가 같은 시너지인지 판단할 때 쓴다.</summary>
    public SynergyData CurrentData { get; private set; }

    /// <summary>위치를 잡는 쪽(컨트롤러)이 쓰는 루트 RectTransform.</summary>
    public RectTransform Rect { get; private set; }

    // 미리 깔아둔 칸 + 필요해서 만든 칸. 한 번 만들면 파괴하지 않고 재사용한다.
    private readonly List<SynergyTooltipUnitSlot> _slots = new();

    // 보드 위 유닛의 카운트 키. SynergyManager와 같은 기준으로 채색 여부를 판단한다.
    private readonly HashSet<int> _boardSpecies = new();
    private readonly HashSet<int> _boardRoots = new();

    // 경고는 종류당 1회만(호버할 때마다 로그가 쏟아지지 않게).
    private bool _warnedTierOverflow;
    private bool _warnedSlotShortage;

    private void Awake()
    {
        Rect = (RectTransform)transform;

        // 프리팹에 미리 깔아둔 칸을 먼저 회수한다(꺼둔 칸도 포함).
        if (_unitSlotRoot != null)
            _slots.AddRange(_unitSlotRoot.GetComponentsInChildren<SynergyTooltipUnitSlot>(true));
    }

    /// <summary>시너지 하나를 그린다. status가 null이면 보유 0으로 취급(전 단계 비활성).</summary>
    public void Bind(SynergyStatus status) => Bind(status?.data, status);

    /// <summary>data는 반드시 있어야 한다. 여는 판단은 컨트롤러가 하므로 여기선 null이면 아무것도 하지 않는다.</summary>
    public void Bind(SynergyData data, SynergyStatus status)
    {
        CurrentData = data;
        if (data == null) return;

        if (_symbol != null)
        {
            _symbol.sprite = data.icon;
            _symbol.color = _symbolColor;
            _symbol.enabled = data.icon != null;
        }

        if (_nameText != null) _nameText.text = data.synergyName;

        if (_countText != null)
            _countText.text = string.Format(_countFormat, status != null ? status.uniqueCount : 0);

        BindTierLines(data, status);
        BindUnitSlots(data);
    }

    // ─────────────────────────────────────────
    // 단계별 효과
    // ─────────────────────────────────────────

    private void BindTierLines(SynergyData data, SynergyStatus status)
    {
        if (_tierLines == null) return;

        var tiers = data.tiers;
        int tierCount = tiers != null ? tiers.Count : 0;
        int activeIndex = status != null ? status.activeTierIndex : -1;

        for (int i = 0; i < _tierLines.Length; i++)
        {
            var line = _tierLines[i];
            if (line == null) continue;

            bool used = i < tierCount;
            // 줄 오브젝트를 끄면 VerticalLayoutGroup이 자리를 접어 패널이 그만큼 짧아진다.
            line.gameObject.SetActive(used);
            if (!used) continue;

            var tier = tiers[i];
            line.text = string.Format(_tierFormat, tier.count,
                                      string.IsNullOrEmpty(tier.effectDescription) ? "-" : tier.effectDescription);

            // 지금 발동 중인 단계만 밝게 — 시너지 행의 "2 > 4 > 6" 표기와 같은 규칙.
            line.color = i == activeIndex ? _activeTierColor : _inactiveTierColor;
        }

        if (tierCount > _tierLines.Length && !_warnedTierOverflow)
        {
            _warnedTierOverflow = true;
            Debug.LogWarning($"[SynergyTooltip] '{data.synergyName}'의 단계가 {tierCount}개인데 줄이 " +
                             $"{_tierLines.Length}개뿐 — 프리팹에 단계 줄을 더 깔고 Tier Lines에 물릴 것", this);
        }
    }

    // ─────────────────────────────────────────
    // 효과를 받는 유닛 아이콘
    // ─────────────────────────────────────────

    private void BindUnitSlots(SynergyData data)
    {
        if (_unitSlotRoot == null) return;

        CollectBoardKeys();

        var members = SynergyMemberIndex.MembersOf(data);
        int shown = 0;

        for (int i = 0; i < members.Count; i++)
        {
            var slot = SlotAt(i);
            if (slot == null) break; // 칸 상한 도달 — 아래에서 경고

            slot.Bind(members[i].data, IsOnBoard(members[i], data.countPerSpecies));
            shown++;
        }

        for (int i = shown; i < _slots.Count; i++)
            if (_slots[i] != null) _slots[i].SetEmpty();

        if (shown < members.Count && !_warnedSlotShortage)
        {
            _warnedSlotShortage = true;
            Debug.LogWarning($"[SynergyTooltip] '{data.synergyName}' 멤버 {members.Count}명 중 {shown}명만 표시됨 — " +
                             "Unit Slot Prefab을 물리거나 Max Unit Slots를 늘릴 것", this);
        }
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

    /// <summary>
    /// 필드(보드) 위 유닛의 카운트 키를 모은다. 벤치는 세지 않는다 — SynergyManager와 같은 기준이라야
    /// "채색된 아이콘 수 = 시너지 카운트"가 맞는다.
    /// </summary>
    private void CollectBoardKeys()
    {
        _boardSpecies.Clear();
        _boardRoots.Clear();

        if (!GameManager.TryGet(out var gm)) return;

        var board = gm.Board;
        if (board == null) return;

        foreach (var unit in board.GetUnitsOnBoard())
        {
            if (unit == null || unit.data == null) continue;

            _boardSpecies.Add(unit.data.id);
            _boardRoots.Add(EvolutionFamily.RootId(unit.data));
        }
    }

    /// <summary>
    /// 종 집합과 계열 집합을 <b>따로</b> 두는 이유: 루트 id도 결국 종 id라 한 집합에 섞으면
    /// "종 5번이 올라와 있다"가 "계열 5번이 올라와 있다"로 오인돼 엉뚱한 아이콘이 켜진다.
    /// </summary>
    private bool IsOnBoard(SynergyMemberIndex.Member member, bool countPerSpecies)
        => countPerSpecies ? _boardSpecies.Contains(member.countKey)
                           : _boardRoots.Contains(member.countKey);
}
