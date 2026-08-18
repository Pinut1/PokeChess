using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;

/// <summary>
/// 유닛 하나의 런타임 정보를 보여주는 상세창(StatInfo_Panel 프리팹에 붙인다).
/// 우클릭으로 열리며, 여는 쪽은 StatInfoController가 담당한다.
///
/// 스탯은 <b>PokemonData 원본이 아니라 PokemonUnit의 런타임 값</b>을 읽는다.
/// 성급 배수·영웅증강 배수·진화 상태가 이미 반영된 값이라 실제 전투 수치와 일치한다.
///
/// HP/마나는 매 프레임 갱신한다. 전투 중에는 실제로 깎이는 값이 BattleUnit에 있으므로
/// 원본 PokemonUnit이 아니라 그쪽을 읽는다(UnitStatusBarHud와 같은 이유 — HP 변경 이벤트가 없다).
///
/// 씬 배선 대응(StatInfo_Panel):
///   CardContanier_pf/Pokemon_Illust   → Illustration
///   CardContanier_pf/NameText          → Name Text
///   CardContanier_pf/SynergyIcon_1·2   → 시너지 슬롯 (ShopCardUI와 같은 구성)
///   Status_Background                  → Status Bar (UnitStatusBarUI)
///   Status_Background/hpText·mpText    → Hp Text / Mana Text
///   Role_Image/RoleText                → Role Text
///   StarRange_Image                    → Star Image (성급별 스프라이트 교체)
///   info_Panel의 텍스트 2개            → Placement Text / Range Text
///   Stat_Text                          → Stat Text (방어력·공격력·공격속도·주문력)
///   info_Panel/itemSlot_Image (1)(2)   → Item Icons
///     (Item Icons에 물리는 건 칸 테두리(1)(2)가 아니라 그 안의 아이콘 Image — itemSlot_Image (3)(4))
///
/// 장착 아이템 칸에 커서를 올리면 아이템 상점·인벤토리와 같은 설명창(ItemTooltipUI)이 뜬다.
/// 칸마다 ItemHoverTarget을 런타임에 붙이므로 프리팹 배선은 늘지 않는다 — 자세한 건 SetupItemHovers.
/// </summary>
public class StatInfoPanelUI : MonoBehaviour
{
    /// <summary>역할 영문명과 표시용 한글명 한 쌍. 데이터에는 영문(Archer 등)만 들어 있다.</summary>
    [System.Serializable]
    public class RoleLabel
    {
        public string role;    // 데이터의 role 문자열 (Archer, Tanker …)
        public string korean;  // 표시용 (원거리딜러, 탱커 …)
    }

    [Header("기본 정보")]
    [Tooltip("Pokemon_Illust. PokemonData.icon이 그대로 카드 일러스트다.")]
    [SerializeField] private Image _illustration;

    [Tooltip("NameText.")]
    [SerializeField] private TextMeshProUGUI _nameText;

    [Tooltip("RoleText. 영웅증강으로 역할이 바뀌면 바뀐 역할이 나온다.")]
    [SerializeField] private TextMeshProUGUI _roleText;

    [Tooltip("역할 표기 형식. {0}=데이터의 영문 역할, {1}=아래 표에서 찾은 한글 이름.\n" +
             "한글 이름을 못 찾으면 영문만 표시한다.")]
    [SerializeField] private string _roleFormat = "{0}({1})";

    [Tooltip("역할 영문 → 한글 대응표. 데이터의 role 문자열과 대소문자 무관하게 맞춘다.")]
    [SerializeField] private RoleLabel[] _roleLabels =
    {
        new() { role = "Tanker",    korean = "탱커" },
        new() { role = "Warrior",   korean = "전사" },
        new() { role = "Assassin",  korean = "암살자" },
        new() { role = "Magician",  korean = "마법사" },
        new() { role = "Archer",    korean = "원거리딜러" },
        new() { role = "Supporter", korean = "서포터" },
    };

    [Tooltip("코스트(구매가, PokemonData.cost). 상점 카드(ShopCardUI._priceText)와 같은 원본을 그대로 " +
             "표시한다 — 아군/적/프리뷰/미러전투/파트너 유닛 전부 동일하게 이 값을 쓴다. " +
             "옛 이름은 _sellPriceText(판매가)였으나 원래 의미(코스트)로 되돌렸다 — FormerlySerializedAs로 " +
             "기존 프리팹 배선(Price_Panel/PriceText)을 그대로 승계한다.")]
    [FormerlySerializedAs("_sellPriceText")]
    [SerializeField] private TextMeshProUGUI _costText;

    [FormerlySerializedAs("_sellPriceFormat")]
    [SerializeField] private string _costFormat = "{0}";

    [Tooltip("성급 아이콘. 성급에 따라 스프라이트만 갈아끼운다.")]
    [SerializeField] private Image _starImage;

