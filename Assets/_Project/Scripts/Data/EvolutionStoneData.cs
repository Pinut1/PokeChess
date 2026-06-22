using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EvolutionMapping
{
    public string targetPokemon;   // 진화 전 포켓몬 영문명
    public string evolvedPokemon;  // 진화 후 포켓몬 영문명
}

[CreateAssetMenu(menuName = "PokeChess/EvolutionStone", fileName = "NewEvolutionStone_Data")]
public class EvolutionStoneData : ScriptableObject
{
    [Header("기본 정보")]
    public int id;
    public string stoneName;
    public string stoneNameEn;
    public string stoneType;       // ex) "water", "fire"

    [TextArea]
    public string description;

    [Header("진화 매핑 (EvolutionMap 시트에서 자동 채워짐)")]
    public List<EvolutionMapping> mappings = new();

    [Header("에셋 참조")]
    public Sprite icon;

    /// <summary>targetPokemon 영문명으로 진화 후 포켓몬 영문명 반환. 없으면 null.
    /// 대소문자 무관 매칭(PokemonDatabase.GetByNameEn과 동일) — 시트 간 영문명 표기 차이 안전망.</summary>
    public string GetEvolvedPokemon(string targetNameEn)
    {
        foreach (var m in mappings)
            if (string.Equals(m.targetPokemon, targetNameEn, System.StringComparison.OrdinalIgnoreCase))
                return m.evolvedPokemon;
        return null;
    }
}
