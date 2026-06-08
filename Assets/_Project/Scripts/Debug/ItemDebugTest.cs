#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class ItemDebugTest : MonoBehaviour
{
    private void OnGUI()
    {
        if (GUI.Button(new Rect(10, 10, 200, 50), "아이템 목록 출력"))
        {
            var guids = AssetDatabase.FindAssets("t:ItemData",
                new[] { "Assets/_Project/ScriptableObjects/Items" });

            if (guids.Length == 0)
            {
                Debug.LogWarning("[Item] SO 없음 — PokeChess/Import Item JSON 먼저 실행");
                return;
            }

            Debug.Log($"[Item] ───── 아이템 {guids.Length}종 ─────");
            foreach (var guid in guids)
            {
                var so = AssetDatabase.LoadAssetAtPath<ItemData>(
                             AssetDatabase.GUIDToAssetPath(guid));

                Debug.Log($"[{so.id}] {so.itemName} ({so.itemNameEn})\n" +
                          $"    설명: {so.description}\n" +
                          $"    {StatSummary(so)}");
            }
        }
    }

    private static string StatSummary(ItemData so)
    {
        var parts = new System.Collections.Generic.List<string>();

        if (so.maxHpPct           > 0) parts.Add($"maxHpPct={so.maxHpPct}");
        if (so.hpBonus            > 0) parts.Add($"hp={so.hpBonus}");
        if (so.hpRegenPercent     > 0) parts.Add($"hpRegen={so.hpRegenPercent}");
        if (so.healTakenDmgPct    > 0) parts.Add($"healTaken={so.healTakenDmgPct}");
        if (so.shieldPctOnFatalHit> 0) parts.Add($"shield={so.shieldPctOnFatalHit}");
        if (so.attackBonus        > 0) parts.Add($"atk={so.attackBonus}");
        if (so.spAtkPct           > 0) parts.Add($"spAtk={so.spAtkPct}");
        if (so.attackSpeedBonus   > 0) parts.Add($"atkSpd={so.attackSpeedBonus}");
        if (so.moveSpdPctOnKill   > 0) parts.Add($"moveSpdOnKill={so.moveSpdPctOnKill}");
        if (so.defenseBonus       > 0) parts.Add($"def={so.defenseBonus}");
        if (so.spDefBonus         > 0) parts.Add($"spDef={so.spDefBonus}");
        if (so.reflectPhysPct     > 0) parts.Add($"reflectPhys={so.reflectPhysPct}");
        if (so.reflectSpPct       > 0) parts.Add($"reflectSp={so.reflectSpPct}");
        if (so.defSpDefPerAttacker> 0) parts.Add($"defPerAttacker={so.defSpDefPerAttacker}");
        if (so.criPct             > 0) parts.Add($"cri={so.criPct}");
        if (so.criDmgPct          > 0) parts.Add($"criDmg={so.criDmgPct}");
        if (so.burnNearOnPhysHit)      parts.Add("burnNear=true");
        if (so.ccImmune)               parts.Add("ccImmune=true");

        return parts.Count > 0 ? string.Join(", ", parts) : "스탯 없음";
    }
}
#endif
