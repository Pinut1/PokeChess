# 구조도 — 샵 · 전투 · 합성 (2026-06-15 기준)

지금 동작하는 세 시스템(샵 / 전투 / 합성)과 그 연결을 정리한 구조도.
핵심 원칙: **모든 매니저는 `GameEvents`(이벤트 버스)로만 통신**하고 서로 직접 참조하지 않는다.

## 1. 컴포넌트 ↔ 이벤트 흐름

```mermaid
flowchart LR
    subgraph IN["입력 / 디버그"]
        DRAG["UnitDragController<br>(Pointer 드래그)"]
        SDBG["ShopDebugTest<br>(구매/리롤 버튼)"]
        READY["RoundPhaseManager<br>(Ready 버튼)"]
        BDBG["BattleDebugTest<br>(전투시작 버튼)"]
    end

    subgraph SHOP["🛒 샵"]
        SM["ShopManager<br>골드 / Roll / Buy / Reroll"]
        UF["UnitFactory<br>PokemonData→PokemonUnit"]
    end

    subgraph BOARD["🧩 보드 / 벤치 / 합성"]
        TILE["HexTile · BenchTile<br>(IDropTarget)"]
        BM["BoardManager<br>배치 · 스왑 · CheckEvolution(합성)"]
        BV["BoardView<br>transform 재배치"]
    end

    SYN["SynergyManager<br>시너지 재계산"]
    BT["BattleManager<br>미러 자동전투"]
    RPM["RoundPhaseManager<br>페이즈 FSM"]
    GE(["⚡ GameEvents (이벤트 버스)"])

    SDBG --> SM
    SM --> UF --> BM
    SM -. "GoldChanged / ShopRerolled" .-> GE

    DRAG --> TILE --> BM
    BM -. "UnitPlaced / UnitBenched" .-> GE
    GE -. "OnUnitPlaced/Benched" .-> SYN
    GE -. "OnUnitPlaced/Benched" .-> BV
    SYN -. "SynergyUpdated" .-> GE

    READY --> RPM
    BDBG -. "BattleStart" .-> GE
    RPM -. "BattleStart" .-> GE
    GE -. "OnBattleStart" .-> BT
    BT -. "BattleEnd(isWin)" .-> GE
    GE -. "OnBattleEnd" .-> RPM
    RPM -. "RoundChanged" .-> GE
    GE -. "OnRoundChanged" .-> SM
```

## 2. 한 라운드의 시간 흐름 (루프)

```mermaid
flowchart TD
    R["RoundChanged"] --> SHOPP["Shopping 페이즈"]
    SHOPP --> INC["ShopManager: 수입 지급 + Roll()"]
    INC --> BUY{"구매?"}
    BUY -->|"Buy(slot)"| FACT["UnitFactory.Create → 벤치 배치"]
    FACT --> MERGE{"보드+벤치에<br>같은 종·같은 성 3개?"}
    MERGE -->|"예"| STAR["CheckEvolution<br>별업 ★→★★→★★★ (연쇄)"]
    MERGE -->|"아니오"| DRAGP["드래그로 보드에 배치"]
    STAR --> DRAGP
    DRAGP --> SYNC["SynergyManager 재계산"]
    SYNC --> RDY["Ready 버튼"]
    RDY --> BATT["Battle: BoardSnapshot → 점대칭 미러 적팀 → 0.1s 틱 자동전투"]
    BATT --> ENDB["BattleEnd(승/패)"]
    ENDB --> RES["Result 페이즈"]
    RES --> R
```

## 3. 이벤트 발행/구독 표

| 이벤트 | 발행(쏨) | 구독(받음) → 하는 일 |
|---|---|---|
| `RoundChanged` | RoundPhaseManager(/Network) | **ShopManager**(수입+Roll), RoundPhaseManager(Shopping 진입) |
| `GoldChanged` / `ShopRerolled` | ShopManager | UI 표시 — 현재는 ShopDebugTest가 직접 읽음 |
| `UnitPlaced` / `UnitBenched` | BoardManager | **SynergyManager**(재계산), **BoardView**(위치 반영) |
| `SynergyUpdated` | SynergyManager | UI / (BattleManager는 전투시작 시 pull) |
| `AllPlayersReady` | Network(Ready) | RoundPhaseManager(Battle 진입) |
| `BattleStart` | RoundPhaseManager | **BattleManager**(시뮬 시작) |
| `BattleEnd(isWin)` | BattleManager | RoundPhaseManager(Result→다음 라운드) |

## 4. 한눈 요약
- **샵**: `RoundChanged`로 깨어나 골드 주고 샵 굴림 → `Buy`가 `UnitFactory`로 유닛 만들어 **벤치**에 넣음.
- **합성(일반 진화, GDD 4.2)**: 배치/벤치가 끝날 때마다 `BoardManager.CheckEvolution`이 **같은 종 3개**를 찾아
  **별업**(연쇄 가능). 별도 매니저가 아니라 배치의 부산물로 BoardManager 안에서 처리.
- **전투**: `Ready` → `BattleStart` → `BattleManager`가 보드 스냅샷을 **거울 복제**해 자동전투 → `BattleEnd` → 결과 → 다음 라운드로 **루프**.
- 세 시스템 모두 서로를 직접 모른다. `GameEvents`만 보고 반응 → 하나를 고쳐도 나머지에 영향 없음.

## 관련 문서
- 구현 플랜: `Docs/PLAN_2026-06-15_shop-bench-board-merge.md`
- 시너지 전투 버프(보류): `Docs/PLAN_2026-06-15_synergy-battle-buffs.md`
- 전투: `Docs/PLAN_2026-06-12_battle-manager.md`
