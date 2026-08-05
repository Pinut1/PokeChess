using System.Collections.Generic;

/// <summary>
/// BattleSnapshot 생성(Create) + 직렬화(Encode/TryDecode) 전담.
///
/// BoardSnapshot과 같은 원칙(주석: "PUN2는 int[] 같은 기본 배열을 그대로 직렬화하므로 커스텀 타입
/// 등록 없이 전송 가능")을 따르되, 이번 데이터는 float·string·유닛별 필드가 섞여 있어 int[] 하나에
/// 욱여넣지 않는다. 대신 PUN2 RPC가 그대로 인자로 받을 수 있는 기본 배열 3종(int[]/float[]/string[])
/// 으로 나눠 인코딩한다 — 이번 단계는 이 형식만 정의하고 NetworkManager RPC 연결은 하지 않는다.
///
/// [돌연변이 봇 재현 가능성 — 구조 확인, 미구현]
/// SpawnMutantBots(BattleManager.cs)의 tier는 GetActiveSynergy("Mutant").activeTierIndex + 1에서 오고,
/// 이 값은 activeSynergies에 그대로 담긴다(synergyId → SynergyDatabase.Instance로 역참조하면
/// synergyNameEn=="Mutant" 판정 가능). 빈 타일 목록은 이 스냅샷에 직접 담지 않지만, 보드 좌표 전체
/// 집합은 양쪽 클라의 로컬 BoardManager가 이미 동일하게 알고 있으므로, "전체 좌표 - units의 (q,r)
/// 점유 좌표" 차집합을 q,r로 정렬하면 SpawnMutantBots과 동일한 빈 타일 순서를 미러 쪽에서도 구할 수
/// 있다 — 즉 필드 추가 없이 구조적으로 재현 가능하다(실제 미러 실행 코드는 이번 단계 범위 밖).
/// </summary>
public static class BattleSnapshotCodec
{
    public const int CURRENT_VERSION = 1;

    // UnitEntry의 int 필드 개수(순서 고정): snapshotUnitIndex, speciesId, starLevel, q, r,
    // itemId0, itemId1, equippedStoneId, previousSpeciesId, isTradeEvolved, evolutionLocked,
    // hasHeroBerry, skillManaCost.
    private const int UNIT_INT_FIELDS = 13;

    // UnitEntry의 string 필드 개수(순서 고정): skillId, roleOverride, attackVfxIdOverride.
    private const int UNIT_STRING_FIELDS = 3;

    // ─────────────────────────────────────────
    // 생성
    // ─────────────────────────────────────────

    /// <summary>
    /// 현재 로컬 상태에서 BattleSnapshot을 만든다. GameManager.Instance를 직접 호출하지 않고
    /// 필요한 원본을 매개변수로 받는 factory다 — 호출부는 아직 없다(이번 단계는 정의만).
    /// </summary>
    /// <param name="board">필드 유닛 조회 대상(널이면 유닛 없이 생성).</param>
    /// <param name="stage">현재 스테이지(널이면 stageId="").</param>
    /// <param name="roundIndex">현재 라운드.</param>
    /// <param name="playerActorNumber">Photon actor number. 순수 데이터 클래스에 Photon 타입 의존을
    /// 넣지 않기 위해 호출측(PhotonNetwork.LocalPlayer.ActorNumber 등)에서 뽑아 인자로 넘긴다.</param>
    /// <param name="cheerleaderChoice">CheerleaderChoice.Current(client-local static)을 이 메서드가
    /// 직접 읽지 않는다 — 미러 전투가 이 값을 읽으면 파트너가 아니라 이 클라이언트 자신의 선택값이
    /// 적용되는 확정적 desync 위험이 있어(사전 분석에서 확인됨), 호출측이 원하는 시점의 값을
    /// 뽑아 인자로 넘기도록 강제한다.</param>
    /// <param name="activeSynergies">SynergyManager.GetActiveSynergies() 결과를 그대로 넘기면 된다.</param>
    public static BattleSnapshot Create(
        BoardManager board,
        StageData stage,
        int roundIndex,
        int playerActorNumber,
        CheerleaderChoice.Option cheerleaderChoice,
        IReadOnlyList<SynergyStatus> activeSynergies)
    {
        var snap = new BattleSnapshot
        {
            snapshotVersion = CURRENT_VERSION,
            playerActorNumber = playerActorNumber,
            roundIndex = roundIndex,
            stageId = stage != null ? stage.stageId : "",
            cheerleaderChoice = cheerleaderChoice
        };

        if (activeSynergies != null)
        {
            foreach (SynergyStatus status in activeSynergies)
            {
                if (status == null || status.data == null || !status.IsActive) continue;
                snap.activeSynergies.Add(new BattleSnapshot.SynergyEntry
                {
                    synergyId = status.data.id,
                    tier = status.activeTierIndex
                });
            }
        }

        if (board != null)
        {
            // 전투 결정론 정렬(1단계, BattleManager.SetupAllyUnits)과 동일한 기준(q,r)을 여기서도
            // 그대로 쓴다. BattleManager.CompareCoords는 private라 재사용할 수 없어 같은 2줄 비교를
            // 이 파일에도 둔다(BattleManager.cs는 이번 단계에서 수정하지 않는다).
            var entries = new List<KeyValuePair<HexCoords, PokemonUnit>>(board.GetBoardSnapshot());
            entries.Sort((a, b) => CompareCoords(a.Key, b.Key));

            int index = 0;
            foreach (KeyValuePair<HexCoords, PokemonUnit> kv in entries)
            {
                PokemonUnit unit = kv.Value;
                if (unit == null || unit.data == null) continue; // 빈 타일은 유닛 목록에 넣지 않음

                snap.units.Add(BuildUnitEntry(unit, kv.Key, index));
                index++;
            }
        }

        return snap;
    }

