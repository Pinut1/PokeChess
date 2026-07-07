# 인수인계 계획 (2026-07-07 · 영욱)

> 영욱이 7월 말 학원 퇴소 예정. 남은 기간 마무리 작업 + Core/Network/전투/보드 파트 인수인계 계획.
> 원칙: **문서로 못 넘기는 것(네트워크/Photon 영역)부터 직접 끝내고**, 나머지는 문서+워크스루로 넘긴다.

---

## 주차별 계획

### 1주차 (~7/13) — 걸려 있는 것 착지 ✅ 대부분 완료 (7/7)
- [x] 미커밋 작업분 정리 → PR #26 (스테이지 1-11→1-5 축소 + RW001~005 저작 + 씬 인스펙터 확정값)
- [x] PR #24 (전적 기록 Phase 1 — 로컬 jsonl) 리뷰 후 머지
- [x] 브랜치 정리 — 머지된 원격 브랜치 13개 삭제, PR #25 오픈(`stage-data → master` 복구)
- [ ] PR #25 · #26 팀 확인 후 머지 → **이후 모든 PR base는 master로 통일**
- [ ] 태욱님께 본인 브랜치 정리 목록 전달 (아래 §브랜치 참고)
- [ ] 이브이 3성 → 봇 전원소환 트리거 실검증 (에디터 플레이)

### 2주차 (7/14~7/20) — 마지막 구현 + 실검증
- [ ] **골드 전송** 구현 (통신기 중 유일한 미구현 — CustomProperties 기반, 마스터 검증)
- [ ] 2인 실테스트 (ParrelSync, FixedRegion=kr + UserId 고정 전제):
  - [ ] 재접속 시나리오 (ReconnectAndRejoin, 유예시간/마스터 교체)
  - [ ] 통신교환 (유닛 전송 + 즉시 진화, 모델A)
  - [ ] 증강 3택1 제공 흐름
  - [ ] 전적 기록 (승리/패배/재접속실패 각 endReason 확인)

### 3주차 (7/21~7/27) — 기능 프리즈 + 문서화
- [ ] **신규 기능 프리즈** — 이후는 버그픽스/문서만
- [ ] 마스터 인수인계 문서 작성 (아래 §인수인계 문서 구조)
- [ ] `NEXT_TASKS.md` 갱신 또는 대체 (6/16 기준이라 실제 코드와 어긋남)
- [ ] 태욱님 워크스루 1~2회 (NetworkManager / BattleManager 중심)

### 마지막 주 (7/28~7/31) — 마무리
- [ ] 잔여 PR 전부 착지 (오픈 PR 0개로 종료)
- [ ] Notion 인수인계 문서 공유 (팀 공유용)
- [ ] 최종 상태 회의 공유

---

## 인수인계 문서 구조 (3주차 작성)
1. 아키텍처 한 장 요약 — GameEvents 규칙, 매니저 맵 (CLAUDE.md 링크)
2. Photon/네트워크 운영 지식 — 테스트 방법, 함정 목록 (FixedRegion=kr, ParrelSync UserId 고정, MasterClient 권위 구조, SerializeField는 씬 인스펙터 값 우선)
3. 데이터 파이프라인 — 시트 → JSON → 임포터 (기획 4명 self-serve 절차)
4. 미완 티켓 전체 목록 — 담당자·상태·다음 액션
5. 보류 결정사항 — PlayFab Phase 2 (Title ID 발급 절차, 로그인 흐름 영향), 파치리스 도발 기획 대기 등

---

## 남은 티켓 현황 (2026-07-07 코드 검증 기준)

| 항목 | 상태 | 비고 |
|---|---|---|
| 통신교환 (유닛 전송+즉시진화) | ✅ 구현됨 (6/23, 모델A) | 2인 실테스트만 남음 |
| 재접속 (ReconnectAndRejoin) | ✅ 구현됨 | 2인 실테스트만 남음 |
| 악(惡) 시너지 첫 스킬 스턴 | ✅ 구현됨 | `BattleManager.MarkDarkFirstSkillStun` |
| 전적 기록 Phase 1 (로컬 jsonl) | ✅ 머지됨 (PR #24) | 후속: GameScene 본씬 MatchRecorder 부착, 전적창 UI(태욱·해인) |
| **골드 전송** | ❌ 미구현 | **2주차 구현 대상 (유일한 잔여 구현)** |
| 증강 3택1 제공 흐름 | 🔶 배선 존재 | 플레이 검증 필요 |
| 이브이 3성 봇 전원소환 | 🔶 구현됨 | 에디터 실검증 필요 |
| 파치리스 도발 | ⏸ 기획 확정 대기 | skillId/targetType/마나비용 |
| PlayFab 전적 Phase 2 | ⏸ 보류 | matchId → Room 속성 GUID 교체 포함 |

### PR #24 리뷰에서 나온 경미한 후속 (비긴급)
- `MatchRecorder._matchActive/_finalized`가 판 종료 후 리셋 안 됨 — 씬 리로드 없는 재시작 기능이 생기면 두 번째 판 기록 누락
- `MatchHistoryStore.Append()` 실패해도 `OnMatchRecorded` 발행됨 — UI가 저장 성공으로 오인 가능

### PR #26 리뷰 포인트 (팀 확인 필요)
- 씬 `_startingGold=10` + RW001 골드 10 → 1라운드 시작 20골드 중복 가능성 (선지급 모델 전제는 씬 0이었음)

---

## 브랜치 정리 (7/7 완료분 + 잔여)
- **완료**: `stage-data → master` PR #25 오픈. 머지된 원격 브랜치 13개 삭제 (claude/* 6, reforger-grant-wiring, fix/critical, AugmentManager_HHI, BoardManager_KKW, art/units-model-setup, taewook-xp-change-event, match-history)
- **태욱님 확인 요청 — 머지 확인된 본인 브랜치 8개 (삭제해도 안전)**:
  `taewook-consumable-system` `taewook-level-xp` `taewook-reward-stage-check` `taewook-shop-pool-probability` `taewook-stage-importer` `taewook-stage-reward-join-importer` `taewook-stage-trainer-reward-importer` `taewook-unit-sold-unequip-items`
- **태욱님 판단 필요 — 미머지 5개**: `taewook-hud-ongui` `taewook-item-inventory-flow` `taewook-item-inventory-flow-clean` `taewook-itemmanager-equip-stats` `taewook-reward-v2-itemshop-reroll`
- PR #25 머지 후 `feature/stage-data` 삭제 → 이후 base는 master