    [Tooltip("1성·2성·3성 스프라이트를 순서대로 3개. 성급에 해당하는 칸이 비어 있으면 아이콘을 숨긴다.")]
    [SerializeField] private Sprite[] _starSprites = new Sprite[3];

    [Header("시너지 (ShopCardUI와 같은 구성)")]
    [SerializeField] private GameObject _synergySlot1;      // SynergyIcon_1
    [SerializeField] private Image _synergyIcon1;           // SynergyIcon_Simbol1
    [SerializeField] private TextMeshProUGUI _synergyName1; // SynergyIcon_Name1
    [SerializeField] private GameObject _synergySlot2;      // SynergyIcon_2
    [SerializeField] private Image _synergyIcon2;           // SynergyIcon_Simbol2
    [SerializeField] private TextMeshProUGUI _synergyName2; // SynergyIcon_Name2

    [Header("게이지 (실시간)")]
    [Tooltip("Status_Background — 머리 위 HP바와 같은 UnitStatusBarUI. HP 눈금까지 그대로 나온다.")]
    [SerializeField] private UnitStatusBarUI _statusBar;

    [Tooltip("hpText. 바 프리팹에는 숫자 표기가 없어 이쪽에서 채운다.")]
    [SerializeField] private TextMeshProUGUI _hpText;
    [Tooltip("mpText.")]
    [SerializeField] private TextMeshProUGUI _manaText;

    [Header("배치 / 사거리 (각각 별도 텍스트)")]
    // 평타 사거리(PokemonData.attackRange)는 VFX 접미사로 베이킹돼 근거리 1 / 원거리 4 두 값만 나온다.
    // 그래서 "전방/후방"을 사거리로 판정할 수 있다.
    [Tooltip("배치 표기. 아래 문구가 통째로 들어간다.")]
    [SerializeField] private TextMeshProUGUI _placementText;

    [Tooltip("사거리가 이 값 이하면 전방(근거리)으로 본다.")]
    [SerializeField] private int _frontRangeMax = 1;

    [SerializeField] private string _frontLabel = "배치: 전방";
    [SerializeField] private string _backLabel  = "배치: 후방";

    [Tooltip("사거리 표기. {0}에 사거리 숫자가 들어간다.")]
    [SerializeField] private TextMeshProUGUI _rangeText;
    [SerializeField] private string _rangeFormat = "사거리: {0}";

    [Header("스탯 (Stat_Text 하나에 모아 표시)")]
    [Tooltip("Stat_Text — 방어력·공격력·공격속도·주문력을 한 덩어리로 표시한다.")]
    [SerializeField] private TextMeshProUGUI _statText;

    [Tooltip("표기 형식. 줄바꿈·순서·문구를 자유롭게 바꿀 수 있고, 안 쓸 자리는 빼면 된다.\n" +
             "{0}=방어력  {1}=평타데미지(공격력)  {2}=공격속도  {3}=스킬데미지(주문력)\n" +
             "배치·사거리는 Placement Text / Range Text가 따로 표시하므로 여기엔 넣지 않는다.")]
    [TextArea(3, 8)]
    [SerializeField] private string _statFormat =
        "방어력: {0}\n평타데미지: {1}\n공격속도: {2}\n스킬데미지: {3}";

    [Header("장착 아이템")]
    [Tooltip("itemSlot_Image (1)(2) 안의 아이콘 Image. 착용한 개수만큼만 켜진다. 진화의 돌도 한 칸을 차지한다.")]
    [SerializeField] private Image[] _itemIcons;

    [Tooltip("아이템 칸에 커서를 올렸을 때 띄울 설명창 컨트롤러(아이템 상점·인벤토리와 같은 것을 쓰면 된다).\n" +
             "비워두면 씬에서 찾아 쓴다 — 이 패널을 프리팹에서 생성하는 경우 씬 오브젝트를 인스펙터에 꽂을 수 없기 때문.")]
    [SerializeField] private ItemTooltipController _itemTooltip;

    [Tooltip("역할군 설명창. 비어 있으면 씬에서 찾는다(프리팹은 씬 오브젝트를 참조할 수 없다). " +
             "Role_Image에 커서를 올리면 이 역할군이 쓰는 추천 아이템이 뜬다.")]
    [SerializeField] private RoleTooltipController _roleTooltip;

    [Tooltip("역할군 아이콘(Role_Image). 비어 있으면 Role Text의 부모에서 찾는다.")]
    [SerializeField] private Image _roleImage;

    // 보드 유닛으로 열었으면 _unit, 전투 유닛(적 포함)으로 열었으면 _battleUnit, 쇼핑 중 파트너
    // 유닛(읽기 전용 표시 데이터)으로 열었으면 _partnerView — 셋 중 하나만 채워진다.
    private PokemonUnit _unit;
    private BattleUnit _battleUnit;
    private OpponentBoardView.PartnerBoardUnitView? _partnerView;
    private float _partnerViewMaxHp; // Bind(PartnerBoardUnitView)에서 1회 계산해 캐시 — RefreshGauges 참고
    private RectTransform _rect;

