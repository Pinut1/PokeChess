using System.Collections.Generic;

/// <summary>
/// 한 플레이어의 보드+벤치 배치를 네트워크로 보내기 위한 경량 스냅샷.
/// 상대 클라이언트는 이 스냅샷만 받아 "읽기 전용 미러"로 렌더한다(풀 BoardManager 불필요).
///
/// 동기화 원칙(설계 결정 2026-06-16): 각자 자기 보드의 권위자, 상대 보드는 복제된 시각 미러.
/// 전투는 각 클라가 자기 보드로 로컬 시뮬하므로 위치/종/성급만 있으면 충분하다.
///
/// PUN2 RPC는 int[] 같은 기본 배열을 그대로 직렬화하므로 커스텀 타입 등록 없이 전송 가능.
/// 인코딩: [count, (speciesId, starLevel, onBoard, a, b) × count]
///   - onBoard == 1: a=q, b=r (보드 큐브 좌표; s는 -q-r로 복원)
///   - onBoard == 0: a=slot, b=0 (벤치 슬롯)
/// </summary>
public class BoardSnapshot
{
    public struct Entry
    {
        public int speciesId;
        public int starLevel;
        public bool onBoard;
        public int a; // onBoard ? q : slot
        public int b; // onBoard ? r : 0
    }

    private const int FIELDS_PER_ENTRY = 5;

    public readonly List<Entry> entries = new();

    /// <summary>BoardManager의 보드/벤치 스냅샷에서 전송용 스냅샷을 만든다(로컬 권위 측에서 호출).</summary>
    public static BoardSnapshot FromBoard(BoardManager board)
    {
        var snap = new BoardSnapshot();
        if (board == null) return snap;

        foreach (var kv in board.GetBoardSnapshot())
        {
            PokemonUnit unit = kv.Value;
            if (unit == null || unit.data == null) continue;
            snap.entries.Add(new Entry
            {
                speciesId = unit.data.id,
                starLevel = unit.starLevel,
                onBoard = true,
                a = kv.Key.q,
                b = kv.Key.r
            });
        }

        var bench = board.GetBenchSnapshot();
        for (int i = 0; i < bench.Count; i++)
        {
            PokemonUnit unit = bench[i];
            if (unit == null || unit.data == null) continue;
            snap.entries.Add(new Entry
            {
                speciesId = unit.data.id,
                starLevel = unit.starLevel,
                onBoard = false,
                a = i,
                b = 0
            });
        }

        return snap;
    }

    /// <summary>RPC 전송용 int[]로 직렬화.</summary>
    public int[] Encode()
    {
        var data = new int[1 + entries.Count * FIELDS_PER_ENTRY];
        data[0] = entries.Count;
        int idx = 1;
        foreach (var e in entries)
        {
            data[idx++] = e.speciesId;
            data[idx++] = e.starLevel;
            data[idx++] = e.onBoard ? 1 : 0;
            data[idx++] = e.a;
            data[idx++] = e.b;
        }
        return data;
    }

    /// <summary>RPC로 받은 int[]를 스냅샷으로 복원.</summary>
    public static BoardSnapshot Decode(int[] data)
    {
        var snap = new BoardSnapshot();
        if (data == null || data.Length == 0) return snap;

        int count = data[0];
        int idx = 1;
        for (int i = 0; i < count && idx + FIELDS_PER_ENTRY - 1 < data.Length; i++)
        {
            snap.entries.Add(new Entry
            {
                speciesId = data[idx++],
                starLevel = data[idx++],
                onBoard = data[idx++] == 1,
                a = data[idx++],
                b = data[idx++]
            });
        }
        return snap;
    }
}
