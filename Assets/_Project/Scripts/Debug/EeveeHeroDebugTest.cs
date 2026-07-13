#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 이브이 영웅증강 디버그. 증강 시스템(해인) 연결 전까지 에디터 검증용.
/// ⓪ 이브이 1마리를 벤치에 무료 지급 (상점 등장 확률에 기대지 않고 검증용 수급)
/// ① 보드/벤치의 이브이 전부에 ApplyEeveeHeroAugment(진화잠금+×1.4)
/// ② 진화잠금된 이브이를 3성으로 강제 별업 → 다음 전투에서 봇 전원소환(4마리) 트리거 확인
/// (자연 경로 검증은 이브이 9마리 3합 — 진화잠금이라 종 스왑 없이 별만 올라야 정상)
/// ③ 파치리스 1마리를 벤치에 무료 지급
/// ④ 파치리스 전부에 ApplyParichisuHeroAugment(탱커 전환 + 날따름 주입) → 전투에서 도발/복귀 검증
/// </summary>
public class EeveeHeroDebugTest : MonoBehaviour
{
    // 날따름 마나비용 — PLACEHOLDER(기획 미확정). 스킬 스펙 자체는 기획 확정(2026-07-10): ENEMY_AREA r4.
    // 검증 편의상 30(전투 시작 ~3초 시전). 기획 확정 시 교체.
    private const int PACHIRISU_TAUNT_MANA_COST = 30;

    private string _lastResult = "";
    private PokemonData _eeveeData;
    private PokemonData _pachirisuData;

