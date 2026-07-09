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

    // 미등록 vfxId 경고는 id당 1회만 (매 시전마다 로그 스팸 방지).
    private static readonly HashSet<string> _warnedIds = new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>대상 유닛들 위치에 VFX 생성. vfxId가 비었거나 미등록이면 조용히 무시(경고 1회).</summary>
    public static void PlayOnUnits(string vfxId, IReadOnlyList<BattleUnit> targets)
    {
        var entry = Resolve(vfxId);
        if (entry == null) return;

        foreach (var t in targets)
        {
            if (t?.visual == null) continue;
            Spawn(entry, t.visual.transform.position);
        }
    }

    /// <summary>단일 유닛 위치에 VFX 생성(시전자 플래시 등).</summary>
    public static void PlayOnUnit(string vfxId, BattleUnit target)
    {
        var entry = Resolve(vfxId);
        if (entry == null || target?.visual == null) return;
        Spawn(entry, target.visual.transform.position);
    }

    private static VfxEntry Resolve(string vfxId)
    {
        if (string.IsNullOrEmpty(vfxId)) return null; // vfxId 미지정 스킬 — 정상 케이스

        var db = VfxDatabase.Instance;
        if (db == null) return null; // DB 에셋 자체가 없음 — Instance가 이미 에러 로그 출력

        var entry = db.Get(vfxId);
        if (entry == null || entry.prefab == null)
        {
            if (_warnedIds.Add(vfxId))
                Debug.LogWarning($"[Vfx] vfxId '{vfxId}' 미등록 또는 prefab 비어있음 — VfxDatabase.asset에 등록하세요.");
            return null;
        }
        return entry;
    }

    private static void Spawn(VfxEntry entry, Vector3 position)
    {
        var go = Object.Instantiate(entry.prefab, position, Quaternion.identity);
        float lifetime = entry.lifetime > 0f ? entry.lifetime : DEFAULT_LIFETIME;
        Object.Destroy(go, lifetime);
    }
}
