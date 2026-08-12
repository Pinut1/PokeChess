using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유닛 상점 카드 5장. 공통 로직은 ShopBarUIBase가 담당하고,
/// 여기선 "유닛 상점 갱신 이벤트/데이터/구매 호출이 무엇인지"만 정의한다.
/// 아이템 상점은 같은 베이스로 ItemShopBarUI : ShopBarUIBase&lt;ScriptableObject, ItemCardUI&gt;
/// 형태로 추가하면 됨(카드 프리팹·ItemCardUI 완성 후).
///
/// 상점 갱신 외에 골드 변동도 구독한다 — 카드 내용은 그대로여도 골드가 줄면
/// 살 수 있던 카드가 못 사는 카드로 바뀌기 때문(흑백 처리 + 클릭 차단).
/// </summary>
public class UnitShopBarUI : ShopBarUIBase<PokemonData, ShopCardUI>
{
    [Header("역할 설명창")]
    [Tooltip("카드에 커서를 올리면 커서 오른쪽 위에 띄울 역할 툴팁. 비우면 뜨지 않는다.")]
    [SerializeField] private RoleTooltipController _roleTooltip;

    protected override void Awake()
    {
        base.Awake(); // 구매 클릭 배선

        for (int i = 0; i < _cards.Length; i++)
            if (_cards[i] != null) _cards[i].Hovered += HandleHovered;
    }

    protected override void OnDisable()
    {
        base.OnDisable(); // Unsubscribe

        // 상점을 닫는데 설명창만 남는 일이 없도록(아이템 상점으로 전환할 때).
        // HideAll이 아니라 카드별 Hide인 이유 — 컨트롤러를 다른 쪽과 공유할 때
        // HideAll은 남이 띄운 툴팁까지 꺼버린다. Hide(owner)는 자기 것이 아니면 그냥 무시된다.
        HideTooltipOwnedByCards();
    }

    private void HideTooltipOwnedByCards()
    {
        if (_roleTooltip == null) return;

        for (int i = 0; i < _cards.Length; i++)
            if (_cards[i] != null) _roleTooltip.Hide(_cards[i]);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _cards.Length; i++)
            if (_cards[i] != null) _cards[i].Hovered -= HandleHovered;
    }

    /// <summary>카드에 커서가 들고 날 때 역할 설명창을 여닫는다.</summary>
    private void HandleHovered(ShopCardUI card, bool entered)
    {
        if (_roleTooltip == null) return;

        if (entered) _roleTooltip.Show(card, card.CurrentData);
        else         _roleTooltip.Hide(card);
    }

    protected override void Subscribe()
    {
        GameEvents.OnShopRerolled += Refresh;
        GameEvents.OnGoldChanged += HandleGoldChanged;

        // 보유 중 강조는 필드·벤치 구성이 바뀔 때마다 다시 판정해야 한다.
        GameEvents.OnUnitPlaced  += HandleRosterChanged;
        GameEvents.OnUnitBenched += HandleRosterChanged;
        GameEvents.OnUnitSold    += HandleRosterChanged;

        // 통신교환 송신(BoardManager.RemoveUnitForTrade)은 판매가 아니라서 OnUnitSold를 발행하지
        // 않고 OnBoardResyncRequested만 발행한다(재접속/재동기화와 공유하는 신호). 이 이벤트가 없으면
        // 송신 직후에도 "보유 중" 강조를 재판정할 트리거가 없어 깜빡임이 그대로 남는다(2026-08 확인).
        // Refresh()는 ShopManager.CurrentSlots를 다시 읽어 카드를 재바인딩만 하는 순수 UI 재계산이라
        // (구매/리롤/골드 차감 등 부작용 없음) 재접속 재동기화처럼 다른 이유로 함께 발행돼도 안전하다.
        GameEvents.OnBoardResyncRequested += Refresh;
    }

    protected override void Unsubscribe()
    {
        GameEvents.OnShopRerolled -= Refresh;
        GameEvents.OnGoldChanged -= HandleGoldChanged;

        GameEvents.OnUnitPlaced  -= HandleRosterChanged;
        GameEvents.OnUnitBenched -= HandleRosterChanged;
        GameEvents.OnUnitSold    -= HandleRosterChanged;

        GameEvents.OnBoardResyncRequested -= Refresh;
    }

    protected override IReadOnlyList<PokemonData> GetSlots()
    {
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        return shop != null ? shop.CurrentSlots : null;
    }

    protected override void Buy(int slotIndex)
    {
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        shop?.Buy(slotIndex);
    }

    /// <summary>바인딩 직후 호출 — 카드 내용은 베이스가 채우고, 살 수 있는지/보유 중인지는 여기서 판정한다.</summary>
    protected override void AfterBind(ShopCardUI card, PokemonData data)
    {
        bool hasGm = GameManager.TryGet(out var gm);

        var shop = hasGm ? gm.Shop : null;
        card.SetAffordable(shop != null && shop.Gold >= data.cost);

        card.SetOwned(OwnsSpecies(hasGm ? gm.Board : null, data));

        // 리롤은 카드 위치를 그대로 두고 내용만 갈아끼운다. 커서가 안 움직이면
        // PointerEnter/Exit이 다시 안 일어나 툴팁에 리롤 전 역할이 남는다 — 여기서 직접 갱신한다.
        if (card.IsHovered && _roleTooltip != null)
            _roleTooltip.Show(card, data);
    }

    /// <summary>
    /// 필드·벤치에 같은 진화 계열을 한 마리라도 갖고 있는지.
    /// 계열 루트(EvolutionFamily.RootId) 기준이라 합체로 종이 바뀐 유닛도 같은 계열로 잡힌다
    /// — 이상해풀을 들고 있으면 상점의 이상해씨도 강조된다(SynergyManager의 카운트 기준과 동일).
    /// </summary>
    private static bool OwnsSpecies(BoardManager board, PokemonData data)
    {
        if (board == null || data == null) return false;

        int rootId = EvolutionFamily.RootId(data);

        return HasFamily(board.GetUnitsOnBoard(), rootId) ||
               HasFamily(board.GetUnitsInBench(), rootId);
    }

    private static bool HasFamily(List<PokemonUnit> units, int rootId)
    {
        if (units == null) return false;

        foreach (var unit in units)
            if (unit != null && unit.data != null &&
                EvolutionFamily.RootId(unit.data) == rootId)
                return true;

        return false;
    }

    private void HandleGoldChanged(int _) => Refresh();

    private void HandleRosterChanged(PokemonUnit _) => Refresh();
}
