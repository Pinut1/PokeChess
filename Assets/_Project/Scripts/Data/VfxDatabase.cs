using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VFX 항목 1개 — Skill Table의 vfxId와 프리팹을 잇는다.
/// 아트가 프리팹을 만들면 이 DB에 (vfxId, prefab)만 등록하면 코드 수정 없이 연결된다.
/// </summary>
[System.Serializable]
public class VfxEntry
{
    /// <summary>Skill Table vfxId와 1:1 (대소문자 무관, 예: "FIRE_PROJECTILE").</summary>
    public string vfxId;

    public GameObject prefab;

    /// <summary>생성 후 자동 파괴까지의 초. 0 이하면 기본값(BattleVfxPlayer.DEFAULT_LIFETIME).</summary>
    public float lifetime;
}

/// <summary>
/// vfxId → VFX 프리팹 룩업 DB. 다른 DB와 달리 임포터가 아니라 아트가 직접 편집한다.
/// (시트에는 vfxId 문자열만 있고 프리팹 참조는 에디터에서만 가능하므로)
/// 에셋 위치 규약: Resources/VfxDatabase.asset (ScriptableDatabase 로더 규약).
/// </summary>
[CreateAssetMenu(fileName = "VfxDatabase", menuName = "PokeChess/Vfx Database")]
public class VfxDatabase : NamedScriptableDatabase<VfxDatabase, VfxEntry>
{
    public List<VfxEntry> all = new();

    protected override IReadOnlyList<VfxEntry> Items => all;

    protected override IEnumerable<string> KeysOf(VfxEntry item)
    {
        yield return item.vfxId;
    }

    /// <summary>vfxId로 조회. 미등록이면 null(경고는 호출부 BattleVfxPlayer가 1회만 출력).</summary>
    public VfxEntry Get(string vfxId) => Lookup(vfxId);
}
