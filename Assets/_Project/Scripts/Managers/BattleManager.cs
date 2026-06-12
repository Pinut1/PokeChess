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

    public float attackCooldown;   // 0 이하가 되면 공격 가능
    public GameObject visual;

    public bool IsAlive => currentHp > 0f;
}

/// <summary>
/// 자동 전투 진행 담당.
/// GameEvents.OnBattleStart 수신 시 BoardManager 스냅샷으로 아군 팀을 만들고,
/// 보드 중심 기준 점대칭 미러 좌표에 동일한 구성의 적 팀을 생성해 거울 대결을 시뮬레이션한다.
/// 결과는 GameEvents.BattleEnd(isWin)으로 통지.
///
/// PvP 상대 보드 동기화는 미구현 — 적 팀은 자기 보드의 미러로 대체 (TODO: 네트워크 동기화 도입 시 교체).
/// </summary>
public class BattleManager : MonoBehaviour
{
    private const float TICK_INTERVAL = 0.1f;
    private const int MAX_TICKS = 300; // 30초 타임아웃

    private readonly List<BattleUnit> _units = new();
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
        foreach (var kv in board.GetBoardSnapshot())
        {
            PokemonUnit unit = kv.Value;
            if (unit == null || unit.data == null) continue;

            HexCoords allyCoords = kv.Key;
            HexCoords enemyCoords = new HexCoords(-allyCoords.q, -allyCoords.r);

            _units.Add(CreateBattleUnit(unit, BattleTeam.Ally, allyCoords));
            _units.Add(CreateBattleUnit(unit, BattleTeam.Enemy, enemyCoords));
        }

        foreach (var bu in _units)
            SpawnVisual(bu);
    }

    private BattleUnit CreateBattleUnit(PokemonUnit unit, BattleTeam team, HexCoords coords)
    {
        return new BattleUnit
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
                    Attack(bu, target);
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

    private static void Attack(BattleUnit attacker, BattleUnit target)
    {
        float raw = attacker.attackType == AttackType.Physical
            ? attacker.attack - target.defense
            : attacker.specialAttack - target.specialDefense;

        float damage = Mathf.Max(1f, raw);
        target.currentHp -= damage;
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
    }
}
