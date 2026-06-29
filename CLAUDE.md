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

## 핵심 규칙
- **매니저끼리 직접 참조 금지** — 반드시 `GameEvents`를 통해 통신
- 새 이벤트는 `GameEvents.cs`에만 추가
- 다른 파트 매니저 건드릴 때는 담당자에게 먼저 확인 (Core/Network/전투/보드 = 영욱, 상점/아이템/UI = 태욱)

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

## 작업 분배 (태욱 — 상점/아이템/UI)
레벨/XP 시스템(PR #13) 후속으로 태욱이 이어받을 작업:
- **XP 변경 이벤트화**: 현재 HUD는 `ShopManager.CurrentXp`를 매 프레임 폴링 중. `GameEvents.XpChanged(CurrentXp, RequiredXp)` 추가 후 UI를 이벤트 구독으로 전환. (캡은 이미 `OnUnitCapChanged`로 이벤트화됨)
- **밸런스 테이블 확정**: `ShopManager`의 `_requiredXpByLevel`, `_unitCapByLevel`, `_roundXpReward`, `_buyXpCostGold`, `_buyXpAmount`는 임시값 — 기획 확정 후 조정.
- **XP 구매 UI 정식화**: `PrototypeHud`의 임시 IMGUI XP 구매 버튼을 정식 `UIManager` 연동으로 교체.

## 데이터 파이프라인
구글 시트 → JSON → `Assets/Resources/Data/` → `PokeChess/Import *` 메뉴 실행 → ScriptableObject 자동 생성  
SO 저장 경로: `Assets/_Project/ScriptableObjects/`

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
