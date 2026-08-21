using System.Collections.Generic;
using UnityEngine;

#if PHOTON_UNITY_NETWORKING
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Photon PUN2 기반 네트워크 매니저.
/// 연결 / 룸 관리 / 라운드 동기화 담당.
/// GameEvents를 통해 다른 매니저와 통신.
/// </summary>
public class NetworkManager : MonoBehaviourPunCallbacks
{
    [Header("테스트")]
    [Tooltip("체크하면 Photon 없이 1인 오프라인 루프로 동작 (테스트 전용)")]
    [SerializeField] private bool _soloMode;

    // ─────────────────────────────────────────
    // 상수
    // ─────────────────────────────────────────

    private const int   MAX_PLAYERS         = 2;
    private const float CONNECT_TIMEOUT     = 10f;
    // Photon 플레이어 닉네임 최대 길이.
    private const int   MAX_NICKNAME_LENGTH = 16;

    /// <summary>프로세스 재시작 후 이전 방 재입장을 시도하기 위해 UserId를 보존할 때 쓰는 PlayerPrefs 키.</summary>
    private const string PREF_LAST_USER_ID = "LastPhotonUserId";

    /// <summary>프로세스 재시작 후 재입장을 시도할 방 이름을 보존할 때 쓰는 PlayerPrefs 키.</summary>
    private const string PREF_LAST_ROOM_NAME = "LastPhotonRoomName";

    /// <summary>타이틀 화면에 "이전 닉네임: ○○" 표시용으로만 쓰는 닉네임(재접속 인증에는 사용하지 않음).</summary>
    private const string PREF_LAST_NICKNAME = "LastPhotonNickname";

    /// <summary>
    /// QA 전용: 같은 PC에서 동일 빌드 exe를 여러 개 띄워 테스트할 때 재접속 PlayerPrefs가 서로
    /// 덮어쓰는 문제를 피하기 위한 저장 키 슬롯. 커맨드라인 "-qaClient=값"으로 지정하며,
    /// 인자가 없거나 형식이 이상하면 null로 남아 일반 실행과 100% 동일하게 동작한다(기존 키 그대로,
    /// 마이그레이션 없음). 값 자체는 프로세스 실행 인자이므로 static readonly로 프로세스 동안 고정 —
    /// NetworkManager는 씬마다 새 인스턴스가 생성되므로(위 s_resyncAfterRejoinPending과 같은 이유)
    /// 인스턴스 필드가 아니라 정적 필드로 둬야 씬 전환 후에도 값이 유지된다.
    /// 프로세스를 자동 식별하는 기능이 아니라 QA가 실행마다 명시적으로 고르는 테스트 프로필 개념이라,
    /// 같은 슬롯을 두 프로세스에 동시에 쓰면 그 둘끼리는 여전히 같은 저장값을 공유한다(의도된 동작).
    /// </summary>
    private static readonly string s_qaSlot = ParseQaSlot();

    /// <summary>커맨드라인에서 "-qaClient=값" 형태의 QA 슬롯 인자를 찾는다. 없거나 값이 비어 있으면 null.</summary>
    private static string ParseQaSlot()
    {
        const string prefix = "-qaClient=";
        foreach (var arg in System.Environment.GetCommandLineArgs())
        {
            if (!arg.StartsWith(prefix, System.StringComparison.Ordinal)) continue;

            string slot = arg[prefix.Length..].Trim();
            return string.IsNullOrEmpty(slot) ? null : slot;
        }
        return null;
    }

    /// <summary>재접속 PlayerPrefs 키에 QA 슬롯 접미사를 붙인다. s_qaSlot이 null이면(일반 실행) baseKey를 그대로 반환 —
    /// 기존 저장값과 완전히 같은 키를 쓰므로 마이그레이션이 필요 없다.</summary>
    private static string PrefKey(string baseKey) =>
        s_qaSlot == null ? baseKey : $"{baseKey}_{s_qaSlot}";

    /// <summary>Start()에서 저장된 세션을 발견하면 채워지는 재입장 대상 방 이름. null이면 저장된 세션 없음(신규 로그인).
    /// 2026-08부터 이 값이 채워져 있어도 자동으로 재입장을 시도하지 않는다 — 타이틀 화면에서 사용자가
    /// AttemptRejoinSavedSession()/AbandonPreviousSession()을 명시적으로 호출할 때만 실제 요청이 나간다.</summary>
    private string _pendingRejoinRoomName;

    /// <summary>PREF_LAST_NICKNAME에서 읽은 표시 전용 닉네임.</summary>
    private string _savedNickname;

    /// <summary>지금 어떤 목적으로 RejoinRoom을 시도 중인지. UI 폴링(IsRejoining)과 OnJoinedRoom/OnJoinRoomFailed 분기에 쓴다.</summary>
    private enum RejoinPurpose { None, EnterGame, NotifyAbandonAndLeave }
    private RejoinPurpose _rejoinPurpose = RejoinPurpose.None;

    /// <summary>AttemptRejoinSavedSession() 실패 시 true. 타이틀 화면이 안내 팝업을 띄우는 데 쓴다.</summary>
    public bool RejoinFailed { get; private set; }

    /// <summary>상대가 재입장(재접속 성공)했는지 확정하기 전에 "포기 통지" RPC가 뒤이어 오지 않는지 짧게 기다리는 시간(초).</summary>
    private const float REJOIN_ABANDON_CHECK_DELAY = 1f;

    private Coroutine _reconnectConfirmRoutine;

    /// <summary>파트너가 재접속을 포기했다는 통지(RPC_PartnerGaveUpReconnect)를 받았는지. OptionsPanelUI가 폴링.</summary>
    private bool _partnerGaveUpReconnectNotice;

    /// <summary>LeaveRoom() 요청~완료(OnLeftRoom/OnDisconnected) 사이. 이 동안 새 SetCustomProperties 호출을 막는다
    /// ("Operation SetProperties ... client state: Leaving" 오류 방지, 2026-08).</summary>
    private bool _isLeavingRoom;

    /// <summary>RequestReturnToTitle()로 예약된 타이틀 씬 이름. LeaveRoom 완료 후에만 실제로 로드한다.</summary>
    private string _pendingTitleSceneName;

    /// <summary>"준비 완료" 여부를 Player CustomProperties에 저장할 때 쓰는 키</summary>
    private const string READY_PROP_KEY = "Ready";

    /// <summary>플레이어 골드를 Player CustomProperties에 저장할 때 쓰는 키(파트너 표시용).</summary>
    private const string GOLD_PROP_KEY = "Gold";

    /// <summary>플레이어의 누적 증강 영문명 배열을 Player CustomProperties에 저장할 때 쓰는 키.
    /// RPC와 달리 유예(비활성) 중에도 서버에 보존돼 재접속·마스터 교체에 안전하다.</summary>
    private const string AUGMENTS_PROP_KEY = "Augments";

    /// <summary>플레이어 레벨을 Player CustomProperties에 저장할 때 쓰는 키(재접속 복원용).</summary>
    private const string LEVEL_PROP_KEY = "Level";

    /// <summary>플레이어 현재 XP(레벨 내 진행도)를 Player CustomProperties에 저장할 때 쓰는 키(재접속 복원용).</summary>
    private const string XP_PROP_KEY = "CurrentXp";

    /// <summary>플레이어의 무료 리롤 잔여 횟수를 Player CustomProperties에 저장할 때 쓰는 키(재접속 복원용).</summary>
    private const string REROLL_COUNT_PROP_KEY = "RerollCount";

    /// <summary>아이템 상점 재접속 스냅샷(JSON 직렬화된 ShopManager.ItemShopReconnectSnapshot)을
    /// Player CustomProperties에 저장할 때 쓰는 키. 개인 로컬 상태라 Room이 아닌 Player 속성에 둔다.</summary>
    private const string ITEM_SHOP_STATE_PROP_KEY = "ItemShopState";

    /// <summary>내 보드+벤치 유닛 스냅샷(JSON 직렬화된 BoardManager.UnitSaveData[])을 Player
    /// CustomProperties에 저장할 때 쓰는 키(1차 구현 — 저장/복원 기반만).</summary>
    private const string UNITS_PROP_KEY = "Units";

    /// <summary>유닛 스냅샷 저장 디바운스 지연(초). 벤치 정리처럼 배치/판매 이벤트가 짧은 시간에
    /// 연달아 발생해도 SetCustomProperties를 매번 동기 호출하지 않고, 마지막 변경 후 이 시간만큼
    /// 조용하면 그때 한 번만 저장한다(Photon CustomProperties 갱신 빈도 제한 회피, 2026-08 코드리뷰).</summary>
    private const float UNIT_SNAPSHOT_SAVE_DELAY = 0.5f;

    /// <summary>디바운스 대기 중인 유닛 스냅샷 저장 코루틴. 새 변경 이벤트가 오면 재시작(타이머 리셋)한다.</summary>
    private Coroutine _saveUnitSnapshotCoroutine;

    /// <summary>재접속 유닛 스냅샷 복원 중인지. 복원 중엔 BoardManager.TryPlaceUnit/TryPlaceInBench가
    /// 발생시키는 OnUnitPlaced/OnUnitBenched로 인해 저장 핸들러가 재실행되며 불완전한 스냅샷을
    /// 덮어쓰는 것을 막는다(2026-08 설계 검토에서 확인된 위험).</summary>
    private bool _isRestoringUnitSnapshot;

    /// <summary>
    /// ResyncAfterReconnect()의 라운드 캐치업(RPC_OnRoundStart 로컬 호출) 실행 중인지. 이 호출이
    /// GameEvents.RoundChanged를 발행해 ShopManager.HandleRoundChanged가 같은 프레임에 동기 실행되는데,
    /// 그 안의 무조건 Roll()/RollItemShop()/이자 계산이 방금 복원한 상점/골드를 다시 덮어쓰는 문제가
    /// 있었다(2026-08 확인). s_isResumingRejoinedMatch는 이번 씬의 Start() 단계 내내 유지돼야 하는
    /// 값이라(ShopManager.Start() 자신의 가드가 이를 그대로 써야 함) 재사용하지 않고, 이 캐치업 호출
    /// "한 번"만을 좁게 감싸는 별도 플래그를 둔다 — try/finally로 호출 직후 즉시 꺼지므로, 이후
    /// 이어지는 정상적인 라운드 진행(2→3라운드 등)에는 전혀 영향을 주지 않는다.
    /// </summary>
    private bool _isApplyingReconnectRoundCatchup;

    /// <summary>재접속 라운드 캐치업(RPC_OnRoundStart 로컬 재호출)이 지금 진행 중인지. ShopManager 등이
    /// 이 시점의 RoundChanged를 "복원 중 재발행"으로 구분해 자신의 초기화 로직을 건너뛸 때 쓴다.</summary>
    public bool IsApplyingReconnectRoundCatchup => _isApplyingReconnectRoundCatchup;

    /// <summary>UnitSaveData[]를 JsonUtility로 직렬화하기 위한 래퍼(JsonUtility는 배열을 루트로 직렬화 못 함).
    /// 순수 JSON 변환 전용 — BoardManager는 이 타입을 몰라도 된다.</summary>
    [System.Serializable]
    private class UnitSnapshotWrapper
    {
        public BoardManager.UnitSaveData[] units;
    }

    /// <summary>팀 공통 HP를 Room CustomProperties에 저장할 때 쓰는 키(GDD: 팀 공통 체력).</summary>
    private const string TEAM_HP_PROP_KEY = "TeamHP";

    /// <summary>게임 씬 로드 완료 여부를 Player CustomProperties에 저장할 때 쓰는 키(라운드 시작 핸드셰이크).</summary>
    private const string SCENE_READY_PROP_KEY = "SceneReady";

    /// <summary>이번 라운드 전투 결과를 Player CustomProperties에 저장할 때 쓰는 키. -1=미보고, 0=패, 1=승.</summary>
    private const string BATTLE_RESULT_PROP_KEY = "BattleResult";
    private const int    RESULT_NOT_REPORTED = -1;

    /// <summary>현재 진행 라운드를 Room CustomProperties에 저장할 때 쓰는 키.
    /// RPC_OnRoundStart는 비접속자에게 유실되므로, 재접속 클라이언트는 이 속성으로 라운드를 복구한다.</summary>
    private const string ROUND_PROP_KEY = "Round";

    /// <summary>내가 마지막으로 수신/적용한 라운드. 재접속 시 Room 속성의 현재 라운드와 비교해 유실분을 복구.</summary>
    private int _lastKnownRound;

    /// <summary>내 보드 스냅샷 송출 revision(단조 증가). 수신 측은 과거 revision을 무시해 순서 역전을 막는다.</summary>
    private int _localBoardRevision;

    /// <summary>상대 보드 스냅샷의 마지막 적용 revision. 새 판(라운드 1) 시작 시 리셋.</summary>
    private int _lastOpponentBoardRevision = -1;

    /// <summary>내 BattleSnapshot 송출 revision(단조 증가). BoardSnapshot의 revision과 별도 필드로 관리한다.</summary>
    private int _localBattleSnapshotRevision;

    /// <summary>파트너 BattleSnapshot의 마지막 적용 revision. 새 판(라운드 1)·파트너 이탈 시 리셋.</summary>
    private int _lastPartnerBattleSnapshotRevision = -1;

    /// <summary>수신에 성공한 파트너 BattleSnapshot. 저장·조회만 제공 — 이번 단계는 미러 전투를 만들지 않는다.</summary>
    private BattleSnapshot _partnerBattleSnapshot;

    /// <summary>파트너 BattleSnapshot을 한 번이라도 정상 수신했는지.</summary>
    public bool HasPartnerBattleSnapshot => _partnerBattleSnapshot != null;

    /// <summary>수신한 파트너 BattleSnapshot(읽기 전용 참조). 수신 전이거나 파트너 이탈로 초기화됐으면 null.
    /// 외부에서 재대입할 수 있는 public setter는 없다 — 내용을 바꾸려면 RPC 수신 경로를 통해서만 갱신된다.</summary>
    public BattleSnapshot PartnerBattleSnapshot => _partnerBattleSnapshot;

    /// <summary>마지막으로 적용된 파트너 BattleSnapshot revision. 수신 전이면 -1(QA 표시용).</summary>
    public int PartnerBattleSnapshotRevision => _lastPartnerBattleSnapshotRevision;

    /// <summary>지금까지 내가 보낸 BattleSnapshot revision(단조 증가, 아직 한 번도 안 보냈으면 0). QA 표시용.</summary>
    public int LocalBattleSnapshotRevision => _localBattleSnapshotRevision;

    /// <summary>팀 라운드가 BothWin(둘 다 승리)이 아닐 때(Split 포함) 차감할 라이프(공용 HP 단위 = 라이프 1).</summary>
    private const int    LIFE_LOSS_ON_TEAM_DEFEAT = 1;

    /// <summary>디버그: 켜지면 팀 공통 HP가 절대 깎이지 않음(무한 HP). PrototypeHud에서 토글. 빌드/검증 편의용.</summary>
    public static bool DebugInfiniteTeamHealth = false;

    /// <summary>디버그: 켜지면 ReportBattleResult가 실제 보고를 안 하고 붙들어둔다(응답 불능 상황 재현용).
    /// PrototypeHud에서 토글. "파트너 이탈 UX로 위임" 흐름을 실제 접속 끊김 없이 재현하려는 목적 —
    /// 에디터 일시정지는 네트워크 자체도 멎어서 "진짜 이탈"과 구분이 안 된다.</summary>
    public static bool DebugSuppressBattleResultReport = false;

    /// <summary>이번 라운드 팀 결과를 이미 판정했는지(MasterClient, 중복 발행 방지). 라운드 시작 시 리셋.</summary>
    private bool _roundResultResolved;

    /// <summary>내가 마지막으로 보고한 전투 결과(승/패). 재전송 요청(RPC_RequestBattleResultResend) 응답용
    /// 캐시일 뿐, 승패 추측에는 쓰지 않는다. 아직 이번 라운드 전투를 안 끝냈으면 null. 라운드 시작 시 리셋.</summary>
    private bool? _lastLocalBattleResult;

    /// <summary>DebugSuppressBattleResultReport가 켜져 있는 동안 ReportBattleResult가 실제로 보내지 않고
    /// 붙들어둔 결과. DebugSendSuppressedBattleResultNow()로 나중에 보낼 수 있다. 라운드 시작 시 리셋.</summary>
    private bool? _suppressedBattleResult;

    /// <summary>지금 억제된(아직 안 보낸) 전투 결과가 있는지. QA 패널에서 "지금 보내기" 버튼 활성화 판정용.</summary>
    public bool HasSuppressedBattleResult => _suppressedBattleResult.HasValue;

    /// <summary>이번 라운드에서 "파트너 응답 불능"을 이미 진단했는지(MasterClient). 라운드 시작 시 리셋.
    /// 파트너가 Photon에는 계속 연결돼 있는데(=진짜 이탈이 아님) 전투 결과 응답만 안 오는 상황을 감지했을
    /// 때 켠다. _opponentDisconnected(Photon 실제 이탈용)와는 트리거·해제 조건이 달라 별도로 관리한다
    /// (2026-08-21 티켓: 승패를 추측하는 대신 기존 이탈 UX로 위임).</summary>
    private bool _partnerResultUnresponsive;

    /// <summary>_partnerResultUnresponsive 진단 후 GIVE_UP_AVAILABLE_DELAY 대기용 코루틴.
    /// OpponentGraceRoutine과 동일한 타이밍이지만 트리거가 달라 독립적으로 운영한다.</summary>
    private Coroutine _partnerUnresponsiveGraceRoutine;

    /// <summary>라운드 1을 한 번만 시작하기 위한 마스터 가드.</summary>
    private bool _gameStarted;

    /// <summary>
    /// 연결 끊김 후 재접속 유예 시간(초). Room.PlayerTtl에도 동일하게 적용(2026-08 파트너 이탈 UX 작업에서 변경하지 않음).
    /// ⚠️ 이 값은 Photon 서버가 끊긴 플레이어의 자리를 실제로 보존해주는 한계 시간이다. 남은 플레이어의
    /// 대기 UI 자체는 GIVE_UP_AVAILABLE_DELAY 이후로 무한정 대기하지만, 그 시점 이후에도 이 60초가 지나면
    /// Photon 서버가 상대 자리를 회수해 재접속이 물리적으로 불가능해질 수 있다(코드로 해결 불가, 기획 확인 필요).
    /// </summary>
    private const float RECONNECT_GRACE_PERIOD = 60f;

    /// <summary>
    /// 상대 이탈 후 [포기하기] 버튼을 노출하기까지 대기 시간(초). RECONNECT_GRACE_PERIOD(60초, PlayerTtl)와는
    /// 별개의 UI 전용 값 — 이 시간이 지나도 자동 종료하지 않고, 사용자가 직접 포기를 선택해야만 세션이 끝난다.
    /// </summary>
    private const float GIVE_UP_AVAILABLE_DELAY = 30f;

    /// <summary>상대가 현재 이탈(재접속 대기) 상태인지. OpponentGraceRoutine 완료 후에도 재접속 시 OnOpponentReconnected를 발행해야 하므로 별도 추적.</summary>
    private bool _opponentDisconnected;

    /// <summary>JoinRandomRoom 실패(빈 방 없음) 시 생성/입장할 고정 방 이름. 양쪽 클라이언트가 같은 이름을 써야 서로 만날 수 있음.</summary>
    private const string FALLBACK_ROOM_NAME = "PokeChessRoom";

    /// <summary>게임 씬 이름(OnRoomFull의 최초 로드, 재입장 복귀 모두 동일 씬을 가리켜야 함).</summary>
    private const string GAME_SCENE_NAME = "GameSceneTest";

    /// <summary>
    /// NetworkManager는 씬마다 새 인스턴스가 생성돼(DontDestroyOnLoad 아님) 재입장 성공 시점의 인스턴스와
    /// 게임 씬 로드 후의 인스턴스가 다르다. 재입장으로 인한 씬 전환이 예약돼 있다는 사실을 새 인스턴스에
    /// 전달하기 위한 정적 플래그(Start에서 소비 즉시 false로 리셋).
    /// </summary>
    private static bool s_resyncAfterRejoinPending;

    /// <summary>
    /// 재접속으로 인한 씬 재로드로 이번 게임 씬에 들어왔는지(1단계: 구분만 함, 실제 데이터 복원은 다음 단계).
    /// s_resyncAfterRejoinPending과 달리 NetworkManager 자신의 Start()에서 소비/리셋하지 않는다 —
    /// BoardManager/ShopManager/RewardManager 등 다른 컴포넌트가 각자의 Awake()에서 이 값을 읽어야 하는데,
    /// 서로 다른 컴포넌트의 Start() 실행 순서는 Unity가 보장하지 않으므로 NetworkManager.Start()가 먼저
    /// 이 값을 지워버리면 경쟁이 생긴다(2026-08 설계 검토). Set은 OnJoinedRoom(재접속 씬 이동 예약 시점),
    /// Clear는 OnJoinedRoom(재접속이 아닌 일반 입장 시점) — OnRoomFull()은 마스터 여부/MATCH_GUID 존재
    /// 여부에 따라 실행이 갈려 안전한 지점이 아니라 사용하지 않는다.
    /// </summary>
    private static bool s_isResumingRejoinedMatch;

    /// <summary>재접속 씬 재로드로 이번 게임 씬에 들어왔는지(읽기 전용). 다른 매니저는 Awake()에서 이 값을
    /// 로컬 필드로 캐싱해 써야 한다(Start() 시점엔 이미 아래 Clear 위치를 지났을 수 있음).</summary>
    public bool IsResumingRejoinedMatch => s_isResumingRejoinedMatch;

    // ─────────────────────────────────────────
    // 상태 프로퍼티 (읽기 전용)
    // ─────────────────────────────────────────

    public bool IsConnected   => PhotonNetwork.IsConnected;
    public bool IsInRoom      => PhotonNetwork.InRoom;
    public bool IsMasterClient => _soloMode || PhotonNetwork.IsMasterClient;
    public int  PlayerCount   => _soloMode ? 1 : PhotonNetwork.CurrentRoom?.PlayerCount ?? 0;
    public bool UsesSharedShopPool => !_soloMode && PhotonNetwork.InRoom;

    // 전적 기록(MatchRecorder) 등이 Photon 타입에 직접 의존하지 않도록 문자열로 노출.
    public string RoomName       => _soloMode ? "solo" : PhotonNetwork.CurrentRoom?.Name ?? "";
    public string LocalNickname  => _soloMode ? "SoloPlayer" : PhotonNetwork.NickName;

    public override void OnEnable()
    {
        base.OnEnable();

        GameEvents.OnPhaseChanged += HandlePhaseChanged;
        GameEvents.OnGoldTransferRequested += HandleGoldTransferRequested;
        GameEvents.OnPlayerReadyApproved += BroadcastPlayerReady;

        // 유닛 스냅샷 저장 트리거(1차 구현) — 새 이벤트를 만들지 않고 기존 이벤트를 재사용.
        GameEvents.OnUnitPlaced += HandleUnitSnapshotDirty;
        GameEvents.OnUnitBenched += HandleUnitSnapshotDirty;
        GameEvents.OnUnitSold += HandleUnitSnapshotDirty;
        GameEvents.OnUnitChanged += HandleUnitSnapshotDirty;
        GameEvents.OnInventoryChanged += HandleUnitSnapshotDirtyNoArg;
    }

    public override void OnDisable()
    {
        GameEvents.OnPhaseChanged -= HandlePhaseChanged;
        GameEvents.OnGoldTransferRequested -= HandleGoldTransferRequested;
        GameEvents.OnPlayerReadyApproved -= BroadcastPlayerReady;

        GameEvents.OnUnitPlaced -= HandleUnitSnapshotDirty;
        GameEvents.OnUnitBenched -= HandleUnitSnapshotDirty;
        GameEvents.OnUnitSold -= HandleUnitSnapshotDirty;
        GameEvents.OnUnitChanged -= HandleUnitSnapshotDirty;
        GameEvents.OnInventoryChanged -= HandleUnitSnapshotDirtyNoArg;

        base.OnDisable();
    }

    private void HandlePhaseChanged(GamePhase phase)
    {
        _isBattlePhase = phase == GamePhase.Battle;

        if (phase == GamePhase.Shopping)
        {
            ApplyPendingTradeEvolutions();
        }
    }

    /// <summary>
    /// 현재 판의 고유 ID(GUID). 협동에선 MasterClient가 방 잠금 시점에 Room 커스텀 속성으로
    /// 배포해 두 클라이언트가 같은 값을 갖는다 — 전적 matchId의 "방이름+분단위 시각" 방식이
    /// 분 경계에서 어긋날 수 있던 문제의 교체분(Phase 2). 솔로는 라운드 1 시작마다 재발급.
    /// 아직 미배포/미입장이면 ""(호출부가 폴백 처리).
    /// </summary>
    public string MatchGuid
    {
        get
        {
            if (_soloMode) return _soloMatchGuid;
            if (PhotonNetwork.InRoom &&
                PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(MATCH_GUID_ROOM_KEY, out object g))
                return g as string ?? "";
            return "";
        }
    }
    private string _soloMatchGuid = "";

