# PokeChess 인수인계 1일 차 패키지

> 인계 예정일: 2026-07-30
> 작성 기준: 2026-07-27
> 대상: 프로젝트를 처음 여는 프로그래머
> 목적: 10분 안에 프로젝트를 실행하고, 안전하게 수정할 범위와 Supabase 권한 이전 절차를 이해한다.

## 0. 먼저 읽을 것

이 문서는 상세 설계서가 아니라 **작업 시작 순서와 사고 방지 체크리스트**다.

상세 상태는 아래 문서를 함께 본다.

- `AGENTS.md`: 담당 영역, 매니저 통신 규칙, 데이터 파이프라인, 기술 부채
- `Docs/HANDOFF_2026-07-20_final.md`: 전체 시스템과 기존 미완 작업
- `Docs/HANDOFF_2026-07-27_battle-visuals.md`: 아트·VFX·진화 모델·8행 전장 통합
- `Docs/HANDOFF_DAY2_2026-07-27.md`: 코드 흐름 추적·안전한 수정·통신기/2클라 실습
- `Docs/WEEKLY_2026-07-27.md`: 이번 주 완료·미완·다음 액션
- `Docs/SCHEMA_2026-07-10_supabase-matches.sql`: 전적 서버 테이블과 RLS
- `Assets/_Project/Docs/AugmentSystem.md`: 증강 시스템

## 1. 10분 시작 가이드

### 준비물

- Unity `6000.3.8f1`
- Git
- Git LFS
- 저장소 접근 권한
- 2인 네트워크 테스트를 할 경우 Photon 대시보드/App ID 접근 권한
- 서버 전적을 관리할 경우 Supabase 프로젝트 접근 권한

### 처음 한 번

1. 저장소를 clone한다.
2. 저장소 루트에서 `git lfs pull`을 실행한다.
3. Unity Hub에서 이 폴더를 Unity `6000.3.8f1`로 연다.
4. 패키지 임포트와 스크립트 컴파일이 끝날 때까지 기다린다.
5. Console의 빨간 오류가 0건인지 확인한다.
6. `Assets/Scenes/GameSceneTest.unity`를 연다.
7. `GameManager > NetworkManager`의 `_soloMode`가 단독 테스트 목적에 맞게 켜져 있는지 확인한다.
8. Play를 누른다.

### 정상 시작 기준

- 보드와 벤치가 생성된다.
- 유닛 상점이 표시된다.
- Level, ReRoll, 유닛/아이템 상점 전환 버튼이 작동한다.
- Console에 `[Network] 솔로 모드` 경고가 표시될 수 있다. 단독 테스트라면 정상이다.
- Console에 컴파일 오류, Missing Script, NullReferenceException이 없어야 한다.
- Supabase가 설정된 씬이면 `[Supabase] 세션 확보` 로그가 표시된다.

### Play를 종료하기 전에

- Play 중에 스크립트를 수정하지 않는다.
- Play 중 변경한 Inspector 값은 종료 후 사라질 수 있다.
- 필요한 화면은 캡처하고, 실제 저장할 값은 Edit 모드에서 다시 적용한다.

## 2. 현재 작업 기준선

| 항목 | 현재 값 |
|---|---|
| 브랜치 | `Pinut1/haein-asset-8row-integration` |
| 현재 HEAD | `aebdf2ad` |
| 기준 씬 | `Assets/Scenes/GameSceneTest.unity` |
| Unity | `6000.3.8f1` |
| 네트워크 | Photon PUN2 |
| 서버 전적 | Supabase Auth + Postgres REST |

### 인계 전 반드시 정리할 것

현재 워킹 트리에는 아직 커밋하지 않은 UI·씬 변경이 있다. 이 상태를 최종 인계 기준으로 사용하지 않는다.

- Canvas의 Level/ReRoll/상점 전환 버튼 실제 연결
- 해당 기능과 중복되던 OnGUI XP 구매·리롤 제거
- 8행 보드와 벤치 간격 조정
- Main Camera FOV 조정

인계 전 다음을 확정한다.

1. 변경 파일을 리뷰한다.
2. Unity Play와 Console을 다시 확인한다.
3. 의도한 파일만 명시적으로 stage한다.
4. 하나의 인계 기준 커밋을 만든다.
5. 원격 저장소에 push한 뒤 이 문서의 브랜치·HEAD를 갱신한다.

