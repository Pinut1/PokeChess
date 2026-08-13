using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 플레이어의 보드+벤치 배치를 네트워크로 보내기 위한 경량 스냅샷.
/// 상대 클라이언트는 이 스냅샷만 받아 "읽기 전용 미러"로 렌더한다(풀 BoardManager 불필요).
///
/// 동기화 원칙(설계 결정 2026-06-16): 각자 자기 보드의 권위자, 상대 보드는 복제된 시각 미러.
/// 전투는 각 클라가 자기 보드로 로컬 시뮬하므로 위치/종/성급만 있으면 충분했으나, 쇼핑 중 파트너
/// 전체화면 관전에 장착 아이템/인벤토리를 보여주기 위해 최소한으로 확장했다(2026-08).
///
/// PUN2 RPC는 int[] 같은 기본 배열을 그대로 직렬화하므로 커스텀 타입 등록 없이 전송 가능.
/// 인코딩(v3, 2026-08 파트너 마나바 수정 대응): [MAGIC, 0, 0, 0, 0, unitCap(legacy용 사본), version, count,
///          (speciesId, starLevel, onBoard, a, b, itemId0, itemId1, roleOverride,
///          attackRangeOverride, heroStatMultiplier, isTradeEvolved, equippedStoneId,
///          effectiveManaCost) × count,
///          invItemCount, itemId × invItemCount, invStoneCount, stoneId × invStoneCount,
///          hasRemover(0/1), reforgerCount, unitCap]
///
///   - 앞 6개 int(LEGACY_PREFIX_SIZE)는 "legacy-safe compatibility prefix"다. v1(버전 개념이
///     아예 없던 구버전 — 첫 int를 곧바로 entries count로 읽음)과 v2+ 사이를 잇는 1회성 다리로,
///     v1 Decoder가 이 6개를 자신의 기존 파싱 순서(count→itemCount→stoneCount→hasRemover→
///     reforgerCount→unitCap)로 읽어도 반드시 entries 0개·인벤토리 0개·hasRemover false·
///     reforgerCount 0·정확한 unitCap으로 안전하게 끝나도록 값을 맞춰 넣는다(2026-08 코드리뷰
///     후속 대응, git 히스토리 기준 v1 Decode의 정확한 소비 순서로 인덱스 단위 검증 완료).
///     v1 Decoder는 이 6개 int만 읽고 함수를 끝내므로(그 뒤는 아예 읽지 않음) data[6] 이후는
///     v1 쪽에서 전혀 관여하지 않는다.
///   - LEGACY_COMPAT_MAGIC(data[0]): 반드시 음수인 상수. v1 payload의 첫 int(entries count)는
///     List.Count라 항상 0 이상이므로, 음수 MAGIC은 v1 payload와 확률이 아니라 구조적으로
///     절대 충돌하지 않는다 — "우연히 count==2였을 때만 버전 오판"하던 이전(2026-08 1차 대응)
///     버전만 있던 구조의 결함을 근본적으로 없앤다.
///   - version(data[6]): CURRENT_VERSION과 다르면 Decode가 즉시 null을 반환한다(필드 밀림으로
///     잘못된 값을 조용히 만들어내는 대신 명시적으로 실패 — BattleSnapshotCodec.CURRENT_VERSION과
///     같은 원칙). v3 이후 레이아웃이 또 바뀌어도 이 6-int legacy prefix를 다시 늘릴 필요는 없다
///     — v2+ Decoder는 이미 MAGIC부터 확인하므로, v2 Decoder가 v3 payload를 받아도 MAGIC은
///     일치하고 version만 달라 정상적으로(경고 후 null) 거부된다. legacy prefix는 "버전 개념이
///     전혀 없던 v1"과의 호환에만 필요한 값이라 한 번만 만들면 된다.
///   - onBoard == 1: a=q, b=r (보드 큐브 좌표; s는 -q-r로 복원)
///   - onBoard == 0: a=slot, b=0 (벤치 슬롯)
///   - itemId0/itemId1 == 0: 해당 슬롯 미장착 (BattleSnapshot.UnitEntry와 동일 의미)
///   - roleOverride: 0=오버라이드 없음, 1~6=PokemonRole 상수 인덱스(EncodeRoleOverride/DecodeRoleOverride 참고).
///     BoardSnapshot이 int[] 단일 채널이라(BattleSnapshot처럼 string[]이 없음) 문자열 대신
///     고정된 6종 역할 집합을 정수 인덱스로 왕복한다 — 시트에서 온 자유 문자열(PokemonData.role)이
///     아니라 HeroAugment가 PokemonRole.XXX 상수만 대입하는 값이라 안전하다.
///   - attackRangeOverride: PokemonUnit.NoRangeOverride(-1)=오버라이드 없음, 그 외 실제 사거리값.
///   - heroStatMultiplier: ×100 스케일 정수(예: 1.4 → 140). 오버라이드 없는 기본값 1f는 100으로
///     인코딩되며, 이는 "배수 없음"과 값이 같아 의미상 자연스럽게 no-op로 복원된다.
///   - isTradeEvolved: 0/1. PokemonUnit.isTradeEvolved 그대로 — 파트너 프리뷰가 특수진화 성급
///     배율표(SPECIAL_STAR_MULTIPLIER)를 골라 쓰기 위해 필요(2026-08 코드리뷰 대응, 기존엔 없어서
///     통신진화/돌진화 유닛이 항상 일반 배율로 낮게 표시됐다).
///   - equippedStoneId: 0 = 미장착. EvolutionStoneData.id 그대로(itemId0/1과 같은 규약).
///   - effectiveManaCost: PokemonUnit.EffectiveManaCost 그대로. 0 = 마나바 없음(스킬 없음), 그 외 값이면
///     파트너 프리뷰가 마나바를 표시한다(2026-08 코드리뷰 대응 — 기존엔 없어서 영웅증강으로 스킬을
///     주입받은 유닛(원본 종 manaCost=0)의 파트너 쇼핑 프리뷰에 마나바가 아예 안 뜨는 문제가 있었다).
///   - 꼬리(인벤토리) 섹션은 유닛 배치와 무관한 플레이어 레벨 데이터라 entries 뒤에 이어 붙인다.
///     Decode는 각 섹션 진입 전 idx가 배열 범위 안인지 확인해, 꼬리가 잘린 데이터를 받아도 예외
///     없이 그 지점에서 멈춘다(기본값 유지) — version이 맞은 뒤의 "부분 전송"에 대한 방어이며,
///     레이아웃 자체가 다른(구버전) 데이터는 위 version 체크가 먼저 걸러낸다.
/// </summary>
public class BoardSnapshot
{
    /// <summary>현재 페이로드 버전. 필드 추가 등으로 레이아웃이 바뀔 때마다 올린다(v1: 기존 10필드,
    /// v2: isTradeEvolved/equippedStoneId 2필드 추가, v3: effectiveManaCost 1필드 추가(2026-08
    /// 파트너 마나바 표시 오류 대응) — 전부 2026-08 코드리뷰 대응). v4 이후로도 이 상수만 올리면
    /// 되고, LEGACY_COMPAT_MAGIC/LEGACY_PREFIX_SIZE는 다시 건드릴 필요 없다.</summary>
    public const int CURRENT_VERSION = 3;

