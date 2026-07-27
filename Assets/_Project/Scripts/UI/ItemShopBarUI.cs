using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 상점 카드 4장. 카드 내용 동기화는 ShopBarUIBase가 담당하고,
/// 여기선 "아이템 상점 갱신 이벤트/데이터/구매 호출이 무엇인지"만 정의한다(UnitShopBarUI와 동일 구조).
/// 슬롯 규칙(0=진화의 돌, 1~3=일반 아이템)은 ShopManager.RollItemShop이 정하므로
/// UI는 슬롯 인덱스를 그대로 따라가기만 하면 된다.
/// 유닛 상점과 다른 점은 카드마다 개별 리롤 버튼이 있다는 것뿐이라,
/// 베이스의 구매 배선에 더해 슬롯 단위 리롤을 여기서 추가로 연결한다.
/// </summary>
public class ItemShopBarUI : ShopBarUIBase<ScriptableObject, ItemCardUI>
{
    protected override void Awake()
    {
        base.Awake(); // 구매 클릭 배선

        for (int i = 0; i < _cards.Length; i++)
        {
            int slotIndex = i; // 클로저 캡처용 로컬 복사
            _cards[i].RerollClicked += () => RerollSlot(slotIndex);
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable(); // Subscribe + Refresh
        RefreshRerollButtons();
    }

    // 리롤/구매/라운드 갱신이 모두 OnItemShopRerolled로 통지되므로 한 핸들러에서 같이 처리한다.
    protected override void Subscribe()   => GameEvents.OnItemShopRerolled += RefreshAll;
    protected override void Unsubscribe() => GameEvents.OnItemShopRerolled -= RefreshAll;

    protected override IReadOnlyList<ScriptableObject> GetSlots()
    {
        var shop = GameManager.Instance != null ? GameManager.Instance.Shop : null;
        return shop != null ? shop.CurrentItemSlots : null;
    }

    protected override void Buy(int slotIndex)
    {
        var shop = GameManager.Instance != null ? GameManager.Instance.Shop : null;
        shop?.BuyItem(slotIndex);
    }

    private void RerollSlot(int slotIndex)
    {
        var shop = GameManager.Instance != null ? GameManager.Instance.Shop : null;
        shop?.RerollItemSlot(slotIndex);
    }

    private void RefreshAll()
    {
        Refresh(); // 카드 내용 바인딩 / Sold 처리
        RefreshRerollButtons();
    }

    /// <summary>라운드 갱신 시 전부 다시 켜지고, 리롤을 쓴 슬롯만 꺼진다.</summary>
    private void RefreshRerollButtons()
    {
        var shop = GameManager.Instance != null ? GameManager.Instance.Shop : null;

        for (int i = 0; i < _cards.Length; i++)
            _cards[i].SetRerollAvailable(shop != null && shop.CanRerollItemSlot(i));
    }
}
