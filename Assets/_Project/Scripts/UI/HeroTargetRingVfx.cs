using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 영웅증강의 <b>이동 효과 대상</b>("가장 강한 1마리") 발밑에 표식 링을 띄우는 뷰 전용 컴포넌트.
/// 이브이·파치리스 각각 링 1개씩 — 둘 다 고르면 링이 2개 뜬다(각자 자기 대상 위에).
///
/// 왜 필요한가 — <see cref="HeroAugment.MobileTarget"/>은 아이템을 몰아주거나 유닛을 올리고 내릴 때마다
/// 실시간으로 바뀌는데, 지금까지 그 결과를 알 방법이 콘솔 로그와 QA 패널뿐이었다. 플레이어가
/// "지금 누가 버프를 받고 있는지"를 보드에서 바로 봐야 아이템을 어디에 몰아줄지 판단할 수 있다.
///
/// <b>왜 BattleVfxPlayer를 안 쓰는가</b> — 그쪽은 재생 후 lifetime이 지나면 자동 Destroy되는 일회성이다
/// (BattleVfxPlayer.Create의 Object.Destroy(go, lifetime)). 이 링은 대상이 바뀔 때까지 계속 떠 있어야 해서,
/// 지속형 선례인 보호막 상태 VFX(BattleManager.SyncShieldVfx)와 같은 방식으로 직접 Instantiate해 들고 있는다.
///
/// <b>표시 시점은 "항상"</b>(기획 확정 2026-08-19) — 준비 단계와 전투 중 모두. 다만 대상이 화면에
/// 보이지 않을 때는 링도 숨긴다(아래 <see cref="TryResolveAnchor"/> 참고).
/// </summary>
public class HeroTargetRingVfx : MonoBehaviour
{
    [Tooltip("VfxDatabase에 등록된 표식 링 id. 위치 보정(높이)은 그 엔트리의 positionOffset이 담당한다.")]
    [SerializeField] private string _vfxId = "VFX_HeroTarget_Ring";

    [Tooltip("VfxEntry.positionOffset 위에 더 얹을 보정. 평소엔 0으로 두고 DB 쪽 값으로 맞추는 게 좋다.")]
    [SerializeField] private Vector3 _extraOffset = Vector3.zero;

    // 증강 1개 = 링 1개. 증강이 사라지거나 대상이 없어지면 링도 파괴한다.
    private readonly Dictionary<HeroAugment, GameObject> _rings = new();

    // 매 프레임 새 List를 만들지 않기 위한 재사용 버퍼(정리 대상 수집용).
    private readonly List<HeroAugment> _stale = new();

    // VfxDatabase 조회 결과 캐시. _vfxId는 런타임에 바뀌지 않으므로 한 번 찾으면 끝이다.
    // 매 프레임 다시 찾으면 안 되는 이유 — 에셋이 없을 때 ScriptableDatabase.Instance 게터가
    // 접근할 때마다 Resources.Load를 재시도하고 Debug.LogError를 찍는다(_instance가 계속 null이라
    // 캐싱이 안 됨). LateUpdate에서 부르는 경로라 그대로 두면 초당 수십~수백 건의 에러 로그가 쌓인다.
    private VfxEntry _entry;

    // 한 번 실패하면 더 찾지 않는다. Resources 에셋이 플레이 도중에 생겨나는 일은 없어서,
    // 재시도해봐야 위와 같은 로그 폭주만 만든다.
    private bool _entryLookupFailed;

    // 유닛이 이동한 뒤(전투 틱/보드 재배치) 위치를 읽어야 링이 한 프레임 밀리지 않는다.
    private void LateUpdate()
    {
        SyncRings();
    }

    private void OnDisable() => DestroyAllRings();

    private void SyncRings()
    {
        if (!GameManager.TryGet(out var gm) || gm.Augment == null)
        {
            DestroyAllRings();
            return;
        }

        // ① 살아있는 영웅증강마다 링을 맞춘다.
        foreach (var augment in gm.Augment.ActiveAugments)
        {
            if (augment is not HeroAugment hero) continue;

            if (TryResolveAnchor(hero.MobileTarget, out Vector3 position))
                PlaceRing(hero, position);
            else
                RemoveRing(hero); // 대상 없음 또는 화면에 안 보임
        }

        // ② 증강 목록에서 빠진(=더 이상 활성이 아닌) 링 정리. 실제로는 증강이 제거되는 경로가
        //    아직 없지만, 남겨두면 파괴된 대상 위에 링이 떠 있는 상태가 되므로 방어해 둔다.
        _stale.Clear();
        foreach (var kv in _rings)
            if (!IsStillActive(gm.Augment, kv.Key)) _stale.Add(kv.Key);

        foreach (var hero in _stale) RemoveRing(hero);
    }

