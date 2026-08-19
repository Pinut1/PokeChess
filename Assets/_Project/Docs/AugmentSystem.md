# PokeChess 증강 시스템 정리

## 개요
증강 라운드에 3개의 증강 중 1개를 선택해 영구 효과를 얻는 시스템.
**확정 6종 — Augment Table v2 (2026-07-16 해인님 회신) 반영.** 등급은 전체 SUPERBALL(=Gold) 단일.
선택은 로컬 전용(2인 각자 경제와 동일 규칙). 파트너 증강 동기화는 완료(7/17, PR #43) — Player CustomProperties 미러, `GameEvents.OnPartnerAugmentsChanged`, 전적 partner.augments 기록.

---

## 게임 내 흐름
```
증강 라운드 진입 (stage_data의 preReward=AugmentChoice, 현재 1-2)
  → RewardManager가 AugmentManager.OfferChoice() 호출
    → 풀(6종 − 보유분)에서 3개 무작위 추첨 → GameEvents.AugmentOfferReady
      → 선택 UI 표시 (임시: AugmentOfferHud — AugmentManager가 자동 부착)
        → 선택 / 카드 내려두기(블로킹 해제) / 1분 초과·전원 Ready 시 랜덤 자동 선택
          → AugmentFactory 생성 + Apply() + GameEvents.AugmentSelected (전적 기록 등)
```

### 블로킹 UX (기획 확정 2026-07-16, 롤체 기준)
- 선택 창이 떠 있는 동안 조작 블로킹 — `AugmentManager.IsChoiceBlocking`
  - 임시 HUD는 화면 전체 클릭을 모달로 흡수(다른 OnGUI 차단)
  - ⚠️ **3D 배치 입력(레이캐스트)은 IMGUI로 못 막음** — 입력 처리 측이 `IsChoiceBlocking` 참조 필요(후속 배선)
- "▼ 카드 내려두기" 버튼으로 블로킹 해제(하단 복귀 버튼 표시), 타이머는 계속 진행
- **1분 초과** 또는 **전원 Ready(전투 진입)** 시 미선택이면 랜덤 자동 선택
  - 정확한 기획은 "로컬 Ready 클릭 시" — Ready 로컬 신호가 이벤트화되면 그 시점으로 개선

---

## 확정 증강 6종 (Augment Table v2)
| AugmentId(v2) | 이름 | 발동 | 효과 | 연결 seam |
|---|---|---|---|---|
| `ECONOMY_GOLD` | 골드를 획득했다! | 즉시 | +50G, 이자율 1→2(10G당) | `Shop.AddInterestPerTenGold`/`AddGold` |
| `ECONOMY_SHOP` | 구독서비스 | 즉시 + 1~3R 중 1회 (총 2회 확정) | 서로 다른 4코 5마리 상점 오픈 + 회당 4G | `Shop.OpenCostFourShopOnce` |
| `HERO_PACHIRISU` | 기술머신:날따름 | 즉시 | 파치리스 지급 + 탱커 전환 + 날따름 + ×1.4 | `PokemonUnit.ApplyParichisuHeroAugment` |
| `HERO_EEVEE` | 기술머신:나인이볼부스트 | 즉시 | 이브이 지급 + 진화잠금 + 마법사 전환 + ×1.4 + 3성 봇소환 | `PokemonUnit.ApplyEeveeHeroAugment` |
| `REROLL_TICKET` | 하이퍼 티켓 | 즉시(+상시) | 무료 리롤 +5, 리롤 시 45% 환급 | `Shop.AddReroll` / `OnRerollSpent` |
| `GAMBLE_STONE` | 진화 스페셜리스트 | R2~5 매 라운드 | 돌 1개/라운드(최대 4) + 최초 재조합기+2G | `Item.AddStone`/`AddReforger` |

### v2 반영으로 확정/정리된 것 (2026-07-16)
- ~~레벨 할인~~ **삭제** — v2 목록에 없음 (`Shop.AddBuyXpCostDiscount`도 제거)
- 구독서비스: 구 "15% 지속 확률 등장" → **확정 발동 2회**로 정정 (확률 주입 로직 제거)
- 영웅증강 "전용리롤 3": 전용 상점 아님 — **일반 무료 리롤 3개가 맞는 구현** (HeroAugment 유지)
- 영웅증강 둘 다 ×1.4 (파치리스에도 배수 적용), 이브이 역할 → **마법사**
- 채석가(→진화 스페셜리스트): 돌 무작위·매 라운드 지급 맞는 구현 + 최초 재조합기+2G 추가
- 날따름 마나 30: 임시 유지 (엑셀 밸런스 조정 후 확정값 재전달 예정)
- 역할군 변경 시 스탯: **역할 태그만 변경(타겟 우선순위), 스탯은 종 원본 ×1.4 — 별도 스탯 테이블 없음** (개발 회신)

### 별도 티켓 (전투 신규 메커니즘 — 미구현)
- [x] ~~이브이 v2 "나인이볼부스트"~~ ✅ 구현(8/19): 영웅 이브이가 **스킬 1회 시전마다 진화체 1종을
      순서대로 봇 소환** + 그 종에 대응하는 버프를 **이브이 자신에게**(봇이 아님) 부여.
      8종 전부 **스킬 시전으로만** 나온다(전투 시작 즉발 소환 없음).
      성급별 마나코스트 **80/60/30**(1/2/3성)이 "몇 종까지 닿는가"를 가른다 — 1성 5종 / 2성 6종 /
      **3성만 8종 완성(24초)**(기획 의도: 성급을 올릴 이유). 전투 길이는 최대 40 시뮬초(정규 30 + 오버타임 10).
      순서·수치 = `Data/HeroEeveeBoostTable.cs`, 전투 로직 = `BattleManager.TryCastHeroEeveeBoost`.
      봇은 돌연변이 봇과 같은 형식(source=null)이라 전투가 끝나면 `Cleanup()`에서 사라진다.
      전용 VFX 없음 — 소환되는 진화체 자체가 연출(기획 확정 8/19).
      같이 해결: 진화의 돌 면역, "가장 강한 이브이" 대상 선정(→ `HeroAugment` 이동 효과 모델).
      ※ 「돌연변이 시너지 → 고유 시너지 전환」은 **계획 없음으로 확정**(8/19) — 대신 이브이 영웅증강
        보유 시 돌연변이를 비활성화한다(`SynergyManager.SuppressMutantIfHeroEevee`).
- [ ] `SK_EEVEE_HERO` 스킬화(시트 행 추가). 지금은 이브이 원본 스킬(Celebrate, 마나 60)의 **시전
      타이밍만** 빌려 쓰고 효과는 코드가 통째로 대체한다 — 동작에는 문제 없고, 마나코스트를 따로
      잡고 싶을 때 필요해지는 작업이다.
- [x] ~~자뭉열매~~ ✅ 구현(7/17): 전투당 1회, HP 45% 미만 시 언타겟+행동불능 + 매초 maxHP 15% 회복,
      **완전 회복하거나 다른 아군이 없으면 복귀**(기획 확정 7/17 — TFT 블리츠크랭크식). `BattleUnit.BERRY_*`
- [ ] 파치리스 v2 잔여: 투사체 끌어당김(수치 미확정), `SK_PACHIRISU_HERO`
- [ ] skill_table에 `SK_PACHIRISU_HERO`/`SK_EEVEE_HERO` 행 추가(시트 반영)
- [ ] 3D 배치 입력의 `IsChoiceBlocking` 참조 배선

---

## 코드 구조
| 파일 | 역할 |
|------|------|
| `Data/AugmentData.cs` | AugmentId enum(6종, v2 id와 1:1) + 표시용 데이터 (ScriptableObject) |
| `Augments/Augment.cs` | 추상 베이스 — `Apply()` + 라이프사이클 훅 |
| `Augments/AugmentCatalog.cs` | 6종 표시 정보 정의(코드 생성) — 시트 확정 시 JSON 임포터로 전환 |
| `Augments/AugmentFactory.cs` | AugmentId → 구체 클래스 생성 |
| `Augments/Implementations/HeroAugment.cs` | 영웅증강 공통 골격(즉시지급/리롤 3/고정·이동 효과 분리 + 대상 선정) |
| `Data/HeroEeveeBoostTable.cs` | 나인이볼부스트 소환 순서 8종 + 종별 버프 수치(밸런스 조정은 이 표만) |
| `Managers/AugmentManager.cs` | 오퍼 추첨, 블로킹/타임아웃/자동선택, 활성 증강 관리, GameEvents 브릿지 |
| `UI/AugmentOfferHud.cs` | 임시 3택1 OnGUI(모달+내려두기) — 정식 UI(태욱, UIManager) 이관 대상 |

### 새 증강 추가 방법
1. `AugmentId` enum에 항목 추가 (v2 augmentId와 이름 맞춤)
2. `Implementations/` 폴더에 클래스 작성, 필요한 훅만 오버라이드
3. `AugmentFactory`에 case 한 줄 추가
4. `AugmentCatalog`에 표시 정보(이름/설명) 추가

### 라이프사이클 훅 (필요한 것만 오버라이드)
```csharp
public virtual void Apply()                      // 선택 시 1회
public virtual void OnRoundChanged(int round)    // 매 라운드
public virtual void OnBattleStart()              // 전투 시작
public virtual void OnBattleEnd(bool isWin)      // 전투 종료
public virtual void OnUnitPlaced(PokemonUnit u)  // 유닛 보드 배치
public virtual void OnUnitBenched(PokemonUnit u) // 유닛 벤치 배치 (구매/보상 획득 포함)
public virtual void OnUnitSold(PokemonUnit u)    // 유닛 판매
public virtual void OnRerollSpent()              // 수동 리롤 소모 (환급용)
```

---

## 데이터 관리
현재는 **AugmentCatalog(코드)** 가 6종 정의부 — 원본은 `Augment Table v2` md 문서(해인님).
시트/JSON 파이프라인이 생기면 v2 컬럼(`deliveryType`/`triggerTiming` 포함) 기준으로 임포터 전환하고
AugmentCatalog를 제거한다. `augmentNameEn`이 v2 `augmentId` 문자열과 1:1 (전적 기록에도 이 값 사용).

---

## 담당
- **구조 / 로직**: 김영욱 (황해인 파트 대행, 2026-07-16 구현 + v2 반영)
- **선택 UI 정식화**: 김태욱 (UI) — `AugmentOfferHud` 로직을 UIManager로 이관 (+배치 입력 블로킹 배선)
- **증강 목록/수치 기획**: 황해인 (기획) — 원본: Augment Table v2
