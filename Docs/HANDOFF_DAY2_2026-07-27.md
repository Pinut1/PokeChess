# PokeChess 인수인계 2일 차 실습서

> 인계 예정일: 2026-07-30
> 작성 기준: 2026-07-27
> 대상: 프로젝트 실행은 완료했지만 Unity·네트워크 코드가 익숙하지 않은 프로그래머
> 목적: 기존 기능의 흐름을 직접 추적하고, 안전하게 작은 수정과 검증을 완료한다.

## 0. 오늘의 완료 기준

아래 항목을 인수자가 직접 수행하면 2일 차가 끝난다.

- [ ] 현재 브랜치·커밋·워킹 트리 상태를 설명한다.
- [ ] Canvas 준비 버튼에서 Photon 준비 집계까지 코드 흐름을 찾는다.
- [ ] `GameEvents`를 사용하는 이유를 설명한다.
- [ ] 작은 UI 변경을 별도 브랜치에서 수행한다.
- [ ] Unity 컴파일 오류 0건과 Play 동작을 확인한다.
- [ ] 의도한 파일만 stage해 커밋한다.
- [ ] 통신기 작업이 별도 브랜치에 있고 아직 통합되지 않았음을 확인한다.
- [ ] 문제 발생 시 먼저 볼 로그와 담당 파일을 찾는다.

## 1. 작업을 시작하기 전에

### 저장소 상태 확인

PowerShell 또는 Git 터미널에서 실행한다.

```powershell
git branch --show-current
git log -1 --oneline
git status --short
```

각 명령의 의미:

| 명령 | 확인하는 것 |
|---|---|
| `git branch --show-current` | 지금 수정이 들어갈 브랜치 |
| `git log -1 --oneline` | 현재 코드의 정확한 기준 커밋 |
| `git status --short` | 아직 커밋하지 않은 변경과 새 파일 |

`git status --short`에 예상하지 못한 파일이 보이면 바로 수정하지 않는다. 다른 작업자의 변경일 수 있으므로 파일 소유자와 먼저 확인한다.

### 작업 브랜치 생성

최종 인계 기준 브랜치가 확정된 뒤 그 브랜치에서 새 작업 브랜치를 만든다.

```powershell
git switch <최종-인계-브랜치>
git pull --ff-only
git switch -c practice/handoff-day2-<이름>
```

`git pull`이 충돌하거나 fast-forward가 불가능하다고 나오면 임의로 merge하지 말고 기존 담당자에게 현재 로그와 상태를 전달한다.

## 2. 실습 A — 준비 버튼 흐름 따라가기

현재 준비 버튼은 매니저를 직접 호출하지 않고 이벤트를 따라간다.

```text
GameSceneTest / BattleRaedy_Button
  → UIManager.HandleBattleReadyButtonClicked
  → GameEvents.RequestPlayerReady
  → RoundPhaseManager.HandlePlayerReadyRequested
  → GameEvents.ApprovePlayerReady
  → NetworkManager.BroadcastPlayerReady
  → 두 플레이어 Ready 집계
  → GameEvents.AllPlayersReady
  → RoundPhaseManager.HandleAllPlayersReady
  → Shopping에서 Battle로 전환
```

### 직접 찾을 파일

1. `Assets/_Project/Scripts/Managers/UIManager.cs`
2. `Assets/_Project/Scripts/Core/GameEvents.cs`
3. `Assets/_Project/Scripts/Core/RoundPhaseManager.cs`
4. `Assets/_Project/Scripts/Network/NetworkManager.cs`

### 확인 질문

인수자가 코드 화면을 보면서 답한다.

1. 쇼핑 페이즈가 아닌데 준비 요청이 들어오면 어디에서 거부하는가?
2. 준비 버튼을 한 번 누른 뒤 다시 누를 수 없게 하는 값은 무엇인가?
3. 솔로 모드와 Photon 모드의 준비 처리는 어디에서 갈라지는가?
4. 두 플레이어가 모두 준비됐다는 최종 이벤트는 무엇인가?
5. `UIManager`가 `RoundPhaseManager`나 `NetworkManager`를 직접 호출하지 않는 이유는 무엇인가?

정답을 외우는 것보다 파일과 메서드를 직접 찾아 설명하는 것이 중요하다.

## 3. 실습 B — 안전한 작은 수정

첫 수정은 게임 규칙이 아니라 UI 문구 또는 Inspector 배치처럼 복구가 쉬운 항목으로 한다.

권장 실습:

- 준비 버튼의 표시 문구 변경
- 전송 실패 안내 문구 개선
- UI Tooltip 또는 주석의 오래된 표현 정리

