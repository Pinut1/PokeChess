# 📐 Stage Data 스키마 명세서 (개발 확정본 v2)

> 작성일: 2026-06-19 / 작성: 김영욱(Core·전투/보드)
> 대상: 기획 — Stage Data(Trainer Entry / Stage Table / Reward Table) 시트 작성자
>
> 기존 기획안의 **3-시트 구조(Trainer Entry / Stage Table / Reward Table)는 그대로 유지**합니다.
> 다만 현재 구현된 전투 시스템이 **헥스 좌표 기반 위치 전투 + 보스 스탯 배수 + 행(行) 단위 보상**에 의존하므로, 그 세 가지를 입력할 수 있도록 컬럼을 보강했습니다.
> 모든 데이터는 JSON 자동 변환을 전제로 합니다. 표기 규칙을 반드시 지켜주세요.

---

## 🧩 1. Trainer Entry 시트

트레이너(관장/챔피언/야생)가 사용하는 포켓몬 구성 + **전투 위치/난이도**를 정의합니다.

| Column | 필수 | 설명 | 예시 |
|---|---|---|---|
| `trainerId` | ✅ | 지정 ID만 사용 (하단 목록) | `KANTO_GYM_BROCK` |
| `slot` | ✅ | **전투 배치 위치 번호 (1~6)**. 아래 배치도 참고. 트레이너 내 중복 금지 | `1` |
| `pokemonId` | ✅ | 포켓몬 DB 영문 ID 그대로 (하단 규칙) | `Onix` |
| `star` | ✅ | 성급 1~3 | `2` |
| `statMul` | ⬜ | **전 스탯 배수 (보스 강화용)**. 기본 `1` | `2.5` |
| `hpMul` | ⬜ | HP만 추가 배수 (탱커형). 기본 `1` | `2` |
| `atkMul` | ⬜ | 공격력만 추가 배수. 기본 `1` | `1.5` |
| `itemSet` | ⬜ | 아이템 세트 ID. 없으면 `NONE` | `TANK_1` |
| `q`, `r` | ⬜ | **(보스 전용 옵션)** 좌표 직접 지정. 비우면 slot 기본 배치 사용 | `2`, `1` |

### 🔸 `slot` 배치도 (중요 — 기존 안에서 빠졌던 부분)

`slot`은 단순 순서가 아니라 **적 보드 위 고정 위치**입니다. 개발이 아래 배치도로 slot→좌표를 매핑합니다.

```
   [4] [5] [6]   ← 뒷열 (원거리·서포터 배치 권장)
     [1] [2] [3] ← 앞열 (탱커·근접 배치 권장)
```

- 일반전/야생전은 **slot만** 채우면 됩니다 (q/r 불필요).
- 관장전·챔피언전처럼 **정확한 포메이션이 필요한 보스**는 `q`,`r`로 직접 지정 가능 (역기획서 기준).
- **한 줄 = 포켓몬 한 마리 = 위치 한 칸** 입니다.
  - 기존 안의 `count`(동일 N마리) 컬럼은 **삭제**했습니다. 같은 포켓몬을 2마리 두려면 **slot을 달리해서 2줄**로 작성해주세요. (좌표가 1칸인데 count=2면 둘째 마리 위치가 정의되지 않아서입니다.)

### 작성 예시

| trainerId | slot | pokemonId | star | statMul | hpMul | atkMul | itemSet |
|---|---|---|---|---|---|---|---|
| KANTO_GYM_BROCK | 1 | Geodude | 1 | 1 | 1 | 1 | NONE |
| KANTO_GYM_BROCK | 2 | Geodude | 1 | 1 | 1 | 1 | NONE |
| KANTO_GYM_BROCK | 4 | Onix | 2 | 1.5 | 2 | 1 | TANK_1 |
| KANTO_CHAMPION_BLUE | 1 | Charizard | 3 | 2.5 | 1 | 1.5 | AP_1 |

---

## 🎮 2. Stage Table 시트

스테이지 진행 순서를 정의합니다. (기존 안에서 거의 유지, 값 매핑만 정정)

| Column | 필수 | 설명 | 허용값 |
|---|---|---|---|
| `stage` | ✅ | 스테이지(챕터) 번호 | `1` |
| `round` | ✅ | 라운드 번호 | `1`~`11` |
| `battleType` | ✅ | 전투 타입 | `Wild` / `Gym` / `Champion` |
| `trainerId` | ✅ | 참조 트레이너 (Trainer Entry의 ID) | `KANTO_GYM_BROCK` |
| `eventId` | ✅ | 전투 전 이벤트 | `NONE` / `AUGMENT` / `ITEM` / `COMPANION_ITEM` |
| `rewardId` | ✅ | 보상 ID (Reward Table 참조) | `RW003` |

- **야생 희귀/엘리트 구분**은 별도 컬럼 없이 `trainerId`(`WILD_COMMON`/`WILD_RARE`/`WILD_ELITE`)로 표현합니다. 개발이 이걸로 스테이지 타입을 자동 분류합니다.
- `stage`+`round` 조합으로 `stageId`(예: `1-3`)를 자동 생성하므로 별도 입력 불필요.