`git commit -a`는 사용하지 않는다. Unity가 만든 무관한 설정 변경이나 다른 작업자의 변경까지 섞일 수 있다.

## 3. 프로젝트를 이해하는 가장 짧은 경로

### 플레이 흐름

`LobbyScene` → 방 생성/입장 → `GameScene` 또는 테스트용 `GameSceneTest` → 쇼핑 → 준비 → 전투 → 결과 → 다음 라운드

### 코드 흐름

```text
Canvas 버튼
  → UIManager
  → GameEvents 요청
  → 담당 Manager가 검증·처리
  → GameEvents 결과
  → UIManager가 화면 갱신
```

### 핵심 매니저

| 영역 | 매니저 | 먼저 볼 파일 |
|---|---|---|
| 게임 전체 | `GameManager` | `Assets/_Project/Scripts/Core/GameManager.cs` |
| 이벤트 허브 | `GameEvents` | `Assets/_Project/Scripts/Core/GameEvents.cs` |
| 라운드 | `RoundPhaseManager` | `Assets/_Project/Scripts/Core/RoundPhaseManager.cs` |
| 네트워크 | `NetworkManager` | `Assets/_Project/Scripts/Network/NetworkManager.cs` |
| 보드·벤치 | `BoardManager` | `Assets/_Project/Scripts/Managers/BoardManager.cs` |
| 전투 | `BattleManager` | `Assets/_Project/Scripts/Managers/BattleManager.cs` |
| 상점·경제 | `ShopManager` | `Assets/_Project/Scripts/Managers/ShopManager.cs` |
| UI | `UIManager` | `Assets/_Project/Scripts/Managers/UIManager.cs` |
| 증강 | `AugmentManager` | `Assets/_Project/Scripts/Managers/AugmentManager.cs` |
| 전적 서버 | `SupabaseMatchUploader` | `Assets/_Project/Scripts/Network/SupabaseMatchUploader.cs` |

## 4. 절대 규칙

1. 매니저끼리 직접 참조하지 않는다. 요청과 결과는 `GameEvents`를 사용한다.
2. 새 이벤트는 `GameEvents.cs`에만 추가한다.
3. 다른 담당자의 매니저를 수정하기 전에 담당자와 변경 범위를 확인한다.
4. `Assets/Resources/Data/`의 JSON이 데이터 원본이다.
5. ScriptableObject는 임포터 산출물이므로 직접 수정하지 않는다.
6. 데이터 변경은 구글 시트 → JSON → `PokeChess/Import *` 메뉴 순서로 반영한다.
7. 진화 모델 교체는 `dca63495`에 이미 구현되어 있다. 재구현하지 않고 회귀 검증만 한다.
8. 모델 프리팹이 없는 경우에만 캡슐 폴백이 보이는 것이 정상이다.
9. Play 중 리컴파일하지 않는다.
10. 공유 씬을 수정한 뒤에는 씬 전체 diff를 확인한다.

## 5. 안전하게 시작할 작업과 위험한 작업

### 비교적 안전

- UI 문구 변경
- Canvas 버튼을 기존 `GameEvents` 요청에 연결
- Inspector의 위치·크기 미세 조정
- 기존 밸런스 값 확인
- 문서와 테스트 체크리스트 갱신

### 먼저 상의

- `GameManager`, `NetworkManager`, `BattleManager`, `BoardManager` 구조 변경
- Photon RPC, Room/Player Custom Property 변경
- 전투 좌표 또는 8행 좌표 규칙 변경
- Supabase RLS, 테이블, Auth 설정 변경
- JSON 스키마와 임포터 변경
- 프리팹·씬의 대규모 재배선
- API 키 교체 또는 비활성화

## 6. Supabase 권한 인수인계

### 현재 프로젝트가 사용하는 것

- 용도: 전적 업로드와 서버 전적 조회
- 인증: Supabase Anonymous Auth
- 데이터: `profiles`, `matches`
- 접근 방식: SDK 없이 `UnityWebRequest`로 Auth/Data API 호출
- Unity 설정 위치: `GameSceneTest > GameManager > SupabaseMatchUploader`
- 현재 씬 상태: Project URL과 legacy `anon` key가 설정되어 있음
- 로컬 원본: 전적은 먼저 로컬 jsonl에 기록되며, 서버 실패가 게임 진행을 막지 않음

### 비밀정보 분류

