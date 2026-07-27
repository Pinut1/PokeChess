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

## 후속 작업

### B. 8행 전장 센터링/카메라

`BoardManager._centerOffset`은 현재 생성하는 아군 4행 타일 평균으로 계산된다. 적 rows 4~7은 전투 시 좌표 변환으로 추가되는 구조이므로, 8행 전장 전체 기준의 화면 중심/카메라가 필요하면 별도로 조정해야 한다.

- 상태: 미완료. 육안 및 Play 모드 확인 뒤 조정할 작업.
- 범위: 보드 중심값, 카메라 위치/프레이밍 및 관련 씬 설정.
- 주의: 8행 좌표 배치 자체(`GetEnemyBattleCoords`)를 되돌리는 작업이 아니다.

### C. 전투 비주얼 캡슐을 실제 모델로 교체

`BattleManager.SpawnVisual(BattleUnit bu)`는 현재도 `GameObject.CreatePrimitive(PrimitiveType.Capsule)`로 전투 비주얼을 생성하고 팀별 파랑/빨강을 적용한다. 보드의 `PokemonUnit` 모델과는 별도 전투 경로다.

- 상태: 미완료.
- 목표: BattleUnit의 데이터에서 `data.modelPrefab`을 사용해 실제 모델을 생성하고, 기존 위치 갱신/정리 흐름과 호환되게 한다.
- 전제: A 통합 브랜치에서 art 에셋을 사용할 수 있으므로, 이 브랜치에서 구현·테스트한다.
- 확인 항목: 아군/적 모두 모델 생성, 프리팹 부재 시의 안전한 폴백, 전투 종료 시 정리, 기존 보드 오브젝트 비활성화/복귀 흐름.

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
- **전투 캡슐 교체**: `BattleManager.SpawnVisual` 경로이며 C에서 처리할 미완료 작업이다.
- **8행 좌표 통합**: A에서 반영됐다. 남은 B는 좌표 통합이 아니라 화면 센터링/카메라 조정이다.
