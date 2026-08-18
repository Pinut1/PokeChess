using UnityEngine;

/// <summary>
/// 영웅증강 공통 골격(이브이/파치리스). 선택 시:
///   ① 대상 종 1마리 벤치 즉시지급(무료, 상점 풀 미차감 — 보상 지급과 동일 규칙)
///   ② 무료 리롤 3개 (기획 "전용리롤 3"은 대상 종만 나오는 전용 상점 리롤로 추정 —
///      전용 상점 미구현이라 일반 무료 리롤로 PLACEHOLDER. TODO: 기획 확인 후 교체)
///   ③ 효과 태그 — 아래 두 층으로 나뉜다
///
/// <b>효과는 두 층이다</b> (기획 확정 2026-08-18 "가장 강한 1마리"):
/// <list type="table">
///   <item><term>고정 효과</term><description>보유한 대상 종 <b>전부</b>(보드+벤치). 되돌리지 않는 편도.
///     이브이 진화잠금이 여기 해당하고, 파치리스는 고정 효과가 없다.</description></item>
///   <item><term>이동 효과</term><description><b>보드에서 가장 강한 1마리</b>에만. 대상이 바뀌면
///     이전 개체에서 걷어내고 새 개체에 붙인다.</description></item>
/// </list>
///
/// <b>대상 선정</b>(후보는 보드에 올라간 개체만 — 벤치는 후보가 아니다):
///   ① 성급이 가장 높은 개체 → ② 동률이면 장착 슬롯이 많은 개체(진화의 돌 포함) →
///   ③ 그래도 동률이면 보드에 가장 마지막으로 올린 개체(<see cref="PokemonUnit.boardEntrySeq"/>).
/// 보드에 한 마리도 없으면 대상 없음 — 아무도 이동 효과를 받지 않는다.
///
/// <b>재선정은 실시간</b>이다. 성급 변화(합체·진화)·아이템 장착/해제·벤치↔보드 이동에서 다시 뽑는다.
/// 보드 안에서 자리만 옮기는 것은 대상을 바꾸지 않는다(boardEntrySeq를 갱신하지 않으므로 자동으로 성립).
///
/// 전투 중 동결 장치는 두지 않는다 — 보드 후보 집합과 그 성급·아이템이 전투 중 바뀔 경로가 없다.
/// (보드 유닛 드래그 차단 = UnitDragController, 보드 유닛 아이템 장착 차단 = ItemManager,
///  전투 중 합체는 벤치만으로 3마리가 채워질 때만 즉시 처리 = BoardManager. 벤치는 애초에 후보가 아니다.)
/// </summary>
public abstract class HeroAugment : Augment
{
    private const int HERO_REROLL_COUNT = 3; // PLACEHOLDER — "전용리롤 3" 해석 확정 대기

    /// <summary>대상 종 영문명(PokemonData.pokemonNameEn 기준).</summary>
    protected abstract string SpeciesNameEn { get; }

    /// <summary>이동 효과가 바꿔놓을 역할. 표시용 조회(TryGetRoleOverride)가 쓴다.</summary>
    protected abstract string OverriddenRole { get; }

    /// <summary>
    /// 고정 효과를 1기에 적용한다(보유분 전부 대상, 되돌리지 않음). 고정 효과가 없는 증강은
    /// 오버라이드하지 않으면 된다 — 파치리스가 그렇다.
    /// 이미 적용된 유닛을 다시 받아도 안전해야 한다(중복 호출됨).
    /// </summary>
    protected virtual void ApplyFixed(PokemonUnit unit) { }

    /// <summary>이동 효과를 1기에 적용한다. 대상이 바뀔 때만 호출된다.</summary>
    protected abstract void ApplyMobile(PokemonUnit unit);

    /// <summary>이동 효과를 1기에서 걷어낸다. <see cref="ApplyMobile"/>이 건 것만 정확히 되돌릴 것.</summary>
    protected abstract void RemoveMobile(PokemonUnit unit);

    /// <summary>
    /// 지금 이동 효과를 받고 있는 개체. 없으면 null.
    /// 파괴된 유닛(합체로 소모·판매)은 Unity의 == null 오버로드가 null로 잡아주므로 별도 정리가 필요 없다.
    /// 바닥 VFX 표시와 QA 확인이 이 값을 읽는다.
    /// </summary>
    public PokemonUnit MobileTarget => _mobileTarget != null ? _mobileTarget : null;

    private PokemonUnit _mobileTarget;

