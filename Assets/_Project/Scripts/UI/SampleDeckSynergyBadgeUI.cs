using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 견본덱 목록 줄에 찍히는 시너지 뱃지 한 칸 — 등급 프레임 위에 시너지 심볼을 얹는다.
///
/// 게임 안 시너지 패널(<see cref="SynergyRowUI"/>)의 활성 블록과 <b>같은 프레임 스프라이트·같은
/// 색 규칙</b>을 쓴다. 프레임을 안 쓰고 심볼만 흰색으로 찍으면 "이 덱이 이 시너지를 몇 단계까지
/// 올리는지"가 목록에서 안 보인다 — 브론즈 4개인 덱과 골드 2개인 덱이 똑같이 생기게 된다.
/// (스프라이트·색은 에디터 도구가 <c>SynergyRow_Pf</c>에서 복사해 넣으므로 두 화면이 자동으로 맞는다)
///
/// 문턱을 못 넘은 시너지는 <see cref="SampleDeckPanelUI"/>가 목록에 넣기 전에 걸러내므로 여기까지
/// 오지 않는다. 아래 비활성 분기는 데이터가 바뀌었을 때를 위한 안전장치다.
/// </summary>
public class SampleDeckSynergyBadgeUI : MonoBehaviour
{
    [Tooltip("등급별로 스프라이트가 교체되는 프레임. 비활성이면 꺼진다.")]
    [SerializeField] private Image _frame;

    [Tooltip("시너지 심볼(SynergyData.icon).")]
    [SerializeField] private Image _symbol;

    [Header("등급별 프레임 스프라이트")]
    [SerializeField] private Sprite _uniqueFrame;
    [SerializeField] private Sprite _bronzeFrame;
    [SerializeField] private Sprite _silverFrame;
    [SerializeField] private Sprite _goldFrame;
    [SerializeField] private Sprite _prismFrame;

    [Header("등급별 프레임 색")]
    [Tooltip("흰색이면 스프라이트 원본 그대로 나온다(곱셈 틴트).")]
    [SerializeField] private Color _uniqueFrameColor = Color.white;
    [SerializeField] private Color _bronzeFrameColor = Color.white;
    [SerializeField] private Color _silverFrameColor = Color.white;
    [SerializeField] private Color _goldFrameColor = Color.white;
    [SerializeField] private Color _prismFrameColor = Color.white;

    [Header("심볼 색")]
    [Tooltip("프레임 위에 얹히는 활성 심볼. 밝은 프레임 위라 보통 검정이다.")]
    [SerializeField] private Color _activeSymbolColor = Color.black;

    [Tooltip("프레임 없이 찍히는 비활성 심볼.")]
    [SerializeField] private Color _inactiveSymbolColor = new(1f, 1f, 1f, 0.55f);

    /// <summary>지금 그리고 있는 시너지. 비었으면 null.</summary>
    public SynergyStatus CurrentStatus { get; private set; }

    public void Bind(SynergyStatus status)
    {
        CurrentStatus = status;

        if (status == null || status.data == null)
        {
            if (_frame != null) _frame.enabled = false;
            if (_symbol != null) _symbol.enabled = false;
            return;
        }

        SynergyGrade grade = SynergyGradeUtil.Of(status);
        bool active = grade != SynergyGrade.Inactive;

        if (_frame != null)
        {
            Sprite sprite = FrameOf(grade);

            // 비활성이거나 그 등급 스프라이트가 안 물려 있으면 프레임 없이 심볼만 찍는다.
            _frame.enabled = active && sprite != null;
            _frame.sprite = sprite;
            _frame.color = FrameColorOf(grade);
        }

        if (_symbol != null)
        {
            _symbol.sprite = status.data.icon;
            // 아이콘이 아직 없는 시너지는 흰 사각형 대신 빈 칸으로 둔다.
            _symbol.enabled = status.data.icon != null;
            _symbol.color = active ? _activeSymbolColor : _inactiveSymbolColor;
        }
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
            default:                  return null;
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
}
