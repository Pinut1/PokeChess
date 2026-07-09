using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전체 PokemonData를 모아 영문명/도감번호로 조회하는 중앙 레지스트리(SO 1개).
/// 싱글턴/이름맵 보일러플레이트는 NamedScriptableDatabase 베이스가 담당하고,
/// 도감번호(id) 보조 인덱스만 이 클래스에서 확장한다.
///
/// 채우기: PokeChessImporter의 "Import Pokemon JSON"이 all을 자동 갱신 → 손으로 드래그 불필요.
/// 읽기:  런타임은 PokemonDatabase.Instance (Resources/PokemonDatabase.asset 1회 로드) 로 접근.
/// </summary>
[CreateAssetMenu(menuName = "PokeChess/PokemonDatabase", fileName = "PokemonDatabase")]
public class PokemonDatabase : NamedScriptableDatabase<PokemonDatabase, PokemonData>
{
    [Tooltip("임포터(Import Pokemon JSON)가 자동으로 채움. 수동 편집 비권장.")]
    public List<PokemonData> all = new();

    private Dictionary<int, PokemonData> _byId;

    protected override IReadOnlyList<PokemonData> Items => all;

    // 대소문자 무시 매칭(안전망): 기획 시트 pokemonId가 UPPER_SNAKE/소문자로 와도
    // DB의 PascalCase(nameEn)과 매칭되도록. 안 그러면 적이 조용히 누락됨.
    protected override IEnumerable<string> KeysOf(PokemonData p) { yield return p.pokemonNameEn; }

    // 도감번호 인덱스는 이름 맵과 함께 빌드.
    protected override void BuildExtraIndexes()
    {
        _byId = new Dictionary<int, PokemonData>();
        foreach (var p in all)
            if (p != null) _byId[p.id] = p;
    }

    protected override void ClearCache()
    {
        base.ClearCache();
        _byId = null;
    }

    /// <summary>영문명으로 조회. 없으면 null.</summary>
    public PokemonData GetByNameEn(string nameEn) => Lookup(nameEn);

    /// <summary>도감번호(id)로 조회. 없으면 null.</summary>
    public PokemonData GetById(int id)
    {
        EnsureMap();
        return _byId.TryGetValue(id, out var d) ? d : null;
    }
}
