using System;
using UnityEngine;

/// <summary>
/// 매니저 간 직접 참조 대신 이벤트로 통신.
/// 새 이벤트는 반드시 여기에만 추가할 것.
/// </summary>
public static class GameEvents
{
    // ──────────────────────────────────────────
    // 전투
    // ──────────────────────────────────────────

    /// <summary>전투 시작</summary>
    public static event Action OnBattleStart;

    /// <summary>전투 종료. true = 승, false = 패</summary>
    public static event Action<bool> OnBattleEnd;

    /// <summary>두 플레이어 모두 준비 완료 — 전투 페이즈로 전환</summary>
    public static event Action OnAllPlayersReady;

    // ──────────────────────────────────────────
    // 골드 / 레벨
    // ──────────────────────────────────────────

    /// <summary>골드 변경. 인자 = 변경 후 골드 총량</summary>
    public static event Action<int> OnGoldChanged;

    /// <summary>플레이어 레벨 변경. 인자 = 변경 후 레벨</summary>
    public static event Action<int> OnLevelChanged;

    /// <summary>플레이어 체력 변경. 인자 = 변경 후 체력 (팀 공통 HP — GDD 기준)</summary>
    public static event Action<int> OnHealthChanged;

    /// <summary>파트너(상대 클라이언트) 골드 변경. 인자 = 파트너의 현재 골드. UI 표시용.</summary>
    public static event Action<int> OnPartnerGoldChanged;

    // ──────────────────────────────────────────
    // 증강
    // ──────────────────────────────────────────

    /// <summary>증강 선택 완료</summary>
    public static event Action<AugmentData> OnAugmentSelected;

    // ──────────────────────────────────────────
    // 유닛
    // ──────────────────────────────────────────

    /// <summary>
    /// 상대(파트너) 보드 배치가 갱신됨. 인자 = 파트너 보드의 읽기 전용 스냅샷.
    /// NetworkManager가 RPC 수신 후 발행 → OpponentBoardView가 미러 렌더.
    /// </summary>
    public static event Action<BoardSnapshot> OnOpponentBoardChanged;

    /// <summary>유닛 보드에 배치됨</summary>
    public static event Action<PokemonUnit> OnUnitPlaced;

    /// <summary>유닛 벤치로 돌아옴</summary>
    public static event Action<PokemonUnit> OnUnitBenched;

    /// <summary>유닛 판매됨</summary>
    public static event Action<PokemonUnit> OnUnitSold;

    /// <summary>시너지 재계산 완료. 수신 측은 SynergyManager.GetActiveSynergies()로 pull</summary>
    public static event Action OnSynergyUpdated;

    // ──────────────────────────────────────────
    // 샵
    // ──────────────────────────────────────────

    /// <summary>샵 리롤됨</summary>
    public static event Action OnShopRerolled;

    // ──────────────────────────────────────────
    // 라운드 / 페이즈
    // ──────────────────────────────────────────

    /// <summary>라운드 번호 변경. 인자 = 새 라운드 번호</summary>
    public static event Action<int> OnRoundChanged;

    /// <summary>페이즈 전환. 인자 = 새 페이즈</summary>
    public static event Action<GamePhase> OnPhaseChanged;

    /// <summary>
    /// 현재 라운드의 스테이지가 확정됨(라운드 시작 시점). 인자 = 진입한 StageData.
    /// RoundPhaseManager가 StageDatabase에서 해석 후 발행.
    /// 구독 측 훅(기획 확정 후 담당자가 채움):
    ///   - 전투 전 보상/증강(preReward), 트레이너 등장 연출, 스테이지 타입별 BGM/배경 등.
    /// 전투의 적 구성은 BattleManager가 Phase.CurrentStage에서 직접 읽으므로 이 이벤트 구독 불필요.
    /// </summary>
    public static event Action<StageData> OnStageEntered;

    // ──────────────────────────────────────────
    // 연결 끊김 / 재접속
    // ──────────────────────────────────────────

    /// <summary>상대방 연결 끊김(재접속 대기 중). 인자 = 유예 시간(초)</summary>
    public static event Action<float> OnOpponentDisconnected;

    /// <summary>상대방이 유예시간 내 재접속함</summary>
    public static event Action OnOpponentReconnected;

    /// <summary>재접속 유예시간 종료. 인자 = 둘 다 끊겼는지 여부 (true면 세션 종료)</summary>
    public static event Action<bool> OnGracePeriodExpired;

    /// <summary>세션 종료(패배 처리). 전적 기록은 미구현 — 로그로만 처리</summary>
    public static event Action OnSessionEnded;

    // ──────────────────────────────────────────
    // Invoke 헬퍼 (외부에서 직접 ?.Invoke 말고 여기 통해서 호출)
    // ──────────────────────────────────────────

    public static void AugmentSelected(AugmentData data) => OnAugmentSelected?.Invoke(data);
    public static void BattleStart()           => OnBattleStart?.Invoke();
    public static void BattleEnd(bool isWin)   => OnBattleEnd?.Invoke(isWin);
    public static void AllPlayersReady()       => OnAllPlayersReady?.Invoke();
    public static void GoldChanged(int amount) => OnGoldChanged?.Invoke(amount);
    public static void LevelChanged(int level) => OnLevelChanged?.Invoke(level);
    public static void HealthChanged(int health) => OnHealthChanged?.Invoke(health);
    public static void PartnerGoldChanged(int gold) => OnPartnerGoldChanged?.Invoke(gold);
    public static void OpponentBoardChanged(BoardSnapshot snap) => OnOpponentBoardChanged?.Invoke(snap);
    public static void UnitPlaced(PokemonUnit unit)  => OnUnitPlaced?.Invoke(unit);
    public static void UnitBenched(PokemonUnit unit) => OnUnitBenched?.Invoke(unit);
    public static void UnitSold(PokemonUnit unit)    => OnUnitSold?.Invoke(unit);
    public static void SynergyUpdated()              => OnSynergyUpdated?.Invoke();
    public static void ShopRerolled()               => OnShopRerolled?.Invoke();
    public static void RoundChanged(int round)      => OnRoundChanged?.Invoke(round);
    public static void PhaseChanged(GamePhase phase) => OnPhaseChanged?.Invoke(phase);
    public static void StageEntered(StageData stage) => OnStageEntered?.Invoke(stage);

    public static void OpponentDisconnected(float graceSeconds) => OnOpponentDisconnected?.Invoke(graceSeconds);
    public static void OpponentReconnected()        => OnOpponentReconnected?.Invoke();
    public static void GracePeriodExpired(bool bothDisconnected) => OnGracePeriodExpired?.Invoke(bothDisconnected);
    public static void SessionEnded()               => OnSessionEnded?.Invoke();
}
