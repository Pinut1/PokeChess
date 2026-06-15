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

        GameObject visual;
        if (data.modelPrefab != null)
        {
            visual = Object.Instantiate(data.modelPrefab, go.transform);
        }
        else
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.transform.SetParent(go.transform);
            visual.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
        }
        visual.transform.localPosition = Vector3.zero;

        // 레이캐스트 픽업용 콜라이더 보장 (모델 프리팹에 없을 수도 있음)
        if (go.GetComponentInChildren<Collider>() == null)
            go.AddComponent<CapsuleCollider>();

        unit.ResetForBattle(); // currentHp 채움 (Start 전 사용 대비 명시 호출)
        return unit;
    }
}