    // 아이콘 칸마다 하나씩. _itemIcons와 같은 순서다(Awake에서 붙인다).
    private ItemHoverTarget[] _itemHovers;

    // Role_Image에 런타임으로 붙이는 커서 감지기. 지금 표시 중인 역할군을 들고 있는다.
    private RoleHoverTarget _roleHover;

    // 역할군 설명창에 넘길 현재 표시 대상. 보드/전투/파트너 어느 경로로 열렸든 여기에 담긴다.
    private string _currentRole;
    private PokemonData _currentData;

    // 지금 보고 있는 대상이 진화 잠금인지(나인이볼부스트 이브이 등). 잠겨 있으면 돌을 추천하지 않는다.
    //
    // ⚠️ _unit을 직접 보면 안 된다 — Bind(BattleUnit)·Bind(PartnerBoardUnitView)는 _unit을 null로
    // 두므로, 전투 중이나 파트너 보드에서는 잠긴 유닛도 "진화 가능"으로 잘못 나온다.
    // 그래서 각 Bind가 자기 경로에 맞는 값을 여기 직접 채운다.
    private bool _currentEvolutionLocked;

    public RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

    /// <summary>보드 유닛으로 열려 있으면 그 유닛. 아니면 null.</summary>
    public PokemonUnit Unit => _unit;

    /// <summary>전투 유닛으로 열려 있으면 그 유닛. 아니면 null.</summary>
    public BattleUnit BattleUnit => _battleUnit;

    /// <summary>쇼핑 중 파트너 유닛(읽기 전용 표시 데이터)으로 열려 있는지.</summary>
    public bool IsShowingPartnerView => _partnerView.HasValue;

    /// <summary>보드/벤치 유닛 정보를 채우고 창을 켠다.</summary>
    public void Bind(PokemonUnit unit)
    {
        if (unit == null || unit.data == null)
        {
            Hide();
            return;
        }

        _unit = unit;
        _battleUnit = null;
        _partnerView = null;
        _currentEvolutionLocked = unit.evolutionLocked;
        gameObject.SetActive(true);

        FillIdentity(unit.data, unit.starLevel, unit.Role);
        RefreshUnitStatsAndItems();
        RefreshGauges(); // 켜는 프레임에 빈 게이지가 보이지 않게
    }

    /// <summary>
    /// 스탯 텍스트·장착 아이템 칸을 _unit 기준으로 다시 채운다. Bind 직후는 물론, 장착·해제 등으로
    /// GameEvents.OnInventoryChanged/OnUnitChanged가 뜰 때도 같은 경로로 재사용한다(중복 구현 금지).
    /// 장비 보너스는 PokemonUnit.ComputeFinalStats(공용 계산, Combat/ItemStatEffect와 동일 공식)로 구한다.
    /// </summary>
    private void RefreshUnitStatsAndItems()
    {
        if (_unit == null) return;

        _unit.ComputeFinalStats(
            out _, out _,
            out float finalAttack, out float finalSpellPower, out float finalAttackSpeed, out float finalDefense);

        FillStats(_unit.Range, finalDefense, finalAttack, finalAttackSpeed, finalSpellPower);
        FillItems(_unit.items, _unit.equippedStone);
    }

    /// <summary>
    /// 전투 중인 유닛 정보를 채우고 창을 켠다. 적도 그대로 볼 수 있다.
    /// 전투 중에는 화면에 보이는 게 원본 PokemonUnit이 아니라 전투 전용 인스턴스라 이 경로가 필요하다.
    /// </summary>
    public void Bind(BattleUnit unit)
    {
        if (unit == null || unit.data == null)
        {
            Hide();
            return;
        }

        _battleUnit = unit;
        _unit = null;
        _partnerView = null;
        // 전투 인스턴스는 잠금 플래그를 들고 있지 않다. 내 보드의 원본이 있으면 거기서 읽고,
        // 적처럼 원본이 없으면 알 수 없으니 잠기지 않은 것으로 둔다(돌 추천이 뜨는 쪽).
        _currentEvolutionLocked = unit.source != null && unit.source.evolutionLocked;
        gameObject.SetActive(true);

        // 아군이고 보드 위 원본 참조가 있으면(전투 전용 인스턴스가 아니라 실제 내 유닛) 그 Role을
        // 우선한다 — 영웅증강으로 바뀐 역할이 거기 남아 있다. 적/미러전투는 원본이 없다(source==null).
        bool hasSource = unit.team == BattleTeam.Ally && unit.source != null;

        string role = hasSource ? unit.source.Role
                    : !string.IsNullOrEmpty(unit.role) ? unit.role
                    : unit.data.role;

        FillIdentity(unit.data, unit.starLevel, role);
        FillStats(unit.range, unit.defense, unit.attack, unit.attackSpeed, unit.spellPower);
        FillItems(unit.displayItems, unit.displayStone);
        RefreshGauges();
    }

