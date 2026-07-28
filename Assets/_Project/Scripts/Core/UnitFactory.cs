using UnityEngine;

/// <summary>
/// PokemonData로부터 런타임 PokemonUnit 인스턴스를 생성하는 팩토리.
/// 시각요소 부착(modelPrefab 또는 placeholder 캡슐)과 콜라이더 보장은
/// <see cref="PokemonUnit.RefreshVisual"/>에 있다 — 진화로 data가 스왑될 때 재사용되기 때문.
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

        // 모델 부착 + 콜라이더 보장. 진화로 data가 스왑될 때 유닛이 스스로 다시 호출한다.
        unit.RefreshVisual();

        unit.ResetForBattle(); // currentHp 채움 (Start 전 사용 대비 명시 호출)
        return unit;
    }
}
