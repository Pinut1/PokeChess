# PokeChess 증강 시스템 정리

## 개요
특정 라운드마다 3개의 증강 중 1개를 선택해 영구 효과를 얻는 시스템.  
**MOCKUP 상태** — 증강 목록 및 세부 수치는 기획 확정 후 채워질 예정.

---

## 게임 내 흐름
```
특정 라운드 도달
  → 증강 선택지 3개 제공
    → 플레이어가 1개 선택
      → 즉시 효과 적용
        → 이후 라운드 내내 지속
```

---

## 등급 (Tier)
| 등급 | 설명 |
|------|------|
| Silver | 기본 증강 |
| Gold | 강력한 증강 |
| Prismatic | 최상위 증강 |

---

## 코드 구조
| 파일 | 역할 |
|------|------|
| `AugmentData.cs` | 이름 / 등급 / 아이콘 등 표시용 데이터 (ScriptableObject) |
| `Augment.cs` | 모든 증강의 추상 베이스 — `Apply()` + 라이프사이클 훅 |
| `AugmentFactory.cs` | AugmentId → 구체 클래스 생성 |
| `AugmentManager.cs` | 활성 증강 관리, GameEvents 브릿지 |

### 새 증강 추가 방법
1. `AugmentId` enum에 항목 추가
2. `Implementations/` 폴더에 클래스 작성, 필요한 훅만 오버라이드
3. `AugmentFactory`에 case 한 줄 추가

### 라이프사이클 훅 (필요한 것만 오버라이드)
```csharp
public virtual void Apply()                      // 선택 시 1회
public virtual void OnRoundChanged(int round)    // 매 라운드
public virtual void OnBattleStart()              // 전투 시작
public virtual void OnBattleEnd(bool isWin)      // 전투 종료
public virtual void OnUnitPlaced(PokemonUnit u)  // 유닛 배치
public virtual void OnUnitSold(PokemonUnit u)    // 유닛 판매
```

---

## 예시 증강 (목업)
| 이름 | 등급 | 효과 |
|------|------|------|
| HP 강화 | Silver | 모든 유닛 최대 체력 +150 |
| 공격 강화 | Silver | 모든 유닛 공격력 +10% |
| 이자 수입 | Silver | 라운드마다 보유 골드의 10% 추가 지급 (최대 5골드) |
| 전투 회복 | Gold | 전투 시작 시 모든 유닛 체력 20% 회복 |

---

## 데이터 관리 (JSON)

### 방식
증강 120개를 개별 SO로 관리하면 파일이 너무 많아짐.  
→ **AugmentDatabase** (SO 1개) 안에 전체 목록을 리스트로 관리.  
기존 구글시트 → JSON → 임포터 파이프라인 그대로 사용.

### 스프레드시트 (Augment 시트) 컬럼
```
id | name | nameEn | augmentId | tier | description
```

| 컬럼 | 설명 |
|------|------|
| `augmentId` | 코드 클래스와 연결되는 키 (예: `HpBonus`, `GoldInterest`) |
| `tier` | `Silver` / `Gold` / `Prismatic` |

### JSON 예시
```json
[
  {
    "id": 1,
    "name": "HP 강화",
    "nameEn": "HP Boost",
    "augmentId": "HpBonus",
    "tier": "Silver",
    "description": "모든 유닛 최대 체력 +150"
  }
]
```

### 주의
- `augmentId` 값은 반드시 `AugmentId` enum + `AugmentFactory` case와 일치해야 함
- 새 증강 추가 시 시트 → 코드 순서로 작업

### JSON 파일 위치
```
Assets/Resources/Data/augment_data.json
```

### Unity 임포트 메뉴
```
PokeChess/Import Augment JSON   ← 기획 확정 후 구현 예정
```

---

## 기획팀 확정 필요 사항
- [ ] 증강 제공 라운드 (TFT 기준: 2-1, 3-2, 4-2)
- [ ] 2인 협동 시 증강 선택 방식 — 둘이 같이 고르는지 / 각자 따로 고르는지
- [ ] 전체 증강 목록 및 수치

---

## 담당
- **구조 / 로직**: 김영욱 (Core)
- **선택 UI**: 김태욱 (UI)
- **증강 목록 기획**: 황해인 (기획)
