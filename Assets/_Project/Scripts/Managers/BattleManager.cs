using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자동 전투 진행 담당 (협동 PVE).
/// GameEvents.OnBattleStart 수신 시 BoardManager 스냅샷으로 아군 팀을 만들고,
/// 현재 라운드의 StageData에 정의된 적 구성을 미러 좌표에 생성해 시뮬레이션한다.
/// 결과는 GameEvents.BattleEnd(isWin)으로 통지.
///
/// 적은 PokemonUnit이 아니라 경량 BattleUnit 스냅샷(source=null)으로만 존재한다.
/// StageData/적 풀이 없으면 기존 "내 보드 미러"로 폴백(디버그/씬 호환).
/// </summary>
public class BattleManager : MonoBehaviour
{
    private const float TICK_INTERVAL = 0.1f;
    private const int MAX_TICKS = 400; // 40초 타임아웃(2026-08-22: 전투 시간 부족 의견 반영, 30초→40초)

    // 마나 충전 — 기획 확정(2026-07-10): 초당 10 고정만. 평타/피격비례 충전은 스코프 아웃.
    // 밸런스는 충전 방식이 아니라 유닛별 manaCost(마나통) 크기로 조절한다.
    private const float MANA_PER_SECOND = 10f;

    // CC(기둥C) — 기획 확정(2026-07-10): 스킬용 STUN은 이번 스코프 아웃(증강 6종에 없음),
    // SLOW는 보류(대상 유닛/아이템 미구현). 메커니즘은 유지하되 데이터가 안 쓴다.
    private const float STUN_DURATION = 1.5f;   // 악 시너지 스턴(SynergyConstants)과는 별개
    private const float SLOW_DURATION = 3f;
    private const float SLOW_MULTIPLIER = 0.5f;

    // 날따름(TAUNT, 파치리스 영웅증강 스킬) — 기획 확정(2026-07-10):
    // 지속 = base(1.0s) × 1.4(영웅증강 스탯보정) × 성급(1성 1.0 / 2성 1.8 / 3성 2.8)
    // → 1.4s / 2.52s / 3.92s. 날따름은 영웅증강 주입 스킬이라 1.4를 상수로 취급한다.
    private const float TAUNT_BASE_DURATION  = 1.0f;
    private const float TAUNT_HERO_STAT_MULT = 1.4f;
    private static readonly float[] TAUNT_STAR_MULT = { 1.0f, 1.0f, 1.8f, 2.8f }; // starLevel(1~3) 인덱스

    /// <summary>
    /// 지원 버프(AsBuff·ManaRegen) 공용 계수 — 기획 확정 2026-08-19.
    ///   ManaRegen : 즉시 회복 마나 = spellPower × 이 값
    ///   AsBuff    : 공속 증가량   = (spellPower × 이 값)%p
    ///
    /// <b>0.5에서 0.05로 내렸다.</b> 주문력이 세 자리(30~205)라 0.5를 그대로 쓰면 단위가 무너졌다 —
    /// 공속은 1성 +60%p에서 3성 +287%p(플러시)까지 튀어 비행 시너지 최고티어(+35%)를 압도했고,
    /// 마나는 마이농이 자기 코스트(45)의 2~6배를 돌려받아 시전 즉시 마나바가 다시 찼다.
    ///
    /// 두 효과를 같은 값으로 두는 이유 — 둘 다 "서포터가 areaRadius 안의 아군에게 거는 지원"이라 같은 급이어야
    /// 하고, 손잡이가 하나면 밸런스 조정이 한 줄로 끝난다. 단위가 달라 계수를 나눠야 했던 시기가
    /// 있었지만(마나=절대값, 공속=%p) 0.05에서는 양쪽 다 적정 구간에 들어온다:
    ///   공속  플러시 1성 +10%p → 3성 +29%p / 이브이 3성 +20%p / 파치리스 3성 +17%p
    ///   마나  마이농 1성 10 → 3성 29 (패시브 충전 초당 10 기준 "3초를 벌어주는" 값)
    ///
    /// <b>자가 순환은 구조적으로 막혀 있다</b> — 지원 버프는 시전자 자신을 대상에서 제외한다
    /// (<see cref="GetAllyTargets"/>). 따라서 이 계수를 올려도 "자기가 자기 마나를 채워 무한 시전"은
    /// 나오지 않는다. 다만 마이농을 2마리 놓으면 서로를 채워줄 수는 있으므로,
    /// <b>회복량 &lt; 자기 마나코스트</b>는 지키는 게 안전하다. 마이농 기준 상한은
    /// 205 × 2.8(3성) × 계수 × 1.3(치어리더 마나 선택지 — <see cref="GainMana"/>가 받는 쪽
    /// manaGainMultiplier를 한 번 더 곱한다) &lt; 45 → 약 0.060이고, 0.05는 그 아래다.
    /// </summary>
    private const float SUPPORT_BUFF_SPELLPOWER_COEF = 0.05f;

    private const float AS_BUFF_DURATION = 3f;  // 기획 확정(2026-08-19, 해인) — 더 이상 PLACEHOLDER 아님

    // role 기반 타겟 우선순위(기둥C) — PLACEHOLDER(기획확정 전): 낮을수록 먼저 타겟팅.
    private static readonly Dictionary<string, int> ROLE_TARGET_PRIORITY = new()
    {
        { PokemonRole.Supporter, 0 },
        { PokemonRole.Magician,  1 },
        { PokemonRole.Archer,    1 },
        { PokemonRole.Assassin,  2 },
        { PokemonRole.Warrior,   3 },
        { PokemonRole.Tanker,    4 },
    };
    private const int DEFAULT_ROLE_PRIORITY = 2; // 미지정/알 수 없는 role 폴백

    // 적 진영은 BoardManager.GetEnemyBattleCoords로 아군 보드 너머(rows 4~7) 연속 좌표에 배치한다.
    // 논리·시각 좌표가 동일해져 근접이 미들라인을 걸어 넘는다(구 ENEMY_BOARD_OFFSET 시각분리 방식 폐기).

    // 현재 스테이지는 RoundPhaseManager(Phase.CurrentStage)가 라운드별로 확정해 제공.
    // 적 영문명 → PokemonData 해석은 중앙 PokemonDatabase.Instance가 담당.
    // 스테이지/DB 둘 중 하나라도 없으면 "내 보드 미러"로 폴백(씬/디버그 호환).

    [Tooltip("적 진영 바닥 한 칸에 사용할 HexTile 프리팹입니다. 아군 보드(BoardManager._tilePrefab)와 같은 것을 " +
             "넣으면 양쪽 바닥이 이어져 보입니다. 비어 있으면 기존 원기둥 임시 타일을 생성합니다. " +
             "적 진영은 장식 전용이라 드롭 처리와 콜라이더는 자동으로 꺼집니다.")]
    [SerializeField] private HexTile _enemyTilePrefab;

    [Tooltip("30초 기본 전투가 끝났는데 적이 남아있을 때 추가로 주는 오버타임 길이(초). " +
             "0 이하면 오버타임 없이 그 시점 적 생존 여부로 즉시 판정한다. 기획 미확정 수치(2026-07-30 기준).")]
    [SerializeField] private float _overtimeDuration = 10f;

    [Tooltip("오버타임 동안 tick을 더 빠르게 소비하는 배속. 1 이하면 기본 전투와 같은 속도로 진행한다. " +
             "기획 미확정 수치(2026-07-30 기준).")]
    [SerializeField] private float _overtimeSpeedMultiplier = 2f;

    [Tooltip("사거리 밖일 때 한 칸 이동하는 주기(초). SimulateTick(0.1초)과는 별개 — 타일 사이 보간/애니메이션 " +
             "없이 즉시 스냅 이동이라 매 틱(초당 10칸) 그대로면 너무 빨라 보여서 도입. 유닛별 moveCooldown으로 " +
             "관리하며, 실제 이동 간격은 이 값을 BattleUnit.moveSpeedMultiplier로 나눈 값이다(배속 아이템 효과 유지). " +
             "0 이하로 두면 무한/초고속 이동이 될 수 있어 [Min]과 별개로 사용 시점에서도 최소값으로 방어한다.")]
    [Min(0.02f)]
    [SerializeField] private float _moveInterval = 0.2f;

    private readonly List<BattleUnit> _units = new();
    private readonly List<GameObject> _mirrorTiles = new();

    // 적 진영 바닥 타일(좌표 → 타일). 적이 서 있는 칸만 짙게 칠하려고 좌표로 되찾을 수 있게 들고 있는다.
    // 준비 단계 프리뷰와 전투 중 미러 보드가 번갈아 쓰며, 각자 걷어낼 때(ClearEnemyPreview/Cleanup) 같이 비운다.
    // 원기둥 폴백 타일은 HexTile이 아니라 여기 담기지 않는다 — 색 구분도 되지 않는다.
    private readonly Dictionary<HexCoords, HexTile> _enemyTiles = new();
    private Coroutine _battleCoroutine;

    // 이동 BFS(FindNextStep)가 보드 밖으로 벗어나지 않도록 쓰는 전투 가능 전체 좌표(아군 보드 +
    // 그 미러인 적 진영). _enemyTiles는 시각화 캐시라 프리팹 미할당/미러전투 경로에서 비어있을 수
    // 있어 이동 판정에 쓰면 안 된다 — BuildValidCoords()가 BoardManager.GetEnemyBattleCoords()로
    // 직접 계산해 채운다. 전투 시작(SetupUnits/SetupMirrorUnits)마다 새로 채우고 Cleanup()에서 비운다.
    private readonly HashSet<HexCoords> _validCoords = new();

    // 전투 중 소환(나인이볼부스트)의 대기열 — 담는 것은 <b>종 데이터뿐이고 좌표는 없다</b>.
    // 행동 루프가 _units를 foreach로 도는 도중에는 Add할 수 없어(컬렉션 변경 예외) 여기 담아뒀다가
    // 루프 직후 FlushPendingSpawns()가 자리를 골라 _units로 옮긴다. 자리를 예약 시점이 아니라
    // 편입 시점에 고르는 것이 핵심이다 — 그래야 같은 틱에 다른 유닛이 그 칸으로 이동해도 겹치지 않는다.
    private readonly List<PokemonData> _pendingSpawns = new();

    // FindNextStep의 isImmediateBacktrack 판정(정상 후보 제외용)과 막다른 자리 fallback
    // 후보군에서 참조하는 "직전에 있던 칸" 1개 기억(경로 캐시 아님). 유닛이 실제로 한 칸
    // 이동할 때 MoveTowards가 갱신한다. BattleUnit.cs는 건드리지 않고 여기(BattleManager)에서만
    // 관리 — 전투 시작(SetupUnits/SetupMirrorUnits)마다 새로 비우고 Cleanup()에서도 비워,
    // 이전 전투의 BattleUnit 참조가 안 남게 한다.
    private readonly Dictionary<BattleUnit, HexCoords> _previousCoords = new();

    // 가장 최근에 실제로 사용한 fallback(막다른 자리 후퇴) 후보의 (타겟, areaDist, steps) —
    // "그 후퇴가 어떤 타겟을 상대로, 어느 정도 상황을 뚫어줬는지"를 기억한다. FindNextStep이
    // 다음 호출에서 새로 계산한 fallback 후보가 이 기록과 세 값 전부 완전히 같으면(=같은
    // 타겟을 상대로 상황이 조금도 안 바뀌었으면) 같은 후퇴를 반복하지 않고 대기한다 — 다르면
    // (타겟이 바뀌었거나 주변 점유가 바뀌어 areaDist·steps가 달라졌으면) 다시 써도 된다고 본다.
    //
    // "직전 이동이 fallback였는지"만 boolean으로 기억하는 방식은 처음엔 이걸로 했었는데, 유닛이
    // 대기 상태에 들어가면(MoveTowards가 이동 없이 조기 리턴) 그 플래그를 갱신할 기회 자체가
    // 없어 영원히 true로 고정되는 회귀가 있었다(2026-08 코드리뷰 지적 — 보드 상황이 나중에
    // 바뀌어도 그 유닛은 전투가 끝날 때까지 fallback을 다시 못 씀). 시간/틱 기반 쿨다운도
    // 고려했으나 "일정 시간이 지나면 무조건 허용"은 실제로 아무것도 안 바뀌었어도 재시도해
    // 왕복이 다시 보일 수 있다 — 그래서 시간이 아니라 "이 후퇴가 실제로 다른 상황을 뚫어주는지"
    // 자체를 비교하는 이 방식을 택했다(경로 캐시 아님 — 매 호출 새로 계산되는 BFS 결과의
    // 요약값만 저장).
    //
    // (areaDist, steps) 두 값만 저장했을 때는(2026-08 재리뷰 지적) target을 매 틱 재조준하는
    // 이 게임에서(FindInRangeEnemy/FindNearestEnemy) 타겟이 바뀌었는데도 우연히 같은
    // (areaDist, steps) 조합이 나오면 서로 무관한 상황을 "동일 상황"으로 오판해 정당한 이동까지
    // 막을 수 있었다 — target 참조를 지문에 추가해 타겟이 바뀌는 경우는 항상 "다른 상황"으로
    // 확실히 구분되게 했다. FindNextStep 안에서만 갱신·소비하며, 실제 이동 성공 여부와 무관하게
    // "다음에 다시 봐도 되는 기준선"으로만 쓰인다. _previousCoords와 같은 생명주기(전투 시작·
    // Cleanup마다 비움)로 관리한다.
    private readonly Dictionary<BattleUnit, (BattleUnit target, int areaDist, int steps)> _lastFallbackProfile = new();

    /// <summary>_previousCoords·_lastFallbackProfile은 항상 같은 생명주기(전투 시작·Cleanup마다
    /// 비움)라 짝으로 관리한다 — 둘 중 하나만 비우는 실수를 막기 위해 호출부는 이 메서드
    /// 하나만 부르면 된다.</summary>
    private void ClearMovementTracking()
    {
        _previousCoords.Clear();
        _lastFallbackProfile.Clear();
    }

    /// <summary>파트너 관전 미러 전투 코루틴(실전투 _battleCoroutine과 완전히 별도 필드).
    /// null이 아니면 이 인스턴스가 이미 미러 전투를 실행 중이라는 뜻이다.</summary>
    private Coroutine _mirrorBattleCoroutine;

    /// <summary>
    /// 이 인스턴스의 시각 오브젝트(visual)에만 적용하는 월드 오프셋. 실전투 인스턴스는 항상
    /// Vector3.zero(기존 동작과 완전히 동일) — ConfigureMirrorVisuals를 호출한 미러 인스턴스만
    /// 값이 채워진다. BattleUnit.coords(판정 좌표)에는 전혀 영향 없다.
    /// </summary>
    private Vector3 _visualOffset = Vector3.zero;

    /// <summary>
    /// 이 인스턴스가 생성하는 visual의 부모 Transform. null이면(실전투 기본값) 기존과 동일하게
    /// 씬 루트에 생성된다 — 미러 인스턴스만 자기 자신의 Transform으로 설정해 미러 visual을
    /// 전부 그 아래로 모으고, 실전투 visual과 계층으로도 확실히 구분·정리되게 한다.
    /// </summary>
    private Transform _visualParent;

    /// <summary>
    /// true면 BattleVfxPlayer.Play*를 호출한다. 실전투/미러 인스턴스 모두 기본값 true를 그대로 쓴다.
    /// 예전에는 미러 인스턴스에서 false로 꺼뒀다 — BattleVfxPlayer._activeVfx가 static 전역이라
    /// 실전투와 미러가 서로의 VFX를 지울 위험이 있었기 때문(ClearAllActive가 실전투 종료 시 전체를
    /// 지움). 지금은 BattleVfxPlayer가 scope(이 인스턴스 자신)별로 생성 목록을 분리해 관리하고
    /// (ClearScope), Play* 호출부(BasicAttack 등)가 this를 scope로 넘기므로 이 문제가 해소됐다 —
    /// 그래서 미러도 VFX를 켠 채로 안전하게 실행한다. 이 필드/게이트 자체는 향후 다시 끌 필요가
    /// 생길 때를 대비해 구조만 유지한다(현재는 아무 경로도 false로 설정하지 않음).
    /// </summary>
    private bool _playBattleVfx = true;

    // 파트너 이탈 시 전투를 멈추던 _isPaused는 제거됐다(2026-08-22, 파트너 이탈 재설계).
    // 원래 PR #67에서 "재접속 대기 팝업 뒤에서 게임이 계속 조작되던" 버그를 고치며 상점/드래그 차단과
    // 함께 들어간 것인데, 부작용으로 남은 플레이어가 최대 60초(RECONNECT_GRACE_PERIOD) 동안 멈춘 화면을
    // 보게 됐다 — 빠진 사람이 아니라 남은 사람이 대기 비용을 전부 지는 구조였다.
    //
    // 새 방향: 전투 중 이탈이 나도 남은 플레이어는 그대로 전투를 끝낸다. 이탈한 쪽의 전투 결과는
    // 미러 전투(PartnerBattleMirrorController)가 스냅샷으로 계산하므로, 기다리지 않고 라운드를 완결할 수 있다.
    // 상점/아이템 조작 차단(NetworkManager.IsAwaitingPartnerReconnect)은 그대로 살아 있어, 전투가 끝난 뒤
    // 다음 라운드로 넘어가지 못하는 구간은 여전히 막힌다.
    //
    // 참고: 미러 루프(RunMirrorBattleTickLoop)는 원래부터 _isPaused를 보지 않았다 — 이 제거로 달라지는 건
    // 실전투(SimulateBattleLoop/RunOvertime)뿐이다.

    // 준비 단계에 미리 보여주는 적 진영(바닥 + 적 모델). 시각 전용이라 BattleUnit을 만들지 않는다 —
    // 스탯 스냅샷은 전투 시작 시점 보드/아이템 기준이어야 하므로 미리 만들어 두면 안 된다.
    private readonly List<GameObject> _previewObjects = new();

    // 준비 단계 적 프리뷰의 스탯 사본. 전투 목록(_units)에는 넣지 않으므로 시뮬레이션에 관여하지 않는다.
    // 스탯창이 준비 단계에도 적 정보를 보여줄 수 있게 두는 읽기 전용 스냅샷이다.
    private readonly List<BattleUnit> _previewEnemies = new();

    /// <summary>준비 단계에 세워둔 적 프리뷰. 전투 중에는 비어 있다(그때는 Units를 쓸 것).</summary>
    public IReadOnlyList<BattleUnit> PreviewEnemies => _previewEnemies;

    /// <summary>전투 유닛 목록(읽기 전용). 전투 HUD, 유닛 정보 조회, 파트너 미러 전투 표시에서 쓴다.</summary>
    public IReadOnlyList<BattleUnit> Units => _units;

    private void OnEnable()
    {
        GameEvents.OnBattleStart += HandleBattleStart;
        GameEvents.OnStageEntered += HandleStageEntered;
    }

    private void OnDisable()
    {
        GameEvents.OnBattleStart -= HandleBattleStart;
        GameEvents.OnStageEntered -= HandleStageEntered;

        // 컴포넌트가 꺼지면 프리뷰를 지울 주체가 사라지므로 씬에 남지 않게 정리한다.
        ClearEnemyPreview();
    }

    /// <summary>라운드 스테이지가 확정되면 준비 단계 동안 이번 라운드 적 진영을 미리 보여준다.</summary>
    private void HandleStageEntered(StageData stage) => ShowEnemyPreview(stage);

    private void HandleBattleStart()
    {
        // 전투용 실제 적이 같은 자리에 생성되므로 프리뷰를 먼저 걷어낸다(겹쳐 보이는 것 방지).
        ClearEnemyPreview();

        if (_battleCoroutine != null) StopCoroutine(_battleCoroutine);
        _battleCoroutine = StartCoroutine(RunBattle());
    }

    private IEnumerator RunBattle()
    {
        SetupUnits();

        if (_units.Count == 0)
        {
            // 보드에 유닛이 하나도 없음 — 즉시 종료(엣지케이스), 승리로 처리
            Debug.Log("[Battle] 유닛 없음 → 승리로 처리 (엣지케이스)");
            Cleanup(); // 이 인스턴스 scope의 VFX 정리까지 Cleanup()이 담당(BattleVfxPlayer.ClearScope)
            GameEvents.BattleEnd(BattleEndReason.Victory);
            yield break;
        }

        // 활성 시너지 적용: ① 일반 스탯버프(SynergyConstants 수치)
        // ② 특수효과(얼음 적디버프/치어리더 선택/돌연변이 봇소환/악 첫스킬 스턴).
        // 향후 SynergyData.statType 추가 시 ①을 statType 기반 리팩터 가능.
        ApplySynergyBuffs();
        ApplySynergySpecials();

        // 시뮬레이션 루프(전멸 판정 시 조기 종료, 타임아웃 시 allyWon=null로 남김).
        var result = new BattleLoopResult();
        yield return SimulateBattleLoop(result);

        Cleanup(); // 이 인스턴스 scope의 VFX 정리까지 Cleanup()이 담당(BattleVfxPlayer.ClearScope)

        // 30초 안에 승부가 안 났으면(조기 종료 없음) 오버타임으로 넘어간다(기획 확정 2026-07-30).
        BattleEndReason reason;
        if (result.allyWon != null)
        {
            // 기본 30초 이내 조기 종료 (한쪽 전멸)
            reason = result.allyWon.Value ? BattleEndReason.Victory : BattleEndReason.Defeat;
            Debug.Log($"[Battle] 조기 종료 → {reason} (전멸)");
        }
        else
        {
            // 기존: 타임아웃 후 HP 합계 비교로 판정(레퍼런스 게임과 달라 폐기, 롤백 대비 보존).
            // bool allyWon = DetermineWinnerByRemainingHp();
            // reason = allyWon ? BattleEndReason.DecisionVictory : BattleEndReason.DecisionDefeat;

            // 변경: 30초 후 오버타임 진행, 종료 시 적이 하나라도 살아있으면 무조건 패배.
            bool allyWon = result.overtimeAllyWon ?? false;
            reason = allyWon ? BattleEndReason.DecisionVictory : BattleEndReason.DecisionDefeat;
            Debug.Log($"[Battle] 타임아웃 30초 경과 → 오버타임 진행 → {reason} (적 생존 여부 판정)");
        }

        GameEvents.BattleEnd(reason);
    }

