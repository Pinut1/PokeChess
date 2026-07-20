# Core 잔여 작업 인계 — 2026-07-20

> 기준 브랜치: `master` / 기준 커밋: `4abd156f`
>
> 이 문서는 과거 체크리스트가 아니라 **현재 코드 대조 결과**다. 구현 여부 판단은 이 문서와 실제 코드를 우선한다.

## 결론

기존 잔여 목록 8개 중 아래 2개는 이미 구현돼 있다.

- **일반 유닛 전송 백엔드**: `NetworkManager.SendTradeUnit`가 매핑 없는 유닛도 같은 종·성급으로 파트너 벤치에 전달한다. 요청→상대 벤치 검증→스폰→ACK→송신 측 제거의 원자 트랜잭션 구조다.
- **적 유닛 아이템 효과**: `EnemyPlacement.heldItemEn`을 `ItemDatabase`로 해석해 `ItemStatEffect`, `ItemConditionalEffect`, CC 면역을 적 `BattleUnit`에도 부착한다. 커밋 `6d447163`에서 완료됐다.

따라서 새로 구현할 기능으로 다시 잡지 않는다. 다만 통신교환·골드 전송은 실제 2클라이언트 회귀 테스트가 남아 있다.

## Claude 작업 큐

### P0 — 코드만으로 착수 가능

#### 1. 재접속/후입장 보드 재동기화

현재 상태:

- `ReconnectAndRejoin`, 60초 유예, 페이즈 타이머 중단/재개, 기존 매치 씬 재로드 방지는 구현돼 있다.
- 보드 스냅샷은 `RpcTarget.Others` 비버퍼 RPC이며, 보드 변경으로 dirty가 생길 때만 전송된다.
- 재접속 직후 상대 보드 스냅샷을 요청하거나 강제로 재송출하는 경로가 없다.

완료 조건:

- 재접속 성공 시 양쪽이 현재 보드+벤치 스냅샷을 다시 교환한다.
- 재접속 중 발생한 라운드/페이즈/준비 상태를 현재 Room/Player CustomProperties에서 복구한다.
- 중복 스냅샷과 순서 역전으로 과거 상태가 덮어쓰이지 않도록 revision 또는 명확한 최신성 규칙을 둔다.
- 일반 최초 입장과 재입장을 구분해 기존 매치를 다시 시작하거나 씬을 재로드하지 않는다.
- PUN 미설치 오프라인 스텁도 public API가 깨지지 않는다.

관련 파일:

- `Assets/_Project/Scripts/Network/NetworkManager.cs`
- `Assets/_Project/Scripts/Network/BoardSyncBroadcaster.cs`
- `Assets/_Project/Scripts/Network/BoardSnapshot.cs`
- `Assets/_Project/Scripts/Core/RoundPhaseManager.cs`

#### 2. 파트너 보드 종/모델 해석

현재 상태:

- `BoardSnapshot.Entry.speciesId`는 이미 전송된다.
- `OpponentBoardView`는 speciesId를 오브젝트 이름에만 쓰고 모든 유닛을 Capsule primitive로 표시한다.

완료 조건:

- speciesId로 `PokemonDatabase`/`PokemonData`를 해석한다.
- 프로젝트의 기존 유닛 비주얼 생성 경로를 재사용해 종별 모델 또는 사용 가능한 대표 비주얼을 표시한다.
- 모델이 없거나 ID가 잘못된 경우 캡슐 폴백을 유지하고 경고는 스팸되지 않게 한다.
- 미러 오브젝트는 읽기 전용이며 드래그/보드 레이캐스트에 개입하지 않는다.
- 풀링 또는 동등한 재사용 구조를 유지한다.

관련 파일:

- `Assets/_Project/Scripts/Network/OpponentBoardView.cs`
- `Assets/_Project/Scripts/Network/BoardSnapshot.cs`
- `Assets/_Project/Scripts/Core/UnitFactory.cs`
- `Assets/_Project/Scripts/Data/PokemonDatabase.cs`

### P1 — UI 담당 협의 후 착수

#### 3. 통신기 정식 UI 트리거

현재 상태:

- 유닛 전송과 골드 전송 백엔드는 구현돼 있다.
- 호출자는 `TradeDebugTest`, `GoldTransferDebugTest`뿐이며 둘 다 `UNITY_EDITOR` 전용이다.

필요 작업:

- 태욱(UI 담당)과 화면 위치·선택 UX·모달/확인 동작을 먼저 합의한다.
- 벤치 유닛 선택→파트너 전송, 골드 수량 선택→송금 UI를 정식 HUD에 연결한다.
- `GameEvents.OnTradeUnitReceived`, `OnTradeRejected`, `OnGoldTransferCompleted`, `OnGoldTransferRejected`, `OnPartnerGoldReceived`로 결과를 표시한다.
- 전송 처리 중 재입력 방지, 상대 오프라인/벤치 가득/골드 부족 사유를 사용자에게 보여준다.
- 디버그 스크립트는 검증용으로 남기되 플레이어 UI의 의존성으로 쓰지 않는다.

