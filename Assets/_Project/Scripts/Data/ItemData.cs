using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ItemStatModifier
{
    public string key;
    public float value;
    public bool isPercent;
    public bool hasValue = true;
}

[CreateAssetMenu(menuName = "PokeChess/Item", fileName = "NewItem_Data")]
public class ItemData : ScriptableObject
{
    [Header("기본 정보")]
    public int id;
    public string itemName;
    public string itemNameEn;

    [TextArea]
    public string description;

    [Header("원본 스탯 (GeneralItem 시트)")]
    public List<ItemStatModifier> sourceStats = new();

    [Header("스탯 효과")]
    public float hpBonus;               // hp
    public float maxHpPct;              // maxHpPct
    public float hpRegenPercent;        // hpRegenPercent — 매 초 최대 HP의 N% 회복
    public float healTakenDmgPct;       // healTakenDmgPct — 피격 피해의 N% 흡수
    public float shieldPctOnFatalHit;   // shieldPctOnFatalHit — 치명적 피해 시 최대HP N% 보호막

    public float attackBonus;           // atk
    public float attackPct;             // AD% — 공격력 N% 증가(현재값 기준)
    public float spAtkPct;              // spAtkPct
    public float spellPowerBonus;       // AP flat — 주문력 N 고정 증가
    public float hpConditionalSpAtkPct; // hpConditionalSpAtkPct — 기합의머리띠 전용: HP>50%면 이 값의 2배, 아니면 그대로 N%를
                                         // 주문력에 동적 가산(다른 아이템/시너지의 주문력은 건드리지 않음). 0이면 미보유.
    public float attackSpeedBonus;      // atkSpdPct
    public float moveSpdPctOnKill;      // moveSpdPctOnKill — 적 처치 시 이동속도 N%

    public float defenseBonus;          // def
    public float defensePct;            // DEF% — 방어력 N% 증가(현재값 기준)
    public float spDefBonus;            // spDef
    public float reflectPhysPct;        // reflectPhysPct — 물리 피해 N% 반사
    public float reflectSpPct;          // reflectSpPct — 특수 피해 N% 반사
    public float defSpDefPerAttacker;   // defSpDefPerAttacker — 공격 중인 적 1명당 방/특방 +N

    public float criPct;                // criPct — 치명타율 N%
    public float criDmgPct;             // criDmgPct — 치명타 피해 N%

    public float manaRegenBonus;        // MP flat — 초당 마나 충전량에 고정 가산(배수 곱셈 전)

    // ── 보호막/시간 기반 고유효과(2026-08 기획 확정 2차분) ──
    public float combatStartShieldPct;         // 규살열매 — 전투 시작 즉시 최대HP N% 보호막
    public float shieldDuration;                // 규살열매(8)/조개껍질방울(5) 공용 — 보호막 최대 유지 시간(초)
    public float onShieldEndSkillDamageAmpPct;  // 규살열매 — 보호막 소멸(만료/소진) 시 스킬피해 N%p 영구 가산
    public float hpThresholdPct;                // 조개껍질방울(40)/의문열매(60) 공용 — 이 HP비율 이하 진입 시 발동(전투당 1회)
    public float thresholdShieldPct;             // 조개껍질방울(25)/의문열매(40) 공용 — 임계값 발동 시 최대HP N% 보호막
    public float shieldDecayDuration;            // 의문열매 — 부여된 보호막이 이 시간(초) 동안 선형으로 0까지 감소
    public float periodicInterval;               // 초점렌즈(5)/캄라열매(1) 공용 — 이 주기(초)마다 1스택
    public float periodicSkillDamageAmpPct;      // 초점렌즈 — 주기마다 스킬피해 N%p 누적(상한 없음)
    public float periodicAttackSpeedPct;         // 캄라열매 — 주기마다 공격속도 N% 가산형 누적(상한 없음)

    // ── 스택형 고유효과(2026-08 기획 확정 3차분) ──
    public float basicAttackDamageAmpPerStackPct; // 왕의징표석(2)/선제공격손톱(3.5) 공용 — 스택 1개당 평타피해 N%p
    public float skillDamageAmpPerStackPct;       // 왕의징표석 전용 — 스택 1개당 스킬피해 N%p(선제공격손톱은 0=미사용)
    public float maxCombatStacks;                 // 왕의징표석(25)/선제공격손톱(15) 공용 — 최대 스택 수(도달 후 정지)
    public float maxStackDamageAmpPct;            // 왕의징표석 전용 — 최대 스택 도달 시 공용 AMP N%p 1회 가산
    public float maxStackAttackSpeedPct;          // 선제공격손톱 전용 — 최대 스택 도달 시 공격속도 N% 1회 가산
    public bool  gainStackOnTakeDamage;           // 왕의징표석 전용 true — 피격 시에도 스택 증가(선제공격손톱은 false)

    // ── 상시/기본형 고유효과(2026-08 기획 확정 4차분) ──
    // 아래 둘은 "기본 stat"이라 ItemConditionalEffect가 아니라 ItemStatFormula.ApplyAll(전투 시작
    // 1회)에서 다른 기본 스탯과 함께 처리된다 — 트리거·조건 없음(치리열매/야타비열매/삐삐인형).
    public float damageAmpPct;                    // 치리열매(10)/야타비열매(15) 공용 — 공용 피해증폭 N%p
                                                    // (왕의징표석 25스택 보너스와 같은 BattleUnit.damageAmpPct에 가산)
    public float skillDamageAmpPct;               // 삐삐인형 — 상시 스킬피해 N%p(BattleUnit.skillDamageAmpPct에
                                                    // 가산 — 초점렌즈/규살열매/왕의징표석의 전투 중 동적 가산과 같은 필드)
    public float healLowestAllyPctOfDamage;        // 큰뿌리 — 자신이 가한 최종 HP 피해량의 N%로 팀 내 최저HP비율
                                                    // 아군 1명 회복(본인 포함 가능, 사망자 제외, maxHp 클램프)

    // ── 전투 시작 인접 아군 오라(2026-08 기획 확정 5차분) ──
    // 대상: 전투 시작 시 hex distance==1인 같은 팀 아군(자신 제외). ItemConditionalEffect.OnCombatStart에서 처리.
    public float adjacentAllyAttackSpeedPct;    // 구애머리띠 — 인접 아군 공격속도 +N%(전투 종료까지 유지, 중첩은 가산)
    public float adjacentAllySpellPowerBonus;   // 라즈열매 — 인접 아군 주문력 +N(flat)
    public float adjacentAllyManaBonus;         // 라즈열매 — 인접 아군에게 마나 N 즉시 지급(BattleManager.GainMana 재사용)

    public bool  burnNearOnPhysHit;     // burnNearOnPhysHit — 물리 피격 시 주변 적 화상
    public bool  ccImmune;              // ccImmune — 첫 CC 무효화

    [Header("에셋 참조")]
    public Sprite icon;
}