    /// <summary>legacy-safe prefix 식별자(data[0]). 버전 개념이 없던 v1 Decoder는 이 자리를
    /// entries count로 읽는데, List.Count는 절대 음수가 될 수 없으므로 음수 상수를 쓰면 v1 payload와
    /// 구조적으로 충돌 불가능하다(count가 우연히 이 값과 같아지는 시나리오 자체가 존재하지 않음).
    /// 값 자체의 의미는 없고 "반드시 음수"라는 성질만 중요하다.</summary>
    private const int LEGACY_COMPAT_MAGIC = -20260812;

    /// <summary>legacy-safe prefix 길이(MAGIC + legacy itemCount/stoneCount/hasRemover/reforgerCount
    /// + legacy unitCap = 6). v1 Decoder가 MAGIC을 음수 count로 읽어 entries 루프를 0회 건너뛴 뒤
    /// 이어서 소비하는 정확히 6개 필드에 대응한다(git 히스토리 기준 v1 Decode 순서로 검증됨) — v1
    /// Decoder는 이 6개를 다 읽으면 함수를 끝내므로 data[LEGACY_PREFIX_SIZE] 이후는 절대 읽지 않는다.</summary>
    private const int LEGACY_PREFIX_SIZE = 6;

    public struct Entry
    {
        public int speciesId;
        public int starLevel;
        public bool onBoard;
        public int a; // onBoard ? q : slot
        public int b; // onBoard ? r : 0
        public int itemId0; // 0 = 슬롯0 미장착. PokemonUnit.MaxItemSlots(2) 고정 슬롯.
        public int itemId1; // 0 = 슬롯1 미장착.
        public string roleOverride;     // "" = 오버라이드 없음. PokemonUnit.roleOverride와 동일 의미.
        public int attackRangeOverride; // PokemonUnit.NoRangeOverride(-1) = 오버라이드 없음.
        public float heroStatMultiplier; // PokemonUnit.heroStatMultiplier. 기본값 1f = 배수 없음.
        public bool isTradeEvolved;     // PokemonUnit.isTradeEvolved 그대로.
        public int equippedStoneId;     // 0 = 미장착. EvolutionStoneData.id.
        public int effectiveManaCost;   // PokemonUnit.EffectiveManaCost 그대로. 0 = 마나바 없음.
    }

