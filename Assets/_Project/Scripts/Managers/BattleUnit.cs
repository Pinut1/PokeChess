using System.Collections.Generic;
using UnityEngine;

public enum BattleTeam { Ally, Enemy }

/// <summary>
/// 전투 중 한 유닛의 런타임 상태. 원본 PokemonUnit은 변경하지 않고 이 클래스에 스냅샷.
/// </summary>
public class BattleUnit
{
    public PokemonUnit source;     // 아군이면 보드 위 원본 참조(시각화 토글용), 적이면 null
    public PokemonData data;
    public BattleTeam team;
    public HexCoords coords;

    public float currentHp;
    public float maxHp;
    public float attack;          // 평타 데미지
    public float defense;         // 받는 데미지 경감
    public float spellPower;      // 스킬 데미지(SPELL effectType) 기반값
    public float attackSpeed;
    public int range;

    // ── 크리티컬 (아이템으로 부여, 기본 무영향) ──
    public float critChance     = 0f;     // 0~1. criPct 아이템으로 증가
    public float critMultiplier = 1.5f;   // 크리 시 배수(TFT 표준). criDmgPct 아이템으로 증가

    // ── 마나/스킬 (maxMana <= 0 이면 스킬 없음 → 평타만) ──
    public float currentMana;
    public float maxMana;            // = PokemonData.manaCost
    public float manaGainMultiplier = 1f; // 마나 충전 속도 배수(정령 시너지/치어리더 등). 1=기본.
    public SkillEffectType skillEffectType;  // 효과 분기 (Attack/Spell=데미지, Stun/Slow/Taunt=CC, HpRegen/Shield/ManaRegen/AsBuff=지원)
    public SkillTargetType skillTargetType;
    public int   skillAreaRadius;    // *_Area: 중심 반경(칸)
    public int   skillLineLength;    // EnemyLine: 시전자 기준 직선(칸)
    public string skillVfxId;        // 시전 시 재생할 VFX(VfxDatabase 키). 비어있으면 재생 없음.

    // 평타 VFX는 스킬과 달리 PokemonData.attackVfxId에서 온다(스킬 테이블이 아님).
    // 프리팹이 "대상 머리 위에서 떨어지는" 연출을 자체적으로 갖고 있어, 코드는 피격자 위치에 생성만 한다(해인 확인 7/24).
    public string attackVfxId;

    public bool HasSkill => maxMana > 0f;

    public float attackCooldown;   // 0 이하가 되면 공격 가능
    public GameObject visual;

    // ── 효과 훅(기둥B) — 아이템/스킬/시너지가 전투 틱에 꽂히는 진입점 ──
    public readonly List<ICombatEffect> effects = new();

    /// <summary>
    /// 표시 전용 장착 아이템 목록(HP바 아래 아이콘). effects는 ICombatEffect로 변환돼 있어
    /// 어떤 아이템이었는지 되짚을 수 없으므로 원본 ItemData를 따로 들고 있는다.
    /// 아군은 PokemonUnit.items, 적은 트레이너 heldItemEn을 해석한 결과가 들어온다.
    /// 전투 로직은 이 목록을 읽지 않는다 — 효과는 전부 effects가 담당.
    /// </summary>
    public readonly List<ItemData> displayItems = new();

    /// <summary>
    /// 표시 전용 장착 진화의 돌. 돌은 효과가 아니라 종족 교체(data 스왑)로 반영되므로
    /// effects에도 displayItems에도 남지 않는다 — 아이콘을 띄우려면 따로 들고 있어야 한다.
    /// 아군만 값이 들어오고(적은 트레이너 데이터에 돌 개념이 없음) 전투 로직은 읽지 않는다.
    /// </summary>
    public EvolutionStoneData displayStone;

    // ── 조건부 아이템 효과 상태(기둥B 2단계) ──
    public float shield;                  // 보호막 흡수량. 0 이하면 없음(shieldPctOnFatalHit).
    public float burnDamagePerTick;       // 화상 중 매틱 고정(True) 피해. 0이면 화상 없음.
    public float burnTicksRemaining;      // 화상 잔여 틱 수(시간이 아니라 틱 카운트).
    public float moveSpeedMultiplier = 1f; // 이동 가속 배수(moveSpdPctOnKill로 누적 가산).

