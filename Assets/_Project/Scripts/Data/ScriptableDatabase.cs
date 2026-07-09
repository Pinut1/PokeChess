using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resources 루트에 단 하나만 존재하는 DB ScriptableObject의 공통 싱글턴 로더.
/// 에셋 이름 = 타입 이름 규약(ItemDatabase → Resources/ItemDatabase.asset).
/// CRTP(자기 타입 파라미터)로 타입별 static Instance를 안전하게 보장한다.
///
/// 직렬화 데이터(all/tables/stages)는 각 서브클래스가 보유한다 — Unity의 제네릭 베이스
/// 직렬화 이슈를 피하기 위해 베이스에는 직렬화 필드를 두지 않는다.
/// </summary>
public abstract class ScriptableDatabase<TSelf> : ScriptableObject
    where TSelf : ScriptableDatabase<TSelf>
{
    private static TSelf _instance;

    /// <summary>런타임 단일 접근. Resources에서 1회 로드 후 캐싱. 없으면 에러 로그 후 null.</summary>
    public static TSelf Instance
    {
        get
        {
            if (_instance == null)
            {
                string assetName = typeof(TSelf).Name;
                _instance = Resources.Load<TSelf>(assetName);
                if (_instance == null)
                    Debug.LogError($"[{assetName}] Resources/{assetName}.asset 없음 — 임포터(Import * JSON)를 먼저 실행하세요.");
            }
            return _instance;
        }
    }
}

/// <summary>
/// 항목을 문자열 키(대소문자 무관)로 조회하는 DB의 공통 베이스.
/// Item/EvolutionStone/Pokemon/Synergy DB가 공유하던 _byName 맵 + EnsureMap +
/// InvalidateCache + OnEnable/OnDisable 무효화 보일러플레이트를 한 곳으로 모았다.
///
/// 서브클래스는 항목 컬렉션(Items)과 각 항목의 키들(KeysOf)만 제공하면 된다.
/// 추가 인덱스(예: 도감번호)는 BuildExtraIndexes/ClearCache 오버라이드로 확장한다.
/// </summary>
public abstract class NamedScriptableDatabase<TSelf, TItem> : ScriptableDatabase<TSelf>
    where TSelf : NamedScriptableDatabase<TSelf, TItem>
    where TItem : class
{
    private Dictionary<string, TItem> _byName;

    /// <summary>맵 빌드 대상 항목들(서브클래스의 직렬화 리스트).</summary>
    protected abstract IReadOnlyList<TItem> Items { get; }

    /// <summary>각 항목이 등록될 키들(대소문자 무관). 보통 1개, 시너지처럼 여러 개일 수 있음.</summary>
    protected abstract IEnumerable<string> KeysOf(TItem item);

    /// <summary>키로 조회. 비어있거나 없으면 null.</summary>
    protected TItem Lookup(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        EnsureMap();
        return _byName.TryGetValue(key, out var d) ? d : null;
    }

    protected void EnsureMap()
    {
        if (_byName != null) return;

        _byName = new Dictionary<string, TItem>(System.StringComparer.OrdinalIgnoreCase);
        var items = Items;
        if (items != null)
        {
            foreach (var item in items)
            {
                if (item == null) continue;
                foreach (var key in KeysOf(item))
                    if (!string.IsNullOrEmpty(key)) _byName[key] = item;
            }
        }

        BuildExtraIndexes();
    }

    /// <summary>이름 맵 외 추가 인덱스 구축 훅(EnsureMap 말미 호출). 기본 동작 없음.</summary>
    protected virtual void BuildExtraIndexes() { }

    /// <summary>캐시 무효화 훅. 추가 인덱스를 가진 서브클래스는 오버라이드해 함께 비운다.</summary>
    protected virtual void ClearCache() => _byName = null;

    /// <summary>핫리로드/임포트 후 캐시 무효화. 임포터가 호출.</summary>
    public void InvalidateCache() => ClearCache();

    protected virtual void OnEnable()  => InvalidateCache();
    protected virtual void OnDisable() => InvalidateCache();
}
