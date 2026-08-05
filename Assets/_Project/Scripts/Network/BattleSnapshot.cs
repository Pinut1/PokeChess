using System.Collections.Generic;

/// <summary>
/// 전투 시작 직전 로컬 상태를 네트워크로 전달 가능한 순수 데이터로 옮긴 스냅샷(파트너 관전 미러링용).
///
/// BoardSnapshot(준비 단계 파트너 보드 표시용 — 위치+종+성급만)과는 목적이 다르다.
/// 이건 미러 전투를 "동일 시뮬레이션으로 재현"하기 위한 전투 재현용 스냅샷이라 필드가 훨씬 많다.
/// BoardSnapshot은 이 작업에서 건드리지 않는다.
///
/// 벤치 유닛은 포함하지 않는다 — 실제 전투(BattleManager.SetupAllyUnits)도 필드 유닛만 참여시킨다.
/// 돌연변이 봇은 PokemonUnit 원본이 없는 전투 중 생성물이라 이 유닛 목록에 포함하지 않는다
/// (BattleSnapshotCodec.cs 상단 주석에 미러 쪽 재현 가능성 근거를 남겨둠).
///
/// Unity 오브젝트 참조(ScriptableObject 등)는 담지 않는다 — 전부 id/문자열 키로만 저장한다.
/// 이 클래스는 MonoBehaviour가 아닌 순수 데이터 홀더라 [SerializeField]를 쓰지 않는다.
/// </summary>
public class BattleSnapshot
{
    /// <summary>유닛 1마리의 전투 재현에 필요한 상태. 필드는 BoardSnapshot.Entry와 같은 방식(공개 camelCase)으로 노출한다.</summary>
    public struct UnitEntry
    {
        public int snapshotUnitIndex;      // 정렬된 배열 내 위치(0부터 연속). 중복/순서 검증에도 쓰인다.
        public int speciesId;              // PokemonData.id
        public int starLevel;
        public int q;
        public int r;
        public int itemId0;                // 0 = 슬롯0 미장착. PokemonUnit.MaxItemSlots(2)에 맞춘 고정 슬롯.
        public int itemId1;                // 0 = 슬롯1 미장착.
        public int equippedStoneId;        // 0 = 없음
        public int previousSpeciesId;      // 0 = 없음(돌 장착 전 원본 종, preStoneData)
        public bool isTradeEvolved;
        public bool evolutionLocked;       // 이브이 영웅증강
        public bool hasHeroBerry;          // 파치리스 영웅증강 v2 자뭉열매
        public float heroStatMultiplier;
        public string roleOverride;        // "" = 오버라이드 없음
        public string skillId;             // "" = 스킬 없음(평타만). PokemonUnit.EffectiveSkill 기준(주입 스킬 우선).
        public int skillManaCost;          // PokemonUnit.EffectiveManaCost 기준(실제 전투 생성 경로와 동일 값).
        public string attackVfxIdOverride; // "" = 오버라이드 없음
    }

    /// <summary>활성 시너지 1개(비활성은 담지 않음 — BattleManager.ApplySynergyBuffs가 쓰는 것과 동일 기준).</summary>
    public struct SynergyEntry
    {
        public int synergyId; // SynergyData.id
        public int tier;      // SynergyStatus.activeTierIndex
    }

    public int snapshotVersion;
    public int playerActorNumber;
    public int roundIndex;
    public string stageId;
    public CheerleaderChoice.Option cheerleaderChoice;

    public readonly List<SynergyEntry> activeSynergies = new();
    public readonly List<UnitEntry> units = new();

    /// <summary>Unity 오브젝트 참조 비교가 아니라 값 필드만 비교하는 구조적 동치 검사(round-trip 검증용).</summary>
    public bool EquivalentTo(BattleSnapshot other)
    {
        if (other == null) return false;

        if (snapshotVersion != other.snapshotVersion) return false;
        if (playerActorNumber != other.playerActorNumber) return false;
        if (roundIndex != other.roundIndex) return false;
        if (stageId != other.stageId) return false;
        if (cheerleaderChoice != other.cheerleaderChoice) return false;

        if (activeSynergies.Count != other.activeSynergies.Count) return false;
        for (int i = 0; i < activeSynergies.Count; i++)
            if (!activeSynergies[i].Equals(other.activeSynergies[i])) return false;

        if (units.Count != other.units.Count) return false;
        for (int i = 0; i < units.Count; i++)
            if (!units[i].Equals(other.units[i])) return false;

        return true;
    }

    /// <summary>디버그/로그용 1줄 요약. 프로덕션 경로에서 자동 호출되지 않는다(호출측 책임).</summary>
    public string ToDebugString()
    {
        return $"BattleSnapshot v{snapshotVersion} actor={playerActorNumber} round={roundIndex} " +
               $"stage={stageId} cheer={cheerleaderChoice} synergies={activeSynergies.Count} units={units.Count}";
    }
}
