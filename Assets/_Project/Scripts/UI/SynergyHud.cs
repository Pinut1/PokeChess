using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 시너지 활성/비활성 현황 표시(IMGUI, 롤체 좌측 특성 패널 스타일).
/// 매 프레임 SynergyManager.GetAllSynergyStatuses()(카운트 1+ 전부)를 pull → 좌측 상단에 세로 목록.
/// 활성 티어는 동/은/금/프리즘 색으로 강조, 비활성은 회색 + "현재/다음단계" 진행 표시.
/// 행에 마우스를 올리면 단계별 효과 설명 툴팁 표시(롤체와 동일).
/// 정식 uGUI 특성 패널은 황해인(UI) 담당 — 이건 프로토 검증용.
/// </summary>
public class SynergyHud : MonoBehaviour
{
    private const float X = 10f, Y = 170f, W = 200f, ROW_H = 22f, PAD = 6f;

    // 티어별 색(롤체 동/은/금/프리즘 느낌).
    private static readonly Color[] TierColors =
    {
        new Color(0.80f, 0.52f, 0.30f), // tier1 동
        new Color(0.80f, 0.80f, 0.84f), // tier2 은
        new Color(0.95f, 0.82f, 0.30f), // tier3 금
        new Color(0.55f, 0.90f, 1.00f), // tier4 프리즘
    };
    private static readonly Color InactiveColor = new Color(0.55f, 0.55f, 0.55f);

    private GUIStyle _rowStyle;
    private GUIStyle _tooltipStyle;

    private void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Synergy == null) return;

        var source = gm.Synergy.GetAllSynergyStatuses();
        if (source == null || source.Count == 0) return;

        // IReadOnlyList → 정렬 가능한 List로 복사(원본 불변 유지).
        var statuses = new List<SynergyStatus>(source);

        // 활성 먼저, 그다음 카운트 많은 순(롤체 정렬과 동일 감각).
        statuses.Sort((a, b) =>
        {
            if (a.IsActive != b.IsActive) return b.IsActive.CompareTo(a.IsActive);
            return b.uniqueCount.CompareTo(a.uniqueCount);
        });

        _rowStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };

        float height = 26f + statuses.Count * ROW_H + PAD;
        GUILayout.BeginArea(new Rect(X, Y, W, height), GUI.skin.box);
        GUILayout.Label("시너지");

        foreach (var s in statuses)
        {
            if (s?.data == null) continue;

            _rowStyle.normal.textColor = s.IsActive
                ? TierColors[Mathf.Clamp(s.activeTierIndex, 0, TierColors.Length - 1)]
                : InactiveColor;

            string name = !string.IsNullOrEmpty(s.data.synergyName) ? s.data.synergyName : s.data.synergyNameEn;
            // GUIContent의 tooltip을 채워두면 마우스 호버 시 GUI.tooltip에 잡힘.
            var content = new GUIContent($"{(s.IsActive ? "●" : "○")} {name}  {Progress(s)}", BuildTooltip(s));
            GUILayout.Label(content, _rowStyle);
        }

        GUILayout.EndArea();

        DrawCheerleaderChoice(statuses, Y + height);
        DrawTooltip();
    }

    /// <summary>치어리더 활성 시 효과 선택(공속/마나) 토글을 시너지 패널 아래에 표시.</summary>
    private static void DrawCheerleaderChoice(List<SynergyStatus> statuses, float top)
    {
        bool active = false;
        foreach (var s in statuses)
            if (s?.data != null && s.IsActive &&
                string.Equals(s.data.synergyNameEn, "Cheerleader", System.StringComparison.OrdinalIgnoreCase))
            { active = true; break; }
        if (!active) return;

        GUILayout.BeginArea(new Rect(X, top + 4f, W, 56f), GUI.skin.box);
        GUILayout.Label("치어리더 효과 선택");
        var cur = CheerleaderChoice.Current;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(cur == CheerleaderChoice.Option.AttackSpeed ? "▶ 공속 +15%" : "공속 +15%"))
            CheerleaderChoice.Current = CheerleaderChoice.Option.AttackSpeed;
        if (GUILayout.Button(cur == CheerleaderChoice.Option.ManaRegen ? "▶ 마나 +30%" : "마나 +30%"))
            CheerleaderChoice.Current = CheerleaderChoice.Option.ManaRegen;
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    /// <summary>호버 중인 행의 GUI.tooltip을 커서 옆 박스로 그림(영역 밖에서 그려 클리핑 회피).</summary>
    private void DrawTooltip()
    {
        if (string.IsNullOrEmpty(GUI.tooltip)) return;

        _tooltipStyle ??= new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
            fontSize = 12,
            padding = new RectOffset(8, 8, 6, 6),
            richText = false
        };

        var content = new GUIContent(GUI.tooltip);
        const float w = 300f;
        float h = _tooltipStyle.CalcHeight(content, w);

        Vector2 m = Event.current.mousePosition;
        // 커서 오른쪽 아래 기본, 화면 밖이면 안쪽으로 클램프.
        float x = Mathf.Min(m.x + 16f, Screen.width  - w - 4f);
        float y = Mathf.Min(m.y + 8f,  Screen.height - h - 4f);
        GUI.Box(new Rect(x, y, w, h), content, _tooltipStyle);
    }

    /// <summary>비활성/하위티어면 "현재수/다음단계", 최고단계 충족이면 "현재수 (T단계)".</summary>
    private static string Progress(SynergyStatus s)
    {
        var tiers = s.data.tiers;
        if (tiers != null)
            for (int i = 0; i < tiers.Count; i++)
                if (s.uniqueCount < tiers[i].count)
                    return $"{s.uniqueCount}/{tiers[i].count}";
        return $"{s.uniqueCount} (T{s.activeTierIndex + 1})";
    }

    /// <summary>롤체식 툴팁: 시너지명 + 보유 수 + 단계별 효과(충족 단계는 ●).</summary>
    private static string BuildTooltip(SynergyStatus s)
    {
        string name = !string.IsNullOrEmpty(s.data.synergyName) ? s.data.synergyName : s.data.synergyNameEn;
        var sb = new StringBuilder();
        sb.Append(name).Append("  (보유 ").Append(s.uniqueCount).Append(')');

        var tiers = s.data.tiers;
        if (tiers != null)
            foreach (var t in tiers)
            {
                bool met = s.uniqueCount >= t.count;
                sb.Append('\n').Append(met ? "● " : "○ ").Append('(').Append(t.count).Append(") ")
                  .Append(string.IsNullOrEmpty(t.effectDescription) ? "-" : t.effectDescription);
            }
        return sb.ToString();
    }
}
