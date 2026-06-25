using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전체 SynergyData를 모아 시너지명(한/영)으로 조회하는 중앙 레지스트리(SO 1개).
/// PokemonDatabase/ItemDatabase와 동일 패턴 — 매니저가 인스펙터로 리스트를 직접 들고 있던 방식을 대체.
///
/// 채우기: PokeChessImporter "Import Synergy JSON"이 all을 자동 갱신 → 손으로 드래그 불필요.
/// 읽기:  런타임은 SynergyDatabase.Instance (Resources/SynergyDatabase.asset 1회 로드) 로 접근.
/// </summary>
[CreateAssetMenu(menuName = "PokeChess/SynergyDatabase", fileName = "SynergyDatabase")]
public class SynergyDatabase : ScriptableObject
{
    [Tooltip("임포터(Import Synergy JSON)가 자동으로 채움. 수동 편집 비권장.")]
    public List<SynergyData> all = new();

    // 한글명 + 영문명 둘 다 키로 등록(PokemonData.synergies가 어느 쪽으로 와도 매칭).
    private Dictionary<string, SynergyData> _byKey;

    private static SynergyDatabase _instance;
    public static SynergyDatabase Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<SynergyDatabase>("SynergyDatabase");
            if (_instance == null)
                Debug.LogError("[SynergyDatabase] Resources/SynergyDatabase.asset 없음 — 'Import Synergy JSON' 먼저 실행");
            return _instance;
        }
    }

    /// <summary>시너지명(한글/영문, 대소문자 무관)으로 조회. 없으면 null.</summary>
    public SynergyData GetByKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        EnsureMap();
        return _byKey.TryGetValue(key, out var d) ? d : null;
    }

    private void EnsureMap()
    {
        if (_byKey != null) return;
        _byKey = new Dictionary<string, SynergyData>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var s in all)
        {
            if (s == null) continue;
            if (!string.IsNullOrEmpty(s.synergyName))   _byKey[s.synergyName]   = s;
            if (!string.IsNullOrEmpty(s.synergyNameEn)) _byKey[s.synergyNameEn] = s;
        }
    }

    private void OnEnable()  => InvalidateCache();
    private void OnDisable() => InvalidateCache();
    public void InvalidateCache() => _byKey = null;
}
