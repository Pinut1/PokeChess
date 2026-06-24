using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전체 EvolutionStoneData를 모아 영문명으로 조회하는 중앙 레지스트리(SO 1개). PokemonDatabase와 동일 패턴.
///
/// 채우기: PokeChessImporter의 "Import EvolutionStone JSON"이 all을 자동 갱신.
/// 읽기:  런타임은 EvolutionStoneDatabase.Instance (Resources/EvolutionStoneDatabase.asset 1회 로드) 로 접근.
/// </summary>
[CreateAssetMenu(menuName = "PokeChess/EvolutionStoneDatabase", fileName = "EvolutionStoneDatabase")]
public class EvolutionStoneDatabase : ScriptableObject
{
    [Tooltip("임포터(Import EvolutionStone JSON)가 자동으로 채움. 수동 편집 비권장.")]
    public List<EvolutionStoneData> all = new();

    private Dictionary<string, EvolutionStoneData> _byName;

    private static EvolutionStoneDatabase _instance;
    public static EvolutionStoneDatabase Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<EvolutionStoneDatabase>("EvolutionStoneDatabase");
            if (_instance == null)
                Debug.LogError("[EvolutionStoneDatabase] Resources/EvolutionStoneDatabase.asset 없음 — 'Import EvolutionStone JSON' 먼저 실행");
            return _instance;
        }
    }

    /// <summary>영문명으로 조회(대소문자 무관). 없으면 null.</summary>
    public EvolutionStoneData GetByNameEn(string nameEn)
    {
        if (string.IsNullOrEmpty(nameEn)) return null;
        EnsureMap();
        return _byName.TryGetValue(nameEn, out var d) ? d : null;
    }

    private void EnsureMap()
    {
        if (_byName != null) return;
        _byName = new Dictionary<string, EvolutionStoneData>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var stone in all)
            if (stone != null && !string.IsNullOrEmpty(stone.stoneNameEn))
                _byName[stone.stoneNameEn] = stone;
    }

    private void OnEnable()  => InvalidateCache();
    private void OnDisable() => InvalidateCache();
    public void InvalidateCache() => _byName = null;
}
