using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전체 ItemData를 모아 영문명으로 조회하는 중앙 레지스트리(SO 1개).
/// 싱글턴/이름맵 보일러플레이트는 NamedScriptableDatabase 베이스가 담당.
///
/// 채우기: PokeChessImporter의 "Import Item JSON"이 all을 자동 갱신.
/// 읽기:  런타임은 ItemDatabase.Instance (Resources/ItemDatabase.asset 1회 로드) 로 접근.
/// </summary>
[CreateAssetMenu(menuName = "PokeChess/ItemDatabase", fileName = "ItemDatabase")]
public class ItemDatabase : NamedScriptableDatabase<ItemDatabase, ItemData>
{
    [Tooltip("임포터(Import Item JSON)가 자동으로 채움. 수동 편집 비권장.")]
    public List<ItemData> all = new();

    protected override IReadOnlyList<ItemData> Items => all;
    protected override IEnumerable<string> KeysOf(ItemData item) { yield return item.itemNameEn; }

    /// <summary>영문명으로 조회(대소문자 무관). 없으면 null.</summary>
    public ItemData GetByNameEn(string nameEn) => Lookup(nameEn);

    /// <summary>ID로 일반 아이템 조회. 없으면 null.</summary>
    public ItemData GetById(int id)
    {
        return all.Find(item => item != null && item.id == id);
    }
}
