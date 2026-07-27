using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 아이템 상점 카드 한 장의 표시/구매·리롤 클릭을 담당.
/// 0번 슬롯은 진화의 돌(EvolutionStoneData), 1~3번 슬롯은 일반 아이템(ItemData)이 들어오지만
/// 표시 레이아웃은 동일해서 Bind에서 타입만 분기한다(슬롯 규칙은 ShopManager.RollItemShop이 정함).
/// 데이터 바인딩만 하고 실제 구매·리롤 처리는 상위 컨트롤러(ItemShopBarUI)가 연결한다.
/// 카드 루트(이 컴포넌트가 붙는 오브젝트)는 ItemCardContanier — 프리팹에서 항상 활성 상태로 유지해야
/// Horizontal Layout Group이 슬롯 자리를 접지 않는다. Sold 상태에서도 이 오브젝트 자체는 끄지 않는다.
/// </summary>
public class ItemCardUI : MonoBehaviour, IShopCardView<ScriptableObject>
{
    [Header("버튼")]
    [SerializeField] private Button _buyButton;      // ItemCard_Button
    [Tooltip("이 카드 1칸만 다시 굴리는 버튼. 슬롯당 라운드 1회, 사용하면 다음 라운드까지 비활성.")]
    [SerializeField] private Button _rerollButton;   // ItemCard_RerollButton

    [Header("표시 요소")]
    [SerializeField] private GameObject _backPanel;       // Item_BackPanel
    [SerializeField] private Image _itemImage;            // Item_Image
    [SerializeField] private TextMeshProUGUI _nameText;   // Item_Name
    [Tooltip("가격 단위가 골드가 아니라 아이템 쿠폰임을 나타내는 아이콘.")]
    [SerializeField] private GameObject _ticketImage;     // Ticket_Image
    [SerializeField] private TextMeshProUGUI _priceText;  // Price_Text

    [Header("구매완료 오버레이 (미배치 시 비워둬도 동작)")]
    [SerializeField] private GameObject _soldOverlay;

    public ScriptableObject CurrentData { get; private set; }

    /// <summary>구매 클릭 시 발행. 실제 구매 로직은 컨트롤러가 넘겨준다(ShopManager 직접 참조 안 함).</summary>
    public event Action Clicked;

    /// <summary>리롤 클릭 시 발행. 유닛 카드에는 없는 아이템 카드 전용 동작이라 IShopCardView에는 넣지 않는다.</summary>
    public event Action RerollClicked;

    private void Awake()
    {
        _buyButton.onClick.AddListener(() => Clicked?.Invoke());

        if (_rerollButton != null)
            _rerollButton.onClick.AddListener(() => RerollClicked?.Invoke());
    }

    public void Bind(ScriptableObject data)
    {
        CurrentData = data;

        _buyButton.interactable = true;
        SetContentActive(true);
        if (_soldOverlay != null) _soldOverlay.SetActive(false);

        // 레이아웃은 같고 데이터 출처만 다름 — 돌은 EvolutionStoneDatabase, 아이템은 ItemDatabase에서 옴.
        switch (data)
        {
            case EvolutionStoneData stone:
                _itemImage.sprite = stone.icon;
                _nameText.text = stone.stoneName;
                break;

            case ItemData item:
                _itemImage.sprite = item.icon;
                _nameText.text = item.itemName;
                break;
        }

        var shop = GameManager.Instance != null ? GameManager.Instance.Shop : null;
        if (shop != null) _priceText.text = shop.ItemPrice.ToString();
    }

    /// <summary>슬롯이 비었을 때(구매 직후, 다음 갱신 전까지) 카드 콘텐츠를 숨기고 Sold 오버레이를 노출.</summary>
    public void SetSold()
    {
        CurrentData = null;

        _buyButton.interactable = false;
        SetContentActive(false);
        if (_soldOverlay != null) _soldOverlay.SetActive(true);
    }

    /// <summary>이번 라운드에 이 슬롯을 리롤할 수 있는지 반영. 컨트롤러가 ShopManager 상태를 보고 호출한다.</summary>
    public void SetRerollAvailable(bool available)
    {
        if (_rerollButton != null) _rerollButton.interactable = available;
    }

    /// <summary>
    /// 리롤 버튼은 여기서 제외한다 — 구매로 비워진 슬롯도 리롤 대상이라 Sold 상태에서 살아있어야 한다.
    /// </summary>
    private void SetContentActive(bool on)
    {
        if (_backPanel != null) _backPanel.SetActive(on);
        if (_ticketImage != null) _ticketImage.SetActive(on);
        _itemImage.gameObject.SetActive(on);
        _nameText.gameObject.SetActive(on);
        _priceText.gameObject.SetActive(on);
    }
}
