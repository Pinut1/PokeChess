# SynergyManager 구현 (크리티컬 패스 C) — 2026-06-12

## Context

어제(6/11~12) 크리티컬 패스 A(유닛 성급 스케일링), B(BoardManager 실배치/벤치 API)가 커밋 `5913239`로 완료됨. 인수인계 문서의 개발 순서(BoardManager → SynergyManager → BattleManager)에 따라 오늘은 **SynergyManager**를 구현한다. 어제 계획모드 플랜이 유실되었으므로, **이 플랜을 `Docs/PLAN_2026-06-12_synergy-manager.md`로 저장한 뒤 작업 시작** (오늘부터 플랜은 항상 문서로 보존).

범위: 보드 위 유닛 기준 시너지 카운트/활성 티어 계산 + pull API + 이벤트 통지까지. 실제 스탯 버프 적용은 BattleManager 구현 시로 미룸(SynergyTier에 구조화된 효과 데이터가 없고 설명 문자열뿐임).

## 확정된 설계 결정

- **카운팅 룰**: 시너지별 **고유 종**(unique species, `data.id` 기준 HashSet) 카운트. 같은 포켓몬 중복은 1번만, 성급 무관, **보드 위 유닛만** (벤치 제외) — TFT 표준.
- **데이터 흐름**: 트리거+pull 하이브리드 (6/8 팀 결정). `GameEvents.OnUnitPlaced/OnUnitBenched/OnUnitSold` 구독 → 매번 `GameManager.Instance.Board.GetUnitsOnBoard()`로 전체 재계산. 보드 최대 28칸이라 증분 관리 불필요(오버엔지니어링 지양).
- **네임스페이스**: **글로벌** (없음). 확인 결과 BoardManager 포함 전 파일이 글로벌 — 인수인계 문서의 "PokeChess.Managers 도입" 기술은 현재 코드와 불일치.
- **시너지 매칭**: `synergyName`(한글) + `synergyNameEn`(영문) **양쪽 키로 Dictionary 구성**. `PokemonData.synergies` 주석은 한글 예시("전기")인데 데이터가 아직 없어 어느 쪽일지 불확실 → 둘 다 받기. 미등록 문자열은 `Debug.LogWarning` 1회 (데이터 오타 탐지).
- **이벤트**: `GameEvents.OnSynergyUpdated` (파라미터 없는 `Action`) 신규 추가. UI는 수신 후 pull.
- **효과 적용 seam**: `GetActiveSynergies()`가 그 자체로 인터페이스. BattleManager가 전투 시작 시 pull해서 스냅샷 사본에 버프 적용 예정. 지금은 TODO 주석만.

## 단계

### Step 0 — 플랜 문서 보존
이 플랜을 `Docs/PLAN_2026-06-12_synergy-manager.md`로 복사 저장.

### Step 1 — 시너지 데이터 생성 (선행 조건)
시너지 에셋이 **아예 없음** (`ScriptableObjects/`엔 Items만 존재).
- `Assets/_Project/Recources/synergy_data.json` 생성 (item_data.json과 같은 위치, 폴더명 오타 'Recources' 그대로 따름). 테스트용 2~3종이면 충분 (예: 전기/Electric, 비행/Flying, 불꽃/Fire — 티어 예: 2/4/6마리).
- 임포터 JSON 스키마: `{ "synergies": [ { id, name, nameEn, tiers: [{count, effectDescription}] } ] }` — `PokeChessImporter.cs:23,270-287` 참고.
- Unity 메뉴 "PokeChess > Import Synergy JSON" 실행 → `ScriptableObjects/Synergies/*.asset` 생성 (Unity Editor 작업, MCP 또는 사용자에게 요청).

### Step 2 — GameEvents.cs 수정
`Assets/_Project/Scripts/Core/GameEvents.cs` 유닛 섹션에 추가:
```csharp
public static event Action OnSynergyUpdated;
public static void SynergyUpdated() => OnSynergyUpdated?.Invoke();
```

### Step 3 — SynergyManager.cs 구현 (스텁 → 전체 재작성)
`Assets/_Project/Scripts/Managers/SynergyManager.cs`, 글로벌 네임스페이스, AugmentManager의 OnEnable/OnDisable 구독 패턴 따름:

- `SynergyStatus` (직렬화 가능 클래스): `SynergyData data`, `int uniqueCount`, `int activeTierIndex` (-1=비활성), 편의 프로퍼티 `IsActive`, `ActiveTier`.
- `[SerializeField] List<SynergyData> _synergyDatabase` — 인스펙터 할당 (프로젝트에 런타임 SO 로딩 패턴 없음).
- `Awake()`: 한글명+영문명 양쪽 키 Dictionary 구성.
- `OnEnable/OnDisable`: `OnUnitPlaced`/`OnUnitBenched`/`OnUnitSold` 구독/해제 → 핸들러는 전부 `RecalculateSynergies()` 호출.
- `RecalculateSynergies()`: 보드 유닛 pull → 시너지별 종 HashSet → 활성 티어 = 충족하는 최고 티어(tiers 뒤에서부터 검사, 임포터가 오름차순 저장) → 상태 리스트 갱신 → `GameEvents.SynergyUpdated()`.
- Public API:
  - `GetActiveSynergies() → IReadOnlyList<SynergyStatus>` (활성만)
  - `GetAllSynergyStatuses() → IReadOnlyList<SynergyStatus>` (count>0 전부, UI 회색표시용)
- 매니저 직접 참조 금지 유지 — 보드 pull은 `GameManager.Instance.Board` 허브 경유 (RoundPhaseManager의 기존 패턴).

### Step 4 — SynergyDebugTest.cs (신규)
`Assets/_Project/Scripts/Debug/SynergyDebugTest.cs` — `ItemDebugTest.cs` 패턴 따라 OnGUI 버튼으로 현재 시너지 상태(이름/카운트/활성 티어 설명) 로그 출력. 테스트 유닛은 `ScriptableObject.CreateInstance<PokemonData>()`로 런타임 생성 가능(포켓몬 에셋도 아직 없음).

### Step 5 — 씬 와이어링 (Unity Editor)
GameSceneTest 씬의 SynergyManager에 `_synergyDatabase` 에셋 할당. GameManager의 `_synergyManager` 필드는 이미 존재.

## 검증 (Play 모드)

1. 같은 시너지 포켓몬 2종 배치 → count 2, 임계치 도달 시 티어 활성 + `OnSynergyUpdated` 발화 확인
2. 동일 종 중복 배치 → count 변화 없음
3. 보드→벤치 이동 → count 감소
4. 보드 유닛 판매(OnUnitSold) → 재계산
5. 미등록 시너지 문자열 → LogWarning

## 수정/생성 파일

| 파일 | 작업 |
|---|---|
| `Docs/PLAN_2026-06-12_synergy-manager.md` | 신규 (플랜 보존) |
| `Assets/_Project/Recources/synergy_data.json` | 신규 |
| `Assets/_Project/Scripts/Core/GameEvents.cs` | 이벤트 1개 추가 |
| `Assets/_Project/Scripts/Managers/SynergyManager.cs` | 스텁 → 구현 |
| `Assets/_Project/Scripts/Debug/SynergyDebugTest.cs` | 신규 |