    private void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Board == null) return;

        float x = 300f, y = 570f;   // GoldTransferDebugTest(x=300, y=460) 아래
        GUI.Label(new Rect(x, y, 320, 22), "이브이 영웅증강 디버그:");
        y += 24f;

        if (GUI.Button(new Rect(x, y, 220, 24), "⓪ 이브이 벤치 지급"))
            _lastResult = GiveEeveeToBench(gm);
        y += 26f;

        if (GUI.Button(new Rect(x, y, 220, 24), "① 이브이 전부 영웅증강 적용"))
            _lastResult = ApplyAugmentToAllEevees(gm);
        y += 26f;

        if (GUI.Button(new Rect(x, y, 220, 24), "② 진화잠금 이브이 3성 강제"))
            _lastResult = ForceLockedEeveesToThreeStar(gm);
        y += 26f;

        if (GUI.Button(new Rect(x, y, 220, 24), "③ 파치리스 벤치 지급"))
            _lastResult = GivePachirisuToBench(gm);
        y += 26f;

        if (GUI.Button(new Rect(x, y, 220, 24), "④ 파치리스 도발 증강 적용"))
            _lastResult = ApplyAugmentToAllPachirisu(gm);
        y += 26f;

        if (GUI.Button(new Rect(x, y, 220, 24), "⑤ 보드 유닛 스킬 상태 로그"))
            _lastResult = LogBoardSkillStates(gm);
        y += 26f;

        if (!string.IsNullOrEmpty(_lastResult))
            GUI.Label(new Rect(x, y, 320, 22), _lastResult);
    }

    private string GiveEeveeToBench(GameManager gm)
    {
        if (_eeveeData == null)
            _eeveeData = FindPokemonData("Eevee");
        return GiveToBench(gm, _eeveeData, "이브이");
    }

    private string GivePachirisuToBench(GameManager gm)
    {
        if (_pachirisuData == null)
            _pachirisuData = FindPokemonData("Pachirisu");
        return GiveToBench(gm, _pachirisuData, "파치리스");
    }

    private static string GiveToBench(GameManager gm, PokemonData data, string label)
    {
        if (data == null)
            return $"{label} PokemonData 에셋을 못 찾음";

        if (!gm.Board.HasBenchSpace())
            return "벤치가 가득 참";

        PokemonUnit unit = UnitFactory.Create(data);
        if (unit == null || !gm.Board.TryPlaceInBench(unit))
        {
            if (unit != null) Destroy(unit.gameObject);
            return "벤치 배치 실패";
        }
        return $"{label} 지급 완료 (상점 풀 미차감)";
    }

    private static PokemonData FindPokemonData(string nameEn)
    {
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:PokemonData"))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<PokemonData>(path);
            if (data != null &&
                string.Equals(data.pokemonNameEn, nameEn, System.StringComparison.OrdinalIgnoreCase))
                return data;
        }
        return null;
    }

    private static string ApplyAugmentToAllEevees(GameManager gm)
    {
        int applied = 0;
        foreach (var unit in AllOwnedUnits(gm))
        {
            if (!IsEevee(unit) || unit.evolutionLocked) continue;
            unit.ApplyEeveeHeroAugment();
            applied++;
        }
        return applied > 0 ? $"영웅증강 적용: {applied}마리" : "대상 이브이 없음(미적용분)";
    }

    private static string ForceLockedEeveesToThreeStar(GameManager gm)
    {
        int forced = 0;
        foreach (var unit in AllOwnedUnits(gm))
        {
            if (!IsEevee(unit) || !unit.evolutionLocked || unit.starLevel >= 3) continue;
            unit.starLevel = 3;
            GameEvents.UnitChanged(unit);
            forced++;
        }
        return forced > 0 ? $"3성 강제: {forced}마리 → 다음 전투에서 봇 4마리 확인"
                          : "진화잠금 이브이 없음(① 먼저)";
    }

    private static string ApplyAugmentToAllPachirisu(GameManager gm)
    {
        int applied = 0, already = 0, seen = 0;
        foreach (var unit in AllOwnedUnits(gm))
        {
            if (!IsSpecies(unit, "Pachirisu")) continue;
            seen++;
            if (unit.HasGrantedSkill) { already++; continue; }
            unit.ApplyParichisuHeroAugment(PokemonRole.Tanker, CreateTauntSkill(), PACHIRISU_TAUNT_MANA_COST);
            applied++;
        }
        string msg = $"도발 증강 — 파치리스 {seen}마리 발견, 신규 적용 {applied}, 기적용 {already}";
        Debug.Log($"[PachirisuDebug] {msg}");
        return msg;
    }

    /// <summary>보드/벤치 전 유닛의 스킬 상태를 콘솔에 덤프 — 증강 주입이 전투 스냅샷에 반영될지 사전 확인용.</summary>
    private static string LogBoardSkillStates(GameManager gm)
    {
        int count = 0;
        foreach (var unit in AllOwnedUnits(gm))
        {
            var skill = unit.EffectiveSkill;
            Debug.Log($"[PachirisuDebug] {(unit.isOnBoard ? "보드" : "벤치")} {unit.data?.pokemonName}({unit.data?.pokemonNameEn}) {unit.starLevel}성" +
                      $" | role={unit.Role} | granted={unit.HasGrantedSkill}" +
                      $" | skill={(skill != null && skill.HasSkill ? $"{skill.skillId}/{skill.effectType}/r{skill.areaRadius}" : "없음")}" +
                      $" | manaCost={unit.EffectiveManaCost}");
            count++;
        }
        return $"유닛 {count}개 상태를 콘솔에 출력함";
    }

    /// <summary>날따름 스킬 정의(기획 확정 2026-07-10: ENEMY_AREA r4, 시전자 중심). 증강 시스템(해인) 연결 전 임시 인라인.</summary>
    private static PokemonSkillData CreateTauntSkill() => new PokemonSkillData
    {
        skillId    = "PACHIRISU_TAUNT",
        skillName  = "날따름",
        effectType = SkillEffectType.Taunt,
        targetType = SkillTargetType.EnemyArea,
        areaRadius = 4,
        vfxId      = ""
    };

    private static System.Collections.Generic.IEnumerable<PokemonUnit> AllOwnedUnits(GameManager gm)
    {
        foreach (var unit in gm.Board.GetUnitsOnBoard())
            if (unit != null) yield return unit;
        foreach (var unit in gm.Board.GetUnitsInBench())
            if (unit != null) yield return unit;
    }

    private static bool IsEevee(PokemonUnit unit) => IsSpecies(unit, "Eevee");

    private static bool IsSpecies(PokemonUnit unit, string nameEn)
        => unit != null && unit.data != null &&
           string.Equals(unit.data.pokemonNameEn, nameEn, System.StringComparison.OrdinalIgnoreCase);
}
#endif
