using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 시너지 툴팁 아래쪽 "효과를 받는 유닛" 아이콘 한 칸.
/// 코스트별 테두리를 갈아끼우고, 그 유닛이 <b>필드에 배치돼 있으면 채색·아니면 흑백</b>으로 표시한다.
/// 데이터 조회는 하지 않고 SynergyTooltipUI가 넘겨주는 것만 그린다.
///
/// 아이템 툴팁(<see cref="ItemTooltipUI"/>)의 "이 돌로 진화하는 포켓몬" 줄도 같은 칸을 쓴다.
/// 그쪽은 배치 여부와 무관한 목록이라 항상 placed=true로 부른다.
///
/// 빈 칸은 이 오브젝트를 통째로 끈다(SetEmpty). 시너지마다 멤버 수가 달라 칸 수도 달라지는데,
/// GridLayoutGroup이 켜진 칸만 자동으로 채워주기 때문에 자리를 비워둘 이유가 없다.
/// (시너지 패널 행과 달리 "n번째 칸 고정" 같은 제약이 없다)
/// </summary>
public class SynergyTooltipUnitSlot : MonoBehaviour
{
    [Header("표시")]
    [Tooltip("유닛 아이콘. 전용 아이콘이 나오기 전까지는 상점 카드와 같은 PokemonData.icon을 쓴다.")]
    [SerializeField] private Image _icon;

    [Tooltip("코스트별 테두리. 스프라이트는 프리팹에 잡아둔 것 하나를 그대로 쓰고 색만 코스트별로 바뀐다.")]
    [SerializeField] private Image _costFrame;

    [Tooltip("(선택) 이름 표기. 안 쓸 거면 비워둬도 된다.")]
    [SerializeField] private TextMeshProUGUI _nameText;

    [Header("코스트별 테두리 색 (인덱스 = cost-1, 1~5코스트)")]
    [Tooltip("테두리 스프라이트는 흰색 계열 하나만 두고 이 색을 곱해서 코스트를 구분한다.\n" +
             "Image.color가 곱셈이라 원본이 흰색일수록 지정한 색이 그대로 나온다 — " +
             "원본에 색이 섞여 있으면 곱해진 만큼 탁해진다.\n" +
             "칸 수가 배열보다 큰 코스트가 들어오면 마지막 색을 쓴다.")]
    [SerializeField] private Color[] _costColors =
    {
        new(0.62f, 0.66f, 0.70f, 1f), // 1코스트 — 회색
        new(0.20f, 0.74f, 0.38f, 1f), // 2코스트 — 초록
        new(0.22f, 0.55f, 0.92f, 1f), // 3코스트 — 파랑
        new(0.68f, 0.33f, 0.88f, 1f), // 4코스트 — 보라
        new(0.96f, 0.76f, 0.22f, 1f), // 5코스트 — 금색
    };

    [Header("미배치 표시")]
    [Tooltip("PokeChess/UI/Grayscale 셰이더로 만든 머티리얼(Art/UI/Shaders/ui_Grayscale_mat). " +
             "비워두면 아래 색만 어두워지고 흑백은 적용되지 않는다.")]
    [SerializeField] private Material _grayscaleMaterial;

    [Tooltip("필드에 배치된 유닛 아이콘의 색. 보통 흰색(원본 그대로).")]
    [SerializeField] private Color _placedTint = Color.white;

    [Tooltip("아직 배치하지 않은 유닛에 곱할 색. 아이콘에는 흑백 위에 곱해지고, " +
             "테두리에는 코스트 색에 곱해져 코스트는 알아보되 가라앉은 상태가 된다.")]
    [SerializeField] private Color _unplacedTint = new(0.55f, 0.55f, 0.55f, 1f);

    public PokemonData CurrentData { get; private set; }

    /// <summary>한 칸 채우기. placed=true면 채색, false면 흑백.</summary>
    public void Bind(PokemonData data, bool placed)
    {
        CurrentData = data;

        if (data == null)
        {
            SetEmpty();
            return;
        }

        gameObject.SetActive(true);
        Color tint = placed ? _placedTint : _unplacedTint;

        if (_icon != null)
        {
            // 전용 아이콘이 있으면 그것, 없으면 상점 일러스트로 폴백한다(PokemonData.UnitIcon).
            Sprite sprite = data.UnitIcon;

            _icon.sprite = sprite;
            // 그림이 아직 없는 포켓몬은 흰 사각형 대신 빈 칸으로 둔다(테두리는 남는다).
            _icon.enabled = sprite != null;
            _icon.color = tint;
            _icon.material = placed ? null : _grayscaleMaterial;
        }

        if (_costFrame != null)
        {
            // 스프라이트는 건드리지 않는다 — 흰색 테두리 한 장에 색만 곱해 코스트를 구분한다.
            // 미배치여도 흑백으로 뭉개지 않는 이유: 테두리 색 자체가 코스트 정보라
            // 회색이 되면 "안 뽑은 5코스트"인지 "1코스트"인지 구분이 안 된다.
            Color cost = CostColorOf(data.cost);
            _costFrame.color = placed ? cost : cost * _unplacedTint;
        }

        // TMP에는 흑백 머티리얼을 씌우지 않는다 — SDF 렌더링이 깨진다(ShopCardUI와 같은 이유).
        if (_nameText != null)
        {
            _nameText.text = data.pokemonName;
            _nameText.color = tint;
        }
    }

    /// <summary>코스트에 해당하는 테두리 색. 배열을 벗어나면 양 끝 값으로 맞춘다.</summary>
    private Color CostColorOf(int cost)
    {
        if (_costColors == null || _costColors.Length == 0) return Color.white;

        int index = Mathf.Clamp(cost - 1, 0, _costColors.Length - 1);
        return _costColors[index];
    }

    /// <summary>남는 칸. 그리드가 자리를 접도록 오브젝트째 끈다.</summary>
    public void SetEmpty()
    {
        CurrentData = null;
        gameObject.SetActive(false);
    }
}
