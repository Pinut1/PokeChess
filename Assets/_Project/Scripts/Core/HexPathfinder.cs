using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pointy-topped 헥스 보드 A* 경로탐색.
/// 좌표계: odd-r offset (홀수 행이 오른쪽으로 반 칸 밀림).
/// </summary>
public static class HexPathfinder
{
    // ══════════════════════════════════════════════════════
    // 내부 노드
    // ══════════════════════════════════════════════════════

    private sealed class HexNode
    {
        public int Col, Row;
        public int G;           // 시작 → 현재 실제 비용
        public int H;           // 현재 → 목표 휴리스틱 (HexDistance)
        public int F => G + H;  // 총 예상 비용
        public HexNode Parent;

        public HexNode(int col, int row, int g, int h, HexNode parent = null)
        {
            Col = col; Row = row; G = g; H = h; Parent = parent;
        }
    }

    // ══════════════════════════════════════════════════════
    // Pointy-top odd-r 이웃 오프셋
    //
    //  짝수 행(even):        홀수 행(odd):
    //   [-1,-1] [ 0,-1]      [ 0,-1] [+1,-1]
    //   [-1, 0] [+1, 0]      [-1, 0] [+1, 0]
    //   [-1,+1] [ 0,+1]      [ 0,+1] [+1,+1]
    // ══════════════════════════════════════════════════════

    private static readonly (int dc, int dr)[] EvenNeighbors =
    {
        (-1, -1), ( 0, -1),
        (-1,  0), (+1,  0),
        (-1, +1), ( 0, +1),
    };

    private static readonly (int dc, int dr)[] OddNeighbors =
    {
        ( 0, -1), (+1, -1),
        (-1,  0), (+1,  0),
        ( 0, +1), (+1, +1),
    };

    // ══════════════════════════════════════════════════════
    // 공개 API
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// A* 경로 탐색.
    /// <para>반환: [첫 스텝 … 목표] 좌표 목록. 이미 목표면 빈 리스트. 경로 없으면 null.</para>
    /// <para><paramref name="isWalkable"/>: 목표 셀은 항상 통과 가능으로 처리됨.</para>
    /// </summary>
    public static List<Vector2Int> FindPath(
        int startCol, int startRow,
        int goalCol,  int goalRow,
        int boardCols, int boardRows,
        Func<int, int, bool> isWalkable)
    {
        if (startCol == goalCol && startRow == goalRow)
            return new List<Vector2Int>();

        var openList  = new List<HexNode>();
        var closedSet = new HashSet<int>();

        // TODO 1: 시작 노드를 openList에 추가 (G=0, H=HexDistance)

        while (openList.Count > 0)
        {
            // TODO 2: openList에서 F 최솟값 노드 꺼내기
            //         (F 동점이면 H 작은 노드 우선)
            HexNode current = null; // <- 교체 필요

            // TODO 3: 목표 도달 확인 → ReconstructPath 반환

            // TODO 4: closedSet에 추가 (이미 있으면 continue)

            foreach (var nb in GetNeighbors(current.Col, current.Row, boardCols, boardRows))
            {
                bool isGoal = nb.x == goalCol && nb.y == goalRow;

                // TODO 5: closedSet 포함 → skip
                // TODO 6: 장애물이고 목표가 아니면 → skip  (!isGoal && !isWalkable)
                // TODO 7: newG = current.G + 1, newH = HexDistance
                // TODO 8: openList에 같은 좌표의 노드가 있고 G가 더 좋으면 교체, 아니면 skip
                // TODO 9: openList에 새 HexNode 추가
            }
        }

        return null; // 경로 없음
    }

    // ══════════════════════════════════════════════════════
    // 내부 헬퍼
    // ══════════════════════════════════════════════════════

    /// <summary>목표 노드에서 부모를 역추적해 경로 리스트 반환.</summary>
    private static List<Vector2Int> ReconstructPath(HexNode goal)
    {
        var path = new List<Vector2Int>();

        // TODO: goal → Parent → … → Parent==null 까지 순회
        //       path.Add(new Vector2Int(node.Col, node.Row))
        //       마지막에 path.Reverse()

        return path;
    }

    /// <summary>보드 경계 내 6방향 이웃 반환 (Pointy-top odd-r).</summary>
    private static IEnumerable<Vector2Int> GetNeighbors(int col, int row, int boardCols, int boardRows)
    {
        var deltas = (row % 2 == 0) ? EvenNeighbors : OddNeighbors;
        foreach (var (dc, dr) in deltas)
        {
            int nc = col + dc;
            int nr = row + dr;
            if (nc >= 0 && nc < boardCols && nr >= 0 && nr < boardRows)
                yield return new Vector2Int(nc, nr);
        }
    }

    /// <summary>
    /// 두 타일 사이 최단 헥스 거리.
    /// odd-r offset → cube 좌표 변환 후 계산.
    /// <code>
    /// x = col - (row - (row &amp; 1)) / 2
    /// z = row
    /// y = -x - z
    /// dist = (|Δx| + |Δy| + |Δz|) / 2
    /// </code>
    /// </summary>
    private static int HexDistance(int col0, int row0, int col1, int row1)
    {
        // TODO: 위 공식으로 구현
        return 0;
    }

    /// <summary>(col, row) → 정수 해시 키.</summary>
    private static int EncodeKey(int col, int row, int boardCols)
        => row * boardCols + col;
}
