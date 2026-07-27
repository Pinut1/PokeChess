# HANDOFF 2026-07-27 — 전투 비주얼 통합

> 이어서 작업하기 전, 이 문서의 커밋 기준과 실제 워킹 트리를 함께 확인할 것.

## 현재 통합 상태

현재 작업 브랜치는 `Pinut1/haein-asset-8row-integration`이며, HEAD는 `b0abaa0f`이다.

| 구분 | 근거 | 상태 |
|---|---|---|
| haein art 에셋 및 VFX 기반 | `96abea1d`가 HEAD의 조상 | 포함됨 |
| 진화 시 모델 프리팹 교체 | `dca63495`가 HEAD의 조상 | 포함됨 |
| 8행(4+4) 단일 전장 | `4be7856a`의 변경이 `b0abaa0f`로 체리픽됨 | 포함됨 |

따라서 **A. 브랜치 베이스 정리/통합은 완료 상태**다. art + VFX + `dca63495` 진화 모델 수정 + 8행 전장 변경이 이 브랜치에 모여 있다. 이 문서는 코드 실행이나 Unity Play 결과를 새로 판정하지 않으며, 위 상태는 커밋 그래프와 변경 내용 기준이다.

## A에서 반영된 8행 전장

`b0abaa0f`는 적을 기존 아군 4행에 겹쳐 놓고 시각용 Z 오프셋으로 분리하던 방식을 제거했다.

- `BoardManager.GetEnemyBattleCoords`가 적 로컬 좌표를 대칭한 뒤 `_rows`만큼 옮겨, 적을 rows 4~7에 배치한다.
- `BattleManager`의 적 스폰, 미러 보드 타일, 비주얼 위치 갱신이 위 좌표를 사용한다.
- `ENEMY_BOARD_OFFSET` 및 적 전용 시각 오프셋 분기는 제거되어, 논리 좌표와 시각 좌표가 같은 8행 전장을 사용한다.

원본 전장 통합 커밋은 `4be7856a`이고, 현재 브랜치에는 같은 변경이 `b0abaa0f`로 들어와 있다. 따라서 `4be7856a` 자체가 HEAD의 조상인지로 통합 여부를 판단하면 안 된다.

## B/C 구현 상태

### B. 8행 전장 센터링/카메라

`BoardManager.GenerateBoard()`가 `_centerOffset`을 계산할 때, 생성된 아군 4행 좌표뿐 아니라 `GetEnemyBattleCoords()`로 얻은 적 4행 좌표도 함께 평균낸다. 따라서 보드 기준점은 8행 전체의 중심을 사용한다.

- 상태: 코드 구현 및 Unity 컴파일 완료. Play 모드 육안 프레이밍 확인은 남음.
- 범위: 보드 중심값을 8행 기준으로 조정. 카메라 씬 설정은 아직 변경하지 않음.
- 주의: 8행 좌표 배치 자체(`GetEnemyBattleCoords`)를 되돌리는 작업이 아니다.

### C. 전투 비주얼 캡슐을 실제 모델로 교체

`BattleUnit`이 전투 시각화용 `PokemonData`를 보존하고, `BattleManager.SpawnVisual(BattleUnit bu)`가 `data.modelPrefab`을 생성하도록 변경했다. 모델 프리팹이 없을 때만 기존 팀 색상 캡슐을 폴백으로 사용한다.

- 상태: 코드 구현 및 Unity 컴파일 완료. 실제 전투 Play 육안 확인은 남음.
- 적용 범위: 아군, 스테이지 적, 돌연변이 봇 생성 경로 모두 `PokemonData`를 전달한다.
- 유지 사항: 기존 위치 갱신/전투 종료 정리와 보드 원본 오브젝트 비활성화·복귀 흐름은 그대로 사용한다.
- 확인 항목: 모델 크기·피벗·방향, 아군/적 실제 표시, 전투 종료 후 복귀.

## 검증 결과

- Unity 6000.3.8f1 스크립트 컴파일 오류 0건.
- EditMode 테스트 16개 중 15개 통과.
- 실패 1개: `SceneStabilityTests.GameScene_UsesNetworkMode_AndSingleOpponentBoardView` — `GameSceneTest`의 기존 Photon 네트워크 모드 설정 기대 불일치로, 이번 B/C 변경 파일과 무관.
- 남은 검증: GameSceneTest Play 모드에서 8행 프레이밍과 모델 피벗·크기 육안 확인.

## 진화 모델 교체 — 재구현 금지

진화 시 모델이 바뀌지 않던 문제는 이미 `dca63495`에서 해결됐다.

- `PokemonUnit.RefreshVisual()`이 현재 `data.modelPrefab`으로 시각 자식을 다시 생성한다.
- `UnitFactory` 생성 시와 데이터 스왑 경로(합체 진화, 진화의 돌 장착/해제)에서 이를 호출한다.

후속 작업에서는 이 기능을 **재구현하지 말 것**. A 통합 브랜치에서 진화 전후 모델이 기대대로 교체되는지만 확인한다. 이는 C의 전투 전용 `SpawnVisual` 캡슐 문제와 별개다.

## 작업 진입점

- `Assets/_Project/Scripts/Managers/BoardManager.cs`
  - `_centerOffset`, 보드 생성, `CoordsToWorldPosition` — B
  - `GetEnemyBattleCoords` — A 반영 확인용
- `Assets/_Project/Scripts/Managers/BattleManager.cs`
  - `SpawnVisual` — C
  - `UpdateVisualPosition`, 전투 종료 비주얼 정리 — C 영향 범위 확인
- `Assets/_Project/Scripts/Core/PokemonUnit.cs`
  - `RefreshVisual` — 이미 반영된 진화 모델 교체, 확인만

## 혼동 방지

- **진화 모델 교체**: 보드/유닛의 `PokemonUnit.RefreshVisual` 경로이며 `dca63495`에서 완료됐다.
- **전투 캡슐 교체**: `BattleManager.SpawnVisual` 경로이며 C 코드 구현 완료, Play 육안 확인이 남았다.
- **8행 좌표 통합**: A에서 반영됐다. B에서 8행 전체 평균 센터링을 구현했으며 Play 프레이밍 확인이 남았다.
