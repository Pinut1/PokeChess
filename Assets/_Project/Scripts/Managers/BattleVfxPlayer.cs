using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 VFX 재생 헬퍼 — 순수 뷰 레이어.
/// 시뮬레이션은 결정론적 틱 기반이고 VFX는 결과에 영향을 주지 않으므로
/// 네트워크 동기화 없이 각 클라이언트가 자기 화면에서만 재생한다.
/// 위치는 유닛의 visual 트랜스폼을 그대로 쓴다(적 보드 오프셋이 이미 반영된 좌표).
/// </summary>
public static class BattleVfxPlayer
{
    /// <summary>VfxEntry.lifetime이 0 이하일 때 쓰는 자동 파괴 시간(초).</summary>
    public const float DEFAULT_LIFETIME = 2f;


    /// <summary>VfxDatabase 키 접두어. 시트의 attackVfxId가 이걸 빼먹고 오는 경우를 폴백 조회로 흡수한다.</summary>
    public const string VFX_ID_PREFIX = "VFX_";

    // 미등록 vfxId 경고는 id당 1회만 (매 시전마다 로그 스팸 방지).
    private static readonly HashSet<string> _warnedIds = new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// scope 미지정 호출(진화 연출 등 BattleManager 소속이 아닌 VFX)이 쓰는 공용 버킷.
    /// 실전투/미러 BattleManager는 각자 자기 인스턴스(this)를 scope로 넘겨 별도 버킷에 쌓는다 —
    /// 그래야 한쪽의 정리가 다른 쪽 VFX를 건드리지 않는다(ClearScope). ClearAllActive()는 이
    /// 버킷을 포함한 전체를 지우는 기존 동작을 그대로 보존한다(다른 용도가 생길 경우 대비).
    /// </summary>
    private static readonly object _globalScope = new object();

    // scope(주로 BattleManager 인스턴스)별로 생성된 VFX 오브젝트들. 전투 종료 시 자기 scope분만 파괴하기 위해 추적.
    private static readonly Dictionary<object, List<GameObject>> _activeVfxByScope = new();

    /// <summary>모든 scope의 활성 VFX를 즉시 파괴한다(기존 전역 정리 동작 보존). 전투 종료 경로는
    /// 더 이상 이 메서드를 쓰지 않고 <see cref="ClearScope"/>로 자기 scope만 정리한다.</summary>
    public static void ClearAllActive()
    {
        foreach (var list in _activeVfxByScope.Values)
            foreach (var go in list)
                if (go != null) Object.Destroy(go);
        _activeVfxByScope.Clear();
    }

    /// <summary>
    /// 특정 scope(보통 BattleManager 인스턴스 자신)가 생성한 VFX만 파괴한다. 다른 scope의 VFX는
    /// 전혀 건드리지 않는다 — 실전투 종료가 미러 VFX를, 혹은 그 반대를 지우는 간섭을 막기 위함.
    /// scope가 null이거나 등록된 적 없으면 아무 일도 하지 않는다.
    /// </summary>
    public static void ClearScope(object scope)
    {
        if (scope == null) return;
        if (!_activeVfxByScope.TryGetValue(scope, out var list)) return;

        foreach (var go in list)
            if (go != null) Object.Destroy(go);
        _activeVfxByScope.Remove(scope);
    }

    /// <summary>대상 유닛들 위치에 VFX 생성. vfxId가 비었거나 미등록이면 조용히 무시(경고 1회).</summary>
    public static void PlayOnUnits(string vfxId, IReadOnlyList<BattleUnit> targets, object scope = null, int layer = -1)
    {
        var entry = Resolve(vfxId);
        if (entry == null) return;

        SpawnPerTarget(entry, targets, scope, layer);
    }