    /// <summary>매치 진행 여부 판별 키(public — 로비 방 목록 표시(진행 중 태그)에서 RoomListUI가 읽음).</summary>
    public const string MATCH_GUID_ROOM_KEY = "MatchGuid";

    /// <summary>방장 표시 닉네임 Room 속성 키(public — 로비 방 목록 표시에서 RoomListUI가 읽음).
    /// 값 자체는 NetworkManager만 쓴다(생성 시/마스터 교체 시) — 다른 곳은 읽기 전용.</summary>
    public const string HOST_NICKNAME_PROP_KEY = "HostNickname";
    public string PartnerNickname
    {
        get
        {
            if (_soloMode || !PhotonNetwork.InRoom) return "";
            var others = PhotonNetwork.PlayerListOthers;
            return others != null && others.Length > 0 ? others[0].NickName : "";
        }
    }

    /// <summary>상대방 재접속 유예 타이머</summary>
    private Coroutine _opponentGraceRoutine;

    /// <summary>GracePeriodExpired 중복 발행 방지. 로컬 유예 코루틴(60초)과 Photon PlayerTtl(동일 60초) 만료로 인한
    /// OnPlayerLeftRoom(IsInactive=false) 재호출이 거의 동시에 도착해 같은 이탈 건을 두 번 발행할 수 있다.</summary>
    private bool _gracePeriodExpiredFired;

    /// <summary>본인 재접속 시도 타이머</summary>
    private Coroutine _selfReconnectRoutine;

    // ─────────────────────────────────────────
    // 연결
    // ─────────────────────────────────────────

    private void Awake()
    {
        // 자동 씬 동기화는 "연결 전에" 켜져 있어야 팔로워(비마스터)가 마스터의 LoadLevel을 따라온다.
        // 다른 컴포넌트(타이틀 화면의 TitleScreenUI)가 Start에서 Connect()를 부를 수 있어
        // Start 순서 경합을 피하려고 모든 Start보다 먼저인 Awake에서 켠다.
        if (!_soloMode) PhotonNetwork.AutomaticallySyncScene = true;

        // 재입장 복귀로 로드된 게임 씬 — 내가 마스터를 재획득해도 라운드 1을 다시 시작하면 안 된다.
        // 다른 매니저의 Start()(예: GameSceneBootstrap.NotifySceneReady)와의 순서 경합을 피하려고
        // 모든 Start보다 먼저인 Awake에서 즉시 반영한다(실제 재동기화 호출은 Start에서, 다른 매니저의
        // OnEnable 구독이 끝난 뒤에 함).
        if (s_resyncAfterRejoinPending)
            _gameStarted = true;

        // AuthValues(UserId) 초기화도 같은 이유로 Awake에서 먼저 끝내야 한다. 예전엔 Start()에서
        // 했는데, TitleScreenUI.Start()의 Connect()가 이 NetworkManager.Start()보다 먼저 실행되면
        // PhotonNetwork.IsConnected가 이미 true가 되어버려 저장 UserId 복원 분기가 스킵되고,
        // Photon 서버가 새 UserId를 발급해버렸다(2026-08 확인 — 저장된 UserId와 Rejoin 시점 UserId가
        // 달라 재입장이 실패하는 근본 원인이었음. 로그: 저장 UserId != Rejoin 직전 MyUserId).
        EnsureAuthIdentity();
    }

    /// <summary>
    /// AuthValues(UserId)/닉네임을 Connect() 이전에 확정한다. Awake()에서만 호출 — Start 순서
    /// 경합 회피 이유는 Awake() 주석 참고. 내용 자체는 예전 Start()에 있던 것과 동일하다(로직 변경 없음).
    /// </summary>
    private void EnsureAuthIdentity()
    {
        if (_soloMode) return;

        // 씬 로컬이라 GameManager는 씬마다 새로 생기지만, 이미 연결돼 있으면(로비→게임 전환 후)
        // 닉네임/인증값을 다시 설정하지 않는다(재접속 식별자 보존). 씬 동기화는 위에서 이미 켬.
        if (PhotonNetwork.IsConnected)
        {
            // AuthValues/닉네임은 건드리지 않지만(연결된 세션의 정체성 보존), 저장된 재접속 세션의
            // 표시 상태(_pendingRejoinRoomName/_savedNickname)는 다시 로드한다. 방만 나가 타이틀로
            // 돌아온 경우(Photon 연결 자체는 끊기지 않음) 이 값들이 새 씬의 새 인스턴스에서 전혀
            // 채워지지 않던 문제(2026-08 확인) 수정 — 이 분기 자체를 타는 것과는 무관하게 필요하다.
            ReloadSavedRejoinSessionDisplayState();
            return;
        }

        // 실제 로비에서 닉네임이 전달되지 않은 테스트 상황을 대비한 임시값.
        // 이미 로그인/로비 UI에서 닉네임을 설정했다면 해당 값을 덮어쓰지 않는다.
        if (string.IsNullOrWhiteSpace(PhotonNetwork.NickName))
            PhotonNetwork.NickName = $"Player_{System.Guid.NewGuid().ToString()[..4]}";

        // ReconnectAndRejoin/RejoinRoom이 같은 플레이어로 인식하려면 재연결 시에도 동일한 UserId가 필요함.
        // AuthValues를 미리 고정해두지 않으면 재접속 시 새 UserId가 발급되어
        // "User does not exist in this game" 오류로 입장이 거부됨.
        //
        // 프로세스가 완전히 재시작된 경우(강제 종료 후 재실행 등)에도 같은 자리로 재입장을 시도할 수 있도록,
        // 이전 세션에서 저장해 둔 UserId/RoomName이 있으면 새로 발급하지 않고 그대로 재사용한다.
        string savedUserId = ReloadSavedRejoinSessionDisplayState();

        string userId = !string.IsNullOrEmpty(savedUserId)
            ? savedUserId
            : System.Guid.NewGuid().ToString();

        PhotonNetwork.AuthValues = new AuthenticationValues(userId);

        Debug.Log($"[Network][Rejoin] AuthValues.UserId 설정: {ShortUserId(userId)}... (저장값 재사용: {!string.IsNullOrEmpty(savedUserId)})");
    }

    private void Start()
    {
        // 진단 로그(2026-08, 재접속 복원 미실행 문제 추적용) — 로직 변경 없음. 어느 분기가 실제로
        // 실행되는지(재접속 복원이 왜 안 도는지) 다음 테스트에서 바로 확정하기 위함.
        Debug.Log($"[Network][Rejoin][Diag] Start() 진입 — _soloMode={_soloMode}, " +
                  $"PhotonNetwork.IsConnected={PhotonNetwork.IsConnected}, " +
                  $"s_resyncAfterRejoinPending={s_resyncAfterRejoinPending}, " +
                  $"s_isResumingRejoinedMatch={s_isResumingRejoinedMatch}");

        if (_soloMode)
        {
            Debug.LogWarning("[Network] 솔로 모드 — Photon 미사용, 즉시 라운드 1 시작");
            BroadcastRoundStart(1);
            return;
        }

        // AuthValues(UserId)/닉네임 초기화는 Awake()의 EnsureAuthIdentity()가 이미 끝냈다(Start 순서
        // 경합 회피 — Awake() 주석 참고). 여기 남는 건 다른 매니저의 OnEnable 구독이 끝난 뒤에만
        // 실행해야 하는, 실제로 Start 타이밍이 필요한 재접속 복원 로직뿐이다.
        if (PhotonNetwork.IsConnected)
        {
            // 이 인스턴스가 이미 라운드가 진행 중인 방에서 생성됐다면(s_resyncAfterRejoinPending이 세팅된
            // 정상 재접속 경로가 아니더라도 — 예: PUN AutomaticallySyncScene을 타고 수동적으로 씬이
            // 재생성된 경우) 신규 매치 시작으로 오판하지 않도록 _gameStarted를 미리 true로 간주한다.
            // 정말 신규 매치라면 Room에 ROUND_PROP_KEY가 아직 없으므로 이 체크는 아무 영향이 없다.
            // (OnPlayerPropertiesUpdate의 HasActiveRoundInRoom 방어와는 책임이 다르다 — 여기는 이 인스턴스의
            // _gameStarted 자체를 최대한 이른 시점에 정확히 복원하는 초기화, 그쪽은 그 복원이 어떤 이유로든
            // 누락됐을 때의 최종 방어선이다.)
            if (!_gameStarted && HasActiveRoundInRoom())
                _gameStarted = true;

            // 재입장 성공 후 게임 씬으로 명시적으로 복귀한 경우 — 이 씬의 다른 매니저들이 Awake/OnEnable로
            // GameEvents 구독을 마친 뒤(Start 시점엔 항상 보장됨) 예약된 재동기화를 실행한다.
            if (s_resyncAfterRejoinPending)
            {
                s_resyncAfterRejoinPending = false;
                Debug.Log("[Network][Rejoin] 게임 씬 로드 완료 — 예약된 재동기화 실행");
                ResyncAfterReconnect();
            }
        }
    }

    /// <summary>
    /// PlayerPrefs에 저장된 재접속 세션의 표시 상태(_pendingRejoinRoomName/_savedNickname)를 다시 읽어
    /// 반영한다. AuthValues는 여기서 건드리지 않는다 — Photon 연결이 유지된 채(방만 나가 타이틀로 돌아온
    /// 경우) 호출해도 안전하도록 EnsureAuthIdentity()(Awake에서 호출)의 "연결됨" 분기와 "신규 연결" 분기
    /// 양쪽에서 공용으로 쓴다. 반환값(저장된 UserId)은 신규 연결 분기에서만 AuthValues 발급에 사용된다.
    /// </summary>
    private string ReloadSavedRejoinSessionDisplayState()
    {
        string savedUserId = PlayerPrefs.GetString(PrefKey(PREF_LAST_USER_ID), "");
        string savedRoomName = PlayerPrefs.GetString(PrefKey(PREF_LAST_ROOM_NAME), "");
        string savedNickname = PlayerPrefs.GetString(PrefKey(PREF_LAST_NICKNAME), "");

        Debug.Log($"[Network][Rejoin] 저장값 읽음: UserId={ShortUserId(savedUserId)}..., RoomName='{savedRoomName}', Nickname='{savedNickname}'");

        // 표시용 닉네임은 재접속 세션(UserId+RoomName) 유무와 무관하게 항상 반영한다 —
        // 정상 타이틀 이동으로 재접속 세션은 삭제돼도 "마지막으로 쓴 닉네임"은 남아 있어야 하기 때문(2026-08).
        _savedNickname = savedNickname;

        if (!string.IsNullOrEmpty(savedUserId) && !string.IsNullOrEmpty(savedRoomName))
        {
            // 2026-08: 여기서 자동으로 재입장하지 않는다 — 타이틀 화면이 선택지를 보여주고
            // 사용자가 명시적으로 AttemptRejoinSavedSession()/AbandonPreviousSession()을 호출해야 한다.
            _pendingRejoinRoomName = savedRoomName;
            Debug.Log($"[Network][Rejoin] 이전 세션 발견(자동 재입장 없음) — 타이틀 화면에서 사용자 선택 대기: {savedRoomName}");
        }
        else
        {
            Debug.Log("[Network][Rejoin] 저장된 세션 없음 — 신규 로그인 흐름으로 진행");
        }

        return savedUserId;
    }

    /// <summary>타이틀 화면에 저장된 이전 세션(재입장 후보)이 있는지. 있어도 자동으로 재입장하지 않는다.</summary>
    public bool HasSavedSession => !string.IsNullOrEmpty(_pendingRejoinRoomName);

    /// <summary>표시 전용 이전 닉네임("이전 닉네임: ○○"). 재접속 인증에는 쓰지 않는다.</summary>
    public string SavedNickname => _savedNickname ?? "";

    /// <summary>AttemptRejoinSavedSession()/AbandonPreviousSession() 요청이 진행 중인지. 타이틀 UI가 버튼 잠금에 쓴다.</summary>
    public bool IsRejoining => _rejoinPurpose != RejoinPurpose.None;

    /// <summary>파트너가 재접속을 포기했다는 통지를 받았는지. OptionsPanelUI가 폴링.</summary>
    public bool PartnerGaveUpReconnect => _partnerGaveUpReconnectNotice;

    /// <summary>파트너 연결 끊김으로 재접속을 기다리는 중인지(무한 대기 포함, 30초 유예와 무관하게 true).
    /// ShopManager/ItemManager/UnitDragController 등 입력 처리부가 공통으로 체크해 대기 중 조작을 막는다(2026-08).</summary>
    public bool IsAwaitingPartnerReconnect => _opponentDisconnected;

    /// <summary>디버그 로그에 UserId 전체를 남기지 않기 위한 축약 표시(앞 8자).</summary>
    private static string ShortUserId(string userId) =>
        string.IsNullOrEmpty(userId) ? "(없음)" : userId[..Mathf.Min(8, userId.Length)];

    /// <summary>
    /// 재입장 실패 원인 추적용(2026-08). Editor/Build 간 방·계정 식별자가 실제로 같은지, IsOpen/AutoSync가
    /// 어떤 시점에 어떤 값인지를 로그로 비교하기 위한 진단 덤프. AttemptRejoinSavedSession 직전,
    /// OnJoinedRoom/OnJoinRoomFailed 진입 시 호출한다.
    /// </summary>
    private string DumpNetworkDiagnostics(string tag)
    {
        var room = PhotonNetwork.CurrentRoom;
        var local = PhotonNetwork.LocalPlayer;
        var master = PhotonNetwork.MasterClient;
        return $"[Network][Diag:{tag}] AppVersion={PhotonNetwork.AppVersion} Region={PhotonNetwork.CloudRegion} " +
               $"Room={(room != null ? room.Name : "(none)")} PlayerCount={(room != null ? room.PlayerCount : 0)}/{MAX_PLAYERS} " +
               $"IsOpen={(room != null ? room.IsOpen.ToString() : "n/a")} " +
               $"MyActor={(local != null ? local.ActorNumber : -1)} MyUserId={ShortUserId(PhotonNetwork.AuthValues?.UserId)}... " +
               $"MasterActor={(master != null ? master.ActorNumber : -1)} AutoSync={PhotonNetwork.AutomaticallySyncScene} " +
               $"Scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} " +
               $"RejoinPurpose={_rejoinPurpose} ResyncPending={s_resyncAfterRejoinPending} " +
               $"IsResumingMatch={s_isResumingRejoinedMatch}";
    }

    /// <summary>방 입장(신규/재입장 모두)에 성공할 때마다 UserId/RoomName/닉네임을 최신 상태로 저장한다.</summary>
    private void SaveRejoinSession()
    {
        string userId = PhotonNetwork.AuthValues.UserId;
        string roomName = PhotonNetwork.CurrentRoom.Name;
        string nickname = PhotonNetwork.NickName;

        PlayerPrefs.SetString(PrefKey(PREF_LAST_USER_ID), userId);
        PlayerPrefs.SetString(PrefKey(PREF_LAST_ROOM_NAME), roomName);
        PlayerPrefs.SetString(PrefKey(PREF_LAST_NICKNAME), nickname);
        PlayerPrefs.Save();

        Debug.Log($"[Network][Rejoin] 세션 저장: UserId={ShortUserId(userId)}..., RoomName={roomName}, Nickname={nickname}");
    }

    /// <summary>의도적 퇴장, 재입장 실패, PlayerTtl 만료, 재접속 포기 확정 등으로 더 이상 유효하지 않은
    /// 재접속 인증 정보(UserId+RoomName)만 정리한다. 표시용 닉네임(PREF_LAST_NICKNAME/_savedNickname)은
    /// 재접속 인증과 무관한 별개 값이라 여기서 함께 지우지 않는다(2026-08 — 정상 타이틀 이동 후에도
    /// 닉네임 입력칸 기본값으로 계속 써야 하므로). HasSavedSession(재입장 가능 여부)은
    /// _pendingRejoinRoomName 기준이라 이 정리로 정확히 갱신된다.</summary>
    private void ClearSavedRejoinSession(string reason)
    {
        PlayerPrefs.DeleteKey(PrefKey(PREF_LAST_USER_ID));
        PlayerPrefs.DeleteKey(PrefKey(PREF_LAST_ROOM_NAME));
        PlayerPrefs.Save();

        _pendingRejoinRoomName = null;

        Debug.Log($"[Network][Rejoin] 저장된 세션 정리 (원인: {reason})");
    }

    private Coroutine _connectTimeoutRoutine;

    /// <summary>
    /// 로그인 또는 로비 UI에서 입력받은 닉네임을
    /// 현재 Photon 로컬 플레이어의 닉네임으로 적용한다.
    ///
    /// 설정된 닉네임은 방 입장 후 다른 플레이어에게 공유되며,
    /// MatchRecorder가 LocalNickname / PartnerNickname을 통해
    /// 전적 기록에 자동으로 저장한다.
    /// </summary>
    /// <param name="nickname">사용자가 입력한 닉네임</param>
    /// <returns>닉네임 적용에 성공하면 true, 적용할 수 없으면 false</returns>
    public bool TrySetLocalNickname(string nickname)
    {
        // 솔로 모드는 Photon 플레이어 정보를 사용하지 않는다.
        if (_soloMode)
        {
            Debug.LogWarning("[Network] 솔로 모드에서는 Photon 닉네임을 사용하지 않습니다.");
            return false;
        }

        // 현재 UI에는 방 입장 후 닉네임 변경 경로가 없지만,
        // 다른 스크립트에서 이 공개 메서드를 잘못 호출하는 경우를 방지한다.
        // 한 판 도중 닉네임이 바뀌어 파트너 표시와 전적 기록이 어긋나지 않도록 차단한다.
        if (PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[Network] 방 입장 후에는 닉네임을 변경할 수 없습니다.");
            return false;
        }

        // 입력 앞뒤의 불필요한 공백을 제거한다.
        string trimmedNickname = nickname?.Trim();

        // 공백 또는 빈 문자열은 유효한 닉네임으로 인정하지 않는다.
        if (string.IsNullOrEmpty(trimmedNickname))
        {
            Debug.LogWarning("[Network] 닉네임이 비어 있습니다.");
            return false;
        }

        // 로비 UI와 네트워크 데이터의 닉네임 길이를 일정하게 제한한다.
        if (trimmedNickname.Length > MAX_NICKNAME_LENGTH)
            trimmedNickname = trimmedNickname[..MAX_NICKNAME_LENGTH];

        // Photon 플레이어 정보에 적용.
        // 방에 들어가면 파트너가 Player.NickName으로 이 값을 조회할 수 있다.
        PhotonNetwork.NickName = trimmedNickname;

        Debug.Log($"[Network] 닉네임 설정 완료: {PhotonNetwork.NickName}");
        return true;
    }

    public void Connect()
    {
        if (_soloMode) { Debug.Log("[Network] 솔로 모드 — 연결 생략"); return; }
        if (PhotonNetwork.IsConnected) return;
        Debug.Log("[Network] Photon 서버 연결 시도...");
        PhotonNetwork.ConnectUsingSettings();

        if (_connectTimeoutRoutine != null)
            StopCoroutine(_connectTimeoutRoutine);
        _connectTimeoutRoutine = StartCoroutine(ConnectTimeoutRoutine());
    }

    private System.Collections.IEnumerator ConnectTimeoutRoutine()
    {
        yield return new WaitForSeconds(CONNECT_TIMEOUT);

        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Debug.LogWarning($"[Network] 연결 타임아웃 ({CONNECT_TIMEOUT}s) — 연결 시도 중단");
            PhotonNetwork.Disconnect();
        }

