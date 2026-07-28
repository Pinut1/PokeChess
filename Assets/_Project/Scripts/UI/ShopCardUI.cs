using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 유닛 상점 카드 한 장의 표시/구매 클릭을 담당.
/// 데이터 바인딩만 하고 실제 구매 처리(ShopManager.Buy)는 상위 컨트롤러(UnitShopBarUI)가 연결한다.
/// 카드 루트(이 컴포넌트가 붙는 오브젝트)는 CardContanier — 프리팹에서 항상 활성 상태로 유지해야
/// Horizontal Layout Group이 슬롯 자리를 접지 않는다. Sold 상태에서도 이 오브젝트 자체는 끄지 않는다.
/// </summary>
public class ShopCardUI : MonoBehaviour, IShopCardView<PokemonData>
{
    [Header("프레임 / 버튼")]
    [SerializeField] private Image _cardFrame;   // Card_Frame의 Image
    [SerializeField] private Button _buyButton;  // Card_Frame의 Button (SpriteSwap)

    [Header("코스트별 프레임 스프라이트 (인덱스 = cost-1, 1~5코스트)")]
    [SerializeField] private Sprite[] _normalFrames = new Sprite[5];
    [SerializeField] private Sprite[] _highlightedFrames = new Sprite[5];
    [SerializeField] private Sprite[] _pressedFrames = new Sprite[5];

    [Header("일러스트 / 이름 / 가격")]
    [SerializeField] private Image _illustration;   // Pokemon_Illust
    [SerializeField] private TextMeshProUGUI _nameText;      // NameText
    [SerializeField] private GameObject _pricePanel;         // Price_Panel
    [SerializeField] private TextMeshProUGUI _priceText;     // PriceText

    [Header("시너지 슬롯 1/2")]
    [SerializeField] private GameObject _synergySlot1;       // SynergyIcon_1
    [SerializeField] private TextMeshProUGUI _synergyName1;  // SynergyIcon_Name1
    [SerializeField] private Image _synergyIcon1;            // SynergyIcon_Simbol1
    [SerializeField] private GameObject _synergySlot2;       // SynergyIcon_2
    [SerializeField] private TextMeshProUGUI _synergyName2;  // SynergyIcon_Name2
    [SerializeField] private Image _synergyIcon2;            // SynergyIcon_Simbol2

    [Header("2성 예고 오버레이")]
    [Tooltip("이 카드를 사면 2성이 되는지 여부를 표시. 현재 BoardManager에 보유 개수 조회 API가 없어 항상 꺼진 채로 둠(TODO).")]
    [SerializeField] private GameObject _choiceCardFrame;    // Choice_CardFrame

    [Header("구매완료 오버레이 (미배치 시 비워둬도 동작)")]
    [SerializeField] private GameObject _soldOverlay;

    public PokemonData CurrentData { get; private set; }

    private void Awake()
    {
        _buyButton.onClick.AddListener(HandleClicked);
    }

    /// <summary>클릭 시 호출할 콜백 등록. 실제 구매 로직은 컨트롤러가 넘겨준다(ShopManager 직접 참조 안 함).</summary>
    public event Action Clicked;

    private void HandleClicked() => Clicked?.Invoke();

    public void Bind(PokemonData data)
    {
        CurrentData = data;

        _buyButton.interactable = true;
        _cardFrame.gameObject.SetActive(true);
        _illustration.gameObject.SetActive(true);
        _nameText.gameObject.SetActive(true);
        _pricePanel.SetActive(true);
        if (_soldOverlay != null) _soldOverlay.SetActive(false);

        int idx = Mathf.Clamp(data.cost - 1, 0, _normalFrames.Length - 1);
        _cardFrame.sprite = _normalFrames[idx];

        var state = _buyButton.spriteState;
        if (idx < _highlightedFrames.Length && _highlightedFrames[idx] != null)
            state.highlightedSprite = _highlightedFrames[idx];
        if (idx < _pressedFrames.Length && _pressedFrames[idx] != null)
            state.pressedSprite = _pressedFrames[idx];
        _buyButton.spriteState = state;

        _illustration.sprite = data.icon;
        _nameText.text = data.pokemonName;
        _priceText.text = data.cost.ToString();

        BindSynergySlot(_synergySlot1, _synergyName1, _synergyIcon1, data.synergies, 0);
        BindSynergySlot(_synergySlot2, _synergyName2, _synergyIcon2, data.synergies, 1);

        // TODO: BoardManager에 (speciesId, starLevel) 보유 개수 조회 공개 API가 생기면
        // 여기서 2개 이상 보유 시 true로 전환. 현재는 항상 꺼둠.
        _choiceCardFrame.SetActive(false);
    }

    /// <summary>슬롯이 비었을 때(구매 직후, 다음 리롤 전까지) 카드 콘텐츠를 숨기고 Sold 오버레이를 노출.</summary>
    public void SetSold()
    {
        CurrentData = null;

        _buyButton.interactable = false;
        _cardFrame.gameObject.SetActive(false);
        _illustration.gameObject.SetActive(false);
        _nameText.gameObject.SetActive(false);
        _pricePanel.SetActive(false);
        _synergySlot1.SetActive(false);
        _synergySlot2.SetActive(false);
        _choiceCardFrame.SetActive(false);
        if (_soldOverlay != null) _soldOverlay.SetActive(true);
    }

    private static void BindSynergySlot(GameObject slot, TextMeshProUGUI nameText, Image icon,
                                         List<string> synergies, int index)
    {
        string key = (synergies != null && synergies.Count > index) ? synergies[index] : null;
        if (string.IsNullOrEmpty(key))
        {
            slot.SetActive(false);
            return;
        }

        var synergy = SynergyDatabase.Instance != null ? SynergyDatabase.Instance.GetByKey(key) : null;

        slot.SetActive(true);
        nameText.text = synergy != null ? synergy.synergyName : key;
        icon.sprite = synergy != null ? synergy.icon : null;
    }
}