    /// <summary>
    /// 쇼핑 중 파트너 유닛(OpponentBoardView.PartnerBoardUnitView, 읽기 전용 표시 데이터)으로 창을 연다.
    /// 실제 PokemonUnit이 없으므로(가짜 PokemonUnit을 만들어 속이지 않는다는 원칙) BoardSnapshot에서
    /// 온 species/starLevel/items/roleOverride/attackRangeOverride/heroStatMultiplier만으로 채울 수
    /// 있는 항목만 정확히 표시한다.
    ///
    /// 역할/사거리/스탯 배수는 영웅증강 등으로 바뀐 런타임 값(BoardSnapshot.Entry.roleOverride 등,
    /// 2026-08 확장)을 phantom에 그대로 옮겨 PokemonUnit의 기존 계산 프로퍼티(Role/Range/
    /// ComputeFinalStats)를 그대로 재사용한다 — 파치리스/이브이 같은 특정 증강을 이 UI가 직접
    /// 분기하지 않는다. 진화의 돌/통신진화 여부도 마찬가지로 BoardSnapshot.Entry(isTradeEvolved/
    /// equippedStoneId, 2026-08 확장)에서 그대로 옮겨 IsSpecialEvolved 판정(특수 진화 배율표
    /// 1.0/2.0/3.0)이 실제 유닛과 동일하게 적용된다.
    /// HP/마나는 UnitStatusBarHud.DrawPartnerShopBars와 동일한 근거(PokemonUnit.ResetForBattle 계약)로
    /// 항상 가득/빔으로 표시한다.
    /// </summary>
    public void Bind(OpponentBoardView.PartnerBoardUnitView view)
    {
        if (view.species == null)
        {
            Hide();
            return;
        }

        _battleUnit = null;
        _unit = null;
        _partnerView = view;
        // ⚠️ BoardSnapshot.Entry에는 evolutionLocked가 있지만 PartnerBoardUnitView가 그걸 나르지
        // 않는다(Network/OpponentBoardView.cs). 파트너의 잠긴 이브이에는 돌 추천이 뜬다 —
        // 고치려면 그 struct에 필드를 하나 더해야 해서 Network 담당(영욱)과 조율이 필요하다.
        _currentEvolutionLocked = false;
        gameObject.SetActive(true);

        // ComputeFinalStats/Range/Role을 읽기 전에 반드시 override가 반영돼야 한다 — CreatePhantom
        // 내부 설정 순서가 이를 보장한다(미러 전투 CreateMirrorBattleUnit과 동일한 순서 제약).
        // 파트너 정보창은 ComputeFinalStats로 장비 포함 최종 스탯을 계산하므로 items를 채운다.
        var phantom = PokemonUnit.CreatePhantom("PartnerStatPhantom", new PhantomUnitConfig
        {
            species = view.species,
            starLevel = view.starLevel,
            roleOverride = view.roleOverride,
            attackRangeOverride = view.attackRangeOverride,
            heroStatMultiplier = view.heroStatMultiplier,
            isTradeEvolved = view.isTradeEvolved,
            equippedStone = view.equippedStone,
            items = view.items,
        });

        FillIdentity(view.species, view.starLevel, phantom.Role);

        phantom.ComputeFinalStats(
            out float finalMaxHp, out _,
            out float finalAttack, out float finalSpellPower, out float finalAttackSpeed, out float finalDefense);
        int range = phantom.Range;

        Destroy(phantom.gameObject);

        // HP 절대 수치 표시(hpText)에 쓸 값 — RefreshGauges가 매 프레임 새로 만들지 않도록 Bind
        // 시점에 한 번만 계산해 캐시한다. 실제 currentHp는 전송되지 않으므로(§ 클래스 doc) 항상 이
        // maxHp 그대로("가득 참")를 쓴다 — 게이지 바는 UnitStatusBarHud와 같은 이유로 눈금은 끈다.
        _partnerViewMaxHp = finalMaxHp;

        FillStats(range, finalDefense, finalAttack, finalAttackSpeed, finalSpellPower);
        FillItems(view.items, view.equippedStone);
        RefreshGauges();
    }

