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

    // 내부 JSON 래퍼는 PokemonJsonDb로 명명 — 런타임 SO 클래스 PokemonDatabase와 이름 충돌 회피.
    [Serializable] private class PokemonJsonDb      { public List<PokemonEntry>     pokemon;    }
    [Serializable] private class ItemDatabase       { public List<ItemEntry>        items;      }
    [Serializable] private class SynergyDatabase    { public List<SynergyEntry>     synergies;  }
    [Serializable] private class ConsumableDatabase { public List<ConsumableEntry>  consumables; }
    [Serializable] private class StoneDatabase      { public List<StoneEntry>       stones; }
    [Serializable] private class StageJsonDb        { public List<StageEntry>       stages; }
    [Serializable] private class RewardJsonDb       { public List<RewardTableJson>  tables; }
    [Serializable] private class TrainerEntryJsonDb { public List<TrainerEntryJson> trainers; }
    [Serializable] private class TradeEvoDatabase   { public List<TradeEvoJson>     mappings; }

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
        public string evolvesInto;   // 진화 후 포켓몬 영문명 (최종형은 빈 문자열)
        public string obtainBy;      // "" 또는 "shop"=상점구매가능 / "stone"·"trade"=진화로만 획득(풀 제외)
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
        public string name, nameEn, description;
        public string statKey, statKey2;
        public float  statValue, statValue2;
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

    [Serializable]
    private class ConsumableEntry
    {
        public int id;
        public string name, nameEn, consumableType, description;
    }

    [Serializable]
    private class StoneEntry
    {
        public int id;
        public string name, nameEn, stoneType, description;
        public string targetPokemon, evolvedPokemon;   // 매핑 (행마다 하나씩)
    }

    
    [Serializable]
    private class StageEntry
    {
        public string stageId;
        public int chapter;
        public int round;
        public int order;

        public string stageType;
        public string preReward;
        public string trainerName;
        public string trainerId;        // Trainer Entry 참조 키 (join 임포터에서 사용)

        public string rewardTableId;    // "RW001" 등 문자열 키
        public List<EnemyPlacementJson> enemies;
    }

    [Serializable]
    private class TrainerEntryJson
    {
        public string trainerId;
        public string trainerName;
        public List<EnemyPlacementJson> enemies;
    }

    [Serializable]
    private class EnemyPlacementJson
    {
        public string pokemonNameEn;
        public string pokemonNameKr;
        public int starLevel;
        public int q;
        public int r;
        public string heldItemEn;

        public float statMultiplier;
        public float hpMultiplier;
        public float atkMultiplier;
    }


    [Serializable]
    private class RewardTableJson
    {
        public string rewardTableId;    // "RW001" 등 문자열 키
        public string label;
        public List<RewardEntryJson> rewards;
    }

    [Serializable]
    private class RewardEntryJson
    {
        public string kind;        // gold / item / consumable / stone / unit / augment
        public int    amount;
        public string refNameEn;
        public float  dropChance;  // 0~1, 0이면 확정(1)로 보정
    }

    [Serializable]
    private class TradeEvoJson
    {
        public string targetPokemonEn, evolvedPokemonEn, note;
    }

    // ──────────────────────────────────────────
    // 메뉴 항목
    // ──────────────────────────────────────────

    [MenuItem("PokeChess/Import Pokemon JSON")]
    public static void ImportPokemon()
    {
        var json = Resources.Load<TextAsset>("Data/pokemon_data");
        if (json == null) { Debug.LogError("[PokeChess] pokemon_data.json 없음"); return; }

        var db = JsonUtility.FromJson<PokemonJsonDb>(json.text);
        string dir = $"{SO_PATH}/Pokemon";
        EnsureDir(dir);

        var imported = new List<PokemonData>();

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

            so.evolvesIntoEn = e.evolvesInto ?? "";

            // obtainBy 미지정("")/"shop" → 상점 구매 가능. "stone"·"trade" 등은 진화로만 획득 → 풀 제외.
            so.shopBuyable = string.IsNullOrEmpty(e.obtainBy) ||
                             e.obtainBy.Equals("shop", StringComparison.OrdinalIgnoreCase);

            // modelPrefab / icon 은 덮어쓰지 않음 (Inspector 수동 연결 보호)
            EditorUtility.SetDirty(so);
            imported.Add(so);
        }

        UpdatePokemonDatabase(imported);

        AssetDatabase.SaveAssets();
        Debug.Log($"[PokeChess] 포켓몬 {db.pokemon.Count}종 Import 완료 (PokemonDatabase 자동 갱신)");
    }

    /// <summary>중앙 PokemonDatabase(Resources/PokemonDatabase.asset)를 임포트된 전체 목록으로 갱신.</summary>
    private static void UpdatePokemonDatabase(List<PokemonData> all)
    {
        const string resDir = "Assets/Resources";
        EnsureDir(resDir);

        var db = LoadOrCreate<PokemonDatabase>($"{resDir}/PokemonDatabase.asset");
        db.all = all;
        db.InvalidateCache();
        EditorUtility.SetDirty(db);
    }

    [MenuItem("PokeChess/Import Item JSON")]
    public static void ImportItems()
    {
        var json = Resources.Load<TextAsset>("Data/item_data");
        if (json == null) { Debug.LogError("[PokeChess] item_data.json 없음"); return; }

        // 루트가 배열([])로 export된 경우 래핑
        var jsonText = json.text.TrimStart();
        if (jsonText.StartsWith("["))
            jsonText = $"{{\"items\":{jsonText}}}";

        var db = JsonUtility.FromJson<ItemDatabase>(jsonText);
        string dir = $"{SO_PATH}/Items";
        EnsureDir(dir);

        foreach (var e in db.items)
        {
            string path = $"{dir}/{e.nameEn}_Item.asset";
            var so = LoadOrCreate<ItemData>(path);

            so.id          = e.id;
            so.itemName    = e.name;
            so.itemNameEn  = e.nameEn;
            so.description = e.description;

            if (!string.IsNullOrEmpty(e.statKey))  ApplyItemStat(so, e.statKey,  e.statValue);
            if (!string.IsNullOrEmpty(e.statKey2)) ApplyItemStat(so, e.statKey2, e.statValue2);

            EditorUtility.SetDirty(so);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[PokeChess] 아이템 {db.items.Count}종 Import 완료");
    }

    [MenuItem("PokeChess/Import Consumable JSON")]
    public static void ImportConsumables()
    {
        var json = Resources.Load<TextAsset>("Data/consumable_data");
        if (json == null) { Debug.LogError("[PokeChess] consumable_data.json 없음"); return; }

        var db  = JsonUtility.FromJson<ConsumableDatabase>(json.text);
        string dir = $"{SO_PATH}/Consumables";
        EnsureDir(dir);

        foreach (var e in db.consumables)
        {
            string path = $"{dir}/{e.nameEn}_Consumable.asset";
            var so = LoadOrCreate<ConsumableData>(path);

            so.id              = e.id;
            so.consumableName  = e.name;
            so.consumableNameEn = e.nameEn;
            so.consumableType  = e.consumableType;
            so.description     = e.description;

            EditorUtility.SetDirty(so);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[PokeChess] 소모템 {db.consumables.Count}종 Import 완료");
    }

    [MenuItem("PokeChess/Import EvolutionStone JSON")]
    public static void ImportEvolutionStones()
    {
        var json = Resources.Load<TextAsset>("Data/evolution_stone_data");
        if (json == null) { Debug.LogError("[PokeChess] evolution_stone_data.json 없음"); return; }

        var jsonText = json.text.TrimStart();
        if (jsonText.StartsWith("["))
            jsonText = $"{{\"stones\":{jsonText}}}";

        var db = JsonUtility.FromJson<StoneDatabase>(jsonText);

        // id로 그룹핑 → SO 1개당 매핑 여러 개
        var groups = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<StoneEntry>>();
        foreach (var e in db.stones)
        {
            if (!groups.ContainsKey(e.id))
                groups[e.id] = new System.Collections.Generic.List<StoneEntry>();
            groups[e.id].Add(e);
        }

        string dir = $"{SO_PATH}/EvolutionStones";
        EnsureDir(dir);

        foreach (var kv in groups)
        {
            var first = kv.Value[0];
            string path = $"{dir}/{first.nameEn}_Stone.asset";
            var so = LoadOrCreate<EvolutionStoneData>(path);

            so.id          = first.id;
            so.stoneName   = first.name;
            so.stoneNameEn = first.nameEn;
            so.stoneType   = first.stoneType;
            so.description = first.description;

            so.mappings.Clear();
            foreach (var e in kv.Value)
                if (!string.IsNullOrEmpty(e.targetPokemon))
                    so.mappings.Add(new EvolutionMapping
                    {
                        targetPokemon  = e.targetPokemon,
                        evolvedPokemon = e.evolvedPokemon
                    });

            EditorUtility.SetDirty(so);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[PokeChess] 진화의 돌 {groups.Count}종 Import 완료");
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

    [MenuItem("PokeChess/Import Reward JSON")]
    public static void ImportRewards()
    {
        var json = Resources.Load<TextAsset>("Data/reward_data");
        if (json == null) { Debug.LogError("[PokeChess] reward_data.json 없음"); return; }

        var jsonDb = JsonUtility.FromJson<RewardJsonDb>(json.text);
        if (jsonDb == null || jsonDb.tables == null)
        {
            Debug.LogError("[PokeChess] reward_data.json 파싱 실패");
            return;
        }

        // 테이블당 .asset을 만들지 않고, 단일 RewardDatabase.asset의 List를 통째로 교체. (StageDatabase와 동일 패턴)
        var tables = new List<RewardData>();
        foreach (var t in jsonDb.tables)
        {
            var table = new RewardData
            {
                rewardTableId = t.rewardTableId,
                label         = t.label,
                rewards       = new List<RewardEntry>()
            };

            if (t.rewards != null)
                foreach (var r in t.rewards)
                    table.rewards.Add(new RewardEntry
                    {
                        kind       = ParseRewardKind(r.kind),
                        amount     = r.amount,
                        refNameEn  = r.refNameEn,
                        dropChance = r.dropChance <= 0f ? 1f : r.dropChance
                    });

            tables.Add(table);
        }

        const string resDir = "Assets/Resources";
        EnsureDir(resDir);
        var so = LoadOrCreate<RewardDatabase>($"{resDir}/RewardDatabase.asset");
        so.tables = tables;
        EditorUtility.SetDirty(so);

        AssetDatabase.SaveAssets();
        Debug.Log($"[PokeChess] 보상 테이블 {tables.Count}개 Import 완료 (RewardDatabase 단일 에셋 갱신)");
    }

    [MenuItem("PokeChess/Import Stage JSON")]
    public static void ImportStages()
    {
        var json = Resources.Load<TextAsset>("Data/stage_data");
        if (json == null) { Debug.LogError("[PokeChess] stage_data.json 없음"); return; }

        var jsonDb = JsonUtility.FromJson<StageJsonDb>(json.text);
        if (jsonDb == null || jsonDb.stages == null)
        {
            Debug.LogError("[PokeChess] stage_data.json 파싱 실패");
            return;
        }

        // trainer_entry_data.json 로드
        var trainerMap = LoadTrainerEntryMap();

        // 스테이지당 .asset을 만들지 않고, 단일 StageDatabase.asset의 List를 통째로 교체.
        var stages = new List<StageData>();

        foreach (var e in jsonDb.stages)
        {
            var stage = new StageData
            {
                stageId = e.stageId,
                chapter = e.chapter,
                round = e.round,
                order = e.order,
                stageType = ParseStageType(e.stageType),
                preReward = ParsePreStageReward(e.preReward),
                trainerName = e.trainerName ?? "",
                trainerId = e.trainerId ?? "",
                rewardTableId = e.rewardTableId ?? "",
                enemies = new List<EnemyPlacement>()
            };

            List<EnemyPlacementJson> sourceEnemies = null;

            // 1순위: trainerId가 있고 trainer_entry_data.json에서 찾을 수 있으면 그 적 구성을 사용
            if (!string.IsNullOrEmpty(e.trainerId) &&
                trainerMap.TryGetValue(e.trainerId, out var trainerEntry) &&
                trainerEntry.enemies != null)
            {
                sourceEnemies = trainerEntry.enemies;

                if (!string.IsNullOrEmpty(trainerEntry.trainerName))
                    stage.trainerName = trainerEntry.trainerName;
            }
            else
            {
                // 2순위: 전환기 폴백 — 기존 stage_data.json 인라인 enemies 사용
                sourceEnemies = e.enemies;

                if (!string.IsNullOrEmpty(e.trainerId))
                    Debug.LogWarning($"[PokeChess] trainerId '{e.trainerId}'를 trainer_entry_data에서 찾지 못해 stage_data 인라인 enemies를 사용합니다.");
            }

            AddEnemies(stage, sourceEnemies);

            stages.Add(stage);
        }

        const string resDir = "Assets/Resources";
        EnsureDir(resDir);

        var so = LoadOrCreate<StageDatabase>($"{resDir}/StageDatabase.asset");
        so.stages = stages;
        EditorUtility.SetDirty(so);

        AssetDatabase.SaveAssets();
        Debug.Log($"[PokeChess] 스테이지 {stages.Count}개 Import 완료 (trainer_entry join 적용)");
    }

    [MenuItem("PokeChess/Import TradeEvolution JSON")]
    public static void ImportTradeEvolutions()
    {
        var json = Resources.Load<TextAsset>("Data/trade_evolution_data");
        if (json == null) { Debug.LogError("[PokeChess] trade_evolution_data.json 없음"); return; }

        var db = JsonUtility.FromJson<TradeEvoDatabase>(json.text);
        string dir = $"{SO_PATH}/Evolution";
        EnsureDir(dir);

        string path = $"{dir}/TradeEvolution_Data.asset";   // 통신진화는 단일 SO에 모음
        var so = LoadOrCreate<TradeEvolutionData>(path);

        so.mappings = new List<TradeEvolutionMapping>();
        if (db.mappings != null)
            foreach (var m in db.mappings)
                if (!string.IsNullOrEmpty(m.targetPokemonEn))
                    so.mappings.Add(new TradeEvolutionMapping
                    {
                        targetPokemonEn  = m.targetPokemonEn,
                        evolvedPokemonEn = m.evolvedPokemonEn,
                        note             = m.note
                    });

        EditorUtility.SetDirty(so);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PokeChess] 통신진화 매핑 {so.mappings.Count}개 Import 완료");
    }

    // ──────────────────────────────────────────
    // 유틸
    // ──────────────────────────────────────────

    private static Dictionary<string, TrainerEntryJson> LoadTrainerEntryMap()
    {
        var map = new Dictionary<string, TrainerEntryJson>(StringComparer.OrdinalIgnoreCase);

        var trainerJson = Resources.Load<TextAsset>("Data/trainer_entry_data");
        if (trainerJson == null)
        {
            Debug.LogWarning("[PokeChess] trainer_entry_data.json 없음 — stage_data 인라인 enemies를 사용합니다.");
            return map;
        }

        var trainerDb = JsonUtility.FromJson<TrainerEntryJsonDb>(trainerJson.text);
        if (trainerDb == null || trainerDb.trainers == null)
        {
            Debug.LogWarning("[PokeChess] trainer_entry_data.json 파싱 실패 — stage_data 인라인 enemies를 사용합니다.");
            return map;
        }

        foreach (var trainer in trainerDb.trainers)
        {
            if (trainer == null || string.IsNullOrEmpty(trainer.trainerId))
                continue;

            map[trainer.trainerId] = trainer;
        }

        Debug.Log($"[PokeChess] trainer_entry_data {map.Count}개 로드 완료");
        return map;
    }

    private static void AddEnemies(StageData stage, List<EnemyPlacementJson> sourceEnemies)
    {
        if (stage == null || sourceEnemies == null)
            return;

        foreach (var enemy in sourceEnemies)
        {
            if (enemy == null)
                continue;

            stage.enemies.Add(new EnemyPlacement
            {
                pokemonNameEn = enemy.pokemonNameEn ?? "",
                pokemonNameKr = enemy.pokemonNameKr ?? "",
                starLevel = enemy.starLevel <= 0 ? 1 : enemy.starLevel,
                q = enemy.q,
                r = enemy.r,
                heldItemEn = enemy.heldItemEn ?? "",
                statMultiplier = NormalizeMultiplier(enemy.statMultiplier),
                hpMultiplier = NormalizeMultiplier(enemy.hpMultiplier),
                atkMultiplier = NormalizeMultiplier(enemy.atkMultiplier)
            });
        }
    }

    private static RewardKind ParseRewardKind(string s) => s switch
    {
        "reroll"                    => RewardKind.Reroll,
        "item"                      => RewardKind.Item,
        "consumable"                => RewardKind.Consumable,
        "stone" or "evolutionStone" => RewardKind.EvolutionStone,
        "unit"                      => RewardKind.Unit,
        "augment" or "augmentChoice"=> RewardKind.AugmentChoice,
        _                           => RewardKind.Gold,
    };

    private static void ApplyItemStat(ItemData so, string key, float value)
    {
        switch (key)
        {
            case "hp":                  so.hpBonus              = value; break;
            case "maxHpPct":            so.maxHpPct             = value; break;
            case "hpRegenPercent":      so.hpRegenPercent       = value; break;
            case "healTakenDmgPct":     so.healTakenDmgPct      = value; break;
            case "shieldPctOnFatalHit": so.shieldPctOnFatalHit  = value; break;
            case "atk":                 so.attackBonus          = value; break;
            case "spAtkPct":            so.spAtkPct             = value; break;
            case "atkSpdPct":           so.attackSpeedBonus     = value; break;
            case "moveSpdPctOnKill":    so.moveSpdPctOnKill     = value; break;
            case "def":                 so.defenseBonus         = value; break;
            case "spDef":               so.spDefBonus           = value; break;
            case "reflectPhysPct":      so.reflectPhysPct       = value; break;
            case "reflectSpPct":        so.reflectSpPct         = value; break;
            case "defSpDefPerAttacker": so.defSpDefPerAttacker  = value; break;
            case "criPct":              so.criPct               = value; break;
            case "criDmgPct":           so.criDmgPct            = value; break;
            case "burnNearOnPhysHit":   so.burnNearOnPhysHit    = value > 0; break;
            case "ccImmune":            so.ccImmune             = value > 0; break;
            default: Debug.LogWarning($"[PokeChess] 알 수 없는 statKey: {key}"); break;
        }
    }

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

    private static StageType ParseStageType(string s)
    {
        if (Enum.TryParse(s, true, out StageType result))
            return result;

        Debug.LogWarning($"[PokeChess] 알 수 없는 StageType: {s}, WildCommon으로 처리");
        return StageType.WildCommon;
    }

    private static PreStageReward ParsePreStageReward(string s)
    {
        if (Enum.TryParse(s, true, out PreStageReward result))
            return result;

        Debug.LogWarning($"[PokeChess] 알 수 없는 PreStageReward: {s}, None으로 처리");
        return PreStageReward.None;
    }

    private static float NormalizeMultiplier(float value)
    {
        return value <= 0f ? 1f : value;
    }
}


#endif
