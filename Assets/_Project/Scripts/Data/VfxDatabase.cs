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
/// VFX를 어느 방향으로 쏠지. 프리팹에 방향이 있는지(빔·투사체)는 아트만 아는 정보라
/// spawnMode와 마찬가지로 에셋에서 지정한다.
/// </summary>
public enum VfxAimMode
{
    /// <summary>회전 없이 제자리 생성(기본). 장판·버프·타격 이펙트처럼 방향이 없는 프리팹용.</summary>
    None = 0,

    /// <summary>
    /// 시전자 위치에서 대상 쪽을 보도록 회전해 <b>하나만</b> 생성하고, 파티클 수명을 실제 거리에
    /// 맞춰 대상에서 멈추게 한다. 마법사 빔(ENEMY_LINE)처럼 한 줄기가 뻗는 연출용.
    ///
    /// 이걸 끄면 방향성 프리팹이 대상 위치에 회전 없이 생성돼 프리팹 제작 방향(보통 월드 +Z)으로
    /// 날아간다 — 원거리 평타에서 겪었던 것과 같은 증상이다(BattleVfxPlayer.IsRangedVfx 주석 참고).
    /// </summary>
    FromCaster = 1,

    /// <summary>
    /// 오브젝트 자체를 시전자 위치에서 대상 위치까지 실제로 이동시킨다(코드 제어, VfxTravelMover).
    /// 파티클 자체에 전진 속도가 없는 프리팹(정지형 버스트 등)도 이 모드면 화면에서 실제로 날아간다.
    /// spawnMode보다 우선한다(FromCaster와 동일한 이유 — 한 줄기만 생성).
    /// </summary>
    TravelToTarget = 2,
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

    /// <summary>
    /// 생성 위치 보정(월드 기준). 유닛 발밑이 기준이라 머리 위에 띄우려면 Y를 올린다(도발 표시 등).
    ///
    /// 프리팹 <b>루트</b>를 위로 올려두는 방식은 통하지 않는다 — Instantiate가 위치를 인자로 덮어쓰기 때문.
    /// 프리팹 안에서 해결하려면 루트가 아니라 <b>자식</b>을 올려야 한다(자식 위치는 상대값이라 유지된다).
    /// </summary>
    public Vector3 positionOffset;

    /// <summary>생성 위치/개수. 기본 PerTarget = 기존 동작이라 기등록 항목은 그대로 둬도 변화 없음.</summary>
    public VfxSpawnMode spawnMode = VfxSpawnMode.PerTarget;

    /// <summary>
    /// 조준 방식. 기본 None = 기존 동작이라 기등록 항목은 그대로 둬도 변화 없다.
    /// 방향이 있는 프리팹(빔·투사체)만 FromCaster로 바꾸면 된다.
    /// FromCaster는 spawnMode보다 우선한다 — 한 줄기만 생성하므로 PerTarget/Center 구분이 의미 없다.
    /// </summary>
    public VfxAimMode aimMode = VfxAimMode.None;

    /// <summary>
    /// 조준형일 때 파티클 수명을 실제 거리에 맞춰 늘릴지(수명 = 거리 / Start Speed).
    /// <b>단일 투사체</b>가 Start Speed로 날아가는 프리팹에서만 맞는 전제다.
    ///
    /// 본체·트레일·잔상이 서브이펙트로 나뉘어 각자 다른 속도·수명을 쓰는 프리팹은 끌 것 —
    /// 짧게 잡아둔 트레일 수명까지 늘려 잔상이 과하게 길어진다.
    /// 기본 true(원거리 평타가 이 보정에 의존한다).
    /// </summary>
    public bool fitLifetimeToDistance = true;

    /// <summary>
    /// 조준형일 때 빔 길이를 실제 대상 거리에 맞춰 늘릴지. 시전자와 대상을 잇는 빔(태양빔 등)용.
    ///
    /// Stretched Billboard로 그린 파티클의 lengthScale만 비율로 키운다 —
    /// 트랜스폼 스케일을 키우면 굵기까지 굵어져 빔이 뭉툭해진다.
    /// 늘리는 방향은 파티클 속도 방향이라 조준 회전이 이미 처리한다.
    /// </summary>
    public bool stretchToDistance;

    /// <summary>
    /// 프리팹이 기본 상태에서 덮는 거리(월드 단위). 빔 길이를 (실제 거리 / 이 값)배로 키운다.
    /// 씬에 프리팹을 놓고 빔이 닿는 거리를 눈으로 재서 넣으면 된다. 0 이하면 늘리지 않는다.
    /// </summary>
    public float referenceDistance = 1f;

    /// <summary>
    /// Center 모드에서 스킬 areaRadius에 맞춰 프리팹을 자동 확대할지.
    /// 켤 거면 프리팹을 "반경 1칸" 기준으로 제작해야 한다(반경1=×1, 반경2≈×1.63, 반경4≈×2.9).
    /// 기본 false — 기존 프리팹 크기를 건드리지 않는다.
    /// </summary>
    public bool scaleWithRadius;

    /// <summary>
    /// TravelToTarget 전용 — 시전자에서 대상까지 도달하는 데 걸리는 시간(초). 이 페이스로 계산한
    /// 속도를 대상 도착 이후에도 그대로 유지하며 화면 밖으로 나갈 때까지 계속 날아간다(VfxTravelMover).
    /// 전투 데미지는 이동 완료를 기다리지 않고 스킬 시전 즉시 적용되므로(BattleManager.CastSkill),
    /// 너무 길게 잡으면 "맞기 전에 이미 데미지가 들어간 것처럼" 보인다. 0.15초 이하 권장.
    /// </summary>
    public float travelDuration = 0.12f;

    /// <summary>
    /// TravelToTarget 전용 — 대상 지점에 도달한 뒤의 동작. 기본 false = 도달 즉시 파괴("맞고 사라짐").
    /// true면 멈추지 않고 같은 속도로 계속 직진하다가 화면 밖으로 나갈 때 파괴("관통해서 날아감").
    /// </summary>
    public bool travelPastTarget;

    /// <summary>
    /// FromCaster + stretchToDistance 조합 전용 — 빔 길이(lengthScale)를 0에서 목표값까지
    /// 이 시간(초) 동안 애니메이션으로 늘린다("빔이 뻗어나가는" 연출). 0이면 기존처럼
    /// 즉시 완성된 길이로 생성(변화 없음 — 기존 등록 항목은 전부 이 값이 0이라 영향 없음).
    /// </summary>
    public float beamGrowDuration;

    /// <summary>
    /// 조준형(FromCaster/TravelToTarget) 전용 — 스폰 위치를 "시전자→대상 방향"을 따라 이 값(월드 단위)만큼
    /// 앞으로 민다. positionOffset(월드 고정 오프셋)과 달리 캐스터가 어느 쪽을 보고 있든 항상
    /// 타겟 방향(=대체로 캐스터 정면)으로 밀리므로, 캐스터 visual 피벗이 얼굴/몸 중심이 아니라
    /// 머리 뒤쪽 등에 있어 스폰 지점이 안쪽으로 파묻혀 보일 때 이걸로 보정한다. 기본 0(변화 없음).
    /// </summary>
    public float forwardOffset;
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
