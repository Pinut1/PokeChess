# 시너지 효과 스키마 — 황해인 핸드오프 (2026-06-25)

> 작성: 영욱(팀장). 기획 `synergy_table.csv` 도착 → SynergyData 효과 구조를 확정하기 위한 매핑 명세.
> **이 문서는 [PLAN_2026-06-15_synergy-battle-buffs.md](PLAN_2026-06-15_synergy-battle-buffs.md)를 갱신/대체한다** (아래 "구 플랜에서 바뀐 점" 참조).

## 현재 상태 (코드 기준)
- ✅ **카운트/티어 활성화**: `SynergyManager`가 보드 pull → 고유종 카운트 → 활성 티어 판정 → `SynergyUpdated` 발행, `GetActiveSynergies()` API 완비. **여긴 손댈 것 없음.**
- ❌ **효과 데이터 구조**: `SynergyData`/`SynergyTier`에 `count` + `effectDescription`(설명 문자열)뿐. 기계가 읽을 효과 타입/수치 없음. ← **이 문서가 채울 부분 (황해인)**
- ❌ **전투 적용**: BattleManager가 시너지를 안 읽음. ← seam은 영욱(BattleManager 레인)

## ✅ 수치 확정됨 (2026-06-25 — SynergyTable_Guide.md §6)
기획이 티어별 수치 전부 + 아키텍처를 확정: **수치는 데이터(JSON)가 아니라 코드 상수로 관리**
(`statType`/발동단계만 데이터, 증가량은 코드). → `Assets/_Project/Scripts/Data/SynergyConstants.cs`에
13종 전 티어 값 입력 완료. 더 이상 "베이킹 금지" 대상 아님(기획이 코드 상수 방식 명시).

## 제안 스키마

CSV를 보면 `statType`과 "어떤 스탯"은 **시너지 단위**로 고정이고, 티어마다 변하는 건 *수치*뿐이다.
그래서 효과 타입은 시너지 레벨, 수치는 티어 레벨에 둔다(구 플랜은 티어마다 effectType을 뒀는데 불필요).

```csharp
// SynergyData.cs
public enum SynergyCalc { Fixed, Percent, Special }   // CSV statType
public enum SynergyStat {                              // 어떤 스탯을 건드리는지
    None, Attack, Defense, SpellPower, AtkSpeed, MaxHp, CritChance, ManaRegen, Shield
}
public enum SynergySpecial {                           // calc==Special일 때만 의미
    None, SpawnBots, EnemyAtkSpeedDebuff, StunOnFirstSkill, PlayerChoiceBuff
}

public class SynergyData : ScriptableObject {
    // ... 기존 id/name/icon/tiers 유지 ...
    public SynergyCalc    calc;        // 신규
    public SynergyStat    stat;        // 신규 (Special이면 None)
    public SynergySpecial special;     // 신규 (calc==Special일 때만)
}

public class SynergyTier {
    public int    count;               // 기존 유지
    public float  value;               // 신규 — 티어별 매그니튜드 (★기획 수치 대기)
    public string effectDescription;   // 기존 유지 (UI 표시용)
}
```
`value` 의미: `Percent`는 0.2 = +20%, `Fixed`는 절대값 가산.

## CSV → enum 매핑 (일반 12개 — 제네릭 스탯버프)

| synergyId | stat | calc | 비고 |
|---|---|---|---|
| GRASS 풀 | AtkSpeed | Fixed | |
| POISON 독 | Defense | Fixed | |
| WATER 물 | Shield | Percent | 전투 시작 시 spellPower 기반 보호막 → `bu.shield`(기존 필드) 재사용 |
| FIRE 불꽃 | Defense | Percent | |
| FLYING 비행 | AtkSpeed | Percent | |
| NORMAL 노말 | CritChance | Percent | ✅ BattleUnit.critChance 기존 존재(아이템 criPct용) → 재사용. CritFactor가 데미지에 반영 |
| ETHEREAL 정령 | ManaRegen | Percent | ⚠️ 마나 충전 *속도* 필드 부재(GainMana 고정량). `manaGainMultiplier` 신규 필드 필요 |
| GROUND 대지 | MaxHp | Percent | maxHp·currentHp 둘 다 가산 |
| ELECTRIC 전기 | SpellPower | Fixed | |
| BUG 벌레 | Attack | Fixed | |
| BREAKER 파괴 | Attack | Percent | |
| DRAGON 드래곤 | SpellPower | Percent | |

