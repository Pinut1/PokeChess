using UnityEngine;

/// <summary>
/// 영웅증강 공통 골격(이브이/파치리스). 선택 시:
///   ① 대상 종 1마리 벤치 즉시지급(무료, 상점 풀 미차감 — 보상 지급과 동일 규칙)
///   ② 무료 리롤 3개 (기획 "전용리롤 3"은 대상 종만 나오는 전용 상점 리롤로 추정 —
///      전용 상점 미구현이라 일반 무료 리롤로 PLACEHOLDER. TODO: 기획 확인 후 교체)
///   ③ 보유 중인 대상 종 전부에 효과 태그 + 이후 획득분(구매/보상/합체)도 배치/벤치 훅에서 자동 태그
/// </summary>
public abstract class HeroAugment : Augment
{
    private const int HERO_REROLL_COUNT = 3; // PLACEHOLDER — "전용리롤 3" 해석 확정 대기

    /// <summary>대상 종 영문명(PokemonData.pokemonNameEn 기준).</summary>
    protected abstract string SpeciesNameEn { get; }

    /// <summary>대상 유닛 1기에 영웅증강 효과 적용(이미 적용된 유닛은 스킵 처리 포함).</summary>
    protected abstract void Tag(PokemonUnit unit);

    public override void Apply()
    {
        GrantUnitToBench();
        GameManager.Instance.Shop?.AddReroll(HERO_REROLL_COUNT);
        TagAllOwned();
    }

    public override void OnUnitPlaced(PokemonUnit unit)  => TagIfTarget(unit);
    public override void OnUnitBenched(PokemonUnit unit) => TagIfTarget(unit);

    private void TagIfTarget(PokemonUnit unit)
    {
        if (IsTarget(unit)) Tag(unit);
    }

    private bool IsTarget(PokemonUnit unit)
        => unit != null && unit.data != null &&
           string.Equals(unit.data.pokemonNameEn, SpeciesNameEn, System.StringComparison.OrdinalIgnoreCase);

    private void TagAllOwned()
    {
        var board = GameManager.Instance.Board;
        if (board == null) return;

        int tagged = 0;
        foreach (var unit in board.GetUnitsOnBoard())
            if (IsTarget(unit)) { Tag(unit); tagged++; }
        foreach (var unit in board.GetUnitsInBench())
            if (IsTarget(unit)) { Tag(unit); tagged++; }

        Debug.Log($"[Augment] {SpeciesNameEn} 영웅증강 — 보유분 {tagged}마리 태그");
    }

    /// <summary>대상 종 1마리를 벤치에 무료 지급(보상 지급과 동일 경로 — 골드/풀 미차감).</summary>
    private void GrantUnitToBench()
    {
        var db = PokemonDatabase.Instance;
        PokemonData data = db != null ? db.GetByNameEn(SpeciesNameEn) : null;
        if (data == null)
        {
            Debug.LogWarning($"[Augment] '{SpeciesNameEn}' PokemonDatabase에 없음 — 즉시지급 실패");
            return;
        }

        var board = GameManager.Instance.Board;
        if (board == null || !board.HasBenchSpace())
        {
            Debug.LogWarning($"[Augment] 벤치 가득 — {SpeciesNameEn} 즉시지급 유실");
            return;
        }

        PokemonUnit unit = UnitFactory.Create(data);
        if (unit == null) return;

        if (!board.TryPlaceInBench(unit))
        {
            Object.Destroy(unit.gameObject); // 방어적 정리(HasBenchSpace 통과 후 실패는 이론상 도달 안 함)
            return;
        }

        Tag(unit); // OnUnitBenched 훅으로도 태그되지만 Apply() 중 활성 목록 편입 전이라 직접 보장
        Debug.Log($"[Augment] {SpeciesNameEn} 1마리 벤치 즉시지급");
    }
}