    /// <summary>roleOverride 문자열 ↔ int[] 정수 인덱스 왕복 전용(BoardSnapshot의 int[] 단일 채널
    /// 제약 때문에 string[]을 새로 열지 않고 고정 인덱스로 압축한다). PokemonRole의 6개 상수
    /// 순서를 그대로 따르며, 0은 "오버라이드 없음"으로 예약한다. 새 역할이 추가되면 이 배열 끝에만
    /// 추가할 것 — 중간에 끼워 넣으면 이미 전송된 인덱스 의미가 바뀐다(RewardKind/SoundId와 같은 이유).</summary>
    private static readonly string[] ROLE_OVERRIDE_TABLE =
    {
        PokemonRole.Tanker, PokemonRole.Warrior, PokemonRole.Assassin,
        PokemonRole.Magician, PokemonRole.Archer, PokemonRole.Supporter
    };

    private const int HERO_STAT_MULTIPLIER_SCALE = 100;

    /// <summary>roleOverride 문자열 → int[] 인덱스. 빈 값/알 수 없는 값은 0(오버라이드 없음)으로
    /// 안전 폴백한다(PokemonRole.cs의 "타겟팅 쪽은 알 수 없는 값을 안전하게 폴백" 원칙과 동일).</summary>
    private static int EncodeRoleOverride(string roleOverride)
    {
        if (string.IsNullOrEmpty(roleOverride)) return 0;
        for (int i = 0; i < ROLE_OVERRIDE_TABLE.Length; i++)
            if (ROLE_OVERRIDE_TABLE[i] == roleOverride) return i + 1;
        return 0;
    }

    /// <summary>int[] 인덱스 → roleOverride 문자열. 범위 밖 값은 ""(오버라이드 없음)으로 폴백.</summary>
    private static string DecodeRoleOverride(int index)
    {
        int tableIndex = index - 1;
        if (tableIndex < 0 || tableIndex >= ROLE_OVERRIDE_TABLE.Length) return "";
        return ROLE_OVERRIDE_TABLE[tableIndex];
    }

    private const int FIELDS_PER_ENTRY = 13; // v3: 기존 10 + isTradeEvolved + equippedStoneId + effectiveManaCost

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
                itemId1 = EquippedItemIdAt(unit, 1),
                roleOverride = unit.roleOverride ?? "",
                attackRangeOverride = unit.attackRangeOverride,
                heroStatMultiplier = unit.heroStatMultiplier,
                isTradeEvolved = unit.isTradeEvolved,
                equippedStoneId = unit.equippedStone != null ? unit.equippedStone.id : 0,
                effectiveManaCost = unit.EffectiveManaCost
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
                itemId1 = EquippedItemIdAt(unit, 1),
                roleOverride = unit.roleOverride ?? "",
                attackRangeOverride = unit.attackRangeOverride,
                heroStatMultiplier = unit.heroStatMultiplier,
                isTradeEvolved = unit.isTradeEvolved,
                equippedStoneId = unit.equippedStone != null ? unit.equippedStone.id : 0,
                effectiveManaCost = unit.EffectiveManaCost
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

