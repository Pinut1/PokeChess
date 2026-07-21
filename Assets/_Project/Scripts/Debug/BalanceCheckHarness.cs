#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 밸런스 시트(PokeChess_Balance_Tool.xlsx "1v1 자동전투 시뮬레이터") 기대값을
/// 실제 BattleManager.Mitigation 및 크리 미포함 시트 모델 DPS와 대조하는 디버그 하네스.
///
/// 지금까지 엑셀에서 눈으로 확인하던 걸 씬에서 버튼 한 번으로 판정한다.
///
/// 무엇을 어디서 가져오는지 (과장 없이):
/// - 경감계수: BattleManager.Mitigation을 직접 호출한다. 공식을 여기 복사해두면
///   코드가 바뀌어도 계속 PASS가 떠서 검증 의미가 없으므로 실제 함수를 부른다.
///   따라서 이 항목은 코드가 시트와 어긋나면 FAIL이 뜬다.
/// - 평타 DPS: 이 하네스가 ATK × AS × Mitigation으로 자체 계산한다. 시트의 DPS
///   모델을 재현한 것이지 BattleManager의 전투 경로를 그대로 태운 값이 아니다.
///   실제 전투는 효과(OnDealDamage) → CritFactor → Mitigation 순서로 처리되므로
///   효과와 크리가 빠져 있다. 즉 "시트 모델이 코드의 경감계수와 맞는지"를 보는 것이지
///   전투 파이프라인 전체를 검증하는 것이 아니다.
///
/// PASS/FAIL 검증 범위: 경감계수, 평타 DPS(위 단서 포함). 둘 다 시트에 기대값이 있다.
///
/// 검증하지 않는 것(시트에 근거가 없어 판정 불가):
/// - CritFactor: 시트 1v1·Data·상수 탭 어디에도 크리 항목이 없다(2026-07-14 기준 확인).
///   따라서 대조하지 않고, "전장 유닛 실측" 섹션에서 현재 값을 표시만 한다.
/// - 효과(ICombatEffect) 개입: 아이템/시너지가 OnDealDamage에서 amount를 가공하는
///   경로는 이 하네스가 재현하지 않는다.
/// - 스킬 DPS·총 DPS·승자: 마나 충전/시전 주기가 얽혀 있어 미모델링.
///   시트 값을 참고치로만 출력한다.
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

            // 시트의 DPS 모델(ATK × AS × 경감)을 재현한 값이다. BattleManager의 전투 경로
            // (효과 → CritFactor → Mitigation)를 그대로 태운 값이 아니므로 효과·크리가 빠져 있다.
            float actualABasicDps = c.aAtk * c.aAtkSpeed * actualAMitigation;
            float actualBBasicDps = c.bAtk * c.bAtkSpeed * actualBMitigation;

            Check($"  {c.aName} 평타 DPS", actualABasicDps, c.expectedABasicDps, DpsTolerance);
            Check($"  {c.bName} 평타 DPS", actualBBasicDps, c.expectedBBasicDps, DpsTolerance);

            // 평타만 놓고 본 TTK — 스킬 미포함이라 시트 최종 승자와 다를 수 있다(참고용)
            float aTtk = actualABasicDps > 0f ? c.bHp / actualABasicDps : float.PositiveInfinity;
            float bTtk = actualBBasicDps > 0f ? c.aHp / actualBBasicDps : float.PositiveInfinity;
            _lines.Add($"  · 평타만 TTK: {c.aName}→{aTtk:F1}초 / {c.bName}→{bTtk:F1}초 (참고, 미검증)");
            _lines.Add($"  · 시트 최종 승자: {c.expectedWinner} (남은 HP {c.expectedWinnerHp:F0}) — 스킬 포함, 미검증");
        }

        _lines.Add("");
        _lines.Add("※ PASS/FAIL 대상은 경감계수·평타 DPS뿐입니다.");
        _lines.Add("※ 경감계수는 BattleManager.Mitigation 직접 호출, 평타 DPS는 시트 모델 재현(효과·크리 제외).");
        _lines.Add("※ CritFactor는 시트에 크리 항목이 없어 대조하지 않습니다(아래 실측 섹션에 표시만).");
    }

    private void Check(string label, float actual, float expected, float tolerance)
    {
        bool ok = Mathf.Abs(actual - expected) <= tolerance;
        if (!ok) _allPassed = false;
        _lines.Add($"{label}: {actual:F3} [시트 {expected:F2}] {(ok ? "PASS" : "FAIL")}");
    }

    /// <summary>기존 하네스 배치: TradeDebugTest(10,460) / GoldTransfer(300,460) / EeveeHero(300,570).
    /// 겹치지 않도록 우측 상단을 쓰고, 목록은 아래로 무한히 자라지 않게 상한을 둔다.</summary>
    private const int MaxSynergyLines = 6;
    private const int MaxUnitLines    = 6;

    private const float LineH       = 20f;
    private const float HeaderH     = 22f;
    private const float TitleH      = 24f;
    private const float ButtonH     = 28f;
    private const float VerdictH    = 24f;
    private const float ScrollMaxH  = 120f;
    private const float ScrollMinH  = 36f;

    private void OnGUI()
    {
        const float w = 430f;
        float x = Screen.width - w - 20f;
        const float top = 10f;

        // 배경 박스는 내용보다 먼저 그려야 뒤에 깔리므로, 높이를 미리 계산한다.
        // 고정 높이로 두면 시너지/유닛 줄 수에 따라 내용이 박스 밖으로 삐져나온다.
        float contentH = MeasureContentHeight();
        GUI.Box(new Rect(x - 5f, top - 5f, w + 10f, contentH + 10f), GUIContent.none);

        float y = top;

        GUI.Label(new Rect(x, y, w, HeaderH), "밸런스 시트 대조 (PokeChess_Balance_Tool.xlsx)");
        y += TitleH;

        if (GUI.Button(new Rect(x, y, 150f, ButtonH - 4f), "공식 대조 실행"))
            RunSheetComparison();
        y += ButtonH;

        if (_hasRun)
        {
            GUI.color = _allPassed ? Color.green : Color.red;
            GUI.Label(new Rect(x, y, w, HeaderH), _allPassed ? "전체 PASS" : "FAIL 있음 — 코드와 시트가 어긋났습니다");
            GUI.color = Color.white;
            y += VerdictH;

            float scrollH = ScrollHeight();
            _scroll = GUI.BeginScrollView(new Rect(x, y, w, scrollH), _scroll,
                                          new Rect(0f, 0f, w - 20f, _lines.Count * 18f));
            for (int i = 0; i < _lines.Count; i++)
                GUI.Label(new Rect(0f, i * 18f, w - 20f, 18f), _lines[i]);
            GUI.EndScrollView();
            y += scrollH + 6f;
        }

        DrawSynergySection(x, ref y, w);
        DrawLiveUnitSection(x, ref y, w);
    }

    private float ScrollHeight()
    {
        return Mathf.Clamp(_lines.Count * 18f, ScrollMinH, ScrollMaxH);
    }

    /// <summary>배경 박스 높이용 사전 측정. 아래 Draw*와 줄 수 계산이 반드시 일치해야 한다.</summary>
    private float MeasureContentHeight()
    {
        float h = TitleH + ButtonH;
        if (_hasRun) h += VerdictH + ScrollHeight() + 6f;
        h += HeaderH + SynergyLineCount() * LineH;
        h += HeaderH + UnitLineCount() * LineH;
        return h;
    }

    private int SynergyLineCount()
    {
        var gm = GameManager.Instance;
        var synergy = gm != null ? gm.Synergy : null;
        if (synergy == null) return 1;                       // "SynergyManager 없음"

        var active = synergy.GetActiveSynergies();
        if (active == null || active.Count == 0) return 1;   // "활성 시너지 없음"

        int shown = Mathf.Min(active.Count, MaxSynergyLines);
        return shown + (active.Count > MaxSynergyLines ? 1 : 0); // "… 외 N개"
    }

    private int UnitLineCount()
    {
        var gm = GameManager.Instance;
        var battle = gm != null ? gm.Battle : null;
        if (battle == null || battle.Units == null || battle.Units.Count == 0) return 1; // "전투 중이 아님"

        return Mathf.Min(battle.Units.Count, MaxUnitLines);
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

        int shownSynergy = 0;
        foreach (var s in active)
        {
            if (shownSynergy >= MaxSynergyLines) break;

            string name = s.data != null ? s.data.name : "?";
            GUI.Label(new Rect(x, y, w, 20f),
                $"  {name}: {s.uniqueCount}종 → 티어 {s.activeTierIndex + 1}단계");
            y += 20f;
            shownSynergy++;
        }

        if (active.Count > MaxSynergyLines)
        {
            GUI.Label(new Rect(x, y, w, 20f), $"  … 외 {active.Count - MaxSynergyLines}개");
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
            GUI.Label(new Rect(x, y, w, LineH), "  전투 중이 아님");
            y += LineH; // 마지막 섹션이라 지금은 무의미하지만, 섹션이 추가되면 어긋나므로 맞춰둔다
            return;
        }

        int shown = 0;
        foreach (var bu in battle.Units)
        {
            if (bu == null || shown >= MaxUnitLines) break;

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
