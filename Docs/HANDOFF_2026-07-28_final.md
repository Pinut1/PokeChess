# PokeChess 최종 인수인계 (김영욱 → 팀)

> **기준 시점: 2026-07-28, master `87e2e11f`**
> 김영욱 수료일 = 2026-07-28 (마지막 근무일). 이 문서가 **최종본**이며, 이전 인수인계 문서와 충돌하는 내용이 있으면 **이 문서가 우선**한다.
>
> 함께 볼 문서
> - `CLAUDE.md` — 규칙·담당 분배·기술 부채 총괄 (**PLACEHOLDER 수치의 단일 진실**)
> - `Docs/HANDOFF_2026-07-20_final.md` — 시스템별 완료/미완 내역 (7/21 기준, 아래 §2 정정분 적용해서 읽을 것)
> - `Docs/HANDOFF_2026-07-20_core-gaps.md` — Core 잔여 작업 코드 대조
> - `Docs/HANDOFF_DAY1_2026-07-27.md` / `DAY2` — 인수자 온보딩 절차 (Supabase 권한 이관 포함)

---

## 1. 오늘(7/28) 한 일 — master에 병합 완료 (PR #57)

7/27까지 세 갈래로 갈라져 있던 브랜치를 하나로 합치고 검증했다.

| 커밋 | 내용 |
|---|---|
| `6e8d1f51` | `master`(art/hanna_UI — 아이템 인벤토리 20슬롯 + 시너지 슬롯) 병합 |
| `04c4d7ab` | 전투 준비 버튼 이름 교정분 반영 |
| `d14d6bf1` | 디버그 IMGUI HUD 7종 비활성화 |
| `b2fd7c8a` | `haein_UI`(상점 카드 UI 일체 + 아이템 상점) 병합 |
| `20575d60` | `Singleton`에 로그 없는 조회 수단 추가 + UI 호출부 정리 |

**검증 결과** (Unity Play, 실측)

- 컴파일 에러 0 / 씬 Missing Script 0 / Broken Prefab 0 / Play 진입 에러 0
- 유닛 상점 카드 5장 + 아이템 상점 카드 4장 정상 렌더, 슬롯 데이터 정상
- **8행 단일 전장 동작 확인**: 아군 `r=-1~2`, 적 `r=3~6` — 겹침 없는 연속 8행. 월드 z가 r에 비례(r당 1.73)해 논리 좌표와 시각 좌표가 같은 공간에 있다
- **전투 비주얼 실제 모델 10/10, 캡슐 폴백 0건**
- **근접 교전 확인**: 전투 중 아군이 `r=3~4`까지 전진하고 적이 `r=1`까지 내려옴. "근접이 적을 못 때린다"던 증상 해소
- 리롤 3종(유닛 리롤 / XP 구매 / 아이템 슬롯별 리롤) 전부 정상, 라운드 넘기면 슬롯 리롤 복구

---

## 2. ⚠️ 이전 문서 정정 — 아래는 더 이상 유효하지 않다

### 2-1. `haein_UI` 브랜치는 **전부 master에 흡수됐다**

```
origin/haein_UI : behind 23 / ahead 0
```

해인님 7/27 일지의 "병합 시 `GameSceneTest.unity` 충돌 재발 가능성 높음"은 **해소됐다.** 그 브랜치에서 계속 작업하면 이미 반영된 커밋 위에 또 쌓게 된다. **master에서 새 브랜치를 딸 것.**

### 2-2. 아이템 리롤은 "카드별 슬롯 1회" 모델이 master에 들어와 있다

`ShopManager.RerollItemSlot(int)` / `CanRerollItemSlot(int)` / `_itemSlotRerollUsed[]`.
구 모델(`ItemShopRerollCount`, `AddItemShopReroll()`, `RerollItemShop()`)은 제거됐다.

### 2-3. `SellZone`은 씬에 없었고, 지금은 다른 방식으로 대체 중이다

`Scripts/Core/SellZone.cs`는 존재하지만 **씬 인스턴스가 0개**였다. Collider 기반이라 ScreenSpaceOverlay Canvas인 상점 바에는 애초에 붙지 않는다. PR #58에서 포인터 좌표 판정 방식으로 대체했다. **`SellZone.cs`는 현재 죽은 코드다** — #58 병합 후 제거 여부를 판단할 것.

