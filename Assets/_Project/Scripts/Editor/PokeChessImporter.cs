#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using PokeChess.EditorTools;
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
    [Serializable] private class ItemJsonDb         { public List<ItemEntry>        items;      }
    [Serializable] private class SynergyJsonDb      { public List<SynergyEntry>     synergies;  }
    [Serializable] private class ConsumableDatabase { public List<ConsumableEntry>  consumables; }
    [Serializable] private class StoneJsonDb        { public List<StoneEntry>       stones; }
    [Serializable] private class StageJsonDb        { public List<StageEntry>       stages; }
    [Serializable] private class RewardJsonDb       { public List<RewardTableJson>  tables; }
    [Serializable] private class TrainerEntryJsonDb { public List<TrainerEntryJson> trainers; }
    [Serializable] private class TradeEvoDatabase   { public List<TradeEvoJson>     mappings; }
    [Serializable] private class DeckJsonDb         { public List<DeckJson>         decks; }

    [Serializable]
    private class PokemonEntry
    {
        public int id;
        public string name, nameEn;
        public int cost;
        public int range;
        public float hp, attack, defense, atkSpeed;
        public float spellPower;
        public int manaCost;
        public string skillId;          // Skill Table 참조 (없으면 평타만)
        public string attackVfxId;      // 평타 VFX 키 (v11 신설)
        public List<string> synergies;  // synergy1, synergy2 합친 배열
        public string role;
        public string obtainBy;         // shop=상점풀 / evolution·stone·trade·synergy·wild=풀 제외
        public string evolvesInto;      // 별업 진화체 영문명 (최종형/별루트는 빈 문자열)
    }

    [Serializable] private class SkillTableJsonDb { public List<SkillTableEntry> skills; }

    [Serializable]
    private class SkillTableEntry
    {
        public string skillId, skillName, skillType, role;
        public string effectType, targetType;
        public int areaRadius, lineLength;
        public string vfxId, description;
    }

    [Serializable]
    private class ItemEntry
    {
        public int id;
        public string name, nameEn, description;
        public string statKey, statKey2;
        public float  statValue, statValue2;
        public List<ItemStatEntry> stats;
    }

    [Serializable]
    private class ItemStatEntry
    {
        public string key;
        public float value;
        public bool isPercent;
        public bool hasValue = true;
    }

    [Serializable]
    private class SynergyEntry
    {
        public int id;
        public string name, nameEn;
        public List<SynergyTierEntry> tiers;
        public bool countPerSpecies;   // 시트 countPerSpecies 컬럼. 키가 없으면 JsonUtility가 false로 둔다.
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

        // 기존 chapter도 유지하고, 시트에서 stage라는 이름으로 들어와도 받을 수 있게 추가
        public int chapter;
        public int stage;
        public int round;
        public int order;

        // 기존 stageType도 유지하고, 신규 스키마의 battleType도 받을 수 있게 추가
        public string stageType;
        public string battleType;

        public string preReward;
        public string trainerName;
        public string trainerId;

        // 기존 rewardTableId도 유지하고, 신규 시트의 rewardId도 받을 수 있게 추가
        public string rewardTableId;
        public string rewardId;

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

        // 신규 스키마: Trainer Entry 시트는 slot(1~6)을 우선 사용
        public int slot;

        // 전환기 / 보스전 override용: q/r이 직접 들어오면 그대로 사용 가능
        public int q;
        public int r;

        // 시트 컬럼 itemSet1 / itemSet2. "NONE"은 빈 칸을 뜻하는 시트 관례라 임포트에서 걸러낸다.
        public string heldItemEn;
        public string heldItemEn2;

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

    [Serializable]
    private class DeckJson
    {
        public int deckId;
        public string deckName;
        public int unitCount;
        public List<string> activeSynergies;
        public int totalGoldToBuild;
        public List<DeckUnitJson> units;
    }

    [Serializable]
    private class DeckUnitJson
    {
        public int pokemonId;
        public int starLevel;
        public int slot;
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

        var skillMap = LoadSkillTableMap(); // skillId → Skill Table 행
        var imported = new List<PokemonData>();

        foreach (var e in db.pokemon)
        {
            string path = $"{dir}/{e.nameEn}_Data.asset";
            var so = LoadOrCreate<PokemonData>(path);

            so.id            = e.id;
            so.pokemonName   = e.name;
            so.pokemonNameEn = e.nameEn;
            so.cost          = e.cost;
            so.hp            = e.hp;
            so.attack        = e.attack;
            so.defense       = e.defense;
            so.attackSpeed   = e.atkSpeed;
            so.range         = e.range;
            so.spellPower    = e.spellPower;
            so.manaCost      = e.manaCost;
            so.synergies     = e.synergies ?? new List<string>();
            so.role          = e.role ?? "";
            so.skillId       = e.skillId ?? "";
            so.attackVfxId   = e.attackVfxId ?? "";

            // skillId로 Skill Table join → skill에 베이킹. 없으면 평타만(빈 스킬).
            so.skill = BuildSkill(e.skillId, skillMap);

            // 평타 사거리는 attackVfxId로 Skill Table의 ATTACK 행을 조인해 lineLength에서 가져온다.
            // (시트의 range 컬럼은 사거리가 아니라 진화 단계라 쓰면 안 된다.)
            so.attackRange = ResolveAttackRange(e.attackVfxId, skillMap, e.name);

            so.evolvesIntoEn = e.evolvesInto ?? "";

            // obtainBy 미지정("")/"shop" → 상점 풀 포함. evolution/stone/trade/synergy/wild → 풀 제외.
            so.shopBuyable = string.IsNullOrEmpty(e.obtainBy) ||
                             e.obtainBy.Equals("shop", StringComparison.OrdinalIgnoreCase);

            // 원본 문자열도 보관한다 — shopBuyable만으로는 "적 전용(wild)"과
            // "상점엔 없지만 플레이어가 얻는 진화체(evolution/stone/trade/synergy)"를 구분할 수 없다.
            so.obtainBy = e.obtainBy ?? string.Empty;

            // modelPrefab / icon 은 덮어쓰지 않음 (Inspector 수동 연결 보호)
            EditorUtility.SetDirty(so);
            imported.Add(so);
        }

        UpdatePokemonDatabase(imported);
        EvolutionFamily.Invalidate();    // 진화 사슬(evolvesInto)이 바뀌었을 수 있음
        SynergyMemberIndex.Invalidate(); // 포켓몬의 시너지 목록·계열이 바뀌었을 수 있음

        AssetDatabase.SaveAssets();
        int withSkill = imported.FindAll(p => p.skill != null && p.skill.HasSkill).Count;
        Debug.Log($"[PokeChess] 포켓몬 {db.pokemon.Count}종 Import 완료 (스킬 join {withSkill}종, PokemonDatabase 갱신)");
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

        var db = JsonUtility.FromJson<ItemJsonDb>(jsonText);
        string dir = $"{SO_PATH}/Items";
        EnsureDir(dir);

        var imported = new List<ItemData>();

        foreach (var e in db.items)
        {
            string path = $"{dir}/{e.nameEn}_Item.asset";
            var so = LoadOrCreate<ItemData>(path);

            so.id          = e.id;
            so.itemName    = e.name;
            so.itemNameEn  = e.nameEn;
            so.description = e.description;

            ResetItemStats(so);
            so.sourceStats.Clear();

            if (e.stats != null && e.stats.Count > 0)
            {
                foreach (var stat in e.stats)
                {
                    if (stat == null || string.IsNullOrWhiteSpace(stat.key)) continue;

                    so.sourceStats.Add(new ItemStatModifier
                    {
                        key       = stat.key.Trim(),
                        value     = stat.value,
                        isPercent = stat.isPercent,
                        hasValue  = stat.hasValue,
                    });

                    if (stat.hasValue)
                        ApplyGeneralItemStat(so, stat.key.Trim(), stat.value, stat.isPercent);
                    else
                        Debug.LogWarning($"[PokeChess] Item id={e.id} '{e.nameEn}'의 {stat.key} 값이 비어 있음");
                }
            }
            else
            {
                // 구형 item_data.json 호환.
                if (!string.IsNullOrEmpty(e.statKey))
                {
                    so.sourceStats.Add(new ItemStatModifier { key = e.statKey, value = e.statValue });
                    ApplyItemStat(so, e.statKey, e.statValue);
                }
                if (!string.IsNullOrEmpty(e.statKey2))
                {
                    so.sourceStats.Add(new ItemStatModifier { key = e.statKey2, value = e.statValue2 });
                    ApplyItemStat(so, e.statKey2, e.statValue2);
                }
            }

            EditorUtility.SetDirty(so);
            imported.Add(so);
        }

        UpdateItemDatabase(imported);

        AssetDatabase.SaveAssets();
        Debug.Log($"[PokeChess] 아이템 {db.items.Count}종 Import 완료 (ItemDatabase 갱신)");
    }

    /// <summary>중앙 ItemDatabase(Resources/ItemDatabase.asset)를 임포트된 전체 목록으로 갱신.</summary>
    private static void UpdateItemDatabase(List<ItemData> all)
    {
        const string resDir = "Assets/Resources";
        EnsureDir(resDir);

        var db = LoadOrCreate<ItemDatabase>($"{resDir}/ItemDatabase.asset");
        db.all = all;
        db.InvalidateCache();
        EditorUtility.SetDirty(db);
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

        var db = JsonUtility.FromJson<StoneJsonDb>(jsonText);

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

        var imported = new List<EvolutionStoneData>();

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
            imported.Add(so);
        }

        UpdateEvolutionStoneDatabase(imported);
        EvolutionFamily.Invalidate();    // 돌 진화 매핑이 바뀌었을 수 있음
        SynergyMemberIndex.Invalidate(); // 계열이 바뀌면 시너지 카운트 키도 바뀐다

        AssetDatabase.SaveAssets();
        Debug.Log($"[PokeChess] 진화의 돌 {groups.Count}종 Import 완료 (EvolutionStoneDatabase 갱신)");
    }

    /// <summary>중앙 EvolutionStoneDatabase(Resources/EvolutionStoneDatabase.asset)를 임포트된 전체 목록으로 갱신.</summary>
    private static void UpdateEvolutionStoneDatabase(List<EvolutionStoneData> all)
    {
        const string resDir = "Assets/Resources";
        EnsureDir(resDir);

        var db = LoadOrCreate<EvolutionStoneDatabase>($"{resDir}/EvolutionStoneDatabase.asset");
        db.all = all;
        db.InvalidateCache();
        EditorUtility.SetDirty(db);
    }

    [MenuItem("PokeChess/Import Synergy JSON")]
    public static void ImportSynergies()
    {
        var json = Resources.Load<TextAsset>("Data/synergy_data");
        if (json == null) { Debug.LogError("[PokeChess] synergy_data.json 없음"); return; }

        var db = JsonUtility.FromJson<SynergyJsonDb>(json.text);
        string dir = $"{SO_PATH}/Synergies";
        EnsureDir(dir);

        var imported = new List<SynergyData>();

        foreach (var e in db.synergies)
        {
            string path = $"{dir}/{e.nameEn}_Synergy.asset";
            var so = LoadOrCreate<SynergyData>(path);

            so.id              = e.id;
            so.synergyName     = e.name;
            so.synergyNameEn   = e.nameEn;
            so.countPerSpecies = e.countPerSpecies;
            so.tiers           = new List<SynergyTier>();

            foreach (var t in e.tiers)
                so.tiers.Add(new SynergyTier { count = t.count, effectDescription = t.effect });

            EditorUtility.SetDirty(so);
            imported.Add(so);
        }

        UpdateSynergyDatabase(imported);
        SynergyMemberIndex.Invalidate(); // 시너지 구성(countPerSpecies 등)이 바뀌었을 수 있음

        AssetDatabase.SaveAssets();
        Debug.Log($"[PokeChess] 시너지 {db.synergies.Count}종 Import 완료 (SynergyDatabase 갱신)");
    }

    /// <summary>중앙 SynergyDatabase(Resources/SynergyDatabase.asset)를 임포트된 전체 목록으로 갱신.</summary>
    private static void UpdateSynergyDatabase(List<SynergyData> all)
    {
        const string resDir = "Assets/Resources";
        EnsureDir(resDir);

        var db = LoadOrCreate<SynergyDatabase>($"{resDir}/SynergyDatabase.asset");
        db.all = all;
        db.InvalidateCache();
        EditorUtility.SetDirty(db);
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

        // reward_data의 refNameEn이 실제 데이터 시트에 존재하는지 검증
        ValidateRewardRefs(jsonDb);

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
        var diagnostics = new TrainerEntryDiagnostics();
        var trainerMap = LoadTrainerEntryMap(diagnostics);

        // 스테이지당 .asset을 만들지 않고, 단일 StageDatabase.asset의 List를 통째로 교체.
        var stages = new List<StageData>();

        foreach (var e in jsonDb.stages)
        {
            int chapter = ResolveChapter(e);
            int round = e.round;

            var stage = new StageData
            {
                stageId = ResolveStageId(e, chapter, round),
                chapter = chapter,
                round = round,
                order = ResolveOrder(e, stages.Count + 1),

                stageType = ResolveStageType(e),
                preReward = ParsePreStageReward(e.preReward),

                trainerName = e.trainerName ?? "",
                trainerId = e.trainerId ?? "",
                rewardTableId = ResolveRewardTableId(e),

                enemies = new List<EnemyPlacement>()
            };

            List<EnemyPlacementJson> sourceEnemies = null;
            TrainerEntryJson trainerEntry = null;
            bool hasTrainerEntry = !string.IsNullOrEmpty(e.trainerId) &&
                                   trainerMap.TryGetValue(e.trainerId, out trainerEntry);

            // 엔트리를 찾았다면 적 구성 사용 여부와 무관하게 이름은 반영하고 "참조됨"으로 집계
            if (hasTrainerEntry)
            {
                diagnostics.RecordTrainerEntryReferenced(e.trainerId);

                if (!string.IsNullOrEmpty(trainerEntry.trainerName))
                    stage.trainerName = trainerEntry.trainerName;
            }

            // 1순위: trainer_entry에 실제 적 구성이 있으면 그것을 사용 (빈 리스트는 없는 것으로 취급)
            if (hasTrainerEntry && trainerEntry.enemies != null && trainerEntry.enemies.Count > 0)
            {
                sourceEnemies = trainerEntry.enemies;
            }
            else
            {
                // 2순위: 전환기 폴백 — 기존 stage_data.json 인라인 enemies 사용
                sourceEnemies = e.enemies;
                diagnostics.RecordInlineEnemyFallback(stage.stageId, e.trainerId, hasTrainerEntry);
            }

            AddEnemies(stage, sourceEnemies);

            // StageType이 전부 전투이므로 적 0마리는 항상 결함
            if (stage.enemies.Count == 0)
                diagnostics.RecordEmptyStage(stage.stageId);

            stages.Add(stage);
        }

        // 깨진 데이터가 에셋에 저장되지 않도록, 쓰기 전에 진단하고 결함이 있으면 중단한다.
        LogTrainerEntryDiagnostics(diagnostics, trainerMap.Keys);
        if (diagnostics.HasBlockingIssues)
        {
            Debug.LogError(
                "[PokeChess] 스테이지 Import 중단 — 위 오류를 해결한 뒤 다시 실행하세요. " +
                "StageDatabase.asset은 변경되지 않았습니다.");
            return;
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
        const string resDir = "Assets/Resources";           // 런타임 TradeEvolutionData.Instance가 Resources에서 로드
        EnsureDir(resDir);

        string path = $"{resDir}/TradeEvolution_Data.asset"; // 통신진화는 단일 SO에 모음
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
        EvolutionFamily.Invalidate();    // 통신교환 매핑이 바뀌었을 수 있음
        SynergyMemberIndex.Invalidate(); // 계열이 바뀌면 시너지 카운트 키도 바뀐다

        AssetDatabase.SaveAssets();
        Debug.Log($"[PokeChess] 통신진화 매핑 {so.mappings.Count}개 Import 완료");
    }

    [MenuItem("PokeChess/Import Deck JSON")]
    public static void ImportDecks()
    {
        var json = Resources.Load<TextAsset>("Data/deck_data");
        if (json == null) { Debug.LogError("[PokeChess] deck_data.json 없음"); return; }

        var jsonDb = JsonUtility.FromJson<DeckJsonDb>(json.text);
        if (jsonDb == null || jsonDb.decks == null)
        {
            Debug.LogError("[PokeChess] deck_data.json 파싱 실패");
            return;
        }

        // pokemonId 존재 검증용 — PokemonDatabase가 아직 미임포트면 id 검증만 생략
        var pokemonIds = LoadPokemonIdSet();

        // 덱당 .asset을 만들지 않고, 단일 DeckDatabase.asset의 List를 통째로 교체. (RewardDatabase와 동일 패턴)
        var decks = new List<DeckData>();
        foreach (var d in jsonDb.decks)
        {
            var deck = new DeckData
            {
                deckId           = d.deckId,
                deckName         = d.deckName ?? "",
                unitCount        = d.unitCount,
                activeSynergies  = d.activeSynergies ?? new List<string>(),
                totalGoldToBuild = d.totalGoldToBuild,
                units            = new List<DeckUnitEntry>()
            };

            if (d.units != null)
                foreach (var u in d.units)
                {
                    if (pokemonIds.Count > 0 && !pokemonIds.Contains(u.pokemonId))
                        Debug.LogWarning($"[PokeChess] 덱 {d.deckId} '{d.deckName}': pokemonId {u.pokemonId} 가 PokemonDatabase에 없음");

                    deck.units.Add(new DeckUnitEntry
                    {
                        pokemonId = u.pokemonId,
                        starLevel = u.starLevel <= 0 ? 1 : u.starLevel,
                        slot      = u.slot
                    });
                }

            if (deck.unitCount != deck.units.Count)
                Debug.LogWarning($"[PokeChess] 덱 {d.deckId} '{d.deckName}': unitCount={deck.unitCount} vs 실제 유닛 {deck.units.Count}기");

            decks.Add(deck);
        }

        const string resDir = "Assets/Resources";
        EnsureDir(resDir);
        var so = LoadOrCreate<DeckDatabase>($"{resDir}/DeckDatabase.asset");
        so.decks = decks;
        EditorUtility.SetDirty(so);

        AssetDatabase.SaveAssets();
        Debug.Log($"[PokeChess] 견본덱 {decks.Count}개 Import 완료 (DeckDatabase 단일 에셋 갱신)");
    }

    /// <summary>PokemonDatabase에서 도감 id 집합 로드 (Deck pokemonId 검증용). 미임포트면 빈 집합.</summary>
    private static HashSet<int> LoadPokemonIdSet()
    {
        var set = new HashSet<int>();
        var db = AssetDatabase.LoadAssetAtPath<PokemonDatabase>("Assets/Resources/PokemonDatabase.asset");
        if (db == null || db.all == null)
        {
            Debug.LogWarning("[PokeChess] PokemonDatabase.asset 없음 — Deck pokemonId 검증 생략");
            return set;
        }
        foreach (var p in db.all)
            if (p != null)
                set.Add(p.id);
        return set;
    }

    // ──────────────────────────────────────────
    // 유틸
    // ──────────────────────────────────────────

    private static void ValidateRewardRefs(RewardJsonDb rewardDb)
    {
        if (rewardDb == null || rewardDb.tables == null)
            return;

        var itemNames = LoadItemNameSet();
        var consumableNames = LoadConsumableNameSet();
        var stoneNames = LoadStoneNameSet();

        foreach (var table in rewardDb.tables)
        {
            if (table == null || table.rewards == null)
                continue;

            foreach (var reward in table.rewards)
            {
                if (reward == null)
                    continue;

                // gold, itemCoupon, augment처럼 refNameEn이 필요 없는 보상은 제외
                if (string.IsNullOrWhiteSpace(reward.refNameEn))
                    continue;

                string kind = reward.kind ?? "";
                string refNameEn = reward.refNameEn.Trim();

                bool found = false;

                switch (kind)
                {
                    case "item":
                        found = itemNames.Contains(refNameEn);
                        break;

                    case "consumable":
                        found = consumableNames.Contains(refNameEn);
                        break;

                    case "stone":
                    case "evolutionStone":
                    case "evolution_stone":
                        found = stoneNames.Contains(refNameEn);
                        break;

                    default:
                        Debug.LogWarning($"[PokeChess] Reward ref 검증 대상이 아닌 kind입니다: table={table.rewardTableId}, kind={kind}, refNameEn={refNameEn}");
                        continue;
                }

                if (found)
                {
                    Debug.Log($"[PokeChess] Reward ref 확인 OK: table={table.rewardTableId}, kind={kind}, refNameEn={refNameEn}");
                }
                else
                {
                    Debug.LogWarning($"[PokeChess] Reward ref 찾기 실패: table={table.rewardTableId}, kind={kind}, refNameEn={refNameEn}");
                }
            }
        }
    }

    private static HashSet<string> LoadItemNameSet()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var json = Resources.Load<TextAsset>("Data/item_data");
        if (json == null)
        {
            Debug.LogWarning("[PokeChess] item_data.json 없음 — Reward item ref 검증 생략");
            return set;
        }

        var jsonText = json.text.TrimStart();
        if (jsonText.StartsWith("["))
            jsonText = $"{{\"items\":{jsonText}}}";

        var db = JsonUtility.FromJson<ItemJsonDb>(jsonText);
        if (db?.items == null)
            return set;

        foreach (var item in db.items)
        {
            if (item != null && !string.IsNullOrWhiteSpace(item.nameEn))
                set.Add(item.nameEn.Trim());
        }

        return set;
    }

    private static HashSet<string> LoadConsumableNameSet()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var json = Resources.Load<TextAsset>("Data/consumable_data");
        if (json == null)
        {
            Debug.LogWarning("[PokeChess] consumable_data.json 없음 — Reward consumable ref 검증 생략");
            return set;
        }

        var db = JsonUtility.FromJson<ConsumableDatabase>(json.text);
        if (db?.consumables == null)
            return set;

        foreach (var consumable in db.consumables)
        {
            if (consumable != null && !string.IsNullOrWhiteSpace(consumable.nameEn))
                set.Add(consumable.nameEn.Trim());
        }

        return set;
    }

    private static HashSet<string> LoadStoneNameSet()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var json = Resources.Load<TextAsset>("Data/evolution_stone_data");
        if (json == null)
        {
            Debug.LogWarning("[PokeChess] evolution_stone_data.json 없음 — Reward stone ref 검증 생략");
            return set;
        }

        var jsonText = json.text.TrimStart();
        if (jsonText.StartsWith("["))
            jsonText = $"{{\"stones\":{jsonText}}}";

        var db = JsonUtility.FromJson<StoneJsonDb>(jsonText);
        if (db?.stones == null)
            return set;

        foreach (var stone in db.stones)
        {
            if (stone != null && !string.IsNullOrWhiteSpace(stone.nameEn))
                set.Add(stone.nameEn.Trim());
        }

        return set;
    }

    private static Dictionary<string, TrainerEntryJson> LoadTrainerEntryMap(TrainerEntryDiagnostics diagnostics)
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

            if (map.ContainsKey(trainer.trainerId))
            {
                diagnostics.RecordDuplicateTrainerId(trainer.trainerId);
                continue;
            }

            map.Add(trainer.trainerId, trainer);
        }

        Debug.Log($"[PokeChess] trainer_entry_data {map.Count}개 로드 완료");
        return map;
    }

    private static void LogTrainerEntryDiagnostics(
        TrainerEntryDiagnostics diagnostics,
        IEnumerable<string> allTrainerIds)
    {
        foreach (var message in diagnostics.BuildReport(allTrainerIds))
        {
            switch (message.Severity)
            {
                case TrainerEntryIssueSeverity.Error:
                    Debug.LogError($"[PokeChess] {message.Text}");
                    break;
                case TrainerEntryIssueSeverity.Warning:
                    Debug.LogWarning($"[PokeChess] {message.Text}");
                    break;
                default:
                    Debug.Log($"[PokeChess] {message.Text}");
                    break;
            }
        }
    }

    private static void AddEnemies(StageData stage, List<EnemyPlacementJson> sourceEnemies)
    {
        if (stage == null || sourceEnemies == null)
            return;

        foreach (var enemy in sourceEnemies)
        {
            if (enemy == null)
                continue;

            int q = enemy.q;
            int r = enemy.r;

            // 신규 스키마: slot(1~6)이 있으면 StageLayout 기준으로 q/r 자동 변환
            if (enemy.slot >= StageLayout.MinSlot && enemy.slot <= StageLayout.MaxSlot)
            {
                HexCoords hex = StageLayout.SlotToHex(enemy.slot);
                q = hex.q;
                r = hex.r;
            }

            stage.enemies.Add(new EnemyPlacement
            {
                pokemonNameEn = enemy.pokemonNameEn ?? "",
                pokemonNameKr = enemy.pokemonNameKr ?? "",
                starLevel = enemy.starLevel <= 0 ? 1 : enemy.starLevel,
                q = q,
                r = r,
                heldItemEn = NormalizeItemName(enemy.heldItemEn),
                heldItemEn2 = NormalizeItemName(enemy.heldItemEn2),
                statMultiplier = NormalizeMultiplier(enemy.statMultiplier),
                hpMultiplier = NormalizeMultiplier(enemy.hpMultiplier),
                atkMultiplier = NormalizeMultiplier(enemy.atkMultiplier)
            });
        }
    }

    private static RewardKind ParseRewardKind(string s) => s switch
    {
        "gold" => RewardKind.Gold,
        "itemCoupon" or "coupon" or "item_coupon" => RewardKind.ItemCoupon,
        "reroll" => RewardKind.Reroll,
        "item" => RewardKind.Item,
        "consumable" => RewardKind.Consumable,
        "stone" or "evolutionStone" => RewardKind.EvolutionStone,
        "unit" => RewardKind.Unit,
        "augment" or "augmentChoice" => RewardKind.AugmentChoice,
        "itemShopReroll" or "item_shop_reroll" => RewardKind.ItemShopReroll,
        "reforger" => RewardKind.Reforger,
        _ => RewardKind.Gold,
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

    private static void ResetItemStats(ItemData so)
    {
        so.hpBonus = 0f;
        so.maxHpPct = 0f;
        so.hpRegenPercent = 0f;
        so.healTakenDmgPct = 0f;
        so.shieldPctOnFatalHit = 0f;
        so.attackBonus = 0f;
        so.attackPct = 0f;
        so.spAtkPct = 0f;
        so.spellPowerBonus = 0f;
        so.attackSpeedBonus = 0f;
        so.moveSpdPctOnKill = 0f;
        so.defenseBonus = 0f;
        so.defensePct = 0f;
        so.spDefBonus = 0f;
        so.reflectPhysPct = 0f;
        so.reflectSpPct = 0f;
        so.defSpDefPerAttacker = 0f;
        so.criPct = 0f;
        so.criDmgPct = 0f;
        so.manaRegenBonus = 0f;
        so.hpConditionalSpAtkPct = 0f;
        so.combatStartShieldPct = 0f;
        so.shieldDuration = 0f;
        so.onShieldEndSkillDamageAmpPct = 0f;
        so.hpThresholdPct = 0f;
        so.thresholdShieldPct = 0f;
        so.shieldDecayDuration = 0f;
        so.periodicInterval = 0f;
        so.periodicSkillDamageAmpPct = 0f;
        so.periodicAttackSpeedPct = 0f;
        so.basicAttackDamageAmpPerStackPct = 0f;
        so.skillDamageAmpPerStackPct = 0f;
        so.maxCombatStacks = 0f;
        so.maxStackDamageAmpPct = 0f;
        so.maxStackAttackSpeedPct = 0f;
        so.gainStackOnTakeDamage = false;
        so.damageAmpPct = 0f;
        so.skillDamageAmpPct = 0f;
        so.healLowestAllyPctOfDamage = 0f;
        so.adjacentAllyAttackSpeedPct = 0f;
        so.adjacentAllySpellPowerBonus = 0f;
        so.adjacentAllyManaBonus = 0f;
        so.burnNearOnPhysHit = false;
        so.ccImmune = false;
    }

    /// <summary>
    /// GeneralItem 시트의 공통 약어를 현재 런타임 스탯에 안전하게 투영한다.
    /// 현재 런타임에 대응 필드가 없거나 단위가 맞지 않는 값은 sourceStats에만 보존한다.
    /// </summary>
    private static void ApplyGeneralItemStat(ItemData so, string key, float value, bool isPercent)
    {
        switch (key.ToUpperInvariant())
        {
            case "HP" when !isPercent:  so.hpBonus += value; break;
            case "HP" when isPercent:   so.maxHpPct += value; break;
            case "AD" when !isPercent:  so.attackBonus += value; break;
            case "AD" when isPercent:   so.attackPct += value; break;
            case "AP" when isPercent:   so.spAtkPct += value; break;
            case "AP" when !isPercent:  so.spellPowerBonus += value; break;
            case "AS" when isPercent:   so.attackSpeedBonus += value; break;
            case "DEF" when !isPercent: so.defenseBonus += value; break;
            case "DEF" when isPercent:  so.defensePct += value; break;
            case "CRT" when isPercent:  so.criPct += value; break;
            case "MP" when !isPercent:  so.manaRegenBonus += value; break;
            case "AMP" when isPercent:  so.damageAmpPct += value; break; // 치리열매/야타비열매 등 상시 공용 피해증폭

            // 아래 4개는 GeneralItem 8종 공통 약어(HP/AD/AP/AS/DEF/CRT/MP/AMP)가 아니라, 개별 아이템의
            // "고유 효과" 수치를 그대로 필드명으로 받는 전용 키다(티라프열매/대파/랑사열매/기합의머리띠,
            // 2026-08 기획 확정 1차분). isPercent는 값의 성격이 필드명에 이미 내포돼 있어 참조하지 않는다.
            case "DEFSPDEFPERATTACKER":   so.defSpDefPerAttacker += value; break;   // 티라프열매 — 나를 타겟 중인 적 1명당 DEF+N
            case "CRIDMGPCT":             so.criDmgPct += value; break;             // 대파/랑사열매 — 치명타 피해량 +N%
            case "HEALTAKENDMGPCT":       so.healTakenDmgPct += value; break;       // 기합의머리띠 — 받은 최종 피해량의 N% 회복
            case "HPCONDITIONALSPATKPCT": so.hpConditionalSpAtkPct += value; break; // 기합의머리띠 — HP>50%면 2배가 되는 주문력 N%
            case "HPREGENPERCENT":        so.hpRegenPercent += value; break;        // 먹다남은음식 — 매초 잃은 체력의 N% 회복

            // 아래 9개는 2026-08 기획 확정 2차분(규살열매/조개껍질방울/의문열매/초점렌즈/캄라열매)
            // 보호막·시간 기반 고유효과 전용 키. 위와 같은 원칙(필드명=키, isPercent 미참조).
            case "COMBATSTARTSHIELDPCT":        so.combatStartShieldPct += value; break;        // 규살열매
            case "SHIELDDURATION":              so.shieldDuration += value; break;              // 규살열매/조개껍질방울 공용
            case "ONSHIELDENDSKILLDAMAGEAMPPCT": so.onShieldEndSkillDamageAmpPct += value; break; // 규살열매
            case "HPTHRESHOLDPCT":              so.hpThresholdPct += value; break;               // 조개껍질방울/의문열매 공용
            case "THRESHOLDSHIELDPCT":          so.thresholdShieldPct += value; break;           // 조개껍질방울/의문열매 공용
            case "SHIELDDECAYDURATION":         so.shieldDecayDuration += value; break;          // 의문열매
            case "PERIODICINTERVAL":            so.periodicInterval += value; break;             // 초점렌즈/캄라열매 공용
            case "PERIODICSKILLDAMAGEAMPPCT":   so.periodicSkillDamageAmpPct += value; break;     // 초점렌즈
            case "PERIODICATTACKSPEEDPCT":      so.periodicAttackSpeedPct += value; break;        // 캄라열매

            // 아래 6개는 2026-08 기획 확정 3차분(왕의징표석/선제공격손톱) 스택형 고유효과 전용 키.
            case "BASICATTACKDAMAGEAMPPERSTACKPCT": so.basicAttackDamageAmpPerStackPct += value; break; // 왕의징표석/선제공격손톱 공용
            case "SKILLDAMAGEAMPPERSTACKPCT":       so.skillDamageAmpPerStackPct += value; break;       // 왕의징표석
            case "MAXCOMBATSTACKS":                 so.maxCombatStacks += value; break;                 // 왕의징표석/선제공격손톱 공용
            case "MAXSTACKDAMAGEAMPPCT":            so.maxStackDamageAmpPct += value; break;            // 왕의징표석
            case "MAXSTACKATTACKSPEEDPCT":          so.maxStackAttackSpeedPct += value; break;          // 선제공격손톱
            case "GAINSTACKONTAKEDAMAGE":           so.gainStackOnTakeDamage = value > 0; break;        // 왕의징표석

            // 아래 2개는 2026-08 기획 확정 4차분(삐삐인형/큰뿌리) 상시/기본형 고유효과 전용 키.
            case "SKILLDAMAGEAMPPCT":           so.skillDamageAmpPct += value; break;           // 삐삐인형 — 상시 스킬피해 N%
            case "HEALLOWESTALLYPCTOFDAMAGE":   so.healLowestAllyPctOfDamage += value; break;    // 큰뿌리

            // 아래 3개는 2026-08 기획 확정 5차분(구애머리띠/라즈열매) 전투 시작 인접 아군 오라 전용 키.
            case "ADJACENTALLYATTACKSPEEDPCT":  so.adjacentAllyAttackSpeedPct += value; break;   // 구애머리띠
            case "ADJACENTALLYSPELLPOWERBONUS": so.adjacentAllySpellPowerBonus += value; break;  // 라즈열매
            case "ADJACENTALLYMANABONUS":       so.adjacentAllyManaBonus += value; break;        // 라즈열매

            // VMP(오마니뱀프)는 최종 기획에서 별도 전투 훅이 필요해 후속 작업으로 분리 — 이번 범위 밖.
            // flat CRT 등 현재 데이터에 없는 조합도 미지원.
            default:
                Debug.LogWarning($"[PokeChess] 현재 런타임 미지원 GeneralItem 스탯: {key}={value}{(isPercent ? "%" : "")}");
                break;
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

    /// <summary>skill_table.json 로드 → skillId(IgnoreCase) → 행 맵. 없으면 빈 맵(평타만).</summary>
    private static Dictionary<string, SkillTableEntry> LoadSkillTableMap()
    {
        var map = new Dictionary<string, SkillTableEntry>(StringComparer.OrdinalIgnoreCase);
        var json = Resources.Load<TextAsset>("Data/skill_table");
        if (json == null) { Debug.LogWarning("[PokeChess] skill_table.json 없음 — 스킬 없이 임포트(평타만)"); return map; }

        var db = JsonUtility.FromJson<SkillTableJsonDb>(json.text);
        if (db?.skills == null) { Debug.LogWarning("[PokeChess] skill_table.json 파싱 실패"); return map; }

        foreach (var s in db.skills)
            if (s != null && !string.IsNullOrEmpty(s.skillId))
                map[s.skillId] = s;
        Debug.Log($"[PokeChess] skill_table {map.Count}개 로드");
        return map;
    }

    /// <summary>
    /// 평타 사거리를 attackVfxId로 Skill Table의 ATTACK 행에서 찾는다(lineLength).
    /// 행이 없으면 접미사 규약(_L=원거리 4 / _S=근거리 1)으로 폴백하고 경고를 남긴다 —
    /// 조용히 1로 떨어지면 원거리 유닛이 적에게 붙어 싸우는데도 원인을 알기 어렵다.
    /// </summary>
    private static int ResolveAttackRange(string attackVfxId, Dictionary<string, SkillTableEntry> map, string pokemonName)
    {
        const int RANGED = 4, MELEE = 1;

        if (string.IsNullOrEmpty(attackVfxId))
        {
            Debug.LogWarning($"[PokeChess] {pokemonName}: attackVfxId 없음 → 평타 사거리 {MELEE}로 폴백");
            return MELEE;
        }

        if (map.TryGetValue(attackVfxId, out var row) && row.lineLength > 0)
            return row.lineLength;

        int fallback = attackVfxId.EndsWith("_L", StringComparison.OrdinalIgnoreCase) ? RANGED : MELEE;
        Debug.LogWarning(
            $"[PokeChess] {pokemonName}: Skill Table에 '{attackVfxId}' ATTACK 행이 없음 " +
            $"→ 접미사 규약으로 사거리 {fallback} 폴백. 시트에 행을 추가할 것");
        return fallback;
    }

    /// <summary>skillId로 Skill Table 행을 찾아 PokemonSkillData 생성. 없으면 빈 스킬(HasSkill=false).</summary>
    private static PokemonSkillData BuildSkill(string skillId, Dictionary<string, SkillTableEntry> map)
    {
        if (string.IsNullOrEmpty(skillId) || !map.TryGetValue(skillId, out var s))
            return new PokemonSkillData(); // skillId 빈/미발견 → 평타만

        return new PokemonSkillData
        {
            skillId     = s.skillId,
            skillName   = s.skillName,
            description = s.description,
            effectType  = ParseEffectType(s.effectType),
            targetType  = ParseTargetType(s.targetType),
            areaRadius  = s.areaRadius,
            lineLength  = s.lineLength,
            vfxId       = s.vfxId
        };
    }

    private static SkillEffectType ParseEffectType(string s)
        => Enum.TryParse(SnakeToPascal(s), true, out SkillEffectType r) ? r : SkillEffectType.Spell;

    private static SkillTargetType ParseTargetType(string s)
        => Enum.TryParse(SnakeToPascal(s), true, out SkillTargetType r) ? r : SkillTargetType.EnemySingle;

    /// <summary>"ENEMY_AREA"/"HP_REGEN" → "EnemyArea"/"HpRegen" (Enum.TryParse용).</summary>
    private static string SnakeToPascal(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var parts = s.Split('_');
        for (int i = 0; i < parts.Length; i++)
            parts[i] = parts[i].Length == 0 ? "" :
                char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1).ToLowerInvariant();
        return string.Concat(parts);
    }

    private static StageType ParseStageType(string s)
    {
        if (Enum.TryParse(s, true, out StageType result))
            return result;

        Debug.LogWarning($"[PokeChess] 알 수 없는 StageType: {s}, WildCommon으로 처리");
        return StageType.WildCommon;
    }

    private static int ResolveChapter(StageEntry e)
    {
        if (e.chapter > 0)
            return e.chapter;

        if (e.stage > 0)
            return e.stage;

        return 1;
    }

    private static string ResolveStageId(StageEntry e, int chapter, int round)
    {
        if (!string.IsNullOrEmpty(e.stageId))
            return e.stageId;

        return $"{chapter}-{round}";
    }

    private static int ResolveOrder(StageEntry e, int fallbackOrder)
    {
        if (e.order > 0)
            return e.order;

        int chapter = ResolveChapter(e);

        if (chapter > 0 && e.round > 0)
            return ((chapter - 1) * 100) + e.round;

        return fallbackOrder;
    }

    private static string ResolveRewardTableId(StageEntry e)
    {
        string raw = !string.IsNullOrEmpty(e.rewardTableId)
            ? e.rewardTableId
            : e.rewardId;

        if (string.IsNullOrEmpty(raw))
            return "";

        raw = raw.Trim();

        if (raw.StartsWith("RW", StringComparison.OrdinalIgnoreCase))
            return raw.ToUpperInvariant();

        if (int.TryParse(raw, out int number))
            return $"RW{number:000}";

        return raw;
    }

    private static StageType ResolveStageType(StageEntry e)
    {
        // 기존 stage_data.json에 stageType이 있으면 기존 값 우선 사용
        if (!string.IsNullOrEmpty(e.stageType) &&
            Enum.TryParse(e.stageType, true, out StageType parsed))
        {
            return parsed;
        }

        string battleType = e.battleType ?? "";
        string trainerId = e.trainerId ?? "";

        string key = $"{battleType} {trainerId}".ToLowerInvariant();

        if (key.Contains("champ") || key.Contains("champion") || key.Contains("boss"))
            return StageType.ChampionBattle;

        if (key.Contains("gym") || key.Contains("trainer"))
            return StageType.GymBattle;

        if (key.Contains("rare"))
            return StageType.WildRare;

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

    /// <summary>
    /// 시트의 아이템 칸을 정규화. 기획 시트는 빈 칸을 "NONE"으로 적으므로 그대로 두면
    /// ItemDatabase 조회가 실패해 매 전투마다 경고가 찍힌다. 빈 문자열로 바꿔 "없음"으로 취급한다.
    /// </summary>
    private static string NormalizeItemName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        string trimmed = value.Trim();
        return trimmed.Equals("NONE", StringComparison.OrdinalIgnoreCase) ? "" : trimmed;
    }
}


#endif
