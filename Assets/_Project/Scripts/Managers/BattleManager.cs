using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BattleTeam { Ally, Enemy }

/// <summary>
/// 전투 중 한 유닛의 런타임 상태. 원본 PokemonUnit은 변경하지 않고 이 클래스에 스냅샷.
/// </summary>
public class BattleUnit
{
    public PokemonUnit source;     // 아군이면 보드 위 원본 참조(시각화 토글용), 적이면 null
    public BattleTeam team;
    public HexCoords coords;

    public float currentHp;
    public float maxHp;
    public float attack;
    public float specialAttack;
    public float defense;
    public float specialDefense;
    public float attackSpeed;
    public int range;
    public AttackType attackType;

    // ── 마나/스킬 (maxMana <= 0 이면 스킬 없음 → 평타만) ──
    public float currentMana;
    public float maxMana;            // = skill.manaCost
    public float skillPower;         // skill.damage × 별 배수(×적 statMul) — 시전 시 특수방어로 경감
    public SkillTargetType skillTargetType;
    public int   skillAreaRadius;    // Area: 대상 중심 반경(칸)
    public int   skillLineLength;    // Line: 시전자 기준 사거리(칸)

    public bool HasSkill => maxMana > 0f;

    public float attackCooldown;   // 0 이하가 되면 공격 가능
    public GameObject visual;

