using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 시너지 패널의 한 행. 활성/비활성 두 양식을 한 프리팹에 형제로 두고 통째로 토글한다
/// (프리팹을 2개로 나누면 활성↔비활성이 바뀔 때마다 Destroy/Instantiate가 필요해 레이아웃이 튄다).
/// 데이터 조회는 하지 않고 SynergyPanelUI가 넘겨주는 SynergyStatus만 그린다.
///
/// ⚠️ 이 오브젝트(행 루트)는 자리를 차지하는 껍데기라 절대 SetActive(false) 하지 않는다.
/// VerticalLayoutGroup에서 루트를 끄면 아래 행들이 위로 밀려 10번째 페이저 자리가 깨진다.
/// 빈 슬롯은 SetEmpty()로 안쪽 두 그룹만 꺼서 표현한다.
///
/// 마우스를 올리면 시너지 설명창(SynergyTooltipUI)을 띄운다. 툴팁 참조는 SynergyPanelUI가
/// Awake에서 넣어주므로 행마다 따로 배선할 필요가 없다.
/// ⚠️ 호버가 잡히려면 <b>행 루트에 Raycast Target이 켜진 Graphic</b>이 하나 있어야 한다
/// (알파 0인 Image면 충분하다). 자식 이미지 위에서 들어온 이벤트는 핸들러가 있는 이 루트로 올라온다.
/// </summary>
public class SynergyRowUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("활성 블록 (Synergy_ActiveGroup)")]
    [SerializeField] private GameObject _activeGroup;
    [Tooltip("등급별로 스프라이트가 교체되는 프레임 (Active_Frame)")]
    [SerializeField] private Image _activeFrame;
    [SerializeField] private Image _activeSymbol;
    [SerializeField] private TextMeshProUGUI _activeNameText;
    [Tooltip("티어 문턱 전체 나열 (SynergeCount_Text) — 현재 도달한 문턱만 밝게 표시")]
    [SerializeField] private TextMeshProUGUI _tierListText;
    [Tooltip("현재 활성 유닛 수 (NowCount+Text)")]
    [SerializeField] private TextMeshProUGUI _nowCountText;

    [Header("비활성 블록 (Synergy_InactiveGroup)")]
    [SerializeField] private GameObject _inactiveGroup;
    [SerializeField] private Image _inactiveSymbol;
    [SerializeField] private TextMeshProUGUI _inactiveNameText;
    [Tooltip("현재 수 / 첫 문턱 (CntTxt). 풀·독·물은 첫 문턱이 3이라 \"2 / 3\"도 나온다.")]
    [SerializeField] private TextMeshProUGUI _inactiveProgressText;

    [Header("등급별 프레임 스프라이트")]
    [SerializeField] private Sprite _uniqueFrame;
    [SerializeField] private Sprite _bronzeFrame;
    [SerializeField] private Sprite _silverFrame;
    [SerializeField] private Sprite _goldFrame;
    [SerializeField] private Sprite _prismFrame;

    [Header("등급별 프레임 색")]
    [Tooltip("흰색이면 스프라이트 원본 그대로 나온다(곱셈 틴트). 스프라이트만으로 부족한 등급만 바꾸면 된다.")]
    [SerializeField] private Color _uniqueFrameColor = Color.white;
    [SerializeField] private Color _bronzeFrameColor = Color.white;
    [SerializeField] private Color _silverFrameColor = Color.white;
    [SerializeField] private Color _goldFrameColor = Color.white;
    [SerializeField] private Color _prismFrameColor = Color.white;

    [Header("심볼 색")]
    [Tooltip("심볼 PNG는 흰색으로 제작 — Image.color를 곱해서 원하는 색을 낸다. " +
             "PNG 배경이 투명해야 하며, 불투명 흰색이면 사각형 전체가 칠해진다.")]
    [SerializeField] private Color _activeSymbolColor = Color.black;
    [SerializeField] private Color _inactiveSymbolColor = Color.white;

    [Header("티어 문턱 텍스트 서식")]
    [SerializeField] private Color _currentTierColor = Color.white;
    [SerializeField] private Color _otherTierColor = new Color(0.62f, 0.62f, 0.62f);
    [Tooltip("문턱 사이 구분자. 앞뒤 공백 포함.")]
    [SerializeField] private string _tierSeparator = " > ";

    public SynergyStatus CurrentStatus { get; private set; }

    // 설명창. SynergyPanelUI가 넣어준다(없으면 호버해도 아무 일도 일어나지 않는다).
    private SynergyTooltipController _tooltip;

    /// <summary>설명창 연결. SynergyPanelUI가 Awake에서 행 전부에 같은 컨트롤러를 물린다.</summary>
    public void AttachTooltip(SynergyTooltipController tooltip) => _tooltip = tooltip;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_tooltip != null) _tooltip.Show(this, CurrentStatus);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_tooltip != null) _tooltip.Hide(this);
    }

    public void Bind(SynergyStatus status)
    {
        CurrentStatus = status;

        if (status == null || status.data == null)
        {
            SetEmpty();
            return;
        }

        var grade = SynergyGradeUtil.Of(status);
        bool active = grade != SynergyGrade.Inactive;

        _activeGroup.SetActive(active);
        _inactiveGroup.SetActive(!active);

        if (active) BindActive(status, grade);
        else        BindInactive(status);

        NotifyTooltip();
    }

    /// <summary>표시할 시너지가 없는 슬롯. 자리는 유지한 채 안쪽만 비운다.</summary>
    public void SetEmpty()
    {
        CurrentStatus = null;
        _activeGroup.SetActive(false);
        _inactiveGroup.SetActive(false);

        NotifyTooltip();
    }

    /// <summary>
    /// 열려 있는 설명창이 이 행을 보고 있다면 새 내용으로 따라오게 한다.
    /// 유닛을 배치하면 정렬이 바뀌어 커서 아래 행의 시너지 자체가 바뀌는데,
    /// 이게 없으면 옛 시너지 설명이 남는다.
    /// </summary>
    private void NotifyTooltip()
    {
        if (_tooltip != null) _tooltip.RowRebound(this, CurrentStatus);
    }

    private void BindActive(SynergyStatus status, SynergyGrade grade)
    {
        _activeFrame.sprite = FrameOf(grade);
        _activeFrame.color = FrameColorOf(grade);
        _activeSymbol.sprite = status.data.icon;
        _activeSymbol.color = _activeSymbolColor;
        _activeNameText.text = status.data.synergyName;
        _nowCountText.text = status.uniqueCount.ToString();
        _tierListText.text = BuildTierListText(status);
    }

    private void BindInactive(SynergyStatus status)
    {
        _inactiveSymbol.sprite = status.data.icon;
        _inactiveSymbol.color = _inactiveSymbolColor;
        _inactiveNameText.text = status.data.synergyName;

        // 첫 문턱이 목표다 — 아직 1티어도 못 채운 상태만 비활성이므로.
        var tiers = status.data.tiers;
        int firstThreshold = (tiers != null && tiers.Count > 0) ? tiers[0].count : 0;
        _inactiveProgressText.text = $"{status.uniqueCount} / {firstThreshold}";
    }

    private Sprite FrameOf(SynergyGrade grade)
    {
        switch (grade)
        {
            case SynergyGrade.Unique: return _uniqueFrame;
            case SynergyGrade.Bronze: return _bronzeFrame;
            case SynergyGrade.Silver: return _silverFrame;
            case SynergyGrade.Gold:   return _goldFrame;
            case SynergyGrade.Prism:  return _prismFrame;
            default:                  return _bronzeFrame;
        }
    }

    private Color FrameColorOf(SynergyGrade grade)
    {
        switch (grade)
        {
            case SynergyGrade.Unique: return _uniqueFrameColor;
            case SynergyGrade.Bronze: return _bronzeFrameColor;
            case SynergyGrade.Silver: return _silverFrameColor;
            case SynergyGrade.Gold:   return _goldFrameColor;
            case SynergyGrade.Prism:  return _prismFrameColor;
            default:                  return Color.white;
        }
    }

    /// <summary>"2 > 4 > 6" 형태로 문턱 전체를 나열하고, 현재 도달한 문턱 숫자만 밝게 칠한다.</summary>
    private string BuildTierListText(SynergyStatus status)
    {
        var tiers = status.data.tiers;
        if (tiers == null || tiers.Count == 0) return string.Empty;

        string curHex = ColorUtility.ToHtmlStringRGB(_currentTierColor);
        string othHex = ColorUtility.ToHtmlStringRGB(_otherTierColor);

        var sb = new StringBuilder();
        for (int i = 0; i < tiers.Count; i++)
        {
            if (i > 0)
                sb.Append("<color=#").Append(othHex).Append('>').Append(_tierSeparator).Append("</color>");

            string hex = i == status.activeTierIndex ? curHex : othHex;
            sb.Append("<color=#").Append(hex).Append('>').Append(tiers[i].count).Append("</color>");
        }
        return sb.ToString();
    }
}
