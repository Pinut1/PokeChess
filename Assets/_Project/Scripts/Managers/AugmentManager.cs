using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 증강 3택1 오퍼 / 활성 증강 관리 / GameEvents 브릿지.
///
/// 흐름: RewardManager가 preReward=AugmentChoice 스테이지에서 OfferChoice() 호출(pull)
///   → 풀(AugmentCatalog 7종 − 보유분)에서 OFFER_COUNT개 무작위 추첨
///   → GameEvents.AugmentOfferReady 발행 → 선택 UI(임시: AugmentOfferHud) 표시
///   → 플레이어 선택 시 SelectAugment() → Apply + GameEvents.AugmentSelected(전적 기록 등).
/// 선택은 로컬 전용(2인 각자 경제와 동일 규칙 — 네트워크 동기화 없음, 파트너 증강 표시는 추후).
/// </summary>
public class AugmentManager : MonoBehaviour
{
    private const int MAX_AUGMENTS = 3;   // 한 판에 최대 보유 가능 증강 수
    public const  int OFFER_COUNT  = 3;   // 선택지 제공 개수 (TFT식 3개 중 1개)

    private readonly List<Augment> _activeAugments = new();
    private readonly List<AugmentData> _pendingOffer = new();

    public IReadOnlyList<Augment> ActiveAugments => _activeAugments;

    /// <summary>현재 표시 중인 선택지. 비어 있으면 선택 대기 없음.</summary>
    public IReadOnlyList<AugmentData> PendingOffer => _pendingOffer;

    // ─────────────────────────────────────────
    // 이벤트 구독
    // ─────────────────────────────────────────

    private void Awake()
    {
        // 임시 3택1 UI — 정식 UI(태욱, UIManager)로 교체 전까지 자동 부착.
        if (GetComponent<AugmentOfferHud>() == null)
            gameObject.AddComponent<AugmentOfferHud>();
    }

    private void OnEnable()
    {
        GameEvents.OnRoundChanged += HandleRoundChanged;
        GameEvents.OnBattleStart  += HandleBattleStart;
        GameEvents.OnBattleEnd    += HandleBattleEnd;
        GameEvents.OnUnitPlaced   += HandleUnitPlaced;
        GameEvents.OnUnitBenched  += HandleUnitBenched;
        GameEvents.OnUnitSold     += HandleUnitSold;
        GameEvents.OnRerollSpent  += HandleRerollSpent;
    }

    private void OnDisable()
    {
        GameEvents.OnRoundChanged -= HandleRoundChanged;
        GameEvents.OnBattleStart  -= HandleBattleStart;
        GameEvents.OnBattleEnd    -= HandleBattleEnd;
        GameEvents.OnUnitPlaced   -= HandleUnitPlaced;
        GameEvents.OnUnitBenched  -= HandleUnitBenched;
        GameEvents.OnUnitSold     -= HandleUnitSold;
        GameEvents.OnRerollSpent  -= HandleRerollSpent;
    }

    // ─────────────────────────────────────────
    // 오퍼 (3택1)
    // ─────────────────────────────────────────

    /// <summary>
    /// 증강 선택지 제시. RewardManager(RewardKind.AugmentChoice)가 호출.
    /// 이미 선택 대기 중이거나 보유 상한/풀 소진이면 스킵.
    /// </summary>
    public void OfferChoice()
    {
        if (_pendingOffer.Count > 0)
        {
            Debug.LogWarning("[Augment] 이미 선택 대기 중 — 오퍼 중복 스킵");
            return;
        }

        if (_activeAugments.Count >= MAX_AUGMENTS)
        {
            Debug.LogWarning("[Augment] 최대 증강 수 도달 — 오퍼 스킵");
            return;
        }

        // 풀 = 전체 7종 − 이미 보유한 증강
        var pool = new List<AugmentData>();
        foreach (var data in AugmentCatalog.All)
            if (data != null && !Owns(data.augmentId))
                pool.Add(data);

        if (pool.Count == 0)
        {
            Debug.LogWarning("[Augment] 남은 증강 없음 — 오퍼 스킵");
            return;
        }

        int count = Mathf.Min(OFFER_COUNT, pool.Count);
        for (int i = 0; i < count; i++)
        {
            int pick = Random.Range(i, pool.Count);
            (pool[i], pool[pick]) = (pool[pick], pool[i]);
            _pendingOffer.Add(pool[i]);
        }

        Debug.Log($"[Augment] 3택1 오퍼: {string.Join(" / ", _pendingOffer.ConvertAll(a => a.augmentName))}");
        // 구독 측이 목록을 들고 있어도 SelectAugment의 _pendingOffer.Clear()에 영향받지 않도록 사본 발행
        GameEvents.AugmentOfferReady(new List<AugmentData>(_pendingOffer));
    }

    /// <summary>
    /// UI에서 플레이어가 증강을 선택했을 때 호출.
    /// 팩토리 생성 → Apply → 활성 목록 편입 → GameEvents.AugmentSelected 통지.
    /// </summary>
    public void SelectAugment(AugmentData data)
    {
        if (data == null) return;

        if (_activeAugments.Count >= MAX_AUGMENTS)
        {
            Debug.LogWarning("[Augment] 최대 증강 수 초과");
            return;
        }

        if (Owns(data.augmentId))
        {
            Debug.LogWarning($"[Augment] {data.augmentName} 이미 보유 — 중복 선택 무시");
            return;
        }

        _pendingOffer.Clear();

        var augment = AugmentFactory.Create(data);
        augment.Apply();
        _activeAugments.Add(augment);

        Debug.Log($"[Augment] {data.augmentName} 선택 (현재 {_activeAugments.Count}/{MAX_AUGMENTS})");
        GameEvents.AugmentSelected(data);
    }

    private bool Owns(AugmentId id)
        => _activeAugments.Exists(a => a.Data != null && a.Data.augmentId == id);

    // ─────────────────────────────────────────
    // 이벤트 핸들러 — 모든 활성 증강에 전파
    // ─────────────────────────────────────────

    private void HandleRoundChanged(int round)
        => _activeAugments.ForEach(a => a.OnRoundChanged(round));

    private void HandleBattleStart()
        => _activeAugments.ForEach(a => a.OnBattleStart());

    private void HandleBattleEnd(bool isWin)
        => _activeAugments.ForEach(a => a.OnBattleEnd(isWin));

    private void HandleUnitPlaced(PokemonUnit unit)
        => _activeAugments.ForEach(a => a.OnUnitPlaced(unit));

    private void HandleUnitBenched(PokemonUnit unit)
        => _activeAugments.ForEach(a => a.OnUnitBenched(unit));

    private void HandleUnitSold(PokemonUnit unit)
        => _activeAugments.ForEach(a => a.OnUnitSold(unit));

    private void HandleRerollSpent()
        => _activeAugments.ForEach(a => a.OnRerollSpent());
}
