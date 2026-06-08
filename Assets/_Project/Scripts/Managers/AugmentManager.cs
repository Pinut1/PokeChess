using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 증강 선택 / 활성 증강 관리 / GameEvents 브릿지.
/// </summary>
public class AugmentManager : MonoBehaviour
{
    private const int MAX_AUGMENTS = 3;   // 한 판에 최대 보유 가능 증강 수
    public const  int OFFER_COUNT  = 3;   // 선택지 제공 개수 (TFT식 3개 중 1개)

    private readonly List<Augment> _activeAugments = new();

    public IReadOnlyList<Augment> ActiveAugments => _activeAugments;

    // ─────────────────────────────────────────
    // 이벤트 구독
    // ─────────────────────────────────────────

    private void OnEnable()
    {
        GameEvents.OnAugmentSelected += HandleAugmentSelected;
        GameEvents.OnRoundChanged    += HandleRoundChanged;
        GameEvents.OnBattleStart     += HandleBattleStart;
        GameEvents.OnBattleEnd       += HandleBattleEnd;
        GameEvents.OnUnitPlaced      += HandleUnitPlaced;
        GameEvents.OnUnitSold        += HandleUnitSold;
    }

    private void OnDisable()
    {
        GameEvents.OnAugmentSelected -= HandleAugmentSelected;
        GameEvents.OnRoundChanged    -= HandleRoundChanged;
        GameEvents.OnBattleStart     -= HandleBattleStart;
        GameEvents.OnBattleEnd       -= HandleBattleEnd;
        GameEvents.OnUnitPlaced      -= HandleUnitPlaced;
        GameEvents.OnUnitSold        -= HandleUnitSold;
    }

    // ─────────────────────────────────────────
    // 외부 호출
    // ─────────────────────────────────────────

    /// <summary>
    /// UI에서 플레이어가 증강을 선택했을 때 호출.
    /// GameEvents.AugmentSelected()를 통해 이 메서드로 흘러들어옴.
    /// </summary>
    public void SelectAugment(AugmentData data)
    {
        if (_activeAugments.Count >= MAX_AUGMENTS)
        {
            Debug.LogWarning("[Augment] 최대 증강 수 초과");
            return;
        }

        var augment = AugmentFactory.Create(data);
        augment.Apply();
        _activeAugments.Add(augment);

        Debug.Log($"[Augment] {data.augmentName} 선택 (현재 {_activeAugments.Count}/{MAX_AUGMENTS})");
        GameEvents.AugmentSelected(data);
    }

    // ─────────────────────────────────────────
    // 이벤트 핸들러 — 모든 활성 증강에 전파
    // ─────────────────────────────────────────

    private void HandleAugmentSelected(AugmentData _) { }   // 필요 시 UI 연동

    private void HandleRoundChanged(int round)
        => _activeAugments.ForEach(a => a.OnRoundChanged(round));

    private void HandleBattleStart()
        => _activeAugments.ForEach(a => a.OnBattleStart());

    private void HandleBattleEnd(bool isWin)
        => _activeAugments.ForEach(a => a.OnBattleEnd(isWin));

    private void HandleUnitPlaced(PokemonUnit unit)
        => _activeAugments.ForEach(a => a.OnUnitPlaced(unit));

    private void HandleUnitSold(PokemonUnit unit)
        => _activeAugments.ForEach(a => a.OnUnitSold(unit));
}