        _connectTimeoutRoutine = null;
    }

    public void Disconnect()
    {
        if (!PhotonNetwork.IsConnected) return;
        PhotonNetwork.Disconnect();
    }

    // ─────────────────────────────────────────
    // 룸 관리
    // ─────────────────────────────────────────

    /// <summary>
    /// 타이틀 화면 QA용 방 목록 새로고침. 로비 상태가 아니면 JoinLobby, 이미 로비라면 LeaveLobby 후
    /// 재입장해 서버로부터 방 목록을 다시 받는다(OnRoomListUpdate로 갱신). 방 생성/입장/재접속 흐름과는
    /// 무관한 별도 기능이며, JoinRoom 등 입장 관련 호출은 하지 않는다.
    /// </summary>
    public void RefreshRoomList()
    {
        if (_soloMode || !PhotonNetwork.IsConnectedAndReady) return;

        Debug.Log($"[RoomList] RefreshRoomList 호출 — InLobby={PhotonNetwork.InLobby}, InRoom={PhotonNetwork.InRoom}");

        if (!PhotonNetwork.InLobby)
            PhotonNetwork.JoinLobby();
        else
        {
            PhotonNetwork.LeaveLobby();
            PhotonNetwork.JoinLobby();
        }
    }

    public void CreateRoom(string roomName)
    {
        var options = new RoomOptions
        {
            MaxPlayers = MAX_PLAYERS,
            IsVisible = true,
            PlayerTtl = (int)(RECONNECT_GRACE_PERIOD * 1000),
            // HostNickname/MatchGuid를 로비 방 목록(RoomInfo.CustomProperties)에도 노출 — 방장 닉네임 표시,
            // "진행 중" 태그 표시에 씀(둘 다 표시 전용, 입장 가능 여부 판정에는 안 씀).
            CustomRoomPropertiesForLobby = new[] { HOST_NICKNAME_PROP_KEY, MATCH_GUID_ROOM_KEY },
            CustomRoomProperties = new Hashtable { { HOST_NICKNAME_PROP_KEY, PhotonNetwork.NickName } }
        };
        PhotonNetwork.CreateRoom(roomName, options);
    }

    public void JoinRoom(string roomName)
    {
        PhotonNetwork.JoinRoom(roomName);
    }

    public void JoinOrCreateRoom(string roomName)
    {
        // 이 RoomOptions/CustomProperties는 방이 실제로 새로 생성될 때만 적용된다(Photon 사양 —
        // 이미 있는 방에 입장할 땐 무시됨). 기존 방의 HostNickname을 덮어쓰지 않음.
        var options = new RoomOptions
        {
            MaxPlayers = MAX_PLAYERS,
            PlayerTtl = (int)(RECONNECT_GRACE_PERIOD * 1000),
            CustomRoomPropertiesForLobby = new[] { HOST_NICKNAME_PROP_KEY, MATCH_GUID_ROOM_KEY },
            CustomRoomProperties = new Hashtable { { HOST_NICKNAME_PROP_KEY, PhotonNetwork.NickName } }
        };
        PhotonNetwork.JoinOrCreateRoom(roomName, options, TypedLobby.Default);
    }

    /// <summary>빈 자리 있는 방 매칭. 없으면 OnJoinRandomFailed에서 새 방 생성.</summary>
    public void JoinRandomRoom()
    {
        PhotonNetwork.JoinRandomRoom();
    }

    /// <summary>
    /// 방을 나간다. 저장된 재접속 세션(UserId+RoomName)은 여기서 지우지 않는다 — 정상 타이틀 이동
    /// (RequestReturnToTitle)도 내부적으로 이 메서드를 쓰는데, "이전 게임으로 들어가기" 기능 자체가
    /// 정상적으로 타이틀로 나간 뒤에도 나중에 복귀할 수 있어야 성립하기 때문이다(2026-08 확인된 회귀 —
    /// 예전엔 여기서 항상 지워서 타이틀 이동 직후 재접속 세션이 사라져 있었다). 세션을 실제로 지워야
    /// 하는 경우(새로 시작하기 확정 등)는 호출부가 ClearSavedRejoinSession을 직접 부른다.
    /// </summary>
    public void LeaveRoom()
    {
        // Leaving 상태 동안 새 SetCustomProperties 호출이 나가면 서버가 거부하며 오류를 남긴다 — 완료(OnLeftRoom/
        // OnDisconnected)까지 차단한다.
        _isLeavingRoom = true;

        PhotonNetwork.LeaveRoom();
    }

    /// <summary>
    /// 옵션창 "타이틀로" 확인 시 OptionsPanelUI가 호출(정상 종료·파트너 이탈 포기 공용).
    /// 방에 있으면 LeaveRoom을 요청만 하고, Photon이 실제로 이탈을 완료(OnLeftRoom, 드물게 OnDisconnected)한
    /// 뒤에야 씬을 전환한다. LeaveRoom 요청과 씬 전환을 같은 프레임에 연달아 하면 아직 "Leaving" 확인이 끝나기
    /// 전에 씬(및 그 안의 PhotonView)이 파괴되어 SetProperties 오류가 나는 문제(2026-08)가 있어 반드시 이 순서를 지킨다.
    /// </summary>
    public void RequestReturnToTitle(string titleSceneName)
    {
        if (string.IsNullOrWhiteSpace(titleSceneName))
            return;

        if (_soloMode || !PhotonNetwork.InRoom)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(titleSceneName);
            return;
        }

        // 타이틀 이동은 이 클라이언트 개인의 씬 전환이어야 한다. AutomaticallySyncScene이 켜진 채로
        // 씬을 바꾸면(특히 내가 마스터 클라이언트일 때) PUN이 이를 룸 전체에 동기화해 방에 남아 있는
        // 파트너까지 같은 씬으로 끌고 가버린다(2026-08 확인된 회귀). 완료까지 잠깐 꺼둔다 —
        // 복원은 NetworkTest에서 새로 생성되는 NetworkManager.Awake()가 항상 true로 되돌리므로 별도 처리 불필요.
        PhotonNetwork.AutomaticallySyncScene = false;

        _pendingTitleSceneName = titleSceneName;
        LeaveRoom();
    }

    /// <summary>
    /// 정상적으로 종료된 매치(항복 합의/패배 확인 등)에서 타이틀로 복귀할 때 전용으로 쓰는 진입점.
    /// 저장된 재접속 세션(ClearSavedRejoinSession)을 먼저 정리한 뒤 기존 안전 경로
    /// (RequestReturnToTitle → LeaveRoom → OnLeftRoom → TryLoadPendingTitleScene)를 그대로 탄다.
    /// 세션을 지우지 않으면 이미 끝난 매치가 타이틀 화면에 "이전 게임이 정상적으로 종료되지
    /// 않았습니다" 오탐으로 남아 [이전 게임으로 들어가기] 후보가 되어버린다(2026-08 확인).
    /// 일반 타이틀 이동(솔로/1인 방에서 게임 도중 나가기 등)은 세션이 여전히 유효하므로
    /// 이 메서드가 아니라 RequestReturnToTitle을 그대로 쓴다.
    /// </summary>
    public void RequestCompletedMatchReturnToTitle(string sceneName, string reason)
    {
        ClearSavedRejoinSession(reason);
        RequestReturnToTitle(sceneName);
    }

    /// <summary>
    /// 타이틀 화면 [이전 게임으로 들어가기] 클릭 시 호출. 저장된 UserId(Start()에서 이미 AuthValues에 반영됨)로
    /// 저장된 RoomName에 재입장을 시도한다. 결과는 OnJoinedRoom(성공)/OnJoinRoomFailed(실패)에서 처리된다.
    /// </summary>
    public void AttemptRejoinSavedSession()
    {
        if (!HasSavedSession || IsRejoining) return;

        RejoinFailed = false;
        _rejoinPurpose = RejoinPurpose.EnterGame;

        // AutomaticallySyncScene이 켜진 채로 RejoinRoom에 성공하면, PUN이 룸에 기록된 "현재 씬"
        // (OnRoomFull이 매치 시작 시 PhotonNetwork.LoadLevel로 세팅해둔 것)을 감지해 이 커스텀 흐름과
        // 무관하게 자체적으로 GameSceneTest를 먼저 불러올 수 있다 — 그러면 s_resyncAfterRejoinPending/
        // s_isResumingRejoinedMatch가 세팅되기 전에 씬이 바뀌어 재동기화가 누락된다(2026-08 확인).
        // AbandonPreviousSession()이 이미 같은 이유로 꺼두는 것과 동일한 조치. 복원은 성공 시
        // OnJoinedRoom()이 직접 씬을 불러온 뒤 그 씬의 새 NetworkManager.Awake()가 되돌리고,
        // 실패 시(전송 실패/OnJoinRoomFailed)는 각각 아래에서 명시적으로 되돌린다.
        PhotonNetwork.AutomaticallySyncScene = false;

        Debug.Log(DumpNetworkDiagnostics("AttemptRejoinSavedSession/Before"));
        bool sent = PhotonNetwork.RejoinRoom(_pendingRejoinRoomName);
        Debug.Log($"[Network][Rejoin] 이전 게임 재입장 요청 전송 여부: {sent}");

        if (!sent)
        {
            _rejoinPurpose = RejoinPurpose.None;
            RejoinFailed = true;
            PhotonNetwork.AutomaticallySyncScene = true;
        }
    }

    /// <summary>[이전 게임에 접속할 수 없습니다] 팝업의 [확인] 클릭 시 호출. 저장된 세션을 정리하고 일반 로그인으로 전환한다.</summary>
    public void AcknowledgeRejoinFailure()
    {
        RejoinFailed = false;
        ClearSavedRejoinSession("재입장 실패 확인");
    }

    /// <summary>
    /// 타이틀 화면 [새로 시작하기] 확정 시 호출. 저장된 방에 잠깐 재입장해 기다리고 있을 파트너에게
    /// "재접속 포기" RPC를 전달한 뒤 곧바로 다시 나간다. AutomaticallySyncScene이 켜져 있으면 이 잠깐의
    /// 재입장만으로 GameSceneTest로 끌려갈 수 있어 통지가 끝날 때까지 임시로 꺼둔다.
    /// </summary>
    public void AbandonPreviousSession()
    {
        if (!HasSavedSession || IsRejoining) return;

        _rejoinPurpose = RejoinPurpose.NotifyAbandonAndLeave;
        PhotonNetwork.AutomaticallySyncScene = false;

        bool sent = PhotonNetwork.RejoinRoom(_pendingRejoinRoomName);
        Debug.Log($"[Network][Rejoin] 포기 통지용 재입장 요청 전송 여부: {sent}");

        if (!sent)
        {
            // 알릴 상대에게 접속 자체가 안 됨 — 상대도 이미 나간 것으로 보고 세션만 정리한다.
            _rejoinPurpose = RejoinPurpose.None;
            PhotonNetwork.AutomaticallySyncScene = true;
            ClearSavedRejoinSession("재접속 포기(재입장 요청 전송 실패)");
        }
    }

    /// <summary>파트너의 [상대방이 재접속을 포기했습니다] 안내 [확인] 클릭 시 OptionsPanelUI가 호출.</summary>
    public void AcknowledgePartnerGaveUpReconnect() => _partnerGaveUpReconnectNotice = false;

    /// <summary>LeaveRoom() 완료 시 예약된 타이틀 씬이 있으면 지금 로드한다.</summary>
    private void TryLoadPendingTitleScene()
    {
        if (string.IsNullOrEmpty(_pendingTitleSceneName))
            return;

        string sceneName = _pendingTitleSceneName;
        _pendingTitleSceneName = null;
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }

    public override void OnLeftRoom()
    {
        Debug.Log("[Network] 룸 이탈 완료");
        _isLeavingRoom = false;

        if (!string.IsNullOrEmpty(_pendingTitleSceneName))
        {
            TryLoadPendingTitleScene();
            return;
        }

        // 씬 전환이 필요 없는 이탈(예: 재접속 포기 통지 후 재이탈) — 타이틀 화면이 방 목록을
        // 계속 받을 수 있도록 로비로 돌아간다.
        PhotonNetwork.JoinLobby();
    }

    /// <summary>
    /// 파트너 이탈 대기 중 [포기하기]→[타이틀로 이동]/[게임 종료] 선택 시 OptionsPanelUI가 호출.
    /// 그 이전(대기/포기 버튼 노출)까지는 절대 발행되지 않던 SessionEnded를 이 시점에만 발행해
    /// 기존 일반 패배 처리(RoundPhaseManager GameOver 전환, MatchRecorder 전적 기록)를 그대로 재사용한다.
    /// </summary>
    public void ConfirmPartnerDisconnectGiveUp()
    {
        // 대기 화면은 같지만 사유는 구분해서 전적에 남긴다 — 진짜 이탈(PartnerAbandoned)과
        // "연결은 살아있는데 결과 응답만 없어서 포기"(PartnerResultUnresponsive)는 다른 상황이다
        // (2026-08-22 코드리뷰 지적 — 하나로 뭉뚱그리면 일시적 응답 지연이 파트너가 진짜로
        // 게임을 버렸다고 잘못 기록된다).
        SessionEndReason reason = _partnerResultUnresponsive
            ? SessionEndReason.PartnerResultUnresponsive
            : SessionEndReason.PartnerAbandoned;
        GameEvents.SessionEnded(reason);
    }

    // ─────────────────────────────────────────
    // 라운드 동기화 (MasterClient 전용)
    // ─────────────────────────────────────────

    /// <summary>MasterClient가 다음 라운드 시작을 전체에 알림</summary>
    public void BroadcastRoundStart(int round)
    {
        if (_soloMode)
        {
            // 솔로: 새 판(라운드 1)마다 matchId용 GUID 재발급
            if (round == 1) _soloMatchGuid = System.Guid.NewGuid().ToString("N");
            GameEvents.RoundChanged(round);
            return;
        }

        if (!IsMasterClient) return;
        // 재접속 클라이언트의 라운드 복구용으로 Room 속성에도 기록(RPC는 비접속자에게 유실됨).
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { ROUND_PROP_KEY, round } });
        photonView.RPC(nameof(RPC_OnRoundStart), RpcTarget.All, round);
    }

    /// <summary>MasterClient가 전투 시작을 전체에 알림</summary>
    public void BroadcastBattleStart()
    {
        if (_soloMode) { GameEvents.BattleStart(); return; }

        if (!IsMasterClient) return;
        photonView.RPC(nameof(RPC_OnBattleStart), RpcTarget.All);
    }

    /// <summary>MasterClient가 챕터 완주(최종 라운드 클리어)를 전체에 알림. 다음 라운드 대신 호출.</summary>
    public void BroadcastGameCleared()
    {
        if (_soloMode) { GameEvents.GameCleared(); return; }

        if (!IsMasterClient) return;
        photonView.RPC(nameof(RPC_OnGameCleared), RpcTarget.All);
    }

    /// <summary>쇼핑 페이즈에서 "준비 완료" 버튼 누를 때 호출. 자신의 준비 상태를 CustomProperties에 기록.</summary>
    public void BroadcastPlayerReady()
    {
        // 솔로 모드: 1인 = 전원 준비 완료
        if (_soloMode) { GameEvents.ReadyCountChanged(1, 1); GameEvents.AllPlayersReady(); return; }
        if (_isLeavingRoom) return; // Leaving 중 SetProperties 금지

        var props = new Hashtable { { READY_PROP_KEY, true } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    // ─────────────────────────────────────────
    // 공유 챔피언 풀 / 상점 예약 (MasterClient 권위)
    // ─────────────────────────────────────────

    /// <summary>
    /// excludedBaseIds: 요청자(나) 자신의 벤치+필드를 스캔해 ShopManager가 계산한 3성 보유 진화 라인의
    /// 기본종 id 배열("3성 보유 유닛 상점 노출 제외" 기능). 이 메서드는 그 배열을 그대로 실어 보낼 뿐,
    /// NetworkManager는 BoardManager를 조회하거나 ShopManager의 계산 로직을 대신 수행하지 않는다.
    /// null이면 System.Array.Empty로 정규화해 RPC에 null이 실리지 않게 한다.
    /// </summary>
    public bool RequestSharedShopRoll(int level, bool forceCostFour, bool onlyCostFour, int[] excludedBaseIds)
    {
        if (!UsesSharedShopPool) return false;

        excludedBaseIds ??= System.Array.Empty<int>();

        if (IsMasterClient)
            ProcessSharedShopRoll(PhotonNetwork.LocalPlayer.ActorNumber, level, forceCostFour, onlyCostFour, excludedBaseIds);
        else
            photonView.RPC(nameof(RPC_RequestSharedShopRoll), RpcTarget.MasterClient, level, forceCostFour, onlyCostFour, excludedBaseIds);
        return true;
    }

    /// <summary>
    /// 재접속 복원 전용 — 새로 굴리지 않고, 마스터에게 남아있는 내 기존 상점 예약을 그대로 돌려달라고
    /// 요청한다. 예약이 없으면(마스터 교체 등) 새 상점으로 대체하지 않고 로그만 남기고 끝낸다.
    /// </summary>
    public bool RequestSharedShopRestore()
    {
        if (!UsesSharedShopPool) return false;
        if (IsMasterClient)
            ProcessSharedShopRestore(PhotonNetwork.LocalPlayer.ActorNumber);
        else
            photonView.RPC(nameof(RPC_RequestSharedShopRestore), RpcTarget.MasterClient);
        return true;
    }

    public bool RequestSharedShopPurchase(int revision, int slot)
    {
        if (!UsesSharedShopPool) return false;
        if (IsMasterClient)
            ProcessSharedShopPurchase(PhotonNetwork.LocalPlayer.ActorNumber, revision, slot);
        else
            photonView.RPC(nameof(RPC_RequestSharedShopPurchase), RpcTarget.MasterClient, revision, slot);
        return true;
    }

    /// <summary>
    /// 구매 합체 예외(2슬롯 동시 구매)를 하나의 요청으로 보낸다.
    /// 마스터 클라이언트가 두 슬롯을 모두 검증한 뒤 동시에 승인/거절한다(부분 성공 없음).
    /// </summary>
    public bool RequestSharedShopMergePurchase(int revision, int slotA, int slotB)
    {
        if (!UsesSharedShopPool) return false;
        if (IsMasterClient)
            ProcessSharedShopMergePurchase(PhotonNetwork.LocalPlayer.ActorNumber, revision, slotA, slotB);
        else
            photonView.RPC(nameof(RPC_RequestSharedShopMergePurchase), RpcTarget.MasterClient, revision, slotA, slotB);
        return true;
    }

    public void RequestSharedShopReturn(int pokemonId, int amount)
    {
        if (!UsesSharedShopPool || pokemonId <= 0 || amount <= 0) return;
        if (IsMasterClient) ProcessSharedShopReturn(pokemonId, amount);
        else photonView.RPC(nameof(RPC_RequestSharedShopReturn), RpcTarget.MasterClient, pokemonId, amount);
    }

    /// <summary>
    /// QA 패널에서 지정한 코스트의 유닛 1마리를
    /// 현재 남아 있는 공용 풀에서 실제로 차감해 요청한다.
    ///
    /// 상점 슬롯 예약과는 무관하며 골드는 소비하지 않는다.
    /// 실제 유닛 선택과 공용 풀 차감은 MasterClient가 담당한다.
    /// </summary>
    public bool RequestSharedDebugUnitByCost(int cost)
    {
        if (!UsesSharedShopPool)
            return false;

        if (cost < 1 || cost > 5)
        {
            Debug.LogWarning(
                $"[SharedShopPool][QA] 잘못된 코스트 요청: {cost}"
            );

            return false;
        }

        int actorNumber =
            PhotonNetwork.LocalPlayer != null
                ? PhotonNetwork.LocalPlayer.ActorNumber
                : 0;

        if (actorNumber <= 0)
            return false;

        if (IsMasterClient)
        {
            ProcessSharedDebugUnitByCost(
                actorNumber,
                cost
            );
        }
        else
        {
            photonView.RPC(
                nameof(RPC_RequestSharedDebugUnitByCost),
                RpcTarget.MasterClient,
                cost
            );
        }

        return true;
    }

    [PunRPC]
    private void RPC_RequestSharedShopRoll(int level, bool forceCostFour, bool onlyCostFour, int[] excludedBaseIds, PhotonMessageInfo info)
    {
        if (!IsMasterClient || info.Sender == null) return;
        ProcessSharedShopRoll(info.Sender.ActorNumber, level, forceCostFour, onlyCostFour, excludedBaseIds);
    }

    private void ProcessSharedShopRoll(int actorNumber, int level, bool forceCostFour, bool onlyCostFour, int[] excludedBaseIds)
    {
        // TEMP DEBUG — 3성 상점 제외 확인용. 정상 동작 확인 후 제거.
        Debug.Log($"[Shop][TEMP DEBUG] ProcessSharedShopRoll — actor={actorNumber}, excluded=[{string.Join(",", excludedBaseIds ?? System.Array.Empty<int>())}]");

        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;

        // RPC/로컬 호출 양쪽에서 받은 배열을 이번 처리에서만 쓸 지역 HashSet으로 변환한다 — 필드로 저장하지 않는다.
        var excludedSet = new HashSet<int>(excludedBaseIds ?? System.Array.Empty<int>());

        if (shop == null || !shop.TryAuthorityRollSharedShop(
                actorNumber, level, forceCostFour, onlyCostFour, excludedSet, out int revision, out int[] slots))
        {
            SendSharedShopSnapshot(actorNumber, 0, System.Array.Empty<int>());
            return;
        }

        BroadcastSharedPoolMirror(actorNumber, revision, slots);
        SendSharedShopSnapshot(actorNumber, revision, slots);
    }

    [PunRPC]
    private void RPC_RequestSharedShopRestore(PhotonMessageInfo info)
    {
        if (!IsMasterClient || info.Sender == null) return;
        ProcessSharedShopRestore(info.Sender.ActorNumber);
    }

    /// <summary>
    /// 재접속 복원 전용 — TryAuthorityRollSharedShop(새 추첨)을 부르지 않고, 조회 전용
    /// TryAuthorityGetExistingSharedShopReservation으로 기존 예약만 확인한다. 예약이 없으면
    /// (마스터 교체 등으로 유실된 경우) 새 상점으로 대체하지 않고 로그만 남기고 끝낸다 —
    /// SendSharedShopSnapshot을 호출하지 않으므로 재접속 클라이언트의 로컬 상점 상태를 건드리지 않는다.
    /// </summary>
    private void ProcessSharedShopRestore(int actorNumber)
    {
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        if (shop == null)
        {
            Debug.LogWarning("[SharedShopPool][Restore] ShopManager를 찾을 수 없음 — 상점 복원 생략");
            return;
        }

        if (!shop.TryAuthorityGetExistingSharedShopReservation(actorNumber, out int revision, out int[] slots))
        {
            Debug.LogWarning($"[SharedShopPool][Restore] actor={actorNumber} 기존 상점 예약 없음 — 복원 생략(신규 롤로 대체하지 않음)");
            return;
        }

        Debug.Log($"[SharedShopPool][Restore] actor={actorNumber} 기존 상점 예약 발견 rev={revision} — 스냅샷 재전송");
        SendSharedShopSnapshot(actorNumber, revision, slots);
    }

    private void SendSharedShopSnapshot(int actorNumber, int revision, int[] slots)
    {
        Player target = PhotonNetwork.CurrentRoom?.GetPlayer(actorNumber);
        if (target != null)
            photonView.RPC(nameof(RPC_ApplySharedShopSnapshot), target, revision, slots ?? System.Array.Empty<int>());
    }

    [PunRPC]
    private void RPC_ApplySharedShopSnapshot(int revision, int[] slots)
    {
        GameManager.TryGet(out var gm);
        gm?.Shop?.ApplySharedShopSnapshot(revision, slots);
    }

    [PunRPC]
    private void RPC_RequestSharedShopPurchase(int revision, int slot, PhotonMessageInfo info)
    {
        if (!IsMasterClient || info.Sender == null) return;
        ProcessSharedShopPurchase(info.Sender.ActorNumber, revision, slot);
    }

    private void ProcessSharedShopPurchase(int actorNumber, int revision, int slot)
    {
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        int pokemonId = 0;
        bool success = shop != null && shop.TryAuthorityPurchaseSharedShop(
            actorNumber, revision, slot, out pokemonId);

        Player target = PhotonNetwork.CurrentRoom?.GetPlayer(actorNumber);
        if (target != null)
            photonView.RPC(nameof(RPC_ResolveSharedShopPurchase), target,
                revision, slot, success ? pokemonId : 0, success);

        if (success && shop != null)
        {
            foreach (var state in shop.GetSharedReservationsMirror())
            {
                if (state.actorNumber != actorNumber) continue;
                BroadcastSharedPoolMirror(actorNumber, state.revision, state.slots);
                break;
            }
        }
    }

    [PunRPC]
    private void RPC_ResolveSharedShopPurchase(int revision, int slot, int pokemonId, bool success)
    {
        GameManager.TryGet(out var gm);
        gm?.Shop?.ResolveSharedShopPurchase(revision, slot, pokemonId, success);
    }

    [PunRPC]
    private void RPC_RequestSharedShopMergePurchase(int revision, int slotA, int slotB, PhotonMessageInfo info)
    {
        if (!IsMasterClient || info.Sender == null) return;
        ProcessSharedShopMergePurchase(info.Sender.ActorNumber, revision, slotA, slotB);
    }

    /// <summary>
    /// 마스터 권위로 두 슬롯을 한 번에 검증·소비한다(전체 성공 또는 전체 실패).
    /// 슬롯별로 나눠 RPC를 두 번 보내지 않고, 이 한 번의 처리 안에서 두 예약을 동시에 소비한다.
    /// </summary>
    private void ProcessSharedShopMergePurchase(int actorNumber, int revision, int slotA, int slotB)
    {
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        int pokemonId = 0;
        bool success = shop != null && shop.TryAuthorityPurchaseMergeSharedShop(
            actorNumber, revision, slotA, slotB, out pokemonId);

        Player target = PhotonNetwork.CurrentRoom?.GetPlayer(actorNumber);
        if (target != null)
            photonView.RPC(nameof(RPC_ResolveSharedShopMergePurchase), target,
                revision, slotA, slotB, success ? pokemonId : 0, success);

        if (success && shop != null)
        {
            foreach (var state in shop.GetSharedReservationsMirror())
            {
                if (state.actorNumber != actorNumber) continue;
                BroadcastSharedPoolMirror(actorNumber, state.revision, state.slots);
                break;
            }
        }
    }

    [PunRPC]
    private void RPC_ResolveSharedShopMergePurchase(
        int revision, int slotA, int slotB, int pokemonId, bool success)
    {
        GameManager.TryGet(out var gm);
        gm?.Shop?.ResolveSharedShopMergePurchase(revision, slotA, slotB, pokemonId, success);
    }

    [PunRPC]
    private void RPC_RequestSharedShopReturn(int pokemonId, int amount)
    {
        if (IsMasterClient) ProcessSharedShopReturn(pokemonId, amount);
    }

    /// <summary>
    /// 비마스터 클라이언트의 QA 코스트 유닛 획득 요청을
    /// MasterClient가 수신한다.
    /// </summary>
    [PunRPC]
    private void RPC_RequestSharedDebugUnitByCost(
        int cost,
        PhotonMessageInfo info)
    {
        if (!IsMasterClient ||
            info.Sender == null)
        {
            return;
        }

        ProcessSharedDebugUnitByCost(
            info.Sender.ActorNumber,
            cost
        );
    }

    private void ProcessSharedShopReturn(int pokemonId, int amount)
    {
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        if (shop == null) return;
        shop.AuthorityReturnSharedShopCopy(pokemonId, amount);
        BroadcastSharedPoolMirror(-1, 0, System.Array.Empty<int>());
    }

    /// <summary>
    /// MasterClient가 현재 남아 있는 공용 풀에서
    /// 지정 코스트의 유닛 카피 1장을 선택하고 차감한다.
    ///
    /// 선택 확률은 ShopManager에서 남은 카피 수에 비례해 계산한다.
    /// 성공하면 요청자에게 선택된 원본 pokemonId를 전달한다.
    /// </summary>
    private void ProcessSharedDebugUnitByCost(
        int actorNumber,
        int cost)
    {
        if (!IsMasterClient)
            return;

        Player target =
            PhotonNetwork.CurrentRoom != null
                ? PhotonNetwork.CurrentRoom.GetPlayer(actorNumber)
                : null;

        if (target == null)
        {
            Debug.LogWarning(
                $"[SharedShopPool][QA] 요청 플레이어를 찾을 수 없음: " +
                $"actor={actorNumber}"
            );

            return;
        }

        if (cost < 1 || cost > 5)
        {
            photonView.RPC(
                nameof(RPC_ResolveSharedDebugUnitByCost),
                target,
                cost,
                0,
                false
            );

            return;
        }

        ShopManager shop =
            GameManager.TryGet(out var gm) ? gm.Shop : null;

        int pokemonId = 0;

        bool success =
            shop != null &&
            shop.TryAuthorityTakeDebugUnitByCost(
                cost,
                out pokemonId
            );

        /*
         * 먼저 요청자에게 결과를 전달한다.
         *
         * 요청자는 ShopManager에서 실제 벤치 생성을 시도하고,
         * 생성이나 배치에 실패하면 기존
         * RequestSharedShopReturn(pokemonId, 1)을 호출해
         * 카피를 다시 공용 풀로 돌려보낸다.
         */
        photonView.RPC(
            nameof(RPC_ResolveSharedDebugUnitByCost),
            target,
            cost,
            success ? pokemonId : 0,
            success
        );

        if (!success)
        {
            Debug.LogWarning(
                $"[SharedShopPool][QA] {cost}코스트 획득 실패 — " +
                "남은 공용 풀 없음 또는 ShopManager 처리 실패"
            );

            return;
        }

        /*
         * 공용 풀에서 실제로 1장이 빠졌으므로
         * 모든 클라이언트의 풀 미러를 갱신한다.
         *
         * actorNumber=-1:
         * 특정 상점 예약 변경이 아니라 풀 수량만 변경됐다는 의미.
         */
        BroadcastSharedPoolMirror(
            -1,
            0,
            System.Array.Empty<int>()
        );

        Debug.Log(
            $"[SharedShopPool][QA] actor={actorNumber}, " +
            $"{cost}코스트 유닛 지급 승인, pokemonId={pokemonId}"
        );
    }

    /// <summary>
    /// MasterClient가 선택·차감한 QA 유닛의 원본 ID를
    /// 요청한 클라이언트가 수신한다.
    ///
    /// 실제 생성과 벤치 배치는 로컬 ShopManager가 처리한다.
    /// </summary>
    [PunRPC]
    private void RPC_ResolveSharedDebugUnitByCost(
        int cost,
        int pokemonId,
        bool success)
    {
        ShopManager shop =
            GameManager.TryGet(out var gm) ? gm.Shop : null;

        if (shop == null)
        {
            /*
             * 정상적인 게임 씬에서는 발생하면 안 되지만,
             * ShopManager가 없는 상태에서 이미 풀이 차감됐다면
             * MasterClient에 카피 반환을 요청한다.
             */
            if (success && pokemonId > 0)
            {
                RequestSharedShopReturn(
                    pokemonId,
                    1
                );
            }

            Debug.LogError(
                "[SharedShopPool][QA] ShopManager 없음 — " +
                "QA 유닛 생성 불가"
            );

            return;
        }

        shop.ResolveSharedDebugUnitByCost(
            cost,
            pokemonId,
            success
        );
    }

    /// <summary>
    /// QA 패널의 "특정 종 지급" 버튼이 호출하는 공개 진입점. RequestSharedDebugUnitByCost와 동일한
    /// authority 규칙(MasterClient가 실제 차감 → 결과 회신 → 요청자가 로컬 생성)이되, 코스트 무작위
    /// 선택이 아니라 지정한 pokemonId 하나만 다룬다.
    /// </summary>
    public bool RequestSharedDebugUnitBySpecies(int pokemonId)
    {
        if (!UsesSharedShopPool) return false;
        if (pokemonId <= 0) return false;

        int actorNumber =
            PhotonNetwork.LocalPlayer != null
                ? PhotonNetwork.LocalPlayer.ActorNumber
                : 0;

        if (actorNumber <= 0) return false;

        if (IsMasterClient)
        {
            ProcessSharedDebugUnitBySpecies(actorNumber, pokemonId);
        }
        else
        {
            photonView.RPC(
                nameof(RPC_RequestSharedDebugUnitBySpecies),
                RpcTarget.MasterClient,
                pokemonId
            );
        }

        return true;
    }

    /// <summary>비마스터 클라이언트의 QA 지정 종 획득 요청을 MasterClient가 수신한다.</summary>
    [PunRPC]
    private void RPC_RequestSharedDebugUnitBySpecies(int pokemonId, PhotonMessageInfo info)
    {
        if (!IsMasterClient || info.Sender == null) return;
        ProcessSharedDebugUnitBySpecies(info.Sender.ActorNumber, pokemonId);
    }

    /// <summary>
    /// MasterClient가 지정 종의 공용 풀 재고를 확인·차감한다. ProcessSharedDebugUnitByCost와 동일한
    /// 흐름이되 코스트 무작위 선택 없이 지정 pokemonId 하나만 다룬다.
    /// </summary>
    private void ProcessSharedDebugUnitBySpecies(int actorNumber, int pokemonId)
    {
        if (!IsMasterClient) return;

        Player target =
            PhotonNetwork.CurrentRoom != null
                ? PhotonNetwork.CurrentRoom.GetPlayer(actorNumber)
                : null;

        if (target == null)
        {
            Debug.LogWarning($"[SharedShopPool][QA] 요청 플레이어를 찾을 수 없음: actor={actorNumber}");
            return;
        }

        ShopManager shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        PokemonData data =
            PokemonDatabase.Instance != null ? PokemonDatabase.Instance.GetById(pokemonId) : null;

        bool success = shop != null && data != null && shop.TryAuthorityTakeDebugUnitBySpecies(data);

        // 먼저 요청자에게 결과를 전달한다. 요청자는 로컬 ShopManager에서 실제 벤치 생성을 시도하고,
        // 생성/배치에 실패하면 RequestSharedShopReturn(pokemonId, 1)으로 카피를 다시 반환한다.
        photonView.RPC(
            nameof(RPC_ResolveSharedDebugUnitBySpecies),
            target,
            pokemonId,
            success
        );

        if (!success)
        {
            Debug.LogWarning($"[SharedShopPool][QA] 지정 종(id={pokemonId}) 획득 실패 — 남은 공용 풀 없음 또는 처리 실패");
            return;
        }

        BroadcastSharedPoolMirror(-1, 0, System.Array.Empty<int>());

        Debug.Log($"[SharedShopPool][QA] actor={actorNumber}, 지정 종 지급 승인, pokemonId={pokemonId}");
    }

    /// <summary>MasterClient가 확인·차감한 QA 지정 종의 결과를 요청한 클라이언트가 수신한다.
    /// 실제 생성과 벤치 배치는 로컬 ShopManager가 처리한다.</summary>
    [PunRPC]
    private void RPC_ResolveSharedDebugUnitBySpecies(int pokemonId, bool success)
    {
        ShopManager shop = GameManager.TryGet(out var gm) ? gm.Shop : null;

        if (shop == null)
        {
            if (success && pokemonId > 0) RequestSharedShopReturn(pokemonId, 1);
            Debug.LogError("[SharedShopPool][QA] ShopManager 없음 — QA 유닛 생성 불가");
            return;
        }

        shop.ResolveSharedDebugUnitBySpecies(pokemonId, success);
    }

    private void BroadcastSharedPoolMirror(int actorNumber, int revision, int[] slots)
    {
        if (!IsMasterClient) return;
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        if (shop == null) return;

        shop.GetSharedPoolMirror(out int[] pokemonIds, out int[] remaining);
        photonView.RPC(nameof(RPC_ApplySharedPoolMirror), RpcTarget.All,
            pokemonIds, remaining, actorNumber, revision, slots ?? System.Array.Empty<int>());
    }

    [PunRPC]
    private void RPC_ApplySharedPoolMirror(
        int[] pokemonIds, int[] remaining, int actorNumber, int revision, int[] slots)
    {
        GameManager.TryGet(out var gm);
        gm?.Shop?.ApplySharedPoolMirror(
            pokemonIds, remaining, actorNumber, revision, slots);
    }

    /// <summary>
    /// 자기 보드 전투 결과(승/패)를 팀에 보고. PlayerHealthManager가 OnBattleEnd에서 호출.
    /// 두 플레이어가 모두 보고하면 MasterClient가 팀 결과를 판정한다(OnPlayerPropertiesUpdate).
    /// 솔로 모드는 1인=팀이므로 즉시 판정.
    /// </summary>
    public void ReportBattleResult(bool isWin)
    {
        if (_soloMode)
        {
            // 1인 = 팀. 승=BothWin, 패=BothLose(Split 불가).
            ResolveSoloRound(isWin);
            return;
        }

        if (_isLeavingRoom) return; // Leaving 중 SetProperties 금지

        if (DebugSuppressBattleResultReport)
        {
            _suppressedBattleResult = isWin;
            Debug.LogWarning($"[Network][QA] 전투 결과 보고 억제 중 — 실제로는 {(isWin ? "승" : "패")}지만 " +
                              "안 보냄. DebugSendSuppressedBattleResultNow()로 나중에 보낼 수 있음.");
            return;
        }

        _lastLocalBattleResult = isWin; // 재전송 요청(RPC_RequestBattleResultResend) 응답용 캐시
        var props = new Hashtable { { BATTLE_RESULT_PROP_KEY, isWin ? 1 : 0 } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    /// <summary>DebugSuppressBattleResultReport로 붙들어뒀던 결과를 지금 보낸다(응답 불능 → 복귀 재현용).
    /// 억제된 결과가 없으면 아무 것도 안 한다.</summary>
    public void DebugSendSuppressedBattleResultNow()
    {
        if (!_suppressedBattleResult.HasValue)
        {
            Debug.LogWarning("[Network][QA] 억제된 결과가 없습니다.");
            return;
        }

        if (_isLeavingRoom) return;

        bool isWin = _suppressedBattleResult.Value;
        _suppressedBattleResult = null;
        _lastLocalBattleResult = isWin;
        var props = new Hashtable { { BATTLE_RESULT_PROP_KEY, isWin ? 1 : 0 } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        Debug.Log($"[Network][QA] 억제됐던 결과({(isWin ? "승" : "패")}) 지금 전송");
    }

    // ─────────────────────────────────────────
    // 항복(2인 합의) — 옵션창 [항복]에서 요청, 파트너 동의 시에만 성립.
    // 마스터 경유 없이 2인 방 상대에게 직접 RPC(패턴: RPC_GoldReceive와 동일).
    // ─────────────────────────────────────────

    private bool _surrenderRequestSent;         // 내가 보낸 요청이 아직 응답 대기 중
    private bool _surrenderRequestReceived;     // 파트너가 보낸 요청에 내가 아직 응답하지 않음
    private bool _surrenderRejectedNotice;      // 내가 보낸 요청이 거절됨 — UI 1회 안내용
    private bool _surrenderCrossCancelledNotice; // 교차 요청으로 취소됨 — UI 1회 안내용

    /// <summary>파트너의 항복 요청에 아직 응답하지 않았는지. OptionsPanelUI가 매 프레임 폴링해 전용 모달을 띄운다.</summary>
    public bool HasIncomingSurrenderRequest => _surrenderRequestReceived;

    /// <summary>내가 보낸 항복 요청이 거절됐는지(1회성 안내). OptionsPanelUI가 감지 즉시 AcknowledgeSurrenderRejected로 소비한다.</summary>
    public bool SurrenderRequestRejected => _surrenderRejectedNotice;

    /// <summary>내 요청이 파트너의 요청과 교차해 취소됐는지(1회성 안내). OptionsPanelUI가 감지 즉시 AcknowledgeSurrenderCrossCancelled로 소비한다.</summary>
    public bool SurrenderRequestCrossCancelled => _surrenderCrossCancelledNotice;

    /// <summary>
    /// 옵션창 [항복] 확인 팝업([요청하기])에서 호출. 파트너에게 항복 요청을 보낸다.
    /// 이미 오가는 요청이 있으면 무시(중복 요청 방지). 솔로/1인 방에서는 무시.
    /// </summary>
    public void RequestSurrender()
    {
        if (_soloMode) return;
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom.PlayerCount < MAX_PLAYERS) return;
        if (_surrenderRequestSent || _surrenderRequestReceived) return;

        _surrenderRequestSent = true;
        photonView.RPC(nameof(RPC_SurrenderRequested), RpcTarget.Others);
    }

    /// <summary>파트너 요청 전용 모달의 [항복하기]/[계속하기]에서 호출.</summary>
    public void RespondToSurrender(bool accepted)
    {
        if (!_surrenderRequestReceived) return;
        _surrenderRequestReceived = false;

        if (accepted)
            photonView.RPC(nameof(RPC_SurrenderResolved), RpcTarget.All, true);
        else
            photonView.RPC(nameof(RPC_SurrenderResolved), RpcTarget.Others, false);
    }

    /// <summary>거절 안내를 표시한 뒤 OptionsPanelUI가 호출해 1회성 알림을 소비한다.</summary>
    public void AcknowledgeSurrenderRejected() => _surrenderRejectedNotice = false;

    /// <summary>교차 취소 안내를 표시한 뒤 OptionsPanelUI가 호출해 1회성 알림을 소비한다.</summary>
    public void AcknowledgeSurrenderCrossCancelled() => _surrenderCrossCancelledNotice = false;

    [PunRPC]
    private void RPC_SurrenderRequested()
    {
        // 내가 이미 요청을 보내둔 상태에서 상대 요청도 도착 — 교차 요청. 자동으로 성립시키지 않고
        // 양쪽 다 취소 처리해 다시 요청할 수 있게 한다(파트너의 명시적 [항복하기]로만 성립해야 함).
        if (_surrenderRequestSent)
        {
            _surrenderRequestSent = false;
            _surrenderRequestReceived = false;
            _surrenderRejectedNotice = false;
            _surrenderCrossCancelledNotice = true;
            return;
        }

        // 이미 파트너의 요청에 응답 대기 중인데 추가 요청이 들어옴 — 기존 요청을 유지한 채 조용히 무시.
        if (_surrenderRequestReceived)
            return;

        _surrenderRequestReceived = true;
    }

    [PunRPC]
    private void RPC_SurrenderResolved(bool accepted)
    {
        _surrenderRequestSent = false;
        _surrenderRequestReceived = false;

        if (accepted)
        {
            // 항복 합의로 매치가 끝났다 — 더 이상 같은 방에서 재시작하지 않고 타이틀로 복귀한다.
            // 실제 타이틀 이동은 이 이벤트를 구독하는 OptionsPanelUI가
            // RequestCompletedMatchReturnToTitle을 통해 수행한다(양쪽 클라 모두 이 RPC를 실행하므로
            // 각자 독립적으로 자기 화면에서 복귀한다).
            GameEvents.SessionEnded(SessionEndReason.Surrender);
        }
        else
            _surrenderRejectedNotice = true;
    }

    // ─────────────────────────────────────────
    // 통신교환
    // 유닛 즉시 전송 → 상대 통신기 대기열 저장 → 수동 수령
    // ─────────────────────────────────────────
    private sealed class TradeUnitPacket
    {
        /// <summary>송신자가 발급한 거래 고유번호.</summary>
        public int tradeId;

        public int pokemonId;
        public int starLevel;
        public int[] itemIds;
        public int stoneId;
        public int preStonePokemonId;

        /// <summary>송신 당시 필드에 배치돼 있던 유닛인지.</summary>
        public bool wasOnBoard;

        /// <summary>이미 통신진화가 완료된 유닛인지(재전송 시 ×1.4·역매핑 보존용).</summary>
        public bool isTradeEvolved;
    }

    /// <summary>
    /// 송신 후 상대방의 실제 수령 ACK를 기다리는 거래 정보.
    /// 유닛 GameObject가 없어져도 패킷만으로 유닛을 복원할 수 있어야 한다.
    /// </summary>
    private sealed class PendingOutgoingTrade
    {
        public TradeUnitPacket packet;
        public int sentRound;
        public string pokemonName;
    }

    /// <summary>파트너에게서 도착해 통신기에 대기 중인 유닛들.</summary>
    private readonly Queue<TradeUnitPacket> _incomingTradeQueue = new();

    /// <summary>
    /// 내가 전송했지만 상대방이 아직 실제로 수령하지 않은 유닛들.
    /// key = tradeId
    /// </summary>
    private readonly Dictionary<int, PendingOutgoingTrade>
        _pendingOutgoingTrades = new();

    /// <summary>로컬 거래 번호 발급용 순번.</summary>
    private int _nextTradeSequence = 1;

    /// <summary>
    /// 이번 라운드에 유닛을 전송했는지. 전송 기회는 라운드마다 새로 주어지며 쌓이지 않는다 —
    /// 안 쓰고 넘어간 라운드를 기억할 필요가 없어 라운드가 시작될 때마다 그냥 false로 되돌린다.
    /// </summary>
    private bool _tradeSentThisRound;

    /// <summary>현재 내 통신기에 대기 중인 수신 유닛 수.</summary>
    public int PendingTradeUnitCount => _incomingTradeQueue.Count;

    /// <summary>파트너가 아직 수령하지 않은 내 송신 유닛 수.</summary>
    public int PendingOutgoingTradeCount => _pendingOutgoingTrades.Count;

    /// <summary>
    /// 플레이어별 거래 고유번호를 발급한다.
    /// 두 플레이어가 각각 같은 순번을 사용해도 ActorNumber가 달라 충돌하지 않는다.
    /// </summary>
    private int CreateTradeId()
    {
        int actorNumber =
            PhotonNetwork.LocalPlayer != null
                ? PhotonNetwork.LocalPlayer.ActorNumber
                : 0;

        int sequence = _nextTradeSequence++;

        if (_nextTradeSequence >= 1000000)
            _nextTradeSequence = 1;

        return actorNumber * 1000000 + sequence;
    }

    /// <summary>
    /// 지금 유닛을 전송할 수 있는지. 보낼 수 없으면 <paramref name="reason"/>에 사유 문구가 담긴다.
    ///
    /// <see cref="SendTradeUnit"/>이 실제로 쓰는 검사와 <b>같은 코드</b>다 — 통신기 안내창이
    /// "전송 준비 완료"라고 띄웠는데 막상 놓으면 거절되는 어긋남을 막으려고 한 곳에 모아 뒀다.
    /// 상태를 바꾸지 않으므로 표시용으로 매 프레임 불러도 된다.
    /// </summary>
    public bool CanSendTradeUnit(out string reason)
    {
        if (_soloMode || !PhotonNetwork.InRoom)
        {
            reason = "파트너 없음";
            return false;
        }

        if (_lastKnownRound <= 0)
        {
            reason = "라운드 시작 전";
            return false;
        }

        if (_tradeSentThisRound)
        {
            reason = "이번 라운드 전송 완료";
            return false;
        }

        Player[] others = PhotonNetwork.PlayerListOthers;

        if (others == null ||
            others.Length == 0 ||
            others[0].IsInactive)
        {
            reason = "파트너 연결 끊김";
            return false;
        }

        // SendTradeUnit이 유닛을 보드에서 빼낼 때 BoardManager가 필요하다. 여기서 같이 보지 않으면
        // 안내창은 "전송 준비 완료"로 떠 있는데 막상 놓으면 거절되는 어긋남이 남는다
        // — 이 함수를 뽑아낸 이유가 바로 그 어긋남을 없애는 것이었다.
        if (!GameManager.TryGet(out var gm) || gm.Board == null)
        {
            reason = "보드 준비 중";
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>사유가 필요 없을 때(가능 여부만 보는 UI용).</summary>
    public bool CanSendTradeUnit() => CanSendTradeUnit(out _);

    /// <summary>
    /// 유닛을 파트너 통신기로 전송한다.
    ///
    /// 전송 시점에 유닛은 내 보드/벤치에서 제거하지만,
    /// 판매가 아니므로 공용 상점 풀이나 아이템 인벤토리에는 반환하지 않는다.
    ///
    /// 유닛의 전체 정보는 tradeId별 송신 대기 저장소에 보존되며,
    /// 상대가 실제로 벤치에 수령한 뒤 ACK를 보내야 제거된다.
    /// </summary>
    public void SendTradeUnit(PokemonUnit unit)
    {
        if (unit == null || unit.data == null)
            return;

        if (!CanSendTradeUnit(out string reason))
        {
            Debug.LogWarning($"[Trade] 전송 불가: {reason}");
            GameEvents.TradeRejected();
            return;
        }

        // CanSendTradeUnit이 이미 걸러내지만 참조를 여기서 다시 얻어야 하므로 방어를 남긴다
        // (여기까지 왔는데 null이면 그 사이에 매니저가 사라진 것이라 로그를 남길 값어치가 있다).
        BoardManager board =
            GameManager.TryGet(out var boardOwner) ? boardOwner.Board : null;

        if (board == null)
        {
            Debug.LogError("[Trade] BoardManager 없음 — 전송 불가");
            GameEvents.TradeRejected();
            return;
        }

        int[] itemIds;

        if (unit.items == null || unit.items.Count == 0)
        {
            itemIds = System.Array.Empty<int>();
        }
        else
        {
            var ids = new List<int>();

            foreach (ItemData item in unit.items)
            {
                if (item != null)
                    ids.Add(item.id);
            }

            itemIds = ids.ToArray();
        }

        int stoneId =
            unit.equippedStone != null
                ? unit.equippedStone.id
                : 0;

        int preStonePokemonId =
            unit.preStoneData != null
                ? unit.preStoneData.id
                : 0;

        bool wasOnBoard = IsUnitOnBoard(board, unit);
        int tradeId = CreateTradeId();

        var packet = new TradeUnitPacket
        {
            tradeId = tradeId,
            pokemonId = unit.data.id,
            starLevel = Mathf.Clamp(unit.starLevel, 1, 3),
            itemIds = itemIds,
            stoneId = stoneId,
            preStonePokemonId = preStonePokemonId,
            wasOnBoard = wasOnBoard,
            isTradeEvolved = unit.isTradeEvolved
        };

        var pendingTrade = new PendingOutgoingTrade
        {
            packet = packet,
            sentRound = _lastKnownRound,
            pokemonName = unit.data.pokemonName
        };

        /*
         * 매우 중요:
         * RemoveUnitForTrade는 판매 처리와 달라야 한다.
         *
         * 여기서는 다음 작업이 일어나면 안 된다.
         * - 공용 상점 풀 반환
         * - 일반 장비 인벤토리 반환
         * - 진화의 돌 인벤토리 반환
         *
         * 오직 보드/벤치 점유 해제만 수행해야 한다.
         */
        if (!board.RemoveUnitForTrade(unit))
        {
            Debug.LogError(
                $"[Trade] {unit.data.pokemonName} 보드/벤치 제거 실패"
            );

            GameEvents.TradeRejected();
            return;
        }

        // GameObject를 제거하기 전에 복구 가능한 패킷을 저장한다.
        _pendingOutgoingTrades.Add(tradeId, pendingTrade);

        // 전송 요청이 정상 등록된 시점에 이번 라운드 기회를 쓴 것으로 표시한다.
        _tradeSentThisRound = true;

        Destroy(unit.gameObject);

        Debug.Log(
            $"[Trade] 전송 등록: tradeId={tradeId}, " +
            $"{pendingTrade.pokemonName} ★{packet.starLevel}, " +
            $"장비 {packet.itemIds.Length}개, 돌 ID {packet.stoneId}, " +
            $"위치 {(packet.wasOnBoard ? "필드" : "벤치")}, " +
            $"ACK 대기 {_pendingOutgoingTrades.Count}마리"
        );

        photonView.RPC(
            nameof(RPC_TradeQueueReceive),
            RpcTarget.Others,
            packet.tradeId,
            packet.pokemonId,
            packet.starLevel,
            packet.itemIds,
            packet.stoneId,
            packet.preStonePokemonId,
            packet.wasOnBoard,
            packet.isTradeEvolved
        );
    }

    private static bool IsUnitOnBoard(
        BoardManager board,
        PokemonUnit targetUnit)
        {
            if (board == null || targetUnit == null)
                return false;

            foreach (PokemonUnit unit in board.GetUnitsOnBoard())
            {
                if (unit == targetUnit)
                    return true;
            }

            return false;
        }

    /// <summary>
    /// 현재 전투 페이즈 여부.
    /// 전투 중 필드 유닛의 통신진화를 다음 쇼핑 페이즈까지 지연하는 데 사용.
    /// </summary>
    private bool _isBattlePhase;

    /// <summary>
    /// 전투 중 발생해 다음 쇼핑 페이즈까지 대기 중인 통신진화 정보.
    /// 유닛 참조를 직접 저장하지 않고 원본/진화체 ID를 저장한다.
    /// 대기 중 유닛이 판매되거나 이동해도 안전하게 다시 조회하기 위함.
    /// </summary>
    private readonly List<PendingTradeEvolution> _pendingTradeEvolutions = new();

    private sealed class PendingTradeEvolution
    {
        public int originalPokemonId;
        public int evolvedPokemonId;
    }

    /// <summary>
    /// 파트너가 보낸 유닛을 통신기 FIFO 대기열에 저장한다.
    /// 이 시점에는 아직 실제 수령이 아니므로 ACK를 보내지 않는다.
    /// </summary>
    [PunRPC]
    private void RPC_TradeQueueReceive(
        int tradeId,
        int pokemonId,
        int starLevel,
        int[] itemIds,
        int stoneId,
        int preStonePokemonId,
        bool wasOnBoard,
        bool isTradeEvolved)
    {
        PokemonData data =
            PokemonDatabase.Instance != null
                ? PokemonDatabase.Instance.GetById(pokemonId)
                : null;

        if (data == null)
        {
            /*
             * ACK를 보내지 않는다.
             * 송신자의 PendingOutgoingTrade가 유지되어 소유권 데이터가
             * 사라지지 않게 한다.
             */
            Debug.LogError(
                $"[Trade] 수신 패킷 저장 실패 — " +
                $"tradeId={tradeId}, Pokemon ID {pokemonId} 조회 불가"
            );

            return;
        }

        var packet = new TradeUnitPacket
        {
            tradeId = tradeId,
            pokemonId = pokemonId,
            starLevel = Mathf.Clamp(starLevel, 1, 3),
            itemIds = itemIds ?? System.Array.Empty<int>(),
            stoneId = stoneId,
            preStonePokemonId = preStonePokemonId,
            wasOnBoard = wasOnBoard,
            isTradeEvolved = isTradeEvolved
        };

        _incomingTradeQueue.Enqueue(packet);
        GameEvents.TradeQueueChanged(_incomingTradeQueue.Count);

        Debug.Log(
            $"[Trade] 통신기 도착: tradeId={tradeId}, " +
            $"{data.pokemonName} ★{packet.starLevel}, " +
            $"현재 대기 {_incomingTradeQueue.Count}마리"
        );
    }

    /// <summary>
    /// 상대가 유닛을 실제로 벤치에 배치한 뒤 보내는 수령 완료 ACK.
    /// 해당 tradeId의 송신 대기 데이터만 제거한다.
    /// </summary>
    [PunRPC]
    private void RPC_TradeQueueAck(int tradeId)
    {
        if (!_pendingOutgoingTrades.TryGetValue(
                tradeId,
                out PendingOutgoingTrade pendingTrade))
        {
            Debug.LogWarning(
                $"[Trade] 알 수 없거나 이미 처리된 ACK 수신: tradeId={tradeId}"
            );

            return;
        }

        _pendingOutgoingTrades.Remove(tradeId);

        Debug.Log(
            $"[Trade] 수령 ACK 완료: tradeId={tradeId}, " +
            $"{pendingTrade.pokemonName} ★{pendingTrade.packet.starLevel}, " +
            $"남은 ACK 대기 {_pendingOutgoingTrades.Count}마리"
        );
    }

    /// <summary>
    /// 통신기를 클릭하면 FIFO 순서로 빈 벤치 칸만큼 연속 수령한다.
    ///
    /// 첫 번째 유닛의 복원에 실패하면 그 유닛을 Queue 맨 앞에 유지하고
    /// 뒤의 유닛을 건너뛰지 않는다.
    /// </summary>
    public bool TryReceiveNextTradeUnit()
    {
        if (_incomingTradeQueue.Count == 0)
        {
            Debug.Log("[Trade] 통신기에 대기 중인 유닛이 없습니다.");
            return false;
        }

        BoardManager board =
            GameManager.TryGet(out var gm) ? gm.Board : null;

        if (board == null)
        {
            Debug.LogError("[Trade] BoardManager 없음 — 수령 불가");
            return false;
        }

        if (!board.HasBenchSpace())
        {
            Debug.LogWarning("[Trade] 벤치가 가득 차 수령할 수 없습니다.");
            return false;
        }

        int receivedCount = 0;

        while (_incomingTradeQueue.Count > 0 &&
               board.HasBenchSpace())
        {
            /*
             * FIFO 맨 앞 유닛 수령에 실패하면 반복을 즉시 종료한다.
             * 실패 유닛을 건너뛰고 다음 유닛을 받으면 안 된다.
             */
            if (!TryReceiveFrontTradeUnit(board))
                break;

            receivedCount++;
        }

        if (receivedCount > 0)
        {
            Debug.Log(
                $"[Trade] 통신기 일괄 수령 완료: {receivedCount}마리, " +
                $"남은 대기 {_incomingTradeQueue.Count}마리"
            );
        }

        return receivedCount > 0;
    }

    /// <summary>
    /// FIFO Queue 맨 앞의 유닛 한 마리를 복원한다.
    /// 성공한 경우에만 Queue에서 제거하고 송신자에게 ACK를 보낸다.
    /// 수령 유닛은 항상 수신자의 벤치에 생성되고 벤치 유닛은 전투에 영향을 주지 않으므로,
    /// 송신자가 필드/벤치 어디서 보냈는지와 무관하게 통신진화 대상이면 무조건 즉시 진화체로 생성한다.
    /// </summary>
    private bool TryReceiveFrontTradeUnit(BoardManager board)
    {
        if (board == null ||
            _incomingTradeQueue.Count == 0 ||
            !board.HasBenchSpace())
        {
            return false;
        }

        TradeUnitPacket packet = _incomingTradeQueue.Peek();

        PokemonData originalData =
            PokemonDatabase.Instance != null
                ? PokemonDatabase.Instance.GetById(packet.pokemonId)
                : null;

        if (originalData == null)
        {
            Debug.LogError(
                $"[Trade] 대기 유닛 조회 실패 — " +
                $"tradeId={packet.tradeId}, Pokemon ID {packet.pokemonId}"
            );

            return false;
        }

        TradeEvolutionData tradeEvolution = TradeEvolutionData.Instance;

        string evolvedNameEn =
            tradeEvolution != null
                ? tradeEvolution.GetEvolved(originalData.pokemonNameEn)
                : null;

        bool isTradeEvolution =
            !string.IsNullOrEmpty(evolvedNameEn);

        PokemonData evolvedData = null;

        if (isTradeEvolution)
        {
            evolvedData =
                PokemonDatabase.Instance.GetByNameEn(evolvedNameEn);

            if (evolvedData == null)
            {
                Debug.LogError(
                    $"[Trade] 통신진화체 '{evolvedNameEn}' 조회 실패 — " +
                    $"tradeId={packet.tradeId}"
                );

                return false;
            }
        }

        // 통신진화 대상이면 지연 없이 항상 진화체로 생성한다.
        PokemonData receivedData = evolvedData ?? originalData;

        PokemonUnit receivedUnit =
            UnitFactory.Create(receivedData, packet.starLevel);

        if (receivedUnit == null)
        {
            Debug.LogError(
                $"[Trade] 유닛 생성 실패 — tradeId={packet.tradeId}"
            );

            return false;
        }

        // 일반 장비 복원
        foreach (int itemId in packet.itemIds)
        {
            ItemData item =
                ItemDatabase.Instance != null
                    ? ItemDatabase.Instance.GetById(itemId)
                    : null;

            if (item == null || !receivedUnit.TryEquipItem(item))
            {
                Destroy(receivedUnit.gameObject);

                Debug.LogError(
                    $"[Trade] 일반 장비 복원 실패 — " +
                    $"tradeId={packet.tradeId}, itemId={itemId}"
                );

                return false;
            }
        }

        // 진화의 돌 장착 상태 복원
        if (packet.stoneId > 0)
        {
            EvolutionStoneData stone =
                EvolutionStoneDatabase.Instance != null
                    ? EvolutionStoneDatabase.Instance.GetById(packet.stoneId)
                    : null;

            PokemonData preStoneData =
                packet.preStonePokemonId > 0 &&
                PokemonDatabase.Instance != null
                    ? PokemonDatabase.Instance.GetById(
                        packet.preStonePokemonId
                    )
                    : null;

            if (stone == null || preStoneData == null)
            {
                Destroy(receivedUnit.gameObject);

                Debug.LogError(
                    $"[Trade] 진화의 돌 상태 복원 실패 — " +
                    $"tradeId={packet.tradeId}, " +
                    $"stone={packet.stoneId}, " +
                    $"base={packet.preStonePokemonId}"
                );

                return false;
            }

            receivedUnit.equippedStone = stone;
            receivedUnit.preStoneData = preStoneData;
        }

        // 이미 통신진화 완료 상태로 배송된 유닛은 그 상태를 그대로 복원하고,
        // 이번 수령으로 새로 통신진화 대상이 된 경우도 즉시 반영한다(지연 없음).
        receivedUnit.isTradeEvolved =
            packet.isTradeEvolved || isTradeEvolution;

        receivedUnit.ResetForBattle();

        if (!board.TryPlaceInBench(receivedUnit))
        {
            Destroy(receivedUnit.gameObject);

            Debug.LogWarning(
                $"[Trade] 벤치 배치 실패 — tradeId={packet.tradeId}"
            );

            return false;
        }

        /*
         * 여기까지 성공해야 유닛 소유권이 수신자에게 실제로 이동한 것이다.
         *
         * 상점 풀:
         * - 새 구매가 아니므로 추가 차감 없음
         * - 판매가 아니므로 반환 없음
         *
         * 아이템:
         * - 인벤토리에서 새로 꺼내지 않음
         * - 송신자의 인벤토리로 반환하지 않음
         * - 패킷에 귀속된 상태 그대로 복원
         */

        _incomingTradeQueue.Dequeue();
        GameEvents.TradeQueueChanged(_incomingTradeQueue.Count);

        GameManager.TryGet(out var gm);

        if (isTradeEvolution && evolvedData != null)
        {
            // 수령 유닛은 벤치에만 생성되고 전투에 영향을 주지 않으므로 지연 없이 즉시 진화 처리한다.
            ApplyTradeEvolutionToOwnedUnits(
                originalData,
                evolvedData,
                receivedUnit
            );

            gm?.Shop?.ActivateTradeEvolution(
                originalData.id,
                evolvedData.id
            );

            board.RecheckEvolution(receivedUnit);
        }
        else if (packet.isTradeEvolved)
        {
            /*
             * 이미 통신진화가 완료된 유닛을 재전송받은 경우:
             * 새 진화 사건이 아니므로 해금/광역진화는 발생시키지 않고,
             * 판매 시 원본 종 풀 반환을 위한 역매핑만 등록한다.
             */
            string baseNameEn =
                tradeEvolution != null
                    ? tradeEvolution.GetBaseOf(originalData.pokemonNameEn)
                    : null;

            PokemonData tradeBaseData =
                !string.IsNullOrEmpty(baseNameEn) &&
                PokemonDatabase.Instance != null
                    ? PokemonDatabase.Instance.GetByNameEn(baseNameEn)
                    : null;

            if (tradeBaseData != null)
            {
                gm?.Shop?.RegisterEvolvedToBase(
                    originalData.id,
                    tradeBaseData.id
                );
            }
            else
            {
                Debug.LogWarning(
                    $"[Trade] 통신진화체 '{originalData.pokemonNameEn}'의 " +
                    "원본 종 역조회 실패 — 판매 풀 반환이 어긋날 수 있음"
                );
            }
        }

        // 진화체로 받은 경우에만 연출. 베이스 핸드오버는 진화가 아니다. 반드시 위 if/else if 블록
        // (합체를 유발할 수 있는 마지막 지점 — isTradeEvolution 분기의 ApplyTradeEvolutionToOwnedUnits
        // + board.RecheckEvolution(receivedUnit), 또는 이미 통신진화된 유닛 재전송 분기)이 전부 끝난
        // "합체 여부가 확정된" 뒤에 발행해야 한다. TryPlaceInBench 직후(합체 확정 전)에 발행했더니
        // (2026-08 최초 수정) 이 시점엔 아직 receivedUnit이 벤치에 살아있어 정상적으로 발행됐지만,
        // 뒤이어 ApplyTradeEvolutionToOwnedUnits가 기존 보유 유닛들을 같은 진화체로 바꾸고
        // board.RecheckEvolution(receivedUnit)이 그제서야 3마리 합체를 성사시켜, receivedUnit이
        // 뒤늦게 합체 재료로 소비되고 BoardManager.ExecuteMerge가 최종 결과 유닛으로 또 한 번
        // GameEvents.UnitEvolved를 발행하면서 벤치 위치에 중복 VFX가 남는 문제가 실측(런타임 로그)으로
        // 확인됐다(2026-08 QA). receivedUnit이 지금(모든 합체 트리거가 끝난 뒤)도 실제로 벤치에
        // 남아 있는지를 기존 공개 조회 API(GetUnitsInBench)로 다시 확인해, 합체로 소비됐다면
        // (stillInBenchAfterRecheck=false) 여기서 발행하지 않는다 — 그 경우 BoardManager.ExecuteMerge가
        // 이미 살아있는 최종 결과 유닛 기준으로 GameEvents.UnitEvolved를 발행했다.
        bool stillInBenchAfterRecheck = board.GetUnitsInBench().Contains(receivedUnit);
        if (stillInBenchAfterRecheck && receivedUnit != null && receivedUnit.isTradeEvolved)
            GameEvents.UnitEvolved(receivedUnit, false);

        /*
         * 유닛 생성, 장비 복원, 진화 상태 복원, 벤치 배치가
         * 전부 성공한 뒤에만 해당 tradeId ACK를 보낸다.
         */
        photonView.RPC(
            nameof(RPC_TradeQueueAck),
            RpcTarget.Others,
            packet.tradeId
        );

        string evolutionText =
            isTradeEvolution &&
            evolvedData != null
                ? " → " + evolvedData.pokemonName
                : "";

        // 진화 판정에는 더 이상 사용하지 않고, 수령 로그 기록용으로만 남긴다.
        string originText =
            packet.wasOnBoard
                ? " (필드 출신 유닛 수령)"
                : "";

        Debug.Log(
            $"[Trade] 수령 완료: tradeId={packet.tradeId}, " +
            $"{originalData.pokemonName}{evolutionText} " +
            $"★{receivedUnit.starLevel}{originText}, " +
            $"남은 대기 {_incomingTradeQueue.Count}마리"
        );

        GameEvents.TradeUnitReceived(receivedUnit);

        return true;
    }

    /// <summary>
    /// 통신진화 수령 시 받는 플레이어가 보유한 동일 원본 유닛을 진화시킨다.
    ///
    /// 쇼핑 상태:
    /// - 필드 유닛 즉시 진화
    /// - 벤치 유닛 즉시 진화
    ///
    /// 전투 상태:
    /// - 필드 유닛은 다음 쇼핑 상태까지 진화 대기
    /// - 벤치 유닛은 즉시 진화
    /// </summary>
    private void ApplyTradeEvolutionToOwnedUnits(
        PokemonData originalData,
        PokemonData evolvedData,
        PokemonUnit receivedUnit)
    {
        BoardManager board =
            GameManager.TryGet(out var gm) ? gm.Board : null;

        if (board == null ||
            originalData == null ||
            evolvedData == null)
        {
            return;
        }

        int changedCount = 0;
        int pendingCount = 0;

        // 필드 유닛
        foreach (PokemonUnit unit in board.GetUnitsOnBoard())
        {
            if (unit == null ||
                unit == receivedUnit ||
                unit.data == null ||
                unit.data.id != originalData.id)
            {
                continue;
            }

            if (_isBattlePhase)
            {
                pendingCount++;
                continue;
            }

            if (TryApplyTradeEvolution(
                    unit,
                    receivedUnit,
                    originalData,
                    evolvedData))
            {
                changedCount++;
                GameEvents.UnitPlaced(unit);
            }
        }



        // 벤치 유닛은 전투 중에도 즉시 진화
        foreach (PokemonUnit unit in board.GetUnitsInBench())
        {
            if (TryApplyTradeEvolution(
                    unit,
                    receivedUnit,
                    originalData,
                    evolvedData))
            {
                changedCount++;
                GameEvents.UnitBenched(unit);
            }
        }

        if (_isBattlePhase && pendingCount > 0)
        {
            AddPendingTradeEvolution(
                originalData.id,
                evolvedData.id
            );
        }

        Debug.Log(
            $"[TradeEvolution] {originalData.pokemonName} → " +
            $"{evolvedData.pokemonName} 즉시 진화 {changedCount}마리, " +
            $"필드 진화 대기 {pendingCount}마리"
        );
    }

    private static bool TryApplyTradeEvolution(
        PokemonUnit unit,
        PokemonUnit receivedUnit,
        PokemonData originalData,
        PokemonData evolvedData)
    {
        if (unit == null ||
            unit == receivedUnit ||
            unit.data == null ||
            unit.data.id != originalData.id)
        {
            return false;
        }

        unit.data = evolvedData;
        unit.isTradeEvolved = true;
        unit.RefreshVisual(); // data(종) 변경 직후 모델 갱신 — PokemonUnit.TryEquipStone과 동일 관례
                               // (2026-08 QA 확인: 이게 없으면 데이터는 진화체로 바뀌어도 화면 모델은
                               // 그대로 남는다. ExecuteMerge도 새 유닛 생성 뒤 RefreshVisual을 호출함).
        unit.ResetForBattle();

        return true;
    }

    /// <summary>
    /// 전투 중 지연된 통신진화 정보를 등록한다.
    /// 같은 원본/진화체 조합은 중복 저장하지 않는다.
    /// </summary>
    private void AddPendingTradeEvolution(
        int originalPokemonId,
        int evolvedPokemonId)
    {
        foreach (PendingTradeEvolution pending
                 in _pendingTradeEvolutions)
        {
            if (pending.originalPokemonId == originalPokemonId &&
                pending.evolvedPokemonId == evolvedPokemonId)
            {
                return;
            }
        }

        _pendingTradeEvolutions.Add(
            new PendingTradeEvolution
            {
                originalPokemonId = originalPokemonId,
                evolvedPokemonId = evolvedPokemonId
            }
        );

        Debug.Log(
            $"[TradeEvolution] 진화 대기 등록: " +
            $"{originalPokemonId} → {evolvedPokemonId}"
        );
    }

    /// <summary>
    /// 쇼핑 페이즈 진입 시 전투 중 지연된 통신진화를 적용한다.
    /// </summary>
    private void ApplyPendingTradeEvolutions()
    {
        if (_pendingTradeEvolutions.Count == 0)
            return;

        BoardManager board =
            GameManager.TryGet(out var gm) ? gm.Board : null;

        PokemonDatabase database = PokemonDatabase.Instance;

        if (board == null || database == null)
        {
            Debug.LogWarning(
                "[TradeEvolution] 대기 진화 적용 실패 — " +
                "BoardManager 또는 PokemonDatabase 없음"
            );

            return;
        }

        int changedCount = 0;
        var changedUnits = new List<PokemonUnit>();

        foreach (PendingTradeEvolution pending
                 in _pendingTradeEvolutions)
        {
            PokemonData originalData =
                database.GetById(pending.originalPokemonId);

            PokemonData evolvedData =
                database.GetById(pending.evolvedPokemonId);

            if (originalData == null || evolvedData == null)
            {
                Debug.LogError(
                    $"[TradeEvolution] 대기 진화 데이터 조회 실패 " +
                    $"({pending.originalPokemonId} → " +
                    $"{pending.evolvedPokemonId})"
                );

                continue;
            }

            foreach (PokemonUnit unit in board.GetUnitsOnBoard())
            {
                if (!TryApplyTradeEvolution(
                        unit,
                        null,
                        originalData,
                        evolvedData))
                {
                    continue;
                }

                changedCount++;
                changedUnits.Add(unit);
                GameEvents.UnitPlaced(unit);
            }

            foreach (PokemonUnit unit in board.GetUnitsInBench())
            {
                if (!TryApplyTradeEvolution(
                        unit,
                        null,
                        originalData,
                        evolvedData))
                {
                    continue;
                }

                changedCount++;
                changedUnits.Add(unit);
                GameEvents.UnitBenched(unit);
            }
        }

        _pendingTradeEvolutions.Clear();

        foreach (PokemonUnit unit in changedUnits)
        {
            if (unit != null)
                board.RecheckEvolution(unit);
        }

        Debug.Log(
            $"[TradeEvolution] 쇼핑 페이즈 진입 — " +
            $"대기 유닛 {changedCount}마리 진화 완료"
        );
    }

    // ─────────────────────────────────────────
    // 통신기 골드 전송 (선차감 → 상대 수신 → ack, 실패 시 환급)
    // ─────────────────────────────────────────

    /// <summary>ack 대기 중인 송금액. 0 = 대기 없음. 실패 ack 시 이만큼 환급.</summary>
    private int _pendingGoldTransfer;

    /// <summary>
    /// 내 골드를 파트너에게 전송. 골드는 각자 로컬 권위(ShopManager)라 통신교환과 같은
    /// 피어 ack 모델을 쓴다 — 보내는 쪽이 자기 잔액을 검증·선차감하고, 받는 쪽이 자기 권위로 가산.
    /// 선차감이라 ack 대기 중 이중 송금/초과 지출이 불가능하고, 실패 ack 시 전액 환급된다.
    /// </summary>
    public void SendGoldToPartner(int amount)
    {
        if (amount <= 0) return;
        if (_soloMode || !PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[GoldTransfer] 파트너 없음 — 전송 불가");
            GameEvents.GoldTransferRejected("파트너 없음");
            return;
        }
        if (_pendingGoldTransfer > 0)
        {
            Debug.LogWarning("[GoldTransfer] 이전 전송 처리 중");
            GameEvents.GoldTransferRejected("이전 전송 처리 중");
            return;
        }

        // 유예시간(재접속 대기) 중인 파트너에게 보낸 RPC는 버퍼되지 않고 유실됨 — 골드 증발 방지.
        var others = PhotonNetwork.PlayerListOthers;
        if (others == null || others.Length == 0 || others[0].IsInactive)
        {
            Debug.LogWarning("[GoldTransfer] 파트너 연결 끊김 — 전송 불가");
            GameEvents.GoldTransferRejected("파트너 연결 끊김");
            return;
        }

        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        if (shop == null || shop.Gold < amount)
        {
            Debug.LogWarning($"[GoldTransfer] 골드 부족 — 보유 {(shop != null ? shop.Gold : 0)} < 요청 {amount}");
            GameEvents.GoldTransferRejected("골드 부족");
            return;
        }

        shop.AddGold(-amount); // 선차감 (OnGoldChanged → BoardSyncBroadcaster가 파트너 표시 동기화)
        _pendingGoldTransfer = amount;
        Debug.Log($"[GoldTransfer] 전송 요청: {amount}G → 파트너");
        photonView.RPC(nameof(RPC_GoldReceive), RpcTarget.Others, amount);
    }

    private void HandleGoldTransferRequested(int amount)
    {
        SendGoldToPartner(amount);
    }

    [PunRPC]
    private void RPC_GoldReceive(int amount)
    {
        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        if (amount <= 0 || shop == null)
        {
            Debug.LogWarning($"[GoldTransfer] 수신 거부 (amount={amount}, shop={(shop != null)})");
            photonView.RPC(nameof(RPC_GoldTransferAck), RpcTarget.Others, false);
            return;
        }

        shop.AddGold(amount);
        Debug.Log($"[GoldTransfer] 수신: 파트너에게서 {amount}G");
        GameEvents.PartnerGoldReceived(amount);
        photonView.RPC(nameof(RPC_GoldTransferAck), RpcTarget.Others, true);
    }

    [PunRPC]
    private void RPC_GoldTransferAck(bool success)
    {
        if (_pendingGoldTransfer <= 0) return;
        int amount = _pendingGoldTransfer;
        _pendingGoldTransfer = 0;

        if (!success)
        {
            var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
            shop?.AddGold(amount); // 환급
            Debug.LogWarning($"[GoldTransfer] 전송 실패 — {amount}G 환급");
            GameEvents.GoldTransferRejected("상대 수신 실패 — 환급됨");
            return;
        }

        Debug.Log($"[GoldTransfer] 전송 완료: {amount}G");
        GameEvents.GoldTransferCompleted(amount);
    }

    // ─────────────────────────────────────────
    // 상태 동기화 (보드 미러 / 골드 / 팀 HP)
    // ─────────────────────────────────────────

    /// <summary>현재 팀 공통 HP. Room CustomProperties에서 읽음. 미설정이면 -1.</summary>
    public int TeamHealth
    {
        get
        {
            if (_soloMode || !PhotonNetwork.InRoom) return _soloTeamHp;
            var props = PhotonNetwork.CurrentRoom?.CustomProperties;
            if (props != null && props.TryGetValue(TEAM_HP_PROP_KEY, out object hp))
                return (int)hp;
            return -1;
        }
    }

    private int _soloTeamHp = -1; // 솔로 모드용 로컬 팀 HP 저장

    /// <summary>
    /// 내 보드 배치 스냅샷을 상대 클라이언트에게 송출(미러 렌더용). 상대에게만 전송.
    /// 보드/벤치가 바뀔 때 BoardSyncBroadcaster가 호출한다.
    /// </summary>
    public void BroadcastBoardSnapshot(int[] data)
    {
        if (_soloMode) return; // 파트너 없음 — 송출 불필요
        if (!PhotonNetwork.InRoom) return;
        photonView.RPC(nameof(RPC_OnBoardSnapshot), RpcTarget.Others, ++_localBoardRevision, data);
    }

    /// <summary>
    /// 파트너 관전(PartnerSpectateView.SetExpanded(true))을 열 때 호출 — 상대에게 "지금 보드를
    /// 다시 보내달라"고만 요청한다(요청 RPC 자체엔 보드 데이터를 싣지 않음). 게임 최초 진입 시의
    /// 자동 push(BoardSyncBroadcaster의 OnUnitPlaced/OnUnitBenched 트리거)가 어떤 이유로든
    /// 유실·지연되더라도, 관전을 여는 시점에 최신 상태를 한 번 더 확보하기 위한 pull 경로다
    /// (2026-08 파트너 화면 초기 동기화 문제 대응).
    /// 실제 재송출은 기존 재접속 재동기화와 동일한 경로(GameEvents.BoardResyncRequested →
    /// BoardSyncBroadcaster.MarkBoardDirtyForResync → LateUpdate → BroadcastBoardSnapshot)를
    /// 그대로 타므로 revision 증가/비교 로직도 손대지 않는다.
    /// </summary>
    public void RequestPartnerBoardSnapshot()
    {
        if (_soloMode) return; // 파트너 없음 — 요청 불필요
        if (!PhotonNetwork.InRoom) return;
        photonView.RPC(nameof(RPC_RequestBoardSnapshot), RpcTarget.Others);
    }

    /// <summary>파트너의 재송출 요청 수신 — 기존 재접속 재동기화와 동일한 신호를 그대로 발행한다.</summary>
    [PunRPC]
    private void RPC_RequestBoardSnapshot()
    {
        GameEvents.BoardResyncRequested();
    }

    /// <summary>
    /// 로컬 BattleSnapshot을 파트너에게 1회 전송한다. BroadcastBoardSnapshot과 같은 가드/전송 패턴
    /// (_soloMode/InRoom 체크, RpcTarget.Others, revision 단조 증가)을 따르되, 호출측(QA 진입점)이
    /// 성공 여부를 바로 로그로 남길 수 있도록 bool을 반환한다.
    /// 이번 단계에서는 자동 트리거가 없다 — 오직 명시적으로 호출됐을 때만 전송한다.
    /// </summary>
    public bool BroadcastBattleSnapshot(BattleSnapshot snapshot)
    {
        if (snapshot == null) return false;
        if (_soloMode) return false;      // 파트너 없음 — 송출 불가
        if (!PhotonNetwork.InRoom) return false;

        BattleSnapshotCodec.EncodedPayload payload = BattleSnapshotCodec.Encode(snapshot);
        photonView.RPC(
            nameof(RPC_OnBattleSnapshot),
            RpcTarget.Others,
            ++_localBattleSnapshotRevision,
            payload.ints,
            payload.floats,
            payload.strings
        );
        return true;
    }

    /// <summary>
    /// 게임 씬 로드 완료를 알림(라운드 시작 핸드셰이크). GameSceneBootstrap이 GameScene 진입 시 호출.
    /// 두 클라가 모두 준비되면 MasterClient가 라운드 1을 시작한다.
    /// (로비→게임 전환 중 라운드 시작 RPC가 유실되는 레이스 방지)
    /// </summary>
    public void NotifySceneReady()
    {
        if (_soloMode) { BroadcastRoundStart(1); return; }
        if (!PhotonNetwork.InRoom || _isLeavingRoom) return;
        var props = new Hashtable { { SCENE_READY_PROP_KEY, true } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    /// <summary>내 골드를 Player CustomProperties에 기록 → 파트너 클라가 표시.</summary>
    public void SyncLocalGold(int gold)
    {
        if (_soloMode || !PhotonNetwork.InRoom || _isLeavingRoom) return;
        var props = new Hashtable { { GOLD_PROP_KEY, gold } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    /// <summary>내 누적 증강 목록(영문명, 선택 순)을 Player CustomProperties에 기록 → 파트너 클라가 표시/전적 기록.</summary>
    public void SyncLocalAugments(string[] augmentNamesEn)
    {
        if (_soloMode || !PhotonNetwork.InRoom || _isLeavingRoom) return;
        var props = new Hashtable { { AUGMENTS_PROP_KEY, augmentNamesEn ?? System.Array.Empty<string>() } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    /// <summary>내 레벨/현재 XP를 Player CustomProperties에 기록(재접속 복원용). 두 값은 서로 연관돼
    /// 있으므로 한 번의 SetCustomProperties 호출로 함께 저장한다.</summary>
    public void SyncLocalProgression(int level, int currentXp)
    {
        if (_soloMode || !PhotonNetwork.InRoom || _isLeavingRoom) return;
        var props = new Hashtable { { LEVEL_PROP_KEY, level }, { XP_PROP_KEY, currentXp } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    /// <summary>내 무료 리롤 잔여 횟수를 Player CustomProperties에 기록(재접속 복원용).</summary>
    public void SyncLocalRerollCount(int rerollCount)
    {
        if (_soloMode || !PhotonNetwork.InRoom || _isLeavingRoom) return;
        var props = new Hashtable { { REROLL_COUNT_PROP_KEY, rerollCount } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    /// <summary>내 아이템 상점 스냅샷(JSON)을 Player CustomProperties에 기록(재접속 복원용).
    /// 아이템 상점은 공유 풀 권위와 무관한 개인 로컬 상태라 Room이 아닌 Player 속성에 저장한다.</summary>
    public void SyncLocalItemShopState(string snapshotJson)
    {
        if (_soloMode || !PhotonNetwork.InRoom || _isLeavingRoom) return;
        if (string.IsNullOrEmpty(snapshotJson)) return;
        var props = new Hashtable { { ITEM_SHOP_STATE_PROP_KEY, snapshotJson } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    /// <summary>팀 공통 HP 초기화(MasterClient만, 아직 미설정일 때). PlayerHealthManager가 게임 시작 시 호출.</summary>
    public void InitTeamHealth(int hp)
    {
        if (_soloMode) { _soloTeamHp = hp; GameEvents.HealthChanged(hp); return; }
        if (!IsMasterClient || !PhotonNetwork.InRoom) return;
        if (TeamHealth >= 0) return; // 이미 설정됨(재접속 등)
        SetTeamHealthProp(hp);
    }

    /// <summary>
    /// 전투 패배 데미지를 팀 공통 HP에 반영 요청. 단일 기록자(MasterClient) 권위로 갱신.
    /// 비마스터는 RPC로 마스터에게 위임 → 두 클라가 동시에 써서 생기는 경합 방지.
    /// </summary>
    public void ReportBattleLoss(int damage)
    {
        if (_soloMode) { ApplyTeamDamageLocal(damage); return; }
        if (!PhotonNetwork.InRoom) return;

        if (IsMasterClient) ApplyTeamDamageLocal(damage);
        else photonView.RPC(nameof(RPC_ReportBattleLoss), RpcTarget.MasterClient, damage);
    }

    /// <summary>MasterClient에서만 실행: 현재 팀 HP를 읽어 데미지만큼 깎아 Room 속성에 기록.</summary>
    private void ApplyTeamDamageLocal(int damage)
    {
        if (DebugInfiniteTeamHealth)
        {
            Debug.Log("[Network] 디버그 무한 HP — 팀 데미지 무시");
            return;
        }

        if (_soloMode)
        {
            _soloTeamHp = Mathf.Max(0, _soloTeamHp - damage);
            GameEvents.HealthChanged(_soloTeamHp);
            if (_soloTeamHp <= 0) GameEvents.SessionEnded(SessionEndReason.TeamHpZero);
            return;
        }

        int current = TeamHealth;
        if (current < 0) return; // 아직 초기화 안 됨
        SetTeamHealthProp(Mathf.Max(0, current - damage));
    }

    private void SetTeamHealthProp(int hp)
    {
        if (_isLeavingRoom) return; // Leaving 중 SetProperties 금지
        var props = new Hashtable { { TEAM_HP_PROP_KEY, hp } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    // ─────────────────────────────────────────
    // RPC 수신
    // ─────────────────────────────────────────

    /// <summary>상대 보드 스냅샷 수신 → 미러 렌더용 이벤트 발행.
    /// 재접속 직후 재송출과 일반 송출이 교차 도착할 수 있어, 과거 revision은 버려 최신 미러를 보존한다.</summary>
    [PunRPC]
    private void RPC_OnBoardSnapshot(int revision, int[] data)
    {
        if (revision <= _lastOpponentBoardRevision)
        {
            Debug.Log($"[Network] 과거 보드 스냅샷 무시 (rev {revision} ≤ {_lastOpponentBoardRevision})");
            return;
        }
        _lastOpponentBoardRevision = revision;
        GameEvents.OpponentBoardChanged(BoardSnapshot.Decode(data));
    }

    /// <summary>
    /// 파트너 BattleSnapshot 수신. RPC_OnBoardSnapshot과 같은 원칙 — 과거 revision은 버려 최신을 보존한다.
    /// Decode 실패는 저장하지 않고 경고만 남긴다(기존 네트워크 로그 방식 — Debug.LogWarning).
    /// 이번 단계는 저장·이벤트 발행까지만 한다 — 미러 전투를 만들거나 전투 시작을 지연시키지 않는다.
    /// </summary>
    [PunRPC]
    private void RPC_OnBattleSnapshot(int revision, int[] ints, float[] floats, string[] strings)
    {
        if (revision <= _lastPartnerBattleSnapshotRevision)
        {
            Debug.Log($"[Network] 과거 BattleSnapshot 무시 (rev {revision} ≤ {_lastPartnerBattleSnapshotRevision})");
            return;
        }

        if (!BattleSnapshotCodec.TryDecode(ints, floats, strings, out BattleSnapshot decoded, out string error))
        {
            Debug.LogWarning($"[Network] BattleSnapshot Decode 실패(rev {revision}): {error}");
            return;
        }

        _lastPartnerBattleSnapshotRevision = revision;
        _partnerBattleSnapshot = decoded;
        GameEvents.PartnerBattleSnapshotChanged(decoded);
    }

    /// <summary>비마스터의 패배 데미지 요청을 마스터가 수신 → 팀 HP에 반영.</summary>
    [PunRPC]
    private void RPC_ReportBattleLoss(int damage)
    {
        if (!IsMasterClient) return;
        ApplyTeamDamageLocal(damage);
    }

    [PunRPC]
    private void RPC_OnRoundStart(int round)
    {
        Debug.Log($"[Network] 라운드 {round} 시작 수신");

        // 각 클라이언트가 자기 준비 상태 + 이번 라운드 전투 결과를 리셋
        var props = new Hashtable
        {
            { READY_PROP_KEY, false },
            { BATTLE_RESULT_PROP_KEY, RESULT_NOT_REPORTED }
        };
        if (round == 1) props[AUGMENTS_PROP_KEY] = System.Array.Empty<string>(); // 새 판 — 이전 판 증강 잔존 방지
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        _roundResultResolved = false; // (MasterClient 집계 가드 리셋)
        _lastLocalBattleResult = null; // 이번 라운드 전투 결과 캐시도 새로 시작
        _suppressedBattleResult = null; // QA 억제 테스트 잔여값도 라운드마다 새로
        // 새 라운드가 시작됐는데 직전 라운드의 "응답 불능" 진단이 아직 안 풀린 채로 남아있었다면
        // (예: QA "최종 라운드로 스킵" 버튼처럼 ResolveTeamRound를 거치지 않고 라운드가 바로 시작되는
        // 경로) 대기 모달을 닫아줄 신호를 여기서 대신 쏴야 한다 — 안 그러면 OptionsPanelUI가 그
        // 신호를 영영 못 받아 대기 모달이 멈춰있게 된다(2026-08-22 코드리뷰 지적).
        if (_partnerResultUnresponsive)
        {
            _partnerResultUnresponsive = false;
            GameEvents.PartnerResultRecovered();
        }
        if (_partnerUnresponsiveGraceRoutine != null)
        {
            StopCoroutine(_partnerUnresponsiveGraceRoutine);
            _partnerUnresponsiveGraceRoutine = null;
        }
        _lastKnownRound = round;      // 재접속 라운드 복구 기준점
        _tradeSentThisRound = false;  // 전송 기회는 라운드마다 새로 — 안 쓴 라운드는 이월되지 않는다

        if (round == 1)
        {
            // 새 판 — 보드 스냅샷 revision 리셋(이전 판의 revision과 비교되지 않도록)
            _localBoardRevision = 0;
            _lastOpponentBoardRevision = -1;

            // BattleSnapshot도 별도 필드로 관리하므로 새 판 시작 시 함께 리셋한다.
            _localBattleSnapshotRevision = 0;
            _lastPartnerBattleSnapshotRevision = -1;
            _partnerBattleSnapshot = null;
        }

        GameEvents.RoundChanged(round);
    }

    [PunRPC]
    private void RPC_OnAllPlayersReady()
    {
        Debug.Log("[Network] 2인 모두 준비 완료");
        GameEvents.AllPlayersReady();
    }

    [PunRPC]
    private void RPC_OnBattleStart()
    {
        Debug.Log("[Network] 전투 시작 수신");
        GameEvents.BattleStart();
    }

    [PunRPC]
    private void RPC_OnGameCleared()
    {
        Debug.Log("[Network] 챕터 완주 수신");
        GameEvents.GameCleared();
    }

    [PunRPC]
    private void RPC_OnTeamRoundResolved(int outcome)
    {
        GameEvents.TeamRoundResolved((TeamRoundOutcome)outcome);
    }

    /// <summary>솔로 모드(1인=팀) 즉시 판정. 승=BothWin, 패=BothLose(라이프 -1).</summary>
    private void ResolveSoloRound(bool isWin)
    {
        TeamRoundOutcome outcome = isWin ? TeamRoundOutcome.BothWin : TeamRoundOutcome.BothLose;
        if (!isWin) ApplyTeamDamageLocal(LIFE_LOSS_ON_TEAM_DEFEAT);
        GameEvents.TeamRoundResolved(outcome);
    }

    // ─────────────────────────────────────────
    // Photon 콜백
    // ─────────────────────────────────────────

    public override void OnConnectedToMaster()
    {
        if (_connectTimeoutRoutine != null)
        {
            StopCoroutine(_connectTimeoutRoutine);
            _connectTimeoutRoutine = null;
        }

        Debug.Log("[Network] Photon Master 서버 연결 완료");

        // 2026-08: 저장된 세션이 있어도 여기서 자동으로 재입장하지 않는다 — 타이틀 화면이
        // [이전 게임으로 들어가기]/[새로 시작하기] 선택지를 보여주고, 사용자가 명시적으로
        // AttemptRejoinSavedSession()/AbandonPreviousSession()을 호출할 때만 실제 재입장을 시도한다.
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("[Network] 로비 입장");
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"[Network] 룸 입장 완료 | 인원: {PlayerCount}/{MAX_PLAYERS}");
        Debug.Log(DumpNetworkDiagnostics("OnJoinedRoom"));

        if (_rejoinPurpose == RejoinPurpose.NotifyAbandonAndLeave)
        {
            // 게임 재참여가 아닌 포기 통지용 재입장.
            // 파트너에게 포기 상태만 전달하고 즉시 룸을 나간다.
            Debug.Log("[Network][Rejoin] 포기 통지용 재입장 성공 — 파트너에게 통지 후 재이탈");

            if (PhotonNetwork.PlayerListOthers.Length > 0)
                photonView.RPC(nameof(RPC_PartnerGaveUpReconnect), RpcTarget.Others);

            _rejoinPurpose = RejoinPurpose.None;
            PhotonNetwork.AutomaticallySyncScene = true;

            // 새로 시작하기 확정.
            // 저장된 재접속 세션을 제거하고 룸 종료.
            ClearSavedRejoinSession("새로 시작하기 확정(포기 통지 완료)");

            LeaveRoom();
            return;
        }

        // 이전 게임 재입장 여부 확인.
        // 이 값은 아래 OnRoomFull()에서 신규 게임 시작과 구분하기 위해 유지한다.
        bool wasRejoiningToEnterGame = _rejoinPurpose == RejoinPurpose.EnterGame;

        _rejoinPurpose = RejoinPurpose.None;
        _pendingRejoinRoomName = null;

        SaveRejoinSession();

        if (wasRejoiningToEnterGame)
        {
            // 이전 게임 재입장 성공.
            // 타이틀 씬에서 재입장한 경우에는 Photon 씬 동기화를 기다리지 않고
            // 직접 게임 씬으로 이동한 뒤 상태 복원을 진행한다.
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            if (currentScene != GAME_SCENE_NAME)
            {
                Debug.Log($"[Network][Rejoin] 재입장 성공 — 게임 씬으로 복귀 예약 ({currentScene} → {GAME_SCENE_NAME})");

                // 씬 이동 완료 후 ResyncAfterReconnect 실행.
                s_resyncAfterRejoinPending = true;
                s_isResumingRejoinedMatch = true;

                UnityEngine.SceneManagement.SceneManager.LoadScene(GAME_SCENE_NAME);
            }
            else
            {
                Debug.Log("[Network] 이전 세션 재입장 성공 — 상태 재동기화");

                _gameStarted = true;
                ResyncAfterReconnect();
            }
        }
        else
        {
            // 신규 게임 입장.
            // 이전 재접속 플래그가 남아있다면 제거한다.
            s_isResumingRejoinedMatch = false;
        }


        // 중요:
        // 재접속으로 룸에 다시 들어온 경우 OnRoomFull()을 호출하면
        // 기존 게임 복원이 아니라 신규 게임 시작 흐름이 실행될 수 있다.
        // 따라서 신규 입장일 때만 실행한다.
        if (!wasRejoiningToEnterGame && PlayerCount == MAX_PLAYERS)
        {
            OnRoomFull();
        }
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[Network] 룸 생성 실패 ({returnCode}): {message}");

        // 그 사이 다른 클라이언트가 같은 이름의 방을 먼저 만든 경우 → 그 방으로 입장
        if (returnCode == ErrorCode.GameIdAlreadyExists)
        {
            Debug.Log($"[Network] '{FALLBACK_ROOM_NAME}' 방이 이미 존재 → 입장 시도");
            JoinRoom(FALLBACK_ROOM_NAME);
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[Network] 룸 입장 실패 ({returnCode}): {message}");
        Debug.Log(DumpNetworkDiagnostics("OnJoinRoomFailed"));

        if (_rejoinPurpose == RejoinPurpose.NotifyAbandonAndLeave)
        {
            // 방이 없거나 PlayerTtl 만료 등으로 파트너에게 통지할 방법 자체가 사라짐 —
            // 알릴 상대가 없다고 보고 저장된 세션만 정리한다.
            Debug.LogWarning("[Network][Rejoin] 포기 통지용 재입장 실패 — 알릴 상대 없음, 세션 정리");
            _rejoinPurpose = RejoinPurpose.None;
            PhotonNetwork.AutomaticallySyncScene = true;
            ClearSavedRejoinSession("재접속 포기(재입장 실패)");
            PhotonNetwork.JoinLobby();
            return;
        }

        if (_rejoinPurpose == RejoinPurpose.EnterGame)
        {
            // 저장된 세션으로의 재입장이 실패함(PlayerTtl 만료 등) — 실패 사실만 알리고, 세션 정리는
            // 사용자가 안내 팝업을 확인(AcknowledgeRejoinFailure)할 때 한다.
            Debug.LogWarning("[Network][Rejoin] 재입장 실패");
            _rejoinPurpose = RejoinPurpose.None;
            RejoinFailed = true;
            // AttemptRejoinSavedSession()에서 꺼뒀던 것을 되돌린다 — 실패했으니 타이틀에 남아 이후
            // 정상 신규 입장(JoinOrCreateRoom/JoinRandomRoom)을 시도할 수 있고, 그 흐름은 원래대로
            // AutomaticallySyncScene=true를 전제로 한다.
            PhotonNetwork.AutomaticallySyncScene = true;
            PhotonNetwork.JoinLobby();
        }
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"[Network] 랜덤 룸 없음 → '{FALLBACK_ROOM_NAME}' 생성/입장 시도");
        CreateRoom(FALLBACK_ROOM_NAME);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"[Network] {newPlayer.NickName} 입장 | 인원: {PlayerCount}/{MAX_PLAYERS}");

        _gracePeriodExpiredFired = false;

        if (_opponentGraceRoutine != null)
        {
            StopCoroutine(_opponentGraceRoutine);
            _opponentGraceRoutine = null;
        }

        // _opponentGraceRoutine은 30초(GIVE_UP_AVAILABLE_DELAY) 후 스스로 null이 되므로,
        // "재접속했는지"는 코루틴 존재 여부가 아니라 _opponentDisconnected로 판단해야 한다.
        // 그래야 30초가 지나 무한 대기 중인 상태에서도(코루틴은 이미 끝났지만) 재접속 시 정상적으로 감지된다.
        if (_opponentDisconnected)
        {
            _opponentDisconnected = false;

            // 대기 중 열어뒀던 방을 다시 닫는다(포기 통지용 잠깐 재입장이어도 무해 — 뒤이은 재이탈 시
            // OnPlayerLeftRoom이 다시 열게 되므로 open/close 대칭이 유지됨).
            if (IsMasterClient)
                PhotonNetwork.CurrentRoom.IsOpen = false;

            // 상대가 실제로 게임을 이어가려는 게 아니라 "재접속 포기" 통지만 하러 잠깐 재입장했을 수
            // 있다 — 곧바로 재접속 성공(전투/타이머 재개)을 확정하지 않고 짧게 기다려, 그 사이
            // RPC_PartnerGaveUpReconnect가 도착하면 재접속 성공 처리를 취소한다.
            if (_reconnectConfirmRoutine != null)
                StopCoroutine(_reconnectConfirmRoutine);
            _reconnectConfirmRoutine = StartCoroutine(ConfirmReconnectAfterAbandonCheck());
        }

        // 진행 중 매치에 파트너가 재입장 — 파트너가 자리를 비운 사이 보낸 내 스냅샷 RPC는
        // 유실됐으므로 현재 보드를 재송출해 파트너 쪽 미러를 복구시킨다.
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(MATCH_GUID_ROOM_KEY))
            GameEvents.BoardResyncRequested();

        if (PlayerCount == MAX_PLAYERS)
            OnRoomFull();
    }

    /// <summary>
    /// 상대 재입장 후 REJOIN_ABANDON_CHECK_DELAY만큼 기다렸다가, 그 사이 포기 통지가 오지 않았으면
    /// 그때 재접속 성공을 확정 발행한다(전투/타이머 재개, 대기 모달 종료).
    /// </summary>
    private System.Collections.IEnumerator ConfirmReconnectAfterAbandonCheck()
    {
        yield return new WaitForSeconds(REJOIN_ABANDON_CHECK_DELAY);
        _reconnectConfirmRoutine = null;

        if (_partnerGaveUpReconnectNotice)
            yield break; // 포기 통지가 먼저 도착 — 재접속 성공 처리를 생략한다.

        Debug.Log($"[Network] 상대 재접속 성공 — 대기 종료 (IsAwaitingPartnerReconnect={_opponentDisconnected})");
        GameEvents.OpponentReconnected();
    }

    /// <summary>
    /// 저장된 이전 세션으로 잠깐 재입장한 파트너가 "게임을 이어가지 않고 포기하겠다"고 보내는 통지.
    /// RpcTarget.Others로 즉시 전송되므로 2인 방에서 상대 1명에게만 전달된다(RPC_GoldReceive와 동일 패턴).
    /// </summary>
    [PunRPC]
    private void RPC_PartnerGaveUpReconnect()
    {
        Debug.Log("[Network] 파트너가 재접속을 포기함 — 통지 수신");

        if (_reconnectConfirmRoutine != null)
        {
            StopCoroutine(_reconnectConfirmRoutine);
            _reconnectConfirmRoutine = null;
        }

        _partnerGaveUpReconnectNotice = true;
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"[Network] {otherPlayer.NickName} 퇴장 (Inactive: {otherPlayer.IsInactive})");

        // 응답 전 상대 이탈 — 항복 상태는 정리만 하고, 실제 패배 처리는 남은 이탈 흐름
        // ([포기하기] 선택 시 ConfirmPartnerDisconnectGiveUp → SessionEnded)에 맡긴다.
        _surrenderRequestSent = false;
        _surrenderRequestReceived = false;
        _surrenderRejectedNotice = false;
        _surrenderCrossCancelledNotice = false;

        // 이미 판이 끝난 뒤(GameOver/Victory)의 이탈은 새로 대기 흐름을 시작하지 않는다.
        if (GameManager.TryGet(out var gm) && gm.Phase != null &&
            (gm.Phase.CurrentPhase == GamePhase.GameOver || gm.Phase.CurrentPhase == GamePhase.Victory))
        {
            return;
        }

        // 이미 같은 이탈 건을 처리 중이면 무시한다 — Photon은 같은 이탈에 대해 OnPlayerLeftRoom을
        // 두 번 호출할 수 있다(처음엔 IsInactive=true로 연결 끊김 감지, 이후 PlayerTtl이 실제로
        // 서버에서 만료될 때 IsInactive=false로 한 번 더). 이 가드가 없으면 두 번째 호출 때마다
        // 30초 대기 타이머와 [포기하기] 노출 상태가 계속 리셋된다.
        if (_opponentDisconnected)
            return;

        // "응답 불능" 진단이 먼저 떠 있던 상태였는데 이제 진짜 이탈까지 확인됐다 — 더 확실한 신호가
        // 왔으니 우리 쪽 유예 타이머는 정리하고(중복 병행 방지), 아래 일반 이탈 처리가 처음부터
        // 새로 담당하게 넘긴다. 안 지우면 응답불능 유예 타이머(GameEvents.PartnerResultGiveUpAvailable)와
        // 방금 새로 시작될 실제 이탈 유예 타이머(GameEvents.OnGracePeriodExpired)가 동시에 돌게 된다
        // (2026-08-22 코드리뷰 지적).
        if (_partnerResultUnresponsive)
        {
            _partnerResultUnresponsive = false;
            if (_partnerUnresponsiveGraceRoutine != null)
            {
                StopCoroutine(_partnerUnresponsiveGraceRoutine);
                _partnerUnresponsiveGraceRoutine = null;
            }
        }

        // 2026-08 파트너 이탈 UX: 의도적 퇴장(타이틀로/게임종료로 인한 LeaveRoom)과 비정상 연결 끊김을
        // 더 이상 구분하지 않는다 — 남은 플레이어 입장에서는 어느 쪽이든 "파트너가 없다"는 동일한 상황이므로
        // 둘 다 같은 재접속 대기 흐름(무한 대기 → 30초 후 포기하기)을 시작한다.
        _opponentDisconnected = true;

        // 파트너가 없는 동안 저장된 BattleSnapshot을 그대로 들고 있으면 재접속 후(또는 새 스냅샷을
        // 아직 못 받은 상태에서) 이전 라운드의 오래된 상태를 쓸 위험이 있다 — 이탈 시점에 비운다.
        // revision도 함께 리셋해야 재접속 후 재전송이 "과거 revision"으로 잘못 걸러지지 않는다.
        _partnerBattleSnapshot = null;
        _lastPartnerBattleSnapshotRevision = -1;

        // OnRoomFull()이 매치 시작 시 방을 닫아둔 채라(IsOpen=false) 상대의 RejoinRoom이 "Game closed"로
        // 거부될 수 있다 — 대기 중엔 다시 열어 재접속을 허용한다. PlayerCount는 비활성 자리도 포함해
        // 이미 MAX_PLAYERS이므로 무관한 3자가 랜덤 매칭/방 리스트로 끼어들 위험은 없다("Game full"로 막힘).
        if (IsMasterClient)
            PhotonNetwork.CurrentRoom.IsOpen = true;

        if (_opponentGraceRoutine != null)
            StopCoroutine(_opponentGraceRoutine);
        _opponentGraceRoutine = StartCoroutine(OpponentGraceRoutine());

        GameEvents.OpponentDisconnected(RECONNECT_GRACE_PERIOD);
    }

    /// <summary>
    /// 상대방 이탈 후 [포기하기] 버튼을 노출해도 되는 시점까지의 대기(GIVE_UP_AVAILABLE_DELAY=30초).
    /// 시간 내 재접속하면 OnPlayerEnteredRoom에서 취소됨. 이 코루틴이 끝나도 자동으로 세션을 종료하지 않는다 —
    /// 이후로도 무한정 대기하며, 사용자가 [포기하기]→[타이틀로 이동]/[게임 종료]를 직접 선택해야 끝난다.
    /// </summary>
    private System.Collections.IEnumerator OpponentGraceRoutine()
    {
        yield return GraceDelayRoutine(
            () => _opponentGraceRoutine = null,
            () =>
            {
                bool bothDisconnected = !PhotonNetwork.IsConnectedAndReady;
                Debug.LogWarning($"[Network] 상대 이탈 30초 경과 — 포기하기 노출 가능 (둘 다 끊김: {bothDisconnected})");
                FireGracePeriodExpired(bothDisconnected);
            });
    }

    /// <summary>
    /// GIVE_UP_AVAILABLE_DELAY만큼 기다린 뒤 clearSelf()로 자기 자신을 가리키던 필드를 비우고
    /// onExpired를 부른다. OpponentGraceRoutine·PartnerUnresponsiveGraceRoutine이 공유하는
    /// "기다렸다가 아직 유효하면 알림" 패턴을 한 곳에 모은 것(2026-08-22 코드리뷰 지적 — 중복 제거).
    /// </summary>
    private System.Collections.IEnumerator GraceDelayRoutine(System.Action clearSelf, System.Action onExpired)
    {
        yield return new WaitForSeconds(GIVE_UP_AVAILABLE_DELAY);
        clearSelf();
        onExpired();
    }

    /// <summary>같은 이탈 건에 대해 GracePeriodExpired(포기 가능 알림)를 한 번만 발행한다. 파트너 재입장 시 리셋.</summary>
    private void FireGracePeriodExpired(bool bothDisconnected)
    {
        if (_gracePeriodExpiredFired) return;
        _gracePeriodExpiredFired = true;
        GameEvents.GracePeriodExpired(bothDisconnected);
    }

    private void OnRoomFull()
    {
        // 2인 모두 입장 → 방 닫고 게임 시작
        Debug.Log("[Network] 2인 모두 입장 — 게임 시작");

        if (!IsMasterClient) return;

        // 재접속 시 OnJoinedRoom/OnPlayerEnteredRoom이 다시 호출될 수 있다.
        // 이미 생성된 매치라면 씬을 다시 로드하지 않아 공유 상점 풀과 예약을 보존한다.
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(MATCH_GUID_ROOM_KEY))
        {
            Debug.Log("[Network] 기존 매치 재입장 — 게임 씬 재로드 생략");
            return;
        }

        PhotonNetwork.CurrentRoom.IsOpen = false;

        // 이번 판의 matchId(GUID)를 Room 속성으로 배포 — 두 클라이언트가 같은 값으로 전적을 묶는다.
        // 씬 로드 전에 설정해 게임 시작 시점엔 양쪽 모두 동기화돼 있음.
        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable { { MATCH_GUID_ROOM_KEY, System.Guid.NewGuid().ToString("N") } });

        PhotonNetwork.LoadLevel(GAME_SCENE_NAME);
        // 라운드 1 시작은 여기서 하지 않는다 — 씬 전환 중 RPC 유실 방지를 위해
        // 두 클라가 GameScene 로드를 마치고 SceneReady를 올리면(OnPlayerPropertiesUpdate) 그때 시작.
    }

    /// <summary>
    /// Room CustomProperties의 ROUND_PROP_KEY가 유효한 진행 라운드(1 이상)를 갖고 있는지.
    /// 씬 로드 핸드셰이크가 재접속으로 인한 SceneReady 재갱신을 신규 매치 시작과 구분하는 서버 권위
    /// 기준(2026-08) — 클라이언트별 휘발성 필드(_gameStarted)보다 우선한다. Room이 아직 없거나
    /// 라운드 값이 예상 타입이 아니면 방어적으로 false.
    /// </summary>
    private bool HasActiveRoundInRoom()
    {
        return TryGetCurrentRoundFromRoom(out int round) && round >= 1;
    }

    /// <summary>Room CustomProperties의 ROUND_PROP_KEY를 안전하게 정수로 읽는다. Room이 없거나
    /// 값이 없거나 예상 타입이 아니면 false(round는 0).</summary>
    private bool TryGetCurrentRoundFromRoom(out int round)
    {
        round = 0;
        if (PhotonNetwork.CurrentRoom == null) return false;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROUND_PROP_KEY, out object roundValue)) return false;

        try { round = System.Convert.ToInt32(roundValue); return true; }
        catch (System.Exception) { return false; }
    }

    /// <summary>
    /// 플레이어의 CustomProperties(준비 상태)가 바뀔 때마다 호출됨.
    /// MasterClient만 검사 — 모든 플레이어가 준비 완료면 전체에 알림.
    /// _readyCount(로컬 변수) 대신 Player CustomProperties를 직접 조회하므로
    /// MasterClient가 교체돼도 준비 상태가 유실되지 않음.
    /// </summary>
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        // 파트너 골드 표시: 다른 플레이어의 Gold가 바뀌면 UI 갱신용 이벤트 발행(모든 클라).
        if (targetPlayer != PhotonNetwork.LocalPlayer &&
            changedProps.TryGetValue(GOLD_PROP_KEY, out object gold))
        {
            GameEvents.PartnerGoldChanged((int)gold);
        }

        // 파트너 증강 목록: 다른 플레이어의 Augments가 바뀌면 이벤트 발행(모든 클라).
        if (targetPlayer != PhotonNetwork.LocalPlayer &&
            changedProps.TryGetValue(AUGMENTS_PROP_KEY, out object augments) &&
            augments is string[] augmentNames)
        {
            GameEvents.PartnerAugmentsChanged(augmentNames);
        }

        // 준비 인원 집계 UI 갱신: 누구의 Ready든 바뀌면 전 클라에서 (준비 인원/총 인원)을 다시 센다.
        // 전투 시작 판정(AllPlayersHaveFlag)과 달리 이건 표시 전용이라 MasterClient 여부와 무관하게 발행.
        if (changedProps.ContainsKey(READY_PROP_KEY))
            GameEvents.ReadyCountChanged(CountReadyPlayers(), PlayerCount);

        // 이하 집계는 MasterClient만.
        if (!IsMasterClient) return;

        // 게임 씬 로드 핸드셰이크: 두 클라가 모두 SceneReady면 라운드 1 시작(1회만).
        // _gameStarted는 이 인스턴스의 휘발성 필드라, 신뢰할 수 없는 경로로 씬이 재생성되면(예: 재접속
        // 클라이언트의 SceneReady 재갱신이 AutomaticallySyncScene을 타고 넘어와 마스터 자신의 씬까지
        // 재생성되는 경우) false로 남을 수 있다. Room에 이미 진행 중인 라운드가 기록돼 있으면 신규 매치
        // 시작으로 오판하지 않도록 서버 권위(HasActiveRoundInRoom)로 한 번 더 방어한다
        // (2026-08 재접속 중 라운드1 중복 방송 확인 — RestoreLocalPlayerStateAfterReconnect 완료 후
        // 골드/무료 리롤/시작 유닛/유닛 상점이 RW001 보상으로 재지급되는 회귀).
        if (!_gameStarted && !HasActiveRoundInRoom() &&
            changedProps.ContainsKey(SCENE_READY_PROP_KEY) && AllPlayersHaveFlag(SCENE_READY_PROP_KEY))
        {
            _gameStarted = true;
            BroadcastRoundStart(1);
            return;
        }

        // 준비 완료 집계.
        if (changedProps.ContainsKey(READY_PROP_KEY) && AllPlayersHaveFlag(READY_PROP_KEY))
            photonView.RPC(nameof(RPC_OnAllPlayersReady), RpcTarget.All);

        // 전투 결과 집계: 두 플레이어가 모두 보고했으면 팀 결과 1회 판정.
        if (changedProps.ContainsKey(BATTLE_RESULT_PROP_KEY) && !_roundResultResolved && AllPlayersReportedResult())
            ResolveTeamRound();
    }

    /// <summary>
    /// RoundPhaseManager.ResultTimer의 대기 루프 중간(예: 20/25/30초 경과)에 반복 호출됨 —
    /// 아직 결과가 없는 플레이어에게 재전송을 요청한다. 이미 판정됐으면 아무 것도 안 한다.
    /// 몇 번을 불러도 안전(멱등) — 호출 빈도는 RoundPhaseManager 쪽에서 정한다.
    /// </summary>
    public void RequestBattleResultResendIfNeeded()
    {
        if (_soloMode || !IsMasterClient || _roundResultResolved) return;

        Debug.LogWarning("[Network] 팀 결과 일부 미수신 — 재전송 요청");
        photonView.RPC(nameof(RPC_RequestBattleResultResend), RpcTarget.Others);
    }

    /// <summary>재전송 요청 수신 — 이번 라운드 결과를 이미 계산해뒀으면 다시 보고한다.</summary>
    [PunRPC]
    private void RPC_RequestBattleResultResend()
    {
        if (!_lastLocalBattleResult.HasValue) return;
        if (_isLeavingRoom) return; // Leaving 중 SetProperties 금지(다른 SetCustomProperties 호출부와 동일 가드)

        Debug.Log("[Network] 팀 결과 재전송 요청 수신 — 내 결과 다시 보고");
        var props = new Hashtable { { BATTLE_RESULT_PROP_KEY, _lastLocalBattleResult.Value ? 1 : 0 } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    /// <summary>
    /// RoundPhaseManager.ResultTimer의 대기 루프에서 재전송 요청 후에도 오래(예: 35초) 결과가 안 오면
    /// 한 번 호출됨. 승패를 추측하지 않는다 — 대신 파트너가 Photon에는 연결돼 있는데 결과만 안 오는
    /// 상황인지 확인하고, 맞으면 "응답 불능"으로 진단해 기존 파트너 이탈 UX(대기 모달 → 유예 →
    /// [포기하기] 버튼)로 넘긴다. 최종 결정은 플레이어가 [포기하기]를 눌러야만 내려진다
    /// (2026-08-21 티켓 — 방장 결과로 승패를 추측하던 이전 방식은 라이프 차감이 잘못 스킵되는
    /// 비대칭 버그가 있어 제거함. PR #120 참고).
    /// </summary>
    public void DiagnosePartnerUnresponsiveIfNeeded()
    {
        if (_soloMode || !IsMasterClient || _roundResultResolved || _partnerResultUnresponsive) return;

        if (AllPlayersReportedResult())
        {
            ResolveTeamRound(); // 막판에 도착한 경우 — 정상 판정
            return;
        }

        // 방장 자신도 아직 결과를 안 보고했으면(전투가 안 끝났거나, QA 억제 토글을 자기 자신에게
        // 켜놨거나) 안 온 게 파트너 쪽이라고 단정할 근거가 없다 — 여기서 "파트너 응답 불능"으로
        // 잘못 진단하면 정작 문제는 내 쪽인데 상대를 탓하는 모달이 뜬다(2026-08-22 코드리뷰 지적).
        if (!_lastLocalBattleResult.HasValue) return;

        // 파트너가 진짜로 Photon 방을 나갔으면(IsInactive) OnPlayerLeftRoom 경로가 이미 처리 중이다 —
        // 여기서 또 진단을 띄우면 이미 뜬 "진짜 이탈" 모달 위에 우리 모달까지 겹쳐 뜨는 꼴이 된다.
        Player[] others = PhotonNetwork.PlayerListOthers;
        if (others == null || others.Length == 0 || others[0].IsInactive) return;

        _partnerResultUnresponsive = true;
        Debug.LogWarning("[Network] 팀 결과 재전송 후에도 미수신 — 파트너 응답 불능 진단, 이탈 UX로 위임");
        // OnOpponentDisconnected가 아니라 전용 이벤트를 쏜다 — OnOpponentDisconnected는 RoundPhaseManager
        // (페이즈 타이머 강제 정지 — 지금 이 함수를 부르고 있는 ResultTimer 코루틴 자신이 죽어버림),
        // PartnerBattleMirrorController(미러 전투 중단), PartnerSpectateView(관전 화면 강제 종료)도
        // 함께 구독하는데, 이들은 전부 "진짜로 자리를 비웠다"는 전제의 부작용이라 이 상황엔 안 맞는다
        // (2026-08-22 코드리뷰 지적). OptionsPanelUI만 듣는 GameEvents.PartnerResultUnresponsive로 분리.
        GameEvents.PartnerResultUnresponsive();

        if (_partnerUnresponsiveGraceRoutine != null)
            StopCoroutine(_partnerUnresponsiveGraceRoutine);
        _partnerUnresponsiveGraceRoutine = StartCoroutine(PartnerUnresponsiveGraceRoutine());
    }

    /// <summary>OpponentGraceRoutine과 동일한 타이밍(GIVE_UP_AVAILABLE_DELAY)으로 [포기하기] 노출을 알린다.
    /// 트리거가 실제 Photon 이탈이 아니므로 별도 코루틴 + 별도 이벤트(GameEvents.PartnerResultGiveUpAvailable)로
    /// 독립 운영한다. 이 코루틴은 진단(DiagnosePartnerUnresponsiveIfNeeded)당 하나만 존재하므로
    /// OpponentGraceRoutine의 _gracePeriodExpiredFired 같은 별도 1회 발행 가드가 필요 없다.</summary>
    private System.Collections.IEnumerator PartnerUnresponsiveGraceRoutine()
    {
        yield return GraceDelayRoutine(
            () => _partnerUnresponsiveGraceRoutine = null,
            () =>
            {
                // 대기 중 정상 판정(ResolveTeamRound)이나 실제 이탈 핸드오프(OnPlayerLeftRoom)로
                // 이미 꺼졌을 수 있다 — 그때는 알림을 쏘지 않는다.
                if (!_partnerResultUnresponsive) return;

                Debug.LogWarning("[Network] 파트너 응답 불능 30초 경과 — 포기하기 노출 가능");
                GameEvents.PartnerResultGiveUpAvailable();
            });
    }

    /// <summary>모든 플레이어가 이번 라운드 전투 결과를 보고했는지(-1=미보고).</summary>
    private bool AllPlayersReportedResult()
    {
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (!player.CustomProperties.TryGetValue(BATTLE_RESULT_PROP_KEY, out object v) ||
                (int)v == RESULT_NOT_REPORTED)
                return false;
        }
        return true;
    }

    /// <summary>
    /// MasterClient: 두 플레이어 승패를 집계해 팀 결과 판정 → 라이프 차감 + 전체 브로드캐스트.
    /// 승리 수: 2=BothWin, 1=Split, 0=BothLose. 라이프 차감은 BothWin(둘 다 승리)이 아닌 한 항상 발생한다 —
    /// 한 명만 져도(Split) 라운드 상관없이 즉시 -1(과거 최종보스 5라운드 한정 승격 규칙은 이 일반 규칙에
    /// 흡수되어 제거됨 — outcome을 구분해 쓰는 구독자가 없어 Split→BothLose 승격 자체가 더 이상 의미 없었음).
    /// </summary>
    private void ResolveTeamRound()
    {
        _roundResultResolved = true;

        // "파트너 응답 불능" 진단이 떠 있던 상태였다면(DiagnosePartnerUnresponsiveIfNeeded 참고), 지금
        // 정상적으로 결과가 모여 판정하는 거니까 그 진단을 취소하고 대기 UI를 되돌린다. 파트너가 실제로
        // Photon 방을 나간 게 아니라서 OnPlayerEnteredRoom이 다시 불릴 일이 없다 — 이 복구를 직접 안
        // 해주면 파트너가 정상 복귀해도 화면이 "대기 중"에 계속 멈춰있게 된다.
        if (_partnerResultUnresponsive)
        {
            _partnerResultUnresponsive = false;
            if (_partnerUnresponsiveGraceRoutine != null)
            {
                StopCoroutine(_partnerUnresponsiveGraceRoutine);
                _partnerUnresponsiveGraceRoutine = null;
            }
            // OnOpponentReconnected가 아니라 전용 이벤트로 닫는다 — RoundPhaseManager가
            // OnOpponentReconnected를 들으면 지금 이 함수를 부르고 있는 ResultTimer를 처음부터
            // 다시 시작시켜버린다(불필요한 재시작). OptionsPanelUI만 듣는 GameEvents.PartnerResultRecovered로 분리.
            GameEvents.PartnerResultRecovered();
        }

        int wins = 0;
        foreach (var player in PhotonNetwork.PlayerList)
            if (player.CustomProperties.TryGetValue(BATTLE_RESULT_PROP_KEY, out object v) && (int)v == 1)
                wins++;

        TeamRoundOutcome outcome = wins >= 2 ? TeamRoundOutcome.BothWin
                                 : wins == 1 ? TeamRoundOutcome.Split
                                 : TeamRoundOutcome.BothLose;

        if (outcome != TeamRoundOutcome.BothWin)
            ApplyTeamDamageLocal(LIFE_LOSS_ON_TEAM_DEFEAT); // 라이프 -1 (마스터 권위)

        Debug.Log($"[Network] 팀 라운드 결과: {outcome} (승 {wins}명)");
        photonView.RPC(nameof(RPC_OnTeamRoundResolved), RpcTarget.All, (int)outcome);
    }

    /// <summary>모든 플레이어의 해당 bool CustomProperty가 true인지.</summary>
    private bool AllPlayersHaveFlag(string key)
    {
        foreach (var player in PhotonNetwork.PlayerList)
        {
            bool on = player.CustomProperties.TryGetValue(key, out object v) && (bool)v;
            if (!on) return false;
        }
        return true;
    }

    /// <summary>READY_PROP_KEY가 true인 플레이어 수. 준비 인원 표시(n/총) 집계용.</summary>
    private int CountReadyPlayers()
    {
        int count = 0;
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.CustomProperties.TryGetValue(READY_PROP_KEY, out object v) && (bool)v)
                count++;
        }
        return count;
    }

    /// <summary>팀 공통 HP(Room 속성) 변경 수신 → 모든 클라가 UI 갱신, 0 이하면 세션 종료.</summary>
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (!propertiesThatChanged.TryGetValue(TEAM_HP_PROP_KEY, out object hp)) return;

        int health = (int)hp;
        GameEvents.HealthChanged(health);
        if (health <= 0)
        {
            Debug.LogWarning("[Network] 팀 공통 HP 0 — 세션 종료(게임오버)");
            GameEvents.SessionEnded(SessionEndReason.TeamHpZero);
        }
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        Debug.Log($"[Network] 마스터 클라이언트 변경 → {newMasterClient.NickName}");

        // 로비 방 목록에 표시되는 방장 닉네임(HostNickname)을 새 마스터 닉네임으로 갱신한다.
        if (_isLeavingRoom) return;
        if (newMasterClient != PhotonNetwork.LocalPlayer) return;

        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable { { HOST_NICKNAME_PROP_KEY, PhotonNetwork.NickName } });
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[Network] 연결 끊김: {cause}");

        _isLeavingRoom = false;

        // 자진 퇴장(LeaveRoom/Disconnect 직접 호출)은 재접속 시도 안 함.
        // 이 프로젝트에서는 LeaveRoom()이 OnLeftRoom 대신(또는 그 후) 이 콜백으로 이어질 수 있으므로
        // 예약된 타이틀 씬 로드는 여기서도 시도한다(OnLeftRoom과 이중 실행되지 않도록 내부에서 방어).
        if (cause == DisconnectCause.DisconnectByClientLogic)
        {
            TryLoadPendingTitleScene();
            return;
        }

        if (_selfReconnectRoutine != null)
            StopCoroutine(_selfReconnectRoutine);
        _selfReconnectRoutine = StartCoroutine(SelfReconnectRoutine());
    }

    /// <summary>본인 연결이 끊겼을 때 유예시간 동안 재접속 시도. 실패 시 세션 종료(패배 처리).</summary>
    private System.Collections.IEnumerator SelfReconnectRoutine()
    {
        Debug.Log($"[Network] 재접속 시도 시작 (유예 {RECONNECT_GRACE_PERIOD}초)");
        PhotonNetwork.ReconnectAndRejoin();

        float elapsed = 0f;
        while (elapsed < RECONNECT_GRACE_PERIOD)
        {
            if (PhotonNetwork.InRoom)
            {
                Debug.Log("[Network] 재접속 성공");
                _selfReconnectRoutine = null;
                ResyncAfterReconnect();
                yield break;
            }

            yield return new WaitForSeconds(1f);
            elapsed += 1f;
        }

        Debug.LogWarning("[Network] 재접속 실패 — 세션 종료(패배 처리)");
        _selfReconnectRoutine = null;
        GameEvents.SessionEnded(SessionEndReason.ReconnectFailed);
    }

    /// <summary>
    /// 본인 재접속 성공 직후 상태 복구.
    /// - 내 보드 스냅샷 재송출: 끊긴 동안의 내 보드 변경은 송출이 막혀 파트너 미러가 낡아 있다.
    /// - 파트너 골드/증강/팀 HP 재발행: 속성 자체는 최신이지만 변경 "이벤트"를 못 받아 UI가 낡아 있다.
    /// - 라운드 복구: 끊긴 동안 라운드가 진행됐으면 Room 속성 기준으로 현재 라운드에 쇼핑부터 재진입.
    /// (상대 보드 미러 복구는 상대 클라이언트가 OnPlayerEnteredRoom에서 재송출해 처리된다.)
    /// </summary>
    private void ResyncAfterReconnect()
    {
        GameEvents.BoardResyncRequested();

        // 1) 파트너 정보 복원(기존 로직 유지) — 파트너 CustomProperties는 이벤트로 UI/전적에만 반영.
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player == PhotonNetwork.LocalPlayer) continue;

            if (player.CustomProperties.TryGetValue(GOLD_PROP_KEY, out object gold))
                GameEvents.PartnerGoldChanged((int)gold);

            if (player.CustomProperties.TryGetValue(AUGMENTS_PROP_KEY, out object augments) &&
                augments is string[] augmentNames)
                GameEvents.PartnerAugmentsChanged(augmentNames);
        }

        // 2) 내 정보 복원(2단계 신규) — 서버(Player CustomProperties)에 남아있는 내 Gold/Augments/
        // 레벨·XP/무료 리롤/아이템 상점을 기존 시스템에 반영한다. 유닛 인벤토리는 저장 위치 자체가
        // 없어 이번 단계 범위 밖.
        RestoreLocalPlayerStateAfterReconnect();

        int teamHp = TeamHealth;
        if (teamHp >= 0) GameEvents.HealthChanged(teamHp);

        // 끊긴 동안 라운드가 진행됐으면 현재 라운드로 재진입.
        // RPC_OnRoundStart를 로컬 직접 호출해 일반 라운드 시작과 같은 리셋 절차(준비/전투결과 속성 초기화)를 탄다.
        // 이 호출이 발행하는 RoundChanged를 ShopManager 등이 "복원 중 재발행"으로 구분할 수 있도록
        // 캐치업 플래그로 감싼다(방금 복원한 상점/골드를 무조건 Roll()/이자 재계산이 덮어쓰는 문제 방지).
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROUND_PROP_KEY, out object roundObj))
        {
            int currentRound = (int)roundObj;
            if (currentRound > _lastKnownRound)
            {
                Debug.Log($"[Network] 재접속 라운드 복구: {_lastKnownRound} → {currentRound}");

                _isApplyingReconnectRoundCatchup = true;
                try { RPC_OnRoundStart(currentRound); }
                finally { _isApplyingReconnectRoundCatchup = false; }
            }
        }
    }

    /// <summary>
    /// 파트너가 지금까지 선택한 증강(영문명). GameEvents.OnPartnerAugmentsChanged는 변경된 순간에만
    /// 한 번 발행되므로, 그 이벤트가 이미 지나간 뒤에 구독을 시작한 late-subscriber(예: 재접속이
    /// 아닌 컴포넌트 재활성화)는 값을 영영 못 받는다 — 그런 곳에서 구독 직후 현재 값을 직접
    /// 당겨오는 용도.
    /// </summary>
    public string[] GetPartnerAugmentNamesNow()
    {
        if (!PhotonNetwork.InRoom) return System.Array.Empty<string>();

        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player == PhotonNetwork.LocalPlayer) continue;
            if (player.CustomProperties.TryGetValue(AUGMENTS_PROP_KEY, out object augments) &&
                augments is string[] augmentNames)
                return augmentNames;
        }

        return System.Array.Empty<string>();
    }

    /// <summary>
    /// 재접속 2단계 — 서버에 이미 저장돼 있는 내 Gold/Augments/레벨·XP/무료 리롤/아이템 상점 Player
    /// CustomProperties를 읽어 기존 시스템에 반영한다. GOLD_PROP_KEY/AUGMENTS_PROP_KEY/UNITS_PROP_KEY/
    /// LEVEL_PROP_KEY/XP_PROP_KEY에 이어 REROLL_COUNT_PROP_KEY/ITEM_SHOP_STATE_PROP_KEY를 추가로 읽는다.
    /// 유닛 인벤토리는 애초에 저장 위치가 없어 다루지 않는다.
    /// </summary>
    private void RestoreLocalPlayerStateAfterReconnect()
    {
        if (!GameManager.TryGet(out var gm)) return;

        var myProps = PhotonNetwork.LocalPlayer.CustomProperties;

        // Gold: ShopManager.Gold는 private set이라 직접 대입할 수 없고, 기존 골드 변경 통로인
        // AddGold(delta)를 통해서만 값을 바꾼다 — AddGold 자체가 내부에서 GameEvents.GoldChanged를
        // 발행하므로 "GoldChanged 이벤트 재사용" 요건과 "ShopManager 필드 직접 접근 금지" 요건을
        // 동시에 만족한다(GoldChanged를 단독으로만 쏘면 UI 표시는 갱신되지만 실제 Gold 값은 그대로라
        // 구매 판정과 표시가 어긋나는 문제가 있어 이 방식을 택함).
        if (gm.Shop != null && myProps.TryGetValue(GOLD_PROP_KEY, out object myGold))
        {
            int savedGold = (int)myGold;
            int delta = savedGold - gm.Shop.Gold;
            if (delta != 0)
            {
                gm.Shop.AddGold(delta);
                Debug.Log($"[Network][Rejoin] 내 골드 복원: {gm.Shop.Gold}G (서버 저장값 {savedGold}G)");
            }
        }

        // Level/XP: ShopManager.CurrentLevel/CurrentXp는 private set이라 직접 대입할 수 없다. AddXp()는
        // 정상 XP 획득 경로라 레벨업 판정·이벤트를 동반하므로 재접속 복원에는 쓰지 않고, 부작용 없는
        // 전용 진입점(RestoreProgressionState)만 사용한다. 두 키가 모두 있을 때만 복원한다 — 구버전
        // 세션이나 값이 아직 한 번도 저장되지 않은 신규 게임에서는 조용히 건너뛴다(예외 없음).
        if (gm.Shop != null &&
            myProps.TryGetValue(LEVEL_PROP_KEY, out object myLevel) &&
            myProps.TryGetValue(XP_PROP_KEY, out object myXp))
        {
            gm.Shop.RestoreProgressionState((int)myLevel, (int)myXp);
            Debug.Log($"[Network][Rejoin] 내 레벨/XP 복원: Lv.{gm.Shop.CurrentLevel}, {gm.Shop.CurrentXp}/{gm.Shop.RequiredXp} (서버 저장값 Lv.{myLevel}, XP {myXp})");
        }

        // 무료 리롤 잔여 횟수: ShopManager.RerollCount는 private set이라 직접 대입할 수 없다. AddReroll()은
        // 보상/증강 지급 경로라 재접속마다 재실행되면 안 되므로 부작용 없는 전용 진입점(RestoreRerollCount)만
        // 사용한다. 키가 없으면(구버전 세션) 조용히 건너뛴다 — 새 ShopManager 기본값(0) 유지.
        if (gm.Shop != null && myProps.TryGetValue(REROLL_COUNT_PROP_KEY, out object myRerollCount))
        {
            gm.Shop.RestoreRerollCount((int)myRerollCount);
            Debug.Log($"[Network][Rejoin] 내 무료 리롤 복원: {gm.Shop.RerollCount}");
        }

        // 아이템 상점(개인 로컬 상태, 공유 풀 권위와 무관): 저장된 스냅샷이 있으면 그대로 복원한다.
        // ShopManager.Start()는 재접속 시 RollItemShop()을 스킵하므로, 스냅샷이 없거나(구버전 세션)
        // 구조가 손상돼 RestoreItemShopState()가 false를 반환하면 아이템 상점이 빈 채로 남는다 —
        // 여기서 폴백으로 한 번만 새로 굴려 안전망을 둔다(신규 매치의 초기화 흐름과는 분리된 경로).
        if (gm.Shop != null)
        {
            bool itemShopRestored = false;
            if (myProps.TryGetValue(ITEM_SHOP_STATE_PROP_KEY, out object myItemShopState) &&
                myItemShopState is string itemShopJson && !string.IsNullOrEmpty(itemShopJson))
            {
                itemShopRestored = gm.Shop.RestoreItemShopState(itemShopJson);
            }

            if (!itemShopRestored)
            {
                Debug.Log("[Network][Rejoin] 아이템 상점 스냅샷 없음/복원 실패 — 폴백으로 새로 갱신");
                gm.Shop.RollItemShop();
            }
        }

        // Augments: 기존 GameEvents 흐름(OnPartnerAugmentsChanged)은 전적 기록 전용이라 실제 효과를
        // 적용하지 않는다 — AugmentManager에 실제로 적용까지 하는 진입점이 없어 최소 복원 진입점
        // (RestoreAugmentByNameEn, SelectAugment 재사용)을 새로 추가해 사용한다.
        if (gm.Augment != null && myProps.TryGetValue(AUGMENTS_PROP_KEY, out object myAugments) &&
            myAugments is string[] myAugmentNames)
        {
            foreach (var nameEn in myAugmentNames)
                gm.Augment.RestoreAugmentByNameEn(nameEn);
        }

        // 유닛/보드/벤치(1차 구현) — BoardManager.RestoreFromSnapshot()이 기존 TryPlaceUnit/
        // TryPlaceInBench를 재사용하며 OnUnitPlaced/OnUnitBenched를 그대로 발생시키므로,
        // 그 이벤트로 다시 저장 핸들러가 실행돼 불완전한 스냅샷을 덮어쓰지 않도록 가드로 감싼다.
        if (gm.Board != null && myProps.TryGetValue(UNITS_PROP_KEY, out object myUnitsJson) &&
            myUnitsJson is string unitsJson && !string.IsNullOrEmpty(unitsJson))
        {
            UnitSnapshotWrapper wrapper = null;
            try { wrapper = JsonUtility.FromJson<UnitSnapshotWrapper>(unitsJson); }
            catch (System.Exception e) { Debug.LogError($"[Network][Rejoin] 유닛 스냅샷 파싱 실패: {e.Message}"); }

            if (wrapper?.units != null)
            {
                _isRestoringUnitSnapshot = true;
                try { gm.Board.RestoreFromSnapshot(wrapper.units); }
                finally { _isRestoringUnitSnapshot = false; }

                Debug.Log($"[Network][Rejoin] 유닛 스냅샷 복원 완료: {wrapper.units.Length}개");
            }
        }

        // 유닛 상점(공유 챔피언 풀 예약) 복원 — 새로 저장한 데이터가 아니라, 마스터에게 남아있는
        // 내 기존 예약을 요청만 한다(RequestSharedShopRoll처럼 새로 굴리지 않음).
        RequestSharedShopRestore();
    }

    /// <summary>유닛 스냅샷 저장 트리거(PokemonUnit 인자 있는 이벤트용). 복원 중엔 무시한다.</summary>
    private void HandleUnitSnapshotDirty(PokemonUnit _) => RequestSaveUnitSnapshot();

    /// <summary>유닛 스냅샷 저장 트리거(인자 없는 이벤트용, OnInventoryChanged). 복원 중엔 무시한다.</summary>
    private void HandleUnitSnapshotDirtyNoArg() => RequestSaveUnitSnapshot();

    /// <summary>
    /// 유닛 스냅샷 저장을 디바운스로 예약한다. 벤치 정리처럼 배치/판매 이벤트가 한 프레임 사이에
    /// 여러 번 연달아 발생해도, 매번 직렬화+SetCustomProperties를 동기 호출하지 않고 마지막 요청
    /// 기준 UNIT_SNAPSHOT_SAVE_DELAY초 뒤 한 번만 저장한다(이미 대기 중이면 타이머를 리셋).
    /// </summary>
    private void RequestSaveUnitSnapshot()
    {
        if (_isRestoringUnitSnapshot) return;
        if (_soloMode || !PhotonNetwork.InRoom || _isLeavingRoom) return;

        if (_saveUnitSnapshotCoroutine != null) StopCoroutine(_saveUnitSnapshotCoroutine);
        _saveUnitSnapshotCoroutine = StartCoroutine(SaveUnitSnapshotAfterDelay());
    }

    private System.Collections.IEnumerator SaveUnitSnapshotAfterDelay()
    {
        yield return new WaitForSeconds(UNIT_SNAPSHOT_SAVE_DELAY);
        _saveUnitSnapshotCoroutine = null;
        SaveUnitSnapshot();
    }

    /// <summary>
    /// BoardManager의 현재 보드+벤치 상태를 JSON으로 직렬화해 Player CustomProperties에 저장한다.
    /// RequestSaveUnitSnapshot()의 디바운스 지연 후에만 호출된다 — 재진입 시점의 상태를 다시
    /// 확인해야 하므로 복원/솔로/방 밖 여부를 여기서도 한 번 더 검사한다.
    /// </summary>
    private void SaveUnitSnapshot()
    {
        if (_isRestoringUnitSnapshot) return;
        if (_soloMode || !PhotonNetwork.InRoom || _isLeavingRoom) return;
        if (!GameManager.TryGet(out var gm) || gm.Board == null) return;

        var wrapper = new UnitSnapshotWrapper { units = gm.Board.BuildSnapshot().ToArray() };
        string json = JsonUtility.ToJson(wrapper);

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { UNITS_PROP_KEY, json } });
    }

    // ─────────────────────────────────────────
    // 테스트용 게임 재시작
    // 마스터가 전체 클라이언트에 재시작 RPC를 보내고,
    // 각 클라이언트가 자기 로컬 게임 씬을 직접 다시 로드한다.
    // ─────────────────────────────────────────

    public void RestartGame()
    {
        if (_soloMode)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
            );
            return;
        }

        if (!PhotonNetwork.InRoom || !IsMasterClient)
        {
            Debug.LogWarning("[Restart] 마스터 클라이언트만 재시작할 수 있습니다.");
            return;
        }

        string sceneName =
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        Debug.Log("[Restart] 전체 클라이언트 새 판 재시작 요청");

        // 이전 판 Room 상태 초기화
        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable
            {
            { ROUND_PROP_KEY, 0 },
            { TEAM_HP_PROP_KEY, -1 },
            {
                MATCH_GUID_ROOM_KEY,
                System.Guid.NewGuid().ToString("N")
            }
            }
        );

        // 같은 씬을 PhotonNetwork.LoadLevel로 다시 불러오면
        // 파트너가 씬 변경으로 인식하지 않을 수 있으므로
        // 양쪽 클라이언트가 직접 로컬 씬을 재로드한다.
        photonView.RPC(
            nameof(RPC_RestartGame),
            RpcTarget.AllViaServer,
            sceneName
        );
    }

    [PunRPC]
    private void RPC_RestartGame(string sceneName)
    {
        Debug.Log($"[Restart] 재시작 명령 수신: {sceneName}");

        // 이전 판 Player 상태 초기화
        PhotonNetwork.LocalPlayer.SetCustomProperties(
            new Hashtable
            {
            { SCENE_READY_PROP_KEY, false },
            { READY_PROP_KEY, false },
            { BATTLE_RESULT_PROP_KEY, RESULT_NOT_REPORTED },
            { GOLD_PROP_KEY, 0 },
            { AUGMENTS_PROP_KEY, System.Array.Empty<string>() }
            }
        );

        // 현재 NetworkManager 로컬 상태 초기화
        _gameStarted = false;
        _roundResultResolved = false;
        _lastLocalBattleResult = null;
        _suppressedBattleResult = null;
        // 재시작 시점에 직전 판의 "응답 불능" 진단이 아직 안 풀린 채로 남아있었다면(예: QA "게임 재시작"
        // 버튼) 대기 모달을 닫아줄 신호를 여기서 대신 쏴야 한다 — RPC_OnRoundStart와 동일한 이유
        // (2026-08-22 코드리뷰 지적).
        if (_partnerResultUnresponsive)
        {
            _partnerResultUnresponsive = false;
            GameEvents.PartnerResultRecovered();
        }
        if (_partnerUnresponsiveGraceRoutine != null)
        {
            StopCoroutine(_partnerUnresponsiveGraceRoutine);
            _partnerUnresponsiveGraceRoutine = null;
        }

        // 항복 상태 초기화(이전 판의 잔여 요청/알림이 새 판으로 넘어가지 않도록)
        _surrenderRequestSent = false;
        _surrenderRequestReceived = false;
        _surrenderRejectedNotice = false;
        _surrenderCrossCancelledNotice = false;

        // 파트너 이탈 대기 상태도 새 판으로 넘어가지 않도록 초기화
        _opponentDisconnected = false;
        if (_opponentGraceRoutine != null)
        {
            StopCoroutine(_opponentGraceRoutine);
            _opponentGraceRoutine = null;
        }
        if (_reconnectConfirmRoutine != null)
        {
            StopCoroutine(_reconnectConfirmRoutine);
            _reconnectConfirmRoutine = null;
        }
        _partnerGaveUpReconnectNotice = false;

        _lastKnownRound = 0;
        _localBoardRevision = 0;
        _lastOpponentBoardRevision = -1;
        _localBattleSnapshotRevision = 0;
        _lastPartnerBattleSnapshotRevision = -1;
        _partnerBattleSnapshot = null;

        // 통신교환 상태 초기화
        _pendingOutgoingTrades.Clear();
        _nextTradeSequence = 1;
        _tradeSentThisRound = false;

        _incomingTradeQueue.Clear();
        _pendingTradeEvolutions.Clear();
        _isBattlePhase = false;

        GameEvents.TradeQueueChanged(0);

        // 골드 전송 대기 상태 초기화
        _pendingGoldTransfer = 0;

        StartCoroutine(ReloadGameSceneRoutine(sceneName));
    }

    private System.Collections.IEnumerator ReloadGameSceneRoutine(
        string sceneName)
    {
        // CustomProperties가 서버에 전달될 시간을 잠깐 확보
        yield return new WaitForSecondsRealtime(0.3f);

        Debug.Log($"[Restart] 로컬 씬 재로드: {sceneName}");

        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }

}

