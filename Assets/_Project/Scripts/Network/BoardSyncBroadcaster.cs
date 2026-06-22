using UnityEngine;

/// <summary>
/// 로컬 플레이어의 보드/벤치/골드 변경을 감지해 NetworkManager로 송출하는 다리.
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
        GameEvents.OnUnitPlaced  += MarkBoardDirty;
        GameEvents.OnUnitBenched += MarkBoardDirty;
        GameEvents.OnUnitSold    += MarkBoardDirty;
        GameEvents.OnGoldChanged += HandleGoldChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnUnitPlaced  -= MarkBoardDirty;
        GameEvents.OnUnitBenched -= MarkBoardDirty;
        GameEvents.OnUnitSold    -= MarkBoardDirty;
        GameEvents.OnGoldChanged -= HandleGoldChanged;
    }

    private void MarkBoardDirty(PokemonUnit _) => _boardDirty = true;

    private void HandleGoldChanged(int gold)
    {
        var net = GameManager.Instance != null ? GameManager.Instance.Network : null;
        net?.SyncLocalGold(gold);
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