    /// <summary>코루틴은 out 파라미터를 못 쓰므로 루프 결과를 담아 전달하는 홀더.</summary>
    private sealed class BattleLoopResult
    {
        public bool? allyWon; // null = 기본 30초 안에 미결(오버타임으로 진행), true/false = 한쪽 전멸로 확정.
        public bool? overtimeAllyWon; // 오버타임 결과(즉시 전멸 또는 시간 종료 판정). allyWon이 이미 확정됐으면 안 채워짐.
    }

    /// <summary>
    /// MAX_TICKS까지 매 틱 시뮬레이션. 한쪽이 전멸하면 result.allyWon에 결과를 담고 종료,
    /// 타임아웃이면 오버타임(RunOvertime)으로 넘어간다.
    /// </summary>
    private IEnumerator SimulateBattleLoop(BattleLoopResult result)
    {
        int tick = 0;

        while (tick < MAX_TICKS)
        {
            SimulateTick();

            bool allyAlive  = HasAliveUnit(BattleTeam.Ally);
            bool enemyAlive = HasAliveUnit(BattleTeam.Enemy);

            if (!allyAlive || !enemyAlive)
            {
                result.allyWon = allyAlive; // 둘 다 전멸하면 false(패배 처리)
                yield break;
            }

            tick++;
            yield return new WaitForSeconds(TICK_INTERVAL);
        }

        yield return RunOvertime(result);
    }

    /// <summary>
    /// 30초 타임아웃 후 오버타임. Duration과 Speed는 서로 다른 축이다 —
    /// Duration(_overtimeDuration)은 "현실 시간" 기준으로 오버타임이 유지되는 길이이고,
    /// Speed(_overtimeSpeedMultiplier)는 그 현실 시간 동안 SimulateTick()을 몇 배 더 처리하는지다.
    /// 대기(WaitForSeconds) 횟수(realTicks)는 오직 Duration/TICK_INTERVAL로만 정해지므로
    /// Speed를 아무리 올려도 오버타임이 "짧아지지" 않고, 그 안에서 처리되는 전투 계산량만 늘어난다.
    /// tick 계산(공속/이동/스킬쿨다운/상태이상 등) 자체는 바꾸지 않아 일부 시스템만 빨라지는 불균형이 없다.
    /// 종료 시 적 생존 여부만으로 판정(아군 생존 수/HP/처치 수는 사용하지 않음).
    /// </summary>
    private IEnumerator RunOvertime(BattleLoopResult result)
    {
        if (_overtimeDuration <= 0f)
        {
            result.overtimeAllyWon = !HasAliveUnit(BattleTeam.Enemy);
            yield break;
        }

        // 실제로 시간을 두고 진행하는 오버타임에 진입할 때만, 이 메서드 호출당 정확히 1회 발행(UI 타이머 전환용).
        GameEvents.OvertimeStarted(_overtimeDuration);

        // 현실 시간 축 — 오버타임이 실제로 유지되는 대기 횟수(배속과 무관).
        int realTicks = Mathf.CeilToInt(_overtimeDuration / TICK_INTERVAL);

        // 배속 축 — 그 대기 한 번당 SimulateTick()을 몇 번 처리할지(현실 시간과 별개 변수).
        // 1 이하면 일반 속도(대기 1번당 1틱).
        int simTicksPerWait = _overtimeSpeedMultiplier > 1f
            ? Mathf.Max(1, Mathf.RoundToInt(_overtimeSpeedMultiplier))
            : 1;

        // 배속(simTicksPerWait > 1)일 때 SimulateTick() 여러 번을 한 프레임에 몰아 처리하면 렌더가
        // 버벅인다. 그렇다고 사이사이 yield return null(프레임 단위 대기)을 넣으면 프레임레이트에
        // 따라 실제 걸리는 시간이 늘어나 버려 오버타임 총 길이(_overtimeDuration)가 깨진다.
        // 대신 TICK_INTERVAL을 simTicksPerWait로 쪼개 WaitForSeconds로 나눠 기다리면, 프레임레이트와
        // 무관하게 realTick 한 구간의 실시간 길이가 정확히 TICK_INTERVAL로 유지되면서도
        // SimulateTick() 호출이 서로 다른 프레임에 분산된다.
        float subInterval = TICK_INTERVAL / simTicksPerWait;

        for (int realTick = 0; realTick < realTicks; realTick++)
        {
            for (int i = 0; i < simTicksPerWait; i++)
            {
                SimulateTick();

                bool allyAlive  = HasAliveUnit(BattleTeam.Ally);
                bool enemyAlive = HasAliveUnit(BattleTeam.Enemy);

                if (!allyAlive || !enemyAlive)
                {
                    result.overtimeAllyWon = allyAlive; // 둘 다 전멸하면 false(패배 처리) — 기존 동시 전멸 규칙 유지
                    yield break;
                }

                yield return new WaitForSeconds(subInterval);
            }
        }

        // 오버타임 실제 시간 종료 — 적이 한 마리라도 살아있으면 무조건 패배(기획 확정). 아군 생존 수/HP 무관.
        result.overtimeAllyWon = !HasAliveUnit(BattleTeam.Enemy);
    }

    // ─────────────────────────────────────────
    // 시너지 버프 (수치 = SynergyConstants, 기획 §6)
    // ─────────────────────────────────────────

    /// <summary>
    /// 활성 시너지의 스탯 버프를 "그 트레잇을 보유한 아군"에게만 적용.
    /// 카운트/티어 판정은 SynergyManager(황해인), 수치는 SynergyConstants(기획 §6), 적용은 여기(전투).
    /// PVE라 적은 StageData 구성 → 시너지는 아군에만 적용(미러매치 가정 없음. ICE 적디버프만 추후 예외).
    /// 주의: 포켓몬이 실제 보유한 시너지만 발동(ICE/DARK/CHEERLEADER는 현재 배정 유닛 없음).
    /// </summary>
    private void ApplySynergyBuffs()
    {
        var synergy = GameManager.Instance.Synergy;
        if (synergy == null) return;

        int appliedCount = 0;
        foreach (var status in synergy.GetActiveSynergies())
        {
            if (status?.data == null) continue;
            string synergyId = status.data.synergyNameEn; // 대문자 영문 ID(SynergyConstants가 대소문자 무관 조회)
            int tier = status.activeTierIndex + 1;         // 1-base

            foreach (var bu in _units)
            {
                if (bu.team != BattleTeam.Ally || bu.source == null || bu.source.data == null) continue;

                // 해당 트레잇을 실제로 보유한 유닛에게만(데이터가 한/영 어느 키든 허용).
                var syns = bu.source.data.synergies;
                if (syns == null) continue;
                if (!syns.Contains(status.data.synergyName) && !syns.Contains(status.data.synergyNameEn)) continue;

                if (ApplySynergyBuff(bu, synergyId, tier)) appliedCount++;
            }
        }

        if (appliedCount > 0)
            Debug.Log($"[Synergy] 시너지 버프 {appliedCount}건 적용");
    }

    /// <summary>
    /// SynergyConstants(기획 §6) 수치를 BattleUnit에 적용. percent는 1+v 곱, fixed는 v 가산.
    /// 적용 성공 시 true. 수치 미정의(특수 MUTANT/DARK·고유 ICE/CHEERLEADER 포함)는 false.
    /// </summary>
    private static bool ApplySynergyBuff(BattleUnit bu, string synergyId, int tier)
    {
        float v = SynergyConstants.Value(synergyId, tier);
        if (v <= 0f) return false; // 전용 로직 시너지(봇소환/CC/적디버프/선택)는 여기서 미처리

        switch (synergyId?.ToUpperInvariant())
        {
            // ── 고정값(Fixed) ──
            case "GRASS":    bu.attackSpeed += v; return true;   // 풀: atkSpeed +고정
            case "POISON":   bu.defense     += v; return true;   // 독: defense +고정
            case "ELECTRIC": bu.spellPower  += v; return true;   // 전기: spellPower +고정
            case "BUG":      bu.attack      += v; return true;   // 벌레: attack +고정

            // ── 비율(Percent) ──
            case "FIRE":     bu.defense     *= 1f + v; return true; // 불꽃: defense %
            case "FLYING":   bu.attackSpeed *= 1f + v; return true; // 비행: atkSpeed %
            case "BREAKER":  bu.attack      *= 1f + v; return true; // 파괴: attack %
            case "DRAGON":   bu.spellPower  *= 1f + v; return true; // 드래곤: spellPower %
            case "GROUND":   { float add = bu.maxHp * v; bu.maxHp += add; bu.currentHp += add; return true; } // 대지: hp %
            case "WATER":    bu.shield      += bu.spellPower * v; return true; // 물: spellPower×비율 보호막(전투 시작 1회)
            case "NORMAL":   bu.critChance = Mathf.Min(1f, bu.critChance + v); return true; // 노말: 치명타 확률 +절대
            case "ETHEREAL": bu.manaGainMultiplier += v; return true; // 정령: 마나 충전속도 배수 가산

            default: return false;
        }
    }

    // ─────────────────────────────────────────
    // 특수 시너지 (봇소환/적디버프/선택형) — 수치 = SynergyConstants
    // ─────────────────────────────────────────

    // 돌연변이 봇(덱기획: 돌연변이 2/3/4/5 단계별 = 에브이/브래키/글레이시아/님피아).
    // 에브이=Espeon(이브이 Eevee 아님 — 이전 코드 오타 수정).
    private static readonly string[] MutantBots = { "Espeon", "Umbreon", "Glaceon", "Sylveon" };

    /// <summary>전투 시작 시 특수 시너지 적용(일반 스탯버프 ApplySynergyBuffs 이후 호출).</summary>
    private void ApplySynergySpecials()
    {
        // 얼음: 적 전체 공격속도 감소(고유, 1마리 활성).
        if (GetActiveSynergy("Ice") != null)
            foreach (var bu in _units)
                if (bu.team == BattleTeam.Enemy)
                    bu.attackSpeed *= 1f - SynergyConstants.IceEnemyAtkSpeedReduction;

        // 치어리더: 플레이어 선택 버프를 아군 전체에(고유).
        if (GetActiveSynergy("Cheerleader") != null)
            ApplyCheerleaderChoice();

        // 이브이 영웅증강 v2: 전투 시작 시 즉시 소환하지 않고 "소환 주체"만 지정한다 —
        // 실제 소환은 그 이브이가 스킬을 시전할 때마다 1종씩(CastSkill → TryCastHeroEeveeBoost).
        // 이 경로가 잡히면 일반 돌연변이 시너지는 타지 않는다(애초에 SynergyManager가 눌러둔다).
        if (!MarkHeroEeveeSummoner())
        {
            var mutant = GetActiveSynergy("Mutant");
            if (mutant != null)
                SpawnMutantBots(mutant.activeTierIndex + 1);
        }

        // 악: 트레잇 보유 아군은 첫 스킬 시전 시 대상 스턴(전용 로직 — CastSkill에서 1회 소비).
        var dark = GetActiveSynergy("Dark");
        if (dark != null)
            MarkDarkFirstSkillStun(dark);
    }

    /// <summary>
    /// 악(DARK) 시너지 활성 시, 그 트레잇을 실제 보유한 아군에 첫 스킬 스턴 플래그를 세운다.
    /// 실제 스턴 부여는 CastSkill이 첫 시전 때 darkFirstSkillPending을 소비하며 처리.
    /// (트레잇 보유 판정은 ApplySynergyBuffs와 동일 — 데이터가 한/영 어느 키든 허용.)
    /// </summary>
    private void MarkDarkFirstSkillStun(SynergyStatus dark)
    {
        int marked = 0;
        foreach (var bu in _units)
        {
            if (bu.team != BattleTeam.Ally || bu.source == null || bu.source.data == null) continue;

            var syns = bu.source.data.synergies;
            if (syns == null) continue;
            if (!syns.Contains(dark.data.synergyName) && !syns.Contains(dark.data.synergyNameEn)) continue;

            bu.darkFirstSkillPending = true;
            marked++;
        }

        if (marked > 0)
            Debug.Log($"[Synergy] 악 첫스킬 스턴 대상 {marked}기 마킹");
    }

    /// <summary>
    /// 나인이볼부스트를 발동할 최소 성급. 1 = 성급 제한 없음(현재 값).
    ///
    /// 구 버전(봇 4마리 즉시소환)은 3성을 요구했지만, v2는 스킬을 시전해야 1종씩 나오는 누적형이라
    /// 3성까지 아무 것도 안 나오면 증강을 고른 보람이 한참 뒤에야 생긴다. 그래서 성급 제한을 풀었다.
    /// 밸런스상 다시 조이고 싶으면 이 값만 3으로 올리면 된다 — 판정은 아래 한 곳뿐이다.
    /// </summary>
    private const int HERO_EEVEE_MIN_STAR = 1;


    /// <summary>
    /// 나인이볼부스트의 <b>소환 주체</b>(= 이동 효과를 받고 있는 "가장 강한 이브이") 1기를 찾아
    /// 소환 카운터를 0으로 세운다. 찾았으면 true.
    ///
    /// 판정 기준이 <c>heroStatMultiplier &gt; 1</c>인 이유 — 진화잠금(evolutionLocked)은 보유한 모든
    /// 이브이에 붙는 <b>고정 효과</b>라 그것만으로는 여러 마리가 걸린다. 배수는 <b>이동 효과</b>라
    /// HeroAugment.Reselect()가 딱 한 마리에만 쓰므로, 그 한 마리가 곧 "가장 강한 이브이"다.
    /// (선정 규칙을 여기서 다시 구현하지 않는다 — 재선정을 단일 소스로 두는 원칙 그대로.)
    /// </summary>
    private bool MarkHeroEeveeSummoner()
    {
        foreach (var bu in _units)
        {
            var src = bu.source;
            if (bu.team != BattleTeam.Ally || src == null || src.data == null) continue;

            if (!src.evolutionLocked || src.heroStatMultiplier <= 1f) continue;
            if (src.starLevel < HERO_EEVEE_MIN_STAR) continue;
            if (!string.Equals(src.data.pokemonNameEn, "Eevee", System.StringComparison.OrdinalIgnoreCase))
                continue;

            MarkAsHeroEeveeSummoner(bu);
            Debug.Log($"[Augment] 나인이볼부스트 소환 주체 지정 — 이브이 {src.starLevel}성 " +
                      $"(마나 {bu.maxMana:0} → 이 성급의 도달 한계 안에서 최대 {HeroEeveeBoostTable.Count}종)");
            return true;
        }
        return false;
    }

    /// <summary>활성 시너지 중 영문 ID가 일치하는 것(없으면 null).</summary>
    private static SynergyStatus GetActiveSynergy(string synergyId)
    {
        var syn = GameManager.Instance.Synergy;
        if (syn == null) return null;
        foreach (var s in syn.GetActiveSynergies())
            if (s?.data != null &&
                string.Equals(s.data.synergyNameEn, synergyId, System.StringComparison.OrdinalIgnoreCase))
                return s;
        return null;
    }

    /// <summary>
    /// 치어리더: 선택(공속 +15% 또는 마나충전 +30%)을 아군 전체에.
    /// overrideChoice가 있으면 그 값을 쓰고(미러 전투 — CheerleaderChoice.Current를 직접 읽지 않음),
    /// 없으면 기존과 동일하게 CheerleaderChoice.Current(client-local static)를 읽는다(실전투 경로 그대로).
    /// </summary>
    private void ApplyCheerleaderChoice(CheerleaderChoice.Option? overrideChoice = null)
    {
        CheerleaderChoice.Option choice = overrideChoice ?? CheerleaderChoice.Current;

        foreach (var bu in _units)
        {
            if (bu.team != BattleTeam.Ally) continue;
            if (choice == CheerleaderChoice.Option.AttackSpeed)
                bu.attackSpeed *= 1f + SynergyConstants.CheerleaderAtkSpeedPct;
            else
                bu.manaGainMultiplier += SynergyConstants.CheerleaderManaRegenPct;
        }
    }

    /// <summary>돌연변이: 빈 아군 타일에 봇을 tier 수만큼 누적 소환. 봇은 전투 전용(source=null)이라 시너지 카운트·복원 대상 아님.</summary>
    private void SpawnMutantBots(int tier)
    {
        var db = PokemonDatabase.Instance;
        var board = GameManager.Instance.Board;
        if (db == null || board == null) return;

        // 빈 아군 좌표(스냅샷의 value==null) 수집. Dictionary 순회 순서는 삽입 이력에 우연히
        // 의존하므로(공용 API는 그대로 두고) 여기서 q,r로 명시 정렬해 클라이언트 간 배치 순서를 고정한다.
        var empty = new List<HexCoords>();
        foreach (var kv in board.GetBoardSnapshot())
            if (kv.Value == null) empty.Add(kv.Key);
        empty.Sort(CompareCoords);

        int count = Mathf.Min(tier, MutantBots.Length);
        int placed = 0;
        for (int i = 0; i < count; i++)
        {
            if (placed >= empty.Count) { Debug.LogWarning("[Synergy] 돌연변이 봇 배치할 빈 타일 부족 — 일부 미소환"); break; }

            var data = db.GetByNameEn(MutantBots[i]);
            if (data == null) { Debug.LogWarning($"[Synergy] 돌연변이 봇 '{MutantBots[i]}' DB에 없음 — 스킵"); continue; }

            var bot = CreateBotUnit(data, empty[placed++]);
            _units.Add(bot);
            SpawnVisual(bot);
        }
        if (placed > 0) Debug.Log($"[Synergy] 돌연변이 봇 {placed}마리 소환 (T{tier})");
    }

    /// <summary>봇 BattleUnit 생성(아군, source=null, 별/배수 없는 전투 전용 유닛 — 전투 후 복원 불필요).</summary>
    private BattleUnit CreateBotUnit(PokemonData data, HexCoords coords)
    {
        var bu = new BattleUnit
        {
            source = null,
            data = data,
            team = BattleTeam.Ally,
            coords = coords,
            maxHp = data.hp,
            currentHp = data.hp,
            attack = data.attack,
            defense = data.defense,
            spellPower = data.spellPower,
            attackSpeed = data.attackSpeed,
            range = Mathf.Max(1, data.attackRange),
            attackCooldown = 0f,
            moveCooldown = 0f,
            role = data.role ?? ""
        };
        ApplySkill(bu, data.skill, data.manaCost);
        return bu;
    }

    // ─────────────────────────────────────────
    // 이브이 영웅증강 v2 "나인이볼부스트" — 스킬 1회당 진화체 1종 소환 + 자기 버프
    // ─────────────────────────────────────────
    // 수치·순서는 전부 HeroEeveeBoostTable에 있다(밸런스 조정은 그 표만 수정).
    // 실전투/미러전투가 같은 CastSkill을 타므로 이 경로 하나로 양쪽이 동일하게 동작한다.
    // 결정론 주의: 종 순서는 고정 배열, 빈 타일은 CompareCoords로 정렬해서 고르고, 난수는 쓰지 않는다
    // — 2인이 각자 시뮬레이션해도 같은 결과가 나와야 하기 때문(기존 봇 소환과 같은 규칙).

    /// <summary>
    /// 소환 주체가 시전했다면 진화체 1종을 소환하고 대응 버프를 <b>시전자 자신</b>에게 건다.
    /// 소환이 실제로 일어났으면 true(= 이번 시전은 원래 스킬 효과를 내지 않는다).
    ///
    /// 버프가 봇이 아니라 이브이에게 가는 것이 이 증강의 핵심이다(기획 확정 2026-08-19) —
    /// 샤미드가 나오면 샤미드가 아니라 이브이가 보호막을 얻는다. 봇은 소환 그 자체가 값어치다.
    /// </summary>
    private bool TryCastHeroEeveeBoost(BattleUnit caster)
    {
        int index = caster.heroEeveeSummonIndex;
        if (index < 0 || index >= HeroEeveeBoostTable.Count) return false;

        var entry = HeroEeveeBoostTable.Entries[index];

        // 종이 DB에 없을 때만 실패로 본다(인덱스 미소비). 자리가 없는 경우는 실패가 아니라
        // FlushPendingSpawns가 다음 틱에 다시 시도하므로, 시전은 정상적으로 소모된 것으로 친다.
        if (!QueueHeroEeveeBot(entry.speciesNameEn)) return false;

        caster.heroEeveeSummonIndex = index + 1;
        ApplyEeveeBoost(caster, entry);

        // 마지막 1종까지 부른 순간 원래 스킬(이브이는 Celebrate)로 돌아간다 — 마나 코스트도 같이
        // 원복해야 폴백한 스킬이 나인이볼부스트용 싼 코스트로 계속 나가지 않는다.
        if (caster.heroEeveeSummonIndex >= HeroEeveeBoostTable.Count)
            caster.maxMana = caster.heroEeveeBaseMaxMana;

        Debug.Log($"[Augment] 나인이볼부스트 {index + 1}/{HeroEeveeBoostTable.Count} — " +
                  $"{entry.speciesNameEn} 소환 + 이브이 {entry.label} 버프");
        return true;
    }