---

## 3. 열린 PR 2건 — 이것만 처리하면 된다

### PR #58 `feat(ui): 상점 하단 바 진행 표시 연결 + 유닛 드롭 판매` (MERGEABLE)

- 레벨/XP/골드/아이템 쿠폰/코스트 확률 텍스트 연결 (그전까지 `1레벨`·`0G`·`· 100%`×5 하드코딩 상태였다)
- `UIManager.OnGUI` 진행 패널 중복 제거
- 유닛을 상점 바에 드롭하면 판매 (TFT 방식)
- **씬 파일 미변경** — `.cs` 3개만 바뀌므로 다른 브랜치와 충돌하지 않는다
- 남은 것: 판매 영역이 눈에 보이는 상점 바와 정확히 일치하지 않는다. `UnitDragController`의 `Shop Sell Area`에 RectTransform(예: `Main_BackPanel`)을 인스펙터로 지정하면 코드 수정 없이 해결된다

### PR #54 `feat: 통신교환 기능 연결 및 증강 선택 흐름 개선` — **충돌 해결본 준비 완료**

PR #54 자체는 여전히 CONFLICTING이지만, **7/28에 충돌을 해결한 브랜치를 올려두었다.**

```
해결본: Pinut1/trade-ui-merge-0728   (origin/master 대비 behind 0 / ahead 9)
원본  : feature/trade-ui-integration (behind 49 / ahead 8, CONFLICTING)
```

**재구현하지 말 것.** 이 브랜치를 그대로 쓰거나 #54에 머지하면 된다. 해결 내역은 PR #54 코멘트에도 남겼다.

**해결한 충돌 16곳** (스크립트 15 + 씬 1). 씬은 §4의 fileID 블록 단위 병합으로 처리 — 741 blocks / 중복 fileID 0 / 끊어진 참조 0 / 구조 이상 0. `SceneRoots`는 양쪽 추가분 합집합.

| 파일 | 처리 |
|---|---|
| `PokemonUnit` 성급 배율 | master 확정표 채택(일반 `1.0/1.8/2.8`, 특수 `1.0/2.0/3.0`). 브랜치의 구 모델(`SPECIAL_EVOLUTION_MULTIPLIER` ×1.4)은 폐기 |
| `RoundPhaseManager` 준비 요청 | 브랜치의 **증강 블로킹 가드는 유지**, 브로드캐스트만 `GameEvents.ApprovePlayerReady` 경로로 교체 |
| `PrototypeHud` | QA 패널·확률 패널 유지 + `_showShopDebugBar` 게이트. `DrawReady` 제거(`gm.Phase.PlayerReady()`가 이벤트 방식으로 바뀌며 사라져 두면 컴파일 실패) |
| `UIManager` / `NetworkManager` 구독 | 양쪽 합집합 |

**⚠️ git이 자동 병합으로 통과시켰지만 실제로 깨져 있던 2건** — 자동 병합 성공이 안전을 뜻하지 않는다는 사례다.

- `NetworkManager`: 브랜치의 `override OnEnable/OnDisable`과 master의 `private` 버전이 **중복 정의**(CS0111)
- `AugmentOfferHud`: `AbsorbClicksOutside`는 남았는데 `_blockerStyle` 선언이 **유실**(CS0103)

**검증(Play)**: 컴파일 0 / Missing Script 0 / Broken Prefab 0 / Play 에러 0, 합체 진화 후 모델 정상 교체(콘팡→도나리 2성, `childCount=1`), 전투 진입 시 아군 `r=0` / 적 `r=4,6`으로 8행 유지, 모델 3/3 폴백 0.

**남은 것**: 2클라이언트 Photon 실사(통신교환 유닛 전송·수령, 골드 전송 ACK, 증강 블로킹 중 준비 거부).

---

## 4. 🔑 Unity 씬 충돌 해결법 (오늘 확립 — 재사용할 것)

`GameSceneTest.unity`가 단일 씬이라 앞으로도 계속 충돌한다. **텍스트 3-way 머지를 믿지 말 것.**