#else

/// <summary>
/// PUN2 미설치 시 오프라인 스텁.
/// Window > Package Manager > Asset Store에서 PUN2 설치 후 자동으로 실제 구현으로 교체됨.
/// </summary>
public class NetworkManager : MonoBehaviour
{
    public bool IsConnected    => false;
    public bool IsInRoom       => false;
    public bool IsMasterClient => true;   // 오프라인에서는 항상 호스트 취급
    public int  PlayerCount    => 1;
    public bool UsesSharedShopPool => false;

    // 오프라인은 저장된 세션/재입장 개념 자체가 없다(실구현과 동일 공개 API 유지용 스텁).
    public bool HasSavedSession        => false;
    public string SavedNickname        => "";
    public bool IsRejoining            => false;
    public bool RejoinFailed           => false;
    public bool PartnerGaveUpReconnect => false;
    public bool IsAwaitingPartnerReconnect => false; // 오프라인은 파트너 이탈 자체가 없음(실구현과 동일 공개 API 유지용 스텁).
    public bool IsResumingRejoinedMatch => false; // 오프라인은 재접속 개념 자체가 없음(실구현과 동일 공개 API 유지용 스텁).
    public bool IsApplyingReconnectRoundCatchup => false; // 오프라인은 재접속 캐치업 자체가 없음(실구현과 동일 공개 API 유지용 스텁).
    public void AttemptRejoinSavedSession()          { }
    public void AcknowledgeRejoinFailure()           { }
    public void AbandonPreviousSession()             { }
    public void AcknowledgePartnerGaveUpReconnect()  { }