    /// <summary>
    /// 진화체 1기의 소환을 <b>예약</b>한다. 종만 담아두고 <b>자리는 정하지 않는다</b>.
    /// 돌연변이 봇과 완전히 같은 형식(source=null 전투 전용)이라 전투가 끝나면 Cleanup()에서 사라진다.
    ///
    /// ⚠️ 여기서 좌표를 미리 고르면 안 된다 — 이 메서드는 전투 행동 루프
    /// (<c>foreach (var bu in _units)</c>) 안쪽의 CastSkill에서 호출되므로, 이 시점에 고른 빈 칸은
    /// 같은 틱의 <b>나머지 유닛이 아직 움직이기 전</b> 기준이다. 그 뒤에 다른 유닛이 그 칸으로
    /// 이동해버리면 두 유닛이 같은 헥스에 겹친다(IsOccupied는 아직 _units에 없는 예약분을 못 본다).
    /// 그래서 자리 선정은 이동이 전부 끝난 <see cref="FlushPendingSpawns"/>로 미룬다.
    /// </summary>
    private bool QueueHeroEeveeBot(string speciesNameEn)
    {
        var db = PokemonDatabase.Instance;
        if (db == null) return false;

        var data = db.GetByNameEn(speciesNameEn);
        if (data == null)
        {
            Debug.LogWarning($"[Augment] 나인이볼부스트 '{speciesNameEn}' DB에 없음 — 스킵");
            return false;
        }

        _pendingSpawns.Add(data);
        return true;
    }

    /// <summary>
    /// 예약된 소환을 실제 전투에 편입한다. 행동 루프가 끝난 뒤에만 호출되므로 이 시점의 좌표는
    /// <b>이번 틱의 이동이 전부 반영된 확정 값</b>이다 — 여기서 자리를 골라야 겹침이 생기지 않는다.
    ///
    /// 자리가 없으면 버리지 않고 큐에 남겨 다음 틱에 다시 시도한다(적이 죽거나 아군이 전진하면
    /// 자리가 난다). 큐 길이는 최대 8(HeroEeveeBoostTable.Count)이라 무한히 쌓이지 않는다.
    /// 시각화도 여기서 함께 만든다 — _units에 넣기 전에 visual을 만들면 그 사이 전투가 끝났을 때
    /// Cleanup()이 찾지 못해 남는다.
    /// </summary>
    private void FlushPendingSpawns()
    {
        if (_pendingSpawns.Count == 0) return;

        var board = GameManager.Instance.Board;
        if (board == null) return;

        // 예약 순서(FIFO)대로 처리한다 — 소환 순서가 곧 기획이 정한 진화체 등장 순서다.
        while (_pendingSpawns.Count > 0)
        {
            if (!TryFindEmptyBoardTile(board, out HexCoords coords))
            {
                Debug.LogWarning($"[Augment] 나인이볼부스트 — 빈 타일 없음, " +
                                 $"'{_pendingSpawns[0].pokemonNameEn}' 소환을 다음 틱으로 미룸");
                return;
            }

            var bot = CreateBotUnit(_pendingSpawns[0], coords);
            _pendingSpawns.RemoveAt(0);

            // 여기서 바로 _units에 넣어야 다음 반복이 이 봇의 칸을 점유로 인식한다.
            _units.Add(bot);
            SpawnVisual(bot);
        }
    }

    /// <summary>
    /// 아군 보드에서 비어 있는 칸 하나(좌표 정렬 기준 첫 칸). 없으면 false.
    ///
    /// 점유 판정은 <b>팀과 무관하게</b> 살아있는 유닛 전부를 본다 — 적이 전진해 아군 진영까지
    /// 밀고 들어와 있을 수 있고, 그 칸에 봇을 놓으면 두 유닛이 겹친다. 죽은 유닛의 자리는 다시 쓴다.
    /// 정렬(CompareCoords)은 클라이언트 간 배치 순서를 고정하기 위한 것으로, 기존 봇 소환과 같은 규칙이다.
    /// </summary>
    private bool TryFindEmptyBoardTile(BoardManager board, out HexCoords result)
    {
        var occupied = new HashSet<HexCoords>();
        foreach (var bu in _units)
            if (bu.IsAlive)
                occupied.Add(bu.coords);

        var empty = new List<HexCoords>();
        foreach (var coords in board.GetBoardSnapshot().Keys)
            if (!occupied.Contains(coords)) empty.Add(coords);

        if (empty.Count == 0)
        {
            result = default;
            return false;
        }

        empty.Sort(CompareCoords);
        result = empty[0];
        return true;
    }

    /// <summary>
    /// 종별 버프를 영웅 이브이에게 적용. 값의 의미는 HeroEeveeBoostTable.Stat 주석 참고.
    /// 전투 중에 거는 버프라 최대체력 증가는 늘어난 만큼 즉시 회복시킨다 — 그러지 않으면
    /// 체력 버프가 "빈 체력칸만 늘리는" 효과가 되어 사실상 무의미해진다(GROUND 시너지와 동일 처리).
    /// </summary>
    private static void ApplyEeveeBoost(BattleUnit target, HeroEeveeBoostTable.Entry entry)
    {
        float v = entry.value;

        switch (entry.stat)
        {
            // WATER 시너지와 같은 공식·같은 취급의 '미추적' 보호막 — shield 필드에만 더한다
            // (shieldSources는 스킬/아이템 보호막 전용. BattleUnit 주석의 2026-08 확정 규칙).
            case HeroEeveeBoostTable.Stat.Shield:
                target.shield += target.spellPower * v;
                break;

            case HeroEeveeBoostTable.Stat.AttackSpeed:
                target.attackSpeed *= 1f + v;
                break;

            case HeroEeveeBoostTable.Stat.Defense:
                target.defense *= 1f + v;
                break;

            case HeroEeveeBoostTable.Stat.Attack:
                target.attack *= 1f + v;
                break;

            case HeroEeveeBoostTable.Stat.CritChance:
                target.critChance = Mathf.Min(1f, target.critChance + v);
                break;

            case HeroEeveeBoostTable.Stat.MaxHp:
            {
                float add = target.maxHp * v;
                target.maxHp     += add;
                target.currentHp += add;
                break;
            }

            case HeroEeveeBoostTable.Stat.ManaRegen:
                target.manaGainMultiplier += v;
                break;

            case HeroEeveeBoostTable.Stat.SpellPower:
                target.spellPower *= 1f + v;
                break;
        }
    }

    // ─────────────────────────────────────────
    // 셋업
    // ─────────────────────────────────────────

    private void SetupUnits()
    {
        _units.Clear();
        ClearMovementTracking();
        _pendingSpawns.Clear(); // 이전 전투가 비정상 종료됐을 때의 잔여분 방어

        var board = GameManager.Instance.Board;
        BuildValidCoords(board);

        SetupAllyUnits(board);
        SetupEnemyUnits(board);
        ApplyOnCombatStartEffects();
        SetupVisuals(board);
    }

    /// <summary>
    /// 전투 가능한 전체 헥스 좌표(아군 보드 전체 칸 + BoardManager.GetEnemyBattleCoords()로 계산한
    /// 그 미러인 적 진영)를 _validCoords에 채운다. GetBoardSnapshot()은 배치된 유닛이 아니라 보드
    /// 칸 전체(빈 칸 포함)를 키로 반환하므로 유효 좌표 판정에 그대로 쓸 수 있다(SetupAllyUnits와
    /// 같은 근거). 이동 BFS(FindNextStep)가 보드 경계를 벗어나지 않도록 유닛 생성 전에 호출한다.
    /// 실전투(SetupUnits)와 미러전투(SetupMirrorUnits) 양쪽에서 각자 호출한다.
    /// </summary>
    private void BuildValidCoords(BoardManager board)
    {
        _validCoords.Clear();
        foreach (var coords in board.GetBoardSnapshot().Keys)
        {
            _validCoords.Add(coords);
            _validCoords.Add(board.GetEnemyBattleCoords(coords));
        }
    }

    /// <summary>
    /// 전투 결정론 정렬 기준 — q 오름차순, 같으면 r 오름차순(2026-08 코드리뷰 대응 — 실제 호출부에
    /// 맞게 정정). 두 가지 목적으로 쓰인다:
    /// (1) BoardManager.GetBoardSnapshot()이 반환하는 Dictionary는 순회 순서를 공식 보장하지
    ///     않아(현재는 삽입 이력에 우연히 의존), 두 클라이언트가 항상 같은 순서로 BattleUnit을
    ///     만들도록 소비 지점(BattleManager)에서만 명시 정렬한다 — 공용 API인
    ///     BoardManager.GetBoardSnapshot() 자체의 반환 순서는 바꾸지 않는다(다른 소비처 영향 회피).
    ///     SetupAllyUnits/SpawnMirrorEnemies/SpawnMutantBots/SpawnMirrorMutantBots에서 이 목적으로 쓴다.
    /// (2) FindNextStep의 areaDist → steps 다음 마지막 순위인 결정적 tie-break(네트워크 동기화용)에서도
    ///     쓰인다 — 전투 셋업 시점 1회성 호출이 아니다.
    /// </summary>
    private static int CompareCoords(HexCoords a, HexCoords b)
    {
        int qCompare = a.q.CompareTo(b.q);
        return qCompare != 0 ? qCompare : a.r.CompareTo(b.r);
    }

    /// <summary>아군: 내 보드 스냅샷을 q,r 순으로 정렬해 BattleUnit으로 추가.</summary>
    private void SetupAllyUnits(BoardManager board)
    {
        var entries = new List<KeyValuePair<HexCoords, PokemonUnit>>(board.GetBoardSnapshot());
        entries.Sort((a, b) => CompareCoords(a.Key, b.Key));

        foreach (var kv in entries)
        {
            PokemonUnit unit = kv.Value;
            if (unit == null || unit.data == null) continue;
            _units.Add(CreateAllyUnit(unit, kv.Key));
        }
    }

    /// <summary>적: 현재 스테이지를 미러 좌표에 생성. 스테이지/적이 없으면 "내 보드 미러"로 폴백.</summary>
    private void SetupEnemyUnits(BoardManager board)
    {
        StageData stage = GameManager.Instance.Phase != null ? GameManager.Instance.Phase.CurrentStage : null;
        int enemyCount = stage != null ? SpawnEnemiesFromStage(stage, board) : 0;

        if (enemyCount == 0)
        {
            if (stage == null)
                Debug.LogWarning("[Battle] CurrentStage 없음(StageDatabase 미임포트/매칭 실패) — 내 보드 미러로 폴백");
            else
                Debug.LogWarning($"[Battle] '{stage.stageId}' 적을 하나도 생성 못함(DUMMY/풀 누락) — 미러 폴백");
            SpawnMirrorEnemies(board);
        }
        else
        {
            Debug.Log($"[Battle] '{stage.stageId}' 적 {enemyCount}기 생성");
        }
    }

    /// <summary>[기둥B] 전투 시작 1회 — 장착템 등 효과의 OnCombatStart로 스탯 가산.
    /// 3단계로 나눠 실행한다(2026-08 기획 확정 5차분, 구애머리띠/라즈열매 오라 도입으로 필요해짐):
    /// 1단계 ItemStatEffect(평타스탯형, 유닛 자신의 정적 스탯)를 전체 유닛에 먼저 적용해야
    /// ItemStatFormula.ApplyAll의 대입식(attackSpeed = attackSpeed×(1+pct%))이 "아직 오라를 안 받은
    /// 자기 자신의 기준값"으로 정확히 계산된다 — 순서가 바뀌면 이미 다른 유닛에게서 받은 오라 보너스까지
    /// 자기 정적 %가 잘못 재곱셈하게 된다.
    /// 2단계 나머지(ItemConditionalEffect 등, 오라 포함)는 그 뒤 전체 유닛에 적용.
    /// 3단계는 2단계에서 여러 오라가 누적했을 수 있는 pendingAuraAttackSpeedPct를 유닛당 딱 1번만
    /// attackSpeed에 곱해 소비한다(오라 2개=+60%가 복리 ×1.69가 아니라 합산 후 단일 곱 ×1.6이 되도록).</summary>
    private void ApplyOnCombatStartEffects()
    {
        foreach (var bu in _units)
            foreach (var effect in bu.effects)
                if (effect is ItemStatEffect)
                    effect.OnCombatStart(bu);

        foreach (var bu in _units)
            foreach (var effect in bu.effects)
                if (!(effect is ItemStatEffect))
                    effect.OnCombatStart(bu);

        foreach (var bu in _units)
        {
            if (bu.pendingAuraAttackSpeedPct <= 0f) continue;
            bu.attackSpeed += bu.attackSpeed * (bu.pendingAuraAttackSpeedPct * 0.01f);
            bu.pendingAuraAttackSpeedPct = 0f;
        }
    }

    /// <summary>전투 유닛 시각화 + 미러 보드(상대 보드 시각 분리) 생성.</summary>
    private void SetupVisuals(BoardManager board)
    {
        foreach (var bu in _units)
            SpawnVisual(bu);

        SpawnMirrorBoard(board);
    }

    // ─────────────────────────────────────────
    // 적 진영 프리뷰 (준비 단계)
    //
    // 전투를 시작해야 적이 보이면 덱을 짜는 동안 상대 구성을 알 수 없다.
    // 스테이지가 확정되는 즉시(OnStageEntered) 바닥과 적 모델만 미리 띄워 두고,
    // 전투가 시작되면 걷어내 실제 BattleUnit 시각화에 자리를 넘긴다.
    // 시각 전용이므로 스탯/효과 스냅샷은 기존대로 전투 시작 시점에만 만들어진다.
    // ─────────────────────────────────────────

    /// <summary>
    /// 파트너 관전(쇼핑 중 적 프리뷰) 전용 진입점. 아래 private ShowEnemyPreview를 그대로 호출한다 —
    /// 로직을 복제하지 않는다. 실전투 인스턴스(_visualOffset=0/_visualParent=null)에서는 절대 호출하지
    /// 않는다(호출측 책임) — 미러 인스턴스에서 호출해야 ConfigureMirrorVisuals로 설정된 오프셋 덕분에
    /// 파트너 보드 위치에 그려진다.
    /// </summary>
    public void ShowMirrorEnemyPreview(StageData stage) => ShowEnemyPreview(stage);

    /// <summary>파트너 관전 종료 시 적 프리뷰 정리. 아래 private ClearEnemyPreview를 그대로 호출한다.</summary>
    public void ClearMirrorEnemyPreview() => ClearEnemyPreview();

    /// <summary>
    /// 미러 전투 시작 시 호출. 쇼핑 중 보여주던 적 프리뷰 "유닛"만 제거하고 빨간 육각 타일은 그대로
    /// 둔다 — ClearMirrorEnemyPreview()(전체 정리)를 쓰면 타일까지 같이 사라진다. 아래 private
    /// ClearEnemyPreviewUnitsOnly를 그대로 호출한다.
    /// </summary>
    public void ClearMirrorEnemyPreviewUnitsOnly() => ClearEnemyPreviewUnitsOnly();

    /// <summary>이번 라운드 적 진영(바닥 + 적 모델)을 미리 표시. 기존 프리뷰는 먼저 제거한다.</summary>
    private void ShowEnemyPreview(StageData stage)
    {
        // 전투 진행 중이면 프리뷰 상태를 아무것도 건드리지 않고 즉시 반환한다 — 미러 인스턴스에서는
        // 지금 남아있는 빨간 타일이 실제 미러 전투 적의 바닥으로 쓰이고 있으므로, ClearEnemyPreview를
        // 먼저 호출해 지웠다가 이 가드에 막혀 다시 못 만드는 일이 없어야 한다.
        if (_units.Count > 0) return;

        ClearEnemyPreview();

        var board = GameManager.TryGet(out var gm) ? gm.Board : null;
        if (board == null || stage == null) return;

        SpawnPreviewTiles(board);
        SpawnPreviewEnemies(stage, board);
    }

    private void SpawnPreviewTiles(BoardManager board)
    {
        foreach (var coords in board.GetBoardSnapshot().Keys)
        {
            HexCoords enemyCoords = board.GetEnemyBattleCoords(coords);

            GameObject tile = CreateEnemyTile(enemyCoords);

            tile.name = $"EnemyPreviewTile_{enemyCoords}";
            // _visualOffset은 실전투 인스턴스에서 항상 Vector3.zero라 이 한 줄은 실전투 결과에 영향 없다 —
            // 미러 인스턴스만 파트너 보드 위치로 밀려서 그려진다(SpawnVisual/UpdateVisualPosition과 동일 원칙).
            tile.transform.position = board.CoordsToWorldPosition(enemyCoords) + _visualOffset;
            if (_visualParent != null) tile.transform.SetParent(_visualParent, true);

            _previewObjects.Add(tile);
        }
    }

    /// <summary>SpawnEnemiesFromStage와 같은 좌표 규칙을 쓰되 BattleUnit 없이 모델만 세운다.</summary>
    private void SpawnPreviewEnemies(StageData stage, BoardManager board)
    {
        if (stage.enemies == null) return;

        foreach (var e in stage.enemies)
        {
            if (e == null) continue;

            PokemonData data = ResolvePokemon(e.pokemonNameEn);
            if (data == null) continue; // DUMMY/미해결 슬롯은 건너뜀

            HexCoords coords = board.GetEnemyBattleCoords(new HexCoords(e.q, e.r));

            GameObject visual = data.modelPrefab != null
                ? Instantiate(data.modelPrefab)
                : CreateFallbackEnemyVisual();

            visual.name = $"EnemyPreview_{data.pokemonNameEn}_{coords}";
            // 적은 아군 진영을 바라본다. 전투 시각화(SpawnVisual)와 같은 규칙.
            visual.transform.Rotate(0f, 180f, 0f, Space.World);
            // _visualOffset은 실전투 인스턴스에서 항상 Vector3.zero라 이 한 줄은 실전투 결과에 영향 없다 —
            // 미러 인스턴스만 파트너 보드 위치로 밀려서 그려진다(SpawnVisual/UpdateVisualPosition과 동일 원칙).
            visual.transform.position = board.CoordsToWorldPosition(coords) + Vector3.up * 0.5f + _visualOffset;
            if (_visualParent != null) visual.transform.SetParent(_visualParent, true);
            ApplyVisualLayer(visual);

            // 준비 단계에는 유닛 드래그/아이템 장착 레이캐스트가 살아 있다.
            // 프리뷰는 장식이므로 콜라이더를 꺼서 그 판정에 끼어들지 않게 한다(적 진영 타일과 동일).
            foreach (var col in visual.GetComponentsInChildren<Collider>(true))
                col.enabled = false;

            _previewObjects.Add(visual);

            // 바닥은 SpawnPreviewTiles에서 이미 다 깔렸다 — 그중 이 칸만 점유 색으로 바꾼다.
            MarkEnemyTileOccupied(coords);

            // 전투 때와 같은 계산(별 배수·스테이지 배수·보유 아이템)으로 스탯 사본을 만들어 둔다.
            // _units에 넣지 않으므로 시뮬레이션에는 영향이 없고, 스탯창이 준비 단계에도 적을 보여줄 수 있다.
            BattleUnit preview = CreateEnemyUnit(data, e, coords);
            preview.visual = visual;
            _previewEnemies.Add(preview);
        }
    }

    private static GameObject CreateFallbackEnemyVisual()
    {
        var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
        visual.GetComponent<Renderer>().material.color = Color.red;
        return visual;
    }

    private void ClearEnemyPreview()
    {
        foreach (var obj in _previewObjects)
            if (obj != null) Destroy(obj);

        _previewObjects.Clear();
        _previewEnemies.Clear();
        _enemyTiles.Clear();
    }

    /// <summary>
    /// _previewObjects/_previewEnemies에는 적 프리뷰 "타일"(SpawnPreviewTiles)과 "유닛 모델"
    /// (SpawnPreviewEnemies)이 함께 섞여 있다 — 별도 컬렉션으로 나뉘어 있지 않다. 이 메서드는
    /// _previewEnemies(프리뷰 유닛의 BattleUnit 스냅샷 목록, 각 항목의 .visual이 곧 유닛 모델
    /// GameObject)만 순회해 그 유닛 모델만 Destroy하고 _previewObjects에서도 정확히 그 참조만
    /// 제거한다 — 같은 오브젝트를 두 번 Destroy하지 않는다. 타일 GameObject(_previewObjects에 남는
    /// 나머지)와 _enemyTiles(좌표→타일 조회용 딕셔너리)는 건드리지 않아 화면에 그대로 남는다.
    ///
    /// 유닛을 지우기 전, 그 유닛이 서 있던 칸의 점유색(SetOccupied(true))도 함께 원복한다 —
    /// 안 그러면 타일 자체는 살아있으니 점유색만 미러 전투 내내 남아 몇 칸만 진한 색으로 보인다.
    /// 이 복원은 preview.visual 유효성과 무관하게 항상 실행해야 하므로 visual null 검사보다 먼저 한다.
    /// </summary>
    private void ClearEnemyPreviewUnitsOnly()
    {
        foreach (BattleUnit preview in _previewEnemies)
        {
            if (preview == null) continue;

            if (_enemyTiles.TryGetValue(preview.coords, out HexTile tile) && tile != null)
                tile.SetOccupied(false);

            if (preview.visual == null) continue;
            _previewObjects.Remove(preview.visual);
            Destroy(preview.visual);
        }

        _previewEnemies.Clear();
    }

    /// <summary>StageData의 적 배치(적 진영 로컬좌표)를 미러 좌표에 BattleUnit으로 생성. 생성 수 반환.</summary>
    private int SpawnEnemiesFromStage(StageData stage, BoardManager board)
    {
        int count = 0;
        foreach (var e in stage.enemies)
        {
            if (e == null) continue;

            PokemonData data = ResolvePokemon(e.pokemonNameEn);
            if (data == null) continue; // DUMMY/미해결 슬롯은 건너뜀

            // 기획은 적 자기 진영 로컬좌표(0~3행)로 작성 → 아군 보드 너머(rows 4~7)에 이어 배치.
            HexCoords coords = board.GetEnemyBattleCoords(new HexCoords(e.q, e.r));
            _units.Add(CreateEnemyUnit(data, e, coords));
            count++;
        }
        return count;
    }