    // ── CC 상태(기둥C) ──
    public float stunRemaining;           // 0보다 크면 행동 불능.
    public float slowMultiplier = 1f;     // 1=정상, 0.5=공속 50% 감소.
    public float slowRemaining;           // 슬로우 잔여 시간. 0 도달 시 slowMultiplier 1로 복원.
    public BattleUnit tauntedBy;          // null 아니면 이 유닛을 강제 타겟(시간 만료 또는 도발자 사망 시 해제).
    public float tauntRemaining;          // 도발 잔여 시간. 0 도달 시 tauntedBy 해제.
    public BattleUnit lastTickTarget;     // 이번 틱에 실제로 노린 대상(도발 스냅샷용, 매틱 갱신).
    public BattleUnit tauntReturnTarget;  // 도발 종료 후 복귀할 "원래 타겟"(기획 확정: 재계산 아님).
    public bool HasCcImmuneItem;          // ccImmune 아이템 보유 여부.
    public bool ccImmuneConsumed;         // ccImmune 최초 1회 소모 여부.
    public string role = "";              // PokemonData.role 스냅샷(타겟 우선순위용).
    public int starLevel = 1;             // PokemonUnit.starLevel 스냅샷(날따름 지속시간 공식용).

    // ── 악(DARK) 시너지 — 첫 스킬 시전 시 대상 스턴(1회 소비) ──
    public bool darkFirstSkillPending;    // true면 다음 스킬 시전 시 대상에 스턴 부여 후 false로 소비.

    // ── 지원 스킬 버프(AsBuff) — CC와 동일한 패턴(1=무효과, 시간 지나면 복원) ──
    public float asBuffMultiplier = 1f;   // 1=정상, 1.5=공속 50% 증가.
    public float asBuffRemaining;         // 버프 잔여 시간. 0 도달 시 asBuffMultiplier 1로 복원.

    // ── 자뭉열매(파치리스 영웅증강 v2, TFT 블리츠크랭크식 — 기획 확정 7/17) ──
    // 전투당 1회, HP 45% 미만 시 언타겟+행동불능으로 빠져 매초 maxHP 15% 회복.
    // 완전 회복하거나 다른 아군이 없으면 전투에 복귀한다.
    public const float BERRY_TRIGGER_HP_RATIO  = 0.45f; // 기획 확정(v2)
    public const float BERRY_HEAL_PCT_PER_SEC  = 0.15f; // 기획 확정(v2)

    public bool hasSitrusBerry;       // 파치리스 영웅증강 유닛만 true(전투 시작 스냅샷)
    public bool sitrusBerryConsumed;  // 전투당 1회 소비 여부
    public bool berryActive;          // true면 열매 시식 중

    /// <summary>언타겟 상태(자뭉열매 시식 중). 모든 대상 선정(타겟팅·범위기·도발)에서 제외된다.</summary>
    public bool IsUntargetable => berryActive;

    public bool IsAlive => currentHp > 0f;

    // ─────────────────────────────────────────
    // CC/지원 상태 변이 — 상태(필드)와 그 상태를 바꾸는 행동을 한 곳에 모음.
    // BattleManager(타겟 선정)가 대상을 고른 뒤 이 메서드들로 위임한다.
    // ─────────────────────────────────────────

    /// <summary>매틱 호출 — 스턴/슬로우/AsBuff 잔여시간 차감 및 만료 복원, taunt 시간 만료·도발자 사망 시 해제.</summary>
    public void TickCcState(float deltaTime)
    {
        if (stunRemaining > 0f)
            stunRemaining = Mathf.Max(0f, stunRemaining - deltaTime);

        if (slowRemaining > 0f)
        {
            slowRemaining = Mathf.Max(0f, slowRemaining - deltaTime);
            if (slowRemaining <= 0f) slowMultiplier = 1f;
        }

        if (asBuffRemaining > 0f)
        {
            asBuffRemaining = Mathf.Max(0f, asBuffRemaining - deltaTime);
            if (asBuffRemaining <= 0f) asBuffMultiplier = 1f;
        }

        if (tauntedBy != null)
        {
            tauntRemaining = Mathf.Max(0f, tauntRemaining - deltaTime);
            if (tauntRemaining <= 0f || !tauntedBy.IsAlive)
            {
                // 도발 종료 — 복귀 타겟(tauntReturnTarget)은 남겨둔다.
                // 타겟 선정(BattleManager)이 "복귀 타겟 생존 시 그쪽 우선"으로 소비.
                tauntedBy = null;
                tauntRemaining = 0f;
            }
        }

    }