    public void Hide()
    {
        _unit = null;
        _battleUnit = null;
        _partnerView = null;
        _currentEvolutionLocked = false;
        gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────
    // 아이템 설명창 (호버)
    // ─────────────────────────────────────────

    private void Awake()
    {
        // 씬 배치본이면 인스펙터에 꽂아둔 게 쓰이고, 프리팹에서 생성한 경우엔 여기서 찾는다
        // (프리팹은 씬 오브젝트를 참조할 수 없어 인스펙터에 꽂아둘 방법이 없다).
        if (_itemTooltip == null)
            _itemTooltip = ItemTooltipController.FindInScene(this);

        if (_roleTooltip == null) _roleTooltip = RoleTooltipController.FindInScene(this);

        SetupItemHovers();
        SetupRoleHover();
    }

    /// <summary>
    /// 역할군 아이콘에 커서 감지기를 붙인다. 아이템 칸과 같은 방식으로 런타임에 붙여
    /// 프리팹 배선을 늘리지 않는다.
    /// </summary>
    private void SetupRoleHover()
    {
        // 인스펙터에 안 물렸으면 문서화된 구조(Role_Image/RoleText)에서 부모를 찾는다.
        if (_roleImage == null && _roleText != null)
            _roleImage = _roleText.GetComponentInParent<Image>();

        if (_roleImage == null) return;

        // Raycast Target이 꺼져 있으면 커서 이벤트가 아예 오지 않는다 — 여기서 보장한다.
        _roleImage.raycastTarget = true;

        _roleHover = _roleImage.GetComponent<RoleHoverTarget>();
        if (_roleHover == null) _roleHover = _roleImage.gameObject.AddComponent<RoleHoverTarget>();

        _roleHover.Hovered += HandleRoleHovered;
    }

    private void HandleRoleHovered(RoleHoverTarget target, bool entered)
    {
        if (_roleTooltip == null) return;

        if (!entered || string.IsNullOrEmpty(_currentRole))
        {
            _roleTooltip.Hide(target);
            return;
        }

        // 보드 유닛으로 연 경우엔 진화잠금 여부까지 반영된다(나인이볼부스트 이브이 등).
        _roleTooltip.Show(target, _currentRole, _currentData, _currentEvolutionLocked);
    }

    /// <summary>
    /// 아이콘 칸마다 커서 감지기를 붙인다. 프리팹에 미리 달지 않는 건 배선을 늘리지 않기 위해서다 —
    /// 이미 Item Icons에 물려 있는 아이콘 오브젝트를 그대로 쓴다.
    ///
    /// 아이콘 오브젝트는 빈 칸일 때도 살아 있어(FillItems는 오브젝트가 아니라 Image만 끈다)
    /// 컴포넌트가 유지된다. 대신 꺼진 Image는 레이캐스트에 안 걸리므로 빈 칸에는 설명창이 뜨지 않는다.
    /// </summary>
    private void SetupItemHovers()
    {
        if (_itemIcons == null || _itemIcons.Length == 0) return;

        _itemHovers = new ItemHoverTarget[_itemIcons.Length];

        for (int i = 0; i < _itemIcons.Length; i++)
        {
            Image icon = _itemIcons[i];
            if (icon == null) continue;

            // Raycast Target이 꺼져 있으면 커서 이벤트가 아예 오지 않는다 — 여기서 보장한다.
            icon.raycastTarget = true;

            ItemHoverTarget hover = icon.GetComponent<ItemHoverTarget>();
            if (hover == null) hover = icon.gameObject.AddComponent<ItemHoverTarget>();

            hover.Hovered += HandleItemHovered;
            _itemHovers[i] = hover;
        }
    }

    private void OnDestroy()
    {
        if (_roleHover != null) _roleHover.Hovered -= HandleRoleHovered;

        if (_itemHovers == null) return;

        foreach (ItemHoverTarget hover in _itemHovers)
            if (hover != null) hover.Hovered -= HandleItemHovered;
    }

    /// <summary>
    /// 창이 열려 있는 동안만 구독한다(Bind가 SetActive(true)를 해야 OnEnable이 돎).
    /// 장착·해제(InventoryChanged) / 진화·돌 장착 등(UnitChanged) 기존 이벤트를 그대로 재사용 —
    /// Update 폴링을 새로 추가하지 않는다.
    /// </summary>
    private void OnEnable()
    {
        GameEvents.OnInventoryChanged += HandleInventoryChanged;
        GameEvents.OnUnitChanged += HandleUnitChanged;
        GameEvents.OnUnitSold += HandleUnitSold;
    }

    private void OnDisable()
    {
        GameEvents.OnInventoryChanged -= HandleInventoryChanged;
        GameEvents.OnUnitChanged -= HandleUnitChanged;
        GameEvents.OnUnitSold -= HandleUnitSold;

        // 상세창이 닫히는데 아이템 설명창만 남지 않게.
        // HideAll이 아니라 칸별 Hide인 이유 — 컨트롤러를 인벤토리·아이템 상점과 공유하므로
        // HideAll은 그쪽이 띄운 설명창까지 꺼버린다(ItemInventoryUI.OnDisable과 같은 규칙).
        if (_itemTooltip == null || _itemHovers == null) return;

        foreach (ItemHoverTarget hover in _itemHovers)
            if (hover != null) _itemTooltip.Hide(hover);
    }

    /// <summary>장비/진화의 돌 장착·해제 시 인벤토리가 바뀌면 뜬다. 지금 열려 있는 유닛일 때만 재계산한다.</summary>
    private void HandleInventoryChanged()
    {
        if (_unit == null) return;
        RefreshUnitStatsAndItems();
    }

    /// <summary>
    /// 진화의 돌 장착/해제·통신진화·영웅증강 역할전환 등으로 unit.data/roleOverride 자체가 바뀔 때 뜬다.
    /// 종·역할이 바뀌면 이름·일러스트·역할·성급 아이콘·시너지·판매가도 같이 달라지므로
    /// FillIdentity도 다시 불러야 한다 — Bind()와 같은 인자 구성이라 여기서도 그대로 재사용한다.
    /// </summary>
    private void HandleUnitChanged(PokemonUnit unit)
    {
        if (_unit == null || _unit != unit) return;

        FillIdentity(unit.data, unit.starLevel, unit.Role);
        RefreshUnitStatsAndItems();
    }

    /// <summary>선택된 유닛이 판매되면 뜬다. 판매된 게 지금 열려 있는 유닛이면 창을 닫는다.</summary>
    private void HandleUnitSold(PokemonUnit unit)
    {
        if (_unit == unit) Hide();
    }

    /// <summary>아이템 칸에 커서가 들고 날 때 설명창을 여닫는다(인벤토리·아이템 상점과 같은 방식).</summary>
    private void HandleItemHovered(ItemHoverTarget hover, bool entered)
    {
        if (_itemTooltip == null) return;

        if (entered) _itemTooltip.Show(hover, hover.Data);
        else         _itemTooltip.Hide(hover);
    }

    private void Update()
    {
        // 전투 유닛이 죽으면 visual이 파괴된다 — 빈 창이 남지 않게 닫는다.
        if (_battleUnit != null && (!_battleUnit.IsAlive || _battleUnit.visual == null))
        {
            Hide();
            return;
        }

        // 보드 유닛으로 열려 있던 대상이 합체·판매 등으로 Destroy되면 Unity가 _unit을
        // "가짜 null"로 만든다(== null은 true, 그러나 실제 C# 참조는 아직 남아있음).
        // ReferenceEquals로 "애초에 바인딩된 적 없음"과 구분해야 한 번도 안 연 패널까지 닫히지 않는다.
        if (!ReferenceEquals(_unit, null) && _unit == null)
        {
            Hide();
            return;
        }

        // 파트너 유닛의 미러 visual이 사라지면(스냅샷 갱신으로 다른 배치가 됨/보드에서 빠짐) 닫는다.
        // OpponentBoardView는 스냅샷마다 활성 비주얼을 전부 풀로 반환했다 다시 배치하므로,
        // Transform 참조가 그대로 남아있어도 비활성(SetActive(false))일 수 있어 activeInHierarchy로 확인한다.
        if (_partnerView.HasValue && (_partnerView.Value.visual == null || !_partnerView.Value.visual.gameObject.activeInHierarchy))
        {
            Hide();
            return;
        }

        // 게이지만 매 프레임 갱신한다. 스탯은 전투 중 바뀌지 않으므로 Bind에서 끝낸다.
        if (_unit != null || _battleUnit != null || _partnerView.HasValue) RefreshGauges();
    }

    // ─────────────────────────────────────────
    // 표시 채우기
    // ─────────────────────────────────────────

    /// <summary>
    /// 보드 유닛·전투 유닛·파트너 유닛 공용. 코스트(PokemonData.cost, 상점 카드와 같은 원본)는
    /// 대상과 무관하게 항상 그대로 표시한다 — 아군/적/프리뷰/미러전투/파트너 유닛 모두 동일.
    /// </summary>
    private void FillIdentity(PokemonData data, int starLevel, string role)
    {
        if (_illustration != null)
        {
            _illustration.sprite = data.icon;
            _illustration.enabled = data.icon != null; // 일러스트 미등록이 흰 사각형으로 남지 않게
        }

        SetText(_nameText, data.pokemonName);

        _currentRole = role;
        _currentData = data;

        SetText(_roleText, FormatRole(role));
        ApplyStarIcon(starLevel);

        FillSynergies(data);

        if (_costText != null)
            _costText.text = string.Format(_costFormat, data.cost);
    }

    /// <summary>"Archer" → "Archer(원거리딜러)". 대응표에 없으면 영문만 그대로 쓴다.</summary>
    private string FormatRole(string role)
    {
        if (string.IsNullOrEmpty(role)) return "";

        string korean = null;
        if (_roleLabels != null)
        {
            foreach (RoleLabel label in _roleLabels)
            {
                if (label == null || string.IsNullOrEmpty(label.role)) continue;
                if (!string.Equals(label.role, role, System.StringComparison.OrdinalIgnoreCase)) continue;

                korean = label.korean;
                break;
            }
        }

        return string.IsNullOrEmpty(korean) ? role : string.Format(_roleFormat, role, korean);
    }

    /// <summary>성급에 맞는 스프라이트로 갈아끼운다. 해당 성급 스프라이트가 없으면 아이콘을 숨긴다.</summary>
    private void ApplyStarIcon(int starLevel)
    {
        if (_starImage == null) return;

        int index = starLevel - 1; // 1성 = 0번
        Sprite sprite = _starSprites != null && index >= 0 && index < _starSprites.Length
            ? _starSprites[index]
            : null;

        _starImage.sprite = sprite;
        _starImage.enabled = sprite != null;
    }

    /// <summary>ShopCardUI와 같은 방식 — 시너지 키로 SynergyDatabase에서 이름·아이콘을 찾는다.</summary>
    private void FillSynergies(PokemonData data)
    {
        List<string> synergies = data.synergies;

        BindSynergySlot(_synergySlot1, _synergyIcon1, _synergyName1, synergies, 0);
        BindSynergySlot(_synergySlot2, _synergyIcon2, _synergyName2, synergies, 1);
    }

    private static void BindSynergySlot(GameObject slot, Image icon, TextMeshProUGUI nameText,
                                        List<string> synergies, int index)
    {
        string key = synergies != null && synergies.Count > index ? synergies[index] : null;

        if (string.IsNullOrEmpty(key))
        {
            if (slot != null) slot.SetActive(false);
            return;
        }

        var synergy = SynergyDatabase.Instance != null ? SynergyDatabase.Instance.GetByKey(key) : null;

        if (slot != null) slot.SetActive(true);
        if (nameText != null) nameText.text = synergy != null ? synergy.synergyName : key;

        if (icon != null)
        {
            icon.sprite = synergy != null ? synergy.icon : null;
            icon.enabled = icon.sprite != null;
        }
    }

    /// <summary>
    /// 배치와 사거리를 채운다. 평타 사거리가 근거리 1 / 원거리 4 두 값뿐이라
    /// 그 값으로 전방·후방을 판정한다(별도 데이터 컬럼이 없다).
    /// </summary>
    private void FillStats(int range, float defense, float attack, float attackSpeed, float spellPower)
    {
        SetText(_placementText, range <= _frontRangeMax ? _frontLabel : _backLabel);
        SetText(_rangeText, string.Format(_rangeFormat, range));

        if (_statText == null) return;

        // ⚠️ 형식 문자열에 여기 넘기는 개수(4개)보다 큰 자리번호가 있으면 string.Format이
        // 예외를 던져 Bind가 통째로 멈춘다. 자리번호를 늘릴 땐 인자도 같이 늘릴 것.
        _statText.text = string.Format(
            _statFormat,
            defense.ToString("0"),        // {0} 방어력
            attack.ToString("0"),         // {1} 평타데미지
            attackSpeed.ToString("0.00"), // {2} 공격속도
            spellPower.ToString("0"));    // {3} 스킬데미지
    }

    private void FillItems(IReadOnlyList<ItemData> items, EvolutionStoneData stone)
    {
        if (_itemIcons == null) return;

        // 진화의 돌도 슬롯을 차지하므로 함께 보여준다(PokemonUnit.UsedSlots와 같은 기준).
        // 돌을 앞에 두는 순서는 머리 위 바(UnitStatusBarUI.SetItems)와 맞춘 것이다.
        // 스프라이트가 아니라 데이터 자체를 모으는 건 호버 설명창이 이름·설명까지 필요로 하기 때문.
        var entries = new List<ScriptableObject>();
        if (stone != null) entries.Add(stone);

        if (items != null)
            foreach (ItemData item in items)
                if (item != null) entries.Add(item);

        for (int i = 0; i < _itemIcons.Length; i++)
        {
            ScriptableObject entry = i < entries.Count ? entries[i] : null;

            if (_itemIcons[i] != null)
            {
                // 오브젝트를 끄지 않고 Image만 끈다.
                // 오브젝트째 끄면 뒷배경까지 사라지고, Grid Layout이 빈 칸을 접어버려
                // 두 번째 슬롯이 첫 번째 자리로 밀려 위치가 흔들린다.
                _itemIcons[i].enabled = entry != null;
                if (entry != null) _itemIcons[i].sprite = IconOf(entry);
            }

            BindItemHover(i, entry);
        }
    }

    private static Sprite IconOf(ScriptableObject data) => data switch
    {
        ItemData item            => item.icon,
        EvolutionStoneData stone => stone.icon,
        _                        => null
    };

    /// <summary>
    /// 칸 내용이 바뀔 때 커서 감지기도 같이 갱신한다.
    /// 커서를 올려둔 채 다른 유닛으로 다시 열면 Enter/Exit이 다시 오지 않으므로,
    /// 그때는 설명창을 직접 갈아끼운다(아이템 상점의 카드별 리롤과 같은 이유 — ItemShopBarUI.AfterBind).
    /// </summary>
    private void BindItemHover(int index, ScriptableObject data)
    {
        if (_itemHovers == null || index >= _itemHovers.Length) return;

        ItemHoverTarget hover = _itemHovers[index];
        if (hover == null) return;

        hover.SetData(data);

        if (!hover.IsHovered || _itemTooltip == null) return;

        if (data != null) _itemTooltip.Show(hover, data);
        else              _itemTooltip.Hide(hover);
    }

    // ─────────────────────────────────────────
    // 실시간 게이지
    // ─────────────────────────────────────────

    private void RefreshGauges()
    {
        float hp, maxHp, mana, maxMana;
        bool isAlly = true;

        if (_battleUnit != null)
        {
            hp = _battleUnit.currentHp;
            maxHp = _battleUnit.maxHp;
            mana = _battleUnit.currentMana;
            maxMana = _battleUnit.maxMana;
            isAlly = _battleUnit.team == BattleTeam.Ally; // 적이면 HP바가 붉게 나온다
        }
        else if (_unit != null)
        {
            // 전투 중이면 실제로 깎이는 값은 BattleUnit에 있다(그 값도 결국 ItemStatFormula로 계산됨).
            // 없으면(전투 외, 쇼핑 단계) PokemonUnit.ComputeFinalStats로 같은 공식을 직접 적용한다 —
            // 장비 미포함 원본 값(_unit.currentHp/_unit.MaxHp)을 쓰지 않는다.
            BattleUnit battleUnit = FindBattleUnit(_unit);

            if (battleUnit != null)
            {
                hp = battleUnit.currentHp;
                maxHp = battleUnit.maxHp;
            }
            else
            {
                _unit.ComputeFinalStats(out maxHp, out hp, out _, out _, out _, out _);
            }

            mana    = battleUnit != null ? battleUnit.currentMana : _unit.currentMana;
            maxMana = battleUnit != null ? battleUnit.maxMana     : _unit.EffectiveManaCost;
        }
        else if (_partnerView.HasValue)
        {
            // 쇼핑 중 파트너 유닛은 currentHp/currentMana 자체를 전송하지 않는다 — 필요가 없기
            // 때문이다. PokemonUnit.ResetForBattle()의 계약("전투 시작/라운드 진입 시 HP를 가득
            // 채우고 마나를 비움 — TFT 표준: 매 라운드 풀회복")과 BattleManager.Cleanup()이 전투
            // 종료 시 같은 메서드를 호출하는 것을 근거로, 쇼핑 단계에 존재하는 어떤 유닛이든
            // 이 상태를 벗어나지 않는다(UnitStatusBarHud.DrawPartnerShopBars와 동일 근거 — 임의
            // 가정이 아니라 실제 게임 규칙) — 그래서 항상 가득 찬 것으로 표시한다.
            // maxHp는 Bind에서 phantom PokemonUnit(진화의 돌/통신진화/영웅증강 값 포함)으로
            // 미리 계산해 캐시해둔 값(_partnerViewMaxHp).
            maxHp = _partnerViewMaxHp;
            hp = maxHp;
            // 종의 원본 manaCost가 아니라 effectiveManaCost(PokemonUnit.EffectiveManaCost가 BoardSnapshot을
            // 타고 그대로 전달된 값)를 써야 영웅증강으로 스킬을 주입받은 유닛(원본 종 manaCost=0)도
            // 정확히 마나 게이지가 뜬다(2026-08 코드리뷰 대응 — UnitStatusBarHud.DrawPartnerShopBars와 동일 원인).
            maxMana = _partnerView.Value.effectiveManaCost;
            mana = 0f;
        }
        else return;

        bool hasSkill = maxMana > 0f;

        if (_statusBar != null)
        {
            // 머리 위 바와 같은 규약 — 마나 비율이 음수면 마나 바를 숨긴다. maxHp는 눈금 계산용.
            float hpRatio   = maxHp > 0f ? hp / maxHp : 0f;
            float manaRatio = hasSkill ? mana / maxMana : -1f;

            _statusBar.SetValues(hpRatio, manaRatio, isAlly, maxHp);
        }

        SetGaugeText(_hpText, hp, maxHp);
        if (hasSkill) SetGaugeText(_manaText, mana, maxMana);
    }

    /// <summary>보드 위 원본에 대응하는 전투 인스턴스. 전투 중이 아니거나 참전하지 않았으면 null.</summary>
    private static BattleUnit FindBattleUnit(PokemonUnit unit)
    {
        if (!GameManager.TryGet(out var gm)) return null;

        var battle = gm.Battle;
        if (battle == null || battle.Units == null) return null;

        foreach (var bu in battle.Units)
            if (bu != null && bu.source == unit) return bu;

        return null;
    }

    private static void SetGaugeText(TextMeshProUGUI text, float current, float max)
    {
        if (text == null) return;

        text.text = $"{Mathf.CeilToInt(Mathf.Max(0f, current))} / {Mathf.CeilToInt(max)}";
    }

    private static void SetText(TextMeshProUGUI label, string value)
    {
        if (label != null) label.text = value;
    }
}
