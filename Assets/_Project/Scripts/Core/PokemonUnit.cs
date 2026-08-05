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

    /// <summary>통신교환으로 진화체를 받았는지(NetworkManager.RPC_TradeReceive가 매핑 적중 시 설정). 베이스 핸드오버면 false.</summary>
    public bool isTradeEvolved;

    // ──────────────────────────────────────────
    // 영웅 증강 런타임 변형 (증강 시스템=해인이 선택 시 설정하는 seam)
    // ──────────────────────────────────────────
    // 증강 시스템이 아직 없어 기본값은 전부 '무효과'. 켜지면 전투/진화가 자동 반영된다.
    // 이브이 영웅증강 = evolutionLocked + heroStatMultiplier, 파치리스 영웅증강 = roleOverride + grantedSkill.

    /// <summary>이브이 영웅증강: true면 별업 시 진화체로 스왑하지 않고 종을 유지(3성까지 이브이).
    /// <see cref="BoardManager"/>.CheckEvolution이 참조. 이 플래그가 이브이 3성 봇소환의 판정 기준이기도 함(BattleManager).</summary>
    public bool evolutionLocked;

    /// <summary>
    /// 영웅증강 전용 스탯 배수.
    /// 현재 종의 PokemonData 기본값에 먼저 적용한 뒤,
    /// 일반진화 또는 특수진화 상태에 맞는 성급 배율을 적용한다.
    /// </summary>
    public float heroStatMultiplier = 1f;

    /// <summary>파치리스 영웅증강: 비어있지 않으면 <see cref="Role"/>이 이 값을 반환(서포터→탱커). 시너지는 data.synergies 그대로 유지.</summary>
    public string roleOverride;

    /// <summary>파치리스 영웅증강: null이 아니면 전투에서 data.skill 대신 이 스킬을 시전(도발 부여). 마나비용은 grantedSkillManaCost(0이면 data.manaCost).</summary>
    public PokemonSkillData grantedSkill;
    public int grantedSkillManaCost;

    /// <summary>파치리스 영웅증강 v2 자뭉열매: true면 전투당 1회 HP 45% 미만 시 언타겟+회복(BattleUnit이 스냅샷).</summary>
    public bool hasHeroBerry;

    /// <summary>
    /// 영웅증강: 비어있지 않으면 평타 VFX를 이 값으로 대체(원거리 종이 탱커로 바뀔 때 근접 이펙트로 교체).
    /// 접미 "_L"이 투사체 판정이므로(BattleVfxPlayer.IsRangedVfx) 역할이 바뀌면 이것도 같이 바꿔야 연출이 맞는다.
    /// </summary>
    public string attackVfxIdOverride;

    /// <summary>주입 스킬이 유효한지(파치리스 도발 등).</summary>
    public bool HasGrantedSkill => grantedSkill != null && grantedSkill.HasSkill;

    /// <summary>전투가 실제로 시전할 스킬(주입 스킬 우선, 없으면 원본 종 스킬). 스냅샷(ApplySkill)이 이걸 읽는다.</summary>
    public PokemonSkillData EffectiveSkill => HasGrantedSkill ? grantedSkill : (data != null ? data.skill : null);

    /// <summary>EffectiveSkill에 대응하는 마나비용.</summary>
    public int EffectiveManaCost => HasGrantedSkill && grantedSkillManaCost > 0 ? grantedSkillManaCost : ManaCost;

    /// <summary>전투가 실제로 재생할 평타 VFX(오버라이드 우선, 없으면 원본 종 값).</summary>
    public string EffectiveAttackVfxId =>
        !string.IsNullOrEmpty(attackVfxIdOverride) ? attackVfxIdOverride
                                                  : (data != null ? data.attackVfxId : "");

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

    /// <summary>파치리스 영웅증강 적용(역할 변경 + 스킬 주입 + 스탯 배수 + 평타 VFX 교체). Augment Table v2: ×1.4.</summary>
    public void ApplyParichisuHeroAugment(string newRole, PokemonSkillData tauntSkill, int manaCost = 0,
                                          float statMultiplier = 1.4f, string attackVfxId = null)
    {
        roleOverride          = newRole;
        grantedSkill          = tauntSkill;
        grantedSkillManaCost  = manaCost;
        heroStatMultiplier    = statMultiplier;
        hasHeroBerry          = true; // v2 자뭉열매(전투당 1회 언타겟+회복)
        if (!string.IsNullOrEmpty(attackVfxId)) attackVfxIdOverride = attackVfxId;
        currentHp             = Mathf.Min(currentHp, MaxHp);
        GameEvents.UnitChanged(this);
    }

    // ──────────────────────────────────────────
    // 별 강화 스케일링
    // ──────────────────────────────────────────
    // 기획 확정(2026-07-23, 해인 작성가이드 07.23판): 일반 진화와 특수 진화는
    // "곱하는 계수"가 아니라 서로 다른 배율표를 쓴다.
    //   일반 진화(3마리 합체)        : 1.0 / 1.8 / 2.8
    //   특수 진화(진화의 돌·통신교환): 1.0 / 2.0 / 3.0
    // HP/공격/주문력에만 적용. 방어/공속/사거리는 성과 무관(원본 그대로).
    // 인덱스 = starLevel - 1.
    private static readonly float[] STAR_MULTIPLIER         = { 1f, 1.8f, 2.8f };
    private static readonly float[] SPECIAL_STAR_MULTIPLIER = { 1f, 2.0f, 3.0f };

    /// <summary>
    /// 지정 별 등급의 일반 진화 배수. (BattleManager가 적 유닛 스탯 계산에 재사용 —
    /// 적은 특수진화 상태를 갖지 않으므로 항상 일반 표를 쓴다.)
    /// </summary>
    public static float StarMultiplierFor(int starLevel)
    {
        int idx = Mathf.Clamp(starLevel - 1, 0, STAR_MULTIPLIER.Length - 1);
        return STAR_MULTIPLIER[idx];
    }

    /// <summary>진화의 돌·통신교환으로 얻은 "특수진화체"인지. 돌은 장착 중에만(해제 시 자동 해소), 통신교환은 영구.</summary>
    public bool IsSpecialEvolved => IsStoneEvolved || isTradeEvolved;

    /// <summary>현재 별 등급의 스탯 배수. 특수진화체면 전용 배율표를 쓴다(일반 배수에 곱하지 않음).</summary>
    public float StarMultiplier
    {
        get
        {
            if (!IsSpecialEvolved) return StarMultiplierFor(starLevel);
            int idx = Mathf.Clamp(starLevel - 1, 0, SPECIAL_STAR_MULTIPLIER.Length - 1);
            return SPECIAL_STAR_MULTIPLIER[idx];
        }
    }

    // ──────────────────────────────────────────
    // 유효 스탯 (영웅증강 → 진화 유형별 성급 배율 반영)
    // 전투/스냅샷이 읽는 장비 적용 전 실제 값
    // ──────────────────────────────────────────

    /// <summary>
    /// 현재 종의 기본 능력치에 확정된 순서로 배율을 적용한다.
    /// PokemonData 기본값
    /// → 영웅증강 배율
    /// → 일반/특수진화 상태에 맞는 성급 배율
    /// 특수진화는 일반 성급 배율과 별도로 곱하지 않고,
    /// 특수진화 전용 성급표를 선택해 적용한다.
    /// </summary>
    private float ApplyEffectiveStatMultiplier(float baseStat)
    {
        float heroAugmentApplied =
            baseStat * heroStatMultiplier;

        float starEvolutionApplied =
            heroAugmentApplied * StarMultiplier;

        return starEvolutionApplied;
    }

    public float MaxHp =>
        data != null
            ? ApplyEffectiveStatMultiplier(data.hp)
            : 0f;

    public float Attack =>
        data != null
            ? ApplyEffectiveStatMultiplier(data.attack)
            : 0f;

    public float SpellPower =>
        data != null
            ? ApplyEffectiveStatMultiplier(data.spellPower)
            : 0f;

    public float Defense =>
        data != null
            ? data.defense
            : 0f;

    public float AttackSpeed =>
        data != null
            ? data.attackSpeed
            : 0f;
    /// <summary>평타 사거리(칸). data.range는 진화 단계라 여기 쓰면 안 된다.</summary>
    public int   Range        => data != null ? data.attackRange : 0;
    public int   ManaCost     => data != null ? data.manaCost : 0;
    public string Role        => !string.IsNullOrEmpty(roleOverride) ? roleOverride : (data != null ? data.role : "");

    private void Start()
    {
        ResetForBattle();
    }

    // ──────────────────────────────────────────
    // 시각 요소 (모델 프리팹)
    // ──────────────────────────────────────────
    // data는 진화(합체/돌/통신교환)로 런타임에 스왑되므로 모델도 그때마다 다시 붙여야 한다.
    // 생성 시점 1회 Instantiate로 두면 2성이 돼도 1성 모델이 남는다.

    // [SerializeField] 필수 — 합체 진화는 기존 유닛을 Instantiate로 복제해 새 유닛을 만든다.
    // 직렬화되지 않으면 복제본의 _visual이 null이 되고, RefreshVisual이 이전 모델을 못 지운 채
    // 새 모델만 덧붙여 두 모델이 겹친다. 직렬화하면 Unity가 복제된 자식으로 참조를 remap한다.
    [SerializeField] private GameObject _visual;

    // 성급별 모델 확대 배율. 인덱스 = starLevel - 1.
    // 진화로 프리팹이 바뀌지 않는 성급업(피카츄 2성→3성, 라프라스 1→2→3성)은 크기 말고는
    // 구분할 단서가 없어서 눈으로 성급을 읽을 수 있게 키운다.
    [SerializeField] private float[] _starVisualScale = { 1f, 1.15f, 1.3f };

    /// <summary>현재 data의 modelPrefab으로 시각 요소를 (재)생성. 없으면 placeholder 캡슐.</summary>
    public void RefreshVisual()
    {
        // 복제본이 아직 _visual을 못 물고 있을 때(구 씬 데이터 등)의 안전망 —
        // 이 컴포넌트가 만든 시각 자식은 하나뿐이므로 남은 자식을 먼저 정리한다.
        if (_visual == null && transform.childCount > 0)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject stale = transform.GetChild(i).gameObject;
                stale.transform.SetParent(null);
                if (Application.isPlaying) Destroy(stale);
                else                       DestroyImmediate(stale);
            }
        }

        if (_visual != null)
        {
            // Destroy는 프레임 끝에 처리되므로 즉시 분리 — 안 그러면 아래 Collider 검사가 옛 모델을 본다.
            _visual.transform.SetParent(null);
            // 에디트 모드에서 Destroy는 에러를 낸다(에디터 하네스/테스트에서 호출될 수 있음).
            if (Application.isPlaying) Destroy(_visual);
            else                       DestroyImmediate(_visual);
            _visual = null;
        }

        if (data != null && data.modelPrefab != null)
        {
            _visual = Instantiate(data.modelPrefab, transform);
        }
        else
        {
            _visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _visual.transform.SetParent(transform);
            _visual.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
        }

        _visual.transform.localPosition = Vector3.zero;
        _visual.transform.localRotation = Quaternion.identity;

        // 성급 배율은 프리팹 고유 스케일에 곱한다. RefreshVisual은 매번 프리팹에서 새로
        // Instantiate하므로 여러 번 불려도 배율이 누적되지 않는다.
        _visual.transform.localScale *= StarVisualScale();

        // 드래그 레이캐스트 픽업용 콜라이더 보장 (모델 프리팹에 없을 수도 있음)
        if (GetComponentInChildren<Collider>() == null)
            gameObject.AddComponent<CapsuleCollider>();
    }

    /// <summary>현재 성급의 모델 확대 배율. 배열이 비었거나 범위를 벗어나면 1배(무확대).</summary>
    private float StarVisualScale()
    {
        if (_starVisualScale == null || _starVisualScale.Length == 0) return 1f;

        int idx = Mathf.Clamp(starLevel - 1, 0, _starVisualScale.Length - 1);
        float scale = _starVisualScale[idx];
        return scale > 0f ? scale : 1f;
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

        RefreshVisual();                                // 진화체 모델로 교체
        GameEvents.UnitChanged(this);                   // 시너지 재계산용(머지 트리거는 진화체라 무의미)
        GameEvents.UnitEvolved(this, false);            // 연출용(별은 그대로, 종만 진화)
        return true;
    }

    /// <summary>
    /// 필드 전용 폼 전환(플러시와마이농 ↔ 플러시/마이농). data만 안전하게 교체하고
    /// 성급·장착템·진화의 돌·통신진화 등 나머지 상태는 그대로 유지한다.
    ///
    /// notifyChange=false면 GameEvents.UnitChanged를 발행하지 않는다 — BoardManager가
    /// 배치/벤치 이동 흐름 안에서 자동전환할 때 바로 뒤에 이어지는 기존 UnitPlaced/UnitBenched·
    /// CheckEvolution 호출과 순서가 꼬이거나 중복 실행되는 걸 막기 위함(호출측 책임).
    /// </summary>
    public bool TrySetForm(PokemonData formData, bool notifyChange = true)
    {
        if (formData == null) return false;
        if (data == formData) return false;             // 이미 같은 폼이면 중복 갱신 안 함

        data      = formData;
        currentHp = Mathf.Min(currentHp, MaxHp);

        RefreshVisual();

        if (notifyChange)
            GameEvents.UnitChanged(this);

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

        RefreshVisual();                                // 베이스 종 모델로 원복
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

    /// <summary>
    /// 성급 합체 전에 현재 장착물을 유닛에서 분리한다.
    ///
    /// 일반 아이템과 진화의 돌을 인벤토리에 넣지는 않고 참조만 반환한다.
    /// RemoveStone()을 사용하지 않으므로 data가 원본 포켓몬으로 되돌아가지 않는다.
    ///
    /// 예:
    /// 라이츄 → 돌과 원본 피카츄 정보를 분리하더라도
    /// 현재 data는 라이츄로 유지된다.
    /// </summary>
    public void DetachEquipmentForMerge(
        out EvolutionStoneData detachedStone,
        out PokemonData detachedPreStoneData,
        out List<ItemData> detachedItems)
    {
        detachedStone = equippedStone;
        detachedPreStoneData = preStoneData;
        detachedItems = new List<ItemData>(items);

        items.Clear();
        equippedStone = null;
        preStoneData = null;
    }

    /// <summary>
    /// 이미 돌 진화체인 신규 합체 유닛에 진화의 돌 상태를 복원한다.
    ///
    /// TryEquipStone()은 현재 종에서 다시 진화 대상을 찾기 때문에
    /// 이미 라이츄인 유닛에 번개의돌을 다시 장착할 수 없다.
    /// 따라서 합체 복원에서는 현재 data를 변경하지 않고
    /// 돌과 돌 장착 전 원본 종 정보만 복원한다.
    /// </summary>
    public bool RestoreStoneStateAfterMerge(
        EvolutionStoneData stone,
        PokemonData originalData)
    {
        if (stone == null || originalData == null)
            return false;

        if (equippedStone != null || !HasFreeSlot)
            return false;

        equippedStone = stone;
        preStoneData = originalData;
        return true;
    }
}
