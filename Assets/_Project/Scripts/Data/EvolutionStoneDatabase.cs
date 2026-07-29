using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전체 EvolutionStoneData를 모아 영문명으로 조회하는 중앙 레지스트리(SO 1개).
/// 싱글턴/이름맵 보일러플레이트는 NamedScriptableDatabase 베이스가 담당.
///
/// 채우기: PokeChessImporter의 "Import EvolutionStone JSON"이 all을 자동 갱신.
/// 읽기:  런타임은 EvolutionStoneDatabase.Instance (Resources/EvolutionStoneDatabase.asset 1회 로드) 로 접근.
/// </summary>
[CreateAssetMenu(menuName = "PokeChess/EvolutionStoneDatabase", fileName = "EvolutionStoneDatabase")]
public class EvolutionStoneDatabase : NamedScriptableDatabase<EvolutionStoneDatabase, EvolutionStoneData>
{
    [Tooltip("임포터(Import EvolutionStone JSON)가 자동으로 채움. 수동 편집 비권장.")]
    public List<EvolutionStoneData> all = new();

    protected override IReadOnlyList<EvolutionStoneData> Items => all;
    protected override IEnumerable<string> KeysOf(EvolutionStoneData stone) { yield return stone.stoneNameEn; }

    /// <summary>영문명으로 조회(대소문자 무관). 없으면 null.</summary>
    public EvolutionStoneData GetByNameEn(string nameEn) => Lookup(nameEn);

    /// <summary>ID로 진화의 돌 조회. 없으면 null.</summary>
    public EvolutionStoneData GetById(int id)
    {
        return all.Find(stone => stone != null && stone.id == id);
    }
}