## CSV → 특수 4개 (제네릭 엔진 밖 — 각자 전용 로직)

| synergyId | special | 구현 메모 |
|---|---|---|
| MUTANT 돌연변이 | SpawnBots | 단계마다 봇 유닛 추가(에브이→브래키→글레이시아→님피아). 보드/전투에 유닛 생성 — 별도 설계 필요 |
| ICE 얼음 | EnemyAtkSpeedDebuff | `적 atkSpeed -20%`. ⚠️ **아군 버프가 아니라 적 디버프** — 현재 SynergyManager는 아군만 카운트. 적용은 BattleManager 훅(영욱) |
| DARK 악 | StunOnFirstSkill | 첫 스킬 시 STUN 1.5초 → **기둥C `BattleUnit.ApplyStun()` 재사용 가능**(가장 쉬움) |
| CHEERLEADER 치어리더 | PlayerChoiceBuff | +15% atkSpeed **or** +30% manaRegen 선택 → 증강(AugmentManager) 류 선택 UI 필요 |

## 구 플랜에서 바뀐 점 (PLAN_2026-06-15)
1. **특공/특방 폐지** — 구 enum의 `SpecialAttackPercent`/`SpecialDefenseFlat`는 v9에서 삭제됨. `SpellPower`로 대체.
2. **미러매치 가정 폐기** — 구 플랜은 "내 보드 시너지를 양 팀에 적용". 이제 PVE라 적은 StageData로 따로 구성되고 시너지 데이터가 없음 → **시너지 버프는 아군에게만 적용**. (ICE만 예외적으로 적을 건드림)
3. **effectType은 티어가 아니라 시너지 레벨** — CSV가 그렇게 정규화돼 있음.

## 구현 현황 (2026-06-25)
- ✅ **영욱(완료)**: `SynergyConstants`(§6 수치) + BattleManager `ApplySynergyBuffs`/`ApplySynergyBuff`(RunBattle 진입 전, 아군 트레잇 보유 유닛에 적용). crit=기존 `critChance` 재사용. ETHEREAL용 `manaGainMultiplier` 필드 신설(GainMana 반영). 일반 11종(grass/poison/water/fire/flying/normal/ethereal/ground/electric/bug/breaker/dragon) 동작.
- ✅ **중앙 DB화(영욱)**: `SynergyDatabase`(SO) 신설 — PokemonDatabase/ItemDatabase와 동일 패턴. 임포터가 `UpdateSynergyDatabase`로 자동 갱신, SynergyManager가 `SynergyDatabase.Instance.all`에서 자동 로드 → **인스펙터 수동배선 불필요**. (임포터 DTO `SynergyDatabase`→`SynergyJsonDb` 리네임으로 이름충돌 해소)
- ⏳ **수동작업(Unity)**: `PokeChess/Import Synergy JSON` 재실행 1회만 → `Resources/SynergyDatabase.asset` 생성/갱신되며 13종 자동 등록. (synergy_data.json은 16종으로 갱신됨)
- ✅ **특수 3종 구현(영욱)**: `BattleManager.ApplySynergySpecials`(RunBattle 내, 스탯버프 직후) — **MUTANT**(빈 아군타일에 봇 누적소환 Eevee→Umbreon→Glaceon→Sylveon, 전투전용 source=null이라 카운트·복원 제외) / **ICE**(적 전체 atkSpeed -20%) / **CHEERLEADER**(`CheerleaderChoice` 전역상태 + SynergyHud 토글 → 아군 전체 공속/마나 선택적용). 보유 유닛 있는 MUTANT만 즉시 테스트 가능, ICE/CHEERLEADER는 코드 준비(배정 유닛 0).
- ⏳ **DARK 미구현**: 첫 스킬 시전 STUN 1.5초 — 시전 경로(`CastSkill`) 훅 + 1회 플래그 필요(기둥C `ApplyStun` 재사용). 다음 작업.
- ⏳ **황해인(선택)**: 추후 `SynergyData.statType` 필드 추가 시 BattleManager switch를 statType 기반으로 리팩터 가능(현재는 synergyId 직접 분기). 필수는 아님 — 현재도 동작.

[[project-part-division]] [[project-combat-model-v9]]
