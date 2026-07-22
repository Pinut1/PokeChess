using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보드/벤치 위의 포켓몬 유닛 런타임 상태.
/// 스탯 원본은 PokemonData(ScriptableObject)에 있고, 이 클래스는
/// 별(star) 강화를 반영한 "유효 스탯"을 계산해 노출하며 전투 중 변하는 값만 들고 있음.
/// 전투 시뮬레이션은 BattleManager가 이 값을 스냅샷(복사)해서 돌리므로,
/// 이 인스턴스(원본)는 전투 중 변경되지 않음.
/// </summary>
public class PokemonUnit : MonoBehaviour
{
    [Header("데이터")]
    public PokemonData data;

    [Header("런타임 스탯")]
    public float currentHp;
    public float currentMana;

    [Range(1, 3)]
    public int starLevel = 1;       // 1~3성

    [Header("상태")]
    public bool isOnBoard;          // false = 벤치

    // ──────────────────────────────────────────
    // 아이템 / 진화의 돌 슬롯
    // ──────────────────────────────────────────
    // 슬롯 2칸 공유. 조합: 진돌+장착템 / 장착템+장착템 가능, 진돌+진돌 불가(유닛당 돌 1개).
    // 진화의 돌은 "되돌릴 수 있는 진화" — data를 진화체로 스왑하고 baseData(preStoneData)를 기억.
    // 스탯/스킬/시너지는 모두 data에서 읽으므로 data 스왑만으로 전부 전환됨.
    public const int MaxItemSlots = 2;

    [Header("장착물")]
    public List<ItemData> items = new();        // 일반 장착템 (진돌 제외)
    public EvolutionStoneData equippedStone;     // null = 없음, 유닛당 최대 1개
    public PokemonData preStoneData;             // 돌 빼면 돌아갈 베이스 종 (돌 없으면 null)

    public int  UsedSlots      => items.Count + (equippedStone != null ? 1 : 0);
    public bool HasFreeSlot    => UsedSlots < MaxItemSlots;
    public bool IsStoneEvolved => equippedStone != null;

    /// <summary>통신 진화 override로 현재 종이 영구 교체되었는지.</summary>
    public bool isTradeEvolved;

    /// <summary>
    /// 공용 풀과 경제 계산에 사용하는 최초 상점 원본 종 ID.
    /// 일반 합체·진화의 돌·통신 진화로 data가 바뀌어도 유지한다.
    /// 0은 구버전/씬 직렬화 호환용 미초기화 값이며 시작 시 현재 data.id로 보정한다.
    /// </summary>
    public int poolOriginPokemonId;

    /// <summary>현재 유닛이 이 통신 진화 규칙의 원본 계보인지 확인. 돌 진화 중이면 preStoneData도 확인한다.</summary>
    public bool MatchesTradeEvolutionTarget(PokemonData target)
    {
        if (target == null) return false;
        return data == target || preStoneData == target;
    }

    /// <summary>
    /// 성급·장착물·풀 원산지를 유지한 채 영구 통신 진화체로 종을 교체한다.
    /// 장착 중인 진화의 돌은 유지하되, 이후 제거해도 영구 진화체가 남도록 복귀 기준도 교체한다.
    /// </summary>
    public bool ApplyPermanentTradeEvolution(PokemonData evolvedData)
    {
        if (evolvedData == null || data == null) return false;
        if (poolOriginPokemonId <= 0)
            poolOriginPokemonId = preStoneData != null ? preStoneData.id : data.id;

        bool changed = data != evolvedData || !isTradeEvolved;
        data = evolvedData;
        isTradeEvolved = true;
        if (equippedStone != null)
            preStoneData = evolvedData;

        currentHp = Mathf.Min(currentHp, MaxHp);
        UnitFactory.RefreshVisual(this);
        GameEvents.UnitChanged(this);
        return changed;
    }

    // ──────────────────────────────────────────
    // 영웅 증강 런타임 변형 (증강 시스템=해인이 선택 시 설정하는 seam)
    // ──────────────────────────────────────────
    // 증강 시스템이 아직 없어 기본값은 전부 '무효과'. 켜지면 전투/진화가 자동 반영된다.
    // 이브이 영웅증강 = evolutionLocked + heroStatMultiplier, 파치리스 영웅증강 = roleOverride + grantedSkill.

    /// <summary>이브이 영웅증강: true면 별업 시 진화체로 스왑하지 않고 종을 유지(3성까지 이브이).
    /// <see cref="BoardManager"/>.CheckEvolution이 참조. 이 플래그가 이브이 3성 봇소환의 판정 기준이기도 함(BattleManager).</summary>
    public bool evolutionLocked;

    /// <summary>영웅증강 전용 스탯 배수(이브이 ×1.4). MaxHp/Attack/SpellPower에 곱해지며 특수진화 ×1.5와 독립적으로 누적.</summary>
    public float heroStatMultiplier = 1f;

