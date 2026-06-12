# BattleManager 구현 (크리티컬 패스 D)

## Context
인수인계 개발 순서상 BoardManager(완) → SynergyManager(완) 다음은 BattleManager. 원담당자(김기욱)가
팀을 떠나면서 비어있는 스텁(`Assets/_Project/Scripts/Managers/BattleManager.cs`)을 우리가 구현한다.
`RoundPhaseManager`는 이미 `GameEvents.OnBattleStart`를 발화하고 `OnBattleEnd(bool isWin)`을 기다리는
구조로 되어 있고, 솔로모드 임시 승리/패배 버튼이 그 자리를 메우고 있다. 이번 작업의 목표는 그 버튼을
실제 자동 전투 시뮬레이션으로 대체하는 것.

**PvP 상대 보드 동기화는 아직 구현되어 있지 않음** (NetworkManager에 enemy board sync 없음) — 이번 스코프
밖. v1은 "내 보드 vs 적 팀 스냅샷"을 입력으로 받는 자체 완결형 시뮬레이터로 만들고, 적 팀 데이터는
일단 자기 보드의 미러(거울 복제)를 사용한다. 추후 네트워크 동기화가 들어오면 적 팀 입력만 교체하면 됨.

## 설계 결정
- **시뮬레이션 방식**: 실시간 코루틴 기반 tick 시뮬레이션 (TFT 느낌). `FixedUpdate` 대신
  `BattleManager`에서 코루틴으로 `0.1초` 간격 tick.
- **스냅샷**: 전투 시작 시 `BoardManager.GetBoardSnapshot()`으로 보드 좌표→유닛을 읽어 `BattleUnit`
  (런타임 전투용 경량 클래스, PokemonUnit을 감싸는 값 보관 — currentHp, currentMana, 좌표, 팀)으로 복제.
  원본 `PokemonUnit`(씬 오브젝트)은 변경하지 않음 (PokemonUnit.cs 주석의 기존 계약 유지).
- **적 팀**: `_battleField`를 보드 중앙 기준으로 r축 대칭 미러링한 좌표에 동일 유닛 복제 배치 (자기 자신과
  거울 대결). `BattleManager`에 `GetMirroredCoords(HexCoords)` 헬퍼.
- **시너지 버프**: 전투 시작 시 `GameManager.Instance.Synergy.GetActiveSynergies()`를 pull, 각
  `SynergyTier`에 구조화된 효과 데이터가 없으므로(설명 문자열뿐) **이번 v1에서는 버프 적용 보류** —
  `// TODO: SynergyTier에 효과 데이터 추가되면 적용` 주석만 남김. (메모리에도 동일 TODO 있음)
- **전투 로직 (1틱당)**:
  1. 각 유닛: 대상 없으면 사거리 내 가장 가까운 적 탐색 (헥스 거리 `HexCoords` 기반).
  2. 사거리 내 적 있으면 `AttackSpeed` 기반 쿨다운 누적 → 쿨다운 충족 시 공격:
     - 데미지 = `AttackType == Physical ? Attack - Defense : SpecialAttack - SpecialDefense` (최소 1).
     - 피격자 `currentHp -= damage`. 0 이하면 사망 처리(리스트에서 제거).
  3. 사거리 밖이면 적 방향으로 한 칸 이동(간단화: 매 tick마다 헥스 인접 좌표 중 적과 거리가 줄어드는
     칸으로 이동, 점유 안 된 칸만).
  4. 한쪽 팀의 유닛이 모두 사망하면 전투 종료 → `GameEvents.BattleEnd(승리 여부)`.
  5. 타임아웃(예: 30초 = tick 300회) 시 남은 총 HP 비교로 승패 결정.
- **시각화**: 기존 SynergyDebugTest 패턴처럼, 적 팀 유닛은 `PokemonUnit`을 `Instantiate`해 미러 좌표의
  `HexTile.transform.position`에 배치(Cylinder 자식 포함), 전투 종료 후 제거.
- **BattleDebugTest.cs (신규)**: OnGUI로 "전투 시작(디버그)" 버튼 + 매 tick 로그(유닛별 HP) +
  결과 로그. `RoundPhaseManager`의 "승리/패배(임시)" 버튼은 그대로 두되, BattleManager가 정상 동작하면
  `OnBattleStart` 수신 시 자동으로 시뮬레이션이 돌고 `BattleEnd`가 발화되므로 임시 버튼은 더 이상
  필요 없어짐 → **이번 PR에서 제거**하고 주석도 정리.

## 단계
1. **Docs/PLAN_2026-06-12_battle-manager.md**로 이 플랜 저장 (작업 컨벤션).
2. `Assets/_Project/Scripts/Managers/BattleManager.cs`:
   - `OnEnable/OnDisable`: `GameEvents.OnBattleStart += HandleBattleStart` 구독.
   - `BattleUnit` 내부 클래스 (또는 별도 파일) — 스냅샷 데이터.
   - `RunBattle()` 코루틴: 스냅샷 생성 → 미러 적 팀 생성/시각화 → tick 루프 → 승패 판정 →
     `ResetForBattle()` 호출(원본 유닛 풀회복, 기존 API 재사용) → 시각화 정리 → `GameEvents.BattleEnd(isWin)`.
   - 헥스 인접/거리 계산은 `HexCoords`에 필요한 헬퍼가 있는지 확인 후, 없으면 `HexCoords.cs`에 추가
     (`Distance`, `Neighbors`).
3. `Assets/_Project/Scripts/Core/HexCoords.cs`: 거리/인접 헬퍼 추가 (없을 경우).
4. `Assets/_Project/Scripts/Core/RoundPhaseManager.cs`: Battle 페이즈 임시 승리/패배 버튼 제거
   (`OnGUI`의 `case GamePhase.Battle` 블록 정리), 관련 주석 정리.
5. `Assets/_Project/Scripts/Debug/BattleDebugTest.cs` (신규): OnGUI로 전투 상태/로그 표시.
6. `Assets/_Project/Scripts/Managers/BattleManager.cs` 상단 `// 김기욱 파트` 주석 제거.

## 검증 (Play 모드, 솔로모드)
1. 보드에 유닛 2~3개 배치 → Ready → Battle 진입 시 자동으로 미러 적 팀 생성, 전투 진행 로그 확인.
2. 한쪽 전멸 또는 타임아웃 시 `BattleEnd` 발화 → Result → 다음 라운드로 정상 전환 (회귀 확인).
3. 유닛 1개도 없이 Battle 진입 시 즉시 종료(엣지케이스) 확인.

## 수정/생성 파일
| 파일 | 작업 |
|---|---|
| `Docs/PLAN_2026-06-12_battle-manager.md` | 신규 (플랜 보존) |
| `Assets/_Project/Scripts/Managers/BattleManager.cs` | 스텁 → 구현 |
| `Assets/_Project/Scripts/Core/HexCoords.cs` | 거리/인접 헬퍼 추가 (필요시) |
| `Assets/_Project/Scripts/Core/RoundPhaseManager.cs` | 임시 버튼 제거 |
| `Assets/_Project/Scripts/Debug/BattleDebugTest.cs` | 신규 |
