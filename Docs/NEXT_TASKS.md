# PokeChess 다음 작업 (2026-06-16 기준)

> 작업 이어가기용 체크리스트. GDD(더블업식 2인 Co-op PvE) + 6/16 동기화 작업 기준.
> 팀: **김영욱**(Core/Network + 전투/보드), **김태욱**(상점/아이템/UI). 김기욱 이탈로 전투/보드는 김영욱이 흡수.
> 총 6명 = 개발 2 + 기획·아트 4.

---

## 🎯 1순위: 임포터 / 데이터 시트 우선 (2:4 구조 레버리지)

> **원칙:** 개발 2명은 *콘텐츠*가 아니라 *시스템 + 임포터*만 만든다. 포켓몬·시너지·스테이지를 손으로 박지 말고,
> 기획·아트 4명이 **데이터 시트로 자급(self-serve)** 하게 만든다. 콘텐츠는 싸고(4명), 시스템은 비싸다(2명).
> 개발이 "이번에 지원하는 효과/필드 범위" 명세를 먼저 주고, 기획은 그 안에서 시트를 채운다(게이트).

**개발이 먼저 시스템화할 것 (이게 풀리면 4명이 바로 일 시작):**
- [ ] `PokemonData` 시트 컬럼 확정 + `PokeChessImporter` 확장 — 마나/스킬/role/시너지 필드까지 임포트
- [ ] **시너지 효과 제네릭화** — `SynergyData`에 구조화된 효과 타입(스탯버프 / 보호막 / 추가데미지 등 3~4종) 필드 + 임포터 → 기획이 12시너지를 데이터로 조립
- [ ] **Stage Table / Trainer Entry 임포터** (Wild/Trainer/Boss) → 기획이 5~7라운드 적 구성 채움
- [ ] `Reward Table` 임포터 → 라운드 보상 데이터화
- [ ] Item/Augment 시트 **스키마만 미리 확정** (시스템 구현은 후순위라도 기획이 채워둘 수 있게)

**그 다음 콘텐츠는 비개발 인력이 채움:**
- 기획: 위 시트 채우기(포켓몬 8~12종, 시너지 3~4개부터, 스테이지 5~7) / 밸런싱
- 아트: 캡슐·큐브 → 실제 모델/카드 일러스트 최소분 교체

---

## ✅ 지금까지 완료
- 보드/상점/골드 Photon 동기화 (각자 권위 + 상대 보드 시각 미러)
- 팀 공통 HP (Room 속성, MasterClient 권위)
- 로비→게임 씬 흐름 정상화 (씬 로컬 구조 + SceneReady 핸드셰이크)
- 2인 매칭 → 동시 게임 씬 전환 검증
- 샵·드래그앤드롭·합성·판매·게임오버 (이전 작업)
- 헥스 보드/벤치 생성, 일반 진화(3합체 별업)

## ⚠️ 알려진 임시 구현 (교체 필요)
- [ ] `BattleManager` 적 팀이 **"내 보드 미러"** placeholder → Stage/Trainer 데이터 기반 PVE 적으로 교체
- [ ] 전투에 **시너지/아이템/증강 버프 미적용** (SynergyTier에 구조화된 효과 데이터 없음 — 설명 문자열뿐)
- [ ] `PokemonData.synergies`가 `List<string>` → 기획 확정 후 `List<SynergyType>` enum으로 교체
- [ ] 파트너 보드 미러가 캡슐만 표시 (종/모델 미해석) — 필요 시 PokemonData 룩업 추가

---

## 김영욱 — Core/Network + 전투/보드

### 통신기 (자원 공유 / 통신 진화) — 우선순위 높음
- [ ] 골드 전송 (CustomProperties 기반, 마스터 검증)
- [ ] 유닛 전송 (벤치→파트너 벤치) — **원자 트랜잭션 RPC**: 요청→검증→상대 스폰 (복사/소실 방지, GDD 명시)
- [ ] 통신 진화: 강철톤/핫삼류 전송 즉시 진화, 취소 불가, 양쪽 필드+대기석 동일 포켓몬 모두 진화체로
- [ ] 통신기 UI 트리거 (UI 파트와 연동)

### 전투 (PVE)
- [ ] Stage Table / Trainer Entry 데이터 구조 + 임포터 (Wild/Trainer/Boss)
- [ ] `BattleManager.SetupUnits()` 적 생성을 데이터 기반으로 교체
- [ ] 마나·스킬 전투 (PokemonSkillData: damage/manaCost/targetType/area/line)
- [ ] 시너지 전투 버프 적용 (SynergyTier 효과 데이터 구조화 후)
- [ ] 라운드 보상 (Reward Table) 적용

### 네트워크 안정화
- [ ] 증강 라운드(1-5, 1-10, 2-4) 네트워크 동기화
- [ ] 재접속 시나리오 2인 테스트 (유예시간/마스터 교체)
- [ ] 라운드 진행 중 보드 상태 재동기화(후입장/재접속 대비)

---

## 김태욱 — 상점 / 아이템 / UI

### UI (HUD) — 우선순위 높음
- [ ] 골드 표시 — `GameEvents.OnGoldChanged` 구독
- [ ] 파트너 골드 표시 — `GameEvents.OnPartnerGoldChanged` 구독
- [ ] 팀 공통 HP 표시 — `GameEvents.OnHealthChanged` 구독
- [ ] 라운드/페이즈 표시 — `GameEvents.OnRoundChanged` / `OnPhaseChanged`
- [ ] 상점 슬롯 UI (구매/리롤/레벨업 버튼) — `ShopManager` 연동, `OnShopRerolled` 구독
- [ ] 벤치/보드 시각 정리 (현재 임시 큐브/캡슐)

### 아이템 / 증강
- [ ] 아이템 상점 + 장착/조합 (ItemData recipe)
- [ ] 증강 선택 UI (라운드 진입 시 3택1) — `GameEvents.OnAugmentSelected`
- [ ] 진화의 돌(특수 진화) — 특정 성급에서만, 황금제거기로 해제

---

## 공통 메모 / 주의사항
- **테스트**: 2인=`_soloMode` OFF, 단일=GameSceneTest 직접 + `_soloMode` ON
- **씬 셋업**: GameSceneTest에 `GameSceneBootstrap` 필수, GameManager 오브젝트에 `PhotonView`
- **규칙**: 매니저끼리 직접 참조 금지 — `GameEvents`로만 통신 (pull은 `GameManager.Instance.X`)
- 새 이벤트는 `GameEvents.cs`에만 추가
- 참고 문서: Notion "작업 공유 & 역할 분담 (2026-06-16)" / GDD(로컬 docx)