### 작성 예시

| stage | round | battleType | trainerId | eventId | rewardId |
|---|---|---|---|---|---|
| 1 | 1 | Wild | WILD_COMMON | NONE | RW001 |
| 1 | 3 | Gym | KANTO_GYM_BROCK | NONE | RW003 |
| 1 | 5 | Gym | KANTO_GYM_LT_SURGE | AUGMENT | RW005 |
| 1 | 11 | Champion | KANTO_CHAMPION_BLUE | NONE | RW011 |

---

## 💰 3. Reward Table 시트 (구조 변경)

> 기존의 **가로 확장형(아이템마다 컬럼 추가)** 은 아이템이 늘 때마다 시트·코드가 깨져서, **행(行) 단위 구조**로 바꿨습니다.
> **보상 1개 = 1줄.** 한 rewardId에 보상이 여러 개면 여러 줄로 작성합니다.

| Column | 필수 | 설명 | 예시 |
|---|---|---|---|
| `rewardId` | ✅ | 보상 ID (여러 줄이 같은 ID 공유) | `RW005` |
| `kind` | ✅ | 보상 종류 (하단 목록) | `gold` |
| `refId` | ⬜ | 참조 대상 ID (gold/reroll/augment면 비움) | `MINOR_DITTO` |
| `amount` | ✅ | 수량 (gold면 골드량) | `6` |
| `dropChance` | ⬜ | 획득 확률 0~1. 비우면 `1`(확정) | `0.5` |

### `kind` 허용값

```
gold        — 골드            (refId 비움, amount=골드량)
reroll      — 무료 리롤 횟수   (refId 비움, amount=횟수)
item        — 아이템           (refId = 아이템 ID, 예: ANVIL / REFORGER / MINOR_DITTO / MAJOR_DITTO)
consumable  — 소모품
stone       — 진화의 돌        (refId = 돌 ID)
unit        — 유닛/컴패니언 지급 (refId = pokemonId)
augment     — 증강 3택1 트리거 (refId 비움)
```

> 메타몽 복제기(minor/major Ditto)·모루(Anvil)·재조합기(Reforger)는 전부 `kind=item` + `refId`로 표현합니다. (별도 컬럼 만들지 않음)

### 작성 예시

| rewardId | kind | refId | amount | dropChance |
|---|---|---|---|---|
| RW001 | gold | | 3 | 1 |
| RW005 | gold | | 6 | 1 |
| RW005 | reroll | | 2 | 1 |
| RW005 | item | MINOR_DITTO | 1 | 1 |
| RW011 | item | REFORGER | 1 | 0.5 |

---

## 📎 참조 목록

**trainerId** (지정 ID만): `KANTO_GYM_BROCK` … `KANTO_GYM_GIOVANNI`, `JOHTO_GYM_FALKNER` … `JOHTO_GYM_CLAIR`, `KANTO_CHAMPION_BLUE`, `JOHTO_CHAMPION_LANCE`, `WILD_COMMON`, `WILD_RARE`, `WILD_ELITE` *(기존 목록 그대로)*

**pokemonId 규칙**
- 포켓몬 DB에 등록된 **영문명을 그대로** 사용 (개발이 별도 공유하는 "포켓몬 ID 목록"에서 복사)
- 한글명/별칭 금지
- ⚠️ 표기가 DB와 다르면 **그 적은 조용히 누락**됩니다(에러 안 뜸). 반드시 목록에서 복사해주세요.

**itemSet / refId(아이템)**: 아이템 시스템(태욱님)과 연동되는 값입니다. 정의표가 나오기 전까지는 `NONE`으로 두셔도 됩니다.

---

## 🛠 개발 측에서 처리할 부분 (기획자 입력 불필요)

- `slot → 헥스 좌표(q/r)` 매핑 (배치도 기준), 좌표 미러링
- `stage`+`round` → `stageId`/`order` 자동 생성
- `battleType`+`trainerId` → 내부 StageType(WildCommon/WildRare/GymBattle/ChampionBattle) 분류
- `pokemonId` 대소문자 무시 매칭 (안전망)
- 임포터: 3시트 join → `Trainer DB` + `StageDatabase` + `RewardDatabase` 생성
- `RewardKind`에 `reroll` 종류 추가, `rewardId`를 문자열 키로 처리

---

## 🔁 원본 대비 변경 요약

| 변경 | 이유 |
|---|---|
| Trainer Entry: `slot`을 **배치 위치**로 정의 + 배치도 명시, `q/r` 옵션 추가 | 위치 기반 전투에 좌표 필수인데 원본엔 없었음 |
| Trainer Entry: `statMul/hpMul/atkMul` 추가 | 보스 난이도 배수를 표현할 칸이 없었음 |
| Trainer Entry: `count` 삭제 (→ 여러 줄) | count>1이면 추가 마리 위치가 미정의됨 |
| Reward Table: 가로형 → **행 단위(kind/refId/amount)** | 아이템 추가마다 컬럼·코드 깨짐 + 원본 컬럼명 불일치 |
| Stage Table: `eventId`에 `ITEM` 추가, 값 매핑 정리 | 내부 enum과 1:1 매칭 |
