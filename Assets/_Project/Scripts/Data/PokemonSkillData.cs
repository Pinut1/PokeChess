using System;
using UnityEngine;

/// <summary>
/// 스킬 효과 종류(Skill Table effectType). 시전 시 이 값으로 로직 분기.
/// 데미지/회복/보호막 수치는 데이터에 없고 시전자 스탯(spellPower, ATTACK은 attack)으로 계산.
/// Phase1 구현: Attack/Spell(데미지)만. 나머지(지원/CC)는 기획 수치 확정 후 Phase2.
/// </summary>
public enum SkillEffectType
{
    Attack,     // attack 스탯 기반 데미지 (타겟/범위/VFX만 Skill Table 참조)
    Spell,      // spellPower 기반 데미지
    HpRegen,    // spellPower 기반 회복
    Shield,     // spellPower 기반 보호막
    ManaRegen,  // 마나 충전 속도 버프
    AsBuff,     // 공격속도 증가 버프
    Slow,       // 공격속도 감소
    Stun,       // 행동 불능
    Taunt       // 어그로 강제
}

/// <summary>스킬 대상 선택 방식(Skill Table targetType).</summary>
public enum SkillTargetType
{
    AllySelf,     // 시전자 자신
    AllySingle,   // 아군 단일 (effectType별 우선순위 — HP_REGEN=최저HP, SHIELD=탱커)
    AllyArea,     // 시전자 기준 areaRadius 내 모든 아군
    EnemySingle,  // 현재 어그로 대상
    EnemyArea,    // 시전자 기준 areaRadius 내 모든 적
    EnemyLine     // 전방 직선 lineLength칸 내 모든 적
}

/// <summary>
/// 포켓몬이 사용하는 스킬 1개의 정의(Skill Table 한 행). 임포터가 skillId로 join해 PokemonData.skill에 베이킹.
/// 수치(데미지/회복/보호막)는 여기 없음 — 시전 시 시전자 spellPower(또는 Attack은 attack)로 계산.
/// 마나비용은 PokemonData.manaCost(포켓몬별)에 있음.
/// </summary>
[Serializable]
public class PokemonSkillData
{
    public string skillId;        // 참조 키 (예: "GRASS_MAGICIAN"). 빈 값이면 스킬 없음(평타만).
    public string skillName;

    [TextArea]
    public string description;

    public SkillEffectType effectType;
    public SkillTargetType targetType;

    /// <summary>targetType이 *_AREA 일 때 사용 (헥스 반경).</summary>
    public int areaRadius;

    /// <summary>targetType == EnemyLine 일 때 사용 (전방 칸 수).</summary>
    public int lineLength;

    public string vfxId;

    /// <summary>스킬 존재 여부(skillId 비어있으면 평타만).</summary>
    public bool HasSkill => !string.IsNullOrEmpty(skillId);
}
