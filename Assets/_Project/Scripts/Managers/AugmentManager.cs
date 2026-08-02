using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 증강 3택1 오퍼 / 활성 증강 관리 / GameEvents 브릿지.
///
/// 흐름: RewardManager가 preReward=AugmentChoice 스테이지에서 OfferChoice() 호출(pull)
///   → 풀(AugmentCatalog 6종 − 보유분)에서 OFFER_COUNT개 무작위 추첨
///   → GameEvents.AugmentOfferReady 발행 → 선택 UI(임시: AugmentOfferHud) 표시
///   → 플레이어 선택 시 SelectAugment() → Apply + GameEvents.AugmentSelected(전적 기록 등).
/// 선택은 로컬 전용(2인 각자 경제와 동일 규칙 — 네트워크 동기화 없음, 파트너 증강 표시는 추후).
///
/// 블로킹 UX(기획 확정 2026-07-16, 롤체 기준):
///   선택 창이 떠 있는 동안 다른 조작 블로킹(IsChoiceBlocking — UI는 임시 HUD가 모달로 흡수,
///   3D 배치 입력은 입력 처리 측이 이 프로퍼티를 참조해야 함 — 후속 배선).
///   "카드 내려두기"로 블로킹 해제 가능(SetOfferMinimized), 1분 초과 또는 전원 Ready 시 미선택이면 랜덤 자동 선택.
/// </summary>
public class AugmentManager : MonoBehaviour
{
    private const int MAX_AUGMENTS = 3;   // 한 판에 최대 보유 가능 증강 수
    public const  int OFFER_COUNT  = 3;   // 선택지 제공 개수 (TFT식 3개 중 1개)

    /// <summary>미선택 자동 랜덤 선택까지의 제한 시간(초) — 기획 확정 1분.</summary>
    public const float OFFER_TIMEOUT_SECONDS = 60f;

    private readonly List<Augment> _activeAugments = new();
    private readonly List<AugmentData> _pendingOffer = new();

    private float _offerStartTime;
    private bool  _offerMinimized;

    public IReadOnlyList<Augment> ActiveAugments => _activeAugments;

    /// <summary>현재 표시 중인 선택지. 비어 있으면 선택 대기 없음.</summary>
    public IReadOnlyList<AugmentData> PendingOffer => _pendingOffer;

    /// <summary>선택 창이 조작을 블로킹 중인지. 카드를 내려둔(minimized) 동안은 false.</summary>
    public bool IsChoiceBlocking => _pendingOffer.Count > 0 && !_offerMinimized;

    /// <summary>
    /// 아직 선택하지 않은 증강 오퍼가 존재하는지.
    /// 최소화 여부와 관계없이 라운드 진행을 정지할 때 사용한다.
    /// </summary>
    public bool HasPendingChoice => _pendingOffer.Count > 0;

    /// <summary>오퍼가 떠 있는 동안 남은 자동 선택 시간(초). 오퍼 없으면 0.</summary>
    public float OfferTimeRemaining =>
        _pendingOffer.Count > 0 ? Mathf.Max(0f, OFFER_TIMEOUT_SECONDS - (Time.time - _offerStartTime)) : 0f;

    /// <summary>선택 카드를 화면 아래로 내려두기/다시 올리기(블로킹 해제/재개). 타이머는 계속 흐른다.</summary>
    public void SetOfferMinimized(bool minimized) => _offerMinimized = minimized;

