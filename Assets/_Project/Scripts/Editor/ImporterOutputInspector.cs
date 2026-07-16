using UnityEditor;
using UnityEngine;

/// <summary>
/// 임포터 산출물 SO의 공용 인스펙터 — "단일 진실 공급원은 구글 시트/JSON"을 에디터가 강제한다.
/// 필드를 잠그고(읽기 전용) 경고 배너 + [JSON 열기]/[임포트 실행] 버튼을 제공.
/// 수치를 고치려면 시트/JSON을 수정하고 임포트를 다시 돌리는 것이 유일한 정규 경로.
/// (VfxDatabase는 아트가 직접 편집하는 DB라 대상에서 제외 — 규약상 에디터 편집이 정답.)
/// </summary>
public abstract class ImporterOutputEditor : Editor
{
    /// <summary>이 SO를 재생성하는 임포트 메뉴 경로.</summary>
    protected abstract string ImportMenu { get; }

    /// <summary>원본 JSON 에셋 경로 (Assets/ 기준).</summary>
    protected abstract string JsonPath { get; }

    // 비상용 잠금 해제 — 세션 한정(도메인 리로드 시 다시 잠김). 어차피 다음 임포트 때 덮어써짐을 경고.
    private static bool _unlocked;

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "임포터 산출물 — 원본(단일 진실 공급원)은 구글 시트 → JSON입니다.\n" +
            "여기서 고친 값은 다음 임포트 때 덮어써집니다. 수치 조정은 시트/JSON 수정 → 임포트 실행으로.",
            MessageType.Warning);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("원본 JSON 열기"))
            {
                var json = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
                if (json != null) AssetDatabase.OpenAsset(json);
                else Debug.LogWarning($"[Importer] JSON 없음: {JsonPath}");
            }

            if (GUILayout.Button("임포트 실행"))
                EditorApplication.ExecuteMenuItem(ImportMenu);
        }

        _unlocked = EditorGUILayout.ToggleLeft(
            "비상 편집 잠금 해제 (임시 실험용 — 임포트 시 덮어써짐)", _unlocked);

        using (new EditorGUI.DisabledScope(!_unlocked))
        {
            DrawDefaultInspector();
        }
    }
}

[CustomEditor(typeof(PokemonData)), CanEditMultipleObjects]
public class PokemonDataEditor : ImporterOutputEditor
{
    protected override string ImportMenu => "PokeChess/Import Pokemon JSON";
    protected override string JsonPath   => "Assets/Resources/Data/pokemon_data.json";
}

[CustomEditor(typeof(ItemData)), CanEditMultipleObjects]
public class ItemDataEditor : ImporterOutputEditor
{
    protected override string ImportMenu => "PokeChess/Import Item JSON";
    protected override string JsonPath   => "Assets/Resources/Data/item_data.json";
}

[CustomEditor(typeof(ConsumableData)), CanEditMultipleObjects]
public class ConsumableDataEditor : ImporterOutputEditor
{
    protected override string ImportMenu => "PokeChess/Import Consumable JSON";
    protected override string JsonPath   => "Assets/Resources/Data/consumable_data.json";
}

[CustomEditor(typeof(EvolutionStoneData)), CanEditMultipleObjects]
public class EvolutionStoneDataEditor : ImporterOutputEditor
{
    protected override string ImportMenu => "PokeChess/Import EvolutionStone JSON";
    protected override string JsonPath   => "Assets/Resources/Data/evolution_stone_data.json";
}

[CustomEditor(typeof(SynergyData)), CanEditMultipleObjects]
public class SynergyDataEditor : ImporterOutputEditor
{
    protected override string ImportMenu => "PokeChess/Import Synergy JSON";
    protected override string JsonPath   => "Assets/Resources/Data/synergy_data.json";
}

[CustomEditor(typeof(RewardData)), CanEditMultipleObjects]
public class RewardDataEditor : ImporterOutputEditor
{
    protected override string ImportMenu => "PokeChess/Import Reward JSON";
    protected override string JsonPath   => "Assets/Resources/Data/reward_data.json";
}

[CustomEditor(typeof(StageData)), CanEditMultipleObjects]
public class StageDataEditor : ImporterOutputEditor
{
    protected override string ImportMenu => "PokeChess/Import Stage JSON";
    protected override string JsonPath   => "Assets/Resources/Data/stage_data.json";
}

[CustomEditor(typeof(TradeEvolutionData)), CanEditMultipleObjects]
public class TradeEvolutionDataEditor : ImporterOutputEditor
{
    protected override string ImportMenu => "PokeChess/Import TradeEvolution JSON";
    protected override string JsonPath   => "Assets/Resources/Data/trade_evolution_data.json";
}

[CustomEditor(typeof(DeckDatabase)), CanEditMultipleObjects]
public class DeckDatabaseEditor : ImporterOutputEditor
{
    protected override string ImportMenu => "PokeChess/Import Deck JSON";
    protected override string JsonPath   => "Assets/Resources/Data/deck_data.json";
}