    private static BattleSnapshot.UnitEntry BuildUnitEntry(PokemonUnit unit, HexCoords coords, int index)
    {
        int itemId0 = 0;
        int itemId1 = 0;
        if (unit.items != null)
        {
            if (unit.items.Count > 0 && unit.items[0] != null) itemId0 = unit.items[0].id;
            if (unit.items.Count > 1 && unit.items[1] != null) itemId1 = unit.items[1].id;
        }

        // BattleManager.CreateBattleUnit(880)과 동일 기준 — 주입 스킬(grantedSkill) 우선, 없으면 종 스킬.
        PokemonSkillData effectiveSkill = unit.EffectiveSkill;

        return new BattleSnapshot.UnitEntry
        {
            snapshotUnitIndex = index,
            speciesId = unit.data.id,
            starLevel = unit.starLevel,
            q = coords.q,
            r = coords.r,
            itemId0 = itemId0,
            itemId1 = itemId1,
            equippedStoneId = unit.equippedStone != null ? unit.equippedStone.id : 0,
            previousSpeciesId = unit.preStoneData != null ? unit.preStoneData.id : 0,
            isTradeEvolved = unit.isTradeEvolved,
            evolutionLocked = unit.evolutionLocked,
            hasHeroBerry = unit.hasHeroBerry,
            heroStatMultiplier = unit.heroStatMultiplier,
            roleOverride = unit.roleOverride ?? "",
            skillId = effectiveSkill != null && effectiveSkill.HasSkill ? effectiveSkill.skillId : "",
            skillManaCost = unit.EffectiveManaCost,
            attackVfxIdOverride = unit.attackVfxIdOverride ?? ""
        };
    }

    /// <summary>전투 결정론 정렬 기준(1단계 작업과 동일) — q 오름차순, 같으면 r 오름차순.</summary>
    private static int CompareCoords(HexCoords a, HexCoords b)
    {
        int qCompare = a.q.CompareTo(b.q);
        return qCompare != 0 ? qCompare : a.r.CompareTo(b.r);
    }

    // ─────────────────────────────────────────
    // Encode
    // ─────────────────────────────────────────

    /// <summary>Encode() 결과. 나중에 Photon RPC 인자 3개(int[], float[], string[])로 그대로 넘길 수 있는 형태.</summary>
    public readonly struct EncodedPayload
    {
        public readonly int[] ints;
        public readonly float[] floats;
        public readonly string[] strings;

        public EncodedPayload(int[] ints, float[] floats, string[] strings)
        {
            this.ints = ints;
            this.floats = floats;
            this.strings = strings;
        }
    }

