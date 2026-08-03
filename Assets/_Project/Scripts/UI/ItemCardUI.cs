using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 아이템 상점 카드 한 장의 표시/구매·리롤 클릭을 담당.
/// 0번 슬롯은 진화의 돌(EvolutionStoneData), 1~3번 슬롯은 일반 아이템(ItemData)이 들어오지만
/// 표시 레이아웃은 동일해서 Bind에서 타입만 분기한다(슬롯 규칙은 ShopManager.RollItemShop이 정함).
/// 데이터 바인딩만 하고 실제 구매·리롤 처리는 상위 컨트롤러(ItemShopBarUI)가 연결한다.
/// 카드 루트(이 컴포넌트가 붙는 오브젝트)는 ItemCardContanier — 프리팹에서 항상 활성 상태로 유지해야
/// Horizontal Layout Group이 슬롯 자리를 접지 않는다. Sold 상태에서도 이 오브젝트 자체는 끄지 않는다.
/// </summary>
public class ItemCardUI : MonoBehaviour, IShopCardView<ScriptableObject>,
                          IPointerEnterHandler, IPointerExitHandler
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

    [Header("구매 불가 오버레이")]
    [Tooltip("쿠폰 부족·인벤토리 만석일 때 카드를 덮는 회색 반투명 패널. " +
             "Image의 Raycast Target을 켜야 아래 구매 버튼 클릭이 막힌다.")]
    [SerializeField] private GameObject _purchaseBlocker;
    [Tooltip("블로커 자체에 붙인 Button. 누르면 구매 불가 사유를 잠시 보여준다.")]
    [SerializeField] private Button _blockerButton;
    [Tooltip("구매 불가 사유 텍스트. 평소엔 꺼두고 블로커를 눌렀을 때만 표시한다.")]
    [SerializeField] private TextMeshProUGUI _blockReasonText;
    [Tooltip("사유 표시 지속 시간(초).")]
    [SerializeField] private float _blockReasonDuration = 2f;

    public ScriptableObject CurrentData { get; private set; }

    /// <summary>구매 클릭 시 발행. 실제 구매 로직은 컨트롤러가 넘겨준다(ShopManager 직접 참조 안 함).</summary>
    public event Action Clicked;

    /// <summary>리롤 클릭 시 발행. 유닛 카드에는 없는 아이템 카드 전용 동작이라 IShopCardView에는 넣지 않는다.</summary>
    public event Action RerollClicked;

    /// <summary>커서가 들고 날 때 발행(true=들어옴). 설명창은 컨트롤러가 띄운다.</summary>
    public event Action<ItemCardUI, bool> Hovered;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (CurrentData != null) Hovered?.Invoke(this, true);
    }

    public void OnPointerExit(PointerEventData eventData) => Hovered?.Invoke(this, false);

    private string _blockReason;
    private Coroutine _reasonRoutine;

    private void Awake()
    {
        _buyButton.onClick.AddListener(() => Clicked?.Invoke());

        if (_rerollButton != null)
            _rerollButton.onClick.AddListener(() => RerollClicked?.Invoke());

        if (_blockerButton != null)
            _blockerButton.onClick.AddListener(ShowBlockReason);
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

        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        if (shop != null) _priceText.text = shop.ItemPrice.ToString();
    }

    /// <summary>슬롯이 비었을 때(구매 직후, 다음 갱신 전까지) 카드 콘텐츠를 숨기고 Sold 오버레이를 노출.</summary>
    public void SetSold()
    {
        CurrentData = null;
        Hovered?.Invoke(this, false); // 산 직후 커서가 그대로면 설명창이 남는다

        _buyButton.interactable = false;
        SetContentActive(false);
        if (_soldOverlay != null) _soldOverlay.SetActive(true);

        // 빈 슬롯은 "구매 불가"가 아니라 "살 게 없음"이라 블로커는 띄우지 않는다.
        SetBlockerActive(false);
        HideBlockReason();
    }

    /// <summary>이번 라운드에 이 슬롯을 리롤할 수 있는지 반영. 컨트롤러가 ShopManager 상태를 보고 호출한다.</summary>
    public void SetRerollAvailable(bool available)
    {
        if (_rerollButton != null) _rerollButton.interactable = available;
    }

    /// <summary>
    /// 구매 가능 여부 반영. 불가하면 회색 블로커로 카드를 덮어 클릭을 막고,
    /// 블로커를 누르면 reason을 잠시 표시한다. 리롤 버튼은 블로커 밖에 있어야 계속 눌린다.
    /// </summary>
    public void SetPurchasable(bool canBuy, string reason)
    {
        _blockReason = reason;

        _buyButton.interactable = canBuy;
        SetBlockerActive(!canBuy);

        if (canBuy) HideBlockReason();
    }

    private void SetBlockerActive(bool on)
    {
        if (_purchaseBlocker != null) _purchaseBlocker.SetActive(on);
    }

    private void ShowBlockReason()
    {
        if (_blockReasonText == null || string.IsNullOrEmpty(_blockReason)) return;

        _blockReasonText.text = _blockReason;
        _blockReasonText.gameObject.SetActive(true);

        if (_reasonRoutine != null) StopCoroutine(_reasonRoutine);
        _reasonRoutine = StartCoroutine(HideBlockReasonAfterDelay());
    }

    private IEnumerator HideBlockReasonAfterDelay()
    {
        yield return new WaitForSeconds(_blockReasonDuration);
        _reasonRoutine = null;
        if (_blockReasonText != null) _blockReasonText.gameObject.SetActive(false);
    }

    private void HideBlockReason()
    {
        if (_reasonRoutine != null)
        {
            StopCoroutine(_reasonRoutine);
            _reasonRoutine = null;
        }

        if (_blockReasonText != null) _blockReasonText.gameObject.SetActive(false);
    }

    /// <summary>
    /// 리롤 버튼은 여기서 제외한다 — 구매로 비워진 슬롯도 리롤 대상이라 Sold 상태에서 살아있어야 한다.
    /// 구매 버튼은 오브젝트째 끈다. ItemCard_Button에 Image가 붙어 있어서
    /// interactable만 꺼두면 버튼 프레임이 그대로 남아 Sold 표시 위에 겹친다.
    /// </summary>
    private void SetContentActive(bool on)
    {
        if (_backPanel != null) _backPanel.SetActive(on);
        if (_ticketImage != null) _ticketImage.SetActive(on);
        _itemImage.gameObject.SetActive(on);
        _nameText.gameObject.SetActive(on);
        _priceText.gameObject.SetActive(on);
        _buyButton.gameObject.SetActive(on);
    }
}