    /// <summary>중앙 PokemonDatabase에서 영문명으로 PokemonData 해석. 빈/"DUMMY"/미발견이면 null.</summary>
    private PokemonData ResolvePokemon(string nameEn)
    {
        if (string.IsNullOrEmpty(nameEn) || nameEn == "DUMMY") return null;

        var db = PokemonDatabase.Instance;
        if (db == null) return null; // Instance 접근 시 에러 로그가 이미 출력됨

        var data = db.GetByNameEn(nameEn);
        if (data == null)
            Debug.LogWarning($"[Battle] 적 '{nameEn}'을 PokemonDatabase에서 못 찾음 — 건너뜀");
        return data;
    }

    /// <summary>
    /// "내 보드 미러" 폴백 적 생성(기존 동작). StageData 도입 전/디버그용.
    /// 이 경로도 GetBoardSnapshot() Dictionary를 순회하므로 아군과 같은 기준(q,r)으로 정렬한다.
    /// </summary>
    private void SpawnMirrorEnemies(BoardManager board)
    {
        var entries = new List<KeyValuePair<HexCoords, PokemonUnit>>(board.GetBoardSnapshot());
        entries.Sort((a, b) => CompareCoords(a.Key, b.Key));

        foreach (var kv in entries)
        {
            PokemonUnit unit = kv.Value;
            if (unit == null || unit.data == null) continue;
            HexCoords enemyCoords = board.GetEnemyBattleCoords(kv.Key);
            _units.Add(CreateBattleUnit(unit, BattleTeam.Enemy, enemyCoords));
        }
    }

    /// <summary>
    /// 보드 전체 칸을 점대칭 미러 좌표에 깔아 "상대 보드"를 시각화. 전투 종료 시 제거.
    /// _enemyTilePrefab이 연결돼 있으면 아군 보드와 같은 아트를 쓰고, 없으면 기존 원기둥 임시 타일로 폴백한다.
    /// </summary>
    private void SpawnMirrorBoard(BoardManager board)
    {
        foreach (var coords in board.GetBoardSnapshot().Keys)
        {
            HexCoords enemyCoords = board.GetEnemyBattleCoords(coords);

            GameObject tile = CreateEnemyTile(enemyCoords);

            tile.name = $"EnemyBoardTile_{enemyCoords}";
            tile.transform.position = board.CoordsToWorldPosition(enemyCoords);

            _mirrorTiles.Add(tile);
        }

        // 점유 색을 칠하지 않는다 — 미러 보드는 전투 중에만 존재하고, 전투 중에는 아군 보드도
        // Default Color로 고정하기 때문이다(BoardManager.HandlePhaseChanged와 같은 규칙).
        // 적 배치를 색으로 읽는 건 준비 단계 프리뷰(SpawnPreviewEnemies)의 몫이다.
    }

    /// <summary>
    /// 적 진영 바닥 한 칸 생성. 프리팹이 없으면 원기둥 폴백.
    /// 적 진영은 장식 전용이라 드롭 처리와 콜라이더를 끈다.
    /// HexTile은 IDropTarget이라 그대로 두면 적 진영에 유닛이 놓이고,
    /// 콜라이더가 살아 있으면 아이템 드래그 등 다른 레이캐스트도 가로챈다.
    ///
    /// <b>컴포넌트를 꺼도 색은 바뀐다</b> — Awake는 Instantiate 시점에 이미 돌았고,
    /// SetOccupied는 enabled와 무관한 일반 메서드다.
    /// </summary>
    private GameObject CreateEnemyTile(HexCoords coords)
    {
        GameObject tileGO;

        if (_enemyTilePrefab == null)
        {
            tileGO = CreateFallbackEnemyTile();
        }
        else
        {
            HexTile hex = Instantiate(_enemyTilePrefab);
            hex.enabled = false;

            foreach (var col in hex.GetComponentsInChildren<Collider>(true))
                col.enabled = false;

            _enemyTiles[coords] = hex;
            tileGO = hex.gameObject;
        }

        ApplyVisualLayer(tileGO);
        return tileGO;
    }

    /// <summary>
    /// 실전투/미러 인스턴스를 기존 _visualParent==null 여부로 구분해 LocalGameplayVisual/
    /// PartnerSpectateVisual Layer를 재귀 태깅한다(새 bool 상태를 추가하지 않고 기존 구분 재사용).
    /// Default(0)였던 자식만 바꾸고 UI/Ignore Raycast/Outline 등 특수 Layer 자식은 보존한다.
    /// 두 Layer 중 하나라도 Unity Editor에 없으면 아무 것도 하지 않는다(부분 적용 금지 —
    /// PartnerSpectateView/OpponentBoardView의 Layer 판정과 동일 조건). Instantiate 직후 1회만 호출.
    /// </summary>
    private void ApplyVisualLayer(GameObject root)
    {
        int targetLayer = ResolveVisualLayer();
        if (targetLayer < 0 || root == null) return;

        SetDefaultLayerRecursive(root.transform, targetLayer);
    }

    /// <summary>
    /// 이 인스턴스(실전투/미러)가 만드는 visual이 속해야 할 Layer. 유닛 모델(ApplyVisualLayer)과
    /// 전투 VFX(BattleVfxPlayer.Play* 호출부) 양쪽이 같은 판정을 쓴다 — 둘이 서로 다른 기준으로
    /// 갈리면 모델은 파트너 쪽인데 VFX만 로컬 화면에 새는 식의 불일치가 생긴다.
    /// 두 Layer(LocalGameplayVisual/PartnerSpectateVisual)가 Editor에 없으면 -1(적용 안 함 — 기존
    /// 프리팹 Layer를 그대로 둔다).
    /// </summary>
    private int ResolveVisualLayer()
    {
        int localLayer = LayerMask.NameToLayer("LocalGameplayVisual");
        int partnerLayer = LayerMask.NameToLayer("PartnerSpectateVisual");
        if (localLayer < 0 || partnerLayer < 0) return -1;

        return _visualParent == null ? localLayer : partnerLayer;
    }

    /// <summary>
    /// node의 Layer가 Default(0)이면 targetLayer로 덮어쓰고 자식까지 재귀 적용한다. 의도적으로 다른
    /// Layer가 찍힌 자식(0이 아님)은 보존한다. internal인 이유는 BattleVfxPlayer.Create()가 VFX
    /// GameObject에도 같은 규칙을 적용할 때 이 메서드를 그대로 재사용하기 위함(복제 방지) — 이 밖의
    /// 외부 호출은 없다.
    /// </summary>
    internal static void SetDefaultLayerRecursive(Transform node, int targetLayer)
    {
        if (node.gameObject.layer == 0) node.gameObject.layer = targetLayer;
        for (int i = 0; i < node.childCount; i++)
            SetDefaultLayerRecursive(node.GetChild(i), targetLayer);
    }

    /// <summary>그 칸에 적이 서 있다고 표시한다. 색은 HexTile_Enemy 프리팹의 Occupied Color가 정한다.</summary>
    private void MarkEnemyTileOccupied(HexCoords coords)
    {
        if (_enemyTiles.TryGetValue(coords, out HexTile tile) && tile != null)
            tile.SetOccupied(true);
    }

    /// <summary>프리팹 미연결 시 쓰는 임시 평면(연빨강 원기둥).</summary>
    private static GameObject CreateFallbackEnemyTile()
    {
        var tile = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tile.transform.localScale = new Vector3(0.95f, 0.05f, 0.95f);
        tile.GetComponent<Renderer>().material.color = new Color(1f, 0.7f, 0.7f); // 적 진영 바닥

        // 임시 타일도 드롭·레이캐스트를 방해하지 않도록 콜라이더를 제거한다.
        var col = tile.GetComponent<Collider>();
        if (col != null) Destroy(col);

        return tile;
    }

    /// <summary>내 보드 위 PokemonUnit에서 아군 BattleUnit 생성(원본 참조 유지 → 전투 후 복원).</summary>
    private BattleUnit CreateAllyUnit(PokemonUnit unit, HexCoords coords)
        => CreateBattleUnit(unit, BattleTeam.Ally, coords);

    /// <summary>
    /// StageData의 적 한 칸을 PokemonData + 강화 배수로 적 BattleUnit으로 생성(source=null).
    /// 별 배수는 아군과 동일(PokemonUnit.StarMultiplierFor), 그 위에 statMultiplier(전 스탯)·
    /// hpMultiplier(HP만)·atkMultiplier(공격력만)를 곱한다. 방어/특방/공속은 별 배수 미적용(아군 규칙과 동일).
    /// </summary>
    private BattleUnit CreateEnemyUnit(PokemonData data, EnemyPlacement e, HexCoords coords)
    {
        float star = PokemonUnit.StarMultiplierFor(e.starLevel);
        float sm = e.statMultiplier <= 0f ? 1f : e.statMultiplier;
        float hm = e.hpMultiplier   <= 0f ? 1f : e.hpMultiplier;
        float am = e.atkMultiplier   <= 0f ? 1f : e.atkMultiplier;

        float maxHp = data.hp * star * sm * hm;

        var bu = new BattleUnit
        {
            source = null,
            data = data,
            team = BattleTeam.Enemy,
            coords = coords,
            maxHp = maxHp,
            currentHp = maxHp,
            attack = data.attack * star * sm * am,
            defense = data.defense * sm,
            spellPower = data.spellPower * star * sm,
            attackSpeed = data.attackSpeed,
            range = Mathf.Max(1, data.attackRange),
            attackCooldown = 0f,
            moveCooldown = 0f,
            role = data.role ?? "",
            starLevel = Mathf.Clamp(e.starLevel, 1, 3)
        };
        ApplySkill(bu, data.skill, data.manaCost);
        bu.attackVfxId = data.attackVfxId; // 평타 VFX(아군과 동일 규칙)

        // 트레이너 보유 아이템 → 아군과 동일한 효과 훅을 적 BattleUnit에도 부착.
        // (아군은 unit.items 리스트, 적은 시트 itemSet1/itemSet2를 ItemDatabase로 해석)
        // ItemStatEffect는 아이템별로 하나씩 만들지 않고 마지막에 전체 목록으로 1개만 부착한다 —
        // 순서 무관 합산 계산(ItemStatFormula.ApplyAll)을 위해서다(§CreateBattleUnit과 동일 패턴).
        var resolvedEnemyItems = new List<ItemData>();
        foreach (var itemNameEn in e.HeldItemsEn)
        {
            var itemDb = ItemDatabase.Instance;
            ItemData item = itemDb != null ? itemDb.GetByNameEn(itemNameEn) : null;
            if (item == null)
            {
                Debug.LogWarning($"[Battle] 적 보유 아이템 '{itemNameEn}'을 ItemDatabase에서 못 찾음 — 효과 미적용");
                continue;
            }

            bu.effects.Add(new ItemConditionalEffect(item, this));
            bu.displayItems.Add(item); // HP바 아래 아이콘 표시용
            if (item.ccImmune) bu.HasCcImmuneItem = true;
            resolvedEnemyItems.Add(item);
        }
        if (resolvedEnemyItems.Count > 0)
            bu.effects.Add(new ItemStatEffect(resolvedEnemyItems));

        return bu;
    }

    /// <summary>
    /// 스킬 데이터를 BattleUnit에 반영. skillId가 없거나 manaCost<=0이면 평타만(maxMana=0).
    /// 위력은 데이터에 없음 — 시전 시 effectType에 따라 attack/spellPower로 계산(ApplySkill에선 분기 정보만 복사).
    /// </summary>
    private static void ApplySkill(BattleUnit bu, PokemonSkillData skill, int manaCost)
    {
        if (skill == null || !skill.HasSkill || manaCost <= 0) return;

        bu.maxMana          = manaCost;
        bu.currentMana      = 0f;
        bu.skillEffectType  = skill.effectType;
        bu.skillTargetType  = skill.targetType;
        bu.skillAreaRadius  = Mathf.Max(1, skill.areaRadius);
        bu.skillLineLength  = Mathf.Max(1, skill.lineLength);
        bu.skillVfxId       = skill.vfxId;
    }

    private BattleUnit CreateBattleUnit(PokemonUnit unit, BattleTeam team, HexCoords coords)
    {
        var bu = new BattleUnit
        {
            source = team == BattleTeam.Ally ? unit : null,
            data = unit.data,
            team = team,
            coords = coords,
            maxHp = unit.MaxHp,
            currentHp = unit.MaxHp,
            attack = unit.Attack,
            defense = unit.Defense,
            spellPower = unit.SpellPower,
            attackSpeed = unit.AttackSpeed,
            range = Mathf.Max(1, unit.Range), // 데이터 미설정(0) 시 인접칸까지는 사거리로 취급(TFT 근접 기본)
            attackCooldown = 0f,
            moveCooldown = 0f,
            role = unit.Role,
            starLevel = Mathf.Clamp(unit.starLevel, 1, 3), // 날따름 지속시간 공식용
            hasSitrusBerry = unit.hasHeroBerry // 파치리스 영웅증강 v2 자뭉열매
        };
        // 주입 스킬(파치리스 도발 등) 우선, 없으면 원본 종 스킬. Role도 오버라이드 반영(unit.Role).
        if (unit.data != null)
        {
            ApplySkill(bu, unit.EffectiveSkill, unit.EffectiveManaCost);
            // 평타 VFX는 종 데이터에서(스킬 테이블 아님). 영웅증강으로 역할이 바뀌면
            // 오버라이드가 우선한다(원거리 _L → 근거리 _S).
            bu.attackVfxId = unit.EffectiveAttackVfxId;
        }

        // ItemStatEffect는 아이템별로 하나씩 만들지 않고 마지막에 전체 목록으로 1개만 부착한다 —
        // 순서 무관 합산 계산(ItemStatFormula.ApplyAll)을 위해서다.
        foreach (var item in unit.items)
        {
            bu.effects.Add(new ItemConditionalEffect(item, this));
            bu.displayItems.Add(item); // HP바 아래 아이콘 표시용
            if (item.ccImmune) bu.HasCcImmuneItem = true;
        }
        if (unit.items.Count > 0)
            bu.effects.Add(new ItemStatEffect(unit.items));

        // 돌은 이미 종족 교체로 반영돼 있어 효과 훅이 없다 — 아이콘 표시용으로만 넘긴다.
        bu.displayStone = unit.equippedStone;

        return bu;
    }

    /// <summary>아군은 원본 오브젝트를 숨기고 그 자리에, 적은 미러 좌표에 시각화용 캡슐을 띄움.</summary>
    private void SpawnVisual(BattleUnit bu)
    {
        if (bu.source != null)
            bu.source.gameObject.SetActive(false);

        GameObject visual;
        if (bu.data != null && bu.data.modelPrefab != null)
        {
            // _visualParent가 null이면(실전투) 기존과 동일하게 씬 루트에 생성된다.
            visual = Instantiate(bu.data.modelPrefab, _visualParent);
        }
        else
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            if (_visualParent != null) visual.transform.SetParent(_visualParent, false);
            visual.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
            visual.GetComponent<Renderer>().material.color =
                bu.team == BattleTeam.Ally ? Color.blue : Color.red;
        }

        visual.name = $"BattleVisual_{bu.team}_{bu.coords}";
        ApplyVisualLayer(visual);

        // 전투 시작 시엔 서로 마주 보게 세워두고, 이후 매 틱 실제 노리는 대상을 향해 돌린다.
        // 프리팹의 기본 자세는 UnitFacing이 보관해 요(yaw)만 덧씌우므로 기울기가 유지된다.
        bu.facing = visual.AddComponent<UnitFacing>();
        bu.facing.Initialize(visual.transform.rotation, InitialFacingDirection(bu));

