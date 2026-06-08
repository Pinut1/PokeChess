# PokeChess 진화 시스템 정리

## 진화 종류 (2가지)

| 종류 | 조건 | 예시 |
|------|------|------|
| **별 강화 진화** | 같은 기물 3개 수집 | 팽도리 3개 → 앰라이트 |
| **진화의 돌 진화** | 돌 장착 / 해제 시 복구 | 이브이 + 물의돌 → 샤미드 |

---

## 1. 별 강화 진화

같은 포켓몬 3마리를 모으면 다음 단계 포켓몬으로 교체됨.  
완전히 다른 PokemonData로 전환 (스탯, 모델, 스킬 전부 교체).

### 진화 체인 예시
```
팽도리 (3개) → 앰라이트 (3개) → 앰패트르 (최종)
이브이  → 진화의 돌로만 진화 (별 강화 진화 없음)
```

### 단계별 구분
- **1단계 진화 포켓몬** (한 번만 진화): 2성(3마리)에 진화
- **2단계 진화 포켓몬** (두 번 진화): 각 단계마다 3마리 수집 시 진화

### 데이터 구조 (PokemonData)
```csharp
public string evolvesIntoEn;  // 진화 후 포켓몬 영문명. 최종형은 빈 문자열.
```

### 스프레드시트 (Pokemon 시트)
| nameEn | evolvesInto |
|--------|-------------|
| Piplup | Prinplup |
| Prinplup | Empoleon |
| Empoleon | _(비워둠 — 최종형)_ |
| Eevee | _(비워둠 — 돌로만 진화)_ |

### 구현 담당
`ShopManager` (태욱) — 3마리 합성 시 `evolvesIntoEn` 읽어서 PokemonData 교체

---

## 2. 진화의 돌 진화

돌을 유닛에 장착하면 지정된 포켓몬으로 진화.  
돌을 해제하면 원래 포켓몬으로 복구.

### 조건
- 해당 포켓몬이 `EvolutionStoneData.mappings`에 존재해야 함
- 돌의 `targetPokemon`과 유닛의 `pokemonNameEn`이 일치해야 장착 가능

### 데이터 구조 (EvolutionStoneData)
```csharp
public string stoneNameEn;
public string stoneType;
public List<EvolutionMapping> mappings;

// 헬퍼
public string GetEvolvedPokemon(string targetNameEn);
```

### 스프레드시트 (EvolutionStone 시트)
같은 돌에 대상 포켓몬이 여러 마리면 행을 나눠서 작성.

| id | nameEn | stoneType | targetPokemon | evolvedPokemon |
|----|--------|-----------|--------------|----------------|
| 1 | WaterStone | water | Eevee | Vaporeon |
| 1 | WaterStone | water | Slowpoke | Slowbro |
| 1 | WaterStone | water | Staryu | Starmie |

### 구현 담당
`ItemManager` (태욱) — 장착/해제 시 `GetEvolvedPokemon()` 호출 후 PokemonData 교체

---

## JSON 파일 (Resources/Data/)
```
pokemon_data.json          ← evolvesInto 컬럼 포함
evolution_stone_data.json  ← 돌 정보 + 매핑 합본
```

## Unity 임포트 메뉴
```
PokeChess/Import Pokemon JSON
PokeChess/Import EvolutionStone JSON
```
