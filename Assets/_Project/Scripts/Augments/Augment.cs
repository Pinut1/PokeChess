/// <summary>
/// 모든 증강의 추상 베이스.
/// 새 증강 추가 시: AugmentId enum → 이 클래스 상속 → AugmentFactory 등록.
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
    public virtual void OnBattleEnd(bool isWin)   { }
    public virtual void OnUnitPlaced(PokemonUnit unit) { }
    public virtual void OnUnitSold(PokemonUnit unit)   { }
}