    /// <summary>대상 종이면 바뀔 역할을 알려준다 — 상점 카드처럼 아직 유닛이 없는 곳에서 쓴다.</summary>
    public override bool TryGetRoleOverride(PokemonData data, out string role)
    {
        role = null;

        if (data == null ||
            !string.Equals(data.pokemonNameEn, SpeciesNameEn, System.StringComparison.OrdinalIgnoreCase))
            return false;

        role = OverriddenRole;
        return !string.IsNullOrEmpty(role);
    }

    public override void Apply()
    {
        GrantUnitToBench();

        if (GameManager.TryGet(out var gm)) gm.Shop?.AddReroll(HERO_REROLL_COUNT);

        ApplyFixedToAllOwned();
        Reselect();
    }

    /// <summary>재접속 복원 — 1회성 지급(벤치 유닛/리롤)은 재실행하지 않고 효과만 재적용.</summary>
    public override void Restore()
    {
        ApplyFixedToAllOwned();
        Reselect();
    }

    // ── 재선정 트리거 ────────────────────────────────────────────
    // 어느 경로로 들어와도 하는 일은 같다: 고정 효과를 (해당되면) 붙이고, 이동 효과를 다시 뽑는다.

    public override void OnUnitPlaced(PokemonUnit unit)
    {
        ApplyFixedIfTarget(unit);
        Reselect();
    }

    public override void OnUnitBenched(PokemonUnit unit)
    {
        // 벤치도 고정 효과 범위다(이브이 잠금). 이동 효과는 보드를 떠난 순간 Reselect가 걷어낸다.
        ApplyFixedIfTarget(unit);
        Reselect();
    }

    public override void OnUnitSold(PokemonUnit unit)
    {
        // 팔린 유닛은 곧 파괴된다 — RemoveMobile로 되돌릴 필요도, 되돌릴 수도 없으므로
        // 참조만 끊고 다음 대상을 뽑는다.
        if (unit != null && unit == _mobileTarget) _mobileTarget = null;
        Reselect();
    }

    /// <summary>
    /// 아이템 장착/해제(GameEvents.OnInventoryChanged). ItemManager가 "누가 바뀌었는지"를 알려주지
    /// 않아 보드 후보를 전수 재평가한다 — 후보가 몇 마리뿐이라 비용은 무시할 만하다.
    /// </summary>
    public override void OnInventoryChanged() => Reselect();

    /// <summary>
    /// 라운드 진입 시 한 번 더 맞춘다. 위 훅들로 이미 정확하지만, 어떤 경로로든 어긋났을 때
    /// 다음 라운드에 저절로 복구되게 하는 안전망이다(새 이벤트를 만들지 않고 기존 훅을 재사용).
    /// </summary>
    public override void OnRoundChanged(int round) => Reselect();

    // ── 대상 선정 ────────────────────────────────────────────────

    /// <summary>
    /// 보드에서 가장 강한 1마리를 다시 뽑아 이동 효과를 옮긴다. 대상이 그대로면 아무 것도 하지 않는다.
    /// 이 메서드가 이동 효과의 <b>유일한</b> 기록 지점이다 — 합체 승계(BoardManager)에서 이동 효과를
    /// 승계하지 않는 이유가 이것이다(승계와 재선정이 둘 다 쓰면 어느 쪽이 이겼는지 추적 불가).
    /// </summary>
    private void Reselect()
    {
        PokemonUnit next = SelectStrongestOnBoard();

        // Unity의 == 오버로드가 "파괴된 오브젝트 == null"을 참으로 만들어주므로, 파괴된 이전 대상과
        // null인 새 대상을 비교해도 안전하게 같다고 판정된다.
        if (next == _mobileTarget) return;

        if (_mobileTarget != null) RemoveMobile(_mobileTarget);

        _mobileTarget = next;

        if (_mobileTarget != null) ApplyMobile(_mobileTarget);

        Debug.Log($"[Augment] {SpeciesNameEn} 영웅증강 대상 → " +
                  $"{(_mobileTarget != null ? $"{_mobileTarget.data.pokemonName} {_mobileTarget.starLevel}성(장착 슬롯 {ItemCountOf(_mobileTarget)}개)" : "없음")}");
    }

    /// <summary>
    /// 보드 위 대상 종 중 가장 강한 1마리. 후보가 없으면 null.
    /// 성급 → 아이템 갯수 → 보드 진입 순번(늦을수록 우선) 순으로 비교한다.
    /// </summary>
    private PokemonUnit SelectStrongestOnBoard()
    {
        if (!GameManager.TryGet(out var gm) || gm.Board == null) return null;

        PokemonUnit best = null;

        foreach (PokemonUnit unit in gm.Board.GetUnitsOnBoard())
        {
            if (!IsTarget(unit)) continue;
            if (best == null || IsStrongerThan(unit, best)) best = unit;
        }

        return best;
    }

