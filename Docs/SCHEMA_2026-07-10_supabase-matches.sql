-- =============================================================
-- PokeChess 전적 DB 스키마 v1 (Supabase / Postgres)
-- 2026-07-10 김영욱
--
-- 사용법: Supabase 대시보드 > SQL Editor에 이 파일 전체를 붙여넣고 Run.
--
-- 설계 원칙:
--  - 로컬 MatchRecord(스키마 v1, Data/MatchRecord.cs)를 그대로 미러.
--    각 클라이언트가 "자기 관점(self)" 레코드를 업로드하고,
--    협동 한 판 = 레코드 2건을 match_id로 묶는다 (로컬 jsonl과 동일 모델).
--  - 보드 스냅샷/시너지/증강은 구조가 더 바뀔 수 있어 jsonb로 저장
--    (컬럼 정규화는 랭킹/통계 필요해질 때 Phase 3에서).
--  - 인증은 Supabase 익명 로그인(signInAnonymously) + profiles.nickname.
--    Photon UserId와는 별개 — 업로드 주체 식별용.
-- =============================================================

-- 익명 유저 프로필 (닉네임)
create table public.profiles (
  id         uuid primary key references auth.users (id) on delete cascade,
  nickname   text not null default '' check (char_length(nickname) <= 24),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

-- 전적 1건 = MatchRecord 1건 (자기 관점)
create table public.matches (
  id               bigint generated always as identity primary key,
  user_id          uuid not null references auth.users (id) on delete cascade,

  -- MatchRecord v1 미러
  schema_version   int  not null default 1,
  match_id         text not null,             -- 같은 판 식별(두 클라이언트 동일 값)
  game_version     text not null default '',
  started_at       timestamptz,
  ended_at         timestamptz,
  duration_seconds int  not null default 0,
  result           text not null check (result in ('Victory', 'Defeat')),
  end_reason       text not null default '',  -- "GameCleared" | SessionEndReason 이름
  final_round      int  not null default 0,
  final_stage_id   text not null default '',  -- "1-5" 등, 미해석이면 ''

  -- PlayerRecord(self/partner) 통째로 — board/activeSynergies/augments 포함
  self_record      jsonb not null,
  partner_record   jsonb,                     -- 솔로 모드면 null 허용

  created_at       timestamptz not null default now(),

  -- 같은 유저가 같은 판을 중복 업로드하는 것 방지 (재시도 안전)
  unique (user_id, match_id)
);

-- 전적창 조회용: 내 최근 전적
create index matches_user_recent_idx on public.matches (user_id, ended_at desc);
-- 협동 한 판 묶어 보기용
create index matches_match_id_idx on public.matches (match_id);

-- =============================================================
-- RLS (Row Level Security)
--  - 클라이언트는 anon key로 접근하므로 RLS가 사실상 유일한 방어선.
--  - 쓰기: 본인 레코드만. 읽기: 전적은 본인 것만, 닉네임은 전체 공개
--    (파트너 닉네임 표시용). 랭킹 붙일 때 select 정책만 넓히면 됨.
-- =============================================================
alter table public.profiles enable row level security;
alter table public.matches  enable row level security;

create policy "profiles: 누구나 닉네임 조회"
  on public.profiles for select
  to authenticated
  using (true);

create policy "profiles: 본인 생성"
  on public.profiles for insert
  to authenticated
  with check (auth.uid() = id);

create policy "profiles: 본인 수정"
  on public.profiles for update
  to authenticated
  using (auth.uid() = id)
  with check (auth.uid() = id);

create policy "matches: 본인 전적 조회"
  on public.matches for select
  to authenticated
  using (auth.uid() = user_id);

create policy "matches: 본인 전적 업로드"
  on public.matches for insert
  to authenticated
  with check (auth.uid() = user_id);

-- update/delete 정책 없음 = 전적은 불변(감사 로그 성격). 잘못 쌓이면 서버에서만 정리.
