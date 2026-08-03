using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 유닛 상점 카드 한 장의 표시/구매 클릭을 담당.
/// 데이터 바인딩만 하고 실제 구매 처리(ShopManager.Buy)는 상위 컨트롤러(UnitShopBarUI)가 연결한다.
/// 카드 루트(이 컴포넌트가 붙는 오브젝트)는 CardContanier — 프리팹에서 항상 활성 상태로 유지해야
/// Horizontal Layout Group이 슬롯 자리를 접지 않는다. Sold 상태에서도 이 오브젝트 자체는 끄지 않는다.
/// </summary>
public class ShopCardUI : MonoBehaviour, IShopCardView<PokemonData>,
                          IPointerEnterHandler, IPointerExitHandler
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

    [Header("보유 중 강조 (CardFrame_Effect)")]
    [Tooltip("필드·벤치에 같은 종을 이미 갖고 있을 때만 켜져 은은하게 페이드되는 테두리 이펙트.")]
    [SerializeField] private Image _cardFrameEffect;
    [Tooltip("페이드 인/아웃 속도. 1.1이면 약 0.9초에 한 번 왕복한다.")]
    [SerializeField] private float _frameEffectPulseSpeed = 1.1f;
    [Tooltip("최대 알파(0~255). 은은하게 깔리도록 낮게 잡는다.")]
    [Range(0f, 255f)]
    [SerializeField] private float _frameEffectMaxAlpha = 80f;

    [Header("구매완료 오버레이 (미배치 시 비워둬도 동작)")]
    [SerializeField] private GameObject _soldOverlay;

    [Header("골드 부족 표시")]
    [Tooltip("골드가 모자랄 때 흑백으로 만들 이미지들(Pokemon_Illust, Price_CoinImg 등). " +
             "TMP 텍스트는 넣지 말 것 — SDF 렌더링이 깨진다.")]
    [SerializeField] private Image[] _grayscaleTargets;
    [Tooltip("PokeChess/UI/Grayscale 셰이더로 만든 머티리얼. 비워두면 흑백 없이 클릭만 막힌다.")]
    [SerializeField] private Material _grayscaleMaterial;

    public PokemonData CurrentData { get; private set; }

    private void Awake()
    {
        _buyButton.onClick.AddListener(HandleClicked);
    }

    /// <summary>클릭 시 호출할 콜백 등록. 실제 구매 로직은 컨트롤러가 넘겨준다(ShopManager 직접 참조 안 함).</summary>
    public event Action Clicked;

    private void HandleClicked() => Clicked?.Invoke();

    /// <summary>커서가 들고 날 때 발행(true=들어옴). 역할 설명창은 컨트롤러가 띄운다.</summary>
    public event Action<ShopCardUI, bool> Hovered;

    /// <summary>커서가 이 카드 위에 있는지. 리롤로 내용이 바뀔 때 툴팁을 다시 띄울지 판단하는 데 쓴다.</summary>
    public bool IsHovered { get; private set; }

    public void OnPointerEnter(PointerEventData eventData)
    {
        IsHovered = true;
        if (CurrentData != null) Hovered?.Invoke(this, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        IsHovered = false;
        Hovered?.Invoke(this, false);
    }

    /// <summary>
    /// 보유 중 강조를 켜고 끈다. 보유 판정은 보드를 아는 컨트롤러가 하고,
    /// 이 컴포넌트는 켜진 동안의 연출만 맡는다(카드는 매니저를 직접 참조하지 않는다).
    /// </summary>
    public void SetOwned(bool owned)
    {
        if (_cardFrameEffect == null) return;

        _cardFrameEffect.gameObject.SetActive(owned);
    }

    private void Update()
    {
        if (_cardFrameEffect == null || !_cardFrameEffect.gameObject.activeSelf) return;

        // 0 → 최대 → 0 왕복. 카드마다 Time.time을 공유하므로 5장이 같은 위상으로 숨쉰다.
        float t = Mathf.PingPong(Time.time * _frameEffectPulseSpeed, 1f);

        Color color = _cardFrameEffect.color;
        color.a = t * (_frameEffectMaxAlpha / 255f);
        _cardFrameEffect.color = color;
    }

    public void Bind(PokemonData data)
    {
        CurrentData = data;

        // 흑백/버튼 상태는 골드에 따라 결정되므로 여기서는 기본값으로 되돌리고,
        // 실제 판정은 컨트롤러가 바인딩 직후 SetAffordable로 덮는다.
        _buyButton.enabled = true;
        _buyButton.interactable = true;
        ApplyGrayscale(false);

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

    /// <summary>
    /// 골드로 살 수 있는지 반영. 못 사면 일러스트를 흑백으로 바꾸고 버튼을 끈다.
    /// interactable=false면 클릭뿐 아니라 Hover(Highlighted) 전환도 함께 막힌다 —
    /// Selectable이 비활성 상태에서는 상태 전환 자체를 하지 않기 때문에 Hover는 따로 처리할 게 없다.
    /// </summary>
    public void SetAffordable(bool affordable)
    {
        // interactable을 끄지 않는 이유: SpriteSwap의 Disabled 슬롯에는 "구매완료" 이미지가 들어 있어
        // 골드 부족 상태에서 이미 산 것처럼 보이게 된다. Button 컴포넌트를 통째로 끄면
        // Unity가 상태를 Normal로 되돌린 뒤 포인터 이벤트를 받지 않으므로,
        // 스프라이트는 그대로 두고 클릭·Hover만 막을 수 있다.
        _buyButton.enabled = affordable;
        ApplyGrayscale(!affordable);
    }

    /// <summary>지정된 이미지들만 흑백 머티리얼로 바꾼다. null을 넣으면 기본 UI 머티리얼로 돌아간다.</summary>
    private void ApplyGrayscale(bool gray)
    {
        if (_grayscaleMaterial == null || _grayscaleTargets == null) return;

        Material target = gray ? _grayscaleMaterial : null;
        foreach (var image in _grayscaleTargets)
            if (image != null) image.material = target;
    }

    /// <summary>슬롯이 비었을 때(구매 직후, 다음 리롤 전까지) 카드 콘텐츠를 숨기고 Sold 오버레이를 노출.</summary>
    public void SetSold()
    {
        CurrentData = null;
        Hovered?.Invoke(this, false); // 산 직후 커서가 그대로면 설명창이 남는다

        // 골드 부족으로 컴포넌트를 꺼둔 상태일 수 있어 되살린 뒤 interactable로 막는다
        // (Sold는 Disabled 스프라이트를 쓰는 정상 경로).
        _buyButton.enabled = true;
        _buyButton.interactable = false;
        ApplyGrayscale(false);

        _cardFrame.gameObject.SetActive(false);
        _illustration.gameObject.SetActive(false);
        _nameText.gameObject.SetActive(false);
        _pricePanel.SetActive(false);
        _synergySlot1.SetActive(false);
        _synergySlot2.SetActive(false);
        _choiceCardFrame.SetActive(false);
        SetOwned(false); // 빈 슬롯에 강조가 남지 않도록
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