    /// <summary>
    /// ints 레이아웃: [snapshotVersion, playerActorNumber, roundIndex, cheerleaderChoice,
    ///   synergyCount, (synergyId,tier)×synergyCount, unitCount, (유닛 13필드)×unitCount]
    /// floats 레이아웃: [heroStatMultiplier × unitCount]
    /// strings 레이아웃: [stageId, (skillId,roleOverride,attackVfxIdOverride)×unitCount]
    /// </summary>
    public static EncodedPayload Encode(BattleSnapshot snapshot)
    {
        if (snapshot == null)
            return new EncodedPayload(new[] { 0 }, System.Array.Empty<float>(), new[] { "" });

        int synergyCount = snapshot.activeSynergies.Count;
        int unitCount = snapshot.units.Count;

        var ints = new int[5 + synergyCount * 2 + 1 + unitCount * UNIT_INT_FIELDS];
        var floats = new float[unitCount];
        var strings = new string[1 + unitCount * UNIT_STRING_FIELDS];

        ints[0] = snapshot.snapshotVersion;
        ints[1] = snapshot.playerActorNumber;
        ints[2] = snapshot.roundIndex;
        ints[3] = (int)snapshot.cheerleaderChoice;
        ints[4] = synergyCount;

        int ii = 5;
        foreach (BattleSnapshot.SynergyEntry s in snapshot.activeSynergies)
        {
            ints[ii++] = s.synergyId;
            ints[ii++] = s.tier;
        }

        ints[ii++] = unitCount;

        strings[0] = snapshot.stageId ?? "";
        int si = 1;
        int fi = 0;

        for (int u = 0; u < unitCount; u++)
        {
            BattleSnapshot.UnitEntry e = snapshot.units[u];

            ints[ii++] = e.snapshotUnitIndex;
            ints[ii++] = e.speciesId;
            ints[ii++] = e.starLevel;
            ints[ii++] = e.q;
            ints[ii++] = e.r;
            ints[ii++] = e.itemId0;
            ints[ii++] = e.itemId1;
            ints[ii++] = e.equippedStoneId;
            ints[ii++] = e.previousSpeciesId;
            ints[ii++] = e.isTradeEvolved ? 1 : 0;
            ints[ii++] = e.evolutionLocked ? 1 : 0;
            ints[ii++] = e.hasHeroBerry ? 1 : 0;
            ints[ii++] = e.skillManaCost;

            floats[fi++] = e.heroStatMultiplier;

            strings[si++] = e.skillId ?? "";
            strings[si++] = e.roleOverride ?? "";
            strings[si++] = e.attackVfxIdOverride ?? "";
        }

        return new EncodedPayload(ints, floats, strings);
    }

    // ─────────────────────────────────────────
    // TryDecode — 예외 대신 실패(false)를 반환한다.
    // ─────────────────────────────────────────

