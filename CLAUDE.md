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

> **2026-07-28 기준 최신 인수인계는 `Docs/HANDOFF_2026-07-28_final.md`.** 이전 인수인계 문서와 충돌하면 그 문서가 우선한다.

## 핵심 규칙
- **매니저끼리 직접 참조 금지** — 반드시 `GameEvents`를 통해 통신
- 새 이벤트는 `GameEvents.cs`에만 추가
- 다른 파트 매니저 건드릴 때는 담당자에게 먼저 확인 (Core/Network/전투/보드 = 영욱, 상점/아이템/UI = 태욱)
- **`Singleton.Instance`로 널 검사 금지** — 게터가 null일 때 `LogError`를 찍으므로 검사 자체가 에러 로그가 된다. `GameManager.TryGet(out var gm)` / `HasInstance`를 쓸 것

## 🚨 건드리기 전에 읽을 것 (지뢰)
- **`RewardKind` enum 순서 변경 금지** — Unity가 enum을 int로 직렬화한다. `AugmentChoice(7)`/`ItemShopReroll(8)`/`Reforger(9)`에서 `ItemShopReroll`을 지우면 `Reforger`가 9→8로 밀려 임포트된 `RewardDatabase.asset`의 재련기 보상이 깨진다. **아이템 리롤이 카드별 모델로 바뀌어 안 쓰이더라도 멤버는 유지.** `ParseRewardKind` 폴백이 `_ => Gold`라 매핑을 지우면 기존 행이 경고 없이 골드 2로 둔갑한다
- **`SoundId` enum 재정렬 금지 — 새 항목은 반드시 맨 뒤에 추가** — `RewardKind`와 같은 이유(Unity가 enum을 int로 직렬화). `SoundCatalog.asset`(SoundEntry.id)이 이 정수값으로 클립을 매핑하므로, 중간에 끼워 넣거나 순서를 바꾸면 기존에 저장된 매핑이 통째로 다른 사운드를 가리키게 된다
- **`GameSceneTest.unity` 충돌은 텍스트 3-way로 풀지 말 것** — fileID 기준 블록 단위 병합 절차: `Docs/HANDOFF_2026-07-28_final.md` §4. 디스크에서 씬을 덮어썼는데 Unity가 열어둔 상태면 반드시 **Reload**(Save 누르면 병합 결과 유실)
- **`PokemonUnit._visual`에서 `[SerializeField]` 떼지 말 것** — 합체 진화가 기존 유닛을 `Instantiate`로 복제하므로, 직렬화되지 않으면 복제본의 `_visual`이 null이 되고 `RefreshVisual()`이 이전 모델을 못 지운 채 새 모델을 덧붙여 **두 모델이 겹친다**
- **시너지 카운트 키를 `unit.data.id`로 되돌리지 말 것** — `SynergyManager`는 카운트 키로 `EvolutionFamily.RootId()`(진화 계열 루트)를 쓴다. 종 id로 되돌리면 3합체 시 종이 스왑되는 탓에 이상해씨+이상해풀+이상해꽃이 **풀3 독3**으로 잡혀 한 계열만으로 티어가 발동한다(41개 계열 해당). 티어 수치도 계열 기준으로 재조정돼 있어 같이 헐거워진다. 돌연변이만 `SynergyData.countPerSpecies=true`로 종 단위 예외인데, **이 플래그를 지우면 이브이 9종이 1카운트로 묶여 돌연변이 시너지가 죽는다**. 배경: `Docs/PLAN_2026-07-30_synergy-family-count.md`
- **Play 중 스크립트 저장 금지** — 도메인 리로드로 세션이 깨진다
- **TMP 폰트 아틀라스(`NEXON *SDF.asset`) 커밋 금지** — 다이나믹 아틀라스라 Play할 때마다 글리프가 추가돼 diff가 생긴다
- **`CoinText` 이름 중복** — `Coin_Panel`(골드)과 `Coupon_Panel`(쿠폰) 두 곳. 이름만으로 찾으면 오작동
- **상점 패널 Rect(1600×400)는 보이는 바보다 크다** — 벤치 행·전투시작 버튼까지 덮으므로 화면 영역 판정에 그대로 쓰면 오탐
- ~~`PokemonData.range`는 사거리가 아니라 진화 단계 값이다~~ ✅ 수정 완료(8/7, PR #83): `BattleManager.cs`의 `CreateBotUnit`/`CreateEnemyUnit`이 `data.range` 대신 `data.attackRange`를 쓰도록 수정됨. 아군/미러전투 경로(`PokemonUnit.Range`, `phantom.Range`)는 원래부터 `attackRange`를 참조해 영향 없었음. 같은 PR에서 타겟 선정 로직도 개선(사거리 내 우선 → 이동 가능한 가장 가까운 적 → 기존 role 우선순위 폴백). **주의: `PokemonData.range` 필드 자체는 삭제되지 않고 여전히 존재**(시트 컬럼명도 그대로 `range`) — 진화 단계가 필요한 곳(합성 판정 등)엔 계속 정당하게 쓰인다. 사거리가 필요하면 반드시 `attackRange`를 쓸 것. 새 코드에서 `BattleUnit.range`/`PokemonUnit.Range`를 채울 때 실수로 `data.range`를 다시 끌어다 쓰지 않도록 코드리뷰 시 재확인.

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
- ~~지원스킬 위력~~ ✅ 확정(8/19, 해인): **AsBuff·ManaRegen = 서포터가 "본인을 뺀 areaRadius 내 아군"에게 거는 지원**으로 통일 — 반경은 시트 `areaRadius`가 그대로 결정하고(코드에 상수로 박지 말 것), 코드 규칙은 **본인 제외 한 줄뿐**. 계수는 공용 `SUPPORT_BUFF_SPELLPOWER_COEF=0.05` 하나(AsBuff=(spellPower×0.05)%p 공속, ManaRegen=spellPower×0.05 즉시 마나), `AS_BUFF_DURATION`=3초. 0.5였을 땐 공속이 3성 +287%p(플러시)까지 튀고 마이농이 자기 코스트(45)의 6배를 자기에게 돌려줘 마나 자가 순환이 났다. **본인 제외는 `GetAllyTargets`의 ALLY_AREA 분기에서 effectType으로 판정**한다 — role은 영웅증강이 덮어써서(roleOverride) 전투 중 바뀔 수 있어 기준으로 못 쓴다. 🚨 **`ALLY_SELF`(SelfASBuff, 47마리)에는 본인 제외를 절대 적용하지 말 것** — 자기에게 거는 스킬이라 빼면 효과가 통째로 사라진다. HP_REGEN·SHIELD의 ALLY_AREA는 기존 반경 판정 그대로다.
- 스킬용 STUN: 스코프 아웃(7/10, 증강 6종에 없음 — 메커니즘만 유지), SLOW: 보류(유닛/아이템 미구현)
- `BattleManager` role 타겟 우선순위(`ROLE_TARGET_PRIORITY`)
- `Combat/ItemConditionalEffect` 화상 딜/틱/반경(`BURN_*`), 이속 누적
- ~~`Core/PokemonUnit` 특수진화체 별배율 ×1.5~~ ✅ 확정(7/23, 해인 작성가이드 07.23판): 곱하는 계수가 아니라 **별도 배율표**. 일반 진화 `1.0/1.8/2.8`, 특수 진화(돌·통신교환) `1.0/2.0/3.0`. `SPECIAL_EVOLUTION_MULTIPLIER` 제거됨
- `Managers/ItemManager` 인벤토리 상한 `MAX_INVENTORY_SIZE=20`
- `Managers/ShopManager` 라운드 결과(outcome)별 XP 차등
- ~~유닛 판매 환급액~~ ✅ 확정(7/29, 해인 기획디렉터): 투자 골드(코스트 × 1성 환산 마리 수, 1성=×1/2성=×3/3성=×9)에서 **합성 패널티 −1**. 패널티는 **1코스트를 제외한 모든 코스트**에 붙고, **2성·3성 모두 −1 고정**(3성이라고 −2 아님). **1성은 패널티 없음**(사자마자 팔아도 본전 — 상점 탐색 위축 방지). 예: 2코스트 2성=5, 2코스트 3성=17, 1코스트 2성=3. `ShopManager.SellValue()` 단일 소스라 UI 표시와 실지급액이 자동 일치

**🟡 타 담당 영역**
- ✅ 상점 카드 UI(유닛 5칸/아이템 4칸) — `haein_UI` 병합으로 master 반영 완료(7/28, PR #57). 아이템 리롤은 **카드별 슬롯 1회·무료** 모델(`RerollItemSlot`). 구 모델(`RerollItemShop`·`AddItemShopReroll`·`OnItemShopRerollCountChanged`)은 제거됨
- 레벨/확률/골드/쿠폰 텍스트 바인딩 + 유닛 드롭 판매 — PR #58 (씬 미변경). 판매 영역 시각 정렬은 `UnitDragController.Shop Sell Area` 인스펙터 지정으로 조정
- ✅ 증강 시스템 — **Augment Table v2 확정 6종**(7/16 해인 회신 반영: 레벨할인 삭제, 구독서비스=1회만 오픈(8/15 정정, 장한나 — 기존 "확정 2회 오픈"은 오기), 전 영웅 ×1.4, 이브이→마법사, 전용리롤 아님) + 3택1 오퍼 + 블로킹 UX(모달·내려두기·1분/Ready 자동선택) 구현(영욱 대행). 상세: `Assets/_Project/Docs/AugmentSystem.md`. 자뭉열매는 7/17 완료. 나인이볼부스트(8종 소환+버프)는 8/19 완료. 남은 것: 선택 UI 정식화+배치입력 `IsChoiceBlocking` 배선(태욱), **별도 티켓** — `SK_` 스킬행(시트 미작성)
- ✅ `ShopManager` XP 이벤트화 + `UIManager` 진행 HUD/XP 구매 UI — 완료(PR #39, 태욱). `UIManager`가 Gold/Level/Xp/UnitCap 이벤트 구독, `PrototypeHud`의 XP 폴링·중복 제거
- ✅ `Managers/RewardManager` `AugmentChoice` 지급 — 연결 완료(7/16). preReward(StageData)와 RewardKind 두 경로 모두 지원

**🟢 영욱 영역 (Core/전투/보드/네트워크)**
- ✅ 악(DARK) 시너지 첫 스킬 스턴 — 구현 완료(`BattleManager.MarkDarkFirstSkillStun`/`CastSkill`)
- ✅ 전적 기록 시스템 — 전 구간 완료(7/10): 로컬 jsonl(`MatchRecorder`→`MatchHistoryStore`) + 전적창 UI(태욱, `UIManager`) + 닉네임 입력(`TrySetLocalNickname`) + **Supabase 서버 업로드**(`Network/SupabaseMatchUploader` — 익명 세션+profiles 닉네임+matches 업로드, 스키마 `Docs/SCHEMA_2026-07-10_supabase-matches.sql`) + matchId Room 속성 GUID 배포(`NetworkManager.MatchGuid`) + **전적창 서버 조회 로컬/서버 탭**(Phase 3, PR #45, 7/20 병합). 전 구간 완료.
- `RoundPhaseManager` preReward 훅(`OnStageEntered` 구독) — 기획/담당 분배 후 연결
- 일반 스킬의 CC/지원/타겟팅 메커니즘은 완료. ✅ **나인이볼부스트(HERO_EEVEE) v2 완료(8/19)** — 영웅 이브이가 스킬 1회 시전마다 진화체 1종을 순서대로 봇 소환하고, 그 종의 버프를 **봇이 아니라 이브이 자신에게** 건다. 순서·수치는 `Data/HeroEeveeBoostTable.cs` 한 곳(밸런스 조정 = 이 표만 수정), 전투 로직은 `BattleManager.TryCastHeroEeveeBoost`. 전용 VFX 없음(소환되는 진화체가 곧 연출). `SK_` 영웅스킬은 skill_table에 행 0건(기획 미작성)이라 코드로만 동작. 보스 전용 기믹은 **스코프 아웃**(7/21 확인, 근거: 기획 역기획서 `SCHEMA_2026-06-19_stage-data-v2.md`에 보스=statMul/hpMul+q,r 포메이션만, 패턴/페이즈 컬럼 자체가 없음 — 스탯 배수가 최종 스펙. 현 데이터는 1-5 슬라이스 한정)
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
