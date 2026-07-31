using System.Collections.Generic;

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
    protected override void Subscribe()
    {
        GameEvents.OnShopRerolled += Refresh;
        GameEvents.OnGoldChanged += HandleGoldChanged;

        // 보유 중 강조는 필드·벤치 구성이 바뀔 때마다 다시 판정해야 한다.
        GameEvents.OnUnitPlaced  += HandleRosterChanged;
        GameEvents.OnUnitBenched += HandleRosterChanged;
        GameEvents.OnUnitSold    += HandleRosterChanged;
    }

    protected override void Unsubscribe()
    {
        GameEvents.OnShopRerolled -= Refresh;
        GameEvents.OnGoldChanged -= HandleGoldChanged;

        GameEvents.OnUnitPlaced  -= HandleRosterChanged;
        GameEvents.OnUnitBenched -= HandleRosterChanged;
        GameEvents.OnUnitSold    -= HandleRosterChanged;
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
