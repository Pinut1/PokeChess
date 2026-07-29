using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상점 카드 N장(IShopCardView 구현체)을 데이터 슬롯과 동기화하는 공통 로직.
/// 유닛 상점(5칸)/아이템 상점(4칸)이 이 베이스를 상속해 이벤트 구독·데이터 소스·구매 호출만
/// 다르게 구현하면 된다. 카드 배열 길이가 곧 상점 칸 수라 유닛/아이템 칸 수 차이는
/// 프리팹에 몇 장을 넣느냐로만 결정되고 코드는 손댈 필요 없음.
/// </summary>
public abstract class ShopBarUIBase<TData, TCard> : MonoBehaviour
    where TData : class
    where TCard : Component, IShopCardView<TData>
{
    [Tooltip("Horizontal Layout Group 자식 카드들. 순서가 상점 슬롯 인덱스와 반드시 일치해야 함.")]
    [SerializeField] protected TCard[] _cards;

    protected virtual void Awake()
    {
        for (int i = 0; i < _cards.Length; i++)
        {
            int slotIndex = i; // 클로저 캡처용 로컬 복사
            _cards[i].Clicked += () => Buy(slotIndex);
        }
    }

    protected virtual void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    protected virtual void OnDisable() => Unsubscribe();

    /// <summary>상점 갱신 이벤트(GameEvents.OnShopRerolled 등) 구독.</summary>
    protected abstract void Subscribe();

    /// <summary>Subscribe의 역순 해제.</summary>
    protected abstract void Unsubscribe();

    /// <summary>현재 상점 슬롯 데이터. ShopManager.CurrentSlots / CurrentItemSlots 등.</summary>
    protected abstract IReadOnlyList<TData> GetSlots();

    /// <summary>슬롯 클릭 시 실제 구매 처리. ShopManager.Buy / BuyItem 등.</summary>
    protected abstract void Buy(int slotIndex);

    /// <summary>
    /// 카드 바인딩 직후 훅. 데이터만으로는 알 수 없는 상태(구매 가능 여부 등)를
    /// 상점별로 덧입힐 때 쓴다. 기본 동작 없음.
    /// </summary>
    protected virtual void AfterBind(TCard card, TData data) { }

    protected void Refresh()
    {
        var slots = GetSlots();
        if (slots == null) return;

        for (int i = 0; i < _cards.Length; i++)
        {
            var data = i < slots.Count ? slots[i] : null;
            if (data != null)
            {
                _cards[i].Bind(data);
                AfterBind(_cards[i], data);
            }
            else _cards[i].SetSold();
        }
    }
}
