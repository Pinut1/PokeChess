using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전체 SynergyData를 모아 시너지명(한/영)으로 조회하는 중앙 레지스트리(SO 1개).
/// 싱글턴/이름맵 보일러플레이트는 NamedScriptableDatabase 베이스가 담당.
/// 한글명+영문명 둘 다 키로 등록(PokemonData.synergies가 어느 쪽으로 와도 매칭).
///
/// 채우기: PokeChessImporter "Import Synergy JSON"이 all을 자동 갱신 → 손으로 드래그 불필요.
/// 읽기:  런타임은 SynergyDatabase.Instance (Resources/SynergyDatabase.asset 1회 로드) 로 접근.
/// </summary>
[CreateAssetMenu(menuName = "PokeChess/SynergyDatabase", fileName = "SynergyDatabase")]
public class SynergyDatabase : NamedScriptableDatabase<SynergyDatabase, SynergyData>
{
    [Tooltip("임포터(Import Synergy JSON)가 자동으로 채움. 수동 편집 비권장.")]
    public List<SynergyData> all = new();

    protected override IReadOnlyList<SynergyData> Items => all;

    protected override IEnumerable<string> KeysOf(SynergyData s)
    {
        yield return s.synergyName;
        yield return s.synergyNameEn;
    }

    /// <summary>시너지명(한글/영문, 대소문자 무관)으로 조회. 없으면 null.</summary>
    public SynergyData GetByKey(string key) => Lookup(key);
}