    /// <summary>
    /// 스킬 VFX 재생. 생성 위치/개수는 VfxEntry.spawnMode(아트가 에셋에서 지정)에 따른다.
    /// PerTarget이면 맞은 대상마다 하나씩, Center면 <paramref name="center"/> 위치에 하나만.
    /// </summary>
    /// <param name="center">범위 중심이 되는 유닛. 타겟팅 기준과 일치시켜 호출측이 넘긴다
    /// (대상 기준 스킬=피격 대상, 시전자 기준 스킬=시전자).</param>
    /// <param name="areaRadiusInTiles">스킬 areaRadius(칸). scaleWithRadius가 켜진 항목만 사용.</param>
    /// <param name="caster">시전자. aimMode가 FromCaster인 항목의 출발점.</param>
    /// <param name="aimTarget">조준 대상(보통 primaryTarget). aimMode가 FromCaster일 때만 쓴다.</param>
    public static void PlaySkill(string vfxId, IReadOnlyList<BattleUnit> targets,
                                 BattleUnit center, int areaRadiusInTiles,
                                 BattleUnit caster = null, BattleUnit aimTarget = null,
                                 object scope = null, int layer = -1)
    {
        var entry = Resolve(vfxId);
        if (entry == null) return;

        // 실제 이동형(물/독/드래곤/벌레 등) — 파티클 내부 속도와 무관하게 오브젝트 자체를 옮긴다.
        if (entry.aimMode == VfxAimMode.TravelToTarget &&
            caster?.visual != null && aimTarget?.visual != null)
        {
            SpawnTraveling(entry, caster.visual.transform.position, aimTarget.visual.transform.position, scope, layer);
            return;
        }

        // 조준형(빔·투사체)은 시전자에서 대상 쪽으로 한 줄기만. spawnMode보다 우선한다.
        // 대상마다 하나씩 회전 없이 놓으면 프리팹 제작 방향으로 제각각 날아간다.
        if (entry.aimMode == VfxAimMode.FromCaster &&
            caster?.visual != null && aimTarget?.visual != null)
        {
            SpawnAimed(entry, caster.visual.transform.position, aimTarget.visual.transform.position, scope, layer);
            return;
        }

        if (entry.spawnMode == VfxSpawnMode.PerTarget || center?.visual == null)
        {
            SpawnPerTarget(entry, targets, scope, layer);
            return;
        }

        // Center: 대상이 0기여도 시전 연출은 보여준다(빗나감도 연출의 일부).
        float scale = entry.scaleWithRadius ? RadiusScale(areaRadiusInTiles) : 1f;
        Spawn(entry, center.visual.transform.position, scale, scope, layer);
    }

    private static void SpawnPerTarget(VfxEntry entry, IReadOnlyList<BattleUnit> targets, object scope, int layer)
    {
        if (targets == null) return;
        foreach (var t in targets)
        {
            if (t?.visual == null) continue;
            Spawn(entry, t.visual.transform.position, 1f, scope, layer);
        }
    }

    /// <summary>
    /// "반경 1칸 기준"으로 만든 프리팹을 반경 N칸에 맞추는 배율.
    /// 헥스 반경 N칸이 덮는 월드 반경 = N×(√3·hexSize) + hexSize 이므로
    /// 배율 = (N√3 + 1) / (√3 + 1). hexSize가 약분되어 보드 크기와 무관하다.
    /// (반경1=×1.00, 반경2≈×1.63, 반경4≈×2.90)
    /// </summary>
    public static float RadiusScale(int radiusInTiles)
    {
        int n = Mathf.Max(1, radiusInTiles);
        const float SQRT3 = 1.7320508f;
        return (n * SQRT3 + 1f) / (SQRT3 + 1f);
    }

    /// <summary>단일 유닛 위치에 VFX 생성(시전자 플래시 등). 회전 없음.</summary>
    public static void PlayOnUnit(string vfxId, BattleUnit target, object scope = null, int layer = -1)
    {
        var entry = Resolve(vfxId);
        if (entry == null || target?.visual == null) return;
        Spawn(entry, target.visual.transform.position, 1f, scope, layer);
    }

    /// <summary>
    /// 평타 VFX. 프리팹 종류에 따라 연출 형태가 다르다.
    ///   근거리(_S): 대상 위치에서 터지는 타격 이펙트.
    ///   원거리(_L): 공격자에서 출발해 대상까지 날아가는 투사체.
    ///
    /// 투사체를 대상 위치에 생성하면 도착지에서 출발해 그 너머로 지나가 버린다.
    /// 그래서 출발점을 공격자로 옮기고, 파티클 수명을 실제 거리에 맞춰 대상에서 멈추게 한다.
    /// </summary>
    /// <summary>도발 적용 시 대상 머리 위에 재생하는 이펙트(스킬 시전 VFX와 별개).</summary>
    public static void PlayTauntHit(BattleUnit target, object scope = null, int layer = -1)
    {
        const string TAUNT_HIT_VFX = "VFX_Electric_Taunt_Hit";
        var entry = Resolve(TAUNT_HIT_VFX);
        if (entry == null || target?.visual == null) return;

        Spawn(entry, target.visual.transform.position, 1f, scope, layer);
    }

