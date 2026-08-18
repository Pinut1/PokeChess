using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    [Tooltip("파트너 관전 전체화면이 떠 있는 동안 이 HUD를 숨기기 위한 참조. 비워두면(기본) 기존과 " +
             "동일하게 항상 표시된다 — 관전 기능이 없는 씬에서도 이 HUD가 정상 동작해야 하므로 " +
             "연결을 강제하지 않는다. PIP(축소) 상태는 여기 포함하지 않는다 — 전체화면일 때만 숨긴다.")]
    [SerializeField] private PartnerSpectateView _partnerSpectateView;

    private readonly List<UnitStatusBarUI> _pool = new();

    // 파트너 관전(전투 중 미러 BattleUnit / 쇼핑 중 파트너 보드 공용) 유닛 바 전용 풀 — _pool과
    // 완전히 분리해서 실전투 바와 서로 간섭하지 않는다. 부모도 다르다(PartnerSpectateView.PipRawImage
    // 자식) — DrawPartnerBars 참고.
    private readonly List<UnitStatusBarUI> _mirrorPool = new();

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;
    }

    private void LateUpdate()
    {
        if (_barPrefab != null) DrawLocalBars();

        // 파트너 관전 바는 실전투 바와 독립적으로 매 프레임 갱신한다 — PIP 상태에서도 그려야 하므로
        // 위 IsExpanded 분기(전체화면 전용)에 걸리지 않게 별도 메서드로 뺐다.
        DrawPartnerBars();
    }

    /// <summary>내 로컬 유닛(실전투 또는 상점/보드 단계) 바를 그린다. 기존 LateUpdate 로직 그대로,
    /// 미러 바 추가를 위해 이름만 붙여 분리했다.</summary>
    private void DrawLocalBars()
    {
        if (_barRoot == null || _camera == null) return;

        // 파트너 전체화면 관전 중에는 내 로컬 유닛 상태바를 그리지 않는다 — 관전 카메라와 무관하게
        // Camera.main 기준으로 그려지므로, 그대로 두면 파트너 화면 위에 내 유닛 바가 겹쳐 보인다.
        if (_partnerSpectateView != null && _partnerSpectateView.IsExpanded) { HideFrom(0); return; }

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

            float maxMana = unit.EffectiveManaCost;
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

    // ─────────────────────────────────────────
    // 파트너 관전 바 — 전투 중(미러 BattleUnit) / 쇼핑 중(파트너 보드) 공용
    //
    // 데이터 출처가 단계마다 다르다(로컬 바가 전투/그 외로 갈리는 것과 같은 이유).
    //   전투 중 : PartnerBattleMirrorController.MirrorUnits(BattleManager.Units 그대로) — 미러
    //             BattleUnit 값만 읽는다.
    //   그 외    : PartnerBattleMirrorController.BoardView.ActiveUnitViews(OpponentBoardView가
    //             BoardSnapshot으로 만든 미러 비주얼 정보) — 쇼핑 단계는 BattleUnit이 아예 없으므로
    //             이 목록을 대신 읽는다.
    // 두 경로 모두 실제 PokemonUnit/Board는 전혀 건드리지 않는다.
    //
    // 실전투 바(WorldToScreenPoint, Camera.main 기준 Overlay 배치)와 좌표계가 다르다 — 파트너 유닛은
    // 실전투와 같은 3D 씬에 있지만 PartnerSpectateVisual Layer로만 관전 카메라에 보이므로,
    // Camera.main 기준으로 투영하면 좌표는 나오지만 화면엔 (Layer가 가려서) 안 보이는 값이 나온다.
    // 그래서 관전 카메라(SpectatorCamera)의 WorldToViewportPoint로 뽑은 0~1 비율을 관전 화면을
    // 실제로 그리는 PipRawImage의 사각형에 매핑한다 — RawImage가 PIP든 전체화면이든 그 사각형
    // 기준으로 좌표가 나오므로 별도 분기 없이 두 상태 모두에서 맞는 위치가 나온다.
    //
    // Layer 분리는 이 바에는 적용하지 않는다(적용해도 효과가 없다) — Screen Space Overlay UI는
    // 카메라 CullingMask의 대상이 아니라서 GameObject.layer를 바꿔도 렌더링에 영향이 없다.
    // 대신 바를 PipRawImage의 자식으로 두어 "관전 화면이 꺼져 있으면(비활성) 자식도 함께 안 보인다"
    // 는 계층 활성 상태로 같은 효과(내 화면에 안 새고, 관전 화면 밖에서는 안 보임)를 낸다 —
    // VFX/유닛 모델(LocalGameplayVisual/PartnerSpectateVisual)과 목적은 같고 수단만 이 UI 구조에 맞춘 것.
    //
    // 전투 중/쇼핑 중 두 갈래가 같은 풀(_mirrorPool)을 공유한다. 매 프레임 둘 중 하나만 실행해
    // 그 결과(used)만으로 HideMirrorFrom을 한 번 호출한다 — 둘 다 매번 독립적으로 그리고 따로
    // HideMirrorFrom을 부르면, 내용이 없는 쪽이 나중에 실행되며 방금 그린 바를 지워버리는
    // 경합이 생긴다(예: 쇼핑 중엔 전투 갈래의 결과가 0이라 그 갈래가 나중에 돌면 전부 숨어버림).
    // ─────────────────────────────────────────

    /// <summary>
    /// 파트너 관전(PIP/전체화면) 바 그리기 진입점. 지금 파트너 미러 전투가 실제로 실행 중인지에
    /// 따라 한쪽만 그리고, 그 결과 하나로만 풀을 정리한다(위 클래스 주석 참고). 관전 화면이 지금
    /// 아무것도 보여주고 있지 않으면 전부 숨긴다.
    ///
    /// 전투/쇼핑 분기는 내 로컬 GamePhase가 아니라 mirror.IsRunning(미러 BattleManager 자신의
    /// 실행 상태)으로 판단한다 — 내 전투가 먼저 끝나 Result로 넘어가도 파트너 미러 전투는 계속
    /// 실행 중일 수 있는데(RoundPhaseManager가 양쪽 결과가 모일 때까지 다음 라운드로 넘어가지
    /// 않음), 내 GamePhase를 기준으로 삼으면 그 사이 미러가 도는데도 BoardSnapshot 정적 프리뷰로
    /// 잘못 전환돼 HP/MP 바가 사라지는 문제가 있었다(2026-08 확인).
    /// </summary>
    private void DrawPartnerBars()
    {
        if (_barPrefab == null || _partnerSpectateView == null) { HideMirrorFrom(0); return; }
        if (!_partnerSpectateView.IsShowingContent) { HideMirrorFrom(0); return; }

        RawImage pipImage = _partnerSpectateView.PipRawImage;
        PartnerBattleMirrorController mirror = _partnerSpectateView.MirrorController;

        if (pipImage == null || mirror == null) { HideMirrorFrom(0); return; }

        int used = mirror.IsRunning
            ? DrawPartnerBattleBars(mirror, pipImage)
            : DrawPartnerShopBars(mirror, pipImage);

        HideMirrorFrom(used);
    }

    /// <summary>
    /// 전투 중 — 미러 BattleUnit마다 HP/마나 바. 미러가 끝나 MirrorUnits가 비면(Abort/교체/정상
    /// 종료 전부 BattleManager.Cleanup에서 _units를 비움) 별도 이벤트 없이 다음 프레임에 0을
    /// 반환해 자동으로 사라진다.
    /// </summary>
    private int DrawPartnerBattleBars(PartnerBattleMirrorController mirror, RawImage pipImage)
    {
        IReadOnlyList<BattleUnit> units = mirror.MirrorUnits;
        if (units == null) return 0;

        int used = 0;
        foreach (var bu in units)
        {
            if (bu == null || !bu.IsAlive || bu.visual == null) continue;

            float mana = bu.HasSkill && bu.maxMana > 0f ? bu.currentMana / bu.maxMana : -1f;
            float hp = bu.maxHp > 0f ? bu.currentHp / bu.maxHp : 0f;

            if (PlaceMirror(used, pipImage, bu.visual.transform.position, hp, mana,
                            bu.team == BattleTeam.Ally, bu.maxHp, bu.displayItems, bu.displayStone))
                used++;
        }
        return used;
    }

    /// <summary>
    /// 쇼핑 중 — 파트너 보드 유닛마다 HP/마나 바. 쇼핑 단계는 BattleUnit이 없어(전투가 시작돼야
    /// 만들어짐) OpponentBoardView가 BoardSnapshot으로 만든 미러 비주얼(ActiveUnitViews)을 대신 읽는다.
    ///
    /// HP는 항상 가득 참(1f), 마나는 항상 빔(스킬 보유 시 0, 없으면 -1로 숨김)으로 그린다 — 이건
    /// 추측이 아니라 PokemonUnit.ResetForBattle()의 계약("전투 시작/라운드 진입 시 HP를 가득 채우고
    /// 마나를 비움 — TFT 표준: 매 라운드 풀회복")을 그대로 반영한 것이다. BattleManager.Cleanup()도
    /// 전투 종료 시 같은 메서드로 되돌리므로, 쇼핑 단계에 관찰되는 어떤 PokemonUnit도 이 상태를
    /// 벗어나지 않는다(내 로컬 유닛의 쇼핑 단계 바도 UnitStatusBarHud.DrawUnitList에서 unit.currentHp/
    /// MaxHp·unit.currentMana를 그대로 읽을 뿐이고, 그 값이 이 규칙 때문에 항상 가득/빔으로 나온다 —
    /// 여기서는 그 결과를 상수로 대신할 뿐 계산 기준 자체는 로컬과 동일하다). 마나 바 표시 여부는
    /// PartnerBoardUnitView.effectiveManaCost(PokemonUnit.EffectiveManaCost가 BoardSnapshot을 타고
    /// 그대로 전달된 값)로 판정한다 — 종의 원본 manaCost가 아니라 이 값을 써야 영웅증강으로 스킬을
    /// 주입받은 유닛(원본 종 manaCost=0)도 정확히 마나바가 뜬다(2026-08 코드리뷰 대응).
    ///
    /// maxHp는 눈금(ApplyTicks) 계산에만 쓰이는데, 여기서는 phantom PokemonUnit을 만들어 정확히
    /// 재현하지 않고(StatInfoPanelUI.Bind와 달리) 0을 넘겨 눈금만 끈다(HP 채움 비율 자체는 영향
    /// 없음, UnitStatusBarUI.ApplyTicks 참고).
    /// </summary>
    private int DrawPartnerShopBars(PartnerBattleMirrorController mirror, RawImage pipImage)
    {
        OpponentBoardView boardView = mirror.BoardView;
        if (boardView == null) return 0;

        int used = 0;
        foreach (var view in boardView.ActiveUnitViews)
        {
            if (view.visual == null) continue;

            float mana = view.effectiveManaCost > 0 ? 0f : -1f;

            if (PlaceMirror(used, pipImage, view.visual.position, 1f, mana,
                            true, 0f, view.items, null))
                used++;
        }
        return used;
    }

    /// <summary>
    /// index번째 미러 바를 관전 카메라 뷰포트 → PipRawImage 로컬 좌표로 변환해 배치한다.
    /// Place()와 값 갱신 로직(SetItems/아이템 리프트/SetValues)은 동일하게 따르되, 위치 계산과
    /// 배치 대상(부모/좌표계)만 다르다. 좌표 변환 자체는 PartnerSpectateView.TryProjectWorldToScreen
    /// 하나로 통일돼 있다(AugmentInfoTrigger/StatInfoController와 공용) — PipRawImage 사각형 매핑은
    /// 그쪽이 실제로 화면에 그려주는 자리를 그대로 쓴다는 뜻과 같다.
    /// </summary>
    private bool PlaceMirror(int index, RawImage pipImage, Vector3 worldPos,
                             float hpRatio, float manaRatio, bool isAlly, float maxHp,
                             IReadOnlyList<ItemData> items, EvolutionStoneData stone)
    {
        if (!_partnerSpectateView.TryProjectWorldToScreen(worldPos + Vector3.up * _heightOffset, out Vector2 projected))
            return false; // 관전 카메라 뒤 — 그리지 않는다

        Vector3 screenPos = new Vector3(projected.x, projected.y, 0f);

        var bar = GetMirrorBar(index, pipImage.transform);
        bar.SetVisible(true);

        bar.SetItems(items, stone);
        if (bar.HasVisibleItems) screenPos.y += _itemLiftPixels;

        screenPos.x = Mathf.Round(screenPos.x);
        screenPos.y = Mathf.Round(screenPos.y);

        bar.Rect.position = screenPos;
        bar.SetValues(hpRatio, manaRatio, isAlly, maxHp);
        return true;
    }

    private UnitStatusBarUI GetMirrorBar(int index, Transform parent)
    {
        while (_mirrorPool.Count <= index)
            _mirrorPool.Add(Instantiate(_barPrefab, parent));

        return _mirrorPool[index];
    }

    /// <summary>이번 프레임에 쓰지 않은 미러 바는 숨긴다(실전투 바 풀과 완전히 별개).</summary>
    private void HideMirrorFrom(int startIndex)
    {
        for (int i = startIndex; i < _mirrorPool.Count; i++)
            _mirrorPool[i].SetVisible(false);
    }
}
