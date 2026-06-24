using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전체 ItemData를 모아 영문명으로 조회하는 중앙 레지스트리(SO 1개). PokemonDatabase와 동일 패턴.
///
/// 채우기: PokeChessImporter의 "Import Item JSON"이 all을 자동 갱신.
/// 읽기:  런타임은 ItemDatabase.Instance (Resources/ItemDatabase.asset 1회 로드) 로 접근.
/// </summary>
[CreateAssetMenu(menuName = "PokeChess/ItemDatabase", fileName = "ItemDatabase")]
public class ItemDatabase : ScriptableObject
{
    [Tooltip("임포터(Import Item JSON)가 자동으로 채움. 수동 편집 비권장.")]
    public List<ItemData> all = new();

    private Dictionary<string, ItemData> _byName;

    private static ItemDatabase _instance;
    public static ItemDatabase Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<ItemDatabase>("ItemDatabase");
            if (_instance == null)
                Debug.LogError("[ItemDatabase] Resources/ItemDatabase.asset 없음 — 'Import Item JSON' 먼저 실행");
            return _instance;
        }
    }

    /// <summary>영문명으로 조회(대소문자 무관). 없으면 null.</summary>
    public ItemData GetByNameEn(string nameEn)
    {
        if (string.IsNullOrEmpty(nameEn)) return null;
        EnsureMap();
        return _byName.TryGetValue(nameEn, out var d) ? d : null;
    }

    private void EnsureMap()
    {
        if (_byName != null) return;
        _byName = new Dictionary<string, ItemData>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var item in all)
            if (item != null && !string.IsNullOrEmpty(item.itemNameEn))
                _byName[item.itemNameEn] = item;
    }

    private void OnEnable()  => InvalidateCache();
    private void OnDisable() => InvalidateCache();
    public void InvalidateCache() => _byName = null;
}