    // 실구현과 동일한 표면(전적 기록 등에서 사용).
    public string RoomName        => "offline";
    public string LocalNickname   => "OfflinePlayer";
    public string PartnerNickname => "";
    public string MatchGuid       => ""; // 오프라인은 GUID 미발급 — MatchRecorder가 구형 방식으로 폴백
    public string[] GetPartnerAugmentNamesNow() => System.Array.Empty<string>(); // 오프라인은 파트너 자체가 없음(실구현과 동일 공개 API 유지용 스텁).

    private int _teamHp = -1;
    public int  TeamHealth     => _teamHp;

    private void OnEnable()
    {
        GameEvents.OnPlayerReadyApproved += BroadcastPlayerReady;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerReadyApproved -= BroadcastPlayerReady;
    }

    /// <summary>디버그: 켜지면 팀 공통 HP가 절대 깎이지 않음(무한 HP). PrototypeHud에서 토글.</summary>
    public static bool DebugInfiniteTeamHealth = false;

    /// <summary>오프라인은 ReportBattleResult가 항상 동기 즉시 판정이라 억제할 대상 자체가 없다
    /// (실구현과 동일 공개 API 유지용 스텁).</summary>
    public static bool DebugSuppressBattleResultReport = false;

    /// <summary>오프라인은 항상 false(실구현과 동일 공개 API 유지용 스텁).</summary>
    public bool HasSuppressedBattleResult => false;

