using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 견본덱 목록(1페이지)의 한 줄. 덱 하나를 "이름 · 시너지 뱃지 · 유닛(+아이템) · 완성 비용 ·
/// [공략 더 보기]" 한 줄로 요약한다. 롤체지지 덱 목록과 같은 구성이다.
///
/// 시너지는 <see cref="SampleDeckSynergyBadgeUI"/>가 등급 프레임까지 그리므로, 목록만 봐도
/// 그 덱이 어느 시너지를 몇 단계까지 올리는지 읽힌다.
///
/// 유닛은 배치도(2페이지)와 <b>같은 <see cref="SampleDeckUnitCardUI"/></b>를 쓴다 — 아이콘 아래
/// 아이템 줄이 따라붙는 것도 같은 규칙이다. 다만 목록용 템플릿에는 성급 뱃지·이름을 물리지 않아
/// 아이콘과 아이템만 나온다(칸이 좁아 다 넣으면 뭉갠다).
///
/// 아이콘 줄은 덱마다 개수가 달라 <b>줄 자신이 템플릿을 복제해</b> 채운다. 복제본은 파괴하지 않고
/// 꺼두었다가 다음 덱에서 재사용한다.
/// </summary>
public class SampleDeckListRowUI : MonoBehaviour
{
    [Tooltip("덱 이름.")]
    [SerializeField] private TMP_Text _deckNameText;

    [Tooltip("완성 비용(골드). 숫자만 넣고 단위는 씬에 적어둔다.")]
    [SerializeField] private TMP_Text _goldText;

    [Header("시너지 뱃지")]
    [Tooltip("뱃지가 깔리는 부모. HorizontalLayoutGroup을 붙여둘 것.")]
    [SerializeField] private RectTransform _synergyBadgeArea;

    [Tooltip("복제할 뱃지 한 칸. 부모 아래 비활성으로 둘 것.")]
    [SerializeField] private SampleDeckSynergyBadgeUI _synergyBadgeTemplate;

    [Header("유닛")]
    [Tooltip("유닛 칸이 깔리는 부모. HorizontalLayoutGroup을 붙여둘 것.")]
    [SerializeField] private RectTransform _unitCardArea;

    [Tooltip("복제할 유닛 칸. 부모 아래 비활성으로 둘 것.")]
    [SerializeField] private SampleDeckUnitCardUI _unitCardTemplate;

    [Header("이동")]
    [Tooltip("이 덱의 상세(2페이지)로 들어가는 버튼.")]
    [SerializeField] private Button _detailButton;

    /// <summary>목록에서 몇 번째 덱인지. 패널이 어느 덱을 열지 판단하는 데 쓴다.</summary>
    public int Index { get; private set; } = -1;

    /// <summary>[공략 더 보기]를 눌렀을 때.</summary>
    public event Action<SampleDeckListRowUI> DetailRequested;

    /// <summary>
    /// 이 줄의 유닛 칸에 커서가 들고 날 때. 설명창을 여는 쪽은 패널이라, 줄은 "몇 번 덱의
    /// 어느 칸인지"만 알려준다 — 상세(2페이지)의 배치도와 같은 구조다.
    /// </summary>
    public event Action<SampleDeckListRowUI, SampleDeckUnitCardUI, bool> UnitHovered;

    /// <summary>이 줄의 유닛 칸에 붙은 <b>아이템 아이콘</b>에 커서가 들고 날 때.</summary>
    public event Action<SampleDeckListRowUI, SampleDeckUnitCardUI, SampleDeckItemSlotUI, bool> UnitItemHovered;

    private readonly List<SampleDeckSynergyBadgeUI> _synergyBadges = new();
    private readonly List<SampleDeckUnitCardUI> _unitCards = new();

    private bool _listenerBound;

    private void Awake()
    {
        BindListener();

        // 줄 안의 템플릿도 꺼둔다 — 프리팹을 손보다 켠 채 저장하면 덱마다 빈 뱃지·빈 유닛 칸이
        // 하나씩 더 붙는다. 패널이 자기 템플릿을 끄는 것과 같은 보정이다.
        SampleDeckPool.HideTemplate(_synergyBadgeTemplate);
        SampleDeckPool.HideTemplate(_unitCardTemplate);
    }

    /// <summary>꺼둔 템플릿을 복제해 만들기 때문에 Bind가 Awake보다 먼저 올 수 있다.</summary>
    private void BindListener()
    {
        if (_listenerBound || _detailButton == null) return;

        _listenerBound = true;
        _detailButton.onClick.AddListener(() => DetailRequested?.Invoke(this));
    }

    /// <summary>
    /// 덱 한 줄을 채운다. synergies·units는 패널이 표시 순서대로 정렬해 넘긴다 —
    /// 줄은 순서를 다시 손대지 않는다.
    /// </summary>
    public void Bind(
        int index,
        string deckName,
        int totalGold,
        IReadOnlyList<SynergyStatus> synergies,
        IReadOnlyList<SampleDeckUnitView> units)
    {
        BindListener();

        Index = index;

        if (_deckNameText != null) _deckNameText.text = deckName;
        if (_goldText != null) _goldText.text = totalGold.ToString();

        FillSynergyBadges(synergies);
        FillUnitCards(units);
    }

    private void FillSynergyBadges(IReadOnlyList<SynergyStatus> synergies)
    {
        int count = synergies != null ? synergies.Count : 0;

        for (int i = 0; i < count; i++)
        {
            SampleDeckSynergyBadgeUI badge =
                SampleDeckPool.Take(_synergyBadges, _synergyBadgeTemplate, _synergyBadgeArea, i);

            if (badge == null) break;

            badge.Bind(synergies[i]);
        }

        SampleDeckPool.HideExtras(_synergyBadges, count);
    }

    private void FillUnitCards(IReadOnlyList<SampleDeckUnitView> units)
    {
        int count = units != null ? units.Count : 0;

        for (int i = 0; i < count; i++)
        {
            // 상세 페이지의 배치도와 같은 칸이므로 설명창도 같이 붙는다 — 목록에서만 조용히
            // 반응이 없으면 "여긴 왜 안 뜨지" 하게 된다. 이벤트는 생성 시점에만 건다.
            SampleDeckUnitCardUI card = SampleDeckPool.Take(
                _unitCards, _unitCardTemplate, _unitCardArea, i,
                created =>
                {
                    created.Hovered += HandleUnitHovered;
                    created.ItemHovered += HandleUnitItemHovered;
                });

            if (card == null) break;

            card.Bind(units[i].pokemon, units[i].starLevel, units[i].items);
        }

        SampleDeckPool.HideExtras(_unitCards, count);
    }

    private void HandleUnitHovered(SampleDeckUnitCardUI card, bool entered)
        => UnitHovered?.Invoke(this, card, entered);

    private void HandleUnitItemHovered(
        SampleDeckUnitCardUI card, SampleDeckItemSlotUI slot, bool entered)
        => UnitItemHovered?.Invoke(this, card, slot, entered);
}