    public static void PlayBasicAttack(string vfxId, BattleUnit attacker, BattleUnit target, object scope = null, int layer = -1)
    {
        var entry = Resolve(vfxId);
        if (entry == null || target?.visual == null) return;

        Vector3 hit = target.visual.transform.position;

        bool ranged = IsRangedVfx(vfxId) && attacker != null && attacker.visual != null;
        if (!ranged)
        {
            Spawn(entry, hit, 1f, scope, layer);
            return;
        }

        SpawnAimed(entry, attacker.visual.transform.position, hit, scope, layer);
    }

    /// <summary>
    /// from에서 to 쪽을 보도록 회전해 하나 생성하고, 파티클 수명을 실제 거리에 맞춰 to에서 멈추게 한다.
    /// 원거리 평타(_L)와 조준형 스킬(aimMode=FromCaster)이 공유하는 투사체 연출 처리다.
    /// </summary>
    private static void SpawnAimed(VfxEntry entry, Vector3 from, Vector3 to, object scope, int layer)
    {
        Vector3 d = to - from;
        d.y = 0f;
        float distance = d.magnitude;

        // 같은 자리(예외)면 투사체가 의미 없으니 타격 이펙트로 폴백.
        if (distance < 0.01f) { Spawn(entry, to, 1f, scope, layer); return; }

        Vector3 dir = d / distance;
        var go = Create(
            entry,
            from + dir * entry.forwardOffset,
            Quaternion.LookRotation(dir, Vector3.up),
            1f,
            scope,
            layer);

        FixGrassBeamParticleVelocity(entry, go, dir);

        if (entry.stretchToDistance)
        {
            if (entry.beamGrowDuration > 0f)
            {
                float ratio = distance / Mathf.Max(0.01f, entry.referenceDistance);
                go.AddComponent<VfxBeamGrow>().Begin(ratio, entry.beamGrowDuration);
            }
            else
            {
                StretchBeam(go, distance, entry.referenceDistance); // 기존 동작 그대로(즉시 완성)
            }
        }

        // 수명 보정은 "단일 투사체" 전제라 프리팹에 따라 끌 수 있다(VfxEntry 주석 참고).
        float travel = entry.fitLifetimeToDistance ? FitProjectileTravel(go, distance) : 0f;
        ScheduleDestroy(go, entry, travel);
    }

    /// <summary>
    /// from에서 to까지 오브젝트 자체를 실제로 이동시킨다(파티클 내부 속도와 무관).
    /// 도착 후 그대로 파괴할지, 관통해서 화면 밖으로 나갈 때까지 계속 날아갈지는
    /// entry.travelPastTarget이 정한다. VfxTravelMover가 이동+파괴까지 전부 담당하므로,
    /// 여기서는 ScheduleDestroy를 별도로 걸지 않는다(파괴 타이머 중복 방지).
    /// </summary>
    private static void SpawnTraveling(VfxEntry entry, Vector3 from, Vector3 to, object scope, int layer)
    {
        Vector3 d = to - from;
        d.y = 0f;
        float distance = d.magnitude;

        // 같은 자리면 이동이 의미 없으니 기존 타격 이펙트로 폴백(자체 lifetime로 파괴됨).
        if (distance < 0.01f) { Spawn(entry, to, 1f, scope, layer); return; }

        Vector3 dir = d / distance;
        Vector3 pushedFrom = from + dir * entry.forwardOffset;
        var go = Create(entry, pushedFrom, Quaternion.LookRotation(dir, Vector3.up), 1f, scope, layer);

        var mover = go.AddComponent<VfxTravelMover>();
        mover.Begin(pushedFrom, to, entry.travelDuration, entry.travelPastTarget);
    }

