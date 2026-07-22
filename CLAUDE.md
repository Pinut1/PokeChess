# PokeChess — CLAUDE.md

## 프로젝트 개요
Unity URP 기반 포켓몬 TFT 스타일 오토배틀 게임.  
**2인 협동 PVE** — 두 플레이어가 함께 보스를 공략.  
네트워크: **Photon PUN2** (`PHOTON_UNITY_NETWORKING` 심볼).

## 팀 파트 분배
| 담당자 | 파트 | 주요 스크립트 |
|--------|------|--------------|
| 김영욱 | Core / Network / 전투 / 보드 | `GameManager` `Singleton` `GameEvents` `NetworkManager` `BattleManager` `BoardManager` `SynergyManager` |
| 김태욱 | 상점 / 아이템 / UI | `ShopManager` `ItemManager` `UIManager` |

> 김기욱이 팀에서 빠지면서 전투/보드 파트는 김영욱이 전담한다. (2인 체제)

## 핵심 규칙
- **매니저끼리 직접 참조 금지** — 반드시 `GameEvents`를 통해 통신
- 새 이벤트는 `GameEvents.cs`에만 추가
- 다른 파트 매니저 건드릴 때는 담당자에게 먼저 확인 (Core/Network/전투/보드 = 영욱, 상점/아이템/UI = 태욱)

## 폴더 구조
```
Assets/_Project/Scripts/
  Core/       — GameManager, GameEvents, Singleton, PokemonUnit
  Data/       — ScriptableObject 데이터 클래스
  Managers/   — 각 매니저 (스텁 → 담당자가 구현)
  Network/    — NetworkManager, 네트워크 관련 스크립트
  Editor/     — PokeChessImporter (JSON → ScriptableObject)
```

## 씬 구성
| 씬 | 역할 |
|----|------|
| `LobbyScene` | 방 생성/입장, Photon 연결 |
| `GameScene` | 실제 게임 (보드, 전투, 샵) |
| `ResultScene` | 게임 종료 화면 |

## 미확정 / 추후 수정 필요
- `PokemonData.synergies`가 현재 `List<string>` — 기획팀 시너지 목록 확정 후 `List<SynergyType>` enum으로 교체 필요. `SynergyManager`에서 시너지 비교할 때 오타 주의.

## 기술 부채 추적 (TODO / PLACEHOLDER)
코드에 흩어진 미완/임시 항목을 한곳에 모음. 상태: 🔴 기획 수치 대기 / 🟡 타 담당 / 🟢 영욱 영역.

> 2026-07-20 Core 잔여 작업의 현재 코드 대조·착수 조건: `Docs/HANDOFF_2026-07-20_core-gaps.md`. 과거 `NEXT_TASKS.md`의 미체크 항목만 보고 중복 구현하지 말 것.

**🔴 기획 수치 확정 대기 (메커니즘 구현됨, 값만 PLACEHOLDER)**
- ~~마나 충전 모델~~ ✅ 확정(7/10): 초당 10 고정만(`MANA_PER_SECOND`). 평타/피격비례 제거됨
- ~~TAUNT~~ ✅ 날따름 확정(7/10): 시전자 중심 반경, 지속 1.0×1.4×성급(1/1.8/2.8), 원래 타겟 스냅샷→복귀
- ~~지원스킬 위력~~ ✅ 확정(7/10): spellPower×0.5(`SUPPORT_SPELLPOWER_COEF`, 임시 계수). 단 AsBuff "증가량"·ManaRegen "회복량" 해석(%(p) vs 즉시 마나)은 해인님 재확인 대기, `AS_BUFF_DURATION`은 여전히 PLACEHOLDER
- 스킬용 STUN: 스코프 아웃(7/10, 증강 6종에 없음 — 메커니즘만 유지), SLOW: 보류(유닛/아이템 미구현)
- `BattleManager` role 타겟 우선순위(`ROLE_TARGET_PRIORITY`)
- `Combat/ItemConditionalEffect` 화상 딜/틱/반경(`BURN_*`), 이속 누적
- `Core/PokemonUnit` 특수진화체 별배율 ×1.5
- `Managers/ItemManager` 인벤토리 상한 `MAX_INVENTORY_SIZE=20`
- `Managers/ShopManager` 라운드 결과(outcome)별 XP 차등

