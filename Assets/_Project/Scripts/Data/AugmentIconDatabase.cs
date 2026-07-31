using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 증강 6종의 아이콘 매핑. 인스펙터에서 아트가 직접 지정한다.
///
/// 증강 표시 정보는 기획 시트가 없어 <see cref="AugmentCatalog"/>가 코드로 생성하는데,
/// 스프라이트는 코드에 박을 수 없으므로 이 SO가 id → 아이콘 연결만 담당한다.
/// (다른 DB는 임포터 산출물이라 수동 편집 금지지만, 이건 아트 지정이라 수동이 정상이다.)
///
/// 배치: <c>Assets/Resources/AugmentIconDatabase.asset</c> 하나.
/// ScriptableDatabase 베이스를 쓰지 않는 이유 — 그 게터는 에셋이 없으면 LogError를 찍는다.
/// 아이콘은 없어도 게임이 도는 선택 데이터라 에러가 아니라 조용히 넘어가야 한다.
/// </summary>
[CreateAssetMenu(menuName = "PokeChess/AugmentIconDatabase", fileName = "AugmentIconDatabase")]
public class AugmentIconDatabase : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public AugmentId augmentId;
        public Sprite icon;
    }

    [Tooltip("증강 6종의 아이콘. 비워두면 그 증강만 아이콘 없이 표시된다.")]
    public List<Entry> entries = new();

    private static AugmentIconDatabase _instance;
    private static bool _loadAttempted;

    /// <summary>Resources에서 1회 로드. 에셋이 없으면 null(에러 로그 없음).</summary>
    public static AugmentIconDatabase Instance
    {
        get
        {
            if (_instance == null && !_loadAttempted)
            {
                _loadAttempted = true;
                _instance = Resources.Load<AugmentIconDatabase>(nameof(AugmentIconDatabase));
            }
            return _instance;
        }
    }

    /// <summary>해당 증강의 아이콘. DB나 항목이 없으면 null.</summary>
    public static Sprite Find(AugmentId augmentId)
    {
        AugmentIconDatabase db = Instance;
        return db != null ? db.GetIcon(augmentId) : null;
    }

    private Dictionary<AugmentId, Sprite> _byId;

    private Sprite GetIcon(AugmentId augmentId)
    {
        if (_byId == null)
        {
            _byId = new Dictionary<AugmentId, Sprite>();
            foreach (var e in entries)
            {
                if (e == null || e.icon == null) continue;
                _byId[e.augmentId] = e.icon; // 중복 지정 시 뒤쪽이 이김
            }
        }

        return _byId.TryGetValue(augmentId, out var icon) ? icon : null;
    }

    // 인스펙터에서 아이콘을 교체했을 때 캐시가 굳어 있지 않도록 무효화.
    private void OnEnable()   => _byId = null;
    private void OnValidate() => _byId = null;
}
