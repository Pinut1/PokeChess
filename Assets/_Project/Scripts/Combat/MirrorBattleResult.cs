/// <summary>
/// 미러 전투(파트너 BattleSnapshot 기반) 종료 결과. 실제 전투의 GameEvents.BattleEnd를 대체하는
/// 로컬 콜백 전용 데이터 — 팀 HP/보상/라운드 등 실제 게임 상태에는 전혀 관여하지 않는다.
///
/// survivorCount/remainingHpSum은 스냅샷 원본이 파트너 진영이므로 "아군(Ally)" 팀 기준이다
/// (미러 전투에서 파트너의 유닛은 BattleTeam.Ally로 생성된다). 추후 체크섬 비교의 최소 재료로도
/// 쓸 수 있도록 남겨두되, 이번 단계에서는 체크섬 비교 자체는 하지 않는다.
/// </summary>
public readonly struct MirrorBattleResult
{
    public readonly BattleEndReason outcome;
    public readonly int elapsedTicks;
    public readonly int survivorCount;
    public readonly float remainingHpSum;

    public MirrorBattleResult(BattleEndReason outcome, int elapsedTicks, int survivorCount, float remainingHpSum)
    {
        this.outcome = outcome;
        this.elapsedTicks = elapsedTicks;
        this.survivorCount = survivorCount;
        this.remainingHpSum = remainingHpSum;
    }
}
