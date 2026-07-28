# PokeChess Supabase 전적 시스템 인수인계

> 작성일: 2026-07-28  
> 대상: Unity 전적 기록 및 Supabase 운영 인수자  
> 목적: Unity에서 생성된 전적이 어떤 인증과 데이터 구조를 거쳐 Supabase에 저장되는지 빠르게 파악한다.

## 1. 한눈에 보는 구조

```text
게임 진행
  → MatchRecorder가 라운드·스테이지·보드·증강 상태 추적
  → 게임 종료 시 MatchRecord 생성
  → MatchHistoryStore가 로컬 JSONL에 우선 저장
  → GameEvents.OnMatchRecorded 발행
  → SupabaseMatchUploader가 matches에 업로드
```

Supabase 장애나 인증 실패가 발생해도 게임 진행은 막지 않는다. 로컬 JSONL이 우선 기록이며, Supabase의 `matches`는 서버 사본으로 취급한다.

## 2. 관련 파일

| 역할 | 파일 |
|---|---|
| Supabase 인증·업로드·조회 | `Assets/_Project/Scripts/Network/SupabaseMatchUploader.cs` |
| 게임 종료 전적 생성 | `Assets/_Project/Scripts/Managers/MatchRecorder.cs` |
| 전적 데이터 모델 | `Assets/_Project/Scripts/Data/MatchRecord.cs` |
| 로컬 JSONL 저장·조회 | `Assets/_Project/Scripts/Core/MatchHistoryStore.cs` |
| 전적 관련 이벤트 | `Assets/_Project/Scripts/Core/GameEvents.cs` |
| DB 테이블·인덱스·RLS | `Docs/SCHEMA_2026-07-10_supabase-matches.sql` |

Unity 씬에서는 다음 위치에 설정되어 있다.

```text
GameSceneTest
  └─ GameManager
      └─ SupabaseMatchUploader
```

`SupabaseMatchUploader` Inspector에 Project URL과 클라이언트용 `anon` key가 입력되어 있어야 한다.

## 3. Unity와 Supabase 연결 방식

Supabase Unity SDK는 사용하지 않는다. `SupabaseMatchUploader`가 `UnityWebRequest`를 사용해 다음 API를 직접 호출한다.

- Supabase Auth API: 익명 가입과 세션 갱신
- PostgREST API: `profiles` upsert
- PostgREST API: `matches` insert 및 최근 전적 select

`anon` key는 Unity 클라이언트에 포함되는 공개용 키다. 데이터 보호는 `service_role` 키가 아니라 RLS 정책으로 처리한다.

다음 정보는 Unity, 저장소, 문서 또는 메신저에 넣지 않는다.

- Database password
- `service_role` key
- `sb_secret_...` key
- 개인 Access Token
- Supabase 계정 비밀번호

## 4. 로그인 처리

현재 인증 방식은 **Supabase Anonymous Auth**다. 이메일·비밀번호 로그인은 구현되어 있지 않다.

### 최초 실행

1. `SupabaseMatchUploader.Start()`가 Project URL과 `anon` key를 확인한다.
2. 저장된 refresh token이 없으면 `/auth/v1/signup`에 빈 JSON `{}`을 전송한다.
3. Supabase가 익명 사용자와 세션을 생성한다.
4. 받은 `access_token`과 사용자 UUID는 메모리에 보관한다.
5. `refresh_token`은 Unity `PlayerPrefs`의 `supabase_refresh_token`에 저장한다.

### 재실행

1. `PlayerPrefs`에서 refresh token을 읽는다.
2. `/auth/v1/token?grant_type=refresh_token`으로 세션을 갱신한다.
3. 갱신에 실패하면 새로운 익명 사용자를 생성한다.

같은 기기에서 PlayerPrefs가 유지되면 같은 익명 사용자로 이어진다. PlayerPrefs나 앱 데이터가 삭제되면 새 사용자 UUID가 생성될 수 있다.

### 닉네임

닉네임은 Supabase Auth 계정에 저장하지 않고 `public.profiles`에 저장한다.

```text
profiles.id       = auth.users.id
profiles.nickname = 게임에서 사용하는 닉네임
```

세션 확보 시 한 번 upsert하고, 게임 종료 전적을 올릴 때 실제 플레이에 사용한 닉네임으로 다시 동기화한다.

## 5. 게임 종료와 업로드 시점

`MatchRecorder`는 게임 중 다음 이벤트를 구독해 상태를 추적한다.

