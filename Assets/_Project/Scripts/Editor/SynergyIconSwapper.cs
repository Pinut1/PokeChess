using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SynergyData.icon을 흰색 심볼 시트(synergySymbol_white)의 같은 인덱스 스프라이트로 교체한다.
/// 심볼을 흰색으로 제작해 두면 SynergyRowUI가 Image.color 곱셈으로 등급별 색(검정 등)을 낼 수 있다.
///
/// 매칭은 스프라이트 이름 끝의 인덱스로 한다 — "synergySymbol_7" → "synergySymbol_white_7".
/// 두 시트의 슬라이스 순서가 같다는 전제(2026-07-28 해인님 확인)이며, 순서가 어긋나면
/// 인덱스가 그대로 밀리므로 시트를 다시 자를 때는 이 툴을 다시 돌리기 전에 순서를 재확인해야 한다.
/// </summary>
public static class SynergyIconSwapper
{
    private const string WhiteSheetPath = "Assets/Art/UI/Synergy/synergySymbol_white.png";
    private const string SynergyFolder = "Assets/_Project/ScriptableObjects/Synergies";

    [MenuItem("PokeChess/Swap Synergy Icons To White")]
    public static void SwapToWhite()
    {
        // 흰색 시트의 서브스프라이트를 끝 인덱스로 색인
        var whiteByIndex = new Dictionary<int, Sprite>();
        var suffix = new Regex(@"_(\d+)$");

        foreach (var obj in AssetDatabase.LoadAllAssetRepresentationsAtPath(WhiteSheetPath))
        {
            if (!(obj is Sprite sprite)) continue;

            var m = suffix.Match(sprite.name);
            if (m.Success) whiteByIndex[int.Parse(m.Groups[1].Value)] = sprite;
        }

        if (whiteByIndex.Count == 0)
        {
            Debug.LogError($"[SynergyIconSwapper] {WhiteSheetPath} 에서 서브스프라이트를 찾지 못했습니다 — " +
                            "Sprite Mode가 Multiple인지, 슬라이스가 적용됐는지 확인하세요.");
            return;
        }

        int swapped = 0, skippedNoIcon = 0, skippedAlready = 0, failed = 0;

        foreach (var guid in AssetDatabase.FindAssets("t:SynergyData", new[] { SynergyFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<SynergyData>(path);
            if (data == null) continue;

            if (data.icon == null)
            {
                Debug.LogWarning($"[SynergyIconSwapper] {data.name} — icon이 비어 있어 건너뜀(먼저 컬러 시트로 배정 필요)");
                skippedNoIcon++;
                continue;
            }

            // 이미 흰색 시트를 쓰고 있으면 그대로 둔다(재실행 안전).
            if (AssetDatabase.GetAssetPath(data.icon) == WhiteSheetPath)
            {
                skippedAlready++;
                continue;
            }

            var m = suffix.Match(data.icon.name);
            if (!m.Success)
            {
                Debug.LogWarning($"[SynergyIconSwapper] {data.name} — icon 이름 \"{data.icon.name}\"에 인덱스 접미가 없어 매칭 불가");
                failed++;
                continue;
            }

            int index = int.Parse(m.Groups[1].Value);
            if (!whiteByIndex.TryGetValue(index, out var white))
            {
                Debug.LogWarning($"[SynergyIconSwapper] {data.name} — 흰색 시트에 인덱스 {index}가 없음");
                failed++;
                continue;
            }

            Debug.Log($"[SynergyIconSwapper] {data.synergyName}({data.name}): {data.icon.name} → {white.name}");
            data.icon = white;
            EditorUtility.SetDirty(data);
            swapped++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[SynergyIconSwapper] 교체 {swapped} / 이미 흰색 {skippedAlready} / icon 없음 {skippedNoIcon} / 실패 {failed}");
    }
}
