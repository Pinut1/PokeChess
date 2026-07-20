# PR #47 / #48 Unity MCP 2클라이언트 테스트 결과

- 일시: 2026-07-20 (KST)
- 기준 브랜치: `master` (`66583b62`)
- 대상: `feature/reconnect-board-resync` (`ec724c38`), `feature/partner-board-model` (`0b04b0ff`)
- 환경: Unity `6000.3.8f1`, Photon PUN2, Unity MCP 서버 `3.4.4`, 패키지 `com.coplaydev.unity-mcp 10.1.1-beta.1`
- 클라이언트: 원본 프로젝트 + ParrelSync `PokeChess_clone_0`

## 결론

| 대상 | 판정 | 요약 |
|---|---|---|
| #47 재접속 보드 재동기화 | 핵심 경로 통과 / 최종 재시험 권장 | 강제 연결 손실 후 양쪽 모두 `Joined` 복귀, 보드 revision 양방향 재동기화 및 최종 라운드 값 수렴 확인. 단, 테스트 씬의 `_soloMode=true` 때문에 런타임 우회 전 독립 라운드가 진행되어 깨끗한 라운드 복구 판정은 오염됨. |
| #48 파트너 보드 모델 | 코드 경로 통과 / 데이터 통합 미완 | 종 해석, 모델 생성, 콜라이더 제거, 종별 풀 재사용, 잘못된 ID 폴백을 확인. 그러나 현재 `PokemonDatabase` 139종의 `modelPrefab`이 모두 비어 있어 실제 데이터로는 전부 캡슐 폴백만 표시됨. |

두 PR 모두 컴파일 오류는 없었다. 다만 현재 상태 그대로 병합 판단을 내리기 전에 아래 씬/데이터 선행 항목을 정리하고 2클라이언트 최종 확인을 한 번 더 하는 것이 안전하다.

## #47 재접속/후입장 보드 재동기화

### 정적·자동 테스트

- 양쪽 Unity Editor 강제 Refresh 후 C# 컴파일 오류 0건.
- EditMode job `9c427c11445840cd94676084d86eea21`: `succeeded`, 실제 테스트 `0`개.
- PlayMode job `acb64237bcb5468aa275290c5548192f`: `succeeded`, 실제 테스트 `0`개.
- Test Runner의 assembly 노드는 Passed였지만 테스트 케이스가 없으므로 회귀 테스트 통과로 간주하지 않았다.

### 2클라이언트 Photon 테스트

1. 두 클라이언트가 같은 방에 입장해 `Joined`, `Players=2`를 확인했다.
2. 일반 보드 스냅샷 전송에서 송신측 local revision `1`, 수신측 opponent revision `1`을 확인했다.
3. clone에 `LoadBalancingClient.SimulateConnectionLoss(true)`를 적용하고, 손실 중 main의 보드와 라운드를 변경했다.
4. 시뮬레이션 해제 로그 `[MCPTest] network simulation restored`를 확인했다.
5. 재접속 후 상태:
   - main: local revision `1 → 3`, clone: opponent revision `1 → 3`
   - clone: local revision `0 → 1`, main: opponent revision `-1 → 1`
   - 양쪽 모두 `NetworkClientState=Joined`, `Players=2`
   - 최종 확인 시 양쪽 모두 network field round `5`, room property round `5`, phase `Result`

보드 스냅샷의 강제 양방향 재송신과 최신 revision 수신은 확인됐다.

### 판정 제한

`Assets/Scenes/GameSceneTest.unity`의 두 클라이언트 모두 scene-local `NetworkManager._soloMode`가 `true`였다. 이 때문에 Photon 방에 들어와 있어도 각 클라이언트가 먼저 솔로 라운드를 독립 시작했다. 테스트에서는 파일을 수정하지 않고 reflection으로 `_soloMode=false`를 적용해 네트워크 경로를 검증했다.

최종 병합 전에는 테스트 씬의 solo 설정을 바로잡고, 새 방에서 다음 항목을 다시 확인해야 한다.

- 연결 손실 전 동일 라운드
- 손실 중 master만 라운드 진행
- 재접속 직후 room property와 local round 즉시 일치
- 독립 Reward/RoundPhase 타이머가 두 번 시작되지 않음

## #48 파트너 보드 종/모델 표시

### 정적·자동 테스트

- 양쪽 Unity Editor 강제 Refresh 후 C# 컴파일 오류 0건.
- EditMode job `fb45932ab48f467ab7b3142a295d7025`: `succeeded`, 실제 테스트 `0`개.
- PlayMode job `81dd5d85f3b1477eba01117599916d30`: `succeeded`, 실제 테스트 `0`개.

### 2클라이언트 및 런타임 검증

- 두 클라이언트가 같은 Photon 방에서 `Joined`, `Players=2`, `GameSceneTest` 진입을 확인했다.
- main이 Bulbasaur(`speciesId=1`, 2성) 스냅샷을 RPC로 전송했고 clone에서 `PartnerUnit_Bulbasaur_2star`가 생성됐다.
- 프로젝트의 `PokemonDatabase` 139종을 검사한 결과 `modelPrefab != null`인 데이터는 `0`종이었다. 따라서 실제 데이터 기반 수신에서는 정상적으로 캡슐 폴백이 사용됐다.
- 잘못된 `speciesId=999999`를 전달했을 때 예외 없이 `PartnerUnit_999999_3star` 캡슐이 생성되고 종 해석 실패 경고가 출력됐다.
- 런타임에서 Bulbasaur에 임시 Cube 프리팹을 연결해 모델 분기를 격리 검증했다.
  - 생성 mesh: `Cube` — 캡슐 폴백이 아닌 프리팹 분기 확인
  - 같은 종을 비우고 다시 렌더한 instance ID: `-10036 → -10036` — 풀 재사용 확인
  - 다음 프레임 자식 Collider 수: `0` — 읽기 전용 미러의 레이캐스트 차단 확인