    /// <summary>
    /// 피해로 HP가 깎인 직후 호출 — 자뭉열매 발동 판정(전투당 1회).
    /// 45% "미만"으로 내려간 순간 발동. 이미 죽었으면(과잉 피해) 발동하지 않는다(부활 아님).
    /// </summary>
    public void TryTriggerSitrusBerry()
    {
        if (!hasSitrusBerry || sitrusBerryConsumed || !IsAlive) return;
        if (maxHp <= 0f || currentHp / maxHp >= BERRY_TRIGGER_HP_RATIO) return;

        sitrusBerryConsumed = true;
        berryActive = true;
        Debug.Log($"[Battle] 자뭉열매 발동 — 언타겟 + 매초 {BERRY_HEAL_PCT_PER_SEC:P0} 회복, 완전 회복/아군 전멸 시 복귀 (HP {currentHp:0}/{maxHp:0})");
    }

    /// <summary>
    /// 자뭉열매 매틱 처리(BattleManager가 호출 — "다른 아군 생존" 판정은 로스터를 아는 매니저 몫).
    /// 매초 maxHP 15% 회복하고, 완전 회복하거나 다른 아군이 없으면 복귀한다.
    /// </summary>
    public void TickBerry(float deltaTime, bool hasOtherAliveAlly)
    {
        if (!berryActive) return;

        ApplyHeal(maxHp * BERRY_HEAL_PCT_PER_SEC * deltaTime); // "매초 15%"의 틱 분할

        if (currentHp >= maxHp || !hasOtherAliveAlly)
        {
            berryActive = false;
            Debug.Log($"[Battle] 자뭉열매 종료 — 복귀 ({(currentHp >= maxHp ? "완전 회복" : "아군 전멸")}, HP {currentHp:0}/{maxHp:0})");
        }
    }

    /// <summary>ccImmune 면역을 1회 소모(보유 시 무효화하고 true 반환).</summary>
    private bool TryConsumeCcImmunity()
    {
        if (!HasCcImmuneItem || ccImmuneConsumed) return false;
        ccImmuneConsumed = true;
        return true;
    }

    public void ApplyStun(float duration)
    {
        if (TryConsumeCcImmunity()) return;
        stunRemaining = Mathf.Max(stunRemaining, duration);
    }

    public void ApplySlow(float multiplier, float duration)
    {
        if (TryConsumeCcImmunity()) return;
        slowMultiplier = Mathf.Min(slowMultiplier, multiplier);
        slowRemaining = Mathf.Max(slowRemaining, duration);
    }

    /// <summary>
    /// duration 동안 강제 타겟(날따름, 기획 확정 2026-07-10). 도발자가 먼저 죽으면 조기 해제.
    /// 발동 시점의 원래 타겟을 스냅샷으로 저장 → 종료 시 재계산이 아니라 저장된 타겟으로 복귀.
    /// 도발 중 재도발되면 최초 스냅샷을 유지한다(원래 타겟이 도발자로 오염되는 것 방지).
    /// </summary>
    public void ApplyTaunt(BattleUnit caster, float duration)
    {
        if (TryConsumeCcImmunity()) return;
        if (tauntedBy == null)
            tauntReturnTarget = lastTickTarget; // 도발 직전에 노리던 대상
        tauntedBy = caster;
        tauntRemaining = Mathf.Max(tauntRemaining, duration);
    }

    public void ApplyHeal(float amount)
        => currentHp = Mathf.Min(maxHp, currentHp + Mathf.Max(0f, amount));

    public void ApplyShield(float amount)
        => shield += Mathf.Max(0f, amount);

    public void ApplyAsBuff(float multiplier, float duration)
    {
        asBuffMultiplier = Mathf.Max(asBuffMultiplier, multiplier);
        asBuffRemaining  = Mathf.Max(asBuffRemaining, duration);
    }
}
