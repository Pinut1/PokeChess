using UnityEngine;

/// <summary>
/// 7×8 헥사 보드 생성 및 유닛 배치 관리. 김기욱 파트.
/// </summary>
public class BoardManager : MonoBehaviour
{
    [Header("프리팹")]
    [SerializeField] private GameObject hexTilePrefab;

    [Header("보드 크기")]
    [SerializeField] private int boardCols = 7;   // X 방향 칸 수
    [SerializeField] private int boardRows = 8;   // Z 방향 칸 수

    [Header("타일 크기")]
    [SerializeField] private float hexSize = 1f;  // 중심→꼭짓점 거리(m)

    private HexTile[,] _tiles;

    private void Awake()
    {
        GenerateBoard();
    }

    // ──────────────────────────────────────────────────────
    // 보드 생성
    // ──────────────────────────────────────────────────────

    private void GenerateBoard()
    {
        _tiles = new HexTile[boardCols, boardRows];

        // Pointy-top 헥사 칸 간격
        float colStep = Mathf.Sqrt(3f) * hexSize;   // 열 간격
        float rowStep = 1.5f * hexSize;              // 행 간격

        // 보드 전체를 BoardManager 오브젝트 기준 중앙 정렬
        float totalWidth = (boardCols - 1) * colStep;
        float totalDepth = (boardRows - 1) * rowStep;
        Vector3 origin   = transform.position
            - new Vector3(totalWidth * 0.5f, 0f, totalDepth * 0.5f);

        for (int r = 0; r < boardRows; r++)
        {
            // 홀수 행은 반 칸 오른쪽으로 밀어서 벌집 모양 구성
            float xOffset = (r % 2 == 1) ? colStep * 0.5f : 0f;

            for (int c = 0; c < boardCols; c++)
            {
                Vector3 pos = origin + new Vector3(c * colStep + xOffset, 0f, r * rowStep);

                GameObject go = Instantiate(hexTilePrefab, pos, Quaternion.identity, transform);
                go.name = $"HexTile_{c}_{r}";

                HexTile tile = go.GetComponent<HexTile>();
                tile.col = c;
                tile.row = r;
                _tiles[c, r] = tile;
            }
        }
    }

    // ──────────────────────────────────────────────────────
    // 유닛 배치 API
    // ──────────────────────────────────────────────────────

    /// <summary>유닛을 보드 타일에 올린다. 성공 여부 반환.</summary>
    public bool PlaceUnit(PokemonUnit unit, int col, int row)
    {
        if (!IsValid(col, row)) return false;

        HexTile tile = _tiles[col, row];
        if (tile.IsOccupied) return false;

        RemoveFromBoard(unit);  // 기존 타일에서 먼저 제거

        tile.occupant = unit;
        tile.RefreshColor();
        unit.isOnBoard = true;
        unit.transform.position = tile.transform.position;

        GameEvents.UnitPlaced(unit);
        return true;
    }

    /// <summary>유닛을 보드에서 들어내어 벤치로 보낸다.</summary>
    public void BenchUnit(PokemonUnit unit)
    {
        RemoveFromBoard(unit);
        unit.isOnBoard = false;
        GameEvents.UnitBenched(unit);
    }

    /// <summary>좌표로 타일 반환. 범위 밖이면 null.</summary>
    public HexTile GetTile(int col, int row)
        => IsValid(col, row) ? _tiles[col, row] : null;

    /// <summary>유닛이 현재 있는 타일 반환. 없으면 null.</summary>
    public HexTile GetTileOf(PokemonUnit unit)
    {
        foreach (HexTile tile in _tiles)
            if (tile.occupant == unit) return tile;
        return null;
    }

    // ──────────────────────────────────────────────────────
    // 내부 헬퍼
    // ──────────────────────────────────────────────────────

    private void RemoveFromBoard(PokemonUnit unit)
    {
        HexTile prev = GetTileOf(unit);
        if (prev == null) return;
        prev.occupant = null;
        prev.RefreshColor();
    }

    private bool IsValid(int col, int row)
        => col >= 0 && col < boardCols && row >= 0 && row < boardRows;
}
