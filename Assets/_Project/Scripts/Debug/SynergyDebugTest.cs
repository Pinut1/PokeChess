#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SynergyManager 플레이 모드 검증용.
/// 포켓몬 에셋이 아직 없으므로 PokemonData를 런타임 생성해서 보드에 배치하며 시너지 재계산을 확인.
/// </summary>
public class SynergyDebugTest : MonoBehaviour
{
    private int _nextSpeciesId = 9001;       // 테스트 종 id (실데이터와 안 겹치게)
    private PokemonUnit _lastPlaced;
    private readonly List<PokemonUnit> _spawned = new();

    private void OnEnable()  => GameEvents.OnSynergyUpdated += LogSynergyStatuses;
    private void OnDisable() => GameEvents.OnSynergyUpdated -= LogSynergyStatuses;

    private void OnGUI()
    {
        if (GUI.Button(new Rect(10, 70, 200, 40), "전기 유닛 배치 (새 종)"))
            PlaceTestUnit(_nextSpeciesId++, "전기");

        if (GUI.Button(new Rect(10, 115, 200, 40), "전기 유닛 배치 (중복 종)"))
        {
            if (_lastPlaced != null) PlaceTestUnit(_lastPlaced.data.id, "전기");
            else Debug.LogWarning("[SynergyTest] 먼저 새 종을 배치하세요");
        }

        if (GUI.Button(new Rect(10, 160, 200, 40), "마지막 유닛 벤치로"))
        {
            if (_lastPlaced != null && GameManager.Instance.Board.TryPlaceInBench(_lastPlaced))
            {
                // 벤치 타일은 없으므로 보드 옆쪽으로 시각적으로만 이동
                _lastPlaced.transform.position = new Vector3(-3f, 0.5f, -5f);
            }
        }

        if (GUI.Button(new Rect(10, 205, 200, 40), "오타 시너지 유닛 배치"))
            PlaceTestUnit(_nextSpeciesId++, "전긔"); // 미등록 문자열 경고 확인용

        if (GUI.Button(new Rect(10, 250, 200, 40), "시너지 상태 출력"))
            LogSynergyStatuses();
    }

    private void PlaceTestUnit(int speciesId, string synergy)
    {
        var data = ScriptableObject.CreateInstance<PokemonData>();
        data.id = speciesId;
        data.pokemonName = $"테스트몬{speciesId}";
        data.hp = 100; data.attack = 10;
        data.synergies = new List<string> { synergy };

        var go = new GameObject($"TestUnit_{speciesId}");
        var unit = go.AddComponent<PokemonUnit>();
        unit.data = data;
        _spawned.Add(unit);

        // 보이는 실린더 (실제 모델 없으니 placeholder)
        var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.transform.SetParent(go.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);

        if (FindEmptyTile(out HexCoords coords))
        {
            GameManager.Instance.Board.TryPlaceUnit(unit, coords);
            _lastPlaced = unit;
            MoveUnitToTile(unit, coords);
            Debug.Log($"[SynergyTest] {data.pokemonName}({synergy}) 배치 → {coords}");
        }
        else
        {
            Debug.LogWarning("[SynergyTest] 빈 타일 없음");
            Destroy(go);
        }
    }

    private static bool FindEmptyTile(out HexCoords coords)
    {
        foreach (var kv in GameManager.Instance.Board.GetBoardSnapshot())
        {
            if (kv.Value == null)
            {
                coords = kv.Key;
                return true;
            }
        }
        coords = default;
        return false;
    }

    /// <summary>해당 좌표의 HexTile을 씬에서 찾아 그 위치 + 높이 오프셋으로 유닛을 이동시킴.</summary>
    private static void MoveUnitToTile(PokemonUnit unit, HexCoords coords)
    {
        foreach (var tile in FindObjectsByType<HexTile>(FindObjectsSortMode.None))
        {
            if (tile.GetCoords() == coords)
            {
                unit.transform.position = tile.transform.position + Vector3.up * 0.5f;
                return;
            }
        }
        Debug.LogWarning($"[SynergyTest] {coords}에 해당하는 HexTile을 찾지 못함");
    }

    private static void LogSynergyStatuses()
    {
        var statuses = GameManager.Instance.Synergy.GetAllSynergyStatuses();
        if (statuses.Count == 0)
        {
            Debug.Log("[SynergyTest] 카운트된 시너지 없음");
            return;
        }

        foreach (var s in statuses)
        {
            string tierInfo = s.IsActive
                ? $"활성 (티어 {s.activeTierIndex + 1}: {s.ActiveTier.effectDescription})"
                : "비활성";
            Debug.Log($"[SynergyTest] {s.data.synergyName}({s.data.synergyNameEn}) x{s.uniqueCount} — {tierInfo}");
        }
    }
}
#endif
