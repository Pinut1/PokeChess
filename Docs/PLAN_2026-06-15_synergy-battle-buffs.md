# 시너지 전투 버프 적용 (2026-06-15)

> ⚠️ **보류 (2026-06-15)**: 시너지 밸런스(어떤 시너지가 무슨 효과 몇 %인지)가 게임 디자인상
> 아직 확정되지 않아 구현을 보류함. 아래 구조/단계 설계는 그대로 유효하니, **디자인이 확정되면**
> 이 문서대로 재구현하면 됨. (한 번 구현했다가 가짜 수치 베이킹을 막기 위해 코드/JSON 전부 원복함.)

## Context
BattleManager 자동 전투는 완성됐으나, 시너지가 전투에 **아무 효과가 없음**.
`SynergyTier`에 `count` + `effectDescription`(설명 문자열)만 있어 구조화된 효과 데이터가 없기 때문.
`BattleManager.RunBattle()`의 `// TODO: ... 활성 시너지 버프 적용` 주석(메모리에도 동일 TODO)이 그 자리.
이번 작업은 그 TODO를 실제 스탯 버프 적용으로 대체한다.

## 설계 결정
- **데이터 구조화는 아이템 패턴을 그대로 따른다.** 아이템은 JSON `statKey/statValue` →
  `ApplyItemStat(ItemData, key, value)` switch로 매핑. 시너지도 동일하게
  `SynergyTier`에 `effectType`(enum) + `effectValue`(float)를 추가하고, 임포터가 JSON
  문자열 키를 enum으로 파싱. 설명 문자열(`effectDescription`)은 UI용으로 유지.
- **effectValue 의미**: 퍼센트형은 0.2 = +20%, 플랫형은 절대값 가산.
- **enum (v1 최소 집합, 추후 확장)**:
  `None / AttackPercent / SpecialAttackPercent / MaxHpPercent / AttackSpeedPercent /
   DefenseFlat / SpecialDefenseFlat`
- **적용 대상**: 미러 매치이므로 활성 시너지(=내 보드 기준)를 **양 팀 전부**에 동일 적용.
  `GetActiveSynergies()`는 내 보드만 반영하지만 적은 내 보드의 거울 복제라 구성이 동일.
  추후 실 PvP 적 보드 동기화 도입 시, 적 팀은 적 스냅샷 기준 시너지로 교체(주석으로 명시).
- **적용 시점**: `SetupUnits()` 직후, tick 루프 진입 전 1회. 스냅샷 스탯(`BattleUnit`)에만
  적용하므로 원본 `PokemonUnit`은 불변(기존 계약 유지).
- **MaxHpPercent**: 스냅샷 시 `currentHp = maxHp`이므로 maxHp와 currentHp 둘 다 가산.

## 단계
1. **Docs/PLAN_2026-06-15_synergy-battle-buffs.md** 저장 (작업 컨벤션).
2. `Data/SynergyData.cs`: `SynergyEffectType` enum 추가 + `SynergyTier`에 `effectType`, `effectValue` 필드.
3. `Editor/PokeChessImporter.cs`: `SynergyTierEntry`에 `effectType`(string), `effectValue`(float)
   추가 + `ParseSynergyEffect` 헬퍼로 매핑.
4. `Managers/BattleManager.cs`: `RunBattle()`의 TODO 주석 → `ApplySynergyBuffs()` 호출로 대체.
   `ApplySynergyBuffs` / `ApplyEffectToUnit` 헬퍼 추가, 적용 내역 로그.
5. `Debug/BattleDebugTest.cs`: (선택) 활성 시너지 버프를 OnGUI에 표시해 검증 용이화.

## 검증 (Play 모드, 솔로모드)
1. 같은 시너지 유닛 N마리 배치 → 시너지 티어 활성 → 전투 시작 → 로그에 버프 적용 내역 출력 확인.
2. 버프 적용 전/후 유닛 HP·공격력 차이로 효과 체감(전투 길이/결과 변화).
3. 시너지 비활성(유닛 부족) 시 버프 0 — 회귀 확인.

## 수정/생성 파일
| 파일 | 작업 |
|---|---|
| `Docs/PLAN_2026-06-15_synergy-battle-buffs.md` | 신규 (플랜 보존) |
| `Assets/_Project/Scripts/Data/SynergyData.cs` | enum + 필드 추가 |
| `Assets/_Project/Scripts/Editor/PokeChessImporter.cs` | 효과 파싱 추가 |
| `Assets/_Project/Scripts/Managers/BattleManager.cs` | TODO → 버프 적용 구현 |
| `Assets/_Project/Scripts/Debug/BattleDebugTest.cs` | (선택) 활성 시너지 표시 |
