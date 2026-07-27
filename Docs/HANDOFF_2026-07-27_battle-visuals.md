# HANDOFF 2026-07-27 — 전투 비주얼 & 브랜치 정리

> orca ade에서 이어서 작업하기 위한 인계 문서. 착수 전 이 문서 + 각 브랜치 실제 코드 대조할 것.

## 브랜치 현황

| 브랜치 | 커밋 | 내용 | 상태 |
|--------|------|------|------|
| `master` | `c7885508` | 클린 (origin과 동일) | 기준 |
| `integration/haein-asset-merge` | `96abea1d` | haein art(프리팹/머티리얼/셰이더/스프라이트) + VFX 4커밋 + `.vs` 정크 정리 | **검증 완료, 미머지(PR 대기)** |
| `Pinut1/battle-board-unification` | `4be7856a` | 전투 8행 단일전장 통합(오늘) — **master 기준** | 미머지 |
| `Pinut1/evolution-visual-star-multiplier` | `4acc9118` | VFX 원본 (integration에 포함됨) | — |

## 오늘(7/27) 완료

1. **haein_asset 로컬 통합** — haein은 `.cs` 무수정 확인, 충돌 0건 클린 머지. Unity 검증: 컴파일 에러 0, 씬 미싱 스크립트/프리팹 0, art 스프라이트 배선 정상. `.vs/`(VS Copilot 캐시 6.5MB) 추적 제거.
2. **전투 8행 단일전장 통합** (`4be7856a`) — 적을 `GetMirroredCoords`로 아군과 같은 rows 0~3에 접고 시각만 `ENEMY_BOARD_OFFSET`(Z+10)로 분리하던 방식 폐기. 새 `BoardManager.GetEnemyBattleCoords`로 적을 rows 4~7(아군 너머) 연속 배치 + 오프셋 제거. 근접이 미들라인((_rows-1)↔_rows)을 걸어 넘어 교전. 시뮬 로직 무수정. Play 검증: 적 좌표 rows 4~7, Z 오프셋 갭 소멸, 아군/적 한 보드에서 마주봄(스크린샷 확인).

## 다음 작업 분류

### 🟢 할 작업 (착수 가능, A가 B·C의 선행)

- **A. 브랜치 베이스 정리** — `integration/haein-asset-merge`에서 새 작업 브랜치를 따고 board통합 커밋 `4be7856a`를 cherry-pick. 그러면 art + VFX + 진화모델수정(dca63495) + 8행통합이 한 브랜치에 모여 B·C를 전체 그림에서 테스트 가능. *(master 기준 브랜치엔 dca63495/VFX가 없어 아래 증상이 베이스 탓으로 재현됨 — 재구현 유발 주의.)*
- **B. 8행 전장 센터링/카메라** — `BoardManager._centerOffset`이 아군 28칸 기준이라 8행 전장이 아군 쪽으로 치우쳐 렌더됨. 카메라/센터링 조정. 경미.
- **C. 전투 비주얼 캡슐→실제 모델** — `BattleManager.SpawnVisual`이 전투 유닛을 디버그 캡슐(파랑/빨강)로 생성 중. 실제 `data.modelPrefab`을 쓰도록 개선. **A 필수**(모델/art 있어야 테스트). 신규 작업.

### 🟡 대기 / 하지 말 것

- **진화 시 모델 교체** — 이미 `dca63495`(`PokemonUnit.RefreshVisual()` 추가, data 스왑 경로 전부에서 호출)에서 해결됨. **재구현 금지.** A로 자동 해결, 확인만.
- **`integration → master` PR** — 검증됐으나 대형 art 머지라 팀 조율/리뷰 타이밍 판단 필요.
- **`battle-board-unification → master` PR** — B·C 후속 끝나고 묶어서.

## 핵심 주의 (혼동 방지)

- **"진화 시 모델 안 바뀜"과 "전투 들어가면 캡슐"은 다른 문제.**
  - 진화 모델 = 보드/쇼핑 비주얼, `PokemonUnit.RefreshVisual` 경로 → dca63495에서 이미 수정.
  - 전투 캡슐 = `BattleManager.SpawnVisual` 경로 → 진화수정과 무관, 아직 캡슐(C 작업 대상).
- master 기준 브랜치에서 테스트하면 진화모델/캡슐 증상이 **베이스에 커밋이 없어서** 재현됨. 착수 전 반드시 dca63495 존재 여부 확인.

## 관련 코드 (착수 진입점)

- `Assets/_Project/Scripts/Managers/BattleManager.cs` — `SpawnVisual`(캡슐 생성, C), `UpdateVisualPosition`, `SpawnMirrorBoard`, `SpawnEnemiesFromStage`
- `Assets/_Project/Scripts/Managers/BoardManager.cs` — `GetEnemyBattleCoords`(신규), `GetMirroredCoords`, `CoordsToWorldPosition`, `_centerOffset`(B)
- `Assets/_Project/Scripts/Core/PokemonUnit.cs` — `RefreshVisual`(dca63495, integration 라인에만)