        bu.visual = visual;
        UpdateVisualPosition(bu);
    }

    /// <summary>
    /// 전투 시작 시 기본 시선 방향(월드). 아군은 적 진영 쪽, 적은 그 반대.
    /// 보드 앵커가 회전돼 있어도 맞도록 월드 좌표 변환에서 "행이 커지는 축"을 직접 뽑는다.
    /// </summary>
    private Vector3 InitialFacingDirection(BattleUnit bu)
    {
        var board = GameManager.Instance.Board;
        Vector3 here = board.CoordsToWorldPosition(bu.coords);
        Vector3 nextRow = board.CoordsToWorldPosition(new HexCoords(bu.coords.q, bu.coords.r + 1));

        Vector3 towardEnemyHalf = nextRow - here;
        return bu.team == BattleTeam.Ally ? towardEnemyHalf : -towardEnemyHalf;
    }

    private void UpdateVisualPosition(BattleUnit bu)
    {
        if (bu.visual == null) return;
        // 적도 아군과 동일한 좌표 변환으로 그린다 — 적 좌표가 이미 rows 4~7(아군 너머)라
        // 한 보드의 먼 절반으로 자연스럽게 이어진다. 별도 시각 오프셋 불필요(실전투 기준).
        // _visualOffset은 실전투에서 항상 Vector3.zero라 이 한 줄은 실전투 결과에 영향 없다 —
        // 미러 인스턴스만 파트너 보드 위치로 시각 오브젝트를 밀어서 그린다(판정 좌표는 그대로).
        Vector3 pos = GameManager.Instance.Board.CoordsToWorldPosition(bu.coords) + _visualOffset;
        bu.visual.transform.position = pos + Vector3.up * 0.5f;
    }

    // ─────────────────────────────────────────
    // 시뮬레이션
    // ─────────────────────────────────────────

    private void SimulateTick()
    {
        foreach (var bu in _units)
        {
            if (!bu.IsAlive) continue;

            foreach (var effect in bu.effects)
                effect.OnTick(bu, TICK_INTERVAL);

            if (!bu.IsAlive) continue; // 화상/도트 등으로 이번 틱에 죽었으면 행동 스킵

            SyncShieldVfx(bu); // 추적 보호막 출처 기준 상태 VFX ON/OFF + 위치 추적(수치 로직 무관)
        }

        TickBurn();

        foreach (var bu in _units)
        {
            if (!bu.IsAlive) continue;

            bu.TickCcState(TICK_INTERVAL);

            // 자뭉열매: 매초 15% 회복, 완전 회복하거나 다른 아군이 없으면 복귀(기획 확정 7/17).
            if (bu.berryActive)
                bu.TickBerry(TICK_INTERVAL, HasOtherAliveAlly(bu));

            // 마나 충전(기획 확정): 초당 10 고정 + 아이템 MP flat 가산(manaRegenBonus), 이후 GainMana가
            // manaGainMultiplier(정령 시너지 등 배수)를 곱한다 — (10+MP) × multiplier. 스턴 중에도 차오른다(행동만 불능).
            GainMana(bu, (MANA_PER_SECOND + bu.manaRegenBonus) * TICK_INTERVAL);

            if (bu.stunRemaining > 0f) continue; // 행동 불능 — 이동/공격 모두 스킵
            if (bu.IsUntargetable) continue;     // 자뭉열매 시식 중 — 행동 불능(마나는 위에서 충전됨)

            // 타겟 우선순위: 도발자(강제) > 도발 종료 복귀 타겟(날따름 스냅샷) > 일반 타겟팅
            // 언타겟(자뭉열매) 대상은 강제/복귀 타겟이라도 노릴 수 없다.
            BattleUnit target;
            if (bu.tauntedBy != null && bu.tauntedBy.IsAlive && !bu.tauntedBy.IsUntargetable)
            {
                target = bu.tauntedBy;
            }
            else if (bu.tauntReturnTarget != null && bu.tauntReturnTarget.IsAlive && !bu.tauntReturnTarget.IsUntargetable)
            {
                target = bu.tauntReturnTarget;
            }
            else
            {
                // 복귀 대상이 죽었으면 소비. 일시 언타겟(자뭉열매)이면 스냅샷은 남기고 이번 틱만 일반 타겟팅.
                if (bu.tauntReturnTarget != null && !bu.tauntReturnTarget.IsAlive)
                    bu.tauntReturnTarget = null;

                // 사거리 안 적 우선(있으면 그걸로 공격) → 없으면 역할 우선순위 최상단 적.
                // 예전엔 "이번 틱 전진 가능한 적"(FindReachableEnemy)을 한 단계 더 끼워 우선순위를
                // 양보했지만, MoveTowards가 BFS 우회 이동으로 바뀌면서 더 이상 필요 없어졌다(막혀도
                // 다음 틱에 우회 경로를 찾아 계속 전진하므로) — 순수 역할 우선순위만 남긴다.
                target = FindInRangeEnemy(bu) ?? FindNearestEnemy(bu);
            }
            bu.lastTickTarget = target; // 도발 발동 시 "원래 타겟" 스냅샷의 원천
            if (target == null) continue;

            // 노리는 대상을 바라보게 한다. 사거리 안이든 밖이든(=이동 중이든) 대상 기준이라
            // 원거리 유닛도 제자리에서 표적을 향하고, 도발당하면 도발자 쪽으로 돌아선다.
            // (?. 는 파괴된 UnityEngine.Object를 못 걸러내므로 != null 로 검사한다)
            // FaceTowards는 (전달값 - 자기 visual.position)으로 방향을 계산한다. 자기 위치가
            // 이미 _visualOffset만큼 밀려 있으므로(미러 인스턴스), 대상 좌표도 같은 오프셋을
            // 더해야 방향이 어긋나지 않는다. 실전투는 _visualOffset이 항상 0이라 영향 없다.
            if (bu.facing != null)
                bu.facing.FaceTowards(GameManager.Instance.Board.CoordsToWorldPosition(target.coords) + _visualOffset);

            int distance = bu.coords.DistanceTo(target.coords);

            if (distance <= bu.range)
            {
                bu.attackCooldown -= TICK_INTERVAL;
                if (bu.attackCooldown <= 0f)
                {
                    // 마나가 차 있으면 평타 대신 스킬 시전. 둘 다 같은 공속 쿨다운을 소모.
                    if (bu.HasSkill && bu.currentMana >= bu.maxMana)
                        CastSkill(bu, target);
                    else
                        BasicAttack(bu, target);

                    float baseCooldown = bu.attackSpeed > 0f ? 1f / bu.attackSpeed : 1f;
                    bu.attackCooldown += baseCooldown / Mathf.Max(0.01f, bu.slowMultiplier) / Mathf.Max(0.01f, bu.asBuffMultiplier);
                }
            }
            else
            {
                // attackCooldown과 동일한 패턴(유닛별 쿨다운 감소 → 0 이하면 행동 후 재충전).
                // 예전엔 moveSpeedMultiplier만큼 한 틱에 여러 칸을 스냅 이동시켰는데, 그러면
                // _moveInterval로 "이동 빈도"를 늦추려는 의도와 배속 가산이 이중 적용된다 —
                // 배속은 이제 간격을 줄이는 쪽(아래 나눗셈)으로만 반영하고, 한 틱당 최대 한 칸만
                // 옮긴다(공격도 같은 이유로 틱당 한 번만 처리하는 기존 attackCooldown 구조와 동일).
                bu.moveCooldown -= TICK_INTERVAL;
                if (bu.moveCooldown <= 0f)
                {
                    MoveTowards(bu, target);
                    bu.moveCooldown += Mathf.Max(0.02f, _moveInterval) / Mathf.Max(0.01f, bu.moveSpeedMultiplier);
                }
            }
        }

        // 이번 틱에 예약된 소환(나인이볼부스트)을 실제로 전투에 편입한다. 위 행동 루프가 _units를
        // foreach로 돌고 있어 그 안에서 Add하면 InvalidOperationException이 난다 — 루프가 끝난
        // 여기서 한 번에 옮긴다.
        FlushPendingSpawns();

        // 죽은 유닛 시각화 제거
        foreach (var bu in _units)
        {
            if (!bu.IsAlive && bu.visual != null)
            {
                Destroy(bu.visual);
                bu.visual = null;
                bu.facing = null; // visual과 함께 파괴됨 — 참조를 남기면 다음 틱에 파괴된 객체를 만진다

                // 보호막 상태 VFX는 visual의 자식이 아니라 world-space 독립 오브젝트라 자동으로 같이
                // 파괴되지 않는다 — 명시적으로 정리해야 한다.
                if (bu.shieldVfxInstance != null)
                    Destroy(bu.shieldVfxInstance);
                bu.shieldVfxInstance = null;
                bu.shieldSources.Clear();
                bu.skillShieldSource = null;
            }
        }
    }

    // ── 아이템 고유효과 VFX(2026-08, 기존 VFX 재사용만) ──

    private const string ITEM_SHIELD_STATE_VFX_ID = "VFX_Item_Shield_State"; // VFX_Item_White_SHIELD.prefab, VfxDatabase 등록

    private static bool _shieldVfxMissingWarned;

    /// <summary>
    /// 장비 보호막 출처(Apicot/Shell Bell/Micle) 기준 상태 VFX. 스킬 SHIELD는 제외한다 — 스킬은 시전
    /// 순간 _Shield/ 8종 캐스트 VFX가 따로 재생되므로 이 상태 VFX와 겹쳐 띄우지 않는다(2026-08,
    /// VFX_Item_White_SHIELD 정식 아트 적용). BattleUnit.shieldSources에 remainingAmount>0인 장비
    /// 출처가 하나라도 있으면 유지, 전부 0이면 제거 — WATER 시너지 등 미추적 보호막은 shieldSources에
    /// 아예 들어가지 않으므로 이 판정에 절대 영향을 주지 않는다(WATER만 남아있으면 이 VFX는 안 뜸).
    ///
    /// 기존 SHIELD 스킬 8종(_Shield/ 폴더, BattleVfxPlayer.PlaySkill 경유)과 동일하게 world-space에
    /// 독립 생성한다(bu.visual의 자식이 아님) — 종족 모델 프리팹마다 제각각인 scale을 원천적으로
    /// 상속하지 않는다. 위치는 매틱 bu.visual.transform.position + entry.positionOffset으로 추적한다.
    ///
    /// <b>높이와 크기는 코드가 정하지 않는다</b> — 오프셋은 VfxDatabase 엔트리, 크기는 프리팹 루트
    /// scale이 정한다. BattleVfxPlayer.Create와 같은 규칙이라 쉴드가 늘어나도 코드를 건드릴 일이 없고,
    /// 아트가 프리팹만 보고 다른 쉴드와 중심을 맞출 수 있다(쉴드 중심 높이 = 루트 Y + 본체 로컬 Y ×
    /// 루트 scale인데, 루트 Y는 아래 Instantiate가 덮어쓰므로 높이 보정은 positionOffset으로 준다).
    ///
    /// BattleVfxPlayer는 생성 즉시 lifetime 뒤 자동 Destroy가 전제라 "출처가 남아있는 동안 계속 유지"
    /// 라는 상태형 요구와 맞지 않아 쓰지 않는다 — VfxDatabase에서 엔트리만 조회해 직접
    /// Instantiate/Destroy한다.
    /// </summary>
    private void SyncShieldVfx(BattleUnit bu)
    {
        // 안전한 매틱 정리 지점 — 이번 틱의 흡수(AbsorbShieldDamage)/시간 갱신(OnTick)이 전부 끝난
        // 뒤라 순회 중 컬렉션 변경 문제가 없다. remainingAmount만 바꾸던 단계와 분리된 별도 단계.
        bu.shieldSources.RemoveAll(s => s.remainingAmount <= 0f);

        if (!_playBattleVfx) return;

        bool hasItemShield = HasActiveItemShield(bu);

        if (hasItemShield && bu.shieldVfxInstance == null && bu.visual != null)
        {
            var entry = ResolveShieldVfxEntry();
            if (entry == null || entry.prefab == null)
            {
                if (!_shieldVfxMissingWarned)
                {
                    Debug.LogWarning($"[Vfx] '{ITEM_SHIELD_STATE_VFX_ID}' 미등록 또는 prefab 비어있음 — VfxDatabase.asset에 등록하세요.");
                    _shieldVfxMissingWarned = true;
                }
                return;
            }

            // 크기는 건드리지 않는다 — 프리팹 루트 scale이 그대로 최종 크기다(BattleVfxPlayer와 동일).
            var go = Instantiate(entry.prefab,
                                 bu.visual.transform.position + entry.positionOffset,
                                 Quaternion.identity);

            int layer = ResolveVisualLayer();
            if (layer >= 0) SetDefaultLayerRecursive(go.transform, layer);

            bu.shieldVfxInstance = go;
        }
        else if (!hasItemShield && bu.shieldVfxInstance != null)
        {
            Destroy(bu.shieldVfxInstance);
            bu.shieldVfxInstance = null;
        }
        else if (bu.shieldVfxInstance != null && bu.visual != null)
        {
            // 위치만 매틱 추적. 생성 때와 같은 오프셋을 더해야 유닛이 움직여도 높이가 유지된다.
            var entry = ResolveShieldVfxEntry();
            Vector3 offset = entry != null ? entry.positionOffset : Vector3.zero;

            bu.shieldVfxInstance.transform.position = bu.visual.transform.position + offset;
        }
    }

    /// <summary>
    /// 상태 쉴드 VFX 엔트리 조회. 생성과 매틱 추적 두 곳이 같은 positionOffset을 써야 해서 한 곳으로 모은다.
    /// 캐싱하지 않는다 — 못 찾았을 때 캐시해 두면 나중에 등록해도 영영 안 잡힌다(Get은 딕셔너리 조회).
    /// </summary>
    private static VfxEntry ResolveShieldVfxEntry() =>
        VfxDatabase.Instance != null ? VfxDatabase.Instance.Get(ITEM_SHIELD_STATE_VFX_ID) : null;

    /// <summary>
    /// 장비로 얻은 보호막이 살아 있는지. 스킬 SHIELD는 일부러 제외한다 —
    /// 스킬은 시전 순간 _Shield/ 8종 캐스트 VFX가 따로 재생되므로 상태 VFX를 겹쳐 띄우지 않는다.
    /// </summary>
    private static bool HasActiveItemShield(BattleUnit bu)
    {
        foreach (var source in bu.shieldSources)
        {
            if (source.remainingAmount <= 0f) continue;
            if (source.type != ShieldSourceType.Skill) return true;
        }

        return false;
    }

    /// <summary>
    /// 단일 대상 위치에 1회성 VFX 재생(아이템 고유효과 발동 연출). 기존 BattleVfxPlayer.PlayOnUnit을
    /// _playBattleVfx 게이트 + ResolveVisualLayer()까지 한 번에 감싼 얇은 래퍼 — ItemConditionalEffect처럼
    /// 이 두 private 멤버에 직접 접근할 수 없는 다른 클래스도 실전투/미러 동일한 Layer 규칙을 그대로
    /// 따르게 하기 위함(internal이라 같은 어셈블리 내 호출만 가능).
    /// </summary>
    internal void PlaySingleTargetVfx(string vfxId, BattleUnit target)
    {
        if (!_playBattleVfx) return;
        BattleVfxPlayer.PlayOnUnit(vfxId, target, this, ResolveVisualLayer());
    }

    /// <summary>매틱 burnTicksRemaining>0인 모든 유닛에 고정(True) 피해. 아이템 보유와 무관하게 "화상이 옮은" 유닛 전체 대상.</summary>
    private void TickBurn()
    {
        foreach (var bu in _units)
        {
            if (!bu.IsAlive || bu.burnTicksRemaining <= 0f) continue;

            bu.currentHp -= bu.burnDamagePerTick;
            bu.TryTriggerSitrusBerry(); // 화상 딜로 45% 미만 진입해도 발동
            bu.burnTicksRemaining -= 1f;
            if (bu.burnTicksRemaining <= 0f) bu.burnDamagePerTick = 0f;
        }
    }

    /// <summary>self를 실제로 타겟 중인 적 팀 유닛 수(defSpDefPerAttacker용, 티라프열매 기획 확정
    /// 2026-08 — "사거리 내 적 수"가 아니라 "나를 공격 대상으로 삼고 있는 적 수"). lastTickTarget은
    /// SimulateTick의 타겟팅 루프(두 번째 foreach)에서 매틱 갱신되는데, OnTick(이 메서드의 유일한
    /// 호출측인 ItemConditionalEffect)은 그보다 앞선 첫 번째 foreach에서 도므로 정확히 1틱
    /// (TICK_INTERVAL)만큼 지연된 값을 읽는다 — 화면상 체감되지 않는 수준이라 별도 보정하지 않는다.
    /// 죽은 유닛은 lastTickTarget이 남아 있어도 IsAlive 검사로 제외된다.</summary>
    public int CountUnitsTargeting(BattleUnit self)
    {
        int count = 0;
        foreach (var other in _units)
            if (other.team != self.team && other.IsAlive && other.lastTickTarget == self)
                count++;
        return count;
    }

    /// <summary>center(피격자) 주변의 적 팀 유닛에 화상 설정(비중첩 — 틱 수만 갱신).</summary>
    public void ApplyBurnAround(BattleUnit center, int radius, float dmgPerTick, int ticks)
    {
        foreach (var u in _units)
        {
            if (u.team == center.team || !u.IsAlive || u.IsUntargetable) continue; // 언타겟은 신규 화상 부여 제외(기존 화상은 계속 틱)
            if (center.coords.DistanceTo(u.coords) > radius) continue;

            u.burnDamagePerTick = dmgPerTick;
            u.burnTicksRemaining = ticks;
        }
    }

    // ─────────────────────────────────────────
    // 데미지 공식 (TFT식 비율 경감)
    // ─────────────────────────────────────────
    // 데미지 = 기본위력 × 경감(방어) × 크리배수.
    // 경감은 100/(100+def) → 방어 1당 유효체력 +1%, 항상 양수(max(1) 불필요),
    // 별 배수와 곱셈으로 공존. 관통/타입상성은 추후 이 두 헬퍼에 곱셈 레이어로 확장.

    /// <summary>
    /// 방어 비율 경감 계수. def 1당 유효체력 +1%. (관통은 여기서 def를 깎는 식으로 확장)
    /// </summary>
    public static float Mitigation(float def) => 100f / (100f + Mathf.Max(0f, def));

    /// <summary>크리 기대값 배수(난수 없는 결정론 — 2인 동기화 안전). 크리 없으면 1.</summary>
    public static float CritFactor(BattleUnit a) => 1f + a.critChance * (a.critMultiplier - 1f);

    /// <summary>평타 1회: attack 기반 물리 피해(파이프라인). 마나는 초당 충전만(기획 확정) — 평타 획득 없음.</summary>
    private void BasicAttack(BattleUnit attacker, BattleUnit target)
    {
        // 피해 적용 전 — 이번 틱에 죽어도 피격 위치에 재생(스킬 VFX와 동일 규칙).
        // this를 scope로 넘겨 이 인스턴스(실전투/미러)가 만든 VFX만 자기 Cleanup()에서 정리되게 하고,
        // ResolveVisualLayer()로 유닛 모델과 같은 기준의 Layer를 찍는다(로컬/파트너 화면 분리).
        if (_playBattleVfx)
            BattleVfxPlayer.PlayBasicAttack(attacker.attackVfxId, attacker, target, this, ResolveVisualLayer());

        ResolveDamage(new DamageContext(attacker, target, attacker.attack, DamageType.Physical, isBasicAttack: true));

        foreach (var effect in attacker.effects)
            effect.OnBasicAttack(attacker, target);
    }

    /// <summary>
    /// 스킬 시전: 마나 소모(0으로) 후 effectType 분기.
    /// Attack/Spell=데미지(attack/spellPower 위력), Stun/Slow/Taunt=CC(적 대상), HpRegen/Shield/ManaRegen/AsBuff=지원(아군 대상).
    /// 전부 기획 수치 PLACEHOLDER(지속시간/위력 등) — 메커니즘은 선구현, 수치는 확정 후 교체.
    /// </summary>
    private void CastSkill(BattleUnit caster, BattleUnit primaryTarget)
    {
        caster.currentMana = 0f;

        // [악 시너지] 첫 스킬 시전 시 대상 스턴(1회 소비). 스킬 종류와 무관하게 적용되며,
        // 살아있는 적 대상이 있을 때만(지원 스킬의 primaryTarget이 아군일 가능성 방어).
        if (caster.darkFirstSkillPending)
        {
            caster.darkFirstSkillPending = false;
            if (primaryTarget != null && primaryTarget.IsAlive && primaryTarget.team == BattleTeam.Enemy)
                primaryTarget.ApplyStun(SynergyConstants.DarkFirstSkillStunSeconds);
        }

        // [이브이 영웅증강 v2] 나인이볼부스트 — 이 시전이 소환에 쓰였으면 원래 스킬 효과는 내지 않는다.
        // 스킬 자체가 "진화체를 부르는 것"으로 대체되기 때문이다(기획 확정 2026-08-19: 별도 VFX 없이
        // 소환되는 진화체가 곧 연출). 8종을 다 부른 뒤에는 false가 돌아와 원래 스킬로 되돌아간다.
        if (TryCastHeroEeveeBoost(caster))
        {
            foreach (var effect in caster.effects)
                effect.OnSkillCast(caster);
            return;
        }

        // 데미지 스킬만 처리. 지원(HP_REGEN/SHIELD/AS_BUFF/MANA_REGEN)은 Phase2, CC(SLOW/STUN/TAUNT)는 기둥C에서 구현.
        bool isDamage = caster.skillEffectType == SkillEffectType.Attack ||
                        caster.skillEffectType == SkillEffectType.Spell;
        if (!isDamage)
        {
            ApplyCcOrSupportSkill(caster, primaryTarget);

            foreach (var effect in caster.effects)
                effect.OnSkillCast(caster);
            return;
        }

        // ATTACK=attack(물리), SPELL=spellPower(마법). 경감은 어차피 defense 하나지만 타입은 효과 조건용.
        float power = caster.skillEffectType == SkillEffectType.Attack ? caster.attack : caster.spellPower;
        DamageType type = caster.skillEffectType == SkillEffectType.Attack ? DamageType.Physical : DamageType.Magic;

        var targets = GetSkillTargets(caster, primaryTarget);
        // 피해 적용 전 — 이번 틱에 죽어도 위치에 재생. 장판 중심은 타겟팅 기준과 동일하게 피격 대상.
        // scope/layer는 BasicAttack과 같은 이유로 넘긴다(§ BasicAttack 주석 참고).
        if (_playBattleVfx)
            BattleVfxPlayer.PlaySkill(caster.skillVfxId, targets, primaryTarget, caster.skillAreaRadius,
                                      caster, primaryTarget, this, ResolveVisualLayer());

        foreach (var t in targets)
        {
            if (t == null || !t.IsAlive) continue;
            ResolveDamage(new DamageContext(caster, t, power, type, isBasicAttack: false));
        }

        Debug.Log($"[Battle] {caster.team} 스킬 시전({caster.skillEffectType}) → 대상 {targets.Count}기 (위력 {power:0})");

        foreach (var effect in caster.effects)
            effect.OnSkillCast(caster);
    }

    /// <summary>
    /// CC(Slow/Stun/Taunt, 적 대상) + 지원(HpRegen/Shield/ManaRegen/AsBuff, 아군 대상) 스킬 적용.
    /// 지원형은 primaryTarget(적)을 안 쓰고 GetAllyTargets로 자체 타겟팅한다.
    /// </summary>
    private void ApplyCcOrSupportSkill(BattleUnit caster, BattleUnit primaryTarget)
    {
        bool isSupport = caster.skillEffectType == SkillEffectType.HpRegen   ||
                          caster.skillEffectType == SkillEffectType.Shield   ||
                          caster.skillEffectType == SkillEffectType.ManaRegen ||
                          caster.skillEffectType == SkillEffectType.AsBuff;

        // 날따름(Taunt)은 "시전자 중심 반경 내 적"(기획 확정) — 대상 중심인 EnemyArea와 달라 별도 타겟팅.
        List<BattleUnit> targets;
        if (caster.skillEffectType == SkillEffectType.Taunt)
            targets = GetEnemiesAroundCaster(caster, caster.skillAreaRadius);
        else
            targets = isSupport ? GetAllyTargets(caster) : GetSkillTargets(caster, primaryTarget);

        // 장판 중심은 타겟팅 기준과 일치시킨다 — 지원/날따름은 시전자 중심, 그 외(CC)는 피격 대상 중심.
        bool centeredOnCaster = isSupport || caster.skillEffectType == SkillEffectType.Taunt;
        // scope/layer는 BasicAttack과 같은 이유로 넘긴다(§ BasicAttack 주석 참고).
        if (_playBattleVfx)
            BattleVfxPlayer.PlaySkill(caster.skillVfxId, targets,
                                      centeredOnCaster ? caster : primaryTarget, caster.skillAreaRadius,
                                      caster, primaryTarget, this, ResolveVisualLayer());

        // 날따름 지속시간(기획 확정): base 1.0s × 1.4(영웅증강) × 성급 배수(1.0/1.8/2.8)
        float tauntDuration = TAUNT_BASE_DURATION * TAUNT_HERO_STAT_MULT *
                              TAUNT_STAR_MULT[Mathf.Clamp(caster.starLevel, 1, 3)];

        // 데미지 스킬(CastSkill 본문)과 달리 CC/지원은 여기서 로그 — 안 찍으면 시전 여부를 로그로 검증할 수 없다.
        Debug.Log($"[Battle] {caster.team} 스킬 시전({caster.skillEffectType}) → 대상 {targets.Count}기" +
                  (caster.skillEffectType == SkillEffectType.Taunt ? $" (도발 {tauntDuration:0.0}s)" : ""));

        foreach (var t in targets)
        {
            if (t == null || !t.IsAlive) continue;

            switch (caster.skillEffectType)
            {
                case SkillEffectType.Stun: // 기획 확정: 이번 스코프 데이터 미사용(증강 확장 예정) — 메커니즘만 유지
                    t.ApplyStun(STUN_DURATION);
                    break;
                case SkillEffectType.Slow: // 보류(유닛/아이템 미구현) — 수치 미확정
                    t.ApplySlow(SLOW_MULTIPLIER, SLOW_DURATION);
                    break;
                case SkillEffectType.Taunt:
                    t.ApplyTaunt(caster, tauntDuration);
                    // 도발 적용 시 각 대상 위치에 이펙트 재생(스킬 시전 이펙트와 별개).
                    // scope/layer는 BasicAttack과 같은 이유로 넘긴다(§ BasicAttack 주석 참고).
                    if (_playBattleVfx)
                        BattleVfxPlayer.PlayTauntHit(t, this, ResolveVisualLayer());
                    break;
                case SkillEffectType.HpRegen:
                    t.ApplyHeal(caster.spellPower);
                    break;
                case SkillEffectType.Shield:
                    t.ApplyShield(caster.spellPower);
                    // 스킬 SHIELD 출처 등록/합산(2026-08 확정) — 재시전 시 이전 출처가 아직 살아있으면
                    // 그 객체에 합산(가산형, shield += 와 동일 철학), 없거나 이미 소진됐으면 새로 생성한다.
                    // 종료된 객체를 리스트에 남겨둔 채 재사용하지 않는다(RemoveAll이 정리한 뒤에도 문제
                    // 없도록 항상 "살아있는지"부터 확인).
                    if (t.skillShieldSource != null && t.skillShieldSource.remainingAmount > 0f)
                    {
                        t.skillShieldSource.remainingAmount += caster.spellPower;
                    }
                    else
                    {
                        var skillSource = new ShieldSource(ShieldSourceType.Skill, caster.spellPower,
                            ShieldDecayType.None, 0f, t.NextShieldSequence());
                        t.skillShieldSource = skillSource;
                        t.shieldSources.Add(skillSource);
                    }
                    break;
                case SkillEffectType.ManaRegen: // 회복량 = spellPower × 계수 (즉시 지급, 본인 제외 반경 내 아군)
                    GainMana(t, caster.spellPower * SUPPORT_BUFF_SPELLPOWER_COEF);
                    break;
                case SkillEffectType.AsBuff:    // 증가량 = (spellPower × 계수)%p (본인 제외 반경 내 아군)
                    t.ApplyAsBuff(1f + caster.spellPower * SUPPORT_BUFF_SPELLPOWER_COEF / 100f, AS_BUFF_DURATION);
                    break;
            }
        }
    }

    /// <summary>시전자 중심 반경 내 살아있는 적 전부 (날따름 전용 타겟팅).</summary>
    private List<BattleUnit> GetEnemiesAroundCaster(BattleUnit caster, int radius)
    {
        var result = new List<BattleUnit>();
        foreach (var u in _units)
            if (u.team != caster.team && u.IsAlive && !u.IsUntargetable &&
                caster.coords.DistanceTo(u.coords) <= radius)
                result.Add(u);
        return result;
    }

    /// <summary>targetType별 아군 대상 목록(지원 스킬용). AllySelf=자신, AllyArea=반경 내 아군, AllySingle=최저 HP비율 아군.</summary>
    private List<BattleUnit> GetAllyTargets(BattleUnit caster)
    {
        var result = new List<BattleUnit>();

        switch (caster.skillTargetType)
        {
            case SkillTargetType.AllySelf:
                result.Add(caster);
                break;

            case SkillTargetType.AllyArea:
            {
                // 지원 버프(공속 증가·마나 회복)는 <b>시전자 본인을 뺀</b> 반경 내 아군이다
                // (기획 확정 2026-08-19). 반경 판정은 다른 ALLY_AREA 스킬과 똑같이 적용되고,
                // 본인을 빼는 것만이 이 두 효과의 차이다.
                //
                // 판정을 role이 아니라 effectType으로 하는 이유: BattleUnit.role은 영웅증강이
                // 덮어쓸 수 있어(roleOverride) 전투 중에 바뀔 수 있는 값이다. 반면 이 두 효과를
                // ALLY_AREA로 쓰는 스킬은 데이터상 전부 서포터 스킬(Nuzzle/Coaching/Celebrate/Charge)이라
                // 결과가 같으면서 흔들리지 않는다.
                //
                // ⚠️ 본인 제외는 이 두 효과에만 붙는다. 같은 ALLY_AREA라도 HP_REGEN(GrassWhistle)·
                //    SHIELD(SparklingAria/MagicCoat)는 <b>본인을 포함</b>해야 한다 — 자기 회복·자기 보호막이
                //    빠지면 안 되기 때문. ALLY_SELF(SelfASBuff 47마리 등)는 애초에 이 분기로 오지도 않지만,
                //    혹시라도 같이 묶으면 대상이 0명이 되어 스킬이 통째로 무효가 된다.
                bool excludeSelf = caster.skillEffectType == SkillEffectType.AsBuff ||
                                   caster.skillEffectType == SkillEffectType.ManaRegen;

                foreach (var u in _units)
                {
                    if (u.team != caster.team || !u.IsAlive || u.IsUntargetable) continue;
                    if (excludeSelf && u == caster) continue;
                    if (caster.coords.DistanceTo(u.coords) > caster.skillAreaRadius) continue;

                    result.Add(u);
                }
                break;
            }

            // AllySingle: 데이터 설계 의도(PokemonSkillData 주석) = HpRegen→최저HP, Shield→탱커.
            case SkillTargetType.AllySingle when caster.skillEffectType == SkillEffectType.Shield:
            {
                BattleUnit tanker = null;
                foreach (var u in _units)
                    if (u.team == caster.team && u.IsAlive && !u.IsUntargetable && u.role == PokemonRole.Tanker) { tanker = u; break; }
                var fallback = tanker ?? LowestHpRatioAlly(caster.team);
                if (fallback != null) result.Add(fallback); // 탱커 없으면 최저HP로 폴백
                break;
            }

            case SkillTargetType.AllySingle:
            {
                var weakest = LowestHpRatioAlly(caster.team);
                if (weakest != null) result.Add(weakest);
                break;
            }

            default: // Enemy* 타입이 지원 스킬에 잘못 설정된 경우 — 대상 없음
                break;
        }

        return result;
    }

    /// <summary>team 진영에서 살아있는 유닛 중 currentHp/maxHp 비율이 가장 낮은 유닛. 동률이면
    /// _units 순회 순서상 먼저 나온 쪽(결정론). ItemConditionalEffect(큰뿌리)가 재사용할 수 있도록
    /// public — 기존 스킬 HP_REGEN/SHIELD 타겟팅과 동일한 판정 기준을 그대로 공유한다(새 로직 없음).</summary>
    public BattleUnit LowestHpRatioAlly(BattleTeam team)
    {
        BattleUnit weakest = null;
        float weakestRatio = float.MaxValue;
        foreach (var u in _units)
        {
            if (u.team != team || !u.IsAlive || u.IsUntargetable) continue;
            float ratio = u.maxHp > 0f ? u.currentHp / u.maxHp : 0f;
            if (ratio < weakestRatio) { weakestRatio = ratio; weakest = u; }
        }
        return weakest;
    }

    /// <summary>targetType별 피격 대상 목록(데미지 스킬용 = 적 대상). 항상 살아있는 적만 반환.</summary>
    private List<BattleUnit> GetSkillTargets(BattleUnit caster, BattleUnit primaryTarget)
    {
        var result = new List<BattleUnit>();
        if (primaryTarget == null) return result;

        switch (caster.skillTargetType)
        {
            case SkillTargetType.EnemyArea: // 대상 중심 반경 내 적 전부(언타겟 제외)
                foreach (var u in _units)
                    if (u.team != caster.team && u.IsAlive && !u.IsUntargetable &&
                        u.coords.DistanceTo(primaryTarget.coords) <= caster.skillAreaRadius)
                        result.Add(u);
                break;

            case SkillTargetType.EnemyLine: // 시전자 사거리 내 + 대상 방향(앞쪽)의 적(언타겟 제외)
            {
                int toTarget = caster.coords.DistanceTo(primaryTarget.coords);
                foreach (var u in _units)
                    if (u.team != caster.team && u.IsAlive && !u.IsUntargetable &&
                        caster.coords.DistanceTo(u.coords) <= caster.skillLineLength &&
                        u.coords.DistanceTo(primaryTarget.coords) <= toTarget)
                        result.Add(u);
                if (!result.Contains(primaryTarget) && !primaryTarget.IsUntargetable) result.Add(primaryTarget);
                break;
            }

            case SkillTargetType.EnemySingle:
                if (!primaryTarget.IsUntargetable) result.Add(primaryTarget);
                break;

            default: // Ally* (지원 스킬) — 데미지 경로에선 대상 없음(Phase2에서 아군 타겟팅 구현)
                break;
        }

        return result;
    }

    /// <summary>
    /// 데미지 파이프라인(단일 진입점). 모든 평타·스킬 피해가 여기를 통과한다.
    /// 순서: 공격자 효과(OnDealDamage) → 크리 → 경감(True 제외) → 피격자 효과(OnTakeDamage) → 적용+마나 → OnKill.
    /// 효과 훅(기둥B)이 ctx.amount/type/플래그를 수정해 "X일때 Y"를 구현한다.
    /// </summary>
    private void ResolveDamage(DamageContext ctx)
    {
        if (ctx.target == null || !ctx.target.IsAlive) return;

        foreach (var effect in ctx.source.effects)
            effect.OnDealDamage(ctx.source, ctx);

        // 피해 증폭 파이프라인(기획 확정, 2026-08): 기본 피해 → 평타/스킬 전용 Amp → 공용 AMP → 크리
        // → 경감. "공격자 효과(OnDealDamage)" 단계 직후에 위치시켜 크리·경감보다 먼저 적용한다.
        //
        // 평타/스킬 전용 증폭 — 서로 배타적(같은 히트가 평타이면서 스킬일 수 없음). 같은 계열 내에서는
        // basicAttackDamageAmpPct/skillDamageAmpPct 자체가 이미 여러 아이템의 값을 가산해 담고 있으므로
        // (왕의징표석+선제공격손톱처럼) 여기서는 그 합계를 1번만 곱한다 — 복리 아님. HP_REGEN/SHIELD/
        // ManaRegen/AsBuff 등 비피해 스킬은 ResolveDamage 자체를 타지 않아 자동으로 제외된다.
        if (ctx.isBasicAttack)
        {
            if (ctx.source.basicAttackDamageAmpPct > 0f)
                ctx.amount *= 1f + ctx.source.basicAttackDamageAmpPct * 0.01f;
        }
        else if (ctx.source.skillDamageAmpPct > 0f)
        {
            ctx.amount *= 1f + ctx.source.skillDamageAmpPct * 0.01f;
        }

        // 공용 AMP(damageAmpPct, 왕의징표석 25스택 등) — 평타/스킬 모두 적용. 위 전용 증폭과는 별도
        // 단계로 순차 곱한다(같은 계열끼리의 복리가 아니라 "서로 다른 계열끼리의 순차 곱").
        if (ctx.source.damageAmpPct > 0f)
            ctx.amount *= 1f + ctx.source.damageAmpPct * 0.01f;

        // 크리 (현재 결정론적 기대값 — 각자 보드 로컬 시뮬이라 추후 RNG 크리로 교체 가능)
        ctx.amount *= CritFactor(ctx.source);

        // 경감 (True 타입은 경감 무시 고정딜)
        if (ctx.type != DamageType.True)
            ctx.amount *= Mitigation(ctx.target.defense);

        // 보호막 흡수 — 아이템 보유 여부와 무관하게 항상 정확히 1회 실행(2026-08 확정, 기존
        // ItemConditionalEffect.OnTakeDamage 내부에 있던 흡수 블록을 여기로 이동). preShieldAmount는
        // "공격받았다"는 사실 자체를 쓰는 효과(왕의징표석 등)를 위한 스냅샷 — 흡수로 줄어들기 전 값.
        ctx.preShieldAmount = ctx.amount;
        ShieldAbsorbResult shieldResult = ctx.target.AbsorbShieldDamage(ctx.amount);
        ctx.amount = shieldResult.remainingDamage;
        ctx.depletedShieldSources = shieldResult.depletedSources;

        foreach (var effect in ctx.target.effects)
            effect.OnTakeDamage(ctx.target, ctx);

        // 피해 적용. 피격 비례 마나 획득은 기획 확정(초당 충전만)으로 제거됨.
        float hpBeforeDamage = ctx.target.currentHp;
        ctx.target.currentHp -= ctx.amount;

        // 실제로 HP에서 줄어든 양(오버킬 제외, 보호막이 이미 흡수한 만큼도 제외 — ctx.amount는 보호막
        // 흡수 이후 값이라 여기 반영돼 있음). ctx.amount를 그대로 "가한 피해"로 오인하면 과잉 피해분
        // (예: HP30인 대상에게 100 피해)까지 큰뿌리 회복 계산에 들어가 버리므로 반드시 실측 차이로
        // 구한다. 공격자 effects에 알려 큰뿌리(자신이 가한 최종 피해량 기준 아군 회복) 등이 쓴다 —
        // 이 시점은 증폭·크리·경감·보호막 흡수·HP 반영까지 전부 끝난 뒤라 "최종 피해량" 요건을 만족한다.
        float actualHpDamage = Mathf.Max(0f, hpBeforeDamage - Mathf.Max(0f, ctx.target.currentHp));
        foreach (var effect in ctx.source.effects)
            effect.OnDealDamageResolved(ctx.source, ctx.target, actualHpDamage);

        // OnTakeDamage 단계에서 예약된 자가 회복(ctx.pendingSelfHeal, 기합의머리띠 등)을 피해 적용
        // "직후"에 넣는다 — 피해 적용 전에 먼저 걸면 만피 상태에서 MaxHP 클램프에 막혀 회복분이
        // 소실된다(2026-08 실측 확인). 이 피해로 죽었으면(currentHp<=0) 회복하지 않는다(사후 부활 방지).
        if (ctx.pendingSelfHeal > 0f && ctx.target.currentHp > 0f)
            ctx.target.currentHp = Mathf.Min(ctx.target.maxHp, ctx.target.currentHp + ctx.pendingSelfHeal);

        ctx.target.TryTriggerSitrusBerry(); // 자뭉열매: 45% 미만 진입 순간 발동(전투당 1회)

        if (!ctx.target.IsAlive)
        {
            if (ctx.target.team == BattleTeam.Enemy && SoundManager.TryGet(out var deathSoundManager))
                deathSoundManager.PlaySfx(SoundId.EnemyDeath);

            foreach (var effect in ctx.source.effects)
                effect.OnKill(ctx.source, ctx.target);
        }
    }

    /// <summary>마나 획득(스킬 보유 유닛만, maxMana 상한). manaGainMultiplier(정령 시너지/치어리더 등 상시 배수) 반영.
    /// public — 라즈열매(ItemConditionalEffect.OnCombatStart)가 동일한 clamp 경로를 재사용하기 위해 개방.</summary>
    public static void GainMana(BattleUnit unit, float amount)
    {
        if (!unit.HasSkill) return;
        unit.currentMana = Mathf.Min(unit.maxMana, unit.currentMana + amount * unit.manaGainMultiplier);
    }

    /// <summary>role 우선순위(낮을수록 먼저 타겟) → 동순위 내 최단거리로 타겟 선정(기둥C).</summary>
    private BattleUnit FindNearestEnemy(BattleUnit bu)
    {
        BattleUnit best = null;
        int bestPriority = int.MaxValue;
        int bestDist = int.MaxValue;

        foreach (var other in _units)
        {
            if (other.team == bu.team || !other.IsAlive || other.IsUntargetable) continue;

            int priority = ROLE_TARGET_PRIORITY.TryGetValue(other.role, out var p) ? p : DEFAULT_ROLE_PRIORITY;
            int dist = bu.coords.DistanceTo(other.coords);

            if (priority < bestPriority || (priority == bestPriority && dist < bestDist))
            {
                bestPriority = priority;
                bestDist = dist;
                best = other;
            }
        }

        return best;
    }

    /// <summary>
    /// role 우선순위(낮을수록 먼저) → 동순위 내 최단거리로, 현재 bu.range 안에 있는 적만 대상으로 타겟 선정.
    /// FindNearestEnemy와 tie-break 규칙은 동일하고 사거리 필터만 추가됐다 — "앞이 막혀 못 가는 뒤쪽
    /// 우선순위 적" 대신 지금 때릴 수 있는 적을 먼저 고르기 위함(일반 타겟팅에서만 사용, 도발 경로는 무관).
    /// </summary>
    private BattleUnit FindInRangeEnemy(BattleUnit bu)
    {
        BattleUnit best = null;
        int bestPriority = int.MaxValue;
        int bestDist = int.MaxValue;

        foreach (var other in _units)
        {
            if (other.team == bu.team || !other.IsAlive || other.IsUntargetable) continue;

            int dist = bu.coords.DistanceTo(other.coords);
            if (dist > bu.range) continue;

            int priority = ROLE_TARGET_PRIORITY.TryGetValue(other.role, out var p) ? p : DEFAULT_ROLE_PRIORITY;

            if (priority < bestPriority || (priority == bestPriority && dist < bestDist))
            {
                bestPriority = priority;
                bestDist = dist;
                best = other;
            }
        }

        return best;
    }

    /// <summary>
    /// 타겟을 공격할 수 있는 위치로 한 칸 이동. FindNextStep이 고른 다음 칸으로 옮기기만 한다
    /// (자기 자신이면 이동 없음 — 완전 봉쇄). 실제로 이동했을 때만 옮기기 전 좌표를
    /// _previousCoords에 남긴다 — FindNextStep이 다음 호출에서 "방금 왔던 칸으로 즉시
    /// 되돌아가는" 정상 후보 제외/fallback 판정에 쓴다(경로 캐시 아님).
    /// 이 함수는 SimulateTick 한 번(TICK_INTERVAL)당 최대 1회만 호출되고, 호출될 때마다 정확히
    /// 한 칸만 옮긴다(2026-08 코드리뷰 대응 — 같은 틱 안에서 반복 호출되던 이전 동작 설명 정정).
    /// moveSpeedMultiplier는 이 함수의 호출 횟수나 이동 칸수가 아니라, 호출 주기인
    /// bu.moveCooldown의 재충전 간격(Max(0.02, _moveInterval) / moveSpeedMultiplier)에만 반영된다 —
    /// 배율이 높을수록 여러 SimulateTick에 걸쳐 더 자주(간격이 짧게) 호출될 뿐이다. 매 호출 시점의
    /// 현재 위치·점유 상태 기준으로 새로 계산되는 점(경로 캐시 아님)은 이전과 동일하다.
    /// </summary>
    private void MoveTowards(BattleUnit bu, BattleUnit target)
    {
        HexCoords next = FindNextStep(bu, target);
        if (next == bu.coords) return;

        _previousCoords[bu] = bu.coords;
        bu.coords = next;
        UpdateVisualPosition(bu);
    }

    /// <summary>
    /// FindNextStep이 정상 후보(best)·fallback 후보 양쪽에 똑같이 쓰는 areaDist → steps →
    /// CompareCoords 사전식 비교. 후보(candidate)가 지금까지의 최선(current, hasCurrent가
    /// false면 아직 없음)보다 나은지 판정한다 — 두 후보 집합의 비교 규칙이 어긋나면 네트워크
    /// 동기화(결정적 tie-break)가 깨지므로 반드시 이 한 곳만 고치면 되게 모아뒀다.
    /// </summary>
    private static bool IsBetterCandidate(
        bool hasCurrent, int candidateAreaDist, int candidateSteps, HexCoords candidate,
        int currentAreaDist, int currentSteps, HexCoords current)
    {
        if (!hasCurrent) return true;
        if (candidateAreaDist != currentAreaDist) return candidateAreaDist < currentAreaDist;
        if (candidateSteps != currentSteps) return candidateSteps < currentSteps;
        return CompareCoords(candidate, current) < 0;
    }

    /// <summary>
    /// bu.coords에서 시작해 (유효 좌표 ∩ 미점유) 칸만으로 BFS 전체 탐색을 한 번 수행하고,
    /// 아래 기준으로 가장 좋은 도달 가능한 칸까지의 첫 스텝을 돌려준다(캐시 없음 — 호출마다 새로 계산):
    ///   1차: target을 bu.range 이내에서 때릴 수 있는 빈 칸(= 공격 가능 영역, areaDist == 0)
    ///        중 가장 가까운(스텝 적은) 칸.
    ///   2차: 1차 후보가 하나도 없으면(전부 점유돼 있거나 경로가 막혀 있으면), 도달 가능한 칸 중
    ///        공격 가능 영역까지 남은 거리(areaDist = max(0, dist(target) - bu.range))가 지금
    ///        서 있는 칸보다 실제로 더 작은 칸 — range 1(근접)이든 2 이상(원거리)이든 동일 기준.
    /// 두 기준을 areaDist → steps → CompareCoords(결정적 tie-break, 네트워크 동기화용) 순
    /// 사전식 비교로 통합했다 — areaDist가 작을수록 항상 이긴다.
    ///
    /// 2차보다 나아지는 칸이 하나도 없으면(=지금 위치에서 더 가까워질 방법이 없으면) 원칙적으로
    /// 제자리 대기한다(2026-08, 기획 요청). 예전엔 "지금과 동일한 areaDist인 칸으로 계속
    /// 옆걸음"(3차, 제자리 고정 금지 목적)이 있었는데, 아군이 타겟을 완전히 포위해 어느
    /// 방향으로도 가까워질 수 없을 때 근거리 유닛이 포위망 밖에서 계속 옆걸음치는 원인이었다
    /// (2026-08 실측 영상 확인) — 3차는 완전히 제거했다.
    ///
    /// 물리적 즉시 backtrack 방지: 후보의 "실제로 이번 스텝에 내디딜 첫 칸"(firstStep[current])이
    /// 직전에 있던 칸(_previousCoords[bu])과 같으면 그 후보는 통째로 제외한다. tier(areaDist)나
    /// target에 관계없이 항상 적용되는 조건이라 target이 바뀌어도 우회되지 않는다 — 예전 버전은
    /// "지금 칸과 areaDist가 같은 후보"에만 이 제외를 걸었는데, target이 바뀌면 같은 물리적 칸이
    /// 다른 target 기준으로는 "동률(3차)"이 아니라 "개선(2차)"으로 재분류되면서 제외 대상에서
    /// 빠져나가 즉시 되돌아가는 경우가 있었다(둘 다 target 상대값인 areaDist에 걸려 있었기 때문).
    /// firstStep 비교는 target과 무관한 순수 기하 판정이라 이 우회가 불가능하다.
    /// 이 제외는 BFS 탐색(아래 이웃 확장) 자체에는 전혀 관여하지 않는다 — 직전 칸을 "지나서"
    /// 더 좋은 칸에 도달하는 경로는 그대로 탐색되고, 그 경로의 실제 firstStep이 직전 칸이
    /// 아니라면(대개 그렇다 — 직전 칸은 여기로 오기 직전 위치이므로 그 자체가 막다른 골목이
    /// 아닌 한 그 칸을 거치지 않는 다른 진입 방향이 있다) 정상적으로 선택된다.
    ///
    /// 막다른 자리 fallback: "이번 틱에 실제로 내딛는 첫 걸음 자체가 방금 온 칸"인 후보만 있는
    /// 경우, 무조건 제자리 대기하면 _previousCoords가 실제 이동 성공 시에만 갱신되는 탓에
    /// (MoveTowards) 막힌 지형이 그대로 유지되는 한 같은 이유로 계속 대기해 사실상 영구 정지로
    /// 이어질 수 있다(2026-08 코드리뷰 대응으로 처음 추가된 이유) — 그래서 backtrack 후보군을
    /// hasBest가 끝까지 false일 때만 쓰는 fallback으로 둔다. isImmediateBacktrack이 걸러내는
    /// 건 "물리적으로 직전 칸을 거치는 경로"뿐이라, fallback 후보 자체가 항상 그 직전 칸
    /// 하나만은 아니다 — 그 칸을 거쳐 더 먼 곳까지 이어지는 경로 중 가장 나은(areaDist·steps
    /// 기준) 후보가 fallback으로 뽑힌다(예: 막힌 자리를 빠져나가야만 닿을 수 있는 진짜 개선책).
    ///
    /// 다만 매번 무제한 허용하면, 아군이 타겟을 완전히 포위해 두 칸(A↔B)이 서로의 유일한
    /// 탈출구인 경우 매 틱 A↔B를 계속 왕복하게 된다(2026-08 실측 영상 확인). 이걸 막으려고
    /// 처음엔 "직전 이동이 fallback이었으면 이번엔 금지"라는 1비트 플래그를 썼는데, 유닛이
    /// 대기 상태로 들어가면(MoveTowards 조기 리턴) 그 플래그를 갱신할 기회가 없어 영원히 true로
    /// 고정되는 회귀가 있었다(2026-08 재리뷰 지적 — 보드 상황이 나중에 바뀌어도 그 유닛은 전투가
    /// 끝날 때까지 fallback을 다시 못 씀). 시간/틱 기반 쿨다운도 검토했으나, "일정 시간 지나면
    /// 무조건 재시도"는 아무것도 안 바뀌었어도 왕복이 다시 보일 수 있어 미봉책이라 판단했다.
    ///
    /// 그래서 시간이 아니라 <see cref="_lastFallbackProfile"/>로 "가장 최근에 실제로 쓴 fallback
    /// 후보의 (areaDist, steps)"를 기억해뒀다가, 이번에 새로 계산한 fallback 후보의 (areaDist,
    /// steps)가 그것과 완전히 같을 때만(=상황이 조금도 안 바뀌었을 때만) 재사용을 막는다. 다르면
    /// (주변 점유가 바뀌었거나 타겟이 바뀌어 값이 달라지면) 몇 틱이 지났든 즉시 다시 쓸 수 있다.
    /// A↔B의 완전 대칭 왕복은 매번 정확히 같은 (areaDist, steps)를 만들어내므로 계속 막히고,
    /// 진짜로 상황이 달라진 경우엔 값이 달라지므로 곧바로 다시 허용된다.
    /// </summary>
    private HexCoords FindNextStep(BattleUnit bu, BattleUnit target)
    {
        int startAreaDist = Mathf.Max(0, bu.coords.DistanceTo(target.coords) - bu.range);
        bool hasPrev = _previousCoords.TryGetValue(bu, out HexCoords prevCoords);

        var stepCount = new Dictionary<HexCoords, int>();
        var firstStep = new Dictionary<HexCoords, HexCoords>();
        var queue = new Queue<HexCoords>();
        stepCount[bu.coords] = 0;
        queue.Enqueue(bu.coords);

        bool hasBest = false;
        HexCoords best = default;
        int bestAreaDist = int.MaxValue;
        int bestSteps = int.MaxValue;

        bool hasFallback = false;
        HexCoords fallback = default;
        int fallbackAreaDist = int.MaxValue;
        int fallbackSteps = int.MaxValue;

        while (queue.Count > 0)
        {
            HexCoords current = queue.Dequeue();
            int steps = stepCount[current];

            if (steps > 0)
            {
                int areaDist = Mathf.Max(0, current.DistanceTo(target.coords) - bu.range);

                // firstStep이 직전 칸과 같은 후보(=이번 스텝에 바로 되돌아가는 경로)는 정상
                // 후보에서 항상 제외한다(즉시 왕복 방지).
                bool isImmediateBacktrack = hasPrev && firstStep[current].Equals(prevCoords);

                if (!isImmediateBacktrack && areaDist < startAreaDist)
                {
                    // 정상 후보: 실제로 지금보다 가까워지는 칸만(예전 "3차" — 동일 areaDist
                    // 옆걸음 — 제거됨).
                    if (IsBetterCandidate(hasBest, areaDist, steps, current, bestAreaDist, bestSteps, best))
                    {
                        bestAreaDist = areaDist;
                        bestSteps = steps;
                        best = current;
                        hasBest = true;
                    }
                }
                else if (isImmediateBacktrack && areaDist <= startAreaDist)
                {
                    // 막다른 자리 fallback 후보 — 실제 사용 여부는 루프 밖에서 _lastFallbackProfile와
                    // 비교해 결정한다(여기서는 후보 수집만).
                    if (IsBetterCandidate(hasFallback, areaDist, steps, current, fallbackAreaDist, fallbackSteps, fallback))
                    {
                        fallbackAreaDist = areaDist;
                        fallbackSteps = steps;
                        fallback = current;
                        hasFallback = true;
                    }
                }
            }

            foreach (var neighbor in current.GetNeighbors())
            {
                if (stepCount.ContainsKey(neighbor)) continue;
                if (!_validCoords.Contains(neighbor)) continue;
                if (IsOccupied(neighbor)) continue;

                stepCount[neighbor] = steps + 1;
                firstStep[neighbor] = current.Equals(bu.coords) ? neighbor : firstStep[current];
                queue.Enqueue(neighbor);
            }
        }

        if (hasBest)
        {
            // 정상 진전이 있었으니 이전 fallback 기록은 더 이상 의미 없다 — 다음에 다시 막히면
            // 완전히 새 상황으로 취급한다.
            _lastFallbackProfile.Remove(bu);
            return firstStep[best];
        }

        if (hasFallback)
        {
            bool sameSituationAsLastFallback =
                _lastFallbackProfile.TryGetValue(bu, out var lastProfile) &&
                lastProfile.target == target &&
                lastProfile.areaDist == fallbackAreaDist && lastProfile.steps == fallbackSteps;

            if (!sameSituationAsLastFallback)
            {
                _lastFallbackProfile[bu] = (target, fallbackAreaDist, fallbackSteps);
                return firstStep[fallback];
            }
            // 지난번 fallback과 (타겟, areaDist, steps)가 완전히 같다 = 같은 타겟을 상대로
            // 상황이 조금도 안 바뀌었다 — 재사용하면 그대로 왕복이 재현되므로 대기한다. 기록은
            // 그대로 둔다(다음 틱에도 여전히 안 바뀌었으면 계속 대기, 바뀌면 위 분기로 빠져
            // 다시 허용됨).
        }

        return bu.coords;
    }

    private bool IsOccupied(HexCoords coords)
    {
        foreach (var other in _units)
            if (other.IsAlive && other.coords == coords)
                return true;
        return false;
    }

    /// <summary>self 외에 같은 팀의 살아있는 유닛이 있는지(자뭉열매 복귀 판정 — 혼자 남으면 즉시 복귀).</summary>
    private bool HasOtherAliveAlly(BattleUnit self)
    {
        foreach (var u in _units)
            if (u != self && u.team == self.team && u.IsAlive)
                return true;
        return false;
    }

    private bool HasAliveUnit(BattleTeam team)
    {
        foreach (var bu in _units)
            if (bu.team == team && bu.IsAlive)
                return true;
        return false;
    }

    /// <summary>
    /// 타임아웃 시 남은 총 HP 비율로 승패 결정.
    /// 오버타임 도입(2026-07-30 기획 확정)으로 RunBattle()에서 더 이상 호출하지 않음 —
    /// 롤백 대비 보존.
    /// </summary>
    private bool DetermineWinnerByRemainingHp()
    {
        float allyHp = 0f, enemyHp = 0f;
        foreach (var bu in _units)
        {
            if (bu.team == BattleTeam.Ally) allyHp += Mathf.Max(0f, bu.currentHp);
            else enemyHp += Mathf.Max(0f, bu.currentHp);
        }
        return allyHp >= enemyHp;
    }

    public PokemonUnit GetSourceUnitFromVisual(GameObject hitObject)
    {
        if (hitObject == null)
            return null;

        Transform hitTransform = hitObject.transform;

        foreach (BattleUnit unit in _units)
        {
            if (unit == null ||
                unit.visual == null)
            {
                continue;
            }

            Transform visualTransform =
                unit.visual.transform;

            if (hitTransform == visualTransform ||
                hitTransform.IsChildOf(visualTransform))
            {
                return unit.source;
            }
        }

        return null;
    }

    // ─────────────────────────────────────────
    // 정리
    // ─────────────────────────────────────────

    private void Cleanup()
    {
        foreach (var bu in _units)
        {
            if (bu.visual != null)
                Destroy(bu.visual);
            bu.visual = null;
            bu.facing = null;

            if (bu.shieldVfxInstance != null)
                Destroy(bu.shieldVfxInstance);
            bu.shieldVfxInstance = null;
            bu.shieldSources.Clear();
            bu.skillShieldSource = null;

            if (bu.source != null)
            {
                bu.source.gameObject.SetActive(true);
                bu.source.ResetForBattle();
            }
        }

        _units.Clear();

        // 편입 전에 전투가 끝난 예약분은 그냥 버린다(아직 visual이 없어 정리할 것도 없다).
        _pendingSpawns.Clear();

        foreach (var tile in _mirrorTiles)
            Destroy(tile);

        _mirrorTiles.Clear();
        _enemyTiles.Clear();
        _validCoords.Clear();
        ClearMovementTracking();

        // 이 인스턴스(실전투 또는 미러)가 만든 VFX만 정리한다 — 정적 전역 리스트를 함께 쓰던 예전
        // BattleVfxPlayer.ClearAllActive()와 달리 상대 인스턴스의 VFX는 건드리지 않는다. Cleanup()은
        // RunBattle(실전투 정상 종료)과 RunMirrorBattle/AbortMirrorBattle/FinishMirrorBattle/
        // HandleMirrorBattleException(미러 종료·중단·예외) 전부를 통과하는 단일 지점이라 여기 한 곳만
        // 고치면 모든 종료 경로가 커버된다.
        BattleVfxPlayer.ClearScope(this);
    }

    // ─────────────────────────────────────────
    // 파트너 관전 — 미러 전투
    //
    // 파트너의 BattleSnapshot을 입력으로 실제 전투와 완전히 분리된 전투를 실행한다.
    // 이 메서드들은 전부 "이 BattleManager 인스턴스"의 _units를 쓴다 — 실전투와 동시에
    // 격리 실행하려면 반드시 별도 GameObject의 별도 BattleManager 컴포넌트에서 호출해야 한다
    // (PartnerBattleMirrorController가 그 인스턴스 생성·생명주기를 담당).
    //
    // visual은 SpawnVisual/UpdateVisualPosition을 그대로 재사용해 만든다 — _visualOffset/
    // _visualParent(ConfigureMirrorVisuals가 세팅)만큼 실전투와 다른 위치·부모에 그려진다.
    // 판정 좌표(BattleUnit.coords)는 절대 건드리지 않는다. BattleVfxPlayer(평타/스킬 VFX)도
    // 실전투와 동일하게 호출한다 — BattleVfxPlayer가 scope(이 인스턴스)별로 생성 목록을 분리
    // 관리하고(ClearScope), Play* 호출부가 ResolveVisualLayer()로 LocalGameplayVisual/
    // PartnerSpectateVisual Layer를 찍어주므로 실전투 VFX와 서로 지우거나 화면이 섞이지 않는다.
    //
    // GameEvents.BattleStart/BattleEnd는 발행하지 않는다 — onComplete/onFailed 콜백으로만
    // 결과를 알린다. PlayerHealthManager/RoundPhaseManager/AugmentManager 등 이 두 이벤트를
    // 구독하는 모든 시스템이 자동으로 격리된다(발행 자체를 안 하므로).
    // ─────────────────────────────────────────

    /// <summary>
    /// 이 인스턴스가 지금 미러 전투를 실행 중인지. _mirrorBattleCoroutine의 null 여부가 아니라
    /// 별도 플래그로 판단한다 — Unity 코루틴은 StartCoroutine 호출 시 첫 yield 전까지 동기 실행되므로
    /// (예: 셋업 실패, 또는 전투가 tick 0에 바로 끝나는 경우) 코루틴 내부에서 핸들 변수를 null로
    /// 지워도 그 직후 바깥의 "_mirrorBattleCoroutine = StartCoroutine(...)" 대입이 그 값을
    /// 되돌려 덮어써버리는 순서 문제가 있다. 플래그는 StartCoroutine 호출 "전"에 세우므로 이 문제가 없다.
    /// </summary>
    public bool IsMirrorBattleRunning => _isMirrorBattleRunning;
    private bool _isMirrorBattleRunning;

    /// <summary>현재 _units 중 visual이 만들어져 있는 유닛 수(QA 상태 표시용).</summary>
    public int VisualUnitCount
    {
        get
        {
            int count = 0;
            foreach (var bu in _units)
                if (bu.visual != null) count++;
            return count;
        }
    }

    /// <summary>
    /// 이 인스턴스를 미러 전투 전용으로 설정한다 — 이후 생성되는 모든 visual이 offset만큼
    /// 이동해 표시되고 parent 아래로 모인다(_visualParent가 null이 아니게 됨 → ResolveVisualLayer()가
    /// PartnerSpectateVisual을 반환하게 되는 것도 이 한 줄에서 갈린다).
    /// _playBattleVfx는 건드리지 않는다 — 기본값 true를 실전투와 동일하게 그대로 쓴다. VFX가
    /// 실전투와 서로 지우지 않는 것은 BattleVfxPlayer의 scope 분리(ClearScope)가, 화면이 섞이지
    /// 않는 것은 Layer 분리(ResolveVisualLayer → BattleVfxPlayer.Create의 layer 인자)가 보장한다.
    /// 실전투 인스턴스는 절대 호출하지 않는다(기본값 Vector3.zero/null을 그대로 유지해야
    /// 기존 동작이 보존된다). PartnerBattleMirrorController가 미러 BattleManager를 만든 직후 1회만 호출한다.
    /// </summary>
    public void ConfigureMirrorVisuals(Vector3 offset, Transform parent)
    {
        _visualOffset = offset;
        _visualParent = parent;
    }

    /// <summary>이 인스턴스의 Inspector 연결 적 진영 바닥 프리팹. 미러 인스턴스가 실제 로컬 인스턴스
    /// (GameManager.Battle)에서 이 값을 읽어 ConfigureMirrorEnemyTilePrefab으로 자기 자신에 주입할
    /// 때 쓰는 읽기 전용 노출 — 필드를 그대로 public으로 열지 않는다.</summary>
    public HexTile EnemyTilePrefab => _enemyTilePrefab;

    /// <summary>
    /// 미러 인스턴스 전용 — 실제 로컬 BattleManager의 _enemyTilePrefab을 그대로 물려받는다.
    /// 런타임 AddComponent로 생성되는 미러 인스턴스는 어떤 인스턴스의 Inspector 값도 물려받지 않아
    /// _enemyTilePrefab이 항상 null로 시작하는데, null 상태로 CreateEnemyTile()을 호출하면
    /// CreateFallbackEnemyTile()(연빨강 원기둥 폴백)로 빠져 실제 로컬 적 진영 바닥(HexTile_Enemy)과
    /// 다르게 보인다(관전 화면에서 확인된 문제). null을 넘기면 아무 것도 하지 않는다 — 호출측이
    /// 아직 실제 인스턴스를 못 구했을 때 이미 주입된 값을 실수로 지우지 않기 위함.
    /// 실전투 인스턴스는 절대 호출하지 않는다(ConfigureMirrorVisuals와 같은 원칙).
    /// </summary>
    public void ConfigureMirrorEnemyTilePrefab(HexTile enemyTilePrefab)
    {
        if (enemyTilePrefab == null) return;
        _enemyTilePrefab = enemyTilePrefab;
    }

    /// <summary>
    /// 파트너 BattleSnapshot으로 미러 전투를 시작한다. 이미 실행 중이거나 snapshot이 없거나
    /// 유닛/스테이지 셋업에 실패하면 onFailed만 호출하고 아무것도 진행하지 않는다.
    /// 셋업(유닛 복원·시너지 적용)은 여기서 동기적으로 끝내고, 실제 틱 루프만 코루틴으로 돌린다.
    /// </summary>
    public void RunMirrorBattle(
        BattleSnapshot snapshot,
        System.Action<MirrorBattleResult> onComplete,
        System.Action<string> onFailed)
    {
        if (_isMirrorBattleRunning)
        {
            onFailed?.Invoke("이미 미러 전투가 실행 중입니다");
            return;
        }
        if (snapshot == null)
        {
            onFailed?.Invoke("BattleSnapshot이 null입니다");
            return;
        }

        if (!SetupMirrorUnits(snapshot, out string failReason))
        {
            Cleanup(); // 실패 시점까지 만들어진 부분 유닛(대부분 visual 없음)까지 한 번에 정리.
            onFailed?.Invoke(failReason);
            return;
        }

        ApplySnapshotSynergyBuffs(snapshot.activeSynergies);
        ApplySnapshotSynergySpecials(snapshot);

        _isMirrorBattleRunning = true;
        _mirrorBattleCoroutine = StartCoroutine(RunMirrorBattleTickLoop(onComplete, onFailed));
    }

    /// <summary>실행 중인 미러 전투를 즉시 중단한다(결과 콜백 없음). 파트너 이탈 등 "외부"에서 호출하는
    /// 전용 경로 — 코루틴이 아직 살아있다는 전제로 StopCoroutine을 쏜다. 코루틴 "자기 자신"의 내부
    /// (RunMirrorBattleTickLoop의 예외 처리 등)에서는 재사용하지 말 것: 실행 중인 코루틴이 자기 자신에
    /// StopCoroutine을 거는 건 불필요하고, 이후 yield break로 어차피 빠져나가므로 그쪽은
    /// HandleMirrorBattleException이 상태 정리를 직접 담당한다.
    /// Cleanup()이 남아있는 미러 visual을 전부 파괴한다(bu.source는 전부 null이라 실제 PokemonUnit엔 영향 없음).</summary>
    public void AbortMirrorBattle()
    {
        if (!_isMirrorBattleRunning) return;

        if (_mirrorBattleCoroutine != null) StopCoroutine(_mirrorBattleCoroutine);
        _mirrorBattleCoroutine = null;
        _isMirrorBattleRunning = false;
        Cleanup();
    }

    /// <summary>
    /// 실전투 SimulateBattleLoop/RunOvertime(GameEvents.OvertimeStarted 발행 등)은 재사용하지 않고,
    /// SimulateTick/HasAliveUnit(둘 다 기존 private 메서드 그대로)만 써서 자체 루프를 돈다 —
    /// 미러는 화면에 안 보이므로 오버타임의 "실시간 유지" 연출이 필요 없다(고정 틱까지 즉시 판정).
    /// 유닛/시너지 셋업은 RunMirrorBattle에서 이미 동기적으로 끝낸 상태로 진입한다.
    ///
    /// WaitForSecondsRealtime을 쓴다 — 실전투(RunBattle/SimulateBattleLoop/RunOvertime)의 WaitForSeconds는
    /// 그대로 두고, 미러 루프만 향후 게임 내부 Time.timeScale 변화로부터 독립시키기 위함(현재 원인으로
    /// 단정된 것은 아님 — 격리 목적의 선제 조치).
    /// </summary>
    private IEnumerator RunMirrorBattleTickLoop(
        System.Action<MirrorBattleResult> onComplete,
        System.Action<string> onFailed)
    {
        int tick = 0;
        while (tick < MAX_TICKS)
        {
            // SimulateTick()에서 처리되지 않은 예외가 나면 코루틴이 조용히 죽어 IsRunning이 true로
            // 고착되고 visual도 고아 상태로 남는다(FinishMirrorBattle/AbortMirrorBattle 어느 쪽도 못 탐) —
            // yield break는 catch 안에서 쓸 수 없으므로(C# 제약) 예외만 잡아두고 실제 종료 처리·yield break는
            // try/catch 바깥에서 한다.
            System.Exception tickException = null;
            try
            {
                SimulateTick();
            }
            catch (System.Exception ex)
            {
                tickException = ex;
            }

            if (tickException != null)
            {
                HandleMirrorBattleException(tickException, tick, onFailed);
                yield break;
            }

            bool allyAlive = HasAliveUnit(BattleTeam.Ally);
            bool enemyAlive = HasAliveUnit(BattleTeam.Enemy);

            if (!allyAlive || !enemyAlive)
            {
                FinishMirrorBattle(allyAlive ? BattleEndReason.Victory : BattleEndReason.Defeat, tick, onComplete);
                yield break;
            }

            tick++;
            yield return new WaitForSecondsRealtime(TICK_INTERVAL);
        }

        // 타임아웃 — 실전투 오버타임의 최종 판정 규칙과 동일(적 하나라도 살아있으면 패배)만 적용,
        // 실시간 대기 구간(RunOvertime)은 생략한다.
        bool decisionWin = !HasAliveUnit(BattleTeam.Enemy);
        FinishMirrorBattle(decisionWin ? BattleEndReason.DecisionVictory : BattleEndReason.DecisionDefeat, tick, onComplete);
    }

    /// <summary>
    /// RunMirrorBattleTickLoop의 SimulateTick()에서 처리되지 않은 예외가 발생했을 때만 호출된다.
    /// Victory/Defeat 등 정상 판정으로 위장하지 않고(onComplete 절대 호출 안 함) onFailed로만 알린다.
    /// 진단에 필요한 상태(틱/생존 수/GamePhase/Time.timeScale/예외 전체)를 로그로 남기고, 정상 종료
    /// (FinishMirrorBattle)·파트너 이탈 중단(AbortMirrorBattle)과 동일하게 _mirrorBattleCoroutine=null,
    /// _isMirrorBattleRunning=false, Cleanup()을 빠짐없이 실행한다.
    /// </summary>
    private void HandleMirrorBattleException(System.Exception ex, int tick, System.Action<string> onFailed)
    {
        int allySurvivors = 0, enemySurvivors = 0;
        foreach (var bu in _units)
        {
            if (!bu.IsAlive) continue;
            if (bu.team == BattleTeam.Ally) allySurvivors++;
            else enemySurvivors++;
        }

        string phaseLabel = GameManager.TryGet(out var gm) && gm.Phase != null
            ? gm.Phase.CurrentPhase.ToString()
            : "알 수 없음(GameManager/Phase 없음)";

        Debug.LogError(
            $"[MirrorBattle] SimulateTick 예외로 중단 | Tick={tick} | AllySurvivors={allySurvivors} | " +
            $"EnemySurvivors={enemySurvivors} | GamePhase={phaseLabel} | Time.timeScale={Time.timeScale} | " +
            $"예외: {ex}");

        _mirrorBattleCoroutine = null;
        _isMirrorBattleRunning = false;
        Cleanup();

        onFailed?.Invoke($"미러 전투 내부 오류(tick {tick}): {ex.GetType().Name}: {ex.Message}");
    }

    /// <summary>
    /// 미러 전투 결과 집계(아군 생존 수/잔여 HP 합 — 스냅샷 원본이 파트너 진영이므로 "아군"이 곧 파트너 팀).
    /// 집계를 마친 뒤 Cleanup()으로 미러 visual을 전부 파괴하고 _units를 비운다.
    /// </summary>
    private void FinishMirrorBattle(BattleEndReason outcome, int ticks, System.Action<MirrorBattleResult> onComplete)
    {
        int survivors = 0;
        float hpSum = 0f;
        foreach (var bu in _units)
        {
            if (bu.team != BattleTeam.Ally || !bu.IsAlive) continue;
            survivors++;
            hpSum += Mathf.Max(0f, bu.currentHp);
        }

        Cleanup();
        _mirrorBattleCoroutine = null;
        _isMirrorBattleRunning = false;

        onComplete?.Invoke(new MirrorBattleResult(outcome, ticks, survivors, hpSum));
    }

    /// <summary>
    /// 스냅샷 아군 유닛 + 파트너 스테이지 적을 _units에 채운다. 실패하면 _units를 건드린 채로
    /// false를 반환하고(호출측이 Clear), 원인을 failReason에 남긴다 — 추측/기본값 진행 없음.
    /// </summary>
    private bool SetupMirrorUnits(BattleSnapshot snapshot, out string failReason)
    {
        failReason = null;
        _units.Clear();
        ClearMovementTracking();
        _pendingSpawns.Clear(); // 이전 전투가 비정상 종료됐을 때의 잔여분 방어

        foreach (BattleSnapshot.UnitEntry entry in snapshot.units)
        {
            BattleUnit bu = CreateMirrorBattleUnit(entry, out string unitFail);
            if (bu == null)
            {
                failReason = unitFail;
                return false;
            }
            _units.Add(bu);
        }

        StageData stage = FindStageById(snapshot.stageId);
        if (stage == null)
        {
            failReason = $"StageData 조회 실패: {snapshot.stageId}";
            return false;
        }

        var board = GameManager.Instance.Board;
        if (board == null)
        {
            failReason = "BoardManager 연결 안 됨(좌표 변환 불가)";
            return false;
        }
        BuildValidCoords(board);

        // SpawnMirrorEnemies(폴백)가 아니라 SpawnEnemiesFromStage(정상 경로)를 그대로 재사용한다 —
        // GetEnemyBattleCoords는 순수 좌표 변환(보드 모양)이라 로컬 보드를 넘겨도 결과가 같다.
        int enemyCount = SpawnEnemiesFromStage(stage, board);
        if (enemyCount == 0)
        {
            failReason = $"'{stage.stageId}' 적 구성 생성 실패(DUMMY/풀 누락)";
            return false;
        }

        ApplyOnCombatStartEffects();

        // 실전투 SetupUnits()의 마지막 단계(SetupVisuals)와 같은 순서 — 아이템 효과 적용까지
        // 끝난 뒤에 visual을 만든다. SpawnMirrorBoard(적 보드 바닥 타일)는 이번 단계 범위 밖이라
        // 호출하지 않는다(유닛 모델만 표시 대상).
        foreach (var bu in _units)
            SpawnVisual(bu);

        return true;
    }

    /// <summary>
    /// BattleSnapshot.UnitEntry 하나를 미러 BattleUnit으로 복원한다. ID 조회가 하나라도 실패하면
    /// null + failReason을 반환한다(추측/기본값 금지). 스탯 계산(MaxHp/Attack/...)은 실제 전투
    /// 계산식(PokemonUnit.MaxHp 등)을 복제하지 않기 위해, 씬/보드에 등록되지 않는 임시(비활성)
    /// PokemonUnit 하나를 만들어 프로퍼티를 읽고 즉시 파괴한다 — 이 인스턴스는 _units/Cleanup()
    /// 어디에도 남지 않는다(source는 계속 null).
    /// </summary>
    private BattleUnit CreateMirrorBattleUnit(BattleSnapshot.UnitEntry entry, out string failReason)
    {
        failReason = null;

        PokemonDatabase pokemonDb = PokemonDatabase.Instance;
        PokemonData species = pokemonDb != null ? pokemonDb.GetById(entry.speciesId) : null;
        if (species == null)
        {
            failReason = $"speciesId {entry.speciesId} 조회 실패(PokemonDatabase)";
            return null;
        }

        EvolutionStoneData stone = null;
        if (entry.equippedStoneId != 0)
        {
            EvolutionStoneDatabase stoneDb = EvolutionStoneDatabase.Instance;
            stone = stoneDb != null ? stoneDb.GetById(entry.equippedStoneId) : null;
            if (stone == null)
            {
                failReason = $"equippedStoneId {entry.equippedStoneId} 조회 실패(EvolutionStoneDatabase)";
                return null;
            }
        }

        // previousSpeciesId는 전투 계산에 쓰이지 않지만(현재 data가 이미 진화 반영값), 스냅샷 필드
        // 전부를 조회 가능해야 한다는 원칙에 따라 유효성만 확인한다.
        if (entry.previousSpeciesId != 0 &&
            (pokemonDb == null || pokemonDb.GetById(entry.previousSpeciesId) == null))
        {
            failReason = $"previousSpeciesId {entry.previousSpeciesId} 조회 실패(PokemonDatabase)";
            return null;
        }

        ItemData item0 = null, item1 = null;
        ItemDatabase itemDb = ItemDatabase.Instance;
        if (entry.itemId0 != 0)
        {
            item0 = itemDb != null ? itemDb.GetById(entry.itemId0) : null;
            if (item0 == null) { failReason = $"itemId0 {entry.itemId0} 조회 실패(ItemDatabase)"; return null; }
        }
        if (entry.itemId1 != 0)
        {
            item1 = itemDb != null ? itemDb.GetById(entry.itemId1) : null;
            if (item1 == null) { failReason = $"itemId1 {entry.itemId1} 조회 실패(ItemDatabase)"; return null; }
        }

        // 스킬 해석: 종 기본 스킬과 skillId가 일치하면 그대로 사용. 다르면 "알려진 주입 스킬"인지만
        // 확인한다(현재 코드에서 실제로 동적 생성되는 주입 스킬은 파치리스 날따름 하나뿐 —
        // HeroPachirisuAugment.CreateTauntSkill(), skillId="PACHIRISU_TAUNT" 고정). 그 외는 추측하지
        // 않고 실패 처리한다.
        PokemonSkillData skill = null;
        if (!string.IsNullOrEmpty(entry.skillId))
        {
            if (species.skill != null && species.skill.HasSkill && species.skill.skillId == entry.skillId)
                skill = species.skill;
            else if (entry.skillId == "PACHIRISU_TAUNT")
                skill = HeroPachirisuAugment.CreateTauntSkill();
            else
            {
                failReason = $"skillId '{entry.skillId}'을(를) 종 기본 스킬/알려진 주입 스킬로 해석하지 못함";
                return null;
            }
        }

        // MirrorBattle 장비 스탯은 이후 ItemStatEffect에서 적용하므로 phantom에는 items를 넣지 않는다.
        var phantom = PokemonUnit.CreatePhantom("MirrorStatPhantom", new PhantomUnitConfig
        {
            species = species,
            starLevel = Mathf.Clamp(entry.starLevel, 1, 3),
            heroStatMultiplier = entry.heroStatMultiplier,
            isTradeEvolved = entry.isTradeEvolved,
            equippedStone = stone,
            roleOverride = entry.roleOverride,
            attackRangeOverride = entry.attackRangeOverride,
        });

        float maxHp = phantom.MaxHp;
        float attack = phantom.Attack;
        float spellPower = phantom.SpellPower;
        float defense = phantom.Defense;
        float attackSpeed = phantom.AttackSpeed;
        int range = phantom.Range;
        string role = phantom.Role;

        Destroy(phantom.gameObject);

        var bu = new BattleUnit
        {
            source = null,
            data = species,
            team = BattleTeam.Ally,
            coords = new HexCoords(entry.q, entry.r),
            maxHp = maxHp,
            currentHp = maxHp,
            attack = attack,
            defense = defense,
            spellPower = spellPower,
            attackSpeed = attackSpeed,
            range = Mathf.Max(1, range),
            attackCooldown = 0f,
            moveCooldown = 0f,
            role = role,
            starLevel = Mathf.Clamp(entry.starLevel, 1, 3),
            hasSitrusBerry = entry.hasHeroBerry
        };

        ApplySkill(bu, skill, entry.skillManaCost);
        bu.attackVfxId = !string.IsNullOrEmpty(entry.attackVfxIdOverride) ? entry.attackVfxIdOverride : species.attackVfxId;

        // ItemStatEffect는 아이템별로 하나씩 만들지 않고 마지막에 전체 목록으로 1개만 부착한다 —
        // 순서 무관 합산 계산(ItemStatFormula.ApplyAll, CreateBattleUnit과 동일 패턴)을 위해서다.
        var mirrorItems = new List<ItemData>(2);
        if (item0 != null) mirrorItems.Add(item0);
        if (item1 != null) mirrorItems.Add(item1);
        foreach (var mi in mirrorItems)
            AttachMirrorItemConditionalEffect(bu, mi);
        if (mirrorItems.Count > 0)
            bu.effects.Add(new ItemStatEffect(mirrorItems));

        bu.displayStone = stone;

        return bu;
    }

    /// <summary>CreateBattleUnit의 아이템 조건부 효과 부착부와 같은 내용(둘 다 3줄 수준이라 공용
    /// private 메서드로만 뽑고, CreateBattleUnit 본문은 회귀 위험 때문에 건드리지 않는다).
    /// ItemStatEffect(순서 무관 합산 계산)는 호출측(CreateMirrorBattleUnit)이 전체 목록을 모아
    /// 별도로 1개만 부착한다 — 이 메서드는 건드리지 않는다.</summary>
    private void AttachMirrorItemConditionalEffect(BattleUnit bu, ItemData item)
    {
        bu.effects.Add(new ItemConditionalEffect(item, this));
        bu.displayItems.Add(item);
        if (item.ccImmune) bu.HasCcImmuneItem = true;
    }

    /// <summary>
    /// ApplySynergyBuffs의 스냅샷 버전. bu.source.data.synergies 대신 bu.data.synergies를 쓴다
    /// (source 없는 미러 유닛도 data는 항상 채워져 있음). 수치 적용은 기존 ApplySynergyBuff를 그대로 재사용.
    /// </summary>
    private void ApplySnapshotSynergyBuffs(List<BattleSnapshot.SynergyEntry> activeSynergies)
    {
        if (activeSynergies == null) return;

        int appliedCount = 0;
        foreach (BattleSnapshot.SynergyEntry entry in activeSynergies)
        {
            SynergyData data = FindSynergyById(entry.synergyId);
            if (data == null) continue;

            string synergyId = data.synergyNameEn;
            int tier = entry.tier + 1; // 1-base(ApplySynergyBuffs와 동일 규칙)

            foreach (var bu in _units)
            {
                if (bu.team != BattleTeam.Ally || bu.data == null || bu.data.synergies == null) continue;
                if (!bu.data.synergies.Contains(data.synergyName) && !bu.data.synergies.Contains(data.synergyNameEn)) continue;

                if (ApplySynergyBuff(bu, synergyId, tier)) appliedCount++;
            }
        }

        if (appliedCount > 0)
            Debug.Log($"[MirrorBattle] 시너지 버프 {appliedCount}건 적용");
    }

    /// <summary>ApplySynergySpecials의 스냅샷 버전 — GameManager.Instance.Synergy/CheerleaderChoice.Current를 읽지 않는다.</summary>
    private void ApplySnapshotSynergySpecials(BattleSnapshot snapshot)
    {
        if (SnapshotActiveTier(snapshot, "Ice").HasValue)
            foreach (var bu in _units)
                if (bu.team == BattleTeam.Enemy)
                    bu.attackSpeed *= 1f - SynergyConstants.IceEnemyAtkSpeedReduction;

        if (SnapshotActiveTier(snapshot, "Cheerleader").HasValue)
            ApplyCheerleaderChoice(snapshot.cheerleaderChoice);

        // 실전투와 같다 — 즉시 소환이 아니라 소환 주체만 지정하고, 실제 소환은 그 이브이가
        // 스킬을 시전할 때마다 1종씩 일어난다(CastSkill은 실전투/미러가 공용).
        if (!MarkMirrorHeroEeveeSummoner(snapshot))
        {
            int? mutantTier = SnapshotActiveTier(snapshot, "Mutant");
            if (mutantTier.HasValue)
                SpawnMirrorMutantBots(mutantTier.Value + 1);
        }

        SynergyData dark = SnapshotActiveTier(snapshot, "Dark").HasValue ? FindSynergyByNameEn("Dark") : null;
        if (dark != null)
            MarkMirrorDarkFirstSkillStun(dark);
    }

    /// <summary>MarkDarkFirstSkillStun의 스냅샷 버전 — bu.data.synergies 기준(미러 아군은 source가 null이라 종 데이터로 판정).</summary>
    private void MarkMirrorDarkFirstSkillStun(SynergyData dark)
    {
        foreach (var bu in _units)
        {
            if (bu.team != BattleTeam.Ally || bu.data == null || bu.data.synergies == null) continue;
            if (!bu.data.synergies.Contains(dark.synergyName) && !bu.data.synergies.Contains(dark.synergyNameEn)) continue;
            bu.darkFirstSkillPending = true;
        }
    }

    /// <summary>
    /// MarkHeroEeveeSummoner의 스냅샷 버전 — 판정 기준은 실전투와 같다
    /// (evolutionLocked = 고정 효과라 여러 마리, heroStatMultiplier &gt; 1 = 이동 효과라 딱 한 마리).
    ///
    /// 미러 전투의 아군 BattleUnit은 source가 null이라 PokemonUnit을 볼 수 없어, 스냅샷 엔트리와
    /// 좌표(q,r)로 짝을 맞춘다 — SetupMirrorUnits가 coords를 엔트리 값 그대로 넣으므로 1:1이다.
    /// </summary>
    private bool MarkMirrorHeroEeveeSummoner(BattleSnapshot snapshot)
    {
        PokemonDatabase db = PokemonDatabase.Instance;
        if (db == null) return false;

        foreach (BattleSnapshot.UnitEntry entry in snapshot.units)
        {
            if (!entry.evolutionLocked || entry.heroStatMultiplier <= 1f) continue;
            if (entry.starLevel < HERO_EEVEE_MIN_STAR) continue;

            PokemonData species = db.GetById(entry.speciesId);
            if (species == null ||
                !string.Equals(species.pokemonNameEn, "Eevee", System.StringComparison.OrdinalIgnoreCase))
                continue;

            var coords = new HexCoords(entry.q, entry.r);
            foreach (var bu in _units)
            {
                if (bu.team != BattleTeam.Ally || !bu.coords.Equals(coords)) continue;

                MarkAsHeroEeveeSummoner(bu);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 소환 카운터를 켜고 스킬 마나코스트를 <b>성급별</b> 나인이볼부스트 값으로 갈아끼운다
    /// (실전투/미러 공용). 종 원본(Celebrate 60)은 쓰지 않는다 — 1성·2성은 8종에 못 닿고 3성만
    /// 닿게 하는 계단이 목적이라, 근거와 수치는 <see cref="HeroEeveeBoostTable.SkillManaCost"/> 주석 참고.
    /// </summary>
    private static void MarkAsHeroEeveeSummoner(BattleUnit bu)
    {
        bu.heroEeveeSummonIndex = 0;

        // 8종을 다 부르고 나면 원래 스킬로 되돌아가므로, 그때 쓸 원본 코스트를 보관해 둔다.
        bu.heroEeveeBaseMaxMana = bu.maxMana;
        bu.maxMana = HeroEeveeBoostTable.SkillManaCost(bu.starLevel);

        // 이미 찬 마나가 새 상한을 넘으면 첫 시전이 즉발이 된다 — 전투 시작 직후라 0이지만
        // 호출 순서가 바뀌어도 안전하도록 가둬둔다.
        bu.currentMana = Mathf.Min(bu.currentMana, bu.maxMana);
    }

    /// <summary>
    /// SpawnMutantBots의 스냅샷 버전. 빈 타일은 "로컬 보드의 전체 좌표(모양은 어느 클라이언트나 동일)
    /// − 스냅샷 점유 좌표"로 구한다(파트너의 실제 보드 객체는 이 클라이언트에 없음). CreateBotUnit은
    /// 기존 private 메서드를 그대로 재사용한다.
    /// </summary>
    private void SpawnMirrorMutantBots(int tier)
    {
        PokemonDatabase db = PokemonDatabase.Instance;
        BoardManager board = GameManager.Instance.Board;
        if (db == null || board == null) return;

        var occupied = new HashSet<HexCoords>();
        foreach (var bu in _units)
            if (bu.team == BattleTeam.Ally)
                occupied.Add(bu.coords);

        var empty = new List<HexCoords>();
        foreach (var coords in board.GetBoardSnapshot().Keys)
            if (!occupied.Contains(coords)) empty.Add(coords);
        empty.Sort(CompareCoords);

        int count = Mathf.Min(tier, MutantBots.Length);
        int placed = 0;
        for (int i = 0; i < count; i++)
        {
            if (placed >= empty.Count)
            {
                Debug.LogWarning("[MirrorBattle] 돌연변이 봇 배치할 빈 타일 부족 — 일부 미소환");
                break;
            }

            PokemonData data = db.GetByNameEn(MutantBots[i]);
            if (data == null)
            {
                Debug.LogWarning($"[MirrorBattle] 돌연변이 봇 '{MutantBots[i]}' DB에 없음 — 스킵");
                continue;
            }

            BattleUnit bot = CreateBotUnit(data, empty[placed++]);
            _units.Add(bot);
            SpawnVisual(bot); // 실전투 SpawnMutantBots와 동일 — 이후 소환이라 SetupMirrorUnits의 일괄 스폰을 못 타서 개별 호출.
        }
    }

    /// <summary>synergyNameEn으로 스냅샷 activeSynergies에서 활성 티어(0-base)를 찾는다. 없으면 null.</summary>
    private static int? SnapshotActiveTier(BattleSnapshot snapshot, string synergyNameEn)
    {
        SynergyData target = FindSynergyByNameEn(synergyNameEn);
        if (target == null) return null;

        foreach (BattleSnapshot.SynergyEntry entry in snapshot.activeSynergies)
            if (entry.synergyId == target.id)
                return entry.tier;
        return null;
    }

    /// <summary>SynergyDatabase에 GetById가 없어 여기서 직접 선형 탐색한다(DB 규모가 작아 문제 없음).</summary>
    private static SynergyData FindSynergyById(int synergyId)
    {
        SynergyDatabase db = SynergyDatabase.Instance;
        if (db == null || db.all == null) return null;

        foreach (SynergyData s in db.all)
            if (s != null && s.id == synergyId) return s;
        return null;
    }

    private static SynergyData FindSynergyByNameEn(string synergyNameEn)
    {
        SynergyDatabase db = SynergyDatabase.Instance;
        if (db == null || db.all == null) return null;

        foreach (SynergyData s in db.all)
            if (s != null && string.Equals(s.synergyNameEn, synergyNameEn, System.StringComparison.OrdinalIgnoreCase))
                return s;
        return null;
    }

    /// <summary>StageDatabase에 stageId 조회 API가 없어 여기서 직접 선형 탐색한다.</summary>
    private static StageData FindStageById(string stageId)
    {
        if (string.IsNullOrEmpty(stageId)) return null;

        StageDatabase db = StageDatabase.Instance;
        if (db == null || db.stages == null) return null;

        foreach (StageData s in db.stages)
            if (s != null && s.stageId == stageId) return s;
        return null;
    }
}
