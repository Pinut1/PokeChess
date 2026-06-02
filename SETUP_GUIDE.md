# PokeChess 프로젝트 초기 세팅 가이드

> 처음 프로젝트 받은 사람은 이 순서대로 진행할 것.

---

## 1. 필수 설치

| 도구 | 버전 | 비고 |
|------|------|------|
| Unity | **6.x (팀 공지 버전)** | Unity Hub에서 설치 |
| Git | 최신 | https://git-scm.com |
| Git LFS | 최신 | https://git-lfs.com |
| Photon PUN2 | Asset Store | Unity 패키지 매니저에서 import |

---

## 2. 저장소 클론 및 LFS 초기화

```bash
# LFS 먼저 설치 확인
git lfs version

# 저장소 클론
git clone <GitHub 저장소 URL>
cd PokeChess

# LFS 초기화 (처음 한 번만)
git lfs install
git lfs pull
```

---

## 3. Unity 프로젝트 세팅 (팀장이 최초 1회만)

### 프로젝트 생성
- Unity Hub → New Project → **3D (URP)** → 이름: `PokeChess`
- 생성 위치: 클론한 Git 폴더 안

### 필수 설정
- Edit → Project Settings → **Editor**
  - Asset Serialization Mode: **Force Text** (씬 충돌 방지)
  - Version Control Mode: **Visible Meta Files**
- Edit → Project Settings → **Player**
  - 타겟 플랫폼 확정 후 설정 (PC / Android)

### 폴더 구조 생성
Assets 폴더 안에 아래 폴더 생성 (이미 Scripts는 있음):
```
Assets/_Project/
├── Art/Sprites/Pokemon, UI, Items, Effects
├── Art/Models/Pokemon, Characters, Props
├── Art/Textures/Pokemon, Characters
├── Art/Animations/Pokemon, Characters
├── Art/Fonts/
├── Audio/BGM, SFX
├── Prefabs/Pokemon, UI, Items, Effects
├── ScriptableObjects/Pokemon, Items, Synergies
├── Scripts/ (이미 있음)
├── Scenes/
└── Data/JSON/
```

---

## 4. Photon PUN2 세팅

1. Unity Asset Store에서 **Photon PUN2 Free** import
2. Import 후 자동으로 뜨는 창에 **App ID 입력**
   - https://dashboard.photonengine.com 에서 앱 생성
   - 앱 ID는 팀장이 발급 후 디스코드에 공유
3. PhotonServerSettings 확인:
   - App Id PUN: 입력됨
   - Region: **asia** (권장)

---

## 5. 스크립트 확인

아래 파일이 Scripts/Core, Scripts/Data, Scripts/Editor에 있어야 함:

**Core**
- `Singleton.cs` — 싱글턴 베이스
- `GameManager.cs` — 매니저 허브
- `GameEvents.cs` — 이벤트 정의
- `PokemonUnit.cs` — 유닛 런타임

**Data**
- `PokemonData.cs` — 포켓몬 ScriptableObject
- `ItemData.cs` — 아이템 ScriptableObject
- `SynergyData.cs` — 시너지 ScriptableObject
- `PokemonSkillData.cs` — 스킬 데이터

**Editor**
- `PokeChessImporter.cs` — JSON → SO 임포터

컴파일 에러 없으면 세팅 완료.

---

## 6. JSON 데이터 Import 방법

```
1. 구글 시트 → Export 메뉴 → JSON 내보내기
2. 받은 JSON 파일을 Assets/Resources/Data/ 에 복사
   (pokemon_data.json / item_data.json / synergy_data.json)
3. Unity 상단 메뉴 → PokeChess → Import Pokemon JSON (또는 Item / Synergy)
4. ScriptableObjects 폴더에 자동 생성됨
```

---

## 7. 씬 구성

| 씬 이름 | 용도 |
|---------|------|
| `LobbyScene` | 방 생성/입장, Photon 연결 |
| `GameScene` | 실제 게임 (보드, 전투, 샵) |
| `ResultScene` | 게임 종료 화면 |

Scenes 폴더에 위 3개 씬 생성 후 Build Settings에 추가.

---

## 8. 첫 작업 시작 전 체크리스트

- [ ] Unity 버전 팀 공지 버전과 동일한지 확인
- [ ] `git lfs pull` 완료
- [ ] Photon App ID 입력됨
- [ ] 컴파일 에러 없음
- [ ] 브랜치 `develop`에서 시작 (`git checkout develop`)
- [ ] 작업 시작 디스코드에 공지

---

## 아직 미확정 (기획 확정 후 채울 것)

- [ ] 보드 크기
- [ ] 플레이어 수
- [ ] 골드/경험치 시스템 수치
- [ ] 별 강화 스탯 스케일링 방식
- [ ] 포켓몬 확정 목록 및 실제 JSON 데이터
- [ ] 타겟 플랫폼 (PC / 모바일)