    private static bool IsStillActive(AugmentManager manager, HeroAugment hero)
    {
        foreach (var augment in manager.ActiveAugments)
            if (ReferenceEquals(augment, hero)) return true;
        return false;
    }

    /// <summary>
    /// 대상 유닛이 지금 <b>화면 어디에 그려지고 있는지</b>를 찾는다. 못 찾으면 false(링을 숨긴다).
    ///
    /// 전투가 시작되면 BattleManager가 원본 PokemonUnit을 <c>SetActive(false)</c>로 끄고 전투 전용
    /// 시각화(BattleUnit.visual)를 따로 만들어 움직인다. 그래서 준비 단계처럼 unit.transform만 보면
    /// 전투 중에는 링이 출발 지점에 얼어붙는다 — 전투 중에는 그 유닛의 BattleUnit.visual을 따라가야 한다.
    ///
    /// 어느 쪽으로도 보이지 않으면(파트너 보드 관전 중 등) 링을 숨긴다 — 아무것도 없는 자리에
    /// 링만 떠 있는 것보다 안 보이는 쪽이 맞다.
    /// </summary>
    private static bool TryResolveAnchor(PokemonUnit target, out Vector3 position)
    {
        position = default;
        if (target == null) return false;

        // 전투 중이면 그 유닛의 전투 시각화를 우선한다.
        if (GameManager.TryGet(out var gm) && gm.Battle != null)
        {
            foreach (var bu in gm.Battle.Units)
            {
                if (bu == null || bu.source != target) continue;
                if (!bu.IsAlive || bu.visual == null) return false; // 죽었거나 시각화가 정리된 상태

                position = bu.visual.transform.position;
                return true;
            }
        }

        // 준비 단계 — 보드 위 원본 유닛. 꺼져 있으면(전투 중 숨김/관전) 표시하지 않는다.
        if (!target.gameObject.activeInHierarchy) return false;

        position = target.transform.position;
        return true;
    }

    private void PlaceRing(HeroAugment hero, Vector3 position)
    {
        VfxEntry entry = ResolveEntry();
        if (entry == null) return;

        Vector3 finalPosition = position + entry.positionOffset + _extraOffset;

        if (_rings.TryGetValue(hero, out GameObject ring) && ring != null)
        {
            ring.transform.position = finalPosition; // 이미 있으면 위치만 갱신
            return;
        }

        // 크기는 건드리지 않는다 — 프리팹 루트 scale이 그대로 최종 크기(BattleVfxPlayer·보호막 VFX와 동일 규칙).
        GameObject created = Instantiate(entry.prefab, finalPosition, Quaternion.identity);
        created.name = $"HeroTargetRing_{hero.Data?.augmentName ?? hero.GetType().Name}";

        // 내 화면 전용 연출이라 로컬 레이어로 태깅한다 — 안 하면 파트너 보드를 볼 때 내 표식이 같이 보인다
        // (UnitEvolveVfx가 같은 이유로 쓰는 레이어). 프로젝트에 레이어가 없으면 -1이라 프리팹 값을 유지.
        int layer = LayerMask.NameToLayer("LocalGameplayVisual");
        if (layer >= 0) SetLayerRecursive(created.transform, layer);

        _rings[hero] = created;
    }

    /// <summary>
    /// 표식 링 엔트리를 한 번만 찾아 캐시한다. 실패하면 경고를 1회 남기고 이후로는 조회 자체를 하지 않는다
    /// (이유는 <see cref="_entry"/> 주석 참고 — LateUpdate 경로라 재조회 비용이 그대로 매 프레임 비용이 된다).
    /// </summary>
    private VfxEntry ResolveEntry()
    {
        if (_entry != null) return _entry;
        if (_entryLookupFailed) return null;

        VfxDatabase db = VfxDatabase.Instance;
        VfxEntry found = db != null ? db.Get(_vfxId) : null;

        if (found == null || found.prefab == null)
        {
            Debug.LogWarning($"[Vfx] '{_vfxId}' 미등록 또는 prefab 비어있음 — VfxDatabase.asset에 등록하세요.", this);
            _entryLookupFailed = true;
            return null;
        }

        _entry = found;
        return _entry;
    }

    private void RemoveRing(HeroAugment hero)
    {
        if (!_rings.TryGetValue(hero, out GameObject ring)) return;

        if (ring != null) Destroy(ring);
        _rings.Remove(hero);
    }

    private void DestroyAllRings()
    {
        foreach (var kv in _rings)
            if (kv.Value != null) Destroy(kv.Value);

        _rings.Clear();
    }

    private static void SetLayerRecursive(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursive(root.GetChild(i), layer);
    }
}
