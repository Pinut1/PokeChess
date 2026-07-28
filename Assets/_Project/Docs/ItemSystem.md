# PokeChess 아이템 시스템 정리

## 아이템 종류 (3가지로 분리)

| 종류 | 설명 | 시트 | SO 클래스 |
|------|------|------|----------|
| **Item** | 유닛에 장착하는 일반 아이템 (열매류) | `Item` | `ItemData` |
| **Consumable** | 1회 사용 소모 아이템 | `Consumable` | `ConsumableData` |
| **EvolutionStone** | 장착 시 포켓몬 진화, 해제 시 복구 | `EvolutionStone` + `EvolutionMap` | `EvolutionStoneData` |

---

## Item JSON (GeneralItem 시트)
```
id | name | nameEn | description | stats[]
```
- `stats[]`: `{ key, value, isPercent, hasValue }`
- GeneralItem 원본의 복합 스탯을 개수 제한 없이 보존한다.
- 구형 `statKey/statValue/statKey2/statValue2` JSON도 임포터에서 계속 지원한다.

### GeneralItem 공통 약어
```
HP, AD, AP, AS, DEF, MP, VMP, CRT, AMP
```

현재 전투 런타임에 바로 반영되는 조합은 flat `HP/AD/DEF`, percent `AP/AS/CRT`다.
`MP/VMP/AMP`, flat AP, percent AD/DEF는 `sourceStats`에 보존되지만 효과 구현은 별도 작업이다.

---

## Consumable 시트 컬럼
```
id | name | nameEn | consumableType | description
```

### consumableType 예시 (기획 확정 후 추가)
```
duplicate_unit  → 메타몽 (유닛 복제)
reforge_item    → 재조합기 (아이템 해제)
```

---

## EvolutionStone 시트 컬럼
```
id | name | nameEn | stoneType | description
```

## EvolutionMap 시트 컬럼
```
stoneName | targetPokemon | evolvedPokemon
```

### 예시
```
WaterStone | Eevee    | Vaporeon
WaterStone | Slowpoke | Slowbro
WaterStone | Staryu   | Starmie
```

---

## JSON 파일명 (Resources/Data/)
```
item_data.json
consumable_data.json
evolution_stone_data.json
evolution_map_data.json
```

## Unity 임포트 메뉴
```
PokeChess/Import Item JSON
PokeChess/Import Consumable JSON
PokeChess/Import EvolutionStone JSON
```

---

## MOCKUP (기획 확정 후 수정 필요)
- `ConsumableData.consumableType` — 현재 string, 나중에 enum으로 교체
- `Consumable/Implementations/*.cs` — 효과 로직 미구현