오늘 `master ← haein_UI` 병합 시 git은 **충돌 19곳**을 보고했지만, 실제 충돌은 **1곳**이었다. 나머지 18곳은 양쪽이 서로 다른 새 블록을 같은 위치에 끼워 넣은 것이다. 텍스트 diff는 블록 경계를 몰라서 서로 다른 GameObject 두 개를 한 덩어리로 뭉갠다 — 이 상태로 손수 고르면 컴포넌트가 깨진다(해인님이 7/27에 겪은 `&1620688241` / `&1558058679` 헤더 공유 사고가 같은 원인).

### 절차

씬은 `--- !u!<타입> &<fileID>` 로 시작하는 **독립 블록의 나열**이다. fileID를 키로 삼아 블록 단위로 3-way 병합하면 경계 문제가 사라진다.

1. `git merge <상대브랜치>` → 충돌 발생
2. 세 버전을 꺼낸다
   ```
   git show $(git merge-base HEAD MERGE_HEAD):Assets/Scenes/GameSceneTest.unity > base.unity
   git show HEAD:Assets/Scenes/GameSceneTest.unity                             > ours.unity
   git show MERGE_HEAD:Assets/Scenes/GameSceneTest.unity                       > theirs.unity
   ```
3. 블록 단위로 병합한다. 규칙은 단순하다
   - 한쪽만 수정 → 그쪽 채택
   - 한쪽만 추가 → 포함
   - 양쪽 수정 → **진짜 충돌**. 사람이 판단
4. 병합 결과를 반드시 검증한다
   - 충돌 마커 0
   - 중복 fileID 0
   - **끊어진 씬 내부 참조 0** (`guid:`가 없는 `fileID: N`이 실제 블록을 가리키는지)
   - **부모-자식 상호 등록** (`m_Father`가 가리키는 부모의 `m_Children`에 자신이 있는지)
   - 양쪽 부모의 블록이 하나도 유실되지 않았는지 (집합 비교)
5. Unity에서 씬을 열고 `Missing Script 0 / Broken Prefab 0` 확인 후 Play

오늘 실적:
```
base 292 blocks / ours 694 / theirs 331  ->  merged 733
  양쪽 동일 269 / ours만 10 / theirs만 12 / ours 신규 402 / theirs 신규 39 / 실제 충돌 1
검증: 중복 fileID 0 / 끊어진 참조 0 / 구조 이상 0
```

> ⚠️ **디스크에서 씬을 직접 덮어쓸 때**: Unity가 그 씬을 열어둔 상태면 *"Scene has been changed externally. Reload?"* 모달이 뜨고 에디터가 멈춘다(MCP도 같이 막힌다). 반드시 **Reload**를 누를 것 — 실수로 Save 쪽을 고르면 **병합 결과가 통째로 날아간다.**

---

## 5. 지뢰 목록 — 건드리기 전에 읽을 것

### 5-1. `RewardKind` enum 순서 변경 금지

Unity는 enum을 int로 직렬화한다. 현재 `AugmentChoice(7)` / `ItemShopReroll(8)` / `Reforger(9)`에서 **`ItemShopReroll`을 삭제하면 `Reforger`가 9→8로 밀려** 이미 임포트된 `RewardDatabase.asset`의 재련기 보상이 깨진다. 아이템 리롤 기능이 카드별 모델로 바뀌어 `ItemShopReroll`이 안 쓰이지만 **enum 멤버는 유지해야 한다.**

추가로 `ParseRewardKind`의 폴백이 `_ => RewardKind.Gold`라, `"itemShopReroll"` 매핑을 지우면 기존 행이 **경고 없이 골드 2 지급으로 둔갑**한다.

### 5-2. `Singleton.Instance`로 널 검사하지 말 것

`Instance` 게터는 null일 때 `Debug.LogError`를 찍는다(초기 커밋부터). 따라서 `if (GameManager.Instance != null)`은 **검사 자체가 에러 로그**가 된다. 7/28에 `HasInstance` / `TryGet(out T)`를 추가했으니 그쪽을 쓸 것.

```csharp
// 하지 말 것
var shop = GameManager.Instance != null ? GameManager.Instance.Shop : null;
// 이렇게
var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
```

