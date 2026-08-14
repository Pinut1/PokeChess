using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 견본덱 배치도에서 유닛에 커서를 올렸을 때 뜨는 설명창.
///
/// <b>일부러 스탯을 넣지 않는다</b> — 체력·공격력 같은 값은 성급·아이템·시너지·증강으로 런타임에
/// 계속 바뀌어서, 견본덱에 박아두면 실제와 어긋난 숫자를 보여주게 된다. 여기서는 조합을 짤 때
/// 필요한 "변하지 않는 것"만 보여준다.
///
/// <list type="bullet">
/// <item>이름 · 구매 비용(코스트)</item>
/// <item>역할군</item>
/// <item>시너지 1 / 시너지 2</item>
/// <item>공격 사거리 — 숫자 대신 칸 표시(■□)</item>
/// <item>추천 아이템 아이콘</item>
/// </list>
///
/// ⚠️ <b>항상 활성인 오브젝트</b>(창 루트 등)에 두고, 이 컴포넌트가 자기 자신을 켰다 껐다 한다.
/// 부모에 레이아웃 그룹을 두지 말 것 — 여기서 잡은 위치를 매 프레임 되돌린다.
/// </summary>
public class SampleDeckUnitTooltipUI : MonoBehaviour
{
    /// <summary>시너지 한 줄 — 아이콘과 이름을 묶어 인스펙터에서 한 쌍으로 다룬다.</summary>
    [System.Serializable]
    private struct SynergyLine
    {
        [Tooltip("줄 전체(아이콘+이름)의 루트. 그 자리에 시너지가 없으면 꺼진다.")]
        public GameObject root;

        [Tooltip("시너지 심볼(SynergyData.icon). 아이콘이 없는 시너지면 자동으로 꺼진다.")]
        public Image icon;

        [Tooltip("시너지 이름.")]
        public TMP_Text label;
    }

    [Tooltip("여닫는 대상. 보통 이 컴포넌트가 붙은 오브젝트의 자식(창 본체).")]
    [SerializeField] private RectTransform _root;

    [Header("표시")]
    [SerializeField] private TMP_Text _nameText;

    [Tooltip("구매 비용(코스트). 숫자만 넣고 단위는 씬에 적어둔다.")]
    [SerializeField] private TMP_Text _costText;

    [SerializeField] private TMP_Text _roleText;

    [Tooltip("시너지 줄 2개. 아이콘 + 이름 한 쌍이며, 그 자리에 시너지가 없으면 줄이 꺼진다.")]
    [SerializeField] private SynergyLine[] _synergyLines = new SynergyLine[2];

    [Tooltip("'공격 사거리: ■■□□□□'. 채운 칸 수가 attackRange다.")]
    [SerializeField] private TMP_Text _rangeText;

    [Header("추천 아이템")]
    [Tooltip("아이템 줄 전체. 추천 아이템이 없으면 오브젝트째 꺼진다.")]
    [SerializeField] private GameObject _itemGroup;

    [Tooltip("아이콘이 깔리는 부모. HorizontalLayoutGroup을 붙여둘 것.")]
    [SerializeField] private RectTransform _itemArea;

    [Tooltip("복제할 아이템 아이콘 한 칸. 부모 아래 비활성으로 둘 것.")]
    [SerializeField] private Image _itemIconTemplate;

    [Header("사거리 표시")]
    [Tooltip("사거리 칸의 총 개수. 가장 긴 사거리보다 크게 잡아둘 것.")]
    [SerializeField] private int _rangeSlotCount = 6;

    [SerializeField] private string _rangeFilled = "■";
    [SerializeField] private string _rangeEmpty = "□";

    [Header("커서 기준 위치")]
    [Tooltip("커서로부터 떨어뜨릴 거리(픽셀). x는 오른쪽, y는 위쪽이 양수.")]
    [SerializeField] private Vector2 _cursorOffset = new(18f, -18f);

    [Tooltip("화면 가장자리에서 최소한 남길 여백(픽셀).")]
    [SerializeField] private float _screenMargin = 8f;

    private readonly List<Image> _itemIcons = new();
    private readonly StringBuilder _rangeBuilder = new();

    private Canvas _canvas;

    /// <summary>지금 이 설명창을 띄운 쪽. 다른 칸의 늦은 Exit이 새로 연 창을 끄는 걸 막는다.</summary>
    private object _owner;

    private void Awake()
    {
        if (_root == null) _root = transform as RectTransform;

        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null) _canvas = _canvas.rootCanvas;

        SampleDeckPool.HideTemplate(_itemIconTemplate);