    /// <summary>
    /// Stretched Billboard로 그린 빔의 길이를 실제 거리에 맞춘다(시전자~대상을 잇는 연출).
    ///
    /// 늘어나는 방향은 파티클 속도 방향이라 조준 회전이 이미 맞춰놨고, 길이만 비율로 키우면 된다.
    /// 트랜스폼 스케일이 아니라 lengthScale을 건드리는 이유는 굵기를 그대로 두기 위해서다.
    /// Stretch 모드가 아닌 파츠(착탄 마커·바닥 원 등)는 건드리지 않는다 — 늘어나면 안 되는 것들이다.
    /// </summary>
    private static void StretchBeam(GameObject go, float distance, float referenceDistance)
    {
        if (referenceDistance <= 0.01f) return;

        float ratio = distance / referenceDistance;

        foreach (var renderer in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            if (renderer.renderMode != ParticleSystemRenderMode.Stretch) continue;
            renderer.lengthScale *= ratio;
        }
    }

    /// <summary>
    /// 투사체형 평타 프리팹인지. 판정 기준은 <b>사거리가 아니라 vfxId 접미사</b>다.
    ///
    /// 시트(VFX Table) 계약: <c>_L</c> = 원거리 평타(Archer·Magician·Supporter),
    /// <c>_S</c> = 근거리 평타(Warrior·Tanker·Assassin). 접미사와 role은 예외 없이 1:1이다.
    ///
    /// 반면 PokemonData.range는 진화하며 오르는 밸런스 수치라 연출 형태와 다른 축이다
    /// (_L인데 range=1인 1단계 유닛이 32종). 예전처럼 range로 판정하면 그 32종이
    /// 방향성 있는 투사체 프리팹을 대상 위치에 회전 없이 제자리 생성해 엉뚱하게 날아간다.
    /// </summary>
    private static bool IsRangedVfx(string vfxId)
        => !string.IsNullOrEmpty(vfxId)
           && vfxId.EndsWith("_L", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 파티클이 정확히 distance만큼 날아가고 멈추도록 수명을 맞춘다(수명 = 거리 / 속도).
    /// 프리팹이 Start Speed로 전진하는 형태를 전제한다. 속도가 0이면 제자리 연출이라 건드리지 않는다.
    /// </summary>
    /// <returns>가장 긴 이동 시간(초). 파괴 시점을 이보다 짧게 잡지 않기 위해 쓴다.</returns>
    private static float FitProjectileTravel(GameObject go, float distance)
    {
        float longest = 0f;

        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;

            // MinMaxCurve라 상수/범위 모두 올 수 있다. 범위면 최대값 기준으로 잡아 덜 날아가는 쪽을 허용.
            float speed = main.startSpeed.mode == ParticleSystemCurveMode.Constant
                ? main.startSpeed.constant
                : main.startSpeed.constantMax;

            if (speed <= 0.01f) continue; // 전진하지 않는 파티클(잔불·플래시 등)은 그대로 둔다

            float travel = distance / speed;
            main.startLifetime = travel;
            if (travel > longest) longest = travel;
        }

        return longest;
    }

    /// <summary>
    /// 임의 월드 좌표에 VFX 생성. BattleUnit이 없는 연출(상점 단계 진화 등)에서 쓴다.
    /// 조회·미등록 경고 처리는 전투 VFX와 동일하다.
    /// </summary>
    public static void PlayAt(string vfxId, Vector3 position, float scale = 1f, object scope = null, int layer = -1)
    {
        var entry = Resolve(vfxId);
        if (entry == null) return;
        Spawn(entry, position, scale, scope, layer);
    }

    private static VfxEntry Resolve(string vfxId)
    {
        if (string.IsNullOrEmpty(vfxId)) return null; // vfxId 미지정 스킬 — 정상 케이스

        var db = VfxDatabase.Instance;
        if (db == null) return null; // DB 에셋 자체가 없음 — Instance가 이미 에러 로그 출력

        var entry = db.Get(vfxId);

        // 평타 attackVfxId는 시트에 접두어 없이 들어온다("Water_S"). VfxDatabase 키는 "VFX_Water_S".
        // 스킬 vfxId는 접두어가 붙어 있어 이 폴백을 타지 않는다. 시트가 정리되면 제거 가능.
        if ((entry == null || entry.prefab == null) &&
            !vfxId.StartsWith(VFX_ID_PREFIX, System.StringComparison.OrdinalIgnoreCase))
        {
            entry = db.Get(VFX_ID_PREFIX + vfxId);
        }

        if (entry == null || entry.prefab == null)
        {
            if (_warnedIds.Add(vfxId))
                Debug.LogWarning($"[Vfx] vfxId '{vfxId}' 미등록 또는 prefab 비어있음 — VfxDatabase.asset에 등록하세요. " +
                                 $"('{VFX_ID_PREFIX}{vfxId}' 로도 조회했으나 없음)");
            return null;
        }
        return entry;
    }