    /// <summary>파치리스 영웅증강: 비어있지 않으면 <see cref="Role"/>이 이 값을 반환(서포터→탱커). 시너지는 data.synergies 그대로 유지.</summary>
    public string roleOverride;

    /// <summary>파치리스 영웅증강: null이 아니면 전투에서 data.skill 대신 이 스킬을 시전(도발 부여). 마나비용은 grantedSkillManaCost(0이면 data.manaCost).</summary>
    public PokemonSkillData grantedSkill;
    public int grantedSkillManaCost;

    /// <summary>파치리스 영웅증강 v2 자뭉열매: true면 전투당 1회 HP 45% 미만 시 언타겟+회복(BattleUnit이 스냅샷).</summary>
    public bool hasHeroBerry;

    /// <summary>주입 스킬이 유효한지(파치리스 도발 등).</summary>
    public bool HasGrantedSkill => grantedSkill != null && grantedSkill.HasSkill;

    /// <summary>전투가 실제로 시전할 스킬(주입 스킬 우선, 없으면 원본 종 스킬). 스냅샷(ApplySkill)이 이걸 읽는다.</summary>
    public PokemonSkillData EffectiveSkill => HasGrantedSkill ? grantedSkill : (data != null ? data.skill : null);

    /// <summary>EffectiveSkill에 대응하는 마나비용.</summary>
    public int EffectiveManaCost => HasGrantedSkill && grantedSkillManaCost > 0 ? grantedSkillManaCost : ManaCost;

    /// <summary>
    /// 이브이 영웅증강 적용(진화잠금 + 스탯 배수 + 역할 전환). Augment Table v2: ×1.4, 역할 → 마법사.
    /// 역할 변경은 타겟 우선순위 태그만 바뀌고 스탯은 종 원본 × 배수 유지(역할별 스탯 재계산 없음 — 해인님 회신 2026-07-16).
    /// </summary>
    public void ApplyEeveeHeroAugment(float statMultiplier = 1.4f, string newRole = null)
    {
        evolutionLocked    = true;
        heroStatMultiplier = statMultiplier;
        if (!string.IsNullOrEmpty(newRole)) roleOverride = newRole;
        currentHp          = Mathf.Min(currentHp, MaxHp);
        GameEvents.UnitChanged(this);
    }

    /// <summary>파치리스 영웅증강 적용(역할 변경 + 스킬 주입 + 스탯 배수). Augment Table v2: ×1.4.</summary>
    public void ApplyParichisuHeroAugment(string newRole, PokemonSkillData tauntSkill, int manaCost = 0, float statMultiplier = 1.4f)
    {
        roleOverride          = newRole;
        grantedSkill          = tauntSkill;
        grantedSkillManaCost  = manaCost;
        heroStatMultiplier    = statMultiplier;
        hasHeroBerry          = true; // v2 자뭉열매(전투당 1회 언타겟+회복)
        currentHp             = Mathf.Min(currentHp, MaxHp);
        GameEvents.UnitChanged(this);
    }

    // ──────────────────────────────────────────
    // 별 강화 스케일링
    // ──────────────────────────────────────────
    // TFT 표준: 성이 오를 때마다 약 1.8배. (2성=1.8x, 3성=1.8x1.8=3.24x)
    // HP/공격/특수공격에만 적용. 방어/특수방어/공속/사거리는 성과 무관(원본 그대로).
    // 인덱스 = starLevel - 1.
    private static readonly float[] STAR_MULTIPLIER = { 1f, 1.8f, 3.24f };

    /// <summary>지정 별 등급의 스탯 배수. (BattleManager가 적 유닛 스탯 계산에 재사용)</summary>
    public static float StarMultiplierFor(int starLevel)
    {
        int idx = Mathf.Clamp(starLevel - 1, 0, STAR_MULTIPLIER.Length - 1);
        return STAR_MULTIPLIER[idx];
    }

    /// <summary>현재 별 등급의 스탯 배수.</summary>
    public float StarMultiplier => StarMultiplierFor(starLevel);

    // PLACEHOLDER(기획확정 전): 진화의 돌·통신교환으로 얻은 "특수진화체"는 별배율에 ×1.5 추가.
    // 돌은 장착 중에만(IsStoneEvolved, 해제 시 자동으로 빠짐), 통신교환은 영구(isTradeEvolved).
    private const float SPECIAL_EVOLUTION_MULTIPLIER = 1.5f;
    public bool IsSpecialEvolved => IsStoneEvolved || isTradeEvolved;
    public float SpecialEvolutionMultiplier => IsSpecialEvolved ? SPECIAL_EVOLUTION_MULTIPLIER : 1f;

    // ──────────────────────────────────────────
    // 유효 스탯 (별 강화 반영) — 전투/스냅샷이 읽는 진짜 값
    // ──────────────────────────────────────────

