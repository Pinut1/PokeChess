# PokeChess 인수인계 문서 (2026-07-20, 김영욱 → 팀)

> 김영욱 조기수료에 따른 인수인계. 수료일이 **7/21 → 7/28로 변경**(7/20 확인)되어 마지막 주(7/21~7/27)에 아래 미완 항목 일부를 직접 소진할 예정 — 최종 상태는 7/27 갱신본 참조.
> 이 문서 기준 시점은 **2026-07-20, master `768adcc5`**.
> 함께 볼 문서: `CLAUDE.md`(규칙·기술부채 총괄) / `Docs/NEXT_TASKS.md`(작업 체크리스트) / `Docs/DEVLOG_2026-07-17.md`(마지막 데브로그)

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

→ **7/20 기준 열린 PR 0건, master 워킹트리 클린.**

## 3. 시스템 현황 — 완료된 것 (master 기준)

- **네트워크(PUN2)**: 2인 매칭→동시 씬 전환, 보드/골드/HP 동기화(각자 권위+시각 미러), 팀 공통 HP(Room 속성), 재접속 유예(GracePeriod, 이중 발행 가드), 마스터 교체 대응
- **공용 상점 풀**: MasterClient 권위 예약/구매/반환, revision 스냅샷 동기화 (#42)
- **전투**: 마나(초당 10 고정)/스킬 캐스팅, effectType 9종, CC(스턴·날따름), 지원스킬, role 타겟팅, 악(DARK) 시너지 첫스킬 스턴 — 메커니즘은 전부 구현, **수치 일부만 PLACEHOLDER**(CLAUDE.md 🔴 섹션)
- **상점/경제**: 레벨/XP(이벤트화, PR #39), 리롤, 이자, 유닛캡, XP 구매 UI
- **진화**: 3합체 별업, 진화의 돌(장착/해제), 통신교환 진화(핸드오버→벤치 직행)
- **증강**: Augment Table v2 확정 6종 + 3택1 오퍼 + 블로킹 UX. 상세: `Assets/_Project/Docs/AugmentSystem.md`
- **전적**: 로컬 jsonl + Supabase 업로드 + 전적창 로컬/서버 탭 (#45로 전 구간 완료). 스키마: `Docs/SCHEMA_2026-07-10_supabase-matches.sql`
- **데이터 파이프라인**: 구글 시트→JSON→임포터→SO. skillId v11 규약(CLAUDE.md 참조), 견본덱 임포트

## 4. 미완 작업 (우선순위순)

> Core/Network/전투 잔여 작업의 코드 대조 및 Claude 착수 조건은 `HANDOFF_2026-07-20_core-gaps.md`를 우선 참고.

### 코드 미구현 (별도 티켓)
- [ ] **나인이볼부스트** — 이브이 진화체 8종 순차 소환+버프 (전투 신규 메커니즘, 규모 큼)
- [ ] **재접속/후입장 보드 재동기화** — 유예/재입장 골격은 있으나 비버퍼 보드 스냅샷 복구 없음
- [ ] **통신기 정식 UI** — 일반 유닛/통신진화/골드 전송 백엔드는 완료, 현재 에디터 디버그 UI만 존재
- [ ] **보스 전용 기믹** — Stage 타입/강화 배수만 있고 전용 패턴·페이즈 없음
- [ ] **`SK_EEVEE_HERO`/`SK_PACHIRISU_HERO` 데이터 행** — 현재 TODO/코드 생성 임시 경로
- [ ] **파치리스 적 전체 2초 어그로** — 자뭉열매(#44)와 별개인 어그로 부분
- [ ] AS_BUFF·MANA_REGEN **최신 공식 + 원본 VFX Table 반영**
- [ ] 증강 선택 UI 정식화 + 배치입력 `IsChoiceBlocking` 배선 (태욱)
- [ ] 파트너 보드 미러가 캡슐만 표시 — 종/모델 해석 필요 시 PokemonData 룩업 추가

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
- [ ] 통신교환·골드 전송 2인 실테스트
- [ ] #42 병합 후 공용 풀 **2클라 회귀 테스트** (예약/구매/반환, 마스터 교체 시나리오)
- [ ] 재접속 시나리오 2인 테스트

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

## 7. 문서 지도

| 문서 | 내용 |
|------|------|
| `Docs/HANDOFF_2026-07-20_core-gaps.md` | **Core 잔여 작업 단일 기준** — 현재 코드 대조·P0/P1/P2 큐·착수 조건 |
| `CLAUDE.md` | 규칙·파트 분배·기술부채(🔴🟡🟢)·데이터 파이프라인·skillId v11 |
| `Docs/NEXT_TASKS.md` | 작업 체크리스트 (7/20 갱신) |
| `Assets/_Project/Docs/AugmentSystem.md` | 증강 시스템 상세 |
| `Docs/INVESTIGATION_2026-07-17_shared-pool-branches.md` | #40 vs #42 경쟁 브랜치 경위 |
| `Docs/DEVLOG_*.md` | 일자별 작업 기록 (최신 7/17) |
| `Docs/SCHEMA_2026-07-10_supabase-matches.sql` | 전적 서버 스키마 |
