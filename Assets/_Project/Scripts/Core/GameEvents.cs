using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 라운드의 팀(2인) 단위 결과. 각자 보드 승패를 MasterClient가 집계해 판정.
/// 라이프 규칙: BothWin(둘 다 승리)만 라이프 유지, Split/BothLose는 동일하게 라이프 -1(한 명만 져도 즉시 차감).
/// 보상은 현재 outcome과 무관하게 동일 지급(outcome별 차등은 미확정 TODO — ShopManager.HandleTeamRoundResolved 참고).
/// (RPC 전송용으로 int 캐스팅하므로 값 순서를 바꾸지 말 것.)
/// </summary>
public enum TeamRoundOutcome
{
    BothWin = 0,   // 둘 다 승리
    Split   = 1,   // 한 명만 패배
    BothLose = 2   // 둘 다 패배
}

/// <summary>
/// 세션 종료(패배) 사유. MatchRecorder가 전적(endReason)에 기록하고,
/// 결과 화면 문구 분기(UI)에도 사용. (RPC 전송은 없지만 기록값이므로 이름 변경 주의.)
/// </summary>
public enum SessionEndReason
{
    TeamHpZero,        // 팀 공통 HP 소진
    ReconnectFailed,   // 본인 재접속 실패
    PartnerAbandoned,  // 상대 미재접속(협동 불가)
    BothDisconnected,  // 둘 다 연결 끊김
    Surrender          // 2인 합의 항복(옵션창)
}

