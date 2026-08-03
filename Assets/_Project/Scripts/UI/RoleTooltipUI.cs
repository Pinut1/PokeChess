using TMPro;
using UnityEngine;

/// <summary>
/// 유닛의 역할군만 한 줄로 보여주는 작은 설명창의 표시부.
/// 여닫기와 자리잡기는 <see cref="RoleTooltipController"/>가 맡는다
/// (시너지·아이템 툴팁의 UI/Controller 분리와 같은 구조).
///
/// 프리팹 구조(RoleTooltip_Pf):
///   RoleTooltip_Pf   Image(배경)
///     Role_Text      TextMeshProUGUI
///
/// 역할 한글 대응표는 StatInfoPanelUI와 같은 내용을 여기에도 둔다.
/// 표가 두 벌이 되지만 스탯창의 검증된 인스펙터 배선을 건드리지 않는 쪽을 택했다
/// — 표기를 바꿀 일이 생기면 <b>두 곳을 같이</b> 고쳐야 한다.
/// </summary>
public class RoleTooltipUI : MonoBehaviour
{
    /// <summary>역할 영문명과 표시용 한글명 한 쌍. 데이터에는 영문(Archer 등)만 들어 있다.</summary>
    [System.Serializable]
    public class RoleLabel
    {
        public string role;    // 데이터의 role 문자열 (Archer, Tanker …)
        public string korean;  // 표시용 (원거리딜러, 탱커 …)
    }

    [Tooltip("Role_Text.")]
    [SerializeField] private TextMeshProUGUI _roleText;

    [Tooltip("역할 표기 형식. {0}=데이터의 영문 역할, {1}=아래 표에서 찾은 한글 이름.\n" +
             "한글 이름을 못 찾으면 영문만 표시한다.")]
    [SerializeField] private string _roleFormat = "{0}({1})";

    [Tooltip("역할 영문 → 한글 대응표. 데이터의 role 문자열과 대소문자 무관하게 맞춘다. " +
             "StatInfoPanelUI의 Role Labels와 같은 내용으로 유지할 것.")]
    [SerializeField] private RoleLabel[] _roleLabels =
    {
        new() { role = "Tanker",    korean = "탱커" },
        new() { role = "Warrior",   korean = "전사" },
        new() { role = "Assassin",  korean = "암살자" },
        new() { role = "Magician",  korean = "마법사" },
        new() { role = "Archer",    korean = "원거리딜러" },
        new() { role = "Supporter", korean = "서포터" },
    };

    /// <summary>자리를 잡는 쪽(컨트롤러)이 쓰는 루트 RectTransform.</summary>
    public RectTransform Rect { get; private set; }

    private void Awake() => Rect = transform as RectTransform;

    /// <summary>역할 문자열을 받아 표시한다. 비어 있으면 false — 컨트롤러가 열지 않는다.</summary>
    public bool Bind(string role)
    {
        if (string.IsNullOrEmpty(role)) return false;

        if (_roleText != null) _roleText.text = FormatRole(role);
        return true;
    }

    /// <summary>"Archer" → "Archer(원거리딜러)". 대응표에 없으면 영문만 그대로 쓴다.</summary>
    private string FormatRole(string role)
    {
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
}