    /// <summary>
    /// 잘못된 입력(null/빈 배열/버전 불일치/음수 count/길이 초과/잘린 데이터/중복 인덱스·좌표/
    /// 잘못된 starLevel·음수 ID/알 수 없는 enum 값)에서 예외를 던지지 않고 false + error 메시지로 실패를 알린다.
    /// </summary>
    public static bool TryDecode(int[] ints, float[] floats, string[] strings, out BattleSnapshot snapshot, out string error)
    {
        snapshot = null;
        error = null;

        if (ints == null || floats == null || strings == null) { error = "null 데이터"; return false; }
        // 최소 헤더 길이: [version, actor, round, cheer, synergyCount] + synergyCount==0일 때의 unitCount.
        if (ints.Length < 6) { error = "ints 데이터가 헤더보다 짧음"; return false; }
        if (strings.Length < 1) { error = "strings 데이터에 stageId 없음"; return false; }

        int version = ints[0];
        if (version != CURRENT_VERSION) { error = $"지원하지 않는 snapshotVersion: {version}"; return false; }

        int synergyCount = ints[4];
        if (synergyCount < 0) { error = "activeSynergies count가 음수"; return false; }

        int synergyIntEnd = 5 + synergyCount * 2;
        if (synergyIntEnd + 1 > ints.Length) { error = "activeSynergies 데이터가 잘림"; return false; }

        int unitCount = ints[synergyIntEnd];
        if (unitCount < 0) { error = "unit count가 음수"; return false; }

        int requiredInts = synergyIntEnd + 1 + unitCount * UNIT_INT_FIELDS;
        if (requiredInts > ints.Length) { error = "unit ints 데이터가 잘림"; return false; }

        if (unitCount > floats.Length) { error = "unit floats 데이터가 잘림"; return false; }

        int requiredStrings = 1 + unitCount * UNIT_STRING_FIELDS;
        if (requiredStrings > strings.Length) { error = "unit strings 데이터가 잘림"; return false; }

        int cheerRaw = ints[3];
        if (cheerRaw != (int)CheerleaderChoice.Option.AttackSpeed && cheerRaw != (int)CheerleaderChoice.Option.ManaRegen)
        { error = $"알 수 없는 cheerleaderChoice 값: {cheerRaw}"; return false; }

        var result = new BattleSnapshot
        {
            snapshotVersion = version,
            playerActorNumber = ints[1],
            roundIndex = ints[2],
            cheerleaderChoice = (CheerleaderChoice.Option)cheerRaw,
            stageId = strings[0] ?? ""
        };

        int ii = 5;
        for (int i = 0; i < synergyCount; i++)
        {
            result.activeSynergies.Add(new BattleSnapshot.SynergyEntry
            {
                synergyId = ints[ii++],
                tier = ints[ii++]
            });
        }
        ii++; // unitCount 필드(이미 읽음) 건너뛰기

        var seenIndex = new HashSet<int>();
        var seenCoords = new HashSet<(int, int)>();

        int si = 1;
        int fi = 0;

        for (int u = 0; u < unitCount; u++)
        {
            int snapshotUnitIndex = ints[ii++];
            int speciesId = ints[ii++];
            int starLevel = ints[ii++];
            int q = ints[ii++];
            int r = ints[ii++];
            int itemId0 = ints[ii++];
            int itemId1 = ints[ii++];
            int equippedStoneId = ints[ii++];
            int previousSpeciesId = ints[ii++];
            bool isTradeEvolved = ints[ii++] != 0;
            bool evolutionLocked = ints[ii++] != 0;
            bool hasHeroBerry = ints[ii++] != 0;
            int skillManaCost = ints[ii++];

            float heroStatMultiplier = floats[fi++];

            string skillId = strings[si++] ?? "";
            string roleOverride = strings[si++] ?? "";
            string attackVfxIdOverride = strings[si++] ?? "";

            if (!seenIndex.Add(snapshotUnitIndex)) { error = $"중복 snapshotUnitIndex: {snapshotUnitIndex}"; return false; }
            if (!seenCoords.Add((q, r))) { error = $"중복 좌표: ({q},{r})"; return false; }
            if (speciesId <= 0) { error = $"잘못된 speciesId: {speciesId}"; return false; }
            if (starLevel < 1 || starLevel > 3) { error = $"잘못된 starLevel: {starLevel}"; return false; }
            if (itemId0 < 0 || itemId1 < 0 || equippedStoneId < 0 || previousSpeciesId < 0 || skillManaCost < 0)
            { error = "음수 ID/마나비용"; return false; }

            result.units.Add(new BattleSnapshot.UnitEntry
            {
                snapshotUnitIndex = snapshotUnitIndex,
                speciesId = speciesId,
                starLevel = starLevel,
                q = q,
                r = r,
                itemId0 = itemId0,
                itemId1 = itemId1,
                equippedStoneId = equippedStoneId,
                previousSpeciesId = previousSpeciesId,
                isTradeEvolved = isTradeEvolved,
                evolutionLocked = evolutionLocked,
                hasHeroBerry = hasHeroBerry,
                heroStatMultiplier = heroStatMultiplier,
                roleOverride = roleOverride,
                skillId = skillId,
                skillManaCost = skillManaCost,
                attackVfxIdOverride = attackVfxIdOverride
            });
        }

        snapshot = result;
        return true;
    }

    // ─────────────────────────────────────────
    // round-trip 검증
    // ─────────────────────────────────────────

    /// <summary>Encode→TryDecode 후 원본과 값이 같은지 확인하는 네트워크 연결 전 단위 검증용 헬퍼.</summary>
    public static bool VerifyRoundTrip(BattleSnapshot original, out string error)
    {
        error = null;
        if (original == null) { error = "원본 스냅샷이 null"; return false; }

        EncodedPayload payload = Encode(original);

        if (!TryDecode(payload.ints, payload.floats, payload.strings, out BattleSnapshot decoded, out string decodeError))
        {
            error = $"Decode 실패: {decodeError}";
            return false;
        }

        if (!original.EquivalentTo(decoded))
        {
            error = "Encode→Decode 결과가 원본과 다름";
            return false;
        }

        return true;
    }
}