    /// <summary>선정 기준 비교. 동률이면 false(먼저 찾은 쪽 유지)라 안정적인 결과가 나온다.</summary>
    private static bool IsStrongerThan(PokemonUnit candidate, PokemonUnit incumbent)
    {
        if (candidate.starLevel != incumbent.starLevel)
            return candidate.starLevel > incumbent.starLevel;

        int candidateItems = ItemCountOf(candidate);
        int incumbentItems = ItemCountOf(incumbent);
        if (candidateItems != incumbentItems)
            return candidateItems > incumbentItems;

        // 마지막 동률 처리 — 보드에 더 늦게 올라온 쪽.
        return candidate.boardEntrySeq > incumbent.boardEntrySeq;
    }

    /// <summary>
    /// 선정 기준이 되는 "아이템 갯수" — <b>진화의 돌을 포함한 장착 슬롯 전부</b>(PokemonUnit.UsedSlots).
    ///
    /// 지금은 영웅증강 대상 종이 진화를 안 하거나 잠겨 있어 돌을 낀 개체가 나오지 않지만, 나중에
    /// 영웅증강 대상이 돌로 진화하는 경로가 생기면 돌을 빼고 세는 순간 "아이템을 3개 낀 개체가
    /// 2개짜리에게 밀리는" 식으로 조용히 어긋난다(기획 확인 2026-08-19). 그때 가서 찾기 어려운
    /// 버그라 처음부터 슬롯 전부를 센다.
    ///
    /// "아이템을 몰아줘 대상을 지정한다"(기획 §6)와도 충돌하지 않는다 — 돌을 낀 유닛은 성급 진화를
    /// 하려면 돌을 뺐다가 다시 껴야 해서, 플레이어가 슬롯을 의도적으로 조작하는 흐름 자체가 같다.
    /// </summary>
    private static int ItemCountOf(PokemonUnit unit)
        => unit != null ? unit.UsedSlots : 0;

    // ── 고정 효과 ────────────────────────────────────────────────

    private void ApplyFixedIfTarget(PokemonUnit unit)
    {
        if (IsTarget(unit)) ApplyFixed(unit);
    }

    private bool IsTarget(PokemonUnit unit)
        => unit != null && unit.data != null &&
           string.Equals(unit.data.pokemonNameEn, SpeciesNameEn, System.StringComparison.OrdinalIgnoreCase);

    /// <summary>보유한 대상 종 전부(보드+벤치)에 고정 효과를 건다.</summary>
    private void ApplyFixedToAllOwned()
    {
        if (!GameManager.TryGet(out var gm) || gm.Board == null) return;

        int tagged = 0;

        foreach (PokemonUnit unit in gm.Board.GetUnitsOnBoard())
            if (IsTarget(unit)) { ApplyFixed(unit); tagged++; }

        foreach (PokemonUnit unit in gm.Board.GetUnitsInBench())
            if (IsTarget(unit)) { ApplyFixed(unit); tagged++; }

        Debug.Log($"[Augment] {SpeciesNameEn} 영웅증강 — 보유분 {tagged}마리에 고정 효과");
    }

    /// <summary>대상 종 1마리를 벤치에 무료 지급(보상 지급과 동일 경로 — 골드/풀 미차감).</summary>
    private void GrantUnitToBench()
    {
        var db = PokemonDatabase.Instance;
        PokemonData data = db != null ? db.GetByNameEn(SpeciesNameEn) : null;
        if (data == null)
        {
            Debug.LogWarning($"[Augment] '{SpeciesNameEn}' PokemonDatabase에 없음 — 즉시지급 실패");
            return;
        }

        if (!GameManager.TryGet(out var gm)) return;

        var board = gm.Board;
        if (board == null || !board.HasBenchSpace())
        {
            Debug.LogWarning($"[Augment] 벤치 가득 — {SpeciesNameEn} 즉시지급 유실");
            return;
        }

        PokemonUnit unit = UnitFactory.Create(data);
        if (unit == null) return;

        if (!board.TryPlaceInBench(unit))
        {
            Object.Destroy(unit.gameObject); // 방어적 정리(HasBenchSpace 통과 후 실패는 이론상 도달 안 함)
            return;
        }

        // 벤치로 들어가므로 고정 효과만. OnUnitBenched 훅으로도 걸리지만 Apply() 중에는 아직 활성
        // 증강 목록에 편입되기 전이라 직접 보장한다.
        ApplyFixed(unit);
        Debug.Log($"[Augment] {SpeciesNameEn} 1마리 벤치 즉시지급");
    }
}