| 항목 | 분류 | 전달 방법 |
|---|---|---|
| Project URL | 공개 식별값 | 저장소/문서에 존재 가능 |
| legacy `anon` key | 클라이언트 공개용 | Unity 클라이언트에 포함 가능. RLS 필수 |
| `sb_publishable_...` | 신규 클라이언트 공개용 | 향후 legacy `anon` 대체 후보 |
| Database password | 비밀 | 문서·Git·단톡 금지 |
| `service_role` / `sb_secret_...` | 최고 위험 비밀 | Unity·문서·Git·메신저에 절대 넣지 않음 |
| 개인 Access Token | 개인 비밀 | 공유하지 않고 각자 발급 |
| Supabase 로그인 비밀번호 | 개인 비밀 | 계정 공유 대신 멤버 초대 사용 |

현재 저장소와 `GameSceneTest`에는 `service_role` 문자열이 없으며, 클라이언트에는 계속 넣지 않는다.

### 권장 이전 경로

#### A. Supabase 전용 Organization인 경우

1. 기존 소유자가 Supabase Dashboard에서 해당 Organization의 Team/Members 화면을 연다.
2. 인수자를 **Owner**로 초대한다.
3. 인수자가 24시간 안에 초대를 수락한다.
4. 인수자가 프로젝트, Table Editor, SQL Editor, Auth, Logs, API Keys 화면 접근을 확인한다.
5. 결제 플랜이 있으면 Billing 소유와 결제 수단도 확인한다.
6. 아래 검증 체크리스트를 완료한다.
7. 다른 Owner가 생긴 것을 확인한 뒤에만 기존 소유자가 Leave team을 수행한다.

#### B. 같은 Organization에 다른 프로젝트도 있는 경우

무료/Pro 조직에서 인수자를 Organization Owner로 초대하면 다른 프로젝트까지 접근할 수 있다. 프로젝트 단위 역할은 Team/Enterprise에서만 제공된다.

이 경우 권장 순서는 다음과 같다.

1. 인수자가 별도 Organization을 만든다.
2. 기존 소유자가 Project Settings의 프로젝트 이전 기능으로 대상 프로젝트만 새 Organization에 옮긴다.
3. 이전 전 GitHub Integration, Log Drain, 프로젝트 단위 역할 등 이전 방해 조건을 확인한다.
4. 이전 후 프로젝트 URL과 API 키가 유지되는지 확인한다.
5. Billing과 사용량 책임이 새 Organization으로 넘어갔는지 확인한다.
6. Unity Play 검증을 진행한다.

조직에 다른 프로젝트가 있는지 확인하지 않고 Owner를 초대하지 않는다.

### Supabase Dashboard 검증 체크리스트

- [ ] 인수자 본인 계정으로 로그인됨
- [ ] MFA 활성화
- [ ] 대상 프로젝트 이름과 Project Ref 확인
- [ ] `public.profiles` 테이블 조회 가능
- [ ] `public.matches` 테이블 조회 가능
- [ ] 두 테이블의 RLS가 활성 상태
- [ ] SQL Editor 접근 가능
- [ ] Auth의 Anonymous sign-ins가 활성 상태
- [ ] Auth Users에 테스트 익명 사용자가 생성되는 것 확인
- [ ] Logs에서 Auth/Data API 요청 확인 가능
- [ ] Settings > API Keys 접근 가능
- [ ] `service_role` 또는 secret key가 저장소·Unity에 들어 있지 않음

### Unity에서 최종 검증

1. `GameSceneTest`를 Play한다.
2. Console에서 `[Supabase] 세션 확보`를 확인한다.
3. 닉네임 설정 후 `profiles`에 본인 행이 생성 또는 갱신되는지 확인한다.
4. 테스트 게임을 종료한다.
5. Console에서 `[Supabase] 전적 업로드 완료`를 확인한다.
6. `matches`에 같은 `match_id`가 중복 없이 들어갔는지 확인한다.
7. 전적창의 서버 탭에서 최근 전적이 조회되는지 확인한다.
8. 서버를 잠시 사용할 수 없어도 로컬 jsonl 전적이 남는지 확인한다.

### 키와 비밀번호 처리

