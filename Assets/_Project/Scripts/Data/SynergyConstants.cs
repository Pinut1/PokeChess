using System.Collections.Generic;

/// <summary>
/// 시너지 단계별 강화 수치. 기획 SynergyTable_Guide.md §6 기준(2026-06-25).
///
/// 기획 결정: 시너지 *수치*는 데이터(JSON)가 아니라 **코드 상수**로 관리한다
/// (statType/발동단계만 SynergyData에, 증가량은 여기). 밸런스 조정 = 이 표만 수정.
///
/// 값 의미: percent형은 0.1 = +10%(또는 보호막 비율), fixed형은 절대 가산값. tier는 1-base.
/// </summary>
public static class SynergyConstants
{
    // synergyId(대문자 영문, 대소문자 무관) → tier별 값 배열(인덱스 0 = tier1).
    private static readonly Dictionary<string, float[]> _values =
        new(System.StringComparer.OrdinalIgnoreCase)
    {
        // ── fixed (절대 가산) ──
        ["GRASS"]    = new[] { 0.1f, 0.2f, 0.3f, 0.4f },   // atkSpeed +고정
        ["POISON"]   = new[] { 15f,  30f,  50f,  70f  },   // defense  +고정
        ["ELECTRIC"] = new[] { 20f,  40f,  65f         },  // spellPower +고정
        ["BUG"]      = new[] { 10f,  20f,  35f         },  // attack   +고정

        // ── percent (비율) ──
        ["WATER"]    = new[] { 0.20f, 0.35f, 0.50f, 0.70f }, // shield = spellPower × 비율
        ["FIRE"]     = new[] { 0.10f, 0.25f, 0.40f },        // defense %
        ["FLYING"]   = new[] { 0.10f, 0.20f, 0.35f },        // atkSpeed %
        ["NORMAL"]   = new[] { 0.05f, 0.10f, 0.15f },        // 치명타 확률 +절대(critChance 가산)
        ["ETHEREAL"] = new[] { 0.20f, 0.40f, 0.60f },        // manaRegen %(충전속도 배수 가산)
        ["GROUND"]   = new[] { 0.15f, 0.30f, 0.50f },        // hp %
        ["BREAKER"]  = new[] { 0.10f, 0.20f, 0.35f },        // attack %
        ["DRAGON"]   = new[] { 0.20f, 0.40f },               // spellPower % (2026-07-30: 계열 카운트 전환으로 3티어→2티어)

        // special(MUTANT/DARK)·고유 디버프(ICE)·선택형(CHEERLEADER)은 수치 단순가산이 아니라
        // 전용 로직 → 여기 두지 않음(아래 상수만 참고용).
    };

    /// <summary>ICE: 적 atkSpeed 감소 비율(0.20 = -20%).</summary>
    public const float IceEnemyAtkSpeedReduction = 0.20f;
    /// <summary>DARK: 첫 스킬 사용 시 스턴 지속(초).</summary>
    public const float DarkFirstSkillStunSeconds = 1.5f;
    /// <summary>CHEERLEADER 선택지 수치.</summary>
    public const float CheerleaderAtkSpeedPct = 0.15f;
    public const float CheerleaderManaRegenPct = 0.30f;

    /// <summary>해당 시너지·tier(1-base)의 강화 수치. 미정의/범위 밖이면 0.</summary>
    public static float Value(string synergyId, int tier)
    {
        if (synergyId != null && _values.TryGetValue(synergyId, out var arr)
            && tier >= 1 && tier <= arr.Length)
            return arr[tier - 1];
        return 0f;
    }
}
