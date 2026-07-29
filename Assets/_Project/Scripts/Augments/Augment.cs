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

    // ── 라이프사이클 훅 — 필요한 것만 오버라이드 ──────────────────

    public virtual void OnRoundChanged(int round) { }
    public virtual void OnBattleStart()           { }
    public virtual void OnBattleEnd(BattleEndReason reason) { }
    public virtual void OnUnitPlaced(PokemonUnit unit)  { }
    public virtual void OnUnitBenched(PokemonUnit unit) { }
    public virtual void OnUnitSold(PokemonUnit unit)    { }

    /// <summary>수동 리롤 1회가 실제로 소모됨(무료/골드 무관) — 리롤 환급 증강용</summary>
    public virtual void OnRerollSpent() { }
}