    private void Start()
    {
        Debug.LogWarning("[Network] PUN2 미설치 — 오프라인 모드로 실행 중");
    }

    /// <summary>
    /// PUN2 미설치 환경에서는 Photon 닉네임을 설정하지 않는다.
    /// 실제 구현부와 동일한 공개 API를 유지하기 위한 오프라인 스텁.
    /// </summary>
    public bool TrySetLocalNickname(string nickname)
    {
        Debug.LogWarning("[Network] 오프라인 모드에서는 Photon 닉네임을 설정할 수 없습니다.");
        return false;
    }

    public void Connect()           => Debug.Log("[Network] 오프라인 모드");
    public void Disconnect()        { }
    public void CreateRoom(string _) => Debug.Log("[Network] 오프라인 모드");
    public void JoinRoom(string _)   => Debug.Log("[Network] 오프라인 모드");
    public void JoinOrCreateRoom(string _) => Debug.Log("[Network] 오프라인 모드");
    public void JoinRandomRoom()    => Debug.Log("[Network] 오프라인 모드");
    public void RefreshRoomList()   => Debug.Log("[Network] 오프라인 모드");
    public void LeaveRoom()         { }

    /// <summary>오프라인은 나갈 방이 없으므로 항상 즉시 씬을 로드한다.</summary>
    public void RequestReturnToTitle(string titleSceneName)
    {
        if (!string.IsNullOrWhiteSpace(titleSceneName))
            UnityEngine.SceneManagement.SceneManager.LoadScene(titleSceneName);
    }

