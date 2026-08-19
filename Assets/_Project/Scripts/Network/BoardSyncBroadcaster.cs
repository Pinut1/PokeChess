using UnityEngine;

/// <summary>
/// 로컬 플레이어의 보드/벤치/골드/증강 변경을 감지해 NetworkManager로 송출하는 다리.
/// (송출만 담당 — 수신/렌더는 OpponentBoardView, 매니저 직접 참조 대신 GameEvents 구독)
///
/// 보드 변경 이벤트(배치/벤치/판매/진화)는 한 프레임에 여러 번 터질 수 있어(예: 연쇄 진화)
/// 더티 플래그로 모았다가 LateUpdate에서 1회만 스냅샷을 송출한다.
/// </summary>
public class BoardSyncBroadcaster : MonoBehaviour
{
    private bool _boardDirty;

    /// <summary>전투 중 재접속 복구용 — 마지막으로 Player CustomProperty에 저장한 CurrentBattleTick.
    /// 10 tick(≈1초)마다 갱신되며 저장할 때마다 이 값도 같이 갱신한다. 전투 시작(HandleBattleStart)마다
    /// 0으로 리셋 — 그래야 다음 전투도 0부터 다시 10-tick 간격으로 저장된다.</summary>
    private int _lastSyncedBattleTick;

    /// <summary>Player CustomProperty에 BattleTick을 몇 tick 간격으로 저장할지. TICK_INTERVAL=0.1초
    /// 기준 10 tick≈1초 — 재접속 복구 오차 상한(≤1초)과 Photon 호출 빈도(초당 1회/인) 사이의
    /// 균형점(2026-08 재접속 전투 복구 설계 조사에서 확정).</summary>
    private const int BATTLE_TICK_SYNC_INTERVAL = 10;

    private void OnEnable()
    {
        GameEvents.OnUnitPlaced      += MarkBoardDirty;
        GameEvents.OnUnitBenched     += MarkBoardDirty;
        GameEvents.OnUnitSold        += MarkBoardDirty;
        GameEvents.OnUnitChanged     += MarkBoardDirty;
        // 장착/해제/획득/제거기·재조합기 사용은 전부 ItemManager가 InventoryChanged 하나로 통일해
        // 발행한다(ItemManager.cs 전수 확인 — 누락 경로 없음). OnUnitChanged는 진화/폼 변경 트리거라
        // 장착 아이템 변경은 잡지 못하므로, 이 이벤트를 board-dirty에 추가로 연결해야 파트너 화면의
        // 장착 아이템 표시(BoardSnapshot.Entry.itemId0/1)와 인벤토리(§ inventoryItemIds 등)가
        // 실시간으로 갱신된다.
        GameEvents.OnInventoryChanged += MarkBoardDirtyForResync;
        // 레벨업에 따른 배치 상한 변경도 스냅샷에 실어야 파트너 전체화면 관전의 유닛 수 상한 표시가
        // 즉시 갱신된다(BoardSnapshot.unitCap).
        GameEvents.OnUnitCapChanged   += HandleUnitCapChanged;
        GameEvents.OnGoldChanged     += HandleGoldChanged;
        GameEvents.OnAugmentSelected += HandleAugmentSelected;
        GameEvents.OnBoardResyncRequested += MarkBoardDirtyForResync;
        GameEvents.OnXpChanged       += HandleXpChanged;
        GameEvents.OnRerollCountChanged += HandleRerollCountChanged;
        GameEvents.OnItemShopRerolled += HandleItemShopRerolled;
        GameEvents.OnBattleStart      += HandleBattleStart;
    }

    private void OnDisable()
    {
        GameEvents.OnUnitPlaced      -= MarkBoardDirty;
        GameEvents.OnUnitBenched     -= MarkBoardDirty;
        GameEvents.OnUnitSold        -= MarkBoardDirty;
        GameEvents.OnUnitChanged     -= MarkBoardDirty;
        GameEvents.OnInventoryChanged -= MarkBoardDirtyForResync;
        GameEvents.OnUnitCapChanged   -= HandleUnitCapChanged;
        GameEvents.OnGoldChanged     -= HandleGoldChanged;
        GameEvents.OnAugmentSelected -= HandleAugmentSelected;
        GameEvents.OnBoardResyncRequested -= MarkBoardDirtyForResync;
        GameEvents.OnXpChanged       -= HandleXpChanged;
        GameEvents.OnRerollCountChanged -= HandleRerollCountChanged;
        GameEvents.OnItemShopRerolled -= HandleItemShopRerolled;
        GameEvents.OnBattleStart      -= HandleBattleStart;
    }

    /// <summary>
    /// 전투 시작 시 로컬 BattleSnapshot을 1회 만들어 파트너에게 전송한다(파트너 관전용 자동 전송).
    /// GameEvents.OnBattleStart는 BattleManager.HandleBattleStart(실제 전투 코루틴 시작)와 같은
    /// 프레임에 함께 발화하지만, 이 핸들러는 결과를 기다리지 않는 동기 조립+RPC 호출 한 번뿐이라
    /// 실제 전투 시뮬레이션 시작을 지연시키지 않는다. 관전 기능 자체가 없거나 실패해도
    /// (BroadcastBattleSnapshot이 false를 반환해도) 이 메서드는 그 결과를 무시하고 넘어간다 —
    /// 전투 시작 흐름에 관전 실패가 영향을 주면 안 되기 때문이다.
    /// </summary>
    private void HandleBattleStart()
    {
        if (!GameManager.TryGet(out var gm) || gm.Board == null || gm.Network == null) return;

        BattleSnapshot snapshot = BattleSnapshotCodec.CreateFromCurrentState(gm);
        gm.Network.BroadcastBattleSnapshot(snapshot);

        // 전투 중 재접속 복구용 — 같은 스냅샷을 자기 Player CustomProperty에도 저장(파트너 전송과는
        // 별개 경로). 재접속 시 이 값을 다시 읽어 BattleManager.TryRecoverBattleFromReconnect에 넘긴다.
        gm.Network.SyncLocalBattleSnapshot(snapshot);
        _lastSyncedBattleTick = 0;
    }

