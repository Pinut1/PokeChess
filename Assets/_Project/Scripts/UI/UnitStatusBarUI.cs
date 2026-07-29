using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 유닛 한 기의 머리 위에 붙는 HP/마나 바(롤토체스식 2단). 값 계산은 하지 않고 받은 비율만 그린다.
/// 화면 좌표로 배치되는 스크린 스페이스 UI라 카메라를 향해 돌릴 필요가 없고, 거리와 무관하게 크기가 일정하다.
/// 풀링해서 재사용하므로 생성/파괴는 UnitStatusBarHud가 관리한다.
/// </summary>
public class UnitStatusBarUI : MonoBehaviour
{
    [Header("채우기")]
    [Tooltip("Image Type을 Filled(Horizontal)로 두면 fillAmount로 줄어든다.")]
    [SerializeField] private Image _hpFill;
    [Tooltip("마나가 없는 유닛(스킬 미보유)이면 자동으로 숨긴다.")]
    [SerializeField] private Image _manaFill;
    [SerializeField] private GameObject _manaRoot;

    [Header("색상")]
    [SerializeField] private Color _allyHpColor  = new(0.35f, 0.85f, 0.35f);
    [SerializeField] private Color _enemyHpColor = new(0.9f, 0.3f, 0.3f);
    [Tooltip("마나는 아군/적 구분 없이 한 색을 쓴다. 스프라이트가 흰색이어야 이 색이 그대로 나온다.")]
    [SerializeField] private Color _manaColor    = new(0.3f, 0.6f, 1f);

    [Header("눈금 (롤 체력바식 구분선)")]
    [Tooltip("눈금 무늬를 반복해서 그리는 오버레이. 텍스처 Wrap Mode를 Repeat로 두어야 한다. " +
             "비워두면 눈금 없이 동작한다.")]
    [SerializeField] private RawImage _tickOverlay;
    [Tooltip("눈금 하나가 나타내는 최대 체력. 바 폭은 고정이라 체력이 많을수록 눈금이 촘촘해진다. " +
             "이 프로젝트 HP 범위(1성 300~1500, 3성 최대 약 4200) 기준 100~150이 적당하다.")]
    [SerializeField] private float _hpPerTick = 150f;
    [Tooltip("눈금이 과하게 촘촘해지는 것을 막는 상한. 0이면 제한 없음.")]
    [SerializeField] private int _maxTicks = 30;

    [Tooltip("마나바 눈금 오버레이. 마나는 종별 편차가 작아(40~100) 개수를 고정한다.")]
    [SerializeField] private RawImage _manaTickOverlay;
    [Tooltip("마나바 눈금 개수(고정). 0이면 눈금 없음.")]
    [SerializeField] private int _manaTicks = 4;

    [Header("장착 아이템 (HP바 아래)")]
    [Tooltip("아이템 슬롯 묶음(Unit_ItemSlot_pf). 아이템이 하나도 없으면 통째로 꺼진다.")]
    [SerializeField] private GameObject _itemSlotsRoot;
    [Tooltip("슬롯 테두리. 장착 개수만큼만 켜진다. 인덱스 순서 = 왼쪽부터.")]
    [SerializeField] private GameObject[] _itemSlots;
    [Tooltip("슬롯 안의 아이콘. _itemSlots와 같은 순서로 물려야 한다.")]
    [SerializeField] private Image[] _itemIcons;

    /// <summary>이번 갱신에서 아이템이 하나라도 표시됐는지. Hud가 바를 위로 올릴지 판단할 때 읽는다.</summary>
    public bool HasVisibleItems { get; private set; }

    private RectTransform _rect;
    private float _appliedHpTicks = -1f;   // 마지막으로 uvRect에 반영한 눈금 개수

    public RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

