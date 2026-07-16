# PokeChess 증강 시스템 정리

## 개요
증강 라운드에 3개의 증강 중 1개를 선택해 영구 효과를 얻는 시스템.
**확정 7종 구현 완료(2026-07-16)** — 목록 출처: 덱기획 v1 (GAP_2026-07-01_slice-1-5.md §2.B).
선택은 로컬 전용(2인 각자 경제와 동일 규칙) — 파트너 증강 표시/동기화는 추후.

---

## 게임 내 흐름
```
증강 라운드 진입 (stage_data의 preReward=AugmentChoice, 현재 R2)
  → RewardManager가 AugmentManager.OfferChoice() 호출
    → 풀(7종 − 보유분)에서 3개 무작위 추첨 → GameEvents.AugmentOfferReady
      → 선택 UI 표시 (임시: AugmentOfferHud — AugmentManager가 자동 부착)
        → 플레이어 선택 → AugmentManager.SelectAugment()
          → AugmentFactory 생성 + Apply() + GameEvents.AugmentSelected (전적 기록 등)
```

---

## 확정 증강 7종
| AugmentId | 이름 | 등급* | 효과 | 연결 seam |
|------|------|------|------|------|
| `GoldInterest` | 이자 | Silver | 이자율 +1(10G당) + 즉시 50G | `Shop.AddInterestPerTenGold` / `AddGold` |
| `LevelDiscount` | 레벨 할인 | Silver | XP 구매 비용 -2G (PLACEHOLDER) | `Shop.AddBuyXpCostDiscount` |
| `RerollRefund` | 리롤 환급 | Silver | 리롤 소모 시 45% 환급 | `GameEvents.OnRerollSpent` → `Shop.AddReroll` |
| `PachirisuHero` | 영웅증강: 파치리스 | Prismatic | 탱커 전환 + 날따름 부여 + 즉시지급 1 + 리롤 3 | `PokemonUnit.ApplyParichisuHeroAugment` |
| `EeveeHero` | 영웅증강: 이브이 | Prismatic | 진화잠금 + ×1.4 + 3성 봇소환 + 즉시지급 1 + 리롤 3 | `PokemonUnit.ApplyEeveeHeroAugment` |
| `FourCostShopOpen` | 4코 상점 오픈 | Gold | 즉시 4코 5마리 + 4코 상시 등장(15%, PLACEHOLDER) | `Shop.ForceOpenCostFour` |
| `Quarry` | 채석가 | Silver | 매 라운드 진화의 돌 1개(무작위) | `Item.AddStone` |

*등급 배정은 기획 미명시 PLACEHOLDER.

### 남은 PLACEHOLDER / 기획 확인 필요
- [ ] 레벨 할인폭 (현재 -2G)
- [ ] 4코 강제 오픈 등장률 (현재 15%)
- [ ] 영웅증강 "전용리롤 3" 해석 — 현재 일반 무료 리롤 3개로 구현. 대상 종만 나오는 전용 상점이면 교체 필요
- [ ] 채석가 돌 종류 (현재 EvolutionStoneDatabase 무작위)
- [ ] 날따름 마나비용 (현재 30 — `PachirisuHeroAugment.TAUNT_MANA_COST`)
- [ ] 증강 등급(Tier) 공식 배정
- [ ] 2인 협동 시 파트너 증강 표시/동기화 여부

---

## 코드 구조
| 파일 | 역할 |
|------|------|
| `Data/AugmentData.cs` | AugmentId enum(7종) + 표시용 데이터 (ScriptableObject) |
| `Augments/Augment.cs` | 추상 베이스 — `Apply()` + 라이프사이클 훅 |
| `Augments/AugmentCatalog.cs` | 7종 표시 정보 정의(코드 생성) — 시트 확정 시 JSON 임포터로 전환 |
| `Augments/AugmentFactory.cs` | AugmentId → 구체 클래스 생성 |
| `Augments/Implementations/HeroAugment.cs` | 영웅증강 공통 골격(즉시지급/리롤/태그) |
| `Managers/AugmentManager.cs` | 오퍼 추첨, 활성 증강 관리, GameEvents 브릿지 |
| `UI/AugmentOfferHud.cs` | 임시 3택1 OnGUI — 정식 UI(태욱, UIManager) 이관 대상 |

### 새 증강 추가 방법
1. `AugmentId` enum에 항목 추가
2. `Implementations/` 폴더에 클래스 작성, 필요한 훅만 오버라이드
3. `AugmentFactory`에 case 한 줄 추가
4. `AugmentCatalog`에 표시 정보(이름/설명/티어) 추가

### 라이프사이클 훅 (필요한 것만 오버라이드)
```csharp
public virtual void Apply()                      // 선택 시 1회
public virtual void OnRoundChanged(int round)    // 매 라운드
public virtual void OnBattleStart()              // 전투 시작
public virtual void OnBattleEnd(bool isWin)      // 전투 종료
public virtual void OnUnitPlaced(PokemonUnit u)  // 유닛 보드 배치
public virtual void OnUnitBenched(PokemonUnit u) // 유닛 벤치 배치 (구매/보상 획득 포함)
public virtual void OnUnitSold(PokemonUnit u)    // 유닛 판매
public virtual void OnRerollSpent()              // 수동 리롤 소모 (리롤 환급용)
```

---

## 데이터 관리
현재는 **AugmentCatalog(코드)** 가 7종 정의부 — 기획 시트가 없어 JSON 원본이 성립하지 않음.
증강 시트가 생기면 기존 파이프라인(구글시트 → `augment_data.json` → `Import Augment JSON` → 중앙 DB SO)으로 전환하고
AugmentCatalog를 제거한다. `augmentId` 값은 반드시 `AugmentId` enum + `AugmentFactory` case와 일치해야 함.

---

## 담당
- **구조 / 로직**: 김영욱 (황해인 파트 대행, 2026-07-16 구현)
- **선택 UI 정식화**: 김태욱 (UI) — `AugmentOfferHud` 로직을 UIManager로 이관
- **증강 목록/수치 기획**: 황해인 (기획)
