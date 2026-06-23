using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "PokeChess/Pokemon", fileName = "NewPokemon_Data")]
public class PokemonData : ScriptableObject
{
    [Header("기본 정보")]
    public int id;                  // 도감 번호
    public string pokemonName;      // 한글 이름
    public string pokemonNameEn;    // 영문 이름
    public int cost;                // 코스트 (1~5)

    [Header("스탯")]
    // 신규 전투 모델(SkillSystem_DevGuide): 평타=attack, 스킬=spellPower, 방어는 defense 하나.
    // 특공/특방/attackType은 폐지됨.
    public float hp;
    public float attack;            // 평타 데미지
    public float defense;           // 받는 데미지 경감
    public float attackSpeed;       // 초당 공격 횟수 (atkSpeed)
    public int   range;             // 사거리 (칸 수)
    public float spellPower;        // 스킬 위력 (SPELL/HP_REGEN/SHIELD 등 수치 기반)
    public int   manaCost;          // 스킬 발동 마나 (평타로 충전, 도달 시 발동)

    [Header("속성")]
    public List<string> synergies;  // 시너지 타입 (synergy1, synergy2)
    public string role;             // 역할군 (Tanker/Warrior/Assassin/Magician/Archer/Supporter)

    [Header("스킬")]
    public string skillId;          // Skill Table 참조 키 (임포터가 join해 skill에 베이킹)
    public PokemonSkillData skill;

    [Header("진화")]
    /// <summary>3마리 모았을 때 변환될 포켓몬 영문명. 최종 진화형은 비워둠.</summary>
    public string evolvesIntoEn;

    [Header("획득 경로")]
    /// <summary>
    /// 상점에서 직접 구매 가능한지 여부. 진화의 돌·통신교환으로만 얻는 진화체는 false —
    /// PokemonDatabase에는 등록되지만 ShopManager 풀에서는 제외된다. (기본값 true)
    /// </summary>
    public bool shopBuyable = true;

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