### 통합 잔여

1. 실제 종 모델을 표시하려면 원본 데이터/에셋 파이프라인에서 각 `PokemonData.modelPrefab`을 연결해야 한다. SO를 직접 수정하지 말고 프로젝트의 데이터·에셋 연결 원칙에 맞춰 처리해야 한다.
2. `GameSceneTest.unity`의 동일 `GameManager`에 `OpponentBoardView`가 2개 존재한다.
   - 하나의 offset: `(500, 0, 0)`
   - 다른 하나의 offset: `(0, 0, 14)`
   - 둘 다 `GameEvents.OnOpponentBoardChanged`를 구독해 스냅샷마다 렌더와 경고를 중복 처리한다.
   - 이 중복은 #48 변경 전 `master`에도 존재하는 선행 씬 문제다. 의도한 `(0, 0, 14)` 하나만 남기는지 확인이 필요하다.

## 함께 발견된 기존 환경 이슈

- `GameSceneTest.unity`: `NetworkManager._soloMode=true`로 2클라이언트 테스트 흐름과 충돌.
- `GameSceneTest.unity`: `OpponentBoardView` 컴포넌트 2개 중복.
- Console: `The referenced script (Unknown) on this Behaviour is missing!`.
- clone Console: 구 Input Manager 사용 관련 오류 로그.
- 일부 이전 MCP 서버 재시작의 WebSocket 오류 로그가 main Console에 남아 있었으나 PR 코드 회귀로 보지 않았다.

## 권장 다음 순서

1. `GameSceneTest`의 `_soloMode`를 네트워크 테스트 목적에 맞게 `false`로 정리한다.
2. 중복 `OpponentBoardView`의 의도를 확인하고 하나만 유지한다.
3. 최소 1종(Bulbasaur/Eevee 등)의 실제 `modelPrefab` 연결을 데이터/에셋 파이프라인에서 완료한다.
4. 깨끗한 새 방에서 #47 라운드 복구와 #48 실제 모델 표시를 동시에 2클라이언트 재확인한다.

테스트 중 사용한 `_soloMode` 변경과 임시 Cube 프리팹은 런타임에만 적용했으며 씬/SO에는 저장하지 않았다. Unity MCP 자동 시작 설정도 테스트 종료 후 원래 값인 `false`로 복구했다.

## 후속 안정화 반영

사용자 결정에 따라 같은 날 아래 씬 안정화를 실제 파일에 반영했다.

- `GameSceneTest`: `NetworkManager._soloMode`를 `false`로 변경.
- `GameSceneTest`: offset `(500, 0, 0)`인 중복 `OpponentBoardView` 제거. `(0, 0, 14)` 한 개만 유지.
- `NetworkTest`: 삭제된 `ItemDebugTest`를 가리키던 Missing Script 컴포넌트 제거.

반영 후 검증 결과:

- `NetworkTest`: valid, dirty false, Missing Script 0개.
- `GameSceneTest`: valid, dirty false, Missing Script 0개, `OpponentBoardView` 1개, solo false.
- 우회 코드 없이 두 클라이언트가 같은 Photon 방에 `Joined`, `Players=2`로 입장.
- 양쪽 모두 GameScene에서 solo false, 미러 컴포넌트 1개 확인.
- master가 라운드 1과 2를 순서대로 브로드캐스트하고 양쪽이 같은 라운드 흐름을 수신.
- `NetworkTest` 재실행 후 Console error 0건.

따라서 앞 절의 씬 관련 재시험 권고 1·2번은 완료됐다. 남은 #48 사용자 가시성 조건은 실제 `PokemonData.modelPrefab` 연결이다.

### 개발 빌드 점검

- Build Settings의 활성 씬은 `NetworkTest`와 `GameSceneTest` 두 개로 확인됐다.
- Windows x64 Development Build를 `C:/tmp/PokeChessSmokeBuild/PokeChess.exe` 대상으로 재실행했다.
- 결과: `Build Succeeded`, 오류 0건, 경고 5건, 169.61MB, 150.14초.
- 경고 중 코드 관련 항목은 `UIManager`의 사용되지 않는 hover index 필드 2개(`CS0414`)이며 빌드 차단 항목은 아니다. 나머지는 URP/디버그 셰이더 및 MCP 재연결 메시지다.
- 생성된 Player를 30초간 실행했으며 프로세스가 살아 있고 어셈블리 로드, Photon Master 연결, Lobby 입장까지 확인했다.
- Player 로그에서 크래시, 예외, Missing Script는 발견되지 않았다. D3D12 info queue 질의 실패 메시지 1건은 렌더러 초기화 후 정상 진행되어 비차단으로 분류했다.

### 안정화 회귀 테스트 추가

기존 Unity Test Runner에 실제 테스트 케이스가 0개였던 공백을 줄이기 위해 `PokeChess.EditorTests` EditMode assembly와 씬 안정화 테스트 3개를 추가했다.

- 활성 Build Settings에 `NetworkTest`·`GameSceneTest`가 포함되는지 검사.
- 모든 활성 Build Scene의 Missing Script가 0개인지 검사.
- `GameSceneTest`가 network mode(`_soloMode=false`)이며 `OpponentBoardView`가 정확히 1개인지 검사.

최종 Unity Test Runner 결과: `3 passed / 0 failed / 0 skipped`, job `f08f9bccc34346a9a8557e33b76da5e1`.
