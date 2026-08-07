using System.Collections.Generic;

/// <summary>
/// 한 플레이어의 보드+벤치 배치를 네트워크로 보내기 위한 경량 스냅샷.
/// 상대 클라이언트는 이 스냅샷만 받아 "읽기 전용 미러"로 렌더한다(풀 BoardManager 불필요).
///
/// 동기화 원칙(설계 결정 2026-06-16): 각자 자기 보드의 권위자, 상대 보드는 복제된 시각 미러.
/// 전투는 각 클라가 자기 보드로 로컬 시뮬하므로 위치/종/성급만 있으면 충분했으나, 쇼핑 중 파트너
/// 전체화면 관전에 장착 아이템/인벤토리를 보여주기 위해 최소한으로 확장했다(2026-08).
///
/// PUN2 RPC는 int[] 같은 기본 배열을 그대로 직렬화하므로 커스텀 타입 등록 없이 전송 가능.
/// 인코딩: [count, (speciesId, starLevel, onBoard, a, b, itemId0, itemId1) × count,
///          invItemCount, itemId × invItemCount, invStoneCount, stoneId × invStoneCount,
///          hasRemover(0/1), reforgerCount, unitCap]
///   - onBoard == 1: a=q, b=r (보드 큐브 좌표; s는 -q-r로 복원)
///   - onBoard == 0: a=slot, b=0 (벤치 슬롯)
///   - itemId0/itemId1 == 0: 해당 슬롯 미장착 (BattleSnapshot.UnitEntry와 동일 의미)
///   - 꼬리(인벤토리) 섹션은 유닛 배치와 무관한 플레이어 레벨 데이터라 entries 뒤에 이어 붙인다.
///     Decode는 각 섹션 진입 전 idx가 배열 범위 안인지 확인해, 꼬리가 없는(구버전) 데이터를 받아도
///     예외 없이 그 지점에서 멈춘다(기본값 유지) — 이번 확장 자체에는 버전 협상이 없으므로 방어용.
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
        public int itemId0; // 0 = 슬롯0 미장착. PokemonUnit.MaxItemSlots(2) 고정 슬롯.
        public int itemId1; // 0 = 슬롯1 미장착.
    }

    private const int FIELDS_PER_ENTRY = 7;

    public readonly List<Entry> entries = new();

    // ── 인벤토리(미장착) — 유닛 배치와 무관한 플레이어 레벨 데이터 ──
    public readonly List<int> inventoryItemIds = new();
    public readonly List<int> inventoryStoneIds = new();
    public bool hasRemover;
    public int reforgerCount;

    /// <summary>BoardManager.UnitCap 그대로(레벨→상한 계산은 ShopManager 소유라 여기서 재계산하지 않고,
    /// 이미 계산된 상한값만 전송해 전체화면 파트너 관전의 유닛 수 표시에 재사용한다). 기본값 0.</summary>
    public int unitCap;

    /// <summary>BoardManager/ItemManager의 보드·인벤토리 스냅샷에서 전송용 스냅샷을 만든다(로컬 권위 측에서 호출).
    /// item이 null이면(호출부가 아직 준비 안 된 경우 등) 인벤토리 섹션은 빈 채로 둔다.</summary>
    public static BoardSnapshot FromBoard(BoardManager board, ItemManager item)
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
                b = kv.Key.r,
                itemId0 = EquippedItemIdAt(unit, 0),
                itemId1 = EquippedItemIdAt(unit, 1)
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
                b = 0,
                itemId0 = EquippedItemIdAt(unit, 0),
                itemId1 = EquippedItemIdAt(unit, 1)
            });
        }

        if (item != null)
        {
            foreach (ItemData i in item.Items)
                if (i != null) snap.inventoryItemIds.Add(i.id);

            foreach (EvolutionStoneData s in item.Stones)
                if (s != null) snap.inventoryStoneIds.Add(s.id);

            snap.hasRemover = item.HasRemover;
            snap.reforgerCount = item.ReforgerCount;
        }

        snap.unitCap = board.UnitCap;

        return snap;
    }

    /// <summary>unit.items[slot]의 id. 슬롯이 비었으면(범위 밖/null) 0 — BattleSnapshotCodec.BuildUnitEntry와 동일 규칙.</summary>
    private static int EquippedItemIdAt(PokemonUnit unit, int slot)
    {
        if (unit.items == null || slot >= unit.items.Count || unit.items[slot] == null) return 0;
        return unit.items[slot].id;
    }

    /// <summary>RPC 전송용 int[]로 직렬화.</summary>
    public int[] Encode()
    {
        int size = 1 + entries.Count * FIELDS_PER_ENTRY
                   + 1 + inventoryItemIds.Count
                   + 1 + inventoryStoneIds.Count
                   + 1  // hasRemover
                   + 1  // reforgerCount
                   + 1; // unitCap

        var data = new int[size];
        int idx = 0;

        data[idx++] = entries.Count;
        foreach (var e in entries)
        {
            data[idx++] = e.speciesId;
            data[idx++] = e.starLevel;
            data[idx++] = e.onBoard ? 1 : 0;
            data[idx++] = e.a;
            data[idx++] = e.b;
            data[idx++] = e.itemId0;
            data[idx++] = e.itemId1;
        }

        data[idx++] = inventoryItemIds.Count;
        foreach (int id in inventoryItemIds) data[idx++] = id;

        data[idx++] = inventoryStoneIds.Count;
        foreach (int id in inventoryStoneIds) data[idx++] = id;

        data[idx++] = hasRemover ? 1 : 0;
        data[idx++] = reforgerCount;
        data[idx++] = unitCap;

        return data;
    }

    /// <summary>RPC로 받은 int[]를 스냅샷으로 복원. 각 섹션 진입 전 idx 범위를 확인해, 꼬리가 잘린
    /// (구버전) 데이터를 받아도 예외 없이 그 지점까지만 채우고 멈춘다.</summary>
    public static BoardSnapshot Decode(int[] data)
    {
        var snap = new BoardSnapshot();
        if (data == null || data.Length == 0) return snap;

        int idx = 0;
        int count = data[idx++];
        for (int i = 0; i < count && idx + FIELDS_PER_ENTRY - 1 < data.Length; i++)
        {
            snap.entries.Add(new Entry
            {
                speciesId = data[idx++],
                starLevel = data[idx++],
                onBoard = data[idx++] == 1,
                a = data[idx++],
                b = data[idx++],
                itemId0 = data[idx++],
                itemId1 = data[idx++]
            });
        }

        if (idx < data.Length)
        {
            int itemCount = data[idx++];
            for (int i = 0; i < itemCount && idx < data.Length; i++)
                snap.inventoryItemIds.Add(data[idx++]);
        }

        if (idx < data.Length)
        {
            int stoneCount = data[idx++];
            for (int i = 0; i < stoneCount && idx < data.Length; i++)
                snap.inventoryStoneIds.Add(data[idx++]);
        }

        if (idx < data.Length) snap.hasRemover = data[idx++] == 1;
        if (idx < data.Length) snap.reforgerCount = data[idx++];
        if (idx < data.Length) snap.unitCap = data[idx++];

        return snap;
    }
}
