/// <summary>
/// 모든 증강의 추상 베이스.
/// 새 증강 추가 시: AugmentId enum → 이 클래스 상속 → AugmentFactory 등록.
/// 훅은 AugmentManager가 GameEvents를 구독해 활성 증강 전체에 전파한다.
/// </summary>
public abstract class Augment
{
    public AugmentData Data { get; private set; }

    public void Initialize(AugmentData data) => Data = data;

    /// <summary>증강 선택 시 1회 호출</summary>
    public virtual void Apply() { }

    /// <summary>증강 제거 시 1회 호출 (정리 용도)</summary>
    public virtual void Remove() { }

    /// <summary>
    /// 재접속 복원 전용 진입점. 신규 선택(Apply)과 달리 1회성 지급(무료 유닛/골드/리롤 등)은
    /// 재실행하지 않고, 씬 재로드로 유실되는 지속 효과(스탯 태그, 이자율 가산 등)만 재적용한다.
    /// 기본값은 아무 것도 하지 않음(안전한 기본) — 지속 상태를 가진 증강만 오버라이드해서 사용.
    /// </summary>
    public virtual void Restore() { }

    // ── 라이프사이클 훅 — 필요한 것만 오버라이드 ──────────────────

    public virtual void OnRoundChanged(int round) { }
    public virtual void OnBattleStart()           { }
    public virtual void OnBattleEnd(BattleEndReason reason) { }
    public virtual void OnUnitPlaced(PokemonUnit unit)  { }
    public virtual void OnUnitBenched(PokemonUnit unit) { }
    public virtual void OnUnitSold(PokemonUnit unit)    { }

    /// <summary>수동 리롤 1회가 실제로 소모됨(무료/골드 무관) — 리롤 환급 증강용</summary>
    public virtual void OnRerollSpent() { }

    /// <summary>
    /// 인벤토리/장착 상태가 바뀜(GameEvents.OnInventoryChanged).
    /// ItemManager는 "누가 바뀌었는지"를 알려주지 않으므로(유닛 단위 이벤트 없음) 이 훅을 받은 쪽이
    /// 필요한 범위를 스스로 전수 재평가한다. 영웅증강의 "가장 강한 1마리" 재선정이 첫 사용처다 —
    /// 아이템 갯수가 선정 기준이라 장착/해제 때마다 대상이 바뀔 수 있다.
    /// </summary>
    public virtual void OnInventoryChanged() { }

    // ── 표시용 조회 ────────────────────────────────────────────

    /// <summary>
    /// 이 증강이 해당 <b>종</b>의 역할을 바꾸는지. 아직 사지 않아 PokemonUnit이 없는
    /// 상점 카드에서도 "사면 무엇이 되는지"를 보여주기 위한 조회용이다.
    /// (이미 산 유닛은 PokemonUnit.Role이 roleOverride를 반영하므로 이 훅이 필요 없다.)
    /// 기본값은 "바꾸지 않음".
    /// </summary>
    public virtual bool TryGetRoleOverride(PokemonData data, out string role)
    {
        role = null;
        return false;
    }
}
