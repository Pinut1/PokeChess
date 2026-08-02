using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 시너지 툴팁 아래쪽 "효과를 받는 유닛" 아이콘 한 칸.
/// 코스트별 테두리를 갈아끼우고, 그 유닛이 <b>필드에 배치돼 있으면 채색·아니면 흑백</b>으로 표시한다.
/// 데이터 조회는 하지 않고 SynergyTooltipUI가 넘겨주는 것만 그린다.
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

    [Tooltip("코스트별 테두리. 아래 스프라이트 배열에서 코스트에 맞는 걸로 교체된다.")]
    [SerializeField] private Image _costFrame;

    [Tooltip("(선택) 이름 표기. 안 쓸 거면 비워둬도 된다.")]
    [SerializeField] private TextMeshProUGUI _nameText;

    [Header("코스트별 테두리 스프라이트 (인덱스 = cost-1, 1~5코스트)")]
    [SerializeField] private Sprite[] _costFrames = new Sprite[5];

    [Header("미배치 표시")]
    [Tooltip("PokeChess/UI/Grayscale 셰이더로 만든 머티리얼(Art/UI/Shaders/ui_Grayscale_mat). " +
             "비워두면 아래 색만 어두워지고 흑백은 적용되지 않는다.")]
    [SerializeField] private Material _grayscaleMaterial;

    [Tooltip("필드에 배치된 유닛의 색. 보통 흰색(원본 그대로).")]
    [SerializeField] private Color _placedTint = Color.white;

    [Tooltip("아직 배치하지 않은 유닛의 색. 흑백 위에 곱해져 더 가라앉는다.")]
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
            _icon.sprite = data.icon;
            // 아이콘이 아직 없는 포켓몬은 흰 사각형 대신 빈 칸으로 둔다(테두리는 남는다).
            _icon.enabled = data.icon != null;
            _icon.color = tint;
            _icon.material = placed ? null : _grayscaleMaterial;
        }

        if (_costFrame != null)
        {
            int index = Mathf.Clamp(data.cost - 1, 0, Mathf.Max(0, _costFrames.Length - 1));
            if (index < _costFrames.Length && _costFrames[index] != null)
                _costFrame.sprite = _costFrames[index];

            // 테두리는 코스트 색이 정보라 흑백까지 씌우지 않고 어둡게만 한다.
            _costFrame.color = tint;
        }

        // TMP에는 흑백 머티리얼을 씌우지 않는다 — SDF 렌더링이 깨진다(ShopCardUI와 같은 이유).
        if (_nameText != null)
        {
            _nameText.text = data.pokemonName;
            _nameText.color = tint;
        }
    }

    /// <summary>남는 칸. 그리드가 자리를 접도록 오브젝트째 끈다.</summary>
    public void SetEmpty()
    {
        CurrentData = null;
        gameObject.SetActive(false);
    }
}
