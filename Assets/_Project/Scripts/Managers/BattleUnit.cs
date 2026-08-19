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
    public float manaRegenBonus;     // 아이템 MP 스탯 — 초당 마나 충전량에 flat 가산(배수 곱셈 전). 기본 0.
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
    public UnitFacing facing;      // visual에 붙은 회전 컴포넌트(타겟 바라보기). visual과 생명주기 동일.
    public GameObject shieldVfxInstance; // 공용 보호막 상태 VFX(BattleManager.SyncShieldVfx가 관리). world-space 독립 생성 — visual의 자식 아님.

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
    public float shield;                  // 보호막 총량(권위값). 모든 출처(스킬/장비/WATER 시너지/shieldPctOnFatalHit)가
                                           // 여기 합산된다 — 이 필드 자체는 절대 "파생값"으로 바꾸지 않는다(2026-08 확정).

    // ── 보호막 출처 추적(2026-08 확정) ──
    // shieldSources는 "추적 대상"(스킬 SHIELD/Apicot/Shell Bell/Micle)만 담는다. WATER 시너지·
    // shieldPctOnFatalHit(현재 미사용)는 의도적으로 여기 들어가지 않는 미추적 보호막이다 — shield 필드에는
    // 반영되지만 shieldSources 목록·상태 VFX 판정에는 절대 나타나지 않는다.
    //
    // 신규 전투 규칙(2026-08 확정): 피해는 shieldSources를 FIFO(리스트 Add 순서=생성 순서)로 먼저 소진한
    // 뒤, 추적 출처가 전부 바닥나야만 남은 흡수량이 WATER 등 미추적 보호막에서 빠져나간다. 이 우선순위는
    // 기존 코드에 없던 새 규칙이며 AbsorbShieldDamage 안에서만 구현된다(shield 필드 자체엔 이 우선순위가
    // 드러나지 않음 — shieldSources 목록만이 이 규칙의 근거).
    public readonly List<ShieldSource> shieldSources = new();
    public ShieldSource skillShieldSource; // 유닛당 스킬은 최대 1개 — 재시전 시 이 참조가 살아있으면 합산, 아니면 새로 생성.

    private int _nextShieldSequence;
    /// <summary>이 유닛 안에서 ShieldSource를 새로 만들 때마다 호출 — 유닛별 로컬 카운터라 정적/공유 상태가
    /// 전혀 없고, Time.time 등 비결정적 값도 쓰지 않아 실전투/미러가 항상 같은 순서를 재현한다.</summary>
    public int NextShieldSequence() => _nextShieldSequence++;

    private static readonly IReadOnlyList<ShieldSource> EmptyDepletedShieldSources = System.Array.Empty<ShieldSource>();

    /// <summary>
    /// 보호막 흡수를 정확히 1곳에서 처리하는 공용 진입점(BattleManager.ResolveDamage가 아이템 보유 여부와
    /// 무관하게 항상 1회 호출). shieldSources를 FIFO로 먼저 소진하고, 남은 흡수량은 shield 필드에서만
    /// 차감돼 "미추적 보호막(WATER 등)이 나중에 소진된다"는 규칙을 코드 한 곳에서 구현한다.
    /// 각 출처의 remainingAmount만 정확히 줄어들 뿐 다른 출처는 절대 건드리지 않는다.
    /// </summary>
    public ShieldAbsorbResult AbsorbShieldDamage(float incomingDamage)
    {
        if (shield <= 0f || incomingDamage <= 0f)
            return new ShieldAbsorbResult(incomingDamage, EmptyDepletedShieldSources);

        float absorbed = Mathf.Min(shield, incomingDamage);
        float toDistribute = absorbed;
        List<ShieldSource> depleted = null;

        foreach (var source in shieldSources)
        {
            if (toDistribute <= 0f) break;
            if (source.remainingAmount <= 0f) continue; // 이미 소진된 출처는 건너뜀(재차감 방지)

            float take = Mathf.Min(source.remainingAmount, toDistribute);
            source.remainingAmount -= take;
            toDistribute -= take;

            if (source.remainingAmount <= 0f)
                (depleted ??= new List<ShieldSource>()).Add(source);
        }
        // toDistribute>0으로 남았다면 = 추적 출처를 다 쓰고도 남은 흡수량 = WATER 등 미추적 보호막 몫.
        // shieldSources는 여기서 더 건드리지 않고, 아래 shield 차감 한 줄로만 자연스럽게 반영된다.

        shield -= absorbed;
        return new ShieldAbsorbResult(incomingDamage - absorbed, (IReadOnlyList<ShieldSource>)depleted ?? EmptyDepletedShieldSources);
    }

    public float skillDamageAmpPct;       // 스킬 피해 증폭 누적치(%p). 규살열매/초점렌즈/왕의징표석 등이 가산.
                                           // 평타에는 적용 안 됨(ResolveDamage에서 !isBasicAttack만 적용).
    public float basicAttackDamageAmpPct; // 평타 피해 증폭 누적치(%p). 왕의징표석/선제공격손톱 등이 가산.
                                           // 스킬에는 적용 안 됨(ResolveDamage에서 isBasicAttack만 적용).
    public float damageAmpPct;            // 공용 피해 증폭 누적치(%p, AMP). 평타/스킬 모두 적용.
                                           // skillDamageAmpPct/basicAttackDamageAmpPct와 별도로 순차 곱(복리 아님).
    public float pendingAuraAttackSpeedPct; // 구애머리띠 등 "전투 시작 인접 아군" 공격속도% 오라의 임시 누적분(%p).
                                             // ApplyOnCombatStartEffects 2단계에서 여러 오라가 각각 이 필드에 더하기만 하고,
                                             // 모든 유닛의 2단계가 끝난 뒤 3단계에서 attackSpeed에 딱 1번만 곱해 소비한다
                                             // (오라 2개=+60%를 ×1.3×1.3=1.69가 아니라 ×1.6으로 만들기 위한 장치).
    public float burnDamagePerTick;       // 화상 중 매틱 고정(True) 피해. 0이면 화상 없음.
    public float burnTicksRemaining;      // 화상 잔여 틱 수(시간이 아니라 틱 카운트).
    public float moveSpeedMultiplier = 1f; // 이동 가속 배수(moveSpdPctOnKill로 누적 가산).
    public float moveCooldown;             // 0 이하가 되면 이동 가능(BattleManager._moveInterval 기반, attackCooldown과 동일 패턴).

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

    // ── 이브이 영웅증강 v2 "나인이볼부스트" — 스킬 1회당 진화체 1종 소환 + 자기 버프 ──
    // 이 값이 곧 "다음에 소환할 HeroEeveeBoostTable.Entries의 인덱스"다.
    //   -1        = 이 유닛은 소환 주체가 아님(대부분의 유닛).
    //   0 ~ 7     = 다음 스킬 시전 때 그 인덱스의 종을 소환하고 값이 1 올라간다.
    //   Count(8)  = 8종을 다 소환함 — 이후 스킬은 원래 스킬 효과로 되돌아간다.
    // BattleUnit에 두는 이유: 미러 전투의 아군은 source가 null이라(스냅샷 복원) PokemonUnit을
    // 통해서는 판정할 수 없다. darkFirstSkillPending과 같은 패턴이다.
    public int heroEeveeSummonIndex = -1;

    // 나인이볼부스트가 maxMana를 성급별 값으로 덮어쓰기 <b>전</b>의 원래 코스트(EffectiveManaCost).
    // 8종을 다 소환하고 나면 원래 스킬로 되돌아가므로 마나 코스트도 같이 원복해야 한다 —
    // 안 그러면 폴백한 원래 스킬이 설계보다 싼 값으로 계속 나간다.
    public float heroEeveeBaseMaxMana;

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