- 라운드 변경
- 스테이지 진입
- 플레이어 레벨 변경
- 파트너 보드 변경
- 본인 증강 선택
- 파트너 증강 변경
- 게임 클리어
- 세션 종료

종료 판정은 다음과 같다.

| 종료 이벤트 | `result` | `end_reason` |
|---|---|---|
| `OnGameCleared` | `Victory` | `GameCleared` |
| `OnSessionEnded` | `Defeat` | `SessionEndReason` 값 |

종료 시 `MatchRecord`를 만든 뒤 로컬 JSONL 저장을 먼저 시도한다. 로컬 저장에 성공해야 `GameEvents.OnMatchRecorded`가 발행되고 Supabase 업로드가 시작된다.

Supabase 세션이 없거나 서버 업로드에 실패하면 로그만 남기고 로컬 전적은 유지한다.

## 6. `matches` 저장 구조

`matches`의 한 행은 한 게임 전체가 아니라 **한 사용자의 자기 관점 전적 한 건**이다.

2인 협동에서는 두 클라이언트가 각각 한 행씩 올린다. 두 행은 같은 `match_id`를 사용해 한 판으로 묶는다.

```text
협동 한 판
  ├─ 플레이어 A의 matches 행: self=A, partner=B
  └─ 플레이어 B의 matches 행: self=B, partner=A
```

`unique (user_id, match_id)` 제약으로 같은 사용자의 중복 업로드를 막는다.

### 일반 컬럼

| 컬럼 | 내용 |
|---|---|
| `user_id` | 업로드한 Supabase 익명 사용자 UUID |
| `schema_version` | `MatchRecord` 스키마 버전 |
| `match_id` | 같은 판을 식별하는 공통 ID |
| `game_version` | Unity `Application.version` |
| `started_at` | 게임 시작 UTC |
| `ended_at` | 게임 종료 UTC |
| `duration_seconds` | 플레이 시간 |
| `result` | `Victory` 또는 `Defeat` |
| `end_reason` | 종료 원인 |
| `final_round` | 마지막 라운드 |
| `final_stage_id` | 마지막 스테이지 ID |
| `created_at` | 서버 행 생성 시각 |

### `self_record` JSONB

본인의 최종 상태를 저장한다.

- 닉네임
- MasterClient 여부
- 최종 레벨
- 최종 보드의 포켓몬
- 각 포켓몬의 성급
- 장착 아이템
- 진화의 돌
- 돌 진화 여부
- 활성 시너지
- 선택한 증강

### `partner_record` JSONB

파트너의 마지막 네트워크 미러 상태를 저장한다.

- 닉네임
- MasterClient 여부
- 최종 보드의 포켓몬과 성급
- 파트너 증강

현재 파트너 보드 스냅샷에는 모든 정보가 전송되지 않으므로 다음 값은 불완전하거나 비어 있다.

- 파트너 레벨
- 파트너 장착 아이템
- 파트너 진화의 돌
- 파트너 활성 시너지

솔로 모드에서는 `partner_record`가 `null`이거나 빈 정보로 저장될 수 있다.

## 7. 현재 저장되는 전적과 저장되지 않는 전적

### 현재 저장됨

- 최종 승패
- 종료 원인
- 플레이 시간
- 최종 라운드와 스테이지
- 본인 최종 덱과 성급
- 본인 아이템과 진화의 돌
- 본인 활성 시너지
- 본인·파트너 증강
- 제한적인 파트너 최종 보드

### 현재 저장되지 않음

- 라운드별 승패
- 라운드별 보드와 덱 변화
- 구매·판매·리롤 기록
- 골드·XP 변화
- 전투별 피해량
- 유닛별 전투 통계
- 통신교환과 골드 전송 내역
- 파트너의 완전한 아이템·시너지 정보

## 8. 향후 테이블 확장 기준

현재 확정된 설계는 `matches`에 **게임 종료 시점의 전적 요약**을 저장하는 것이다.

보드·시너지·증강은 구조 변경 가능성이 높아 `self_record`와 `partner_record` JSONB로 유지한다. 단순 필드가 조금 추가되는 정도라면 `MatchRecord.schemaVersion`을 올리고 `matches` 스키마를 확장할 수 있다.

라운드별 상태나 행동 로그처럼 한 판에 여러 건이 생기는 데이터는 `matches` 한 행에 계속 누적하지 않는 것을 권장한다. 필요해지면 다음과 같은 하위 테이블을 별도로 설계한다.

