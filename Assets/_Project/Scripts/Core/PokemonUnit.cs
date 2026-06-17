using UnityEngine;

/// <summary>
/// 보드/벤치 위의 포켓몬 유닛 런타임 상태.
/// 스탯 원본은 PokemonData(ScriptableObject)에 있고, 이 클래스는
/// 별(star) 강화를 반영한 "유효 스탯"을 계산해 노출하며 전투 중 변하는 값만 들고 있음.
/// 전투 시뮬레이션은 BattleManager가 이 값을 스냅샷(복사)해서 돌리므로,
/// 이 인스턴스(원본)는 전투 중 변경되지 않음.
/// </summary>
public class PokemonUnit : MonoBehaviour
{
    [Header("데이터")]
    public PokemonData data;

    [Header("런타임 스탯")]
    public float currentHp;
    public float currentMana;

    [Range(1, 3)]
    public int starLevel = 1;       // 1~3성

    [Header("상태")]
    public bool isOnBoard;          // false = 벤치

    // ──────────────────────────────────────────
    // 별 강화 스케일링
    // ──────────────────────────────────────────
    // TFT 표준: 성이 오를 때마다 약 1.8배. (2성=1.8x, 3성=1.8x1.8=3.24x)
    // HP/공격/특수공격에만 적용. 방어/특수방어/공속/사거리는 성과 무관(원본 그대로).
    // 인덱스 = starLevel - 1.
    private static readonly float[] STAR_MULTIPLIER = { 1f, 1.8f, 3.24f };

    /// <summary>지정 별 등급의 스탯 배수. (BattleManager가 적 유닛 스탯 계산에 재사용)</summary>
    public static float StarMultiplierFor(int starLevel)
    {
        int idx = Mathf.Clamp(starLevel - 1, 0, STAR_MULTIPLIER.Length - 1);
        return STAR_MULTIPLIER[idx];
    }

    /// <summary>현재 별 등급의 스탯 배수.</summary>
    public float StarMultiplier => StarMultiplierFor(starLevel);

    // ──────────────────────────────────────────
    // 유효 스탯 (별 강화 반영) — 전투/스냅샷이 읽는 진짜 값
    // ──────────────────────────────────────────

    public float MaxHp           => data != null ? data.hp * StarMultiplier : 0f;
    public float Attack          => data != null ? data.attack * StarMultiplier : 0f;
    public float SpecialAttack   => data != null ? data.specialAttack * StarMultiplier : 0f;
    public float Defense         => data != null ? data.defense : 0f;
    public float SpecialDefense  => data != null ? data.specialDefense : 0f;
    public float AttackSpeed     => data != null ? data.attackSpeed : 0f;
    public int   Range           => data != null ? data.range : 0;
    public AttackType AttackType => data != null ? data.attackType : AttackType.Physical;

    private void Start()
    {
        ResetForBattle();
    }

    /// <summary>
    /// 전투 시작/라운드 진입 시 HP를 가득 채우고 마나를 비움.
    /// (TFT 표준: 매 라운드 풀회복)
    /// </summary>
    public void ResetForBattle()
    {
        currentHp = MaxHp;
        currentMana = 0f;
    }
}