/// <summary>
/// 전투 종료 방식 구분. 타임아웃 전/후에 따라 판정 방법이 다르다.
/// </summary>
public enum BattleEndReason
{
    Victory,           // 30초 타임아웃 전 아군 전멸 또는 적군 전멸 → 아군 승
    Defeat,            // 30초 타임아웃 전 아군 전멸 → 아군 패
    DecisionVictory,   // 30초 타임아웃 후 HP 판정 → 아군 승
    DecisionDefeat     // 30초 타임아웃 후 HP 판정 → 아군 패
}

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

    /// <summary>전투 종료(종료 방식 구분). Victory/Defeat = 조기 종료, DecisionVictory/DecisionDefeat = 타임아웃 후 판정</summary>
    public static event Action<BattleEndReason> OnBattleEnd;

    /// <summary>
    /// 기본 전투 시간 종료 후 오버타임 진입. 인자 = 오버타임 지속시간(초, BattleManager._overtimeDuration).
    /// 오버타임 종료 전용 이벤트는 없음 — 오버타임이 끝나면(승패 확정) 기존 OnBattleEnd가 발행된다.
    /// </summary>
    public static event Action<float> OnOvertimeStarted;

    /// <summary>
    /// 팀(2인) 단위 라운드 결과 확정. MasterClient가 두 플레이어 승패를 집계해 발행.
    /// 라이프 차감은 NetworkManager(권위)가 처리하고, 보상은 RewardManager가 배수로 지급한다.
    /// </summary>
    public static event Action<TeamRoundOutcome> OnTeamRoundResolved;

    /// <summary>두 플레이어 모두 준비 완료 — 전투 페이즈로 전환</summary>
    public static event Action OnAllPlayersReady;

    /// <summary>UI requests that the local player be marked ready.</summary>
    public static event Action OnPlayerReadyRequested;

    /// <summary>RoundPhaseManager approves a ready request during Shopping.</summary>
    public static event Action OnPlayerReadyApproved;

    /// <summary>
    /// 준비 완료 인원 집계 변경. 인자 = (준비 완료 인원, 총 인원). NetworkManager가 Player
    /// CustomProperties(Ready) 변경 시마다 발행 — UI(StageTopPanelUI)의 "전투 준비중...(n/총)" 표시용.
    /// </summary>
    public static event Action<int, int> OnReadyCountChanged;

    // ──────────────────────────────────────────
    // 골드 / 레벨
    // ──────────────────────────────────────────

    /// <summary>골드 변경. 인자 = 변경 후 골드 총량</summary>
    public static event Action<int> OnGoldChanged;

    /// <summary>플레이어 레벨 변경. 인자 = 변경 후 레벨</summary>
    public static event Action<int> OnLevelChanged;

    /// <summary>XP 변경. 인자 = (현재 XP, 다음 레벨 필요 XP). ShopManager가 발행.</summary>
    public static event Action<int, int> OnXpChanged;

    /// <summary>
    /// 재접속 복원으로 레벨/XP가 통째로 되돌려짐. 인자 = (복원된 레벨, 복원된 XP).
    /// ShopManager.RestoreProgressionState()가 OnLevelChanged/OnXpChanged를 쏘기 <b>직전</b>에 발행한다 —
    /// 뒤따르는 레벨 변경이 "플레이 중 레벨업"이 아니라 복원임을 알리기 위한 것으로,
    /// 레벨업 연출처럼 <b>한 번뿐인 순간</b>을 표현하는 구독자가 자기 자신을 걸러낼 수 있게 한다.
    /// </summary>
    public static event Action<int, int> OnProgressionRestored;

    /// <summary>보드 배치 가능 기물 수 변경. 인자 = 변경 후 캡. ShopManager가 단일 소스로 발행.</summary>
    public static event Action<int> OnUnitCapChanged;

    /// <summary>플레이어 체력 변경. 인자 = 변경 후 체력 (팀 공통 HP — GDD 기준)</summary>
    public static event Action<int> OnHealthChanged;

    /// <summary>파트너(상대 클라이언트) 골드 변경. 인자 = 파트너의 현재 골드. UI 표시용.</summary>
    public static event Action<int> OnPartnerGoldChanged;

    /// <summary>아이템 쿠폰 변경. 인자 = 변경 후 쿠폰 총량</summary>
    public static event Action<int> OnItemCouponChanged;

    /// <summary>아이템/진화의 돌 인벤토리 변경. UI 갱신용.</summary>
    public static event Action OnInventoryChanged;

    /// <summary>
    /// 통신기 골드 전송 요청.
    /// 인자 = 전송할 골드 수량.
    /// UI가 발행하고 NetworkManager가 구독한다.
    /// </summary>
    public static event Action<int> OnGoldTransferRequested;

    // ──────────────────────────────────────────
    // 증강
    // ──────────────────────────────────────────

    /// <summary>
    /// 증강 3택1 선택지가 준비됨. 인자 = 제시된 증강 목록(보통 3개).
    /// AugmentManager가 발행 → 선택 UI가 표시하고, 플레이어 선택 시 AugmentManager.SelectAugment 호출.
    /// </summary>
    public static event Action<IReadOnlyList<AugmentData>> OnAugmentOfferReady;

    /// <summary>증강 선택 완료</summary>
    public static event Action<AugmentData> OnAugmentSelected;

    /// <summary>파트너(상대 클라이언트)의 누적 증강 목록 변경. 인자 = 영문명 배열(선택 순). UI 표시·전적 기록용.</summary>
    public static event Action<string[]> OnPartnerAugmentsChanged;

    // ──────────────────────────────────────────
    // 유닛
    // ──────────────────────────────────────────

    /// <summary>
    /// 상대(파트너) 보드 배치가 갱신됨. 인자 = 파트너 보드의 읽기 전용 스냅샷.
    /// NetworkManager가 RPC 수신 후 발행 → OpponentBoardView가 미러 렌더.
    /// </summary>
    public static event Action<BoardSnapshot> OnOpponentBoardChanged;

    /// <summary>
    /// 내 보드 스냅샷 재송출 요청. 재접속/파트너 재입장 시 유실된 미러 복구용.
    /// NetworkManager가 발행 → BoardSyncBroadcaster가 다음 LateUpdate에 송출.
    /// </summary>
    public static event Action OnBoardResyncRequested;

    /// <summary>
    /// 파트너 BattleSnapshot을 정상 수신(TryDecode 성공 + revision 최신)했을 때만 발행.
    /// NetworkManager.RPC_OnBattleSnapshot이 발행 — 이번 단계는 구독자가 없다(파트너 관전 후속 단계에서 소비 예정).
    /// Decode 실패·과거 revision일 때는 발행하지 않는다.
    /// </summary>
    public static event Action<BattleSnapshot> OnPartnerBattleSnapshotChanged;

    /// <summary>
    /// 파트너 전체화면 관전 진입/종료. 인자 = 지금 전체화면으로 열려 있는지(true=진입, false=종료).
    /// PIP(축소) 상태는 포함하지 않는다 — PartnerSpectateView.IsExpanded와 동일 기준.
    /// PartnerSpectateView.ToggleExpanded가 발행 → UIManager가 구독해 내 상점 HUD를 숨기고(진입)
    /// 관전 진입 전 상태로 복원한다(종료). 표시 여부만 바꿀 뿐 ShopManager 상태·상점 예약/공용 풀은
    /// 건드리지 않는다.
    /// </summary>
    public static event Action<bool> OnPartnerSpectateExpandedChanged;

    /// <summary>유닛 보드에 배치됨</summary>
    public static event Action<PokemonUnit> OnUnitPlaced;

    /// <summary>유닛 벤치로 돌아옴</summary>
    public static event Action<PokemonUnit> OnUnitBenched;

    /// <summary>유닛 배치가 거부됨. 인자 = 플레이어에게 표시할 사유.</summary>
    public static event Action<string> OnUnitPlacementRejected;

    /// <summary>유닛 판매됨</summary>
    public static event Action<PokemonUnit> OnUnitSold;

    /// <summary>
    /// 상점 구매로 새 유닛이 생성·배치 완료됨(뷰 전용, 울음소리 재생용). 인자 = 구매한 PokemonData.
    /// 합체 예외 구매(구매 즉시 3마리째로 성급진화)는 여기서 발행하지 않는다 — 그 경우는
    /// BoardManager.ExecuteMerge가 이미 OnUnitEvolved(최종 종 기준)를 발행하므로, 여기서 또 발행하면
    /// 같은 액션에 울음소리가 두 번(구매 종 + 진화 종) 겹친다. ShopManager.Buy()/ResolveSharedShopPurchase()의
    /// "합체 없이 신규 배치" 성공 경로에서만 발행.
    /// </summary>
    public static event Action<PokemonData> OnPokemonPurchased;

    /// <summary>
    /// 유닛의 종(data)·성(starLevel)이 배치/벤치 외의 경로로 바뀜 (예: 진화의 돌 장착/제거).
    /// BoardManager가 구독 → CheckEvolution 재실행해야 함. 안 그러면 돌 제거로 원복된
    /// 유닛이 같은 종 3마리를 채워도 합체가 트리거되지 않는다.
    /// </summary>
    public static event Action<PokemonUnit> OnUnitChanged;

    /// <summary>
    /// 유닛 진화 완료(연출용). 성급 합체·진화의 돌·통신교환 모두 발화한다.
    /// 인자 isStarUp = 성급이 오른 진화인지 여부(돌/통신교환은 false — 종만 바뀌고 별은 그대로).
    /// 상태 변경 통지는 OnUnitChanged/OnUnitPlaced가 담당하고, 이 이벤트는 뷰 전용이다.
    /// </summary>
    public static event Action<PokemonUnit, bool> OnUnitEvolved;

    /// <summary>
    /// 플러시와마이농(310)이 필드에 올라가 폼(플러시/마이농) 선택 대기 상태가 됨. 인자 = 선택 대상 유닛.
    /// UIManager가 발행 → 선택 UI가 표시하고, 플레이어가 고르면 UIManager.SelectPlusleMinunForm 호출.
    /// 증강 오퍼(OnAugmentOfferReady)와 같은 "제시 → UI 표시 → 매니저에 선택 통지" 흐름이다.
    /// </summary>
    public static event Action<PokemonUnit> OnPlusleMinunChoiceReady;

    /// <summary>
    /// 폼 선택 대기 종료. 선택 완료·시간 초과 자동 선택·전투 시작 자동 선택뿐 아니라
    /// 판매·합체·벤치 복귀로 대상이 사라진 경우에도 발화한다(= 선택 UI를 닫는 단일 신호).
    /// </summary>
    public static event Action OnPlusleMinunChoiceClosed;

    /// <summary>시너지 재계산 완료. 수신 측은 SynergyManager.GetActiveSynergies()로 pull</summary>
    public static event Action OnSynergyUpdated;

    /// <summary>통신교환으로 파트너에게서 유닛을 받아 벤치에 배치 완료(A측). 인자 = 받은 유닛(진화체일 수 있음).</summary>
    public static event Action<PokemonUnit> OnTradeUnitReceived;

    /// <summary>
    /// 통신기에 대기 중인 유닛 수 변경.
    /// 인자 = 현재 대기 중인 유닛 수.
    /// </summary>
    public static event Action<int> OnTradeQueueChanged;

    /// <summary>
    /// 통신교환 전송 실패.
    /// 파트너 없음, 라운드당 전송 횟수 초과, 네트워크 처리 실패 등에 사용.
    /// 보낸 유닛은 그대로 유지된다.
    /// </summary>
    public static event Action OnTradeRejected;

    /// <summary>통신기 골드 전송으로 파트너에게서 골드 수령 완료(받는 쪽). 인자 = 받은 골드. 골드 잔액 표시는 OnGoldChanged가 담당.</summary>
    public static event Action<int> OnPartnerGoldReceived;

    /// <summary>통신기 골드 전송 완료(보낸 쪽, 상대 수신 ack까지 확인). 인자 = 보낸 골드.</summary>
    public static event Action<int> OnGoldTransferCompleted;

    /// <summary>통신기 골드 전송 실패(보낸 쪽 피드백용). 인자 = 사유 문구. 선차감분은 환급된 상태로 발행됨.</summary>
    public static event Action<string> OnGoldTransferRejected;

    // ──────────────────────────────────────────
    // 샵
    // ──────────────────────────────────────────

    /// <summary>UI가 XP 구매를 요청. ShopManager가 골드/최대 레벨을 검증한 뒤 처리한다.</summary>
    public static event Action OnXpPurchaseRequested;

    /// <summary>UI가 유닛 상점 리롤을 요청. ShopManager가 무료 리롤/골드를 검증한 뒤 처리한다.</summary>
    public static event Action OnShopRerollRequested;

    /// <summary>유닛 샵 리롤됨</summary>
    public static event Action OnShopRerolled;

    /// <summary>무료 리롤 자원 변경. 인자 = 변경 후 잔여 리롤 횟수. ShopManager가 단일 소스로 발행(보상 지급/소모 시).</summary>
    public static event Action<int> OnRerollCountChanged;

    /// <summary>수동 리롤 1회가 실제로 소모됨(무료/골드 무관). 리롤 환급 증강 등의 훅 — 구독 측이 확률 판정 후 Shop.AddReroll로 환급.</summary>
    public static event Action OnRerollSpent;

    /// <summary>
    /// 리롤 환급이 실제로 발동함(하이퍼 티켓 45% 적중). UI 표시 전용 훅 —
    /// 환급 수량 자체는 OnRerollCountChanged로 이미 전달되므로 여기서는 "방금 환급됐다"만 알린다.
    /// </summary>
    public static event Action OnRerollRefunded;

    /// <summary>아이템 샵 갱신됨</summary>
    public static event Action OnItemShopRerolled;

    /// <summary>아이템 상점 구매 완료. 인자 = 구매한 일반 아이템 또는 진화의 돌</summary>
    public static event Action<ScriptableObject> OnItemPurchased;


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

    /// <summary>
    /// 상대방 이탈 감지(재접속 대기 시작). 인자 = 참고용 유예 시간(초, RECONNECT_GRACE_PERIOD).
    /// 의도적 퇴장(타이틀로/게임종료)과 비정상 연결 끊김 모두 동일하게 이 이벤트로 통일된다(2026-08 파트너 이탈 UX).
    /// </summary>
    public static event Action<float> OnOpponentDisconnected;

    /// <summary>상대방이 재접속함(대기 중 언제든 발생 가능 — 시간 제한으로 막지 않음)</summary>
    public static event Action OnOpponentReconnected;

    /// <summary>
    /// 상대방 이탈 후 30초 경과 — [포기하기] 버튼을 노출해도 되는 시점(2026-08 재정의, 과거엔 "유예시간 종료=자동 세션 종료" 의미였음).
    /// 더 이상 자동으로 세션을 종료하지 않는다 — 사용자가 직접 포기를 선택해야 GameEvents.SessionEnded가 발행된다.
    /// 인자 bothDisconnected는 발행 시점 참고용(이 시점 기준 분기 처리는 하지 않음 — 동시 이탈 전용 흐름은 범위 밖).
    /// </summary>
    public static event Action<bool> OnGracePeriodExpired;

    /// <summary>세션 종료(패배 처리). 인자 = 종료 사유. RoundPhaseManager(GameOver 전환)·MatchRecorder(전적 기록)가 구독.</summary>
    public static event Action<SessionEndReason> OnSessionEnded;

    /// <summary>
    /// 마지막 라운드(챕터 최종 스테이지) 클리어 — 게임 완주(승리).
    /// MasterClient가 최종 라운드 결과 후 BroadcastGameCleared로 전체에 발행 →
    /// RoundPhaseManager가 GamePhase.Victory로 전환. (다음 라운드를 브로드캐스트하지 않음.)
    /// </summary>
    public static event Action OnGameCleared;

    // ──────────────────────────────────────────
    // 전적
    // ──────────────────────────────────────────

    /// <summary>
    /// 전적 1건 기록 완료(승패 무관). 인자 = 방금 저장된 기록. MatchRecorder가 발행.
    /// UI(결과 화면/전적 창)가 구독해 즉시 표시. 과거 전적 조회는 MatchHistoryStore.LoadRecent() pull.
    /// </summary>
    public static event Action<MatchRecord> OnMatchRecorded;

    /// <summary>서버 전적 조회 요청(전적창 UI → SupabaseMatchUploader). 인자 = 최대 건수.</summary>
    public static event Action<int> OnServerMatchesRequested;

    /// <summary>
    /// 서버 전적 조회 결과(SupabaseMatchUploader → 전적창 UI).
    /// records = 최신순 목록(실패 시 null), error = 실패 사유(성공 시 null/빈 문자열).
    /// </summary>
    public static event Action<IReadOnlyList<MatchRecord>, string> OnServerMatchesLoaded;

    // ──────────────────────────────────────────
    // Invoke 헬퍼 (외부에서 직접 ?.Invoke 말고 여기 통해서 호출)
    // ──────────────────────────────────────────

    public static void AugmentOfferReady(IReadOnlyList<AugmentData> offer) => OnAugmentOfferReady?.Invoke(offer);
    public static void AugmentSelected(AugmentData data) => OnAugmentSelected?.Invoke(data);
    public static void PartnerAugmentsChanged(string[] augmentNamesEn) => OnPartnerAugmentsChanged?.Invoke(augmentNamesEn);
    public static void BattleStart()           => OnBattleStart?.Invoke();
    public static void BattleEnd(BattleEndReason reason) => OnBattleEnd?.Invoke(reason);
    public static void OvertimeStarted(float duration) => OnOvertimeStarted?.Invoke(duration);
    public static void TeamRoundResolved(TeamRoundOutcome outcome) => OnTeamRoundResolved?.Invoke(outcome);
    public static void AllPlayersReady()       => OnAllPlayersReady?.Invoke();
    public static void RequestPlayerReady()    => OnPlayerReadyRequested?.Invoke();
    public static void ApprovePlayerReady()    => OnPlayerReadyApproved?.Invoke();
    public static void ReadyCountChanged(int ready, int total) => OnReadyCountChanged?.Invoke(ready, total);
    public static void GoldChanged(int amount) => OnGoldChanged?.Invoke(amount);
    public static void LevelChanged(int level) => OnLevelChanged?.Invoke(level);
    public static void XpChanged(int current, int required) => OnXpChanged?.Invoke(current, required);
    public static void ProgressionRestored(int level, int currentXp) => OnProgressionRestored?.Invoke(level, currentXp);
    public static void UnitCapChanged(int cap) => OnUnitCapChanged?.Invoke(cap);
    public static void HealthChanged(int health) => OnHealthChanged?.Invoke(health);
    public static void PartnerGoldChanged(int gold) => OnPartnerGoldChanged?.Invoke(gold);
    public static void ItemCouponChanged(int amount) => OnItemCouponChanged?.Invoke(amount);
    public static void InventoryChanged() => OnInventoryChanged?.Invoke();
    public static void OpponentBoardChanged(BoardSnapshot snap) => OnOpponentBoardChanged?.Invoke(snap);
    public static void BoardResyncRequested() => OnBoardResyncRequested?.Invoke();
    public static void PartnerBattleSnapshotChanged(BattleSnapshot snapshot) => OnPartnerBattleSnapshotChanged?.Invoke(snapshot);
    public static void PartnerSpectateExpandedChanged(bool expanded) => OnPartnerSpectateExpandedChanged?.Invoke(expanded);
    public static void UnitPlaced(PokemonUnit unit)  => OnUnitPlaced?.Invoke(unit);
    public static void UnitBenched(PokemonUnit unit) => OnUnitBenched?.Invoke(unit);
    public static void UnitPlacementRejected(string reason) => OnUnitPlacementRejected?.Invoke(reason);
    public static void UnitSold(PokemonUnit unit)    => OnUnitSold?.Invoke(unit);
    public static void PokemonPurchased(PokemonData data) => OnPokemonPurchased?.Invoke(data);
    public static void UnitChanged(PokemonUnit unit) => OnUnitChanged?.Invoke(unit);
    public static void PlusleMinunChoiceReady(PokemonUnit unit) => OnPlusleMinunChoiceReady?.Invoke(unit);
    public static void PlusleMinunChoiceClosed() => OnPlusleMinunChoiceClosed?.Invoke();
    public static void UnitEvolved(PokemonUnit unit, bool isStarUp) => OnUnitEvolved?.Invoke(unit, isStarUp);
    public static void SynergyUpdated()              => OnSynergyUpdated?.Invoke();
    public static void TradeUnitReceived(PokemonUnit unit) => OnTradeUnitReceived?.Invoke(unit);

    public static void TradeQueueChanged(int count)  => OnTradeQueueChanged?.Invoke(count);
    public static void TradeRejected()               => OnTradeRejected?.Invoke();
    public static void PartnerGoldReceived(int amount)     => OnPartnerGoldReceived?.Invoke(amount);
    public static void GoldTransferCompleted(int amount)   => OnGoldTransferCompleted?.Invoke(amount);
    public static void GoldTransferRejected(string reason) => OnGoldTransferRejected?.Invoke(reason);
    public static void RequestXpPurchase()          => OnXpPurchaseRequested?.Invoke();
    public static void RequestShopReroll()          => OnShopRerollRequested?.Invoke();
    public static void ShopRerolled()               => OnShopRerolled?.Invoke();
    public static void RerollCountChanged(int count) => OnRerollCountChanged?.Invoke(count);
    public static void RerollSpent()                => OnRerollSpent?.Invoke();
    public static void RerollRefunded()             => OnRerollRefunded?.Invoke();
    public static void ItemShopRerolled() => OnItemShopRerolled?.Invoke();
    public static void ItemPurchased(ScriptableObject item) => OnItemPurchased?.Invoke(item);
    public static void RoundChanged(int round)       => OnRoundChanged?.Invoke(round);
    public static void PhaseChanged(GamePhase phase) => OnPhaseChanged?.Invoke(phase);
    public static void StageEntered(StageData stage) => OnStageEntered?.Invoke(stage);

    public static void OpponentDisconnected(float graceSeconds) => OnOpponentDisconnected?.Invoke(graceSeconds);
    public static void OpponentReconnected()        => OnOpponentReconnected?.Invoke();
    public static void GracePeriodExpired(bool bothDisconnected) => OnGracePeriodExpired?.Invoke(bothDisconnected);
    public static void SessionEnded(SessionEndReason reason) => OnSessionEnded?.Invoke(reason);
    public static void GameCleared()                => OnGameCleared?.Invoke();
    public static void MatchRecorded(MatchRecord record) => OnMatchRecorded?.Invoke(record);
    public static void RequestServerMatches(int count) => OnServerMatchesRequested?.Invoke(count);
    public static void ServerMatchesLoaded(IReadOnlyList<MatchRecord> records, string error) => OnServerMatchesLoaded?.Invoke(records, error);
    
    public static void RequestGoldTransfer(int amount) => OnGoldTransferRequested?.Invoke(amount);
}