    private static void Spawn(VfxEntry entry, Vector3 position, float scale, object scope, int layer)
    {
        var go = Create(entry, position, Quaternion.identity, scale, scope, layer);
        ScheduleDestroy(go, entry, 0f);
    }

    /// <summary>
    /// 프리팹 루트에 잡아둔 회전. 아트가 프리팹에서 방향을 맞춰둔 경우 그 값이 기준이 된다.
    /// Instantiate에 회전을 넘기면 루트 회전을 <b>덮어쓰므로</b>, 여기에 조준 회전을 곱해서 보존해야 한다.
    /// (이걸 안 하면 프리팹에서 X를 기울여 방향을 맞춰도 게임에서는 그대로 무시된다)
    /// </summary>
    private static Quaternion PrefabRotation(VfxEntry entry)
        => entry.prefab != null ? entry.prefab.transform.rotation : Quaternion.identity;

    private static GameObject Create(VfxEntry entry, Vector3 position, Quaternion rotation, float scale, object scope, int layer)
    {
        // positionOffset은 월드 기준으로 더한다 — 조준 회전을 따라 돌면 "머리 위"가 대상 방향에 따라
        // 흔들려서, 높이 보정으로 쓰기 어려워진다.
        var go = Object.Instantiate(entry.prefab,
                                    position + entry.positionOffset,
                                    rotation * PrefabRotation(entry));
        if (!Mathf.Approximately(scale, 1f)) go.transform.localScale *= scale;

        // layer<0(미지정)이면 프리팹 기본 Layer를 그대로 둔다(기존 동작 보존 — 진화 VFX 등).
        // 실전투/미러 BattleManager는 각자의 LocalGameplayVisual/PartnerSpectateVisual을 넘긴다 —
        // BattleManager.SetDefaultLayerRecursive를 그대로 재사용(복제하지 않음), 유닛 모델의
        // ApplyVisualLayer와 동일 규칙(0=Default인 자식만 덮어씀, 의도적으로 다른 Layer는 보존).
        if (layer >= 0)
            BattleManager.SetDefaultLayerRecursive(go.transform, layer);

        object trackScope = scope ?? _globalScope;
        if (!_activeVfxByScope.TryGetValue(trackScope, out var list))
        {
            list = new List<GameObject>();
            _activeVfxByScope[trackScope] = list;
        }

        // scope 없이(주로 _globalScope) 계속 쌓이기만 하던 문제 보완 — ClearScope는 BattleManager가
        // 자기 scope에 대해서만 부르므로, 그 대상이 아닌 scope(진화 VFX 등)는 아무도 안 비워준다.
        // 여기서 생성 시점마다 이미 파괴된(ScheduleDestroy로 소멸된) 항목을 같이 걷어내면, 별도
        // 정리 호출 없이도 리스트 크기가 항상 "현재 살아있는 VFX 개수" 근처로 유지된다.
        list.RemoveAll(g => g == null);
        list.Add(go);

        return go;
    }

    /// <summary>
    /// 자동 파괴 예약. 투사체는 도착 전에 지워지면 안 되므로 이동 시간보다 짧게 잡지 않는다.
    /// </summary>
    private static void ScheduleDestroy(GameObject go, VfxEntry entry, float minLifetime)
    {
        float lifetime = entry.lifetime > 0f ? entry.lifetime : DEFAULT_LIFETIME;
        if (minLifetime > 0f) lifetime = Mathf.Max(lifetime, minLifetime + 0.3f); // 도착 직후 잔여 연출 여유
        Object.Destroy(go, lifetime);
    }

