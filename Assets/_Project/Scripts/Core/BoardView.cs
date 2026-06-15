using UnityEngine;

/// <summary>
/// 보드/벤치의 논리 상태(BoardManager)가 바뀔 때마다 유닛 GameObject의 transform을
/// 해당 슬롯 위치로 재배치하는 표현(presentation) 레이어.
/// BoardManager는 논리만 다루므로(스왑/배치 후 위치 미반영) 여기서 전체 resync한다.
/// 보드(≤28)+벤치(≤9) 규모라 매번 전체 재배치로 충분하고 스왑/진화도 자동 반영된다.
/// </summary>
public class BoardView : MonoBehaviour
{
    public static BoardView Instance { get; private set; }

    [SerializeField] private float _unitHeight = 0.5f;

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void OnEnable()
    {
        GameEvents.OnUnitPlaced  += HandleChanged;
        GameEvents.OnUnitBenched += HandleChanged;
        GameEvents.OnUnitSold    += HandleChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnUnitPlaced  -= HandleChanged;
        GameEvents.OnUnitBenched -= HandleChanged;
        GameEvents.OnUnitSold    -= HandleChanged;
    }

    private void HandleChanged(PokemonUnit _) => Resync();

    /// <summary>보드+벤치 스냅샷을 읽어 모든 유닛을 제자리에 배치.</summary>
    public void Resync()
    {
        var board = GameManager.Instance != null ? GameManager.Instance.Board : null;
        if (board == null) return;

        foreach (var kv in board.GetBoardSnapshot())
        {
            PokemonUnit unit = kv.Value;
            if (unit == null) continue;
            unit.transform.position = board.CoordsToWorldPosition(kv.Key) + Vector3.up * _unitHeight;
        }

        var bench = board.GetBenchSnapshot();
        for (int i = 0; i < bench.Count; i++)
        {
            PokemonUnit unit = bench[i];
            if (unit == null) continue;
            unit.transform.position = board.BenchSlotWorldPosition(i) + Vector3.up * _unitHeight;
        }
    }
}