수정 후 순서:

1. 파일 저장
2. Unity 스크립트 컴파일 종료 대기
3. Console Error 0건 확인
4. `GameSceneTest` Play
5. 수정한 UI와 기존 Level/ReRoll/상점 전환/준비 버튼 회귀 확인
6. Play 종료
7. `git diff -- <수정한 파일>`로 실제 변경 확인

### 커밋

```powershell
git status --short
git add -- <수정한 파일>
git diff --cached
git commit -m "docs: handoff practice update"
```

다음 명령은 사용하지 않는다.

- `git add .`
- `git commit -a`
- `git reset --hard`
- 다른 작업자의 파일을 확인 없이 restore 또는 삭제

## 4. GameEvents 변경 규칙

새 버튼이나 기능을 연결할 때 기본 형태:

```text
UI 입력
  → GameEvents 요청
  → 담당 매니저 검증 및 처리
  → GameEvents 결과
  → UI 갱신
```

예:

```text
XP 버튼
  → RequestXpPurchase
  → ShopManager가 골드·최대 레벨 검증
  → GoldChanged / XpChanged / LevelChanged
  → UIManager 표시 갱신
```

금지 예:

```csharp
// UIManager에서 다른 매니저를 직접 조작하지 않는다.
GameManager.Instance.Shop.BuyXp();
GameManager.Instance.Network.SendGoldToPartner(5);
```

새 이벤트가 필요하면 `GameEvents.cs`에만 정의하고, 요청을 처리할 담당 매니저와 결과를 표시할 구독자를 명확히 기록한다.

## 5. 통신기 작업 상태 — 재구현 금지

### 브랜치

- 원격 브랜치: `origin/feature/trade-ui-integration`
- 2026-07-27 확인 최신 커밋: `0c315969`
- 현재 통합 브랜치 포함 여부: **미포함**
- `origin/master` 포함 여부: **미포함**

이미 구현된 내용:

- 유닛을 통신기에 드래그해 전송
- 상대 대기열 저장
- 통신기 오브젝트 클릭 수령
- 벤치 빈자리만큼 FIFO 일괄 수령
- 수령 성공 후 ACK
- 장비·진화의 돌·통신진화 상태 이전
- 전투 중 필드 유닛 진화 대기
- 성급진화 연동
- `GameEvents.OnGoldTransferRequested(int)` 기반 골드 전송 요청
- 통신기 골드 `1G/5G/10G` UI와 보유 골드별 버튼 상태
- ACK 완료·실패 결과 표시

이 기능을 현재 통합 브랜치에서 다시 만들지 않는다. 브랜치 통합 전에는 아래 순서를 지킨다.

1. 현재 워킹 트리 변경을 먼저 리뷰·커밋한다.
2. `feature/trade-ui-integration`의 변경 파일과 씬 충돌 가능성을 확인한다.
3. `GameEvents`, `UIManager`, `NetworkManager`, `GameSceneTest.unity` 겹침을 우선 검토한다.
4. 통합 후 Unity 컴파일과 솔로 Play를 먼저 확인한다.
5. 이후 Photon 2클라이언트로 전송·수령·ACK를 검증한다.

### 골드 전송 현재 규칙

- UI 후보 금액: `1G`, `5G`, `10G`
- 버튼 클릭 즉시 전송
- 라운드당 횟수 제한 없음
- 라운드당 총액 제한 없음
- 보유 골드 부족 시 거부
- 파트너 미접속 또는 재접속 대기 중이면 거부
- 이전 전송 ACK 대기 중에는 추가 전송 거부
- 송신자 선차감 → 상대 수령 → ACK
- 실패 ACK 수신 시 송신자에게 전액 환급

골드 UI는 최신 커밋에서 `UIManager → GameEvents → NetworkManager` 구조로 구현됐다. 통합 시 아래 이벤트와 기존 로컬 변경의 `GameEvents`·`UIManager` 변경을 함께 충돌 리뷰한다.

```csharp
public static event Action<int> OnGoldTransferRequested;
```

버튼은 요청 이벤트만 발행하고, `NetworkManager`가 금액·연결·ACK 상태를 검증한다. 브랜치 커밋 기록에는 Photon 2인 플레이 테스트 완료로 적혀 있으나, 통합 브랜치 병합 후 전체 회귀 테스트는 다시 수행한다.

## 6. 증상별 진단표

