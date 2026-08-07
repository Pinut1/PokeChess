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
    private const int MAX_TICKS = 300; // 30초 타임아웃

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

    // 지원 스킬 위력 — 기획 확정(2026-07-10): spellPower × 0.5 (임시 계수, 밸런스 후 조정).
    // AsBuff = (spellPower×계수)%p 공속 증가, ManaRegen = 즉시 마나 (spellPower×계수) 회복으로 해석.
    // (※ "증가량/회복량" 해석은 해인님 재확인 예정)
    private const float SUPPORT_SPELLPOWER_COEF = 0.5f;
    private const float AS_BUFF_DURATION = 3f;  // 지속시간은 언급 없음 — PLACEHOLDER 유지

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
    [SerializeField] private float _overtimeDuration = 5f;

    [Tooltip("오버타임 동안 tick을 더 빠르게 소비하는 배속. 1 이하면 기본 전투와 같은 속도로 진행한다. " +
             "기획 미확정 수치(2026-07-30 기준).")]
    [SerializeField] private float _overtimeSpeedMultiplier = 2f;

    private readonly List<BattleUnit> _units = new();
    private readonly List<GameObject> _mirrorTiles = new();

    // 적 진영 바닥 타일(좌표 → 타일). 적이 서 있는 칸만 짙게 칠하려고 좌표로 되찾을 수 있게 들고 있는다.
    // 준비 단계 프리뷰와 전투 중 미러 보드가 번갈아 쓰며, 각자 걷어낼 때(ClearEnemyPreview/Cleanup) 같이 비운다.
    // 원기둥 폴백 타일은 HexTile이 아니라 여기 담기지 않는다 — 색 구분도 되지 않는다.
    private readonly Dictionary<HexCoords, HexTile> _enemyTiles = new();
    private Coroutine _battleCoroutine;

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

    /// <summary>파트너 이탈 대기 중 전투 일시정지. Time.timeScale을 쓰지 않고 틱 루프 진입 자체를 막는다.</summary>
    private bool _isPaused;

    // 준비 단계에 미리 보여주는 적 진영(바닥 + 적 모델). 시각 전용이라 BattleUnit을 만들지 않는다 —
    // 스탯 스냅샷은 전투 시작 시점 보드/아이템 기준이어야 하므로 미리 만들어 두면 안 된다.
    private readonly List<GameObject> _previewObjects = new();

    // 준비 단계 적 프리뷰의 스탯 사본. 전투 목록(_units)에는 넣지 않으므로 시뮬레이션에 관여하지 않는다.
    // 스탯창이 준비 단계에도 적 정보를 보여줄 수 있게 두는 읽기 전용 스냅샷이다.
    private readonly List<BattleUnit> _previewEnemies = new();

    /// <summary>준비 단계에 세워둔 적 프리뷰. 전투 중에는 비어 있다(그때는 Units를 쓸 것).</summary>
    public IReadOnlyList<BattleUnit> PreviewEnemies => _previewEnemies;

    /// <summary>전투 중인 유닛 스냅샷(읽기 전용). BalanceCheckHarness가 실측 스탯을 확인하는 용도.</summary>
    public IReadOnlyList<BattleUnit> Units => _units;

    private void OnEnable()
    {
        GameEvents.OnBattleStart += HandleBattleStart;
        GameEvents.OnStageEntered += HandleStageEntered;
        GameEvents.OnOpponentDisconnected += HandlePartnerDisconnected;
        GameEvents.OnOpponentReconnected  += HandlePartnerReconnected;
    }

    private void OnDisable()
    {
        GameEvents.OnBattleStart -= HandleBattleStart;
        GameEvents.OnStageEntered -= HandleStageEntered;
        GameEvents.OnOpponentDisconnected -= HandlePartnerDisconnected;
        GameEvents.OnOpponentReconnected  -= HandlePartnerReconnected;

        // 컴포넌트가 꺼지면 프리뷰를 지울 주체가 사라지므로 씬에 남지 않게 정리한다.
        ClearEnemyPreview();
    }

    /// <summary>파트너 이탈 감지 — 진행 중인 전투 틱을 멈춘다(신규 공격/이동/스킬 없음).</summary>
    private void HandlePartnerDisconnected(float graceSeconds) => _isPaused = true;

    /// <summary>파트너 재접속 — 멈췄던 지점부터 전투 틱을 재개한다.</summary>
    private void HandlePartnerReconnected() => _isPaused = false;

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
            // 파트너 이탈 대기 중에는 새 틱을 진행하지 않고 재접속까지 그대로 멈춰 있는다.
            while (_isPaused) yield return null;

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

        for (int realTick = 0; realTick < realTicks; realTick++)
        {
            for (int i = 0; i < simTicksPerWait; i++)
            {
                // 파트너 이탈 대기 중에는 오버타임 틱도 진행하지 않는다.
                while (_isPaused) yield return null;

                SimulateTick();

                bool allyAlive  = HasAliveUnit(BattleTeam.Ally);
                bool enemyAlive = HasAliveUnit(BattleTeam.Enemy);

                if (!allyAlive || !enemyAlive)
                {
                    result.overtimeAllyWon = allyAlive; // 둘 다 전멸하면 false(패배 처리) — 기존 동시 전멸 규칙 유지
                    yield break;
                }
            }

            yield return new WaitForSeconds(TICK_INTERVAL);
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

        // 돌연변이 봇 소환. 이브이 영웅증강(진화잠금 이브이 3성)이 보드에 있으면 봇 전원(4마리) 즉시 소환 —
        // 이 경우 이브이 단독이라 일반 돌연변이 시너지 카운트로는 티어가 안 오르므로 전용 경로로 처리.
        // 없으면 일반 돌연변이 시너지 활성 티어 수만큼 소환.
        if (HasHeroEeveeThreeStar())
            SpawnMutantBots(MutantBots.Length);
        else
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
    /// 이브이 영웅증강(진화잠금) 이브이가 3성으로 아군 보드에 있는지. 봇 전원소환 트리거.
    /// evolutionLocked는 이브이 영웅증강만 세우는 플래그라 종·성만 추가 확인하면 충분.
    /// </summary>
    private bool HasHeroEeveeThreeStar()
    {
        foreach (var bu in _units)
        {
            var src = bu.source;
            if (bu.team != BattleTeam.Ally || src == null || src.data == null) continue;
            if (src.evolutionLocked && src.starLevel >= 3 &&
                string.Equals(src.data.pokemonNameEn, "Eevee", System.StringComparison.OrdinalIgnoreCase))
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
            range = Mathf.Max(1, data.range),
            attackCooldown = 0f,
            role = data.role ?? ""
        };
        ApplySkill(bu, data.skill, data.manaCost);
        return bu;
    }

    // ─────────────────────────────────────────
    // 셋업
    // ─────────────────────────────────────────

    private void SetupUnits()
    {
        _units.Clear();

        var board = GameManager.Instance.Board;

        SetupAllyUnits(board);
        SetupEnemyUnits(board);
        ApplyOnCombatStartEffects();
        SetupVisuals(board);
    }

    /// <summary>
    /// 전투 결정론 정렬 기준 — q 오름차순, 같으면 r 오름차순. 전투 셋업 시점(SetupUnits 안)에만
    /// 1회 쓰인다. BoardManager.GetBoardSnapshot()이 반환하는 Dictionary는 순회 순서를 공식
    /// 보장하지 않아(현재는 삽입 이력에 우연히 의존), 두 클라이언트가 항상 같은 순서로
    /// BattleUnit을 만들도록 소비 지점(BattleManager)에서만 명시 정렬한다 — 공용 API인
    /// BoardManager.GetBoardSnapshot() 자체의 반환 순서는 바꾸지 않는다(다른 소비처 영향 회피).
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

    /// <summary>[기둥B] 전투 시작 1회 — 장착템 등 효과의 OnCombatStart로 스탯 가산(평타스탯형만, 조건부 효과는 후속).</summary>
    private void ApplyOnCombatStartEffects()
    {
        foreach (var bu in _units)
            foreach (var effect in bu.effects)
                effect.OnCombatStart(bu);
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
            range = Mathf.Max(1, data.range),
            attackCooldown = 0f,
            role = data.role ?? "",
            starLevel = Mathf.Clamp(e.starLevel, 1, 3)
        };
        ApplySkill(bu, data.skill, data.manaCost);
        bu.attackVfxId = data.attackVfxId; // 평타 VFX(아군과 동일 규칙)

        // 트레이너 보유 아이템 → 아군과 동일한 효과 훅을 적 BattleUnit에도 부착.
        // (아군은 unit.items 리스트, 적은 시트 itemSet1/itemSet2를 ItemDatabase로 해석)
        foreach (var itemNameEn in e.HeldItemsEn)
        {
            var itemDb = ItemDatabase.Instance;
            ItemData item = itemDb != null ? itemDb.GetByNameEn(itemNameEn) : null;
            if (item == null)
            {
                Debug.LogWarning($"[Battle] 적 보유 아이템 '{itemNameEn}'을 ItemDatabase에서 못 찾음 — 효과 미적용");
                continue;
            }

            bu.effects.Add(new ItemStatEffect(item));
            bu.effects.Add(new ItemConditionalEffect(item, this));
            bu.displayItems.Add(item); // HP바 아래 아이콘 표시용
            if (item.ccImmune) bu.HasCcImmuneItem = true;
        }

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

        foreach (var item in unit.items)
        {
            bu.effects.Add(new ItemStatEffect(item));
            bu.effects.Add(new ItemConditionalEffect(item, this));
            bu.displayItems.Add(item); // HP바 아래 아이콘 표시용
            if (item.ccImmune) bu.HasCcImmuneItem = true;
        }

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
        }

        TickBurn();

        foreach (var bu in _units)
        {
            if (!bu.IsAlive) continue;

            bu.TickCcState(TICK_INTERVAL);

            // 자뭉열매: 매초 15% 회복, 완전 회복하거나 다른 아군이 없으면 복귀(기획 확정 7/17).
            if (bu.berryActive)
                bu.TickBerry(TICK_INTERVAL, HasOtherAliveAlly(bu));

            // 마나 충전(기획 확정): 초당 10 고정. 스턴 중에도 차오른다(행동만 불능).
            GainMana(bu, MANA_PER_SECOND * TICK_INTERVAL);

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
                target = FindNearestEnemy(bu);
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
                int moveSteps = Mathf.Max(1, Mathf.RoundToInt(bu.moveSpeedMultiplier));
                for (int i = 0; i < moveSteps && bu.coords.DistanceTo(target.coords) > bu.range; i++)
                    MoveTowards(bu, target.coords);
            }
        }

        // 죽은 유닛 시각화 제거
        foreach (var bu in _units)
        {
            if (!bu.IsAlive && bu.visual != null)
            {
                Destroy(bu.visual);
                bu.visual = null;
                bu.facing = null; // visual과 함께 파괴됨 — 참조를 남기면 다음 틱에 파괴된 객체를 만진다
            }
        }
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

    /// <summary>self를 사거리 내에 둔 적 팀 유닛 수(defSpDefPerAttacker용 근사 — 타겟 캐싱이 없어 "공격 중"의 정확한 정의는 없음).</summary>
    public int CountAttackersInRange(BattleUnit self)
    {
        int count = 0;
        foreach (var other in _units)
            if (other.team != self.team && other.IsAlive && self.coords.DistanceTo(other.coords) <= other.range)
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
    /// public인 이유: BalanceCheckHarness가 밸런스 시트 기대값과 대조할 때 이 함수를 직접 호출한다.
    /// 하네스가 공식을 복사해두면 검증 의미가 없어지므로 실제 계산 경로를 노출한다.
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
                    break;
                case SkillEffectType.ManaRegen: // 기획 확정: 회복량 = spellPower × 0.5 (즉시 지급)
                    GainMana(t, caster.spellPower * SUPPORT_SPELLPOWER_COEF);
                    break;
                case SkillEffectType.AsBuff:    // 기획 확정: 증가량 = spellPower × 0.5 (%p로 해석)
                    t.ApplyAsBuff(1f + caster.spellPower * SUPPORT_SPELLPOWER_COEF / 100f, AS_BUFF_DURATION);
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
                foreach (var u in _units)
                    if (u.team == caster.team && u.IsAlive && !u.IsUntargetable &&
                        caster.coords.DistanceTo(u.coords) <= caster.skillAreaRadius)
                        result.Add(u);
                break;

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

    /// <summary>team 진영에서 살아있는 유닛 중 currentHp/maxHp 비율이 가장 낮은 유닛.</summary>
    private BattleUnit LowestHpRatioAlly(BattleTeam team)
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

        // 크리 (현재 결정론적 기대값 — 각자 보드 로컬 시뮬이라 추후 RNG 크리로 교체 가능)
        ctx.amount *= CritFactor(ctx.source);

        // 경감 (True 타입은 경감 무시 고정딜)
        if (ctx.type != DamageType.True)
            ctx.amount *= Mitigation(ctx.target.defense);

        foreach (var effect in ctx.target.effects)
            effect.OnTakeDamage(ctx.target, ctx);

        // 피해 적용. 피격 비례 마나 획득은 기획 확정(초당 충전만)으로 제거됨.
        ctx.target.currentHp -= ctx.amount;

        ctx.target.TryTriggerSitrusBerry(); // 자뭉열매: 45% 미만 진입 순간 발동(전투당 1회)

        if (!ctx.target.IsAlive)
        {
            if (ctx.target.team == BattleTeam.Enemy && SoundManager.TryGet(out var deathSoundManager))
                deathSoundManager.PlaySfx(SoundId.EnemyDeath);

            foreach (var effect in ctx.source.effects)
                effect.OnKill(ctx.source, ctx.target);
        }
    }

    /// <summary>마나 획득(스킬 보유 유닛만, maxMana 상한). manaGainMultiplier(정령 시너지/치어리더 등 상시 배수) 반영.</summary>
    private static void GainMana(BattleUnit unit, float amount)
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

    /// <summary>목표 쪽으로 거리를 줄이는 인접 칸 중, 다른 살아있는 유닛이 없는 칸으로 한 칸 이동.</summary>
    private void MoveTowards(BattleUnit bu, HexCoords targetCoords)
    {
        int currentDist = bu.coords.DistanceTo(targetCoords);
        HexCoords best = bu.coords;
        int bestDist = currentDist;

        foreach (var neighbor in bu.coords.GetNeighbors())
        {
            int dist = neighbor.DistanceTo(targetCoords);
            if (dist < bestDist && !IsOccupied(neighbor))
            {
                bestDist = dist;
                best = neighbor;
            }
        }

        if (best != bu.coords)
        {
            bu.coords = best;
            UpdateVisualPosition(bu);
        }
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

            if (bu.source != null)
            {
                bu.source.gameObject.SetActive(true);
                bu.source.ResetForBattle();
            }
        }

        _units.Clear();

        foreach (var tile in _mirrorTiles)
            Destroy(tile);

        _mirrorTiles.Clear();
        _enemyTiles.Clear();

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

        var phantomGO = new GameObject("MirrorStatPhantom");
        var phantom = phantomGO.AddComponent<PokemonUnit>();
        phantom.data = species;
        phantom.starLevel = Mathf.Clamp(entry.starLevel, 1, 3);
        phantom.heroStatMultiplier = entry.heroStatMultiplier;
        phantom.isTradeEvolved = entry.isTradeEvolved;
        phantom.equippedStone = stone;
        phantom.roleOverride = entry.roleOverride;

        float maxHp = phantom.MaxHp;
        float attack = phantom.Attack;
        float spellPower = phantom.SpellPower;
        float defense = phantom.Defense;
        float attackSpeed = phantom.AttackSpeed;
        int range = phantom.Range;
        string role = phantom.Role;

        Destroy(phantomGO);

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
            role = role,
            starLevel = Mathf.Clamp(entry.starLevel, 1, 3),
            hasSitrusBerry = entry.hasHeroBerry
        };

        ApplySkill(bu, skill, entry.skillManaCost);
        bu.attackVfxId = !string.IsNullOrEmpty(entry.attackVfxIdOverride) ? entry.attackVfxIdOverride : species.attackVfxId;

        if (item0 != null) AttachMirrorItemEffects(bu, item0);
        if (item1 != null) AttachMirrorItemEffects(bu, item1);

        bu.displayStone = stone;

        return bu;
    }

    /// <summary>CreateBattleUnit의 아이템 훅 부착부와 같은 내용(둘 다 4줄 수준이라 공용 private
    /// 메서드로만 뽑고, CreateBattleUnit 본문은 회귀 위험 때문에 건드리지 않는다).</summary>
    private void AttachMirrorItemEffects(BattleUnit bu, ItemData item)
    {
        bu.effects.Add(new ItemStatEffect(item));
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

        if (SnapshotHasHeroEeveeThreeStar(snapshot))
        {
            SpawnMirrorMutantBots(MutantBots.Length);
        }
        else
        {
            int? mutantTier = SnapshotActiveTier(snapshot, "Mutant");
            if (mutantTier.HasValue)
                SpawnMirrorMutantBots(mutantTier.Value + 1);
        }

        SynergyData dark = SnapshotActiveTier(snapshot, "Dark").HasValue ? FindSynergyByNameEn("Dark") : null;
        if (dark != null)
            MarkMirrorDarkFirstSkillStun(dark);
    }

    /// <summary>MarkDarkFirstSkillStun의 스냅샷 버전 — bu.data.synergies 기준(HasHeroEeveeThreeStar와 같은 이유).</summary>
    private void MarkMirrorDarkFirstSkillStun(SynergyData dark)
    {
        foreach (var bu in _units)
        {
            if (bu.team != BattleTeam.Ally || bu.data == null || bu.data.synergies == null) continue;
            if (!bu.data.synergies.Contains(dark.synergyName) && !bu.data.synergies.Contains(dark.synergyNameEn)) continue;
            bu.darkFirstSkillPending = true;
        }
    }

    /// <summary>HasHeroEeveeThreeStar의 스냅샷 버전 — snapshot.units(evolutionLocked)를 직접 본다.</summary>
    private static bool SnapshotHasHeroEeveeThreeStar(BattleSnapshot snapshot)
    {
        PokemonDatabase db = PokemonDatabase.Instance;
        if (db == null) return false;

        foreach (BattleSnapshot.UnitEntry entry in snapshot.units)
        {
            if (!entry.evolutionLocked || entry.starLevel < 3) continue;

            PokemonData species = db.GetById(entry.speciesId);
            if (species != null &&
                string.Equals(species.pokemonNameEn, "Eevee", System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
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
