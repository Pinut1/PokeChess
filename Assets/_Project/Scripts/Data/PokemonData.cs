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

    [Header("진화")]
    /// <summary>3마리 모았을 때 변환될 포켓몬 영문명. 최종 진화형은 비워둠.</summary>
    public string evolvesIntoEn;

    [Header("에셋 참조")]
    public GameObject modelPrefab;
    public Sprite icon;

    // ──────────────────────────────────────────
    // 별 강화 스탯
    // ──────────────────────────────────────────
    // 별(star) 스케일링은 데이터에 두지 않고 PokemonUnit.StarMultiplier에서 일괄 계산.
    // (TFT 표준 성당 1.8배 — HP/공격/특수공격에만 적용). 포켓몬별 개별 배수가 필요해지면
    // 여기에 override 필드를 추가하는 식으로 확장.
}
