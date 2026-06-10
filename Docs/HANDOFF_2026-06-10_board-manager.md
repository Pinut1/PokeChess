# 인수인계 — BoardManager / SynergyManager / BattleManager (2026-06-10)

기존 담당자(김기욱)가 사정상 하차하여, 이후 보드/전투 파트는 김영욱이 이어받아 진행합니다.
원래 계획 문서: [포켓체스 개발 정리 - 김기욱](https://app.notion.com/p/3792391f7a838000ac67d324bc6ac1ce)

## 개발 순서
BoardManager → SynergyManager → BattleManager

## 현재까지 구현된 것 (`feature/BoardManager_KKW` → master 머지 완료, 커밋 `61d5764`)
- `Assets/_Project/Scripts/Core/HexCoords.cs` — 헥스 큐브 좌표계(q, r, s) 구조체. `IEquatable` 구현, `ToWorldPosition()`, `DistanceTo()` 포함
- `Assets/_Project/Scripts/Core/IDropTarget.cs` — 드래그앤드롭 공통 인터페이스 (`OnDropUnit`, `OnHoverEnter/Exit`)
- `Assets/_Project/Scripts/Core/HexTile.cs` — 개별 헥스 타일. `IDropTarget` 구현, 매니저 직접 참조 대신 콜백(`Action<PokemonUnit, HexCoords>`)으로 디커플링
- `Assets/_Project/Scripts/Managers/BoardManager.cs`
  - `_battleField: Dictionary<HexCoords, PokemonUnit>` — 보드 상태 단일 진실 공급원
  - `GenerateBoard()` — 4x7 플랫탑 헥스 맵 자동 생성 + 중앙 정렬
  - `TryPlaceUnit()` — 현재 로그만 출력하는 스텁 (배치/스왑 룰 미구현)

## 새로 도입된 컨벤션 (주의)
이번 머지에서 처음으로 `namespace PokeChess.Core` / `PokeChess.Managers`가 도입됨.
기존 Core/Network 파일(`GameManager`, `GameEvents`, `NetworkManager`, `PokemonUnit` 등)은 네임스페이스 없음(글로벌).
→ 당장 컴파일엔 문제없으나, 앞으로 신규 파일에 네임스페이스를 쓸지 통일 여부 결정 필요.

## 다음 작업 (Day by day로 진행 예정)

### 1순위 — BoardManager Phase 1 마무리
- [ ] **UnitField (벤치)** — 1차원 배열 기반 벤치 슬롯 시스템 구현 (오버엔지니어링 지양, Phase 1의 마지막 미완 항목)

### 2순위 — BoardManager Phase 2
- [ ] `TryPlaceUnit` 실제 배치/스왑 로직 구현 (현재 로그만 찍는 스텁)
- [ ] 배치 제한 한도(Max Units) 룰 검사
- [ ] 판매 시 자리 비움 + `GameEvents.OnUnitSold` 연동

### 이후 — Phase 3, 4 및 SynergyManager, BattleManager
원본 Notion 계획 문서의 Phase 정의를 그대로 따라가되, 매 세션 시작 시 "한 줄 목표"를 정해서 범위를 좁혀 진행.

## 참고
- `OnBoardStateChanged` / `OnSynergyUpdated` 이벤트는 아직 `GameEvents.cs`에 미반영 (네이밍 fix 안 됨, 6/8 결정대로 `OnUnitPlaced`/`OnUnitBenched`/`OnUnitSold` 트리거 + `BoardManager.GetUnitsOnBoard()` pull 방식 하이브리드로 진행)
