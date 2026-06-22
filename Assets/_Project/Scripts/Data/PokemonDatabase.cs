using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전체 PokemonData를 모아 영문명/도감번호로 조회하는 중앙 레지스트리(SO 1개).
/// 매니저가 PokemonData 리스트를 각자 인스펙터로 들고 있는 중복을 없앤다.
///
/// 채우기: PokeChessImporter의 "Import Pokemon JSON"이 all을 자동 갱신 → 손으로 드래그 불필요.
/// 읽기:  런타임은 PokemonDatabase.Instance (Resources/PokemonDatabase.asset 1회 로드) 로 접근.
/// </summary>
[CreateAssetMenu(menuName = "PokeChess/PokemonDatabase", fileName = "PokemonDatabase")]
public class PokemonDatabase : ScriptableObject
{
    [Tooltip("임포터(Import Pokemon JSON)가 자동으로 채움. 수동 편집 비권장.")]
    public List<PokemonData> all = new();

    private Dictionary<string, PokemonData> _byName;
    private Dictionary<int, PokemonData> _byId;

    // 런타임 단일 접근. Resources 루트에 단 하나의 DB 에셋만 두면 됨(다른 SO는 이동 불필요).
    private static PokemonDatabase _instance;
    public static PokemonDatabase Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<PokemonDatabase>("PokemonDatabase");
            if (_instance == null)
                Debug.LogError("[PokemonDatabase] Resources/PokemonDatabase.asset 없음 — 'Import Pokemon JSON' 먼저 실행");
            return _instance;
        }
    }

    /// <summary>영문명으로 조회. 없으면 null.</summary>
    public PokemonData GetByNameEn(string nameEn)
    {
        if (string.IsNullOrEmpty(nameEn)) return null;
        EnsureMaps();
        return _byName.TryGetValue(nameEn, out var d) ? d : null;
    }

    /// <summary>도감번호(id)로 조회. 없으면 null.</summary>
    public PokemonData GetById(int id)
    {
        EnsureMaps();
        return _byId.TryGetValue(id, out var d) ? d : null;
    }

    private void EnsureMaps()
    {
        if (_byName != null) return;

        // 대소문자 무시 매칭(안전망): 기획 시트 pokemonId가 UPPER_SNAKE/소문자로 와도
        // DB의 PascalCale(nameEn)과 매칭되도록. 안 그러면 적이 조용히 누락됨.
        _byName = new Dictionary<string, PokemonData>(System.StringComparer.OrdinalIgnoreCase);
        _byId   = new Dictionary<int, PokemonData>();
        foreach (var p in all)
        {
            if (p == null) continue;
            if (!string.IsNullOrEmpty(p.pokemonNameEn)) _byName[p.pokemonNameEn] = p;
            _byId[p.id] = p;
        }
    }

    // 핫리로드/임포트 후 캐시 무효화.
    private void OnEnable()  => InvalidateCache();
    private void OnDisable() => InvalidateCache();
    public void InvalidateCache() { _byName = null; _byId = null; }
}