    public bool IsOfferMinimized => _offerMinimized;

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
        GameEvents.OnRoundChanged    += HandleRoundChanged;
        GameEvents.OnBattleStart     += HandleBattleStart;
        GameEvents.OnBattleEnd       += HandleBattleEnd;
        GameEvents.OnUnitPlaced      += HandleUnitPlaced;
        GameEvents.OnUnitBenched     += HandleUnitBenched;
        GameEvents.OnUnitSold        += HandleUnitSold;
        GameEvents.OnRerollSpent     += HandleRerollSpent;
        GameEvents.OnAllPlayersReady += HandleAllPlayersReady;
    }

    private void OnDisable()
    {
        GameEvents.OnRoundChanged    -= HandleRoundChanged;
        GameEvents.OnBattleStart     -= HandleBattleStart;
        GameEvents.OnBattleEnd       -= HandleBattleEnd;
        GameEvents.OnUnitPlaced      -= HandleUnitPlaced;
        GameEvents.OnUnitBenched     -= HandleUnitBenched;
        GameEvents.OnUnitSold        -= HandleUnitSold;
        GameEvents.OnRerollSpent     -= HandleRerollSpent;
        GameEvents.OnAllPlayersReady -= HandleAllPlayersReady;
    }

    private void Update()
    {
        // 1분 시간 초과 → 미선택 상태면 랜덤 자동 선택 (기획 확정 예외처리)
        if (_pendingOffer.Count > 0 && Time.time - _offerStartTime >= OFFER_TIMEOUT_SECONDS)
            AutoSelectRandom("시간 초과(1분)");
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

        // 풀 = 전체 6종 − 이미 보유한 증강
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

        _offerStartTime = Time.time;
        _offerMinimized = false;

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
        _offerMinimized = false;

        var augment = AugmentFactory.Create(data);
        augment.Apply();
        _activeAugments.Add(augment);

        Debug.Log($"[Augment] {data.augmentName} 선택 (현재 {_activeAugments.Count}/{MAX_AUGMENTS})");
        GameEvents.AugmentSelected(data);
    }

    /// <summary>
    /// 재접속 복원 전용 진입점(2단계). 3택1 오퍼 절차 없이, 서버에 저장돼 있던 영문명으로 증강을
    /// 조용히 재적용한다. SelectAugment()/Apply()는 재사용하지 않는다 — 그 경로를 타면 HeroAugment의
    /// GrantUnitToBench()/AddReroll() 같은 "선택 시 1회" 지급이 재접속마다 다시 실행되기 때문이다.
    /// 대신 Augment.Restore()(기본은 아무 것도 안 함, 지속 효과가 있는 증강만 오버라이드)로
    /// 활성 목록만 복구하고 지속 효과만 재적용한다. GameEvents.AugmentSelected도 발행하지 않는다
    /// (복원은 "선택"이 아니라 상태 되돌리기이므로 신규 선택 통지와 구분).
    /// </summary>
    public void RestoreAugmentByNameEn(string augmentNameEn)
    {
        if (string.IsNullOrEmpty(augmentNameEn)) return;

        foreach (var data in AugmentCatalog.All)
        {
            if (data == null || data.augmentNameEn != augmentNameEn) continue;

            if (Owns(data.augmentId))
            {
                Debug.LogWarning($"[Augment] {data.augmentName} 이미 보유 — 중복 복원 무시");
                return;
            }

            var augment = AugmentFactory.Create(data);
            augment.Restore();
            _activeAugments.Add(augment);

            Debug.Log($"[Augment] {data.augmentName} 복원(지속 효과만 재적용, 현재 {_activeAugments.Count}/{MAX_AUGMENTS})");
            return;
        }

        Debug.LogWarning($"[Augment] 복원 대상 '{augmentNameEn}'을 AugmentCatalog에서 찾지 못함");
    }

    /// <summary>미선택 상태 해소용 랜덤 자동 선택(시간 초과 / Ready). 오퍼 없으면 무시.</summary>
    private void AutoSelectRandom(string reason)
    {
        if (_pendingOffer.Count == 0) return;

        var picked = _pendingOffer[Random.Range(0, _pendingOffer.Count)];
        Debug.Log($"[Augment] {reason} — '{picked.augmentName}' 자동 선택");
        SelectAugment(picked);
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

    private void HandleBattleEnd(BattleEndReason reason)
        => _activeAugments.ForEach(a => a.OnBattleEnd(reason));

    private void HandleUnitPlaced(PokemonUnit unit)
        => _activeAugments.ForEach(a => a.OnUnitPlaced(unit));

    private void HandleUnitBenched(PokemonUnit unit)
        => _activeAugments.ForEach(a => a.OnUnitBenched(unit));

    private void HandleUnitSold(PokemonUnit unit)
        => _activeAugments.ForEach(a => a.OnUnitSold(unit));

    private void HandleRerollSpent()
        => _activeAugments.ForEach(a => a.OnRerollSpent());

    /// <summary>
    /// 전원 준비 완료(전투 진입 직전) 시 미선택이면 랜덤 자동 선택 — 기획 "Ready 시 자동 선택".
    /// (정확한 시점은 로컬 Ready 클릭이지만 Ready 신호가 이벤트화되어 있지 않아 전원 Ready로 보장.
    ///  정식 UI 이관 시 로컬 Ready 버튼에서 직접 호출하도록 개선 여지.)
    /// </summary>
    private void HandleAllPlayersReady() => AutoSelectRandom("Ready(전투 진입)");
}