        // 씬에 켠 채 저장했더라도 시작은 닫힌 상태로 맞춘다.
        if (_root != null) _root.gameObject.SetActive(false);
    }

    /// <summary>커서가 유닛에 들어왔을 때. 표시할 게 없으면 열지 않는다.</summary>
    public void Show(object owner, PokemonData data, IReadOnlyList<ItemData> recommendedItems)
    {
        if (_root == null || data == null)
        {
            Hide(owner);
            return;
        }

        _owner = owner;

        _root.gameObject.SetActive(true);
        Bind(data, recommendedItems);

        // 배치도 위 어떤 칸보다도 앞에 떠야 한다.
        _root.SetAsLastSibling();

        Place(Input.mousePosition);
    }

    /// <summary>커서가 나갔을 때. 이미 다른 칸이 띄운 창은 건드리지 않는다.</summary>
    public void Hide(object owner)
    {
        if (owner != null && _owner != owner) return;

        _owner = null;
        if (_root != null) _root.gameObject.SetActive(false);
    }

    /// <summary>소유자와 무관하게 닫는다. 창을 닫거나 페이지를 옮길 때 쓴다.</summary>
    public void HideAll()
    {
        _owner = null;
        if (_root != null) _root.gameObject.SetActive(false);
    }

    private void Bind(PokemonData data, IReadOnlyList<ItemData> recommendedItems)
    {
        if (_nameText != null) _nameText.text = data.pokemonName;
        if (_costText != null) _costText.text = data.cost.ToString();

        if (_roleText != null)
            _roleText.text = string.IsNullOrWhiteSpace(data.role) ? "-" : data.role;

        for (int i = 0; i < _synergyLines.Length; i++)
            BindSynergy(_synergyLines[i], data, i);

        if (_rangeText != null) _rangeText.text = BuildRangeText(data.attackRange);

        BindItems(recommendedItems);
    }

    /// <summary>
    /// 시너지 줄 하나(아이콘 + 이름). 그 자리에 시너지가 없으면 줄을 꺼서 창 높이를 줄인다.
    ///
    /// 이름은 <see cref="SynergyData.synergyName"/>(한글)을 쓴다 — 포켓몬 데이터에는 영문으로
    /// 들어 있을 수 있는데, 시너지 패널과 표기가 달라지면 같은 시너지인지 알아보기 어렵다.
    /// </summary>
    private static void BindSynergy(SynergyLine line, PokemonData data, int index)
    {
        List<string> synergies = data.synergies;

        bool has =
            synergies != null &&
            index < synergies.Count &&
            !string.IsNullOrWhiteSpace(synergies[index]);

        if (line.root != null) line.root.SetActive(has);
        if (!has) return;

        string raw = synergies[index];
        SynergyData synergy = SampleDeckSynergyLookup.Find(raw);

        if (line.label != null)
            line.label.text = SampleDeckSynergyLookup.DisplayName(synergy, raw);

        if (line.icon != null)
        {
            Sprite sprite = synergy != null ? synergy.icon : null;

            line.icon.sprite = sprite;
            // 아이콘이 아직 없는 시너지는 흰 사각형 대신 빈 칸으로 둔다.
            line.icon.enabled = sprite != null;
        }
    }

    /// <summary>"■■□□□□". 사거리를 숫자로 적지 않는 이유는 칸 수가 곧 보드 거리라 눈에 더 잘 들어와서다.</summary>
    private string BuildRangeText(int attackRange)
    {
        _rangeBuilder.Clear();

        int filled = Mathf.Clamp(attackRange, 0, _rangeSlotCount);

        for (int i = 0; i < _rangeSlotCount; i++)
            _rangeBuilder.Append(i < filled ? _rangeFilled : _rangeEmpty);

        return _rangeBuilder.ToString();
    }

    private void BindItems(IReadOnlyList<ItemData> items)
    {
        int count = 0;

        if (items != null)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null) continue;

                Image icon = SampleDeckPool.Take(_itemIcons, _itemIconTemplate, _itemArea, count);
                if (icon == null) break;

                icon.sprite = items[i].icon;
                icon.enabled = items[i].icon != null;
                count++;
            }
        }

        SampleDeckPool.HideExtras(_itemIcons, count);

        if (_itemGroup != null) _itemGroup.SetActive(count > 0);
    }

    // 열려 있는 동안 커서를 따라간다.
    private void LateUpdate()
    {
        if (_root != null && _root.gameObject.activeSelf) Place(Input.mousePosition);
    }

    /// <summary>
    /// 커서 오른쪽 아래에 붙이되 화면 밖으로 나가면 반대쪽으로 뒤집는다.
    /// <see cref="ItemTooltipController"/>와 같은 계산이라 두 설명창이 같은 자리 규칙으로 움직인다.
    /// </summary>
    private void Place(Vector2 screenPos)
    {
        // ContentSizeFitter 결과를 이번 프레임에 반영한다. 안 하면 방금 켠 프레임에
        // 이전 내용의 크기로 자리를 잡아 창이 한 번 튄다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(_root);

        float scale = _canvas != null ? _canvas.scaleFactor : 1f;
        Vector2 size = _root.rect.size * scale;

        Vector2 topLeft = screenPos + _cursorOffset * scale;
        float margin = _screenMargin * scale;

        if (topLeft.x + size.x > Screen.width - margin)
            topLeft.x = screenPos.x - _cursorOffset.x * scale - size.x;

        if (topLeft.y - size.y < margin)
            topLeft.y = screenPos.y - _cursorOffset.y * scale + size.y;

        topLeft.x = Mathf.Clamp(topLeft.x, margin, Mathf.Max(margin, Screen.width - margin - size.x));
        topLeft.y = Mathf.Clamp(topLeft.y, Mathf.Min(Screen.height - margin, margin + size.y),
                                           Screen.height - margin);

        Vector2 pivot = _root.pivot;
        _root.position = new Vector3(topLeft.x + size.x * pivot.x,
                                     topLeft.y - size.y * (1f - pivot.y),
                                     _root.position.z);
    }
}