- 계정 비밀번호를 넘기지 않는다. Organization 초대 또는 프로젝트 이전을 사용한다.
- Database password를 과거에 공유했다면 인수 완료 후 재설정한다. Unity 클라이언트는 이 비밀번호를 사용하지 않는다.
- legacy `anon` key는 현재 코드가 사용 중이므로 인계 당일 임의로 비활성화하지 않는다.
- Supabase는 legacy `anon`/`service_role`에서 publishable/secret 키로 전환 중이다. publishable key 전환은 별도 작업으로 잡고, Auth·업로드·조회 회귀 테스트 후 legacy 키를 비활성화한다.
- 새 secret key가 필요하면 백엔드 구성요소별로 별도 생성하고, 비밀번호 관리자의 암호화 공유 기능을 사용한다.
- secret key가 노출됐다고 의심되면 새 키 발급 → 사용처 교체 → 기존 키 삭제 순서로 처리한다.

### 장애 시 먼저 볼 로그

| 로그 | 의미 | 먼저 확인할 것 |
|---|---|---|
| `URL/anon key 미설정` | Unity 설정 누락 | `SupabaseMatchUploader` Inspector |
| `익명 로그인 실패` | Auth 진입 실패 | Anonymous sign-ins, URL/key, 네트워크 |
| `프로필 upsert 실패` | `profiles` 쓰기 실패 | RLS와 `auth.uid()` |
| `전적 업로드 실패` | `matches` insert 실패 | RLS, 필수 컬럼, 중복 키 |
| `전적 조회 실패` | 서버 탭 조회 실패 | 사용자 세션, select 정책 |
| `세션 없음 — 업로드 생략` | 인증 전이거나 인증 실패 | 앞선 Auth 로그 |

### Supabase 관련 단일 기준

- 스키마와 RLS: `Docs/SCHEMA_2026-07-10_supabase-matches.sql`
- Unity 연동: `Assets/_Project/Scripts/Network/SupabaseMatchUploader.cs`
- 전적 데이터 모델: `Assets/_Project/Scripts/Data/MatchRecord.cs`

## 7. 첫날 실습 과제

인수자가 직접 수행하고, 기존 담당자는 화면을 보며 보조한다.

1. 저장소를 새 폴더에 clone하고 LFS 파일을 받는다.
2. `GameSceneTest`를 Play한다.
3. Level 버튼을 눌러 `UIManager → GameEvents → ShopManager` 흐름을 찾는다.
4. 라운드 2 증강 오퍼가 `StageData → RewardManager → AugmentManager`로 이어지는 것을 로그에서 찾는다.
5. `BoardManager`의 board/bench offset이 씬 어디에 저장되는지 찾는다.
6. Supabase Dashboard에서 본인 익명 유저와 전적 한 건을 찾는다.
7. 코드나 데이터는 바꾸지 않고 Play를 종료한다.

완료 기준은 “설명을 들었다”가 아니라 인수자가 위 경로를 직접 찾아 설명할 수 있는 것이다.

## 8. 인계 전 작성자가 채울 빈칸

- 최종 인계 브랜치: `[확정 필요]`
- 최종 인계 커밋: `[확정 필요]`
- 원격 저장소 URL: `[별도 전달 또는 접근 권한으로 확인]`
- Supabase Organization 이름: `[문서에 비밀이 아니면 기입]`
- Supabase Project 이름/Ref: `[Dashboard에서 확인]`
- 권한 이전 방식: `[Owner 초대 / 프로젝트 이전]`
- 인수자 이메일: `[문서에 기입하지 말고 초대 시 직접 입력]`
- Photon 인계 방식: `[기존 App 유지 / 신규 App ID]`
- 질문 연락 가능 기간: `[확정 필요]`
- 긴급 연락 담당: `[확정 필요]`

## 9. 1일 차 완료 조건

- [ ] 인수자가 새 환경에서 프로젝트를 실행함
- [ ] Git LFS 에셋 누락이 없음
- [ ] Console 컴파일 오류가 없음
- [ ] 핵심 규칙 10개를 설명함
- [ ] 현재 브랜치와 미커밋 변경을 구분함
- [ ] Supabase 권한 이전 경로를 결정함
- [ ] 인수자가 Supabase 프로젝트에 본인 계정으로 접근함
- [ ] 서버 전적 업로드와 조회를 검증함
- [ ] 실제 비밀번호·secret key를 문서나 Git에 남기지 않음

## 참고 링크

- Supabase Access Control: https://supabase.com/docs/guides/platform/access-control
- Supabase Project Transfers: https://supabase.com/docs/guides/platform/project-transfer
- Supabase API Keys: https://supabase.com/docs/guides/getting-started/api-keys
- Supabase API Security/RLS: https://supabase.com/docs/guides/api/securing-your-api
