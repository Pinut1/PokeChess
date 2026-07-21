#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 밸런스 시트(PokeChess_Balance_Tool.xlsx "1v1 자동전투 시뮬레이터") 기대값을
/// 실제 BattleManager 계산과 대조하는 디버그 하네스.
///
/// 지금까지 엑셀에서 눈으로 확인하던 걸 씬에서 버튼 한 번으로 판정한다.
/// 공식을 여기에 복사해두면 검증 의미가 없으므로 BattleManager.Mitigation/CritFactor를
/// 직접 호출한다 — 코드가 시트와 어긋나면 여기서 FAIL이 뜬다.
///
/// 범위: 경감계수·평타 DPS·TTK·승자까지. 스킬 DPS는 마나 충전/시전 주기가 얽혀 있어
/// 이 하네스에서는 시트 값을 그대로 참고치로만 표시한다(미검증 항목으로 명시).
/// </summary>
public class BalanceCheckHarness : MonoBehaviour
{
    /// <summary>시트 1v1 시트에 입력된 유닛 스탯 + 기대 결과.</summary>
    private struct SheetCase
    {
        public string aName, bName;
        public float aHp, aAtk, aDef, aAtkSpeed;
        public float bHp, bAtk, bDef, bAtkSpeed;

        public float expectedAMitigation;  // A가 B를 때릴 때 경감계수
        public float expectedBMitigation;
        public float expectedABasicDps;
        public float expectedBBasicDps;
        public string expectedWinner;      // 스킬 포함 최종 결과(참고치)
        public float expectedWinnerHp;
    }

    // 출처: PokeChess_Balance_Tool.xlsx — 1v1 자동전투 시뮬레이터 시트 (2026-07-14 기준)
    private static readonly SheetCase[] Cases =
    {
        new SheetCase
        {
            aName = "꼬부기", aHp = 950f, aAtk = 25f, aDef = 45f, aAtkSpeed = 0.50f,
            bName = "파이리", bHp = 650f, bAtk = 30f, bDef = 30f, bAtkSpeed = 0.40f,
            expectedAMitigation = 0.77f,
            expectedBMitigation = 0.69f,
            expectedABasicDps   = 9.6f,
            expectedBBasicDps   = 8.3f,
            expectedWinner      = "파이리",
            expectedWinnerHp    = 354f,
        },
    };

    private const float MitigationTolerance = 0.005f; // 시트가 소수 2자리 반올림이라 그만큼 허용
    private const float DpsTolerance        = 0.05f;

    private readonly List<string> _lines = new();
    private bool _allPassed;
    private bool _hasRun;
    private Vector2 _scroll;

    private void RunSheetComparison()
    {
        _lines.Clear();
        _allPassed = true;
        _hasRun = true;

        foreach (var c in Cases)
        {
            _lines.Add($"◆ {c.aName} vs {c.bName}");

            // A가 B를 때릴 때는 B의 방어로 경감된다
            float actualAMitigation = BattleManager.Mitigation(c.bDef);
            float actualBMitigation = BattleManager.Mitigation(c.aDef);

            Check($"  {c.aName} 경감계수", actualAMitigation, c.expectedAMitigation, MitigationTolerance);
            Check($"  {c.bName} 경감계수", actualBMitigation, c.expectedBMitigation, MitigationTolerance);

            float actualABasicDps = c.aAtk * c.aAtkSpeed * actualAMitigation;
            float actualBBasicDps = c.bAtk * c.bAtkSpeed * actualBMitigation;

            Check($"  {c.aName} 평타 DPS", actualABasicDps, c.expectedABasicDps, DpsTolerance);
            Check($"  {c.bName} 평타 DPS", actualBBasicDps, c.expectedBBasicDps, DpsTolerance);

            // 평타만 놓고 본 TTK — 스킬 미포함이라 시트 최종 승자와 다를 수 있다(참고용)
            float aTtk = actualABasicDps > 0f ? c.bHp / actualABasicDps : float.PositiveInfinity;
            float bTtk = actualBBasicDps > 0f ? c.aHp / actualBBasicDps : float.PositiveInfinity;
            _lines.Add($"  · 평타만 TTK: {c.aName}→{aTtk:F1}초 / {c.bName}→{bTtk:F1}초 (참고)");
            _lines.Add($"  · 시트 최종 승자: {c.expectedWinner} (남은 HP {c.expectedWinnerHp:F0}) — 스킬 포함, 미검증");
        }
    }

