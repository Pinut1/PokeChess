# 솔로(1인) 테스트 모드 — 2026-06-12

## Context
SynergyManager 등 게임플레이 테스트를 하려면 매번 Photon 2인 매칭이 필요해 번거로움.
게임 루프(라운드 시작/준비 완료/전투 시작)가 전부 Photon RPC/CustomProperties에 묶여 있어,
1인 플레이가 불가능. 최소 수정으로 오프라인 1인 루프를 돌 수 있게 한다.

## 설계 (최소 수정 — Photon OfflineMode 대신 단순 우회)
- `NetworkManager`(PUN2 변형)에 `[SerializeField] bool _soloMode` 추가 (인스펙터 토글)
- 솔로일 때:
  - `Start()`: Photon 연결 없이 바로 `BroadcastRoundStart(1)` → 게임 루프 시작
  - `BroadcastRoundStart(round)` → `GameEvents.RoundChanged(round)` 직접 발화 (RPC 우회)
  - `BroadcastBattleStart()` → `GameEvents.BattleStart()` 직접 발화
  - `BroadcastPlayerReady()` → `GameEvents.AllPlayersReady()` 즉시 발화 (1인 = 전원 준비)
  - `IsMasterClient` → 항상 true (ResultTimer의 다음 라운드 진행에 필요)
- `RoundPhaseManager` 디버그 OnGUI: Battle 페이즈에 "승리/패배" 버튼 추가
  (BattleManager 스텁이라 전투가 안 끝남 → 수동으로 BattleEnd 발화해 루프 한 바퀴 확인)
  기존 Ready 버튼과 같은 임시 디버그 UI 패턴.

## 수정 파일
- `Assets/_Project/Scripts/Network/NetworkManager.cs`
- `Assets/_Project/Scripts/Core/RoundPhaseManager.cs`

## 검증 (Unity Play 모드, GameSceneTest 직접 실행)
1. NetworkManager 인스펙터에서 Solo Mode 체크 → 플레이
2. 라운드 1 / Shopping 진입 로그 확인
3. Ready 버튼 → Battle 진입 → 승리/패배 버튼 → Result → 라운드 2 Shopping 복귀
4. 솔로 모드 끄면 기존 Photon 흐름 그대로인지 (회귀 없음)
