using UnityEngine;

/// <summary>
/// PokemonData로부터 런타임 PokemonUnit 인스턴스를 생성하는 팩토리.
/// modelPrefab이 있으면 그것을, 없으면 placeholder 캡슐을 시각요소로 붙인다.
/// 드래그 레이캐스트가 부모 PokemonUnit을 찾으려면 Collider가 필요하므로 없으면 보장해 준다.
/// (디버그 테스트들이 각자 new GameObject + AddComponent 하던 패턴을 한 곳으로 추출)
/// </summary>
public static class UnitFactory
{
    public static PokemonUnit Create(PokemonData data, int starLevel = 1)
    {
        if (data == null)
        {
            Debug.LogError("[UnitFactory] data가 null — 유닛 생성 불가");
            return null;
        }

        var go = new GameObject($"Unit_{data.pokemonNameEn}_{starLevel}star");
        var unit = go.AddComponent<PokemonUnit>();
        unit.data = data;
        unit.starLevel = Mathf.Clamp(starLevel, 1, 3);
        unit.poolOriginPokemonId = data.id;

        RefreshVisual(unit);
        unit.ResetForBattle(); // currentHp 채움 (Start 전 사용 대비 명시 호출)
        return unit;
    }

    /// <summary>현재 PokemonData에 맞춰 모델 자식만 교체. 유닛 루트/위치/아이템/콜라이더는 유지한다.</summary>
    public static void RefreshVisual(PokemonUnit unit)
    {
        if (unit == null || unit.data == null) return;

        const string visualRootName = "UnitVisual";
        Transform visualRoot = unit.transform.Find(visualRootName);
        if (visualRoot == null)
        {
            var root = new GameObject(visualRootName);
            visualRoot = root.transform;
            visualRoot.SetParent(unit.transform, false);
        }

        for (int i = visualRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = visualRoot.GetChild(i).gameObject;
            if (Application.isPlaying) Object.Destroy(child);
            else Object.DestroyImmediate(child);
        }

        GameObject visual;
        if (unit.data.modelPrefab != null)
        {
            visual = Object.Instantiate(unit.data.modelPrefab, visualRoot);
        }
        else
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.transform.SetParent(visualRoot, false);
            visual.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
        }
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        // 레이캐스트 픽업용 콜라이더 보장 (모델 프리팹에 없을 수도 있음)
        if (visual.GetComponentInChildren<Collider>() == null && unit.GetComponent<Collider>() == null)
            unit.gameObject.AddComponent<CapsuleCollider>();
    }
}
