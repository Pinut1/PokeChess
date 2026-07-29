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
    }

    protected override void Unsubscribe()
    {
        GameEvents.OnShopRerolled -= Refresh;
        GameEvents.OnGoldChanged -= HandleGoldChanged;
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

    /// <summary>바인딩 직후 호출 — 카드 내용은 베이스가 채우고, 살 수 있는지는 여기서 판정한다.</summary>
    protected override void AfterBind(ShopCardUI card, PokemonData data)
    {
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        card.SetAffordable(shop != null && shop.Gold >= data.cost);
    }

    private void HandleGoldChanged(int _) => Refresh();
}
