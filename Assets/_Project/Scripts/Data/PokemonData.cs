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

    /// <summary>
    /// ⚠️ 사거리가 아니라 <b>진화 단계</b>(1/2/3단계)다. 시트 컬럼명이 range라 오해하기 쉽다.
    /// 실제 평타 사거리는 attackRange를 쓸 것.
    /// </summary>
    public int   range;

    /// <summary>
    /// 평타 사거리(칸 수). Skill Table의 ATTACK 행을 attackVfxId로 조인해 lineLength를 베이킹한다.
    /// 규약상 _L(원거리) = 4, _S(근거리) = 1. 임포터 산출물이라 직접 수정하지 말 것.
    /// </summary>
    public int   attackRange = 1;
    public float spellPower;        // 스킬 위력 (SPELL/HP_REGEN/SHIELD 등 수치 기반)
    public int   manaCost;          // 스킬 발동 마나 (평타로 충전, 도달 시 발동)

    [Header("속성")]
    public List<string> synergies;  // 시너지 타입 (synergy1, synergy2)
    public string role;             // 역할군 (Tanker/Warrior/Assassin/Magician/Archer/Supporter)

    [Header("스킬")]
    public string skillId;          // Skill Table 참조 키 (임포터가 join해 skill에 베이킹)
    public PokemonSkillData skill;
    /// <summary>평타 VFX 키 (v11 신설, VfxDatabase 룩업용). 재생 연동 전까지 데이터만 보유.</summary>
    public string attackVfxId;

    [Header("진화")]
    /// <summary>3마리 모았을 때 변환될 포켓몬 영문명. 최종 진화형은 비워둠.</summary>
    public string evolvesIntoEn;

    [Header("획득 경로")]
    /// <summary>
    /// 상점에서 직접 구매 가능한지 여부. 진화의 돌·통신교환으로만 얻는 진화체는 false —
    /// PokemonDatabase에는 등록되지만 ShopManager 풀에서는 제외된다. (기본값 true)
    /// </summary>
    public bool shopBuyable = true;

    /// <summary>
    /// 시트의 획득 경로 원본 문자열: shop / evolution / stone / trade / synergy / wild.
    /// shopBuyable은 여기서 파생된 요약값이라 "상점에 안 나온다"까지만 알 수 있고,
    /// <b>플레이어가 아예 못 쓰는 적 전용</b>인지는 구분하지 못한다 — 그 판정은 IsPlayerObtainable을 쓸 것.
    /// (임포터 산출물. 비어 있으면 shop과 같게 취급한다)
    /// </summary>
    public string obtainBy;

    /// <summary>
    /// 플레이어가 어떤 경로로든 보유할 수 있는 포켓몬인지. 적(봇) 전용이면 false.
    /// 시너지 목록·도감처럼 "내가 쓸 수 있는 유닛"을 보여주는 UI는 이걸로 걸러야 한다.
    ///
    /// 현재 적 전용은 obtainBy=wild 하나뿐(잉어킹). evolution·stone·trade·synergy는
    /// 상점에 안 나올 뿐 플레이어가 얻는 유닛이므로 true다.
    /// ⚠️ obtainBy가 비어 있으면(구 데이터·미임포트) 안전하게 true로 본다 —
    /// 새로 추가된 필드라 <b>Import Pokemon JSON을 다시 돌려야</b> 값이 채워진다.
    /// </summary>
    public bool IsPlayerObtainable =>
        !string.Equals(obtainBy, "wild", System.StringComparison.OrdinalIgnoreCase);

    [Header("에셋 참조")]
    public GameObject modelPrefab;

    /// <summary>상점 카드·스탯창에 쓰는 큰 일러스트. (Link Card Illustrations 메뉴가 채운다)</summary>
    public Sprite icon;

    /// <summary>
    /// 작은 칸에 쓰는 전용 아이콘(시너지 툴팁의 유닛 목록 등).
    /// 큰 일러스트를 64×64로 줄이면 무엇인지 알아보기 어려워 따로 둔다.
    /// <b>비어 있으면 icon(일러스트)으로 대체</b>되므로 아이콘이 아직 없어도 화면은 깨지지 않는다.
    /// 읽을 때는 이 필드가 아니라 <see cref="UnitIcon"/>을 쓸 것.
    /// (Link Unit Icons 메뉴가 채운다. 임포터가 건드리지 않는 수동 필드)
    /// </summary>
    public Sprite unitIcon;

    /// <summary>작은 칸에 그릴 그림. 전용 아이콘이 있으면 그것, 없으면 일러스트로 폴백.</summary>
    public Sprite UnitIcon => unitIcon != null ? unitIcon : icon;

    // ──────────────────────────────────────────
    // 별 강화 스탯
    // ──────────────────────────────────────────
    // 별(star) 스케일링은 데이터에 두지 않고 PokemonUnit.StarMultiplier에서 일괄 계산.
    // (TFT 표준 성당 1.8배 — HP/공격/특수공격에만 적용). 포켓몬별 개별 배수가 필요해지면
    // 여기에 override 필드를 추가하는 식으로 확장.
}