    /// <summary>RPC 전송용 int[]로 직렬화. 맨 앞에 legacy-safe prefix(6-int) + CURRENT_VERSION을
    /// 싣는다(2026-08 코드리뷰 대응, 클래스 doc의 layout/WHY 참고).</summary>
    public int[] Encode()
    {
        int size = LEGACY_PREFIX_SIZE // MAGIC + legacy itemCount/stoneCount/hasRemover/reforgerCount + legacy unitCap
                   + 1  // version
                   + 1 + entries.Count * FIELDS_PER_ENTRY
                   + 1 + inventoryItemIds.Count
                   + 1 + inventoryStoneIds.Count
                   + 1  // hasRemover
                   + 1  // reforgerCount
                   + 1; // unitCap

        var data = new int[size];
        int idx = 0;

        // legacy-safe prefix — v1(버전 없던 구버전) Decoder 전용 미끼. 클래스 doc 참고.
        data[idx++] = LEGACY_COMPAT_MAGIC;
        data[idx++] = 0; // legacy itemCount → v1 Decoder가 inventoryItemIds를 비운 채 종료
        data[idx++] = 0; // legacy stoneCount → v1 Decoder가 inventoryStoneIds를 비운 채 종료
        data[idx++] = 0; // legacy hasRemover(false)
        data[idx++] = 0; // legacy reforgerCount
        data[idx++] = unitCap; // legacy unitCap — v1 Decoder가 마지막으로 읽는 필드, 실제 값을 그대로 사용

        data[idx++] = CURRENT_VERSION;
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
            data[idx++] = EncodeRoleOverride(e.roleOverride);
            data[idx++] = e.attackRangeOverride;
            data[idx++] = Mathf.RoundToInt(e.heroStatMultiplier * HERO_STAT_MULTIPLIER_SCALE);
            data[idx++] = e.isTradeEvolved ? 1 : 0;
            data[idx++] = e.equippedStoneId;
            data[idx++] = e.effectiveManaCost;
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

    /// <summary>RPC로 받은 int[]를 스냅샷으로 복원. data[0]이 LEGACY_COMPAT_MAGIC과 다르면(=v1
    /// payload이거나 손상된 데이터) 즉시 null, legacy prefix를 건너뛴 뒤 version이 CURRENT_VERSION과
    /// 다르면 역시 즉시 null을 반환한다 — 필드 밀림으로 잘못된 값을 만들어내는 대신 명시적으로
    /// 실패한다(2026-08 코드리뷰 대응 — BattleSnapshotCodec.CURRENT_VERSION과 같은 원칙, 클래스 doc의
    /// layout/WHY 참고). 호출부(NetworkManager.RPC_OnBoardSnapshot → OpponentBoardView.Render)는 이미
    /// snap == null을 무시하고 마지막으로 렌더된 상태를 유지하므로 별도 방어 코드 없이 안전하다.
    /// MAGIC·version이 맞은 뒤에는 각 섹션 진입 전 idx 범위를 확인해, 꼬리가 잘린 데이터를 받아도
    /// 예외 없이 그 지점까지만 채우고 멈춘다.</summary>
    public static BoardSnapshot Decode(int[] data)
    {
        var snap = new BoardSnapshot();
        if (data == null || data.Length == 0) return snap;

        if (data.Length < LEGACY_PREFIX_SIZE + 1) // legacy prefix(6) + version(1) 최소 헤더 길이
        {
            Debug.LogWarning($"[BoardSnapshot] 헤더 길이 부족({data.Length}) — 스냅샷 무시");
            return null;
        }

        if (data[0] != LEGACY_COMPAT_MAGIC)
        {
            Debug.LogWarning($"[BoardSnapshot] MAGIC 불일치({data[0]}) — 구버전(v1) 또는 손상된 payload로 간주, 스냅샷 무시");
            return null;
        }

        int idx = LEGACY_PREFIX_SIZE; // legacy-safe prefix(data[0..5], v1 Decoder 전용)는 건너뜀

        int version = data[idx++];
        if (version != CURRENT_VERSION)
        {
            Debug.LogWarning($"[BoardSnapshot] 지원하지 않는 버전 {version}(현재 {CURRENT_VERSION}) — 스냅샷 무시");
            return null;
        }

        if (idx >= data.Length) return snap; // 버전만 오고 나머지가 잘린 경우 — 빈 스냅샷으로 안전 반환

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
                itemId1 = data[idx++],
                roleOverride = DecodeRoleOverride(data[idx++]),
                attackRangeOverride = data[idx++],
                heroStatMultiplier = data[idx++] / (float)HERO_STAT_MULTIPLIER_SCALE,
                isTradeEvolved = data[idx++] == 1,
                equippedStoneId = data[idx++],
                effectiveManaCost = data[idx++]
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
