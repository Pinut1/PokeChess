# 데이터 임포트 계약 — 형식 결정 (2026-06-23)

데이터 받기 전 확정. 데이터팀(JSON 제공) · 태욱(임포터) · 영욱(구조/런타임) 공통 계약.
관련: `docs/trainer-entry-pipeline.md`, 메인기획서 3.2 Stage Table.

## #1 Skill Table — skillId 마스터 + import-time join (인라인 베이킹)
- `skill_table.json`: skillId → { skillName, description, damage, manaCost, targetType(single/area/line/all), areaRadius, lineLength }. **스킬의 단일 출처.**
- `pokemon_data.json`: 각 포켓몬은 `skillId`로 참조.
- 임포터: skillId로 skill_table 조회 → `PokemonData.skill`(PokemonSkillData)에 베이킹.
  - 폴백(전환기): pokemon에 인라인 skill이 있으면 그대로 사용.
- **런타임 무변경**: BattleManager는 지금처럼 인라인 `PokemonData.skill`을 읽음.
- 사유: 기획=정규화 마스터시트(재사용/밸런스 일괄), 기술=런타임 변경 0. Trainer Entry와 동일 패턴.

## #2 Reward ID — 문자열 키 통일 ✅ 코드측 완료(2026-06-23, 영욱)
- `rewardTableId`(int) → **string** 마이그레이션 완료. GDD 표기 `RW001` 그대로 키로 사용.
- 변경 완료: `RewardData.rewardTableId`(string), `StageData.rewardTableId`(string), `RewardDatabase.GetByTableId(string)`(OrdinalIgnoreCase), 임포터 DTO(StageEntry/RewardTableJson) string화 + 베이킹, `reward_data.json`(RW001/RW002), `stage_data.json`(야생=RW001/관장·챔피언=RW002). `RewardManager`는 string 호환이라 무수정.
- ⚠️ **재import 필수**: 필드 타입이 int→string이라 기존 `RewardDatabase.asset`/`StageDatabase.asset`의 rewardTableId가 빈값이 됨 → **Import Reward JSON + Import Stage JSON 1회 재실행**해야 RW001/RW002 반영.
- 사유: trainerId도 문자열 → 일관. "RW 떼고 int" 파싱은 앞자리 0/비숫자에서 취약.

## #3 Event ID → PreStageReward (기획서 매핑 준수)
- `NONE → None`, `AUGMENT → AugmentChoice`, `COMPANION → CompanionItem`, (`ITEM → ItemReward` 추가 시).
- 임포터가 Stage Table의 Event ID 문자열을 파싱.

## #4 좌표 — q/r 기본, slot은 야생 전용
- 관장/챔피언: enemies에 `q`/`r` 직접.
- 야생 라운드: `slot`(1~6)로 줄 수 있음 → 임포터가 `StageLayout.SlotToHex(slot)`로 변환.

## #5 trainerId — 대문자 영문이름만 (접두사 없음)
- 예: `GREEN`. `GYM_`/`CHAMP_` 접두사·배틀타입 인코딩 안 함.
- 야생은 트레이너 없음(trainerId 비움). 현재 placeholder의 `GYM_*` ID는 데이터팀 실명 도착 시 이 규칙으로 교체.

## #6 pokemonNameEn — 오타 없는 JSON 제공
- 적/스킬/스테이지가 참조하는 영문명은 전부 PokemonDatabase에 존재해야 함(새 종은 같은 PR로 pokemon_data.json 포함).
- 대소문자는 IgnoreCase 매칭(영욱 처리 완료).

## 순서 (의존)
형식 확정(#1~#6) → 태욱 임포터(trainer/skill join + reward string + event 매핑) → 데이터 도착 → Import. 
join 임포터/리워드 string 마이그레이션은 **데이터 도착 전** 선행 가능(형식만 락되면). placeholder pokemon 스탯 교체만 데이터 도착 후.
