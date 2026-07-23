# PokeChess 인수인계 문서 (김영욱 → 팀)

> 김영욱 조기수료에 따른 인수인계. 수료일 **7/28**. 마지막 주(7/21~7/27)에 미완 항목 일부를 직접 소진 중이라 이 문서는 계속 갱신된다.
> **기준 시점: 2026-07-21, master `aba84436`** (직전 기준은 7/20 `768adcc5`).
> 함께 볼 문서: `CLAUDE.md`(규칙·기술부채 총괄) / `Docs/HANDOFF_2026-07-20_core-gaps.md`(Core 잔여 작업 단일 기준) / `Docs/NEXT_TASKS.md`(체크리스트)

---

## 1. 7/20 병합 완료 내역

오늘 아래 PR 스택을 전부 master에 병합했다. **공용 상점 풀 관련 최신 코드는 전부 master에 있다.**

| PR | 내용 | 비고 |
|----|------|------|
| #42 | 공유 챔피언 풀(MasterClient 권위·revision 스냅샷) + Augment Table v2 확정 6종 + 블로킹 UX | 4코 상점 오픈 v2(확정 2회+4골드), 채석가 최초 보너스 포함 |
| #43 | 파트너 증강 동기화 — 전적 `partner.augments` 채움 | |
| #44 | 자뭉열매 — 파치리스 영웅증강 v2 언타겟+회복 | |
| #45 | 전적창 서버 조회(Phase 3) — 로컬/서버 탭 | 전적 시스템 전 구간 완료 |

## 2. 나머지 PR 처리 결과 (7/20 완료)

| PR | 처리 |
|----|------|
| **#40** `feat(shop): 2인 공용 상점 풀 동기화` | **닫음.** #42(MasterClient 권위 방식)와 같은 기능의 경쟁 구현(PunRPC 방식)이었고 #42 병합으로 대체됨(사유 코멘트 남김, 이견 시 reopen 가능). 경위: `Docs/INVESTIGATION_2026-07-17_shared-pool-branches.md`. **남은 정리**: `feature/shared-unit-pool-sync` 원격 브랜치 삭제(태욱 확인 후). |
| **#46** `feat: 견본덱 UI` | **리뷰 승인 후 병합.** 지적 1건(툴팁·DragWindow 중복 호출)은 PR 코멘트 참고 — 다음 정리 때 제거. 추천 장비 랜덤 배정은 임시(기획 확정 후 교체). |

→ 7/20 기준 열린 PR 0건.

## 2-1. 7/21 병합 내역 (추가)

| PR | 내용 | 담당 |
|----|------|------|
| #47 | **재접속/파트너 재입장 보드 재동기화** — 스냅샷 재교환 + revision 가드 + 라운드 복구 | 영욱 |
| #48 | **파트너 보드 미러 종/모델 해석** — speciesId→PokemonData, 종별 풀, 캡슐 폴백 유지 | 영욱 |
| #49 | 포켓몬 보이스·TFT SFX 사운드 에셋 | 해인 |
| #50 | 보드·벤치 배치 및 씬 안정화 | 영욱 |
| #51 | **임포터 trainer_entry join 진단 강화 + 데이터 결함 시 Import 중단** + EditMode 테스트 15개 | 영욱 |
| #52 | 누락 trainerId 심각도 승격(Warning→Error) + 불변식 테스트 + `.claude` 로컬설정 ignore | 영욱 |
| #53 | **밸런스 시트 대조 하네스**(`BalanceCheckHarness`) + 테스트 구조 한계 문서화 | 영욱 |

그 외 아트 직접 커밋: 상점/아이템 UI 에셋 세트(`1e2bf445`, 태욱), 스카이박스·벤치 타일·Hexlands 재질(`10f736cd`, 해인).

→ **7/21 기준 열린 PR 0건.** 워킹트리에는 Unity가 자동 생성하는 `ProjectSettings/*`·`Assets/Settings/*` 변경이 상시 떠 있으나 실내용 변경이 아니므로 커밋하지 않는다.

## 3. 시스템 현황 — 완료된 것 (master 기준)