**🟡 타 담당 영역**
- ✅ 증강 시스템 — **Augment Table v2 확정 6종**(7/16 해인 회신 반영: 레벨할인 삭제, 구독서비스=확정 2회 오픈, 전 영웅 ×1.4, 이브이→마법사, 전용리롤 아님) + 3택1 오퍼 + 블로킹 UX(모달·내려두기·1분/Ready 자동선택) 구현(영욱 대행). 상세: `Assets/_Project/Docs/AugmentSystem.md`. 자뭉열매는 7/17 완료. 남은 것: 선택 UI 정식화+배치입력 `IsChoiceBlocking` 배선(태욱), **별도 티켓** — 나인이볼부스트(8종 소환+버프)·`SK_` 스킬행(전투 신규 메커니즘)
- ✅ `ShopManager` XP 이벤트화 + `UIManager` 진행 HUD/XP 구매 UI — 완료(PR #39, 태욱). `UIManager`가 Gold/Level/Xp/UnitCap 이벤트 구독, `PrototypeHud`의 XP 폴링·중복 제거
- ✅ `Managers/RewardManager` `AugmentChoice` 지급 — 연결 완료(7/16). preReward(StageData)와 RewardKind 두 경로 모두 지원

**🟢 영욱 영역 (Core/전투/보드/네트워크)**
- ✅ 악(DARK) 시너지 첫 스킬 스턴 — 구현 완료(`BattleManager.MarkDarkFirstSkillStun`/`CastSkill`)
- ✅ 전적 기록 시스템 — 전 구간 완료(7/10): 로컬 jsonl(`MatchRecorder`→`MatchHistoryStore`) + 전적창 UI(태욱, `UIManager`) + 닉네임 입력(`TrySetLocalNickname`) + **Supabase 서버 업로드**(`Network/SupabaseMatchUploader` — 익명 세션+profiles 닉네임+matches 업로드, 스키마 `Docs/SCHEMA_2026-07-10_supabase-matches.sql`) + matchId Room 속성 GUID 배포(`NetworkManager.MatchGuid`) + **전적창 서버 조회 로컬/서버 탭**(Phase 3, PR #45, 7/20 병합). 전 구간 완료.
- 🔺 **통신 진화 수정 작업 중(7/22)** — 기획 규칙이 기존 모델A에서 크게 변경됐다. 현재 `SendTradeUnit`의 1마리 핸드오버 구현을 최종 사양으로 보지 말 것. 실시간 단방향 전송은 유지하되, 수신 즉시 동일 종의 보드·벤치·현재 상점·이후 상점을 모두 진화체로 영구 치환하고 재접속 복구해야 한다. 성급·장착물 유지, 경제·공용 풀은 원본 기준. 확정사항과 구현 함정: `Docs/INVESTIGATION_2026-07-22_trade-evolution-redesign.md`.
- `RoundPhaseManager` preReward 훅(`OnStageEntered` 구독) — 기획/담당 분배 후 연결
- 일반 스킬의 CC/지원/타겟팅 메커니즘은 완료. **나인이볼부스트 증강(HERO_EEVEE)은 구현·동작 중**(간이 봇소환) — 미구현은 v2 풀 연출(진화체 8종 순차 소환+종별 버프)뿐. `SK_` 영웅스킬은 skill_table에 행 0건(기획 미작성)이라 코드로만 동작. 보스 전용 기믹은 **스코프 아웃**(7/21 확인, 근거: 기획 역기획서 `SCHEMA_2026-06-19_stage-data-v2.md`에 보스=statMul/hpMul+q,r 포메이션만, 패턴/페이즈 컬럼 자체가 없음 — 스탯 배수가 최종 스펙. 현 데이터는 1-5 슬라이스 한정)
- 🔺 **자동 테스트가 핵심 로직에 못 붙는 구조 (7/21 확인, 미해결)**
  - 문제: `BattleManager`·`ShopManager`·`NetworkManager` 등 런타임 코드가 전부 **predefined assembly(`Assembly-CSharp`)** 에 있는데, **asmdef 기반 테스트 어셈블리는 predefined assembly를 참조할 수 없다**(Unity 제약). 그래서 `PokeChess.EditorTests`에서 이 타입들이 아예 안 보인다. 기존 `SceneStabilityTests`가 도는 건 UnityEditor API만 쓰기 때문.
  - 정공법: `Assets/_Project/Scripts/`에 런타임 asmdef, `Scripts/Editor/`에 에디터 asmdef를 만들어 predefined assembly에서 탈출. `_Project/Scripts` 밖의 `.cs`는 서드파티 데모뿐이라 충돌 위험은 낮음.
  - ⚠️ **함정**: 지금은 `Assembly-CSharp`가 Photon을 자동 참조하지만, 자체 asmdef로 옮기면 `PhotonUnityNetworking`·`PhotonRealtime` 등을 **전부 명시적으로 참조 추가해야** 한다. 첫 시도에 컴파일 에러가 대량 발생하므로 시간 여유가 있을 때 착수할 것. (수료 직전 착수 금지 — 빌드가 깨진 채 인수인계될 위험)
  - 우회책(현재 적용): 의존성 없는 순수 로직만 전용 asmdef로 분리 — `PokeChess.Editor.Diagnostics`(`TrainerEntryDiagnostics`)가 그 예. 통합 동작은 `Scripts/Debug/`의 디버그 하네스로 확인한다(`BalanceCheckHarness`).

## 작업 분배 (태욱 — 상점/아이템/UI)
레벨/XP 시스템(PR #13) 후속:
- ✅ **XP 변경 이벤트화**: 완료(PR #39). `UIManager`가 `GameEvents.OnXpChanged` 등 구독, `PrototypeHud` 폴링 제거.
- ✅ **XP 구매 UI 정식화**: 완료(PR #39). `PrototypeHud` 임시 버튼 → `UIManager` 진행 패널로 이관.
- **밸런스 테이블 확정**: `ShopManager`의 `_requiredXpByLevel`, `_unitCapByLevel`, `_roundXpReward`, `_buyXpCostGold`, `_buyXpAmount`는 임시값 — 기획 확정 후 조정.(남음)

## 데이터 파이프라인
구글 시트 → JSON → `Assets/Resources/Data/` → `PokeChess/Import *` 메뉴 실행 → ScriptableObject 자동 생성  
SO 저장 경로: `Assets/_Project/ScriptableObjects/`

임포트 대상: Pokemon / Item / Consumable / EvolutionStone / Synergy / Reward / Stage / TradeEvolution / **Deck(견본덱, 7/14 신설)**. Deck은 `deck_data.json` → 단일 `Resources/DeckDatabase.asset`(플레이어 가이드용, pokemonId·unitCount 임포트 검증). skill_table은 Pokemon 임포트 시 skillId로 조인 베이킹.

> 데이터 **원본은 구글 시트**이며 SO는 임포터 산출물(읽기 전용 캐시)임 — SO를 직접 수정하지 말 것.
> 가변 런타임 상태는 `PokemonUnit`/`BattleUnit`에만 둔다. 라이브 서비스로 확장 시 JSON 직로드(+id→에셋 매핑)로 전환 고려.

### skillId 규약 (v11, 2026-07-14 확정)
- 형식: `{타입}_{역할군}` 대문자 스네이크. **한 타입+역할에 스킬 2개면** effectType을 붙임 → `{타입}_{역할군}_{효과}` (예: `WATER_TANKER_SHIELD`/`WATER_TANKER_HP_REGEN`).
- 타입명은 `CHEER`·`ETHEREAL` 사용(과거 `CHEERLEADER`·`FAIRY`는 폐기 별칭 — 시트에 섞여 오면 정규화).
- 🔴 기획 대기: 탱커 4종(Water/Poison/Ground/Cheer)이 현재 **전 포켓몬 SHIELD(또는 AS_BUFF) 일괄 배정** — 포켓몬별 세분은 밸런스 확정 후. `BREAKER_WARRIOR`(리오르·루카리오)는 스킬 정의 없어 **평타만**. vfxId 22건 네이밍(`_EFFECT` 접미) 미적용(아트 조율 대기).
- 변환 스크립트(세션 스크래치패드 `convert_v11.mjs`, Node)는 시트 CSV→JSON 변환 시 위 규칙·orphan 검증을 자동 적용. 재수신 시 재사용(휘발성 주의).

## NetworkManager 사용법
```csharp
// 연결
GameManager.Instance.Network.Connect();

// 매칭 (빈 방 있으면 입장, 없으면 자동 생성)
GameManager.Instance.Network.JoinRandomRoom();

// 라운드 시작 브로드캐스트 (MasterClient만)
GameManager.Instance.Network.BroadcastRoundStart(round);
```
2인 입장 완료 시 방 자동 잠금 + `GameEvents.RoundChanged(1)` 발동.

## GameEvents 목록
| 이벤트 | 발행 주체 | 구독 주체 |
|--------|----------|----------|
| `OnBattleStart` | NetworkManager | BattleManager |
| `OnBattleEnd(bool)` | BattleManager | NetworkManager, UIManager |
| `OnGoldChanged(int)` | ShopManager | UIManager |
| `OnLevelChanged(int)` | ShopManager | UIManager, ShopManager(자기 레벨 동기화) |
| `OnUnitCapChanged(int)` | ShopManager | BoardManager |
| `OnUnitPlaced(unit)` | BoardManager | SynergyManager, UIManager |
| `OnUnitBenched(unit)` | BoardManager | SynergyManager |
| `OnUnitSold(unit)` | ShopManager | UIManager |
| `OnShopRerolled` | ShopManager | UIManager |
| `OnRoundChanged(int)` | NetworkManager | 전체 |
