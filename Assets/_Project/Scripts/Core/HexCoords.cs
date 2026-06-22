using System;
using UnityEngine;

/// <summary>
/// 육각형 큐브 좌표계를 나타내는 구조체 (q + r + s = 0)
/// 딕셔너리의 키(Key)로 사용하기 위해 IEquatable을 구현합니다.
/// </summary>
[Serializable]
public struct HexCoords : IEquatable<HexCoords>
{
    public int q;
    public int r;
    public int s;

    public HexCoords(int q, int r, int s)
    {
        if (q + r + s != 0)
        {
            Debug.LogError($"[HexCoords] 유효하지 않은 큐브 좌표입니다! q+r+s는 0이어야 합니다. 입력값: ({q}, {r}, {s})");
        }

        this.q = q;
        this.r = r;
        this.s = s;
    }

    public HexCoords(int q, int r)
    {
        this.q = q;
        this.r = r;
        this.s = -q - r;
    }

    /// <summary>
    /// 두 헥스 좌표 간의 거리를 반환합니다. (A* 휴리스틱용)
    /// 공식: max(|q1-q2|, |r1-r2|, |s1-s2|)
    /// </summary>
    public int DistanceTo(HexCoords other)
    {
        return Mathf.Max(
            Mathf.Abs(this.q - other.q),
            Mathf.Abs(this.r - other.r),
            Mathf.Abs(this.s - other.s)
        );
    }

    /// <summary>
    /// 플랫 탑(Flat-top) 헥사곤 기준 유니티 월드 좌표(Vector3)로 변환합니다.
    /// </summary>
    /// <param name="hexSize">헥사곤의 중심에서 꼭짓점까지의 거리 (반지름)</param>
    public Vector3 ToWorldPosition(float hexSize = 1f)
    {
        // 플랫 탑의 X 이동량: 1.5 * q
        float x = hexSize * 1.5f * q;

        // 플랫 탑의 Z 이동량: 루트3 * (r + q/2)
        float z = hexSize * Mathf.Sqrt(3f) * (r + q / 2f);

        return new Vector3(x, 0f, z);
    }

    // 큐브 좌표계의 6방향 단위 벡터
    private static readonly HexCoords[] DIRECTIONS =
    {
        new HexCoords(1, 0, -1), new HexCoords(1, -1, 0), new HexCoords(0, -1, 1),
        new HexCoords(-1, 0, 1), new HexCoords(-1, 1, 0), new HexCoords(0, 1, -1)
    };

    /// <summary>인접한 6개 좌표를 반환합니다 (보드 범위 체크는 호출측 책임).</summary>
    public HexCoords[] GetNeighbors()
    {
        var neighbors = new HexCoords[6];
        for (int i = 0; i < 6; i++)
            neighbors[i] = new HexCoords(q + DIRECTIONS[i].q, r + DIRECTIONS[i].r, s + DIRECTIONS[i].s);
        return neighbors;
    }

    #region Equals & GetHashCode (Dictionary Key 사용을 위한 필수 구현)

    public bool Equals(HexCoords other)
    {
        return q == other.q && r == other.r && s == other.s;
    }

    public override bool Equals(object obj)
    {
        return obj is HexCoords other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(q, r, s);
    }

    public static bool operator ==(HexCoords left, HexCoords right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(HexCoords left, HexCoords right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"({q}, {r}, {s})";
    }

    #endregion
}
