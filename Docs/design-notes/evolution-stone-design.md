---
name: project-evolution-stone-design
description: 진화의 돌 = LoL식 장착템(되돌릴 수 있는 진화) 설계 확정 + 머지 상호작용 백로그
metadata: 
  node_type: memory
  type: project
  originSessionId: 0eea62d2-a810-4c58-9601-6de12fae4bf7
---

진화의 돌(EvolutionStoneData)을 **LoL 장착템처럼** 다루기로 확정(6/22). 일반 3머지([[project_meeting_0617]] 진화 3분할 중 하나)·통신교환과 달리 **되돌릴 수 있는** 진화 루트.

## 핵심 설계 — `data` 스왑 + 베이스 기억
`PokemonUnit`(Core, 영욱 파트)에 추가:
- `List<ItemData> items` + `EvolutionStoneData equippedStone` + `PokemonData preStoneData`
- **슬롯 2칸**(롤체 3칸 → 우리 2칸). 조합: 진돌+장착템 ✅ / 장착템+장착템 ✅ / **진돌+진돌 ❌**(유닛당 돌 최대 1개)
- 장착: `preStoneData=data` → `data=PokemonDatabase.Instance.GetByNameEn(evolvedEn)` → `equippedStone=stone`
- 제거기: `data=preStoneData` → 참조 null → **돌 인벤 반환**(재사용 가능)
- 판매: 돌 있으면 원복+돌 반환 → 장착템도 인벤 반환 → **베이스 코스트로 정산**
- 장착 검증: `HasFreeSlot` && (진돌이면 `equippedStone==null`) && `stone.GetEvolvedPokemon(data.pokemonNameEn)!=null`(잘못된 대상 거부)

## 스탯/스킬은 공짜로 따라옴
`PokemonUnit`이 모든 값을 `data` 한 곳에서 읽음(MaxHp/Attack=`data.x * StarMultiplier`, 스킬=`data.skill`, 시너지=`data.synergies`). **`data` 통째 스왑 → 스탯·스킬·시너지 자동 전환.** `starLevel` 유지 → 2성 이브이=2성 부스터, 3성=3성 부스터. BattleManager/SynergyManager **수정 불필요**.

## 데이터 측 필수 작업
1. 진화체(샤미드/부스터 등)도 포켓몬 시트에 자기 행 + 스탯 + 스킬 → PokemonDatabase.all에 1급 엔트리
2. 진화체는 ShopManager `_pool`에서 제외(직접 구매 불가). 시트에 `shopBuyable`(bool) 또는 `obtainBy`(shop/stone/trade) 컬럼 추가 → 임포터가 DB엔 전부, `_pool`엔 buyable만. **통신교환 진화체도 같은 컬럼으로 커버.**

## 머지 상호작용 (BoardManager.CheckEvolution — 매칭=data.id+starLevel, private, place/bench에서만 호출)
- **[필수] 돌 제거가 머지 트리거 안 됨** → `GameEvents.OnUnitChanged(unit)` 신설, BoardManager 구독→CheckEvolution. 안 하면 돌 빼서 원복해도 이브이 3마리 안 합쳐짐.
- **[필수] 돌 낀 유닛 머지 제외**: CheckEvolution 필터에 `equippedStone==null` 추가. 안 하면 2성 부스터 3마리가 data.id로 일치→3성 부스터 합체되며 **돌 3개 Destroy로 증발**. 제외하면 "머지하려면 돌부터 빼라" = 의도된 흐름.
- **[백로그·태욱] 머지 시 장착템 소멸**: CheckEvolution이 소비 2마리를 Destroy(456줄)하면서 그 위 아이템도 사라짐(기존부터 있던 구멍). 아이템 시스템 들어가면 "머지 시 아이템 생존자/인벤 이전" 필요.

## 구현 완료 (6/22, 영욱 파트)
- `PokemonUnit`: 슬롯 필드(items/equippedStone/preStoneData) + UsedSlots/HasFreeSlot/IsStoneEvolved + `TryEquipItem`/`RemoveItem`/`TryEquipStone`/`RemoveStone`/`PrepareForSell`. data 스왑+원복+OnUnitChanged 발화 다 됨.
- `PokemonData.shopBuyable`(기본 true) 추가. `PokeChessImporter`: PokemonEntry.obtainBy 필드+매핑("" / shop→true, stone/trade→false, 기존 JSON 안전).
- `GameEvents.OnUnitChanged(PokemonUnit)` 신설(+UnitChanged 헬퍼). EquipStone/RemoveStone에서 발화.
- `EvolutionStoneData.GetEvolvedPokemon` 대소문자 무관 매칭으로 수정.
- **미컴파일 검증**(Unity 안 돌림). 순수 추가라 기존 영향 없음 예상.

## 남은 연결 (담당자 대기)
- 🟢 BoardManager: `OnUnitChanged` 구독→CheckEvolution 재실행 / 머지 필터에 `equippedStone==null` 추가 (둘 다 시나리오 동작 필수)
- 🟡 태욱 ItemManager: 제거기→RemoveStone 호출+돌 인벤반환, 판매→PrepareForSell 호출, 풀 구성 `PokemonDatabase.all.FindAll(p=>p.shopBuyable)`, 머지 시 아이템 이전
- 🔵 기획: 진화체 행 스탯/스킬, obtainBy 컬럼, 돌↔포켓몬 영문명 철자일치

## 파트 경계
슬롯/베이스 데이터=PokemonUnit(Core, 영욱). 장착/제거 오케스트레이션·제거기·인벤=ItemManager(태욱, 현재 빈 스텁). BoardManager 진화 로직 손댐=담당자 확인 필요. data 스왑 로직은 PokemonUnit 메서드로 빼서 ItemManager가 호출하는 형태 권장.
