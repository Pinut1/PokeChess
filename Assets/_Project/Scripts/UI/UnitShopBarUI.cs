using System.Collections.Generic;

/// <summary>
/// 유닛 상점 카드 5장. 공통 로직은 ShopBarUIBase가 담당하고,
/// 여기선 "유닛 상점 갱신 이벤트/데이터/구매 호출이 무엇인지"만 정의한다.
/// 아이템 상점은 같은 베이스로 ItemShopBarUI : ShopBarUIBase&lt;ScriptableObject, ItemCardUI&gt;
/// 형태로 추가하면 됨(카드 프리팹·ItemCardUI 완성 후).
/// </summary>
public class UnitShopBarUI : ShopBarUIBase<PokemonData, ShopCardUI>
{
    protected override void Subscribe()   => GameEvents.OnShopRerolled += Refresh;
    protected override void Unsubscribe() => GameEvents.OnShopRerolled -= Refresh;

    protected override IReadOnlyList<PokemonData> GetSlots()
    {
        var shop = GameManager.Instance != null ? GameManager.Instance.Shop : null;
        return shop != null ? shop.CurrentSlots : null;
    }

    protected override void Buy(int slotIndex)
    {
        var shop = GameManager.Instance != null ? GameManager.Instance.Shop : null;
        shop?.Buy(slotIndex);
    }
}
