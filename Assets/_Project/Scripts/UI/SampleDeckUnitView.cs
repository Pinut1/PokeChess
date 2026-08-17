using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 견본덱 화면이 유닛 한 기를 그릴 때 필요한 것만 묶은 값. 목록(1페이지)과 배치도(2페이지)가
/// 같은 <see cref="SampleDeckUnitCardUI"/>를 쓰기 때문에 넘기는 모양도 하나로 맞춘다.
///
/// <see cref="items"/>는 일반 장비(ItemData)와 진화의 돌(EvolutionStoneData)이 섞일 수 있다.
/// 비어 있으면 그 유닛은 <b>핵심 유닛이 아니라</b> 아이템 줄이 통째로 꺼진다 —
/// 빈 칸을 남기면 "아이템을 아직 안 정했다"처럼 보인다.
/// </summary>
public readonly struct SampleDeckUnitView
{
    public readonly PokemonData pokemon;
    public readonly int starLevel;
    public readonly IReadOnlyList<ScriptableObject> items;

    public SampleDeckUnitView(PokemonData pokemon, int starLevel, IReadOnlyList<ScriptableObject> items)
    {
        this.pokemon = pokemon;
        this.starLevel = starLevel;
        this.items = items;
    }
}
