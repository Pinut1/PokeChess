# 샵 → 벤치 → 보드 드래그앤드롭 루프 + 유닛 합체(일반 진화) (2026-06-15)

## Context
플레이 가능한 쇼핑 루프가 없음. 현황:
- `ShopManager` = 빈 스텁. 골드 보유 주체 없음 (`GameEvents.OnGoldChanged`만 존재).
- 드래그 입력 컨트롤러(아키텍처 주석의 "PlayerController") 부재. `IDropTarget`은 `HexTile`에만 연결돼 있고 호출자 없음.
- `BoardManager.TryPlaceUnit/TryPlaceInBench`는 **논리 배치만** 함(transform 미이동). 디버그가 수동으로 옮겨왔음.
- 벤치 시각 타일 없음(논리 배열 `_bench`만). 포켓몬 에셋/`pokemon_data.json` 없음 → 런타임 테스트 데이터 필요.
- 유닛은 절차적으로 생성(`new GameObject`+`AddComponent<PokemonUnit>`+primitive).

GDD 4.2 확정: **일반 진화 = 동일 종 3개 합성 → 별업(1성→2성→3성)**. `StarMultiplier` 이미 지원.
(진화의 돌/통신 진화는 별도, 이번 범위 밖.)

## 설계 (단계별)

### 1단계 — 경제 + 샵 (`ShopManager`)
- 골드 보유: `public int Gold { get; private set; }`. 시작 골드/리롤비용/라운드 수입 인스펙터 노출.
- 샵 풀: `[SerializeField] List<PokemonData> _pool`(디자이너), `_shopSize=5`.
- `Roll()` 풀에서 랜덤 N개 공개 → `GameEvents.ShopRerolled` 발화.
- `bool Buy(int slot)`: 골드≥cost & 벤치 여유 검사 → `UnitFactory.Create` → `Board.TryPlaceInBench` →
  골드 차감(`GoldChanged`) → 슬롯 비움. (벤치 배치 시 `OnUnitBenched` 발화 → 합체 검사 트리거)
- `bool Reroll()`: 골드≥리롤비용 → 차감 → `Roll()`.
- `OnRoundChanged` 구독: 수입 지급 + 자동 `Roll()`.

### 2단계 — 유닛 생성 + 벤치 시각화
- `UnitFactory`(신규): `PokemonUnit Create(PokemonData, int star=1)` — `modelPrefab` 있으면 사용, 없으면
  primitive 캡슐. `AddComponent<PokemonUnit>` + data/star 세팅. (디버그 테스트 패턴을 재사용 가능하게 추출)
- `BenchTile`(신규): `MonoBehaviour, IDropTarget`. 슬롯 인덱스 + 콜백 `Action<PokemonUnit,int>` 보유.
- `BoardManager.GenerateBench()`: 보드 아래쪽에 `_benchSize`개 타일을 한 줄로 생성, 콜백 →
  `TryPlaceInBench(unit, slot)`. `BenchSlotWorldPosition(int)` + `GetBenchSnapshot()` 추가.
- `BoardView`(신규): `OnUnitPlaced/OnUnitBenched/OnUnitSold` 구독 → 보드+벤치 스냅샷 기준으로 **모든 유닛
  transform 전체 재배치**(≤37칸이라 전체 resync로 충분, 스왑도 자동 처리). 논리/뷰 분리 유지.

### 3단계 — 드래그앤드롭 (`UnitDragController`)
- `Update`에서 카메라 레이캐스트.
  - 마우스 다운: 히트 콜라이더의 `GetComponentInParent<PokemonUnit>()` → 집기(살짝 들어올림). 쇼핑 페이즈만 허용.
  - 드래그 중: 지면 평면(y=0) 투영 좌표로 유닛 이동 + hover 시 `IDropTarget.OnHoverEnter/Exit`.
  - 마우스 업: 히트의 `IDropTarget` → `OnDropUnit(held)`. 타일/벤치 콜백이 `BoardManager`로 위임 →
    이벤트 발화 → `BoardView`가 재배치. 유효 타겟 없으면 논리 상태 불변 → resync로 원위치 복귀.
- 콜라이더 필요(유닛/타일/벤치). primitive는 자동 포함. `HexTile` 프리팹은 콜라이더 확인 필요(씬 셋업 체크리스트).

### 4단계 — 유닛 합체(일반 진화) — `BoardManager.TryEvolve`
- 배치/벤치 성공 직후 `TryEvolve(data.id, starLevel)` 호출.
- 보드+벤치 통틀어 같은 `data.id` & 같은 `starLevel` 유닛 수집. ≥3 & star<3이면:
  - 3개 선택(방금 놓은 유닛 우선 생존), 나머지 2개 위치 비우고 GameObject 파괴.
  - 생존 유닛 `starLevel+1`, `ResetForBattle()`로 HP 재계산.
  - 셋 중 보드에 있던 게 있으면 보드 좌표에, 없으면 벤치 슬롯에 생존자 재배치 → 이벤트 발화.
  - 재귀 검사(3×2성 → 3성 연쇄).

### 디버그/검증 — `ShopDebugTest`(신규, OnGUI)
- 골드 표시, "구매(슬롯0~4)", "리롤", 현재 샵 목록 표시. 드래그 미연동 환경에서도 루프 검증 가능.

## 검증 (Play 모드, 솔로모드)
1. 라운드 시작 → 샵 5칸 공개 + 시작 골드. 구매 → 골드 차감 + 벤치에 유닛 등장.
2. 같은 종 3개 구매 → 자동 별업(2성), 9개 → 3성 연쇄 확인.
3. 벤치 유닛 드래그 → 보드 타일 드롭 → 배치 + 시너지 재계산. 보드↔보드 스왑, 벤치↔보드 교체 동작.
4. 빈 곳/무효 드롭 시 원위치 복귀. 벤치 가득 시 구매 실패 로그.

## 씬 셋업 체크리스트 (코드로 불가 — Unity 에디터 필요)
- `ShopManager` 컴포넌트에 `_pool`(PokemonData 에셋들) 할당. 없으면 `ShopDebugTest`의 런타임 시드 사용.
- `HexTile` 프리팹에 Collider 존재 확인(레이캐스트용). 없으면 추가.
- `UnitDragController`/`BoardView`를 씬 매니저 오브젝트에 추가. 메인 카메라 태그 확인.

## 수정/생성 파일
| 파일 | 작업 |
|---|---|
| `Docs/PLAN_2026-06-15_shop-bench-board-merge.md` | 신규(플랜) |
| `Managers/ShopManager.cs` | 스텁 → 경제+샵 구현 |
| `Core/UnitFactory.cs` | 신규(유닛 생성 팩토리) |
| `Core/BenchTile.cs` | 신규(IDropTarget) |
| `Managers/BoardManager.cs` | 벤치 생성/좌표/스냅샷 + TryEvolve 추가 |
| `Core/BoardView.cs` | 신규(이벤트 구독 → transform 재배치) |
| `Core/UnitDragController.cs` | 신규(마우스 드래그앤드롭) |
| `Debug/ShopDebugTest.cs` | 신규(OnGUI 검증 UI) |
