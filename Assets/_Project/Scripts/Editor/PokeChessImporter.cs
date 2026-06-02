#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unity 메뉴 → PokeChess → Import JSON
/// Assets/Resources/Data/ 에 JSON 파일을 넣고 실행하면
/// ScriptableObject를 자동 생성/업데이트함.
/// </summary>
public static class PokeChessImporter
{
    private const string SO_PATH = "Assets/_Project/ScriptableObjects";

    // ──────────────────────────────────────────
    // JSON 데이터 클래스 (JsonUtility용 래퍼)
    // ──────────────────────────────────────────

    [Serializable] private class PokemonDatabase    { public List<PokemonEntry> pokemon; }
    [Serializable] private class ItemDatabase       { public List<ItemEntry>    items;   }
    [Serializable] private class SynergyDatabase    { public List<SynergyEntry> synergies; }

    [Serializable]
    private class PokemonEntry
    {
        public int id;
        public string name, nameEn;
        public int cost;
        public float hp, attack, defense, specialAttack, specialDefense, attackSpeed;
        public int range;
        public List<string> synergies;
        public string attackType;
        public SkillEntry skill;
        public string modelPath, iconPath;
    }

    [Serializable]
    private class SkillEntry
    {
        public string name, description;
        public float damage;
        public int manaCost;
        public string targetType;
        public int areaRadius, lineLength;
    }

    [Serializable]
    private class ItemEntry
    {
        public int id;
        public string name, nameEn, category, description;
        public List<string> recipe;
    }

    [Serializable]
    private class SynergyEntry
    {
        public int id;
        public string name, nameEn;
        public List<SynergyTierEntry> tiers;
    }

    [Serializable]
    private class SynergyTierEntry
    {
        public int count;
        public string effect;
    }

    // ──────────────────────────────────────────
    // 메뉴 항목
    // ──────────────────────────────────────────

    [MenuItem("PokeChess/Import Pokemon JSON")]
    public static void ImportPokemon()
    {
        var json = Resources.Load<TextAsset>("Data/pokemon_data");
        if (json == null) { Debug.LogError("[PokeChess] pokemon_data.json 없음"); return; }

        var db = JsonUtility.FromJson<PokemonDatabase>(json.text);
        string dir = $"{SO_PATH}/Pokemon";
        EnsureDir(dir);

        foreach (var e in db.pokemon)
        {
            string path = $"{dir}/{e.nameEn}_Data.asset";
            var so = LoadOrCreate<PokemonData>(path);

            so.id             = e.id;
            so.pokemonName    = e.name;
            so.pokemonNameEn  = e.nameEn;
            so.cost           = e.cost;
            so.hp             = e.hp;
            so.attack         = e.attack;
            so.defense        = e.defense;
            so.specialAttack  = e.specialAttack;
            so.specialDefense = e.specialDefense;
            so.attackSpeed    = e.attackSpeed;
            so.range          = e.range;
            so.synergies      = e.synergies ?? new List<string>();
            so.attackType     = e.attackType == "physical" ? AttackType.Physical : AttackType.Special;

            so.skill = new PokemonSkillData
            {
                skillName   = e.skill.name,
                description = e.skill.description,
                damage      = e.skill.damage,
                manaCost    = e.skill.manaCost,
                targetType  = ParseTargetType(e.skill.targetType),
                areaRadius  = e.skill.areaRadius,
                lineLength  = e.skill.lineLength
            };

            // modelPrefab / icon 은 덮어쓰지 않음 (Inspector 수동 연결 보호)
            EditorUtility.SetDirty(so);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[PokeChess] 포켓몬 {db.pokemon.Count}종 Import 완료");
    }

    [MenuItem("PokeChess/Import Item JSON")]
    public static void ImportItems()
    {
        var json = Resources.Load<TextAsset>("Data/item_data");
        if (json == null) { Debug.LogError("[PokeChess] item_data.json 없음"); return; }

        var db = JsonUtility.FromJson<ItemDatabase>(json.text);
        string dir = $"{SO_PATH}/Items";
        EnsureDir(dir);

        foreach (var e in db.items)
        {
            string path = $"{dir}/{e.nameEn}_Item.asset";
            var so = LoadOrCreate<ItemData>(path);

            so.id          = e.id;
            so.itemName    = e.name;
            so.itemNameEn  = e.nameEn;
            so.category    = e.category == "ingredient" ? ItemCategory.Ingredient : ItemCategory.Result;
            so.description = e.description;
            so.recipe      = e.recipe ?? new List<string>();

            EditorUtility.SetDirty(so);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[PokeChess] 아이템 {db.items.Count}종 Import 완료");
    }

    [MenuItem("PokeChess/Import Synergy JSON")]
    public static void ImportSynergies()
    {
        var json = Resources.Load<TextAsset>("Data/synergy_data");
        if (json == null) { Debug.LogError("[PokeChess] synergy_data.json 없음"); return; }

        var db = JsonUtility.FromJson<SynergyDatabase>(json.text);
        string dir = $"{SO_PATH}/Synergies";
        EnsureDir(dir);

        foreach (var e in db.synergies)
        {
            string path = $"{dir}/{e.nameEn}_Synergy.asset";
            var so = LoadOrCreate<SynergyData>(path);

            so.id            = e.id;
            so.synergyName   = e.name;
            so.synergyNameEn = e.nameEn;
            so.tiers         = new List<SynergyTier>();

            foreach (var t in e.tiers)
                so.tiers.Add(new SynergyTier { count = t.count, effectDescription = t.effect });

            EditorUtility.SetDirty(so);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[PokeChess] 시너지 {db.synergies.Count}종 Import 완료");
    }

    // ──────────────────────────────────────────
    // 유틸
    // ──────────────────────────────────────────

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var so = AssetDatabase.LoadAssetAtPath<T>(path);
        if (so == null)
        {
            so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, path);
        }
        return so;
    }

    private static void EnsureDir(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    private static SkillTargetType ParseTargetType(string s) => s switch
    {
        "area"   => SkillTargetType.Area,
        "line"   => SkillTargetType.Line,
        "all"    => SkillTargetType.All,
        _        => SkillTargetType.Single
    };
}
#endif