    /// <summary>
    /// 라플레시아 Grass 솔라빔에만 파티클 방향 보정 컴포넌트를 추가한다.
    ///
    /// Grass 솔라빔은 Stretched Billboard 방식으로 광선을 표현한다.
    /// 이 방식은 파티클의 velocity를 기준으로 광선을 길게 표시하는데,
    /// 해당 프리팹은 화면에 표시되는 광선이 velocity의 반대 방향으로 뻗는다.
    ///
    /// 따라서 최종 광선이 타겟 방향을 향하도록
    /// 파티클 velocity를 타겟 반대 방향(-dir)으로 지속해서 보정한다.
    /// </summary>
    private static void FixGrassBeamParticleVelocity(
        VfxEntry entry,
        GameObject go,
        Vector3 dir)
    {
        // 다른 빔·투사체에는 적용하지 않고
        // 문제가 발생한 Grass Magician 스킬에만 적용한다.
        if (!string.Equals(
                entry.vfxId,
                "VFX_Grass_Magician_SPELL",
                System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        go.AddComponent<GrassBeamVelocityCorrector>()
          .Initialize(dir);
    }

    /// <summary>
    /// Grass 솔라빔의 실제 광선을 구성하는
    /// beam_glow와 beam_shadow 파티클의 velocity 방향을 보정한다.
    ///
    /// 파티클은 VFX 생성 직후 바로 존재하지 않을 수 있고,
    /// 재생 도중 새로 생성될 수도 있으므로 한 번만 검사하고 제거하지 않는다.
    /// VFX 오브젝트가 파괴될 때까지 살아 있으면서 현재 존재하는 파티클을 보정한다.
    ///
    /// 파티클 속도의 크기는 유지하고 방향만 바꾸므로
    /// Start Speed, 빔 길이, 거리 비례 계산에는 영향을 주지 않는다.
    /// </summary>
    private sealed class GrassBeamVelocityCorrector : MonoBehaviour
    {
        private const string BEAM_GLOW_NAME = "beam_glow";
        private const string BEAM_SHADOW_NAME = "beam_shadow";

        // 라플레시아에서 타겟으로 향하는 월드 방향.
        private Vector3 _targetDirection;

        // 실제 빔을 구성하는 두 ParticleSystem만 캐시한다.
        private ParticleSystem _beamGlow;
        private ParticleSystem _beamShadow;

        /// <summary>
        /// SpawnAimed에서 계산한 타겟 방향을 전달받고
        /// 빔 파티클 시스템을 찾아 캐시한다.
        /// </summary>
        public void Initialize(Vector3 targetDirection)
        {
            _targetDirection = targetDirection.normalized;

            foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.transform.name == BEAM_GLOW_NAME)
                {
                    _beamGlow = ps;
                }
                else if (ps.transform.name == BEAM_SHADOW_NAME)
                {
                    _beamShadow = ps;
                }
            }
        }
        private void Update()
        {
            // 빛과 그림자 파티클을 각각 보정한다.
            // 아직 파티클이 생성되지 않았다면 해당 프레임은 조용히 넘어가고,
            // 다음 프레임에 다시 시도한다.
            CorrectVelocity(_beamGlow);
            CorrectVelocity(_beamShadow);
        }

        /// <summary>
        /// 살아 있는 파티클의 속도 크기는 유지하고 방향만 변경한다.
        /// </summary>
        private void CorrectVelocity(ParticleSystem ps)
        {
            if (ps == null)
                return;

            int particleCount = ps.particleCount;
            if (particleCount == 0)
                return;

            var particles = new ParticleSystem.Particle[particleCount];
            int aliveCount = ps.GetParticles(particles);

            if (aliveCount == 0)
                return;

            // 이 Grass 빔은 velocity의 반대편으로 길게 표시되므로,
            // 최종 빔이 타겟 방향을 향하게 하려면
            // 파티클 velocity는 타겟 반대 방향으로 설정해야 한다.
            Vector3 velocityDirection = -_targetDirection;

            // Local Simulation Space라면 Particle.velocity도
            // 해당 ParticleSystem의 로컬 좌표 기준으로 저장해야 한다.
            if (ps.main.simulationSpace == ParticleSystemSimulationSpace.Local)
            {
                velocityDirection =
                    ps.transform.InverseTransformDirection(velocityDirection);
            }

            for (int i = 0; i < aliveCount; i++)
            {
                // 기존 Start Speed에서 만들어진 속도 크기는 그대로 보존한다.
                float speed = particles[i].velocity.magnitude;

                if (speed <= Mathf.Epsilon)
                    continue;

                particles[i].velocity = velocityDirection * speed;
            }

            ps.SetParticles(particles, aliveCount);
        }
    }
}