### P1 — 기획 스펙 확정 후 착수

#### 4. 나인이볼부스트 정식 스펙

현재 축소 구현:

- 이브이 지급, 진화 잠금, 마법사 역할 전환, 스탯 ×1.4
- 3성 이브이가 있으면 Espeon/Umbreon/Glaceon/Sylveon 봇 4마리 소환

남은 요구:

- 진화체 8종 소환과 종별 버프 전달
- `SK_EEVEE_HERO` 정식 스킬화
- 진화의 돌 면역
- 돌연변이 시너지에서 고유 시너지로 전환
- 보유 이브이 중 "가장 강한 이브이" 1마리 선정

착수 전 반드시 받을 값:

- 8종별 버프의 정확한 효과/수치/지속시간/중첩 규칙
- 8종 동시 소환인지 순차 소환인지, 소환 타이밍과 배치 실패 처리
- "가장 강한" 비교 우선순위(성급, 전투력, 아이템, 배치 여부 동률 처리)
- 고유 시너지 ID와 활성 조건
- `SK_EEVEE_HERO`의 manaCost, effectType, targetType, VFX ID

관련 파일:

- `Assets/_Project/Scripts/Augments/Implementations/HeroEeveeAugment.cs`
- `Assets/_Project/Scripts/Core/PokemonUnit.cs`
- `Assets/_Project/Scripts/Managers/BattleManager.cs`
- `Assets/_Project/Docs/AugmentSystem.md`

#### 5. 보스 전용 기믹

현재 상태:

- `StageType.ChampionBattle`과 적별 `statMultiplier`, `hpMultiplier`, `atkMultiplier`는 있다.
- 런타임은 `stageType`에 따라 분기하지 않으며 일반 적과 같은 스킬/행동 루프를 사용한다.

착수 전 반드시 받을 값:

- 최소 검증 보스 1종의 패턴 목록, 발동 조건, 쿨다운, 대상 규칙
- 페이즈 전환 HP 구간과 페이즈별 변화
- 협동 요소가 두 플레이어 전투에 어떻게 걸리는지
- 소환물 데이터, 전조/VFX, 승패 예외 규칙

권장 구현 방향:

- 보스마다 하드코딩하기보다 Stage/Trainer 데이터가 참조하는 기믹 ID와 런타임 패턴 실행기를 둔다.
- 기존 `BattleUnit` 효과/스킬 루프를 재사용하고 보스 전용 상태만 별도 컴포넌트 또는 전략 객체에 둔다.
- 첫 티켓은 보스 1종 세로 슬라이스로 제한한다.

### P2 — 데이터 원본과 함께 처리

#### 6. `SK_` 영웅증강 스킬행

현재 상태:

- 저장소의 `SK_EEVEE_HERO`, `SK_PACHIRISU_HERO`는 TODO 언급뿐이다.
- 파치리스 날따름은 코드에서 `PokemonSkillData`를 즉석 생성하는 임시 경로다.

완료 조건:

- 구글 시트 원본 `skill_table`에 두 행을 추가하고 JSON→Importer→SO 경로로 베이킹한다.
- 영웅증강이 하드코딩된 임시 스킬이 아니라 정식 데이터 행을 참조한다.
- 기존 일반 스킬로 표현할 수 없는 효과는 최소 범위의 effectType/전투 훅으로 확장한다.
- SO를 직접 수정하지 않는다.

## 수정하지 말아야 할 완료 기능

- 일반 유닛 전송을 별도 네트워크 시스템으로 중복 구현하지 않는다. 현재 통신교환의 매핑 없음 폴백이 일반 전송이다.
- 적 아이템 효과를 다시 연결하지 않는다. `CreateEnemyUnit`에 이미 양쪽 효과 훅이 있다.
- 재접속 작업 중 공유 상점 풀의 MasterClient 권위/revision 프로토콜을 교체하지 않는다.

## 공통 구현 규칙

- 매니저 간 통신은 `GameEvents`를 사용하고 새 이벤트는 `GameEvents.cs`에만 추가한다.
- Core/Network/전투/보드와 UI의 경계를 넘는 작업은 담당자 확인 후 진행한다.
- 데이터 원본은 구글 시트이며 ScriptableObject는 임포터 산출물이다.
- 각 티켓은 솔로 모드 회귀와 `PHOTON_UNITY_NETWORKING` 양쪽 컴파일 경로를 확인한다.