    /// <summary>오프라인은 저장된 재접속 세션 자체가 없으므로(HasSavedSession 항상 false) 정리할 것 없이
    /// RequestReturnToTitle과 동일하게 동작한다(실구현과 동일 공개 API 유지용 스텁).</summary>
    public void RequestCompletedMatchReturnToTitle(string sceneName, string reason) => RequestReturnToTitle(sceneName);

    /// <summary>오프라인은 파트너 이탈 자체가 없으므로 실제로 호출될 일은 없다(실구현과 동일 공개 API 유지용 스텁).</summary>
    public void ConfirmPartnerDisconnectGiveUp() { }

    public void BroadcastRoundStart(int round) => GameEvents.RoundChanged(round);
    public void BroadcastBattleStart()         => GameEvents.BattleStart();
    public void BroadcastGameCleared()         => GameEvents.GameCleared();

    /// <summary>오프라인(1인)에서는 누르는 즉시 "모두 준비"로 처리</summary>
    public void BroadcastPlayerReady()
    {
        GameEvents.ReadyCountChanged(1, 1);
        GameEvents.AllPlayersReady();
    }

    // 상태 동기화 — 오프라인은 파트너가 없으므로 보드 미러/골드는 no-op, 팀 HP만 로컬 처리.
    public void BroadcastBoardSnapshot(int[] _) { }

