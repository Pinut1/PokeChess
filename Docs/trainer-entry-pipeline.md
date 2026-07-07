# Trainer Entry 파이프라인 — Stage/Trainer Entry join 임포터 핸드오프

작성: 김영욱(Core, 데이터 구조 확정) · 구현 담당: 김태욱(PokeChessImporter join)
근거: 메인 기획서 3.2 Stage Table — `stage`가 `trainerId`로 적 구성을 **관계형 참조**.

## 확정된 구조 (영욱)

적 구성을 stage에 인라인하던 방식 → **정규화 + import-time join**으로 전환.

- **Source(정규화)**
  - `Assets/Resources/Data/stage_data.json` — 스테이지마다 `trainerId` 보유(적 구성의 단일 출처 아님).
  - `Assets/Resources/Data/trainer_entry_data.json` — `trainerId → enemies[]`. **적 구성의 단일 출처.** 여러 스테이지가 같은 trainer를 재사용 가능.
- **Join(임포터)**: Import Stage JSON 시, 각 stage의 `trainerId`로 trainer_entry를 조회해 `StageData.enemies`에 **베이킹(비정규화)**.
- **Runtime**: 변경 없음. `BattleManager`는 지금처럼 `StageData.enemies`만 읽음.

`StageData.cs`에 `public string trainerId;` 필드 추가 완료(영욱).

## 전환기 주의 (중요)

지금 `stage_data.json`에는 **인라인 `enemies`가 그대로 남아 있음** — 현행 임포터가 깨지지 않도록 한 폴백.
join 임포터가 들어오기 전까지 Import Stage JSON은 인라인 enemies를 그대로 쓰면 됨.
**join 구현 완료 후** stage_data.json의 인라인 `enemies`는 제거(또는 무시)하고 trainer_entry를 단일 출처로 삼을 것.

## 태욱 구현 항목 (PokeChessImporter.cs)

1. DTO 추가
   ```csharp
   [Serializable] private class TrainerEntryJsonDb { public List<TrainerEntryJson> trainers; }
   [Serializable] private class TrainerEntryJson  { public string trainerId; public string trainerName;
                                                    public List<EnemyPlacementJson> enemies; }
   ```
2. `StageEntry`에 `public string trainerId;` 추가(현재 없음 → JSON의 trainerId를 읽기 위함).
3. `ImportStages()` 수정:
   - `Resources.Load<TextAsset>("Data/trainer_entry")` 로 trainer_entry 로드 → `trainerId → enemies` 딕셔너리 구성.
   - 각 stage 변환 시: `e.trainerId` 가 비어있지 않고 딕셔너리에 있으면 그 enemies로 `StageData.enemies` 채움.
     - 폴백: trainerId 없음/미발견이면 기존처럼 `e.enemies`(인라인) 사용 + 경고 로그.
   - `StageData.trainerId = e.trainerId` 도 함께 세팅(추적용).
   - enemies 매핑 로직(starLevel<=0→1, NormalizeMultiplier 등)은 기존 그대로 재사용.
4. (선택) trainer_entry에 정의됐는데 어떤 stage도 참조 안 하는 trainerId, 또는 stage가 참조하는데 trainer_entry에 없는 trainerId를 import 끝에 경고로 리포트.

## 데이터팀 확인 필요

- `trainerId` 명명: 1-3~1-8은 GDD 표기(GYM_TSUKUSHI/ERIKA/HAYATO/MATIS/NATSUME/KYOU) 사용. 1-9/1-10/1-11은 GDD 발췌에 없어 임시 ID(GYM_BIJUGI/GYM_IHYANG/CHAMP_GREEN) — **확정 표기로 교체 요망.**
- 적 구성/별/좌표/배수는 현재 임시값(기존 인라인 이전). 실제 밸런싱은 기획.
- preReward 정정: GDD 증강 라운드 = 1-5/1-10/2-4. 1-8을 AugmentChoice→None으로 수정함(영욱).
- 챕터2(2-x)부터는 총 21라운드 — 데이터 확장 시 같은 구조로 trainer_entry에 추가.