    private void MarkBoardDirty(PokemonUnit _) => _boardDirty = true;

    /// <summary>재접속/파트너 재입장 시 변경 이벤트 없이도 현재 보드를 강제 재송출.</summary>
    private void MarkBoardDirtyForResync() => _boardDirty = true;

    private void HandleUnitCapChanged(int _) => _boardDirty = true;

    private void HandleGoldChanged(int gold)
    {
        var net = GameManager.Instance != null ? GameManager.Instance.Network : null;
        net?.SyncLocalGold(gold);
    }

    /// <summary>증강 선택 시 누적 목록 전체(영문명, 선택 순)를 재송출 — 유실돼도 다음 선택 때 자가 복구된다.</summary>
    private void HandleAugmentSelected(AugmentData _)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Augment == null || gm.Network == null) return;

        var active = gm.Augment.ActiveAugments;
        var names = new System.Collections.Generic.List<string>(active.Count);
        foreach (var augment in active)
        {
            var data = augment?.Data;
            if (data == null) continue;
            string name = !string.IsNullOrEmpty(data.augmentNameEn) ? data.augmentNameEn : data.augmentName;
            if (!string.IsNullOrEmpty(name)) names.Add(name);
        }
        gm.Network.SyncLocalAugments(names.ToArray());
    }

    /// <summary>
    /// 레벨/XP 저장 트리거. ShopManager.AddXp()는 레벨업이 발생하면 OnLevelChanged를 (레벨업 횟수만큼)
    /// 먼저 쏘고 OnXpChanged를 마지막에 한 번만 쏜다 — 그래서 OnLevelChanged는 구독하지 않고
    /// OnXpChanged만 구독해 최종 확정된 레벨/XP를 한 번만 저장한다(중복 저장 방지).
    /// </summary>
    private void HandleXpChanged(int currentXp, int _)
    {
        if (!GameManager.TryGet(out var gm) || gm.Shop == null || gm.Network == null) return;
        gm.Network.SyncLocalProgression(gm.Shop.CurrentLevel, currentXp);
    }

    /// <summary>무료 리롤 잔여 횟수 저장 트리거.</summary>
    private void HandleRerollCountChanged(int rerollCount)
    {
        if (!GameManager.TryGet(out var gm) || gm.Network == null) return;
        gm.Network.SyncLocalRerollCount(rerollCount);
    }

    /// <summary>
    /// 아이템 상점 저장 트리거. RollItemShop()(라운드 시작)과 RerollItemSlot()(슬롯 개별 리롤) 모두
    /// 이 이벤트를 발행하므로 두 경로 모두 여기서 한 번에 커버된다. 이벤트 인자가 없어 ShopManager가
    /// 직접 현재 상태를 직렬화한 JSON을 만들어 넘겨준다.
    /// </summary>
    private void HandleItemShopRerolled()
    {
        if (!GameManager.TryGet(out var gm) || gm.Shop == null || gm.Network == null) return;
        string snapshotJson = gm.Shop.CreateItemShopReconnectSnapshotJson();
        gm.Network.SyncLocalItemShopState(snapshotJson);
    }

    private void LateUpdate()
    {
        SyncBattleTickIfDue();

        if (!_boardDirty) return;
        _boardDirty = false;

        var gm = GameManager.Instance;
        if (gm == null || gm.Board == null || gm.Network == null) return;

        int[] data = BoardSnapshot.FromBoard(gm.Board, gm.Item).Encode();
        gm.Network.BroadcastBoardSnapshot(data);
    }

    /// <summary>
    /// 전투 중 재접속 복구용 — BattleManager.CurrentBattleTick이 마지막 저장값보다
    /// BATTLE_TICK_SYNC_INTERVAL(10 tick≈1초) 이상 진행됐으면 Player CustomProperty에 갱신한다.
    /// 매 tick마다 저장하지 않는다(Photon 호출 최소화). 전투 중이 아니면(CurrentBattleTick이 갱신되지
    /// 않는 상태) 아무 일도 하지 않는다 — _lastSyncedBattleTick과 항상 같은 값을 유지하다가
    /// HandleBattleStart가 다음 전투 시작 때 0으로 리셋한다.
    /// </summary>
    private void SyncBattleTickIfDue()
    {
        if (!GameManager.TryGet(out var gm) || gm.Battle == null || gm.Phase == null || gm.Network == null) return;
        if (gm.Phase.CurrentPhase != GamePhase.Battle) return; // Shopping 등에서는 이전 전투의 남은 tick값을 잘못 저장하지 않는다.

        int currentTick = gm.Battle.CurrentBattleTick;
        if (currentTick - _lastSyncedBattleTick < BATTLE_TICK_SYNC_INTERVAL) return;

        gm.Network.SyncLocalBattleTick(currentTick, gm.Phase.CurrentRound);
        _lastSyncedBattleTick = currentTick;
    }
}