- `match_rounds`: 라운드별 결과·보드·경제 상태
- `match_events`: 구매·판매·리롤·교환 등의 시간순 이벤트
- `match_unit_stats`: 유닛별 피해량과 전투 통계

별도 테이블을 만들 경우 `match_id`와 사용자 또는 `matches.id`를 외래키로 연결한다. 아직 이 상세 로그 구조는 기획 및 스키마가 확정되지 않았다.

## 9. RLS 정책

두 테이블 모두 RLS가 활성화되어야 한다.

### `profiles`

- 로그인한 사용자는 전체 닉네임 조회 가능
- 본인 UUID의 프로필만 생성·수정 가능

### `matches`

- 본인 `user_id`의 전적만 조회 가능
- 본인 `user_id`의 전적만 insert 가능
- 클라이언트 update/delete 정책 없음

전적은 감사 로그 성격으로 취급한다. 잘못 저장된 행은 Unity 클라이언트가 수정하거나 삭제하지 않고 서버 관리자가 처리한다.

## 10. 인수 후 검증 절차

### Supabase Dashboard

- [ ] 인수자 본인 계정으로 프로젝트에 접근할 수 있다.
- [ ] `public.profiles`와 `public.matches`를 조회할 수 있다.
- [ ] 두 테이블의 RLS가 활성화되어 있다.
- [ ] Auth의 Anonymous sign-ins가 활성화되어 있다.
- [ ] Auth Users에 테스트 익명 사용자가 생성되는지 확인한다.
- [ ] API 및 Auth 로그를 확인할 수 있다.
- [ ] Unity나 저장소에 `service_role` 또는 secret key가 없다.

### Unity

- [ ] `GameSceneTest > GameManager > SupabaseMatchUploader`에 Project URL과 `anon` key가 설정되어 있다.
- [ ] Play 후 Console에 `[Supabase] 세션 확보`가 출력된다.
- [ ] 닉네임 설정 후 `profiles`가 생성 또는 갱신된다.
- [ ] 테스트 게임 종료 후 로컬 JSONL 전적이 생성된다.
- [ ] Console에 `[Supabase] 전적 업로드 완료`가 출력된다.
- [ ] `matches`에 같은 사용자의 `match_id`가 중복 저장되지 않는다.
- [ ] 전적창 서버 조회가 종료 시각 최신순으로 동작한다.
- [ ] Supabase를 사용할 수 없는 상황에서도 로컬 전적이 남는다.

## 11. 장애 시 확인 순서

| 로그 | 의미 | 확인할 것 |
|---|---|---|
| `URL/anon key 미설정` | Unity 설정 누락 | `SupabaseMatchUploader` Inspector |
| `익명 로그인 실패` | Auth 요청 실패 | Anonymous sign-ins, URL, key, 네트워크 |
| `프로필 upsert 실패` | `profiles` 쓰기 실패 | RLS와 `auth.uid()` |
| `세션 없음 — 업로드 생략` | 인증 전 또는 인증 실패 | 앞선 Auth 로그 |
| `전적 업로드 실패` | `matches` insert 실패 | RLS, 필수 컬럼, 스키마, 중복 키 |
| `전적 조회 실패` | 서버 전적 select 실패 | 세션, select RLS, 사용자 UUID |

## 12. 인수자가 먼저 결정할 사항

1. 익명 로그인을 최종 서비스에서도 유지할지, 계정 로그인과 연결할지
2. 앱 데이터 삭제 후 새 익명 계정이 생성되는 문제를 허용할지
3. 협동 한 판을 사용자별 2행으로 유지할지, 별도의 게임 단위 테이블을 둘지
4. 파트너의 아이템·레벨·시너지를 네트워크 스냅샷에 추가할지
5. 라운드별 상세 기록이 실제 분석 요구사항인지
6. 상세 기록이 필요하다면 `match_rounds` 또는 `match_events`를 언제 도입할지

## 13. 단일 기준

- Unity 연동: `Assets/_Project/Scripts/Network/SupabaseMatchUploader.cs`
- 전적 생성: `Assets/_Project/Scripts/Managers/MatchRecorder.cs`
- 데이터 모델: `Assets/_Project/Scripts/Data/MatchRecord.cs`
- DB 스키마와 RLS: `Docs/SCHEMA_2026-07-10_supabase-matches.sql`

코드와 문서가 다를 경우 실제 동작은 위 Unity 코드와 적용된 Supabase DB 스키마를 기준으로 다시 확인한다.
