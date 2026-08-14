using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 견본덱 창이 쓰는 공용 목록 풀. "꺼둔 템플릿 한 칸을 복제해 쓰고, 남는 칸은 끄기만 한다"는
/// 규칙을 한 곳에 모았다 — 덱 목록 줄·시너지 아이콘·유닛 칸·아이템 아이콘이 전부 같은 방식이다.
///
/// 파괴하지 않고 재사용하는 이유는 덱을 바꿀 때마다 Destroy/Instantiate가 일어나면 레이아웃이
/// 한 프레임 튀고 GC도 계속 도는 탓이다. 비활성 칸은 레이아웃 그룹이 세지 않으므로
/// 끄기만 해도 자리가 접힌다.
///
/// 템플릿은 <b>항상 꺼진 채</b>여야 한다. 켜져 있으면 그 자체가 첫 칸처럼 보인다.
/// </summary>
public static class SampleDeckPool
{
    /// <summary>
    /// 풀에서 index번째 칸을 꺼낸다. 모자라면 템플릿을 복제해 늘린다.
    /// 템플릿이 비어 있으면 null — 부르는 쪽이 그 줄만 건너뛴다.
    /// </summary>
    /// <param name="area">복제본을 담을 부모. 비어 있으면 템플릿이 놓인 자리를 쓴다.</param>
    public static T Take<T>(List<T> pool, T template, RectTransform area, int index)
        where T : Component
    {
        return Take(pool, template, area, index, null);
    }

    /// <summary>
    /// 위와 같되, <b>새로 만든 칸에만</b> 초기화 콜백을 준다.
    /// 재사용되는 칸에 이벤트를 다시 걸면 한 번 눌러도 여러 번 불리므로 반드시 생성 시점에만 건다.
    /// </summary>
    public static T Take<T>(
        List<T> pool, T template, RectTransform area, int index, System.Action<T> onCreated)
        where T : Component
    {
        if (template == null) return null;

        while (pool.Count <= index)
        {
            Transform parent = area != null ? area : template.transform.parent;

            // worldPositionStays: false — 켜두면 템플릿에 잡아둔 RectTransform 값이 틀어진다.
            T clone = Object.Instantiate(template, parent, false);
            clone.name = $"{template.name}_{pool.Count}";

            onCreated?.Invoke(clone);
            pool.Add(clone);
        }

        T item = pool[index];
        item.gameObject.SetActive(true);
        return item;
    }

    /// <summary>count개만 남기고 나머지는 끈다.</summary>
    public static void HideExtras<T>(List<T> pool, int count) where T : Component
    {
        for (int i = count; i < pool.Count; i++)
            if (pool[i] != null) pool[i].gameObject.SetActive(false);
    }

    /// <summary>템플릿을 꺼둔다. 씬에서 켠 채 저장했더라도 실행 시 다시 끈다.</summary>
    public static void HideTemplate(Component template)
    {
        if (template != null) template.gameObject.SetActive(false);
    }
}