UI 호출부 8개는 정리했다. **매니저 내부 호출부 30여 곳은 아직 구 관용구**다 — `GameManager` 생성 이후에만 도는 자리라 에러를 안 찍지만, 초기화 순서가 바뀌면 드러난다.

### 5-3. `CoinText`라는 이름이 두 개다

`Coin_Panel/CoinText`(골드)와 `Coupon_Panel/CoinText`(아이템 쿠폰). 이름만으로 찾으면 둘 중 아무거나 잡힌다. 오브젝트 이름을 갈라두는 게 좋다.

### 5-4. 상점 패널 Rect는 보이는 것보다 훨씬 크다

`UnitStore_Panel` / `ItemStore_Panel`의 Rect는 1600×400이라 **벤치 행(화면 y=326)과 전투시작 버튼까지 덮는다.** 이 Rect로 화면 영역 판정을 하면 오탐이 난다. PR #58은 `Bottom_PanelGroup`과의 교집합으로 잘라 해결했다.

### 5-5. `PokemonUnit._visual`은 반드시 `[SerializeField]`여야 한다

합체 진화는 기존 유닛을 `Instantiate`로 복제해 새 유닛을 만든다. `_visual`이 직렬화되지 않으면 **복제본의 `_visual`이 null**이 되고, `RefreshVisual()`이 이전 모델을 지우지 못한 채 새 모델만 덧붙여 **두 모델이 겹친다**(실측: 늪짱이 2성에 `mudkip_pf(Clone)` + `marshtomp_pf(Clone)` 동시 존재, `childCount=2`).

직렬화하면 Unity가 복제된 자식으로 참조를 remap해 정상 동작한다. 7/28에 `[SerializeField]` 추가 + 구 데이터 대비 안전망을 넣었다(`Pinut1/trade-ui-merge-0728`).

### 5-6. Play 중 리컴파일 금지

Play 상태에서 스크립트를 저장하면 도메인 리로드가 걸려 세션이 깨진다. 코드 수정 전에 반드시 Play를 멈출 것. (7/17에 한 번 날렸다)

### 5-7. TMP 폰트 아틀라스는 커밋하지 말 것

`NEXON Lv2 Gothic OTF SDF.asset`은 다이나믹 아틀라스라 Play로 한글을 그릴 때마다 글리프가 추가돼 diff가 생긴다. 실내용 변경이 아니면 되돌릴 것.

---

## 6. 미병합 원격 브랜치 정리

| 브랜치 | ahead | 처리 |
|---|---|---|
| `Pinut1/shop-progress-ui-and-sell-drop` | 2 | **PR #58** — 병합 대상 |
| `Pinut1/trade-ui-merge-0728` | 9 | **PR #54 충돌 해결본** — 이걸 병합 (§3) |
| `feature/trade-ui-integration` | 8 | PR #54 원본. 위 해결본으로 대체 |
| `Pinut1/handoff-final-0728` | 1 | **PR #59** — 이 문서 |
| `haein_UI` | 0 | **삭제 가능** (전부 흡수됨) |
| `feature/trade-evolution-frozen-0722` | 2 | 통신교환 동결본. #54 병합 후 삭제 판단 |
| `feature/taewook-item-inventory-flow` | 8 | behind 310. 태욱님 확인 필요 |
| `feature/shared-unit-pool-sync` | 3 | PR #40 닫힘의 잔재. 태욱님 확인 후 삭제 |
| `feature/BoardManager_HHI` | 2 | behind 343. 사실상 폐기 |
| `agent/board-bench-stabilization` | 1 | 폐기 판단 |
| `docs/devlog-0716` | 1 | 폐기 판단 |
| `feature/ui-placement-rejected-feedback` | 1 | 태욱님 확인 필요 |

---

## 7. 남은 작업 (담당별)

