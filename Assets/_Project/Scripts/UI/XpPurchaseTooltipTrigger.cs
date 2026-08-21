using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// XP 구매 버튼(씬의 "Level Button")에 붙이는 설명 툴팁.
/// 표시는 <see cref="RoleTooltipController"/>를 그대로 빌려 쓴다 — 그쪽은 역할군 대응표에 없는
/// 문자열이 오면 형식을 입히지 않고 원문 그대로 내보내므로 아무 문구나 띄울 수 있다.
///
/// <see cref="TextTooltipTrigger"/>와 하는 일은 같지만, <b>숫자를 인스펙터에 적어두지 않고</b>
/// 호버할 때마다 ShopManager에서 읽어 채운다는 점이 다르다. XP 획득량·구매 비용·라운드 지급량은
/// 전부 "밸런스 확정 후 조정" 딱지가 붙은 임시값이라(ShopManager 인스펙터), 문구에 숫자를 박아두면
/// 표만 바뀌었을 때 툴팁이 조용히 거짓말을 하게 된다.
///
/// 문구는 아래 서식 문자열에 <b>이름표</b>를 넣어 쓴다(대소문자 구분):
///   {key}       단축키 이름. 같은 오브젝트의 ButtonHotkey에서 읽어 "[F]" 꼴로 넣는다
///               (단축키가 없으면 통째로 사라진다 — 대괄호까지)
///   {buyXp}     구매 1회로 얻는 XP
///   {cost}      구매 1회 비용(골드). 기본으로 노란색이 입혀진다(인스펙터 "숫자 색상")
///   {roundXp}   라운드 시작마다 자동으로 받는 XP
///   {level}     현재 레벨
///   {nextLevel} 다음 레벨
///   {xp}        현재 레벨에서 모은 XP
///   {needXp}    다음 레벨까지 필요한 총 XP
///   {remainXp}  다음 레벨까지 남은 XP
///   {buyCount}  다음 레벨까지 필요한 구매 횟수(남은 XP ÷ 1회 획득 XP, 올림)
///   {goldToNext} 다음 레벨까지 드는 총 골드({buyCount} × 1회 비용). 기본으로 노란색이 입혀진다
///
/// ⚠️ {cost}는 <b>구매 1회</b> 비용이라 "{nextLevel}레벨까지 {cost}골드"처럼 쓰면 거짓말이 된다 —
/// Lv3부터는 필요 XP(6·10·20…)가 1회 획득량(4)을 넘어 여러 번 사야 한다. 다음 레벨까지의
/// 비용을 말하려면 반드시 {goldToNext}를 쓸 것.
///
/// TMP 리치텍스트(&lt;b&gt;, &lt;size&gt;, &lt;color&gt;, &lt;sprite&gt;)를 그대로 쓸 수 있다.
///
/// ⚠️ 커서를 받으려면 <b>Raycast Target이 켜진 Graphic</b>이 이 오브젝트에 있어야 한다.
/// 있으면 Awake에서 자동으로 켜주고, 하나도 없으면 경고를 남긴다(TextTooltipTrigger와 같은 규약).
///
/// ⚠️ 이 문구는 여러 줄짜리라 <b>한 줄용 RoleTooltip_Pf에 그대로 띄우면 넘친다</b>.
/// 큰 설명창을 따로 배치하고 그 전용 컨트롤러를 <see cref="_tooltip"/>에 직접 물릴 것 —
/// 비워두면 씬에서 아무거나 찾아 물어서(FindInScene) 한 줄짜리 창에 붙을 수 있다.
/// </summary>
public class XpPurchaseTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("문구")]
    [Tooltip("첫 줄(제목). 비우면 제목 줄 없이 본문만 나온다.")]
    [TextArea(1, 2)]
    [SerializeField] private string _titleFormat = "<b>XP 구매 {key}</b>";

    [Tooltip("본문. 이름표({buyXp} 등)는 호버할 때 실제 값으로 바뀐다.")]
    [TextArea(3, 8)]
    [SerializeField] private string _bodyFormat =
        "{buyXp} XP를 획득해 레벨 업하세요. 레벨 업하면 팀 규모가 커지고 상점에서 더 강력한 챔피언을 " +
        "구매할 수 있습니다. 매 라운드 시작마다 {roundXp} XP를 무료로 획득합니다.";

    [Tooltip("마지막 줄(비용·진행 표시). 비우면 이 줄은 나오지 않는다.")]
    [TextArea(1, 2)]
    [SerializeField] private string _footerFormat = "{nextLevel}레벨까지 {remainXp}";

    [Tooltip("최대 레벨이라 더 살 수 없을 때 마지막 줄 대신 쓸 문구. 비우면 줄이 사라진다.")]
    [TextArea(1, 2)]
    [SerializeField] private string _maxLevelFooter = "최대 레벨";

    [Tooltip("제목·본문·꼬리 사이에 넣을 줄바꿈 수. 1이면 바로 다음 줄, 2면 한 줄 띄운다.")]
    [Range(1, 3)]
    [SerializeField] private int _blankLines = 2;

    [Header("단축키 표기")]
    [Tooltip("{key}에 쓸 단축키. 비우면 같은 오브젝트의 ButtonHotkey를 찾는다 — 보통은 비워두면 된다.")]
    [SerializeField] private ButtonHotkey _hotkey;

    [Tooltip("{key}를 감쌀 서식. {0}=키 이름. 예: \"[{0}]\", \"({0})\"")]
    [SerializeField] private string _keyLabelFormat = "[{0}]";

    [Header("숫자 색상")]
    [Tooltip("골드로 나가는 값({cost}·{goldToNext})에 입힐 색. 끄면 본문과 같은 색으로 나온다.")]
    [SerializeField] private bool _colorGoldCost = true;

    [Tooltip("골드 값에 입힐 색. 알파는 무시된다(TMP color 태그는 RGB만 쓴다).")]
    [SerializeField] private Color _goldColor = new(1f, 0.83f, 0.25f);

    [Tooltip("XP 관련 값({buyXp}·{roundXp}·{remainXp}·{needXp}·{xp})에 입힐 색. 끄면 본문과 같은 색.")]
    [SerializeField] private bool _colorXpValues;

    [Tooltip("XP 값에 입힐 색. 위 토글을 켰을 때만 쓰인다.")]
    [SerializeField] private Color _xpColor = new(0.55f, 0.85f, 1f);

    [Header("배선")]
    [Tooltip("설명창 컨트롤러. 여러 줄이 들어가므로 큰 설명창 전용 컨트롤러를 직접 물릴 것. " +
             "비워두면 씬에서 찾는다(어느 것을 잡을지 보장되지 않아 경고가 남는다).")]
    [SerializeField] private RoleTooltipController _tooltip;

    // 커서가 올라와 있는 동안만 갱신 이벤트를 듣는다. 상시 구독하면 툴팁이 닫힌 채로도 문구를 만든다.
    private bool _hovering;

    private void Awake()
    {
        if (_hotkey == null) _hotkey = GetComponent<ButtonHotkey>();

        if (_tooltip == null) _tooltip = RoleTooltipController.FindInScene(this);

        EnsureRaycastTarget();
    }

    /// <summary>
    /// Raycast Target이 꺼져 있으면 커서 이벤트가 아예 오지 않는다 — 여기서 보장한다.
    /// Graphic이 하나도 없으면 아무리 붙여도 동작하지 않으므로 알려준다.
    /// </summary>
    private void EnsureRaycastTarget()
    {
        var graphic = GetComponent<Graphic>();

        if (graphic == null)
        {
            Debug.LogWarning(
                "[XpPurchaseTooltipTrigger] 이 오브젝트에 Image·Text 같은 Graphic이 없어 커서 이벤트를 " +
                "받을 수 없습니다. 버튼 배경 Image가 있는 오브젝트에 붙이세요.", this);
            return;
        }

        graphic.raycastTarget = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_tooltip == null) return;

        // 이 버튼은 툴팁을 띄운 채로 그대로 누르는 버튼이라, 커서를 올린 동안 숫자가 바뀐다.
        // 열 때 한 번만 그리면 방금 산 XP가 반영되지 않은 옛 숫자가 남는다.
        if (!_hovering) // Enter가 Exit 없이 두 번 오더라도 중복 구독하지 않는다
        {
            _hovering = true;
            GameEvents.OnXpChanged    += HandleProgressChanged;
            GameEvents.OnLevelChanged += HandleLevelChanged;
        }

        Refresh();
    }

    public void OnPointerExit(PointerEventData eventData) => Close();

    private void OnDisable()
    {
        // 커서를 올린 채 버튼이 꺼지면 PointerExit이 오지 않아 설명창만 화면에 남는다.
        Close();
    }

    private void HandleProgressChanged(int currentXp, int requiredXp) => Refresh();
    private void HandleLevelChanged(int level) => Refresh();

    private void Refresh()
    {
        if (!_hovering || _tooltip == null) return;

        string text = Compose();

        // 만들 문구가 없으면(씬 전환 중 ShopManager가 사라진 순간 등) 그냥 return하면 안 된다 —
        // 직전 문구가 화면에 그대로 남아 커서를 뗄 때까지 버틴다. 내 것만 닫는다.
        if (string.IsNullOrWhiteSpace(text))
        {
            _tooltip.Hide(this);
            return;
        }

        // 소유자를 자신으로 넘긴다 — 여러 버튼을 빠르게 훑어도 남의 툴팁을 끄지 않는다.
        _tooltip.Show(this, text);
    }

    private void Close()
    {
        if (_hovering)
        {
            GameEvents.OnXpChanged    -= HandleProgressChanged;
            GameEvents.OnLevelChanged -= HandleLevelChanged;
            _hovering = false;
        }

        if (_tooltip != null) _tooltip.Hide(this);
    }

    /// <summary>
    /// 지금 상점 상태로 문구를 만든다. ShopManager가 아직 없으면(씬 진입 직후 등)
    /// 숫자를 지어내지 않고 빈 문자열을 돌려준다 — 툴팁을 아예 열지 않는 쪽이 낫다.
    /// GameManager는 프로젝트 규칙대로 TryGet으로만 조회한다(Singleton.Instance 널 검사 금지).
    /// </summary>
    private string Compose()
    {
        if (!GameManager.TryGet(out var gm) || gm.Shop == null) return string.Empty;

        ShopManager shop = gm.Shop;

        // RequiredXp는 최대 레벨에서 0이 된다(GetRequiredXp의 범위 밖 반환값). 그때는 남은 XP를
        // 계산할 다음 레벨 자체가 없으므로 꼬리 줄을 최대 레벨 문구로 바꾼다.
        bool atMaxLevel = shop.RequiredXp <= 0;

        string separator = TooltipText.Separator(_blankLines);

        string title  = Fill(_titleFormat, shop, atMaxLevel);
        string body   = Fill(_bodyFormat, shop, atMaxLevel);
        string footer = Fill(atMaxLevel ? _maxLevelFooter : _footerFormat, shop, atMaxLevel);

        var sb = new System.Text.StringBuilder();

        TooltipText.Append(sb, title, separator);
        TooltipText.Append(sb, body, separator);
        TooltipText.Append(sb, footer, separator);

        return sb.ToString();
    }

    /// <summary>
    /// 이름표를 실제 값으로 바꾼다. 안 쓴 이름표는 그대로 남지 않고 전부 치환된다.
    /// 색을 입히는 이름표는 숫자를 TMP color 태그로 감싼다 - 문구 전체가 아니라
    /// <b>숫자만</b> 물들어야 "필요한 골드 4"에서 4만 노란색이 된다.
    /// </summary>
    private string Fill(string format, ShopManager shop, bool atMaxLevel)
    {
        if (string.IsNullOrEmpty(format)) return string.Empty;

        int required = shop.RequiredXp;
        int remain   = atMaxLevel ? 0 : Mathf.Max(0, required - shop.CurrentXp);

        // 남은 XP를 1회 획득량으로 나눠 올린다 — 4XP짜리 구매로 6XP를 채우려면 두 번 사야 한다.
        // 획득량이 0 이하면(인스펙터 오설정) 몇 번을 사도 레벨이 오르지 않으므로 0으로 둔다.
        int buyAmount = shop.BuyXpAmount;
        int buyCount  = (atMaxLevel || buyAmount <= 0) ? 0 : (remain + buyAmount - 1) / buyAmount;

        return format
            .Replace("{key}",       KeyLabel())
            .Replace("{buyXp}",     Xp(shop.BuyXpAmount))
            .Replace("{cost}",      Gold(shop.BuyXpCostGold))
            .Replace("{roundXp}",   Xp(shop.RoundXpReward))
            .Replace("{level}",     shop.CurrentLevel.ToString())
            .Replace("{nextLevel}", (shop.CurrentLevel + 1).ToString())
            .Replace("{xp}",        Xp(shop.CurrentXp))
            .Replace("{needXp}",    Xp(required))
            .Replace("{remainXp}",  Xp(remain))
            .Replace("{buyCount}",  buyCount.ToString())
            .Replace("{goldToNext}", Gold(buyCount * shop.BuyXpCostGold));
    }

    /// <summary>
    /// 단축키 표기("[F]"). 단축키가 안 붙어 있으면 빈 문자열이라 문구에서 통째로 빠진다 —
    /// 없는 키를 "[]"로 남겨두면 오히려 눈에 걸린다.
    /// </summary>
    private string KeyLabel() =>
        TooltipText.KeyLabel(_hotkey != null ? _hotkey.KeyLabel : string.Empty, _keyLabelFormat);

    private string Gold(int value) => Colorize(value, _colorGoldCost, _goldColor);
    private string Xp(int value)   => Colorize(value, _colorXpValues, _xpColor);

    /// <summary>
    /// 숫자를 색 태그로 감싼다. 토글이 꺼져 있으면 태그 없이 숫자만 - 꺼둔 상태에서 굳이
    /// color 태그를 넣으면 TMP가 파싱만 더 하고 결과는 같다.
    /// </summary>
    private static string Colorize(int value, bool enabled, Color color)
    {
        if (!enabled) return value.ToString();

        return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{value}</color>";
    }
}