    public bool IsAlive => currentHp > 0f;
}

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

    // 마나(TFT식): 평타 1회당 고정 획득 + 피해 받을 때 피해량 비례 획득(피격당 상한).
    private const float MANA_PER_ATTACK = 10f;
    private const float MANA_PER_DAMAGE_TAKEN = 0.05f;  // 받은 피해의 5%
    private const float MANA_GAIN_CAP_PER_HIT = 20f;    // 한 번 피격으로 얻는 마나 상한

    // 상대 보드를 시각적으로 분리해서 보여주기 위한 월드 오프셋. 전투 좌표 계산에는 영향 없음(시각화 전용).
    private static readonly Vector3 ENEMY_BOARD_OFFSET = new Vector3(0f, 0f, 10f);

    // 현재 스테이지는 RoundPhaseManager(Phase.CurrentStage)가 라운드별로 확정해 제공.
    // 적 영문명 → PokemonData 해석은 중앙 PokemonDatabase.Instance가 담당.
    // 스테이지/DB 둘 중 하나라도 없으면 "내 보드 미러"로 폴백(씬/디버그 호환).

    private readonly List<BattleUnit> _units = new();
    private readonly List<GameObject> _mirrorTiles = new();
    private Coroutine _battleCoroutine;

    private void OnEnable()  => GameEvents.OnBattleStart += HandleBattleStart;
    private void OnDisable() => GameEvents.OnBattleStart -= HandleBattleStart;

    private void HandleBattleStart()
    {
        if (_battleCoroutine != null) StopCoroutine(_battleCoroutine);
        _battleCoroutine = StartCoroutine(RunBattle());
    }

    private IEnumerator RunBattle()
    {
        SetupUnits();

        if (_units.Count == 0)
        {
            // 보드에 유닛이 하나도 없음 — 즉시 종료(엣지케이스), 승리로 처리
            GameEvents.BattleEnd(true);
            yield break;
        }

        // TODO: GameManager.Instance.Synergy.GetActiveSynergies()로 활성 시너지 버프 적용.
        // SynergyTier에 구조화된 효과 데이터가 아직 없어(설명 문자열뿐) v1에서는 보류.

        bool? allyWon = null;
        int tick = 0;

        while (tick < MAX_TICKS)
        {
            SimulateTick();

            bool allyAlive  = HasAliveUnit(BattleTeam.Ally);
            bool enemyAlive = HasAliveUnit(BattleTeam.Enemy);

            if (!allyAlive || !enemyAlive)
            {
                allyWon = allyAlive; // 둘 다 전멸하면 false(패배 처리)
                break;
            }

            tick++;
            yield return new WaitForSeconds(TICK_INTERVAL);
        }

        if (allyWon == null)
            allyWon = DetermineWinnerByRemainingHp();

        Cleanup();
        GameEvents.BattleEnd(allyWon.Value);
    }

    // ─────────────────────────────────────────
    // 셋업
    // ─────────────────────────────────────────

    private void SetupUnits()
    {
        _units.Clear();

        var board = GameManager.Instance.Board;

        // 아군: 내 보드 스냅샷 그대로.
        foreach (var kv in board.GetBoardSnapshot())
        {
            PokemonUnit unit = kv.Value;
            if (unit == null || unit.data == null) continue;
            _units.Add(CreateAllyUnit(unit, kv.Key));
        }

        // 적: 현재 스테이지(RoundPhaseManager가 라운드별로 확정) → 미러 좌표에 생성.
        StageData stage = GameManager.Instance.Phase != null ? GameManager.Instance.Phase.CurrentStage : null;
        int enemyCount = stage != null ? SpawnEnemiesFromStage(stage, board) : 0;

        // 폴백: 스테이지/적이 하나도 없으면 기존 "내 보드 미러"로 대결(씬/디버그 호환).
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

        foreach (var bu in _units)
            SpawnVisual(bu);

        SpawnMirrorBoard(board);
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

            // 기획은 적 자기 진영 로컬좌표(0~3행)로 작성 → 미러해서 플레이어 앞에 배치.
            HexCoords coords = board.GetMirroredCoords(new HexCoords(e.q, e.r));
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

    /// <summary>"내 보드 미러" 폴백 적 생성(기존 동작). StageData 도입 전/디버그용.</summary>
    private void SpawnMirrorEnemies(BoardManager board)
    {
        foreach (var kv in board.GetBoardSnapshot())
        {
            PokemonUnit unit = kv.Value;
            if (unit == null || unit.data == null) continue;
            HexCoords enemyCoords = board.GetMirroredCoords(kv.Key);
            _units.Add(CreateBattleUnit(unit, BattleTeam.Enemy, enemyCoords));
        }
    }

    /// <summary>
    /// 보드 전체 칸을 점대칭 미러 좌표에 깔아 "상대 보드"를 임시로 시각화.
    /// 실제 타일 프리팹이 아닌 디버그용 평면 — 전투 종료 시 제거.
    /// </summary>
    private void SpawnMirrorBoard(BoardManager board)
    {
        foreach (var coords in board.GetBoardSnapshot().Keys)
        {
            HexCoords mirrored = board.GetMirroredCoords(coords);

            var tile = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tile.name = $"MirrorBoardTile_{mirrored}";
            tile.transform.localScale = new Vector3(0.95f, 0.05f, 0.95f);
            tile.GetComponent<Renderer>().material.color = new Color(1f, 0.7f, 0.7f); // 상대 보드 표시(연빨강)
            tile.transform.position = board.CoordsToWorldPosition(mirrored) + ENEMY_BOARD_OFFSET;

            _mirrorTiles.Add(tile);
        }
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
            team = BattleTeam.Enemy,
            coords = coords,
            maxHp = maxHp,
            currentHp = maxHp,
            attack = data.attack * star * sm * am,
            specialAttack = data.specialAttack * star * sm,
            defense = data.defense * sm,
            specialDefense = data.specialDefense * sm,
            attackSpeed = data.attackSpeed,
            range = Mathf.Max(1, data.range),
            attackType = data.attackType,
            attackCooldown = 0f
        };
        ApplySkill(bu, data.skill, star * sm); // 스킬 위력도 별·강화 배수 반영
        return bu;
    }

    /// <summary>스킬 데이터를 BattleUnit에 반영. manaCost가 없거나 skill이 없으면 평타만(maxMana=0).</summary>
    private static void ApplySkill(BattleUnit bu, PokemonSkillData skill, float powerScale)
    {
        if (skill == null || skill.manaCost <= 0) return;

        bu.maxMana          = skill.manaCost;
        bu.currentMana      = 0f;
        bu.skillPower       = skill.damage * powerScale;
        bu.skillTargetType  = skill.targetType;
        bu.skillAreaRadius  = Mathf.Max(1, skill.areaRadius);
        bu.skillLineLength  = Mathf.Max(1, skill.lineLength);
    }

    private BattleUnit CreateBattleUnit(PokemonUnit unit, BattleTeam team, HexCoords coords)
    {
        var bu = new BattleUnit
        {
            source = team == BattleTeam.Ally ? unit : null,
            team = team,
            coords = coords,
            maxHp = unit.MaxHp,
            currentHp = unit.MaxHp,
            attack = unit.Attack,
            specialAttack = unit.SpecialAttack,
            defense = unit.Defense,
            specialDefense = unit.SpecialDefense,
            attackSpeed = unit.AttackSpeed,
            range = Mathf.Max(1, unit.Range), // 데이터 미설정(0) 시 인접칸까지는 사거리로 취급(TFT 근접 기본)
            attackType = unit.AttackType,
            attackCooldown = 0f
        };
        if (unit.data != null) ApplySkill(bu, unit.data.skill, unit.StarMultiplier); // 스킬 위력에 별 배수 반영
        return bu;
    }

    /// <summary>아군은 원본 오브젝트를 숨기고 그 자리에, 적은 미러 좌표에 시각화용 캡슐을 띄움.</summary>
    private void SpawnVisual(BattleUnit bu)
    {
        if (bu.source != null)
            bu.source.gameObject.SetActive(false);

        var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = $"BattleVisual_{bu.team}_{bu.coords}";
        visual.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
        visual.GetComponent<Renderer>().material.color = bu.team == BattleTeam.Ally ? Color.blue : Color.red;

        bu.visual = visual;
        UpdateVisualPosition(bu);
    }

    private void UpdateVisualPosition(BattleUnit bu)
    {
        if (bu.visual == null) return;
        Vector3 pos = GameManager.Instance.Board.CoordsToWorldPosition(bu.coords);
        if (bu.team == BattleTeam.Enemy) pos += ENEMY_BOARD_OFFSET;
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

            BattleUnit target = FindNearestEnemy(bu);
            if (target == null) continue;

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

                    bu.attackCooldown += bu.attackSpeed > 0f ? 1f / bu.attackSpeed : 1f;
                }
            }
            else
            {
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
            }
        }
    }

    /// <summary>평타 1회: 물리/특수 피해 + 시전자 마나 획득.</summary>
    private void BasicAttack(BattleUnit attacker, BattleUnit target)
    {
        float raw = attacker.attackType == AttackType.Physical
            ? attacker.attack - target.defense
            : attacker.specialAttack - target.specialDefense;

        DealDamage(target, Mathf.Max(1f, raw));
        GainMana(attacker, MANA_PER_ATTACK);
    }

    /// <summary>스킬 시전: 마나 소모(0으로) 후 targetType에 따라 대상들에게 특수 피해.</summary>
    private void CastSkill(BattleUnit caster, BattleUnit primaryTarget)
    {
        caster.currentMana = 0f;

        var targets = GetSkillTargets(caster, primaryTarget);
        foreach (var t in targets)
        {
            if (t == null || !t.IsAlive) continue;
            float dmg = Mathf.Max(1f, caster.skillPower - t.specialDefense); // 스킬은 특수방어로 경감
            DealDamage(t, dmg);
        }

        Debug.Log($"[Battle] {caster.team} 스킬 시전 → 대상 {targets.Count}기 (위력 {caster.skillPower:0})");
    }

    /// <summary>targetType별 피격 대상 목록. 항상 살아있는 적만 반환.</summary>
    private List<BattleUnit> GetSkillTargets(BattleUnit caster, BattleUnit primaryTarget)
    {
        var result = new List<BattleUnit>();
        if (primaryTarget == null) return result;

        switch (caster.skillTargetType)
        {
            case SkillTargetType.All:
                foreach (var u in _units)
                    if (u.team != caster.team && u.IsAlive) result.Add(u);
                break;

            case SkillTargetType.Area: // 대상 중심 반경 내 적 전부
                foreach (var u in _units)
                    if (u.team != caster.team && u.IsAlive &&
                        u.coords.DistanceTo(primaryTarget.coords) <= caster.skillAreaRadius)
                        result.Add(u);
                break;

            case SkillTargetType.Line: // 시전자 사거리 내 + 대상 방향(앞쪽)의 적
            {
                int toTarget = caster.coords.DistanceTo(primaryTarget.coords);
                foreach (var u in _units)
                    if (u.team != caster.team && u.IsAlive &&
                        caster.coords.DistanceTo(u.coords) <= caster.skillLineLength &&
                        u.coords.DistanceTo(primaryTarget.coords) <= toTarget)
                        result.Add(u);
                if (!result.Contains(primaryTarget)) result.Add(primaryTarget);
                break;
            }

            default: // Single
                result.Add(primaryTarget);
                break;
        }

        return result;
    }

    /// <summary>피해 적용 + 피격자 마나 획득(피해 비례, 피격당 상한).</summary>
    private void DealDamage(BattleUnit target, float amount)
    {
        target.currentHp -= amount;
        GainMana(target, Mathf.Min(amount * MANA_PER_DAMAGE_TAKEN, MANA_GAIN_CAP_PER_HIT));
    }

    /// <summary>마나 획득(스킬 보유 유닛만, maxMana 상한).</summary>
    private static void GainMana(BattleUnit unit, float amount)
    {
        if (!unit.HasSkill) return;
        unit.currentMana = Mathf.Min(unit.maxMana, unit.currentMana + amount);
    }

    private BattleUnit FindNearestEnemy(BattleUnit bu)
    {
        BattleUnit nearest = null;
        int nearestDist = int.MaxValue;

        foreach (var other in _units)
        {
            if (other.team == bu.team || !other.IsAlive) continue;

            int dist = bu.coords.DistanceTo(other.coords);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = other;
            }
        }

        return nearest;
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

    private bool HasAliveUnit(BattleTeam team)
    {
        foreach (var bu in _units)
            if (bu.team == team && bu.IsAlive)
                return true;
        return false;
    }

    /// <summary>타임아웃 시 남은 총 HP 비율로 승패 결정.</summary>
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

    // ─────────────────────────────────────────
    // 정리
    // ─────────────────────────────────────────

    private void Cleanup()
    {
        foreach (var bu in _units)
        {
            if (bu.visual != null)
                Destroy(bu.visual);

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
    }
}