### 해인님 (기획/데이터/아트)
- [ ] **아이템 상점 플레이 검증** — 배선은 확인됐고 카드는 뜨지만 구매/리롤 UX 실사 확인 필요
- [ ] 아이템 데이터 개정 → 재임포트 → 아이콘 연결 (베리 5종 PNG 없음, 조합기 3종 기획 미확정)
- [ ] **아이템 리롤 밸런스 확인** — 구매로 비워진 슬롯도 리롤된다. 슬롯당 1회 제한은 있으나 쿠폰이 2개 이상이면 한 슬롯에서 라운드당 2개를 가져갈 수 있다. 의도인지 확인
- [ ] 기획 회신 대기분: AsBuff "증가량"·ManaRegen "회복량" 해석(%(p) vs 즉시 마나), `AS_BUFF_DURATION` 값, 탱커 4종 포켓몬별 스킬 세분, `SK_` 영웅스킬 행

### 태욱님 (상점/아이템/UI)
- [ ] **`ShopManager`/`GameEvents`/`RewardManager` 변경 검토** — 해인님이 아이템 리롤 모델 변경으로 수정했고, 여기에 영욱의 XP구매/리롤 요청 이벤트가 자동 병합으로 합쳐졌다. 컴파일·동작은 확인했지만 소유자 리뷰가 없다
- [ ] `ShopManager` 밸런스 테이블 확정 (`_requiredXpByLevel` `_unitCapByLevel` `_roundXpReward` `_buyXpCostGold` `_buyXpAmount`)
- [ ] 증강 선택 UI 정식화 + 배치입력 `IsChoiceBlocking` 배선
- [ ] PR #58의 `Shop Sell Area` 인스펙터 정렬

### Core/전투/보드 (인수자)
- [x] ~~PR #54 충돌 해결~~ — **7/28 완료.** `Pinut1/trade-ui-merge-0728` 병합만 하면 된다(§3). 재구현·재해결 금지
- [ ] `RoundPhaseManager` preReward 훅 연결
- [ ] 나인이볼부스트 v2 풀 연출 (진화체 8종 순차 소환+종별 버프) — 별도 티켓, 규모 큼
- [ ] 파치리스 적 전체 2초 어그로
- [ ] 🔺 **런타임 asmdef 분리** — 자동 테스트가 핵심 로직에 못 붙는 구조. 착수 조건과 함정은 `CLAUDE.md` 🔺 항목 참조. **여유 있을 때 시작할 것**(첫 시도에 Photon 참조 누락으로 컴파일 에러가 대량 발생한다)

### 실사 테스트 미완 (2클라이언트 Photon 필요, 자동화 불가)
- [ ] 재접속 시나리오 (PR #47)
- [ ] 파트너 보드 미러 (PR #48)
- [ ] 공용 상점 풀 회귀 (PR #42) — 예약/구매/반환, 마스터 교체
- [ ] 통신교환·골드 전송

---

## 8. 운영 정보

`Docs/HANDOFF_DAY1_2026-07-27.md`의 "Supabase 권한 인수인계"를 **단일 기준**으로 사용한다. 계정 정보는 문서에 기재하지 않고 별도 전달한다.

- **Photon PUN2**: App ID는 `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset`
- **Supabase**: URL/anon key는 `SupabaseMatchUploader` 인스펙터 값 (anon key는 공개 전제, RLS가 방어선). **Auth의 Anonymous sign-ins가 꺼지면 전적은 로컬 jsonl에만 남는다**

---

## 9. 문서 지도

| 알고 싶은 것 | 볼 문서 |
|---|---|
| 담당 분배·통신 규칙·PLACEHOLDER 수치 | `CLAUDE.md` |
| 오늘 기준 전체 상태 | **이 문서** |
| 시스템별 완료/미완 상세 | `HANDOFF_2026-07-20_final.md` (§2 정정 적용) |
| Core 잔여 작업 코드 대조 | `HANDOFF_2026-07-20_core-gaps.md` |
| 인수자 온보딩 절차 | `HANDOFF_DAY1_2026-07-27.md` / `DAY2` |
| 증강 시스템 | `Assets/_Project/Docs/AugmentSystem.md` |
| 진화·아이템 시스템 | `Assets/_Project/Docs/EvolutionSystem.md` / `ItemSystem.md` |
| 밸런스 검증 하네스 | `HANDOFF_2026-07-20_final.md` §4 "테스트 수단" |
| 전적 서버 스키마 | `Docs/SCHEMA_2026-07-10_supabase-matches.sql` |
