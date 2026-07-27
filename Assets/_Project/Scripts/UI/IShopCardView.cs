using System;

/// <summary>
/// 상점 카드 한 장이 컨트롤러(ShopBarUIBase)에게 노출해야 하는 최소 계약.
/// 유닛 상점(ShopCardUI/PokemonData)과 향후 아이템 상점(아이템 카드/아이템 데이터)이
/// 같은 컨트롤러 로직(구매 처리/Sold 전환/클릭 배선)을 공유하기 위한 인터페이스.
/// </summary>
public interface IShopCardView<TData>
{
    /// <summary>카드 클릭 시 발행. 실제 구매 처리는 컨트롤러가 구독해서 수행.</summary>
    event Action Clicked;

    /// <summary>슬롯에 데이터가 있을 때 표시 내용을 채움.</summary>
    void Bind(TData data);

    /// <summary>슬롯이 비었을 때(구매 직후 등) 콘텐츠를 숨기고 구매불가 처리.</summary>
    void SetSold();
}