- **네트워크(PUN2)**: 2인 매칭→동시 씬 전환, 보드/골드/HP 동기화(각자 권위+시각 미러), 팀 공통 HP(Room 속성), 재접속 유예(GracePeriod, 이중 발행 가드), 마스터 교체 대응
- **공용 상점 풀**: MasterClient 권위 예약/구매/반환, revision 스냅샷 동기화 (#42)
- **전투**: 마나(초당 10 고정)/스킬 캐스팅, effectType 9종, CC(스턴·날따름), 지원스킬, role 타겟팅, 악(DARK) 시너지 첫스킬 스턴 — 메커니즘은 전부 구현, **수치 일부만 PLACEHOLDER**(CLAUDE.md 🔴 섹션)
- **상점/경제**: 레벨/XP(이벤트화, PR #39), 리롤, 이자, 유닛캡, XP 구매 UI
- **진화**: 3합체 별업, 진화의 돌(장착/해제), 통신교환 진화(현재 모델A 핸드오버 — ⚠️ **최종 기획과 불일치, §4 재작업 항목 참조**)
- **증강**: Augment Table v2 확정 6종 + 3택1 오퍼 + 블로킹 UX. 상세: `Assets/_Project/Docs/AugmentSystem.md`
- **전적**: 로컬 jsonl + Supabase 업로드 + 전적창 로컬/서버 탭 (#45로 전 구간 완료). 스키마: `Docs/SCHEMA_2026-07-10_supabase-matches.sql`
- **데이터 파이프라인**: 구글 시트→JSON→임포터→SO. skillId v11 규약(CLAUDE.md 참조), 견본덱 임포트
- **임포터 데이터 검증**(7/21, #51·#52): stage_data ↔ trainer_entry join 시 중복/누락 trainerId, 적 0마리 스테이지를 검출해 **Import를 중단**하고 `StageDatabase.asset`을 보존한다. 미참조 trainerId는 경고. 진단 로직은 Unity 비의존 `TrainerEntryDiagnostics`로 분리돼 EditMode 테스트로 검증된다
  - "적 0마리 = 항상 결함"의 근거는 `StageType` 4종이 **전부 전투**라는 것. 비전투 스테이지 타입을 추가하면 이 전제를 재검토할 것
  - ⚠️ stage_data의 인라인 `enemies`가 현재 전 스테이지 0마리라 **"전환기 폴백" 경로는 사실상 죽은 코드**다. 제거 시점은 기획/데이터 담당과 합의 필요

## 4. 미완 작업 (우선순위순)

> Core/Network/전투 잔여 작업의 코드 대조 및 Claude 착수 조건은 `HANDOFF_2026-07-20_core-gaps.md`를 우선 참고.

### 코드 미구현 (별도 티켓)
- [~] **나인이볼부스트** — 증강(`HERO_EEVEE`)은 **확정·구현·동작**(이브이 지급+진화잠금+마법사전환+×1.4+3성 봇소환). 미구현은 **v2 풀 연출뿐**: 진화체 8종 순차 소환+종별 버프+돌 면역+돌연변이 시너지 전환+"가장 강한 이브이" 대상 (전투 신규 메커니즘, 규모 큼). "스코프 아웃" 아님 — 증강 자체는 게임에서 동작함
- [~] 🔺 **통신교환 재설계 — 기획 확정(7/22 황해인) + CODEX 구현 동결** → 브랜치 `feature/trade-evolution-redesign`(9032116b, master 미병합). 확정 방향=즉시진화+수신자 광역변환(필드·대기석·상점 즉시·영구, 실시간 단방향 전송, 발동 성급 무제한, 경제 원본기준, 재접속 복구). CODEX가 2~5단계 구현(컴파일 0에러). **잔여**: 네트워크 발동 연결·재접속 복구·2클라 테스트 + 병합 전 확인 2건(진화체 스탯 성급기준, 매핑 9→7 삭제 검증). 단일 기준: `NEXT_TASKS.md` 🔺 + `INVESTIGATION_2026-07-22_trade-evolution-redesign.md`
- [ ] **통신기 정식 UI** — 골드 전송 백엔드 완료(지금 UI 가능). 유닛 통신진화 UI는 위 브랜치가 master 병합된 뒤 착수(기획은 확정). 현재 에디터 디버그 UI만 존재
- [x] ~~보스 전용 기믹~~ — **스코프 아웃(7/21, 7/22 데이터 재확인)**: `StageType`에 Boss 타입 없음(최종=`ChampionBattle`). `CHAMP_R5`=망나뇽 3성 `statMultiplier 2.0`+`hpMultiplier 1.3`+서포팅 4종, 페이즈·패턴 없이 스탯 배수가 스펙. ⚠️ **현 `stage_data`는 "1-5 슬라이스" 한정** — 정식 다챕터 확장 시 재검토 여지
- [ ] **`SK_EEVEE_HERO`/`SK_PACHIRISU_HERO` 데이터 행** — `skill_table.json`에 **SK_ 행 0건(기획 미작성)**. 영웅증강은 현재 SK_ 없이 코드로 동작 → 시트 반영 후 스킬화 대기
- [ ] **파치리스 적 전체 2초 어그로** — 자뭉열매(#44)와 별개인 어그로 부분
- [ ] AS_BUFF·MANA_REGEN **최신 공식 + 원본 VFX Table 반영**
- [ ] 증강 선택 UI 정식화 + 배치입력 `IsChoiceBlocking` 배선 (태욱)

### 7/21 완료로 정정 — ⚠️ 아래 2건은 이미 구현됐다. 중복 착수 금지

- [x] ~~**재접속/후입장 보드 재동기화**~~ — **완료(PR #47)**. 스냅샷 재교환 + revision 가드 + 라운드 복구.
- [x] ~~**파트너 보드 미러가 캡슐만 표시**~~ — **완료(PR #48)**. speciesId→PokemonData 해석, 종별 풀링, 캡슐 폴백은 유지.

> 단, **두 건 다 실제 2클라이언트 Photon 검증은 미수행**이다. 코드·리뷰만 끝난 상태이므로 아래 "테스트 미완"에 남아 있다. 상세 조건은 `HANDOFF_2026-07-20_core-gaps.md` 참조.
>
> #48은 성급별 스케일(`0.6+(star-1)*0.15`)을 모델 루트에 덮어쓰는 방식이라 실제 보드의 성급 스케일링과 시각적으로 다를 수 있다 — 실사 확인 필요.

### 완료로 정정된 항목
- [x] 일반 유닛 전송 백엔드 — 통신교환 모델A에서 매핑 없는 종은 같은 종·성급으로 파트너 벤치에 전달
- [x] 적 유닛 아이템 효과 — `heldItemEn` 해석 후 stat/conditional/CC 면역 훅 부착(`6d447163`)

### 기획 회신/확정 대기 (해인님)
- [ ] AsBuff "증가량"·ManaRegen "회복량" 해석 — %(p)인지 즉시 마나인지
- [ ] `AS_BUFF_DURATION` 값
- [ ] 탱커 4종(Water/Poison/Ground/Cheer) 포켓몬별 스킬 세분
- [ ] 밸런스 테이블: `ShopManager`의 `_requiredXpByLevel` `_unitCapByLevel` `_roundXpReward` `_buyXpCostGold` `_buyXpAmount`
- [ ] 기타 PLACEHOLDER 수치 전체 목록: **CLAUDE.md "기술 부채 추적" 🔴 섹션이 단일 진실**

### 테스트 미완

> **다음 실사 테스트 세션의 우선순위.** 아래는 전부 실제 2클라이언트 Photon 환경이 필요해 자동화가 안 되는 항목이다.

- [ ] **재접속 시나리오 2인 테스트** (PR #47) — 강제 끊김→재접속, 끊긴 동안 라운드 진행, revision 역전
- [ ] **파트너 보드 미러 실사 확인** (PR #48) — 종/모델이 맞게 뜨는지, 성급별 스케일이 실제 보드와 일치하는지
- [ ] #42 병합 후 공용 풀 **2클라 회귀 테스트** (예약/구매/반환, 마스터 교체 시나리오)
- [ ] 통신교환·골드 전송 2인 실테스트

### 테스트 수단 (7/21 추가)

**① 자동 테스트 — EditMode 16개**
`PokeChess.EditorTests` 어셈블리. Test Runner에서 실행하거나 Unity MCP `run_tests`로 돌린다.
대상은 `TrainerEntryDiagnostics`(임포터 데이터 검증) 전부 + 기존 씬 안정성 3개.
⚠️ 전투/상점/네트워크 로직에는 **아직 자동 테스트를 붙일 수 없다** — 이유와 해결법은 `CLAUDE.md` 기술 부채의 🔺 항목 참조.

**② 밸런스 시트 대조 하네스 — `Scripts/Debug/BalanceCheckHarness.cs`**

> ⚠️ **씬에 미포함(의도적)**. 다른 디버그 하네스(`TradeDebugTest` 등)는 `GameSceneTest`의 `GameManager`에 붙어 있지만, 이건 **일부러 안 붙여뒀다** — 씬 파일은 머지 충돌 시 복구가 어려워 공유 씬 변경을 최소화하려는 것.
>
> **쓰는 법**: `GameSceneTest` 열고 `GameManager` 오브젝트에 `BalanceCheckHarness` 컴포넌트를 Add → Play → 화면 **우측 상단** "공식 대조 실행" 버튼. 확인 끝나면 **씬 저장하지 말고** 컴포넌트를 제거할 것.
- 밸런스 시트(`PokeChess_Balance_Tool.xlsx` 1v1 시뮬레이터)의 기대값과 **실제 `BattleManager.Mitigation` 계산을 대조**해 PASS/FAIL 표시. 공식을 하네스에 복사하지 않고 실제 함수를 호출하므로, 코드가 시트와 어긋나면 여기서 잡힌다
  - **PASS/FAIL 대상**: 경감계수, 평타 DPS (둘 다 시트에 기대값이 있는 항목)
  - **미검증**: `CritFactor`는 시트 1v1·Data·상수 탭 어디에도 크리 항목이 없어(2026-07-14 기준) 대조 불가 — 실측 섹션에 값만 표시한다. 시트에 크리 컬럼이 생기면 그때 `Check()`를 추가할 것
- 현재 보드의 **시너지 활성 단계**(`SynergyManager.GetActiveSynergies()`)
- 전투 중 **유닛 실측 스탯**(`BattleManager.Units`) — 시너지/아이템 버프가 실제로 반영됐는지 확인
- 기준 케이스: 꼬부기(HP950/ATK25/DEF45/AS0.50) vs 파이리(HP650/ATK30/DEF30/AS0.40). 7/21 검증 시 4항목 전부 PASS
- 범위 밖: 스킬 DPS는 마나 충전·시전 주기가 얽혀 있어 미검증(시트 값을 참고치로만 표시)
- 밸런스 수치를 바꾸면 **시트와 `Cases` 배열을 함께 갱신**할 것 — 안 그러면 FAIL이 뜬다

## 5. 운영 정보 (계정/인프라)

- **Photon PUN2**: App ID는 `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset`. 대시보드 계정 정보는 별도 전달(문서에 기재하지 않음)
- **Supabase**(전적 서버): URL/anon key는 `SupabaseMatchUploader` 인스펙터 값(anon key는 공개 전제, RLS가 방어선 — service_role 등 비밀키는 저장소에 없음). 계정 정보 별도 전달

### 계정 이관 절차

**Supabase — 조직 초대 방식** (URL/anon key 유지 → 코드·씬 수정 불필요)
1. supabase.com/dashboard → 해당 Organization → Settings > Team(Members)
2. 태욱 이메일을 **Owner** 역할로 초대(무료 플랜 가능) → 수락하면 완료. 이후 영욱은 org 탈퇴해도 프로젝트 유지됨
3. DB 비밀번호는 태욱이 Settings > Database에서 **리셋** 권장(클라이언트는 안 쓰므로 게임에 영향 없음)
4. Auth 설정의 **Anonymous sign-ins 활성화** 상태 유지할 것 — 꺼지면 전적 업로드가 로컬 기록만 남김

**Photon — 신규 App ID 발급 방식** (앱 계정 간 이전은 셀프서비스 미지원)
1. 태욱 본인 계정으로 dashboard.photonengine.com 가입 → Create App > **Photon PUN** (무료 20 CCU, 2인 게임 충분)
2. 새 App ID를 `PhotonServerSettings.asset`의 App Id PUN 칸에 입력 후 커밋 — 방은 App ID별 격리라 전환 즉시 분리됨
3. 기존 App ID는 방치해도 무방

> 두 방식 모두 **비밀번호 전달이 불필요**한 경로. 부득이 비밀번호를 넘길 경우 저장소/단톡 금지, 대면 전달.
- **데이터 원본**: 구글 시트(기획 소유). SO는 임포터 산출물이므로 직접 수정 금지
- **GDD**: 로컬 docx로만 존재 — 사본 공유 필요

## 6. 개발 환경 / 테스트 방법

- 2인 테스트: `_soloMode` OFF, 두 에디터(Main/Clone 또는 빌드) 실행
- 단일 테스트: `GameSceneTest` 씬 직접 실행 + `_soloMode` ON. `GameSceneBootstrap` 오브젝트 필수, GameManager에 `PhotonView` 필요
- **Play 중 리컴파일 금지** (7/17 교훈 — 상태 꼬임)
- 핵심 규칙: 매니저 직접 참조 금지, `GameEvents` 경유. 새 이벤트는 `GameEvents.cs`에만

### 코드 리뷰 수단 (7/21 기준)

- **CodeRabbit 무료 플랜이 소진됐다.** PR #53에서 `failure | Review rate limited`로 확인. 이후 PR에는 자동 리뷰가 붙지 않으며, **체크가 빨간불이어도 사유가 쿼터면 코드 문제가 아니다** — 병합 전 사유를 반드시 확인할 것
- 대체 수단: codex 리뷰(#52·#53에서 사용). 플랜 갱신 여부는 팀에서 결정 필요

### 워킹트리 주의

Unity를 열면 `ProjectSettings/EditorSettings.asset`, `GraphicsSettings.asset`, `ProjectSettings.asset`, `UnityConnectSettings.asset`, `Assets/Settings/PC_RPAsset.asset`, `UniversalRenderPipelineGlobalSettings.asset` 6개가 항상 수정됨으로 뜬다. **실내용 변경이 아니므로 커밋하지 말 것.** 커밋 시 `git add`로 파일을 명시하고 `git commit -a`는 쓰지 않는다.

## 7. 문서 지도

| 문서 | 내용 |
|------|------|
| `Docs/HANDOFF_2026-07-20_core-gaps.md` | **Core 잔여 작업 단일 기준** — 현재 코드 대조·P0/P1/P2 큐·착수 조건 |
| `CLAUDE.md` | 규칙·파트 분배·기술부채(🔴🟡🟢)·데이터 파이프라인·skillId v11 |
| `Docs/NEXT_TASKS.md` | 작업 체크리스트 (7/20 갱신) |
| `Assets/_Project/Docs/AugmentSystem.md` | 증강 시스템 상세 |
| `Docs/INVESTIGATION_2026-07-17_shared-pool-branches.md` | #40 vs #42 경쟁 브랜치 경위 |
| `Docs/DEVLOG_*.md` | 일자별 작업 기록 (최신 **7/21**) |
| `Docs/SCHEMA_2026-07-10_supabase-matches.sql` | 전적 서버 스키마 |
| `Docs/trainer-entry-pipeline.md` | stage_data ↔ trainer_entry join 구조 |
| `Docs/damage-formula.md` | 데미지 파이프라인(효과→크리→경감) — 밸런스 하네스와 함께 볼 것 |
| `Docs/MENTORING_QUESTIONS_2026-07-21.md` | 현업 멘토링 질문지 |
| `Docs/STUDY_PLAN_2026-07-21.md` | 개인 학습 계획 |

## 8. 인계 시 놓치기 쉬운 것

1. **완료된 작업을 다시 하지 말 것** — 이 문서 4절의 "7/21 완료로 정정" 항목(재접속 재동기화, 파트너 보드 모델)과 `core-gaps.md`를 먼저 볼 것. 7/20판 문서에는 이 둘이 미완으로 적혀 있었다
2. **SO 직접 수정 금지** — 임포터 산출물이다. 데이터는 구글 시트 → JSON → `PokeChess/Import *` 메뉴 경로로만 바꾼다
3. **밸런스 수치를 바꾸면** 시트와 `BalanceCheckHarness`의 `Cases` 배열을 함께 갱신 (안 하면 하네스가 FAIL)
4. **전투/상점/네트워크에는 자동 테스트를 붙일 수 없다** — 구조적 이유와 해결법은 `CLAUDE.md` 기술 부채 🔺. asmdef 분리는 Photon 참조를 전부 다시 걸어야 해서 **여유 있을 때만** 착수
5. **GDD가 로컬 docx로만 존재** — 사본 공유가 아직 안 됐다면 최우선으로 받을 것
