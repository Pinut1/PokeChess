using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VFX를 어디에 몇 개 생성할지. 연출 판단이라 아트(해인)가 에셋에서 직접 고른다.
/// </summary>
public enum VfxSpawnMode
{
    /// <summary>맞은 대상 각각의 위치에 하나씩(기본). 유성우처럼 여러 개가 떨어지는 그림.</summary>
    PerTarget = 0,

    /// <summary>범위 중심에 하나만. 중심은 타겟팅 기준과 동일 — 대상 기준 스킬은 피격 대상, 시전자 기준 스킬은 시전자.</summary>
    Center = 1,
}

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

    /// <summary>생성 위치/개수. 기본 PerTarget = 기존 동작이라 기등록 항목은 그대로 둬도 변화 없음.</summary>
    public VfxSpawnMode spawnMode = VfxSpawnMode.PerTarget;

    /// <summary>
    /// Center 모드에서 스킬 areaRadius에 맞춰 프리팹을 자동 확대할지.
    /// 켤 거면 프리팹을 "반경 1칸" 기준으로 제작해야 한다(반경1=×1, 반경2≈×1.63, 반경4≈×2.9).
    /// 기본 false — 기존 프리팹 크기를 건드리지 않는다.
    /// </summary>
    public bool scaleWithRadius;

    /// <summary>
    /// 생성 시 진행 방향으로 회전시킬지. 투사체·베기처럼 방향이 있는 프리팹에만 켠다.
    /// PerTarget이면 시전자→대상 방향, Center면 시전자가 바라보는 방향을 쓴다.
    /// 끄면 기존처럼 Quaternion.identity로 생성된다(폭발·오라 등 방향 없는 연출).
    /// 켤 거면 프리팹을 +Z(정면) 기준으로 제작해야 한다.
    /// </summary>
    public bool orientToDirection;
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
