#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 이브이 영웅증강 디버그. 증강 시스템(해인) 연결 전까지 에디터 검증용.
/// ① 보드/벤치의 이브이 전부에 ApplyEeveeHeroAugment(진화잠금+×1.4)
/// ② 진화잠금된 이브이를 3성으로 강제 별업 → 다음 전투에서 봇 전원소환(4마리) 트리거 확인
/// (자연 경로 검증은 이브이 9마리 3합 — 진화잠금이라 종 스왑 없이 별만 올라야 정상)
/// </summary>
public class EeveeHeroDebugTest : MonoBehaviour
{
    private string _lastResult = "";

    private void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Board == null) return;

        float x = 300f, y = 570f;   // GoldTransferDebugTest(x=300, y=460) 아래
        GUI.Label(new Rect(x, y, 320, 22), "이브이 영웅증강 디버그:");
        y += 24f;

        if (GUI.Button(new Rect(x, y, 220, 24), "① 이브이 전부 영웅증강 적용"))
            _lastResult = ApplyAugmentToAllEevees(gm);
        y += 26f;

        if (GUI.Button(new Rect(x, y, 220, 24), "② 진화잠금 이브이 3성 강제"))
            _lastResult = ForceLockedEeveesToThreeStar(gm);
        y += 26f;

        if (!string.IsNullOrEmpty(_lastResult))
            GUI.Label(new Rect(x, y, 320, 22), _lastResult);
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

    private static System.Collections.Generic.IEnumerable<PokemonUnit> AllOwnedUnits(GameManager gm)
    {
        foreach (var unit in gm.Board.GetUnitsOnBoard())
            if (unit != null) yield return unit;
        foreach (var unit in gm.Board.GetUnitsInBench())
            if (unit != null) yield return unit;
    }

    private static bool IsEevee(PokemonUnit unit)
        => unit != null && unit.data != null &&
           string.Equals(unit.data.pokemonNameEn, "Eevee", System.StringComparison.OrdinalIgnoreCase);
}
#endif