    /// <summary>오프라인은 파트너가 없어 재송출을 요청할 대상 자체가 없다(실구현과 동일 공개 API 유지용 스텁).</summary>
    public void RequestPartnerBoardSnapshot() { }

    // 오프라인은 파트너가 없어 BattleSnapshot 송수신 자체가 없다(실구현과 동일 공개 API 유지용 스텁).
    public bool HasPartnerBattleSnapshot => false;
    public BattleSnapshot PartnerBattleSnapshot => null;
    public int PartnerBattleSnapshotRevision => -1;
    public int LocalBattleSnapshotRevision => 0;
    public bool BroadcastBattleSnapshot(BattleSnapshot _) => false;
    public void SyncLocalGold(int _)            { }
    public void SyncLocalAugments(string[] _)   { }
    public void SyncLocalProgression(int _, int __) { }
    public void SyncLocalRerollCount(int _) { }
    public void SyncLocalItemShopState(string _) { }
    public bool RequestSharedShopRoll(int level, bool forceCostFour, bool onlyCostFour, int[] excludedBaseIds) => false;
    public bool RequestSharedShopRestore() => false;
    public bool RequestSharedShopPurchase(int revision, int slot) => false;
    public bool RequestSharedShopMergePurchase(int revision, int slotA, int slotB) => false;
    public void RequestSharedShopReturn(int pokemonId, int amount) { }

    /// <summary>
    /// 오프라인에서는 공유 풀이 없으므로 false.
    /// ShopManager가 로컬 풀 직접 획득 방식으로 처리한다.
    /// </summary>
    public bool RequestSharedDebugUnitByCost(int cost) => false;

    /// <summary>오프라인에서는 공유 풀이 없으므로 false(실구현과 동일 공개 API 유지용 스텁).
    /// ShopManager가 로컬 풀 직접 획득 방식으로 처리한다.</summary>
    public bool RequestSharedDebugUnitBySpecies(int pokemonId) => false;

    /// <summary>오프라인(1인)은 씬 로드 즉시 라운드 1 시작.</summary>
    public void NotifySceneReady()              => BroadcastRoundStart(1);

    public void InitTeamHealth(int hp)
    {
        if (_teamHp >= 0) return;
        _teamHp = hp;
        GameEvents.HealthChanged(_teamHp);
    }

    public void ReportBattleLoss(int damage)
    {
        if (DebugInfiniteTeamHealth)
        {
            Debug.Log("[Network] 디버그 무한 HP — 팀 데미지 무시");
            return;
        }
        if (_teamHp < 0) return;
        _teamHp = Mathf.Max(0, _teamHp - damage);
        GameEvents.HealthChanged(_teamHp);
        if (_teamHp <= 0) GameEvents.SessionEnded(SessionEndReason.TeamHpZero);
    }

    /// <summary>오프라인(1인=팀): 승=BothWin, 패=BothLose(라이프 -1). 즉시 판정.</summary>
    public void ReportBattleResult(bool isWin)
    {
        if (!isWin) ReportBattleLoss(1);   // 라이프 -1
        GameEvents.TeamRoundResolved(isWin ? TeamRoundOutcome.BothWin : TeamRoundOutcome.BothLose);
    }

    /// <summary>오프라인은 ReportBattleResult가 항상 동기 즉시 판정이라 재전송·응답불능 진단 자체가
    /// 필요 없다(실구현과 동일 공개 API 유지용 스텁 — RoundPhaseManager.ResultTimer가
    /// PHOTON_UNITY_NETWORKING 여부와 무관하게 컴파일되도록 시그니처만 맞춘다).</summary>
    public void RequestBattleResultResendIfNeeded() { }

    /// <summary>위 RequestBattleResultResendIfNeeded와 동일 이유로 no-op.</summary>
    public void DiagnosePartnerUnresponsiveIfNeeded() { }

    /// <summary>오프라인은 억제할 대상 자체가 없다(실구현과 동일 공개 API 유지용 스텁).</summary>
    public void DebugSendSuppressedBattleResultNow() { }

    /// <summary>오프라인은 파트너가 없어 통신교환 불가.</summary>
    public void SendTradeUnit(PokemonUnit unit) => Debug.LogWarning("[Trade] 오프라인 — 파트너 없음, 전송 불가");

    /// <summary>오프라인은 파트너가 없어 전송 자체가 성립하지 않는다(실구현과 동일 공개 API 유지용 스텁).</summary>
    public bool CanSendTradeUnit(out string reason)
    {
        reason = "파트너 없음";
        return false;
    }

    public bool CanSendTradeUnit() => CanSendTradeUnit(out _);

    /// <summary>오프라인은 파트너가 없어 골드 전송 불가.</summary>
    public void SendGoldToPartner(int amount)
    {
        Debug.LogWarning("[GoldTransfer] 오프라인 — 파트너 없음, 전송 불가");
        GameEvents.GoldTransferRejected("파트너 없음");
    }

    public int PendingTradeUnitCount => 0;

    public bool TryReceiveNextTradeUnit()
    {
        Debug.LogWarning("[Trade] 오프라인 — 수령할 파트너 유닛 없음");
        return false;
    }

    public void RestartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
        );
    }

    // 오프라인은 파트너가 없어 항복 요청/응답 자체가 성립하지 않는다(실구현과 동일 공개 API 유지용 스텁).
    public bool HasIncomingSurrenderRequest    => false;
    public bool SurrenderRequestRejected       => false;
    public bool SurrenderRequestCrossCancelled => false;
    public void RequestSurrender()                    { }
    public void RespondToSurrender(bool accepted)      { }
    public void AcknowledgeSurrenderRejected()         { }
    public void AcknowledgeSurrenderCrossCancelled()   { }
}

#endif
