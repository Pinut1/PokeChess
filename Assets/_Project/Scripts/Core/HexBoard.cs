using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 헥스 보드 순수 기하 유틸리티. odd-r offset 좌표계 기준.
/// MonoBehaviour 없이 어디서든 호출 가능한 static 클래스.
/// </summary>
public static class HexBoard
{
    // ──────────────────────────────────────────────────────
    // 좌표 변환
    // ──────────────────────────────────────────────────────

    /// <summary>odd-r offset → cube 좌표 (x+y+z=0 불변)</summary>
    private static Vector3Int OffsetToCube(int col, int row)
    {
        int x = col - (row - (row & 1)) / 2;
        int z = row;
        int y = -x - z;
        return new Vector3Int(x, y, z);
    }

    // ──────────────────────────────────────────────────────
    // 거리 (체비셰프/맨해튼을 모두 대체)
    // ──────────────────────────────────────────────────────

    /// <summary>두 타일 사이의 최단 이동 칸 수.</summary>
    public static int HexDistance(int col0, int row0, int col1, int row1)
    {
        var a = OffsetToCube(col0, row0);
        var b = OffsetToCube(col1, row1);
        return (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z)) / 2;
    }

    // ──────────────────────────────────────────────────────
    // 인접 셀
    // ──────────────────────────────────────────────────────

    /// <summary>
    /// 보드 경계 내 6방향 이웃 셀. odd-r offset 기준.
    /// 짝수 행은 왼쪽, 홀수 행은 오른쪽으로 반 칸 오프셋.
    /// </summary>
    public static IEnumerable<Vector2Int> GetNeighbors(int col, int row, int boardCols, int boardRows)
    {
        int[] dc, dr;
        if (row % 2 == 0)
        {
            dc = new[] { -1,  0, -1, +1, -1,  0 };
            dr = new[] { -1, -1,  0,  0, +1, +1 };
        }
        else
        {
            dc = new[] {  0, +1, -1, +1,  0, +1 };
            dr = new[] { -1, -1,  0,  0, +1, +1 };
        }

        for (int i = 0; i < 6; i++)
        {
            int nc = col + dc[i];
            int nr = row + dr[i];
            if (nc >= 0 && nc < boardCols && nr >= 0 && nr < boardRows)
                yield return new Vector2Int(nc, nr);
        }
    }

    // ──────────────────────────────────────────────────────
    // 범위 셀 집합
    // ──────────────────────────────────────────────────────

    /// <summary>거리 ≤ range인 모든 셀 (스킬 폭발 범위 등).</summary>
    public static List<Vector2Int> GetCellsInRange(
        int col, int row, int range, int boardCols, int boardRows)
    {
        var result = new List<Vector2Int>();
        for (int r = 0; r < boardRows; r++)
        for (int c = 0; c < boardCols; c++)
            if (HexDistance(col, row, c, r) <= range)
                result.Add(new Vector2Int(c, r));
        return result;
    }

    /// <summary>거리 == range인 셀만 (링 모양, A* 경로 후보용).</summary>
    public static List<Vector2Int> GetOuterRangeCells(
        int col, int row, int range, int boardCols, int boardRows)
    {
        var result = new List<Vector2Int>();
        for (int r = 0; r < boardRows; r++)
        for (int c = 0; c < boardCols; c++)
            if (HexDistance(col, row, c, r) == range)
                result.Add(new Vector2Int(c, r));
        return result;
    }
}