    /// <summary>
    /// 표시값 갱신. 비율은 0~1로 클램프한다.
    /// manaRatio가 음수면 마나 바를 숨긴다(스킬이 없거나 마나 개념이 없는 상태).
    /// maxHp는 눈금 개수 계산에만 쓴다.
    /// </summary>
    public void SetValues(float hpRatio, float manaRatio, bool isAlly, float maxHp)
    {
        if (_hpFill != null)
        {
            _hpFill.fillAmount = Mathf.Clamp01(hpRatio);
            _hpFill.color = isAlly ? _allyHpColor : _enemyHpColor;
        }

        bool showMana = manaRatio >= 0f;
        if (_manaRoot != null) _manaRoot.SetActive(showMana);

        if (showMana && _manaFill != null)
        {
            _manaFill.fillAmount = Mathf.Clamp01(manaRatio);
            // Graphic.color 세터가 값이 같으면 무시하므로 매 프레임 대입해도 비용이 없다.
            // 대신 Play 중 인스펙터에서 색을 바꾸면 바로 반영된다(색 고를 때 편함).
            _manaFill.color = _manaColor;
        }

        ApplyTicks(maxHp);
    }

    /// <summary>
    /// 장착 아이템 아이콘 갱신. 슬롯 수(2)를 넘는 아이템은 표시하지 않는다.
    /// 진화의 돌은 아이템과 슬롯을 공유하므로(PokemonUnit.UsedSlots) 함께 넘겨 앞쪽에 표시한다.
    /// 하나도 없으면 묶음을 통째로 꺼서 HP바만 남긴다.
    /// </summary>
    public void SetItems(IReadOnlyList<ItemData> items, EvolutionStoneData stone)
    {
        int shown = 0;
        int capacity = _itemSlots != null ? _itemSlots.Length : 0;

        if (stone != null && shown < capacity)
            ApplySlot(shown++, stone.icon);

        if (items != null)
        {
            for (int i = 0; i < items.Count && shown < capacity; i++)
            {
                if (items[i] == null) continue;
                ApplySlot(shown++, items[i].icon);
            }
        }

        for (int i = shown; i < capacity; i++)
            if (_itemSlots[i] != null) _itemSlots[i].SetActive(false);

        HasVisibleItems = shown > 0;
        if (_itemSlotsRoot != null) _itemSlotsRoot.SetActive(HasVisibleItems);
    }

    private void ApplySlot(int index, Sprite icon)
    {
        if (_itemSlots[index] != null) _itemSlots[index].SetActive(true);

        if (_itemIcons != null && index < _itemIcons.Length && _itemIcons[index] != null)
            _itemIcons[index].sprite = icon;
    }

    /// <summary>
    /// 마나 눈금은 고정 개수라 값이 바뀌어도 다시 계산할 필요가 없다.
    /// 매 프레임 uvRect를 대입하면 불필요한 머티리얼 갱신이 생기므로 시작할 때 한 번만 설정한다.
    /// </summary>
    private void Awake()
    {
        if (_manaTickOverlay == null) return;

        if (_manaTicks <= 0)
        {
            _manaTickOverlay.enabled = false;
            return;
        }

        _manaTickOverlay.uvRect = new Rect(0f, 0f, _manaTicks, 1f);
    }

    /// <summary>
    /// 눈금 개수 = maxHp / _hpPerTick. 바 폭이 고정이므로 텍스처를 그 횟수만큼 반복시켜
    /// 균등한 간격의 구분선을 만든다. 소수점이 남으면 마지막 칸이 잘려 보이는데,
    /// 이는 "눈금 하나 = 일정 체력"이라는 규칙상 올바른 표현이다.
    /// </summary>
    private void ApplyTicks(float maxHp)
    {
        if (_tickOverlay == null) return;

        if (_hpPerTick <= 0f || maxHp <= 0f)
        {
            _tickOverlay.enabled = false;
            return;
        }

        float ticks = maxHp / _hpPerTick;
        if (_maxTicks > 0) ticks = Mathf.Min(ticks, _maxTicks);

        _tickOverlay.enabled = true;

        // 최대 체력은 전투 중 거의 변하지 않는다. 매 프레임 uvRect를 대입하면
        // 값이 같아도 머티리얼이 갱신되므로, 달라졌을 때만 쓴다.
        if (!Mathf.Approximately(_appliedHpTicks, ticks))
        {
            _appliedHpTicks = ticks;
            _tickOverlay.uvRect = new Rect(0f, 0f, ticks, 1f);
        }
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }
}