| 증상 | 먼저 확인할 것 | 담당 파일 |
|---|---|---|
| 버튼이 보이지만 눌러도 반응 없음 | persistent/runtime listener, 오브젝트 이름, EventSystem, Raycast | `UIManager.cs`, 해당 Canvas |
| 준비 버튼이 비활성화됨 | 현재 `GamePhase`, 이미 제출했는지 | `UIManager.cs`, `RoundPhaseManager.cs` |
| 준비 후 전투로 안 넘어감 | 양쪽 `Ready` Player Property, MasterClient 집계 로그 | `NetworkManager.cs` |
| 증강 카드가 선택되지 않음 | 패널 전체를 덮는 IMGUI 버튼, PendingOffer | `AugmentOfferHud.cs`, `AugmentManager.cs` |
| 전투 유닛이 캡슐로 보임 | `PokemonData.modelPrefab` 존재 여부 | `BattleManager.cs`, Pokemon 데이터 |
| 진화 후 모델이 안 바뀜 | 기존 구현 `dca63495` 회귀 여부 | `PokemonUnit.cs`, 보드 비주얼 경로 |
| 상점 데이터가 이상함 | JSON 원본과 Import 로그 | `Assets/Resources/Data/`, `PokeChessImporter.cs` |
| 파트너 보드가 과거 상태로 돌아감 | snapshot revision 증가·수신 가드 | `NetworkManager.cs` |
| 골드 전송 후 골드가 사라짐 | ACK, 환급 로그, 파트너 연결 상태 | `NetworkManager.cs` |
| 서버 전적이 안 보임 | Auth 세션, RLS, API URL/key | `SupabaseMatchUploader.cs` |

## 7. 로그 읽는 순서

오류가 생기면 캡처만 보내지 말고 다음 순서로 정리한다.

1. Play를 중단하지 말고 최초 Error의 전체 Stack Trace를 복사한다.
2. 그 Error 직전의 `[Network]`, `[Phase]`, `[Battle]`, `[Trade]`, `[GoldTransfer]`, `[Supabase]` 로그를 함께 본다.
3. 재현 단계와 현재 페이즈·라운드를 기록한다.
4. 솔로 전용인지 Photon 2인에서만 발생하는지 구분한다.
5. 재현 전 워킹 트리 상태와 커밋을 기록한다.

보고 예:

```text
기준: <브랜치> / <커밋>
환경: Unity 6000.3.8f1, Photon 2클라이언트
재현: Shopping에서 5G 전송 → 상대 종료 → 다시 5G 전송
기대: 연결 끊김 안내와 전송 거부
실제: 버튼 비활성화 없이 요청, ACK 미수신
최초 오류: <첫 Error와 Stack Trace>
관련 로그: [GoldTransfer] ...
```

## 8. Photon 2클라이언트 기본 체크

2인 기능은 솔로 Play만으로 완료 처리하지 않는다.

### 시작 전

- [ ] 두 클라이언트가 같은 Photon App ID와 Region을 사용함
- [ ] 두 클라이언트의 `_soloMode`가 꺼져 있음
- [ ] 서로 다른 플레이어로 같은 방에 입장함
- [ ] 두 클라이언트 모두 `GameSceneTest` 로드 완료
- [ ] MasterClient가 어느 쪽인지 기록

### 최소 회귀

- [ ] 양쪽 준비 완료 후 동시에 Battle 진입
- [ ] 한쪽 승리·한쪽 패배 시 `Split` 판정
- [ ] 파트너 보드 모델과 성급 표시
- [ ] 공용 상점 풀 구매·반환
- [ ] 골드 1G·5G·10G 전송과 잔액
- [ ] 유닛 전송과 FIFO 수령
- [ ] 벤치가 가득 찬 상태에서 대기열 유지
- [ ] 파트너 연결 끊김 중 전송 거부
- [ ] 재접속 후 보드와 현재 라운드 복구
- [ ] MasterClient 이탈 후 중복 결과·보상 없음

통신기 브랜치가 통합되기 전에는 통신기 항목을 “미검증”으로 남기고 현재 브랜치에서 억지로 테스트하지 않는다.

## 9. 2일 차 인계자 확인표

기존 담당자가 인수자에게 묻는다.

1. 지금 고치려는 기능의 담당 매니저는 무엇인가?
2. 입력 요청과 결과 알림 이벤트는 무엇인가?
3. 데이터 원본은 JSON인가, ScriptableObject인가?
4. 이 문제는 솔로에서도 재현되는가?
5. Photon 상태를 바꾸는 주체는 MasterClient인가, 각 플레이어인가?
6. 다른 브랜치에 이미 구현된 기능은 아닌가?
7. Unity Play와 Console 외에 어떤 회귀 항목을 확인했는가?

답을 모르면 구현을 시작하지 않고 관련 문서와 Git 이력을 먼저 확인한다.
