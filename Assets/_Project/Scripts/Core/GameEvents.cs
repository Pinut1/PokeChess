using System;
using UnityEngine;

/// <summary>
/// 매니저 간 직접 참조 대신 이벤트로 통신.
/// 새 이벤트는 반드시 여기에만 추가할 것.
/// </summary>
public static class GameEvents
{
    // ──────────────────────────────────────────
    // 전투
    // ──────────────────────────────────────────

    /// <summary>전투 시작</summary>
    public static event Action OnBattleStart;

    /// <summary>전투 종료. true = 승, false = 패</summary>
    public static event Action<bool> OnBattleEnd;

    // ──────────────────────────────────────────
    // 골드 / 레벨
    // ──────────────────────────────────────────

    /// <summary>골드 변경. 인자 = 변경 후 골드 총량</summary>
    public static event Action<int> OnGoldChanged;

    /// <summary>플레이어 레벨 변경. 인자 = 변경 후 레벨</summary>
    public static event Action<int> OnLevelChanged;

    // ──────────────────────────────────────────
    // 유닛
    // ──────────────────────────────────────────

    /// <summary>유닛 보드에 배치됨</summary>
    public static event Action<PokemonUnit> OnUnitPlaced;

    /// <summary>유닛 벤치로 돌아옴</summary>
    public static event Action<PokemonUnit> OnUnitBenched;

    /// <summary>유닛 판매됨</summary>
    public static event Action<PokemonUnit> OnUnitSold;

    // ──────────────────────────────────────────
    // 샵
    // ──────────────────────────────────────────

    /// <summary>샵 리롤됨</summary>
    public static event Action OnShopRerolled;

    // ──────────────────────────────────────────
    // 라운드
    // ──────────────────────────────────────────

    /// <summary>라운드 번호 변경. 인자 = 새 라운드 번호</summary>
    public static event Action<int> OnRoundChanged;

    // ──────────────────────────────────────────
    // Invoke 헬퍼 (외부에서 직접 ?.Invoke 말고 여기 통해서 호출)
    // ──────────────────────────────────────────

    public static void BattleStart()           => OnBattleStart?.Invoke();
    public static void BattleEnd(bool isWin)   => OnBattleEnd?.Invoke(isWin);
    public static void GoldChanged(int amount) => OnGoldChanged?.Invoke(amount);
    public static void LevelChanged(int level) => OnLevelChanged?.Invoke(level);
    public static void UnitPlaced(PokemonUnit unit)  => OnUnitPlaced?.Invoke(unit);
    public static void UnitBenched(PokemonUnit unit) => OnUnitBenched?.Invoke(unit);
    public static void UnitSold(PokemonUnit unit)    => OnUnitSold?.Invoke(unit);
    public static void ShopRerolled()          => OnShopRerolled?.Invoke();
    public static void RoundChanged(int round) => OnRoundChanged?.Invoke(round);
}
