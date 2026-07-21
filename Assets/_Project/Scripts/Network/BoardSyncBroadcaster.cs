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

    private void OnEnable()
    {
        GameEvents.OnUnitPlaced      += MarkBoardDirty;
        GameEvents.OnUnitBenched     += MarkBoardDirty;
        GameEvents.OnUnitSold        += MarkBoardDirty;
        GameEvents.OnGoldChanged     += HandleGoldChanged;
        GameEvents.OnAugmentSelected += HandleAugmentSelected;
        GameEvents.OnBoardResyncRequested += MarkBoardDirtyForResync;
    }

    private void OnDisable()
    {
        GameEvents.OnUnitPlaced      -= MarkBoardDirty;
        GameEvents.OnUnitBenched     -= MarkBoardDirty;
        GameEvents.OnUnitSold        -= MarkBoardDirty;
        GameEvents.OnGoldChanged     -= HandleGoldChanged;
        GameEvents.OnAugmentSelected -= HandleAugmentSelected;
        GameEvents.OnBoardResyncRequested -= MarkBoardDirtyForResync;
    }

    private void MarkBoardDirty(PokemonUnit _) => _boardDirty = true;

    /// <summary>재접속/파트너 재입장 시 변경 이벤트 없이도 현재 보드를 강제 재송출.</summary>
    private void MarkBoardDirtyForResync() => _boardDirty = true;

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

    private void LateUpdate()
    {
        if (!_boardDirty) return;
        _boardDirty = false;

        var gm = GameManager.Instance;
        if (gm == null || gm.Board == null || gm.Network == null) return;

        int[] data = BoardSnapshot.FromBoard(gm.Board).Encode();
        gm.Network.BroadcastBoardSnapshot(data);
    }
}