    private void Check(string label, float actual, float expected, float tolerance)
    {
        bool ok = Mathf.Abs(actual - expected) <= tolerance;
        if (!ok) _allPassed = false;
        _lines.Add($"{label}: {actual:F3} [시트 {expected:F2}] {(ok ? "PASS" : "FAIL")}");
    }

    private void OnGUI()
    {
        const float w = 430f;
        float x = 10f, y = 10f;

        GUI.Box(new Rect(x - 5f, y - 5f, w + 10f, 320f), GUIContent.none);

        GUI.Label(new Rect(x, y, w, 22f), "밸런스 시트 대조 (PokeChess_Balance_Tool.xlsx)");
        y += 24f;

        if (GUI.Button(new Rect(x, y, 150f, 24f), "공식 대조 실행"))
            RunSheetComparison();
        y += 28f;

        if (_hasRun)
        {
            GUI.color = _allPassed ? Color.green : Color.red;
            GUI.Label(new Rect(x, y, w, 22f), _allPassed ? "전체 PASS" : "FAIL 있음 — 코드와 시트가 어긋났습니다");
            GUI.color = Color.white;
            y += 24f;

            _scroll = GUI.BeginScrollView(new Rect(x, y, w, 120f), _scroll, new Rect(0, 0, w - 20f, _lines.Count * 18f));
            for (int i = 0; i < _lines.Count; i++)
                GUI.Label(new Rect(0f, i * 18f, w - 20f, 18f), _lines[i]);
            GUI.EndScrollView();
            y += 126f;
        }

        DrawSynergySection(x, ref y, w);
        DrawLiveUnitSection(x, ref y, w);
    }

    /// <summary>현재 보드의 시너지 활성 단계 — 실제 SynergyManager 상태를 그대로 읽는다.</summary>
    private void DrawSynergySection(float x, ref float y, float w)
    {
        var gm = GameManager.Instance;
        var synergy = gm != null ? gm.Synergy : null;

        GUI.Label(new Rect(x, y, w, 22f), "── 시너지 활성 현황");
        y += 22f;

        if (synergy == null)
        {
            GUI.Label(new Rect(x, y, w, 20f), "  SynergyManager 없음 (씬 확인)");
            y += 20f;
            return;
        }

        var active = synergy.GetActiveSynergies();
        if (active == null || active.Count == 0)
        {
            GUI.Label(new Rect(x, y, w, 20f), "  활성 시너지 없음");
            y += 20f;
            return;
        }

        foreach (var s in active)
        {
            string name = s.data != null ? s.data.name : "?";
            GUI.Label(new Rect(x, y, w, 20f),
                $"  {name}: {s.uniqueCount}종 → 티어 {s.activeTierIndex + 1}단계");
            y += 20f;
        }
    }

    /// <summary>전투 중 유닛의 실측 스탯 — 시너지/아이템 버프가 실제로 반영됐는지 확인용.</summary>
    private void DrawLiveUnitSection(float x, ref float y, float w)
    {
        var gm = GameManager.Instance;
        var battle = gm != null ? gm.Battle : null;

        GUI.Label(new Rect(x, y, w, 22f), "── 전장 유닛 실측 (전투 중에만 표시)");
        y += 22f;

        if (battle == null || battle.Units == null || battle.Units.Count == 0)
        {
            GUI.Label(new Rect(x, y, w, 20f), "  전투 중이 아님");
            return;
        }

        int shown = 0;
        foreach (var bu in battle.Units)
        {
            if (bu == null || shown >= 6) break;

            GUI.Label(new Rect(x, y, w, 20f),
                $"  [{bu.team}] HP {bu.currentHp:F0}/{bu.maxHp:F0} " +
                $"ATK {bu.attack:F0} DEF {bu.defense:F0} AS {bu.attackSpeed:F2} " +
                $"경감 {BattleManager.Mitigation(bu.defense):F3} 크리× {BattleManager.CritFactor(bu):F2}");
            y += 20f;
            shown++;
        }
    }
}
#endif
