# v11 CSV 데이터 수정 요청 (2026-07-14)

> 대상: 기획 / 황해인. v11 CSV(Pokemon DB·VFX Table·DeckList·DeckUnit)를 임포트하기 전
> 정리가 필요한 항목들입니다. 아래 반영해서 **UTF-8로 재내보내기**만 해주시면 바로 임포트합니다.

## skillId 네이밍 규칙 (확정)

- 기본형: `{타입}_{역할군}` 대문자 스네이크 — 예: `GRASS_MAGICIAN`, `FIRE_TANKER`
- **한 타입+역할에 스킬이 2개면(중복)**: 뒤에 effectType을 그대로 붙임 → `{타입}_{역할군}_{효과}`
- vfxId는 skillId와 1:1, `VFX_` 접두 — 예: `WATER_TANKER_SHIELD` → `VFX_Water_Tanker_SHIELD`

## ① skillId 중복 4건 → 분리

| 현재 (중복) | 효과 | → 최종 skillId | vfxId |
|---|---|---|---|
| `WATER_TANKER` | SHIELD | `WATER_TANKER_SHIELD` | `VFX_Water_Tanker_SHIELD` |
| `WATER_TANKER` | HP_REGEN | `WATER_TANKER_HP_REGEN` | `VFX_Water_Tanker_HP_REGEN` |
| `POISON_TANKER` | SHIELD | `POISON_TANKER_SHIELD` | `VFX_Poison_Tanker_SHIELD` |
| `POISON_TANKER` | HP_REGEN | `POISON_TANKER_HP_REGEN` | `VFX_Poison_Tanker_HP_REGEN` |
| `GROUND_TANKER` | SHIELD | `GROUND_TANKER_SHIELD` | `VFX_Ground_Tanker_SHIELD` |
| `GROUND_TANKER` | HP_REGEN | `GROUND_TANKER_HP_REGEN` | `VFX_Ground_Tanker_HP_REGEN` |
| `CHEER_SUPPORTER` | MANA_REGEN | `CHEER_SUPPORTER_MANA_REGEN` | `VFX_Cheer_Supporter_MANA_REGEN` |
| `CHEER_SUPPORTER` | AS_BUFF | `CHEER_SUPPORTER_AS_BUFF` | `VFX_Cheer_Supporter_AS_BUFF` |

> Pokemon DB에서 이 스킬을 참조하는 포켓몬(예: 파치리스·플러시&마이너스)의 `skillId`도
> 위 둘 중 어느 스킬을 쓰는지에 맞춰 갱신 필요.

## ② 이름 통일 (조인 깨짐 — 결정 필요)

| 항목 | 기존 코드/데이터 | v11 시트 | 통일안 |
|---|---|---|---|
| 치어리더 타입 | `CHEER` | `Cheerleader` | **`CHEER`로 통일** (기존 데이터·파치리스 조인 기준. 시트를 CHEER로 맞춰주세요) |

> 시트를 못 바꾸는 상황이면 반대로 코드/데이터를 `CHEERLEADER`로 맞추는 것도 가능 — 회신 주시면 그쪽으로 처리.

## ③ 그 외 수정 (같이 처리)

| 항목 | 문제 | 수정 |
|---|---|---|
| `Normal_Warrior, Archer` 행 | 역할군이 콤마로 2개 결합됨 | `NORMAL_WARRIOR` / `NORMAL_ARCHER` 2행으로 분리 |
| 님피아(id 700) `attackVfxId` | `Elecric_L` 오타 | `Electric_L` |
| **CSV 전체** | 한글이 전부 `?`로 소실 (ANSI 내보내기 추정) | **UTF-8로 재내보내기** (덱 이름·포켓몬명·스킬명 복구 불가) |

---

## 참고 — 재내보내기 후 자동 반영되는 것 (수정 불필요)

임포터/변환기가 알아서 처리하므로 시트에서 손댈 필요 없는 항목:

- vfxId 네이밍 변경 22건(`VFX_X_Y` → `VFX_X_Y_EFFECT`)은 **아트 5종 프리팹 연결과 충돌 우려**가 있어
  일단 skill_table 기존 vfxId를 유지합니다. (아트팀 장한나님과 별도 조율 후 일괄 적용 예정)
- 이브이 계열 skillId 정리(MUTANT_* → 실제 타입) 등 밸런스 변경 26건은 그대로 반영됩니다.
