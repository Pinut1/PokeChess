using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유닛 머리 위 HP/마나 바를 모아서 관리한다. 바는 풀에서 재사용하고 매 프레임 위치·값만 갱신한다.
///
/// 데이터 출처가 단계마다 다르다.
///   전투 중  : BattleManager.Units (BattleUnit) — 실제로 HP가 깎이는 쪽. 시각화는 bu.visual(전투 전용 인스턴스)
///   그 외    : BoardManager.GetUnitsOnBoard() (PokemonUnit) — 보드에 배치된 원본
/// 두 경로 모두 기존 공개 API만 쓰므로 BattleManager/BoardManager를 수정하지 않는다.
///
/// HP 변경 이벤트가 없어(BattleManager가 currentHp를 여러 곳에서 직접 감소) 매 프레임 읽는 방식을 택했다.
/// 유닛 수가 수십 기 규모라 비용이 무시할 만하고, 전투 코드에 이벤트를 심지 않아도 된다.
/// </summary>
public class UnitStatusBarHud : MonoBehaviour
{
    [Header("프리팹 / 부모")]
    [SerializeField] private UnitStatusBarUI _barPrefab;
    [Tooltip("바가 담길 부모. 보통 최상위 Canvas 아래의 빈 오브젝트.")]
    [SerializeField] private RectTransform _barRoot;

    [Header("배치")]
    [Tooltip("비우면 Camera.main을 쓴다.")]
    [SerializeField] private Camera _camera;
    [Tooltip("유닛 발밑 기준 월드 높이(머리 위로 띄우는 정도).")]
    [SerializeField] private float _heightOffset = 2f;

    [Tooltip("벤치 유닛에도 바를 표시할지. 전투 중에는 벤치가 참전하지 않아 이 값과 무관하게 표시되지 않는다.")]
    [SerializeField] private bool _includeBench = true;

    [Tooltip("장착 아이템이 있을 때 바 전체를 위로 올리는 양(화면 픽셀). " +
             "아이템 줄이 바 아래에 붙으므로 그만큼 올려야 유닛을 가리지 않는다. " +
             "슬롯이 가로 배치라 아이템 1개든 2개든 이동량은 같다.")]
    [SerializeField] private float _itemLiftPixels = 20f;

    private readonly List<UnitStatusBarUI> _pool = new();

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;
    }

    private void LateUpdate()
    {
        if (_barPrefab == null || _barRoot == null || _camera == null) return;
        if (!GameManager.TryGet(out var gm)) { HideFrom(0); return; }

        int used = gm.Phase != null && gm.Phase.CurrentPhase == GamePhase.Battle
            ? DrawBattleBars(gm)
            : DrawBoardBars(gm);

        HideFrom(used);
    }

    /// <summary>전투 중 — 살아있는 BattleUnit 전부(아군/적). HP가 실제로 깎이는 값이 여기 있다.</summary>
    private int DrawBattleBars(GameManager gm)
    {
        var battle = gm.Battle;
        if (battle == null || battle.Units == null) return 0;

        int used = 0;
        foreach (var bu in battle.Units)
        {
            if (bu == null || !bu.IsAlive || bu.visual == null) continue;

            float mana = bu.HasSkill && bu.maxMana > 0f ? bu.currentMana / bu.maxMana : -1f;
            float hp = bu.maxHp > 0f ? bu.currentHp / bu.maxHp : 0f;

            // 전투 중에는 원본 PokemonUnit이 아니라 스냅샷을 그리므로 displayItems/displayStone을 쓴다(적도 동일).
            if (Place(used, bu.visual.transform.position, hp, mana, bu.team == BattleTeam.Ally, bu.maxHp,
                      bu.displayItems, bu.displayStone))
                used++;
        }

        return used;
    }

    /// <summary>전투 외 — 보드 유닛과 벤치 유닛. 벤치도 배치 전에 상태를 비교할 수 있어야 한다.</summary>
    private int DrawBoardBars(GameManager gm)
    {
        var board = gm.Board;
        if (board == null) return 0;

        int used = DrawUnitList(board.GetUnitsOnBoard(), 0);

        if (_includeBench)
            used = DrawUnitList(board.GetUnitsInBench(), used);

        return used;
    }

    /// <summary>PokemonUnit 목록을 startIndex부터 이어서 그린다. 반환값은 다음에 쓸 인덱스.</summary>
    private int DrawUnitList(IReadOnlyList<PokemonUnit> units, int startIndex)
    {
        if (units == null) return startIndex;

        int used = startIndex;
        foreach (var unit in units)
        {
            if (unit == null || unit.data == null) continue;

            float maxHp = unit.MaxHp;
            float hp = maxHp > 0f ? unit.currentHp / maxHp : 0f;

            float maxMana = unit.data.manaCost;
            float mana = maxMana > 0f ? unit.currentMana / maxMana : -1f;

            if (Place(used, unit.transform.position, hp, mana, true, maxHp,
                      unit.items, unit.equippedStone))
                used++;
        }

        return used;
    }

    /// <summary>index번째 바를 월드 위치 위에 배치. 카메라 뒤면 건너뛴다(false 반환).</summary>
    private bool Place(int index, Vector3 worldPos, float hpRatio, float manaRatio, bool isAlly, float maxHp,
                       IReadOnlyList<ItemData> items, EvolutionStoneData stone)
    {
        Vector3 screenPos = _camera.WorldToScreenPoint(worldPos + Vector3.up * _heightOffset);
        if (screenPos.z <= 0f) return false; // 카메라 뒤 — 화면 반대편에 그려지는 것을 막는다

        var bar = GetBar(index);
        bar.SetVisible(true);

        // 아이템 갱신이 먼저다 — 아이템이 있으면 바를 그만큼 올려야 하는데,
        // 그 판단(HasVisibleItems)이 SetItems 안에서 정해지기 때문.
        bar.SetItems(items, stone);
        if (bar.HasVisibleItems) screenPos.y += _itemLiftPixels;

        // 정수 픽셀로 스냅. 소수점 좌표로 움직이면 눈금처럼 얇은 선이 프레임마다 다른 텍셀에 걸려
        // 두꺼워졌다 사라졌다 하며 흔들린다(유닛을 드래그할 때 특히 눈에 띈다).
        screenPos.x = Mathf.Round(screenPos.x);
        screenPos.y = Mathf.Round(screenPos.y);

        bar.Rect.position = screenPos;
        bar.SetValues(hpRatio, manaRatio, isAlly, maxHp);
        return true;
    }

    private UnitStatusBarUI GetBar(int index)
    {
        while (_pool.Count <= index)
            _pool.Add(Instantiate(_barPrefab, _barRoot));

        return _pool[index];
    }

    /// <summary>이번 프레임에 쓰지 않은 바는 숨긴다(파괴하지 않고 다음 프레임에 재사용).</summary>
    private void HideFrom(int startIndex)
    {
        for (int i = startIndex; i < _pool.Count; i++)
            _pool[i].SetVisible(false);
    }
}
