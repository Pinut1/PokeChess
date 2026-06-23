#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// 아이템 장착 및 전투 스탯 반영을 검증하기 위한 Editor 전용 디버그 스크립트.
/// 씬에 임시 오브젝트로 붙여 사용하며, #if UNITY_EDITOR로 감싸 빌드에는 포함되지 않는다.
/// </summary>
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

        if (GUI.Button(new Rect(10, 440, 200, 30), "첫 유닛에 자몽열매 장착"))
        {
            PokemonUnit unit = FindFirstUnit();
            if (unit == null)
            {
                Debug.LogWarning("[ItemDebug] 장착 실패: 보드/벤치에 유닛이 없습니다.");
                return;
            }

            ItemData item = LoadSitrusBerryForTest();
            if (item == null)
            {
                Debug.LogWarning("[ItemDebug] 장착 실패: ItemData가 없습니다. Import Item JSON 먼저 실행");
                return;
            }

            bool success = ItemManager.TryEquipItem(unit, item);

            Debug.Log(
                $"[ItemDebug] 장착 테스트 결과: success={success}, " +
                $"unit={unit.data?.pokemonName ?? "Unknown"}, " +
                $"item={item.itemName}, " +
                $"items.Count={unit.items.Count}/{PokemonUnit.MaxItemSlots}"
            );
        }

        if (GUI.Button(new Rect(10, 474, 200, 30), "첫 유닛 아이템 스탯 확인"))
        {
            PokemonUnit unit = FindFirstUnit();
            if (unit == null)
            {
                Debug.LogWarning("[ItemDebug] 스탯 확인 실패: 보드/벤치에 유닛이 없습니다.");
                return;
            }

            var testBattleUnit = new BattleUnit
            {
                source = unit,
                team = BattleTeam.Ally,
                maxHp = unit.MaxHp,
                currentHp = unit.MaxHp,
                attack = unit.Attack,
                specialAttack = unit.SpecialAttack,
                defense = unit.Defense,
                specialDefense = unit.SpecialDefense,
                attackSpeed = unit.AttackSpeed,
                range = Mathf.Max(1, unit.Range),
                attackType = unit.AttackType,
                attackCooldown = 0f
            };

            Debug.Log(
                $"[ItemDebug] 적용 전: unit={unit.data?.pokemonName ?? "Unknown"}, " +
                $"items={unit.items.Count}, HP={testBattleUnit.maxHp:0.##}, " +
                $"ATK={testBattleUnit.attack:0.##}, DEF={testBattleUnit.defense:0.##}, " +
                $"SPDEF={testBattleUnit.specialDefense:0.##}"
            );

            ItemManager.ApplyItemStats(unit, testBattleUnit);

            Debug.Log(
                $"[ItemDebug] 적용 후: unit={unit.data?.pokemonName ?? "Unknown"}, " +
                $"items={unit.items.Count}, HP={testBattleUnit.maxHp:0.##}, " +
                $"ATK={testBattleUnit.attack:0.##}, DEF={testBattleUnit.defense:0.##}, " +
                $"SPDEF={testBattleUnit.specialDefense:0.##}, " +
                $"CRIT={testBattleUnit.critChance:0.##}"
            );
        }
    }

    private static PokemonUnit FindFirstUnit()
    {
        if (GameManager.Instance == null || GameManager.Instance.Board == null)
            return null;

        var boardUnits = GameManager.Instance.Board.GetUnitsOnBoard();
        if (boardUnits != null && boardUnits.Count > 0)
            return boardUnits[0];

        var benchUnits = GameManager.Instance.Board.GetUnitsInBench();
        if (benchUnits != null && benchUnits.Count > 0)
            return benchUnits[0];

        return null;
    }

    /// <summary>
    /// 스탯 반영 검증을 위해 최대 HP 증가 효과가 있는 자몽열매를 우선 로드한다.
    /// 자몽열매를 찾지 못하면 기존 테스트 흐름을 유지하기 위해 첫 번째 ItemData로 대체한다.
    /// </summary>
    private static ItemData LoadSitrusBerryForTest()
    {
        var guids = AssetDatabase.FindAssets("t:ItemData",
            new[] { "Assets/_Project/ScriptableObjects/Items" });

        if (guids.Length == 0)
            return null;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);

            if (item != null && (item.itemName == "자몽열매" || item.itemNameEn == "Sitrus Berry"))
                return item;
        }

        Debug.LogWarning("[ItemDebug] 자몽열매(Sitrus Berry)를 찾지 못해서 첫 번째 아이템으로 대체합니다.");

        string fallbackPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<ItemData>(fallbackPath);
    }

    private static string StatSummary(ItemData so)
    {
        var parts = new System.Collections.Generic.List<string>();

        if (so.maxHpPct > 0) parts.Add($"maxHpPct={so.maxHpPct}");
        if (so.hpBonus > 0) parts.Add($"hp={so.hpBonus}");
        if (so.hpRegenPercent > 0) parts.Add($"hpRegen={so.hpRegenPercent}");
        if (so.healTakenDmgPct > 0) parts.Add($"healTaken={so.healTakenDmgPct}");
        if (so.shieldPctOnFatalHit > 0) parts.Add($"shield={so.shieldPctOnFatalHit}");
        if (so.attackBonus > 0) parts.Add($"atk={so.attackBonus}");
        if (so.spAtkPct > 0) parts.Add($"spAtk={so.spAtkPct}");
        if (so.attackSpeedBonus > 0) parts.Add($"atkSpd={so.attackSpeedBonus}");
        if (so.moveSpdPctOnKill > 0) parts.Add($"moveSpdOnKill={so.moveSpdPctOnKill}");
        if (so.defenseBonus > 0) parts.Add($"def={so.defenseBonus}");
        if (so.spDefBonus > 0) parts.Add($"spDef={so.spDefBonus}");
        if (so.reflectPhysPct > 0) parts.Add($"reflectPhys={so.reflectPhysPct}");
        if (so.reflectSpPct > 0) parts.Add($"reflectSp={so.reflectSpPct}");
        if (so.defSpDefPerAttacker > 0) parts.Add($"defPerAttacker={so.defSpDefPerAttacker}");
        if (so.criPct > 0) parts.Add($"cri={so.criPct}");
        if (so.criDmgPct > 0) parts.Add($"criDmg={so.criDmgPct}");
        if (so.burnNearOnPhysHit) parts.Add("burnNear=true");
        if (so.ccImmune) parts.Add("ccImmune=true");

        return parts.Count > 0 ? string.Join(", ", parts) : "스탯 없음";
    }
}
#endif