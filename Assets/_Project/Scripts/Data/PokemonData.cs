using System.Collections.Generic;
using UnityEngine;

public enum AttackType
{
    Physical,
    Special
}

[CreateAssetMenu(menuName = "PokeChess/Pokemon", fileName = "NewPokemon_Data")]
public class PokemonData : ScriptableObject
{
    [Header("기본 정보")]
    public int id;                  // 도감 번호
    public string pokemonName;      // 한글 이름
    public string pokemonNameEn;    // 영문 이름
    public int cost;                // 코스트 (1~5)

    [Header("스탯")]
    public float hp;
    public float attack;
    public float defense;
    public float specialAttack;
    public float specialDefense;
    public float attackSpeed;       // 초당 공격 횟수
    public int range;               // 사거리 (칸 수)

    [Header("속성")]
    public AttackType attackType;
    public List<string> synergies;  // 시너지 타입 (예: "전기", "비행")

    [Header("스킬")]
    public PokemonSkillData skill;

    [Header("에셋 참조")]
    public GameObject modelPrefab;
    public Sprite icon;

    // ──────────────────────────────────────────
    // 별 강화 스탯 (확정 후 채울 것)
    // ──────────────────────────────────────────
    // TODO: 2성/3성 스탯 스케일링 방식 기획 확정 후 추가
}