    public float MaxHp        => data != null ? data.hp * StarMultiplier * SpecialEvolutionMultiplier * heroStatMultiplier : 0f;
    public float Attack       => data != null ? data.attack * StarMultiplier * SpecialEvolutionMultiplier * heroStatMultiplier : 0f;
    public float SpellPower   => data != null ? data.spellPower * StarMultiplier * SpecialEvolutionMultiplier * heroStatMultiplier : 0f;
    public float Defense      => data != null ? data.defense : 0f;
    public float AttackSpeed  => data != null ? data.attackSpeed : 0f;
    public int   Range        => data != null ? data.range : 0;
    public int   ManaCost     => data != null ? data.manaCost : 0;
    public string Role        => !string.IsNullOrEmpty(roleOverride) ? roleOverride : (data != null ? data.role : "");

    private void Start()
    {
        if (poolOriginPokemonId <= 0 && data != null)
            poolOriginPokemonId = data.id;
        ResetForBattle();
    }

    /// <summary>
    /// 전투 시작/라운드 진입 시 HP를 가득 채우고 마나를 비움.
    /// (TFT 표준: 매 라운드 풀회복)
    /// </summary>
    public void ResetForBattle()
    {
        currentHp = MaxHp;
        currentMana = 0f;
    }

    // ──────────────────────────────────────────
    // 장착템
    // ──────────────────────────────────────────

    /// <summary>일반 장착템 장착. 빈 슬롯 없으면 실패. (스탯 효과 적용은 전투 스냅샷/ItemManager 몫)</summary>
    public bool TryEquipItem(ItemData item)
    {
        if (item == null || !HasFreeSlot) return false;
        items.Add(item);
        return true;
    }

    /// <summary>장착템 제거. 제거된 아이템 반환(없으면 null). 인벤 반환은 호출측(ItemManager) 몫.</summary>
    public ItemData RemoveItem(ItemData item)
        => items.Remove(item) ? item : null;

    // ──────────────────────────────────────────
    // 진화의 돌 (되돌릴 수 있는 진화)
    // ──────────────────────────────────────────

    /// <summary>
    /// 진화의 돌 장착 시도. 성공하면 data가 진화체로 스왑됨(스탯·스킬·시너지 자동 전환).
    /// 실패 조건: 슬롯 부족 / 이미 돌 보유 / 이 종의 진화 대상 아님 / 진화체가 DB에 없음.
    /// </summary>
    public bool TryEquipStone(EvolutionStoneData stone)
    {
        if (stone == null || data == null) return false;
        if (equippedStone != null) return false;        // 유닛당 돌 1개
        if (!HasFreeSlot) return false;                 // 슬롯 부족

        string evolvedEn = stone.GetEvolvedPokemon(data.pokemonNameEn);
        if (string.IsNullOrEmpty(evolvedEn)) return false;   // 잘못된 대상(예: 물의돌→불 포켓몬)

        var evolved = PokemonDatabase.Instance != null
            ? PokemonDatabase.Instance.GetByNameEn(evolvedEn) : null;
        if (evolved == null)
        {
            Debug.LogError($"[Stone] 진화체 '{evolvedEn}' 가 PokemonDatabase에 없음 — 돌 시트와 포켓몬 시트 영문명 불일치");
            return false;
        }

        preStoneData  = data;
        data          = evolved;
        equippedStone = stone;
        currentHp     = Mathf.Min(currentHp, MaxHp);    // 진화체 MaxHp로 상한(준비단계 전투시작 시 풀회복됨)

        GameEvents.UnitChanged(this);                   // 시너지 재계산용(머지 트리거는 진화체라 무의미)
        return true;
    }

    /// <summary>
    /// 진화의 돌 제거(제거기). data를 베이스 종으로 원복하고 돌을 반환(인벤 복귀는 호출측 몫).
    /// 돌이 없으면 null. 원복으로 같은 종 3마리가 되면 합체돼야 하므로 OnUnitChanged 발화.
    /// </summary>
    public EvolutionStoneData RemoveStone()
    {
        if (equippedStone == null) return null;

        var removed   = equippedStone;
        data          = preStoneData;
        preStoneData  = null;
        equippedStone = null;
        currentHp     = Mathf.Min(currentHp, MaxHp);

        GameEvents.UnitChanged(this);                   // ★ BoardManager가 구독→CheckEvolution 재실행
        return removed;
    }

    /// <summary>
    /// 판매 직전 정리. 돌이 있으면 원복(+돌 반환)하고 장착템을 모두 회수해 반환한다.
    /// 호출 후 data는 베이스 종이므로 ShopManager가 data.cost로 정산하면 된다.
    /// 반환물(돌·아이템)의 인벤 복귀는 호출측(ItemManager/ShopManager) 몫.
    /// </summary>
    public void PrepareForSell(out EvolutionStoneData returnedStone, out List<ItemData> returnedItems)
    {
        returnedStone = RemoveStone();                  // 돌 있으면 원복 + 반환
        returnedItems = new List<ItemData>(items);
        items.Clear();
    }
}
