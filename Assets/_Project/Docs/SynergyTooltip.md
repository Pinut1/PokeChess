# 시너지 설명창(툴팁) — 배선 가이드

시너지 패널의 행에 마우스를 올리면 뜨는 설명창. 롤체 특성 툴팁과 같은 구성이다.

```
[심볼]  시너지 이름                 ← 머리말
(2) 단계별 효과 설명
(4) 단계별 효과 설명                ← 단계 수만큼만 켜진다 (최대 4단계)
[아이콘][아이콘][아이콘] …          ← 효과를 받는 유닛. 코스트별 테두리 / 배치된 유닛만 채색
```

## 스크립트 구성

| 스크립트 | 붙이는 곳 | 역할 |
|---|---|---|
| `SynergyTooltipController` | **항상 켜져 있는** 오브젝트(시너지 패널 루트나 Canvas) | 여닫기 + 위치 계산 |
| `SynergyTooltipUI` | 툴팁 루트(껐다 켜지는 오브젝트) | 내용 표시 |
| `SynergyTooltipUnitSlot` | 유닛 아이콘 칸 | 아이콘 1칸(코스트 테두리·흑백) |
| `SynergyMemberIndex` | (정적 유틸) | 시너지별 "효과 받는 포켓몬" 목록 |

행 쪽 배선은 `SynergyPanelUI`의 **Tooltip 슬롯 하나뿐**이다. 9개 행에는 Awake에서 자동 전달된다.

## 프리팹 (배선 완료본이 저장돼 있음)

`Assets/Art/UI/Ui_Prefabs/` 아래 2개. 컴포넌트·값이 이미 채워져 있으니 씬에 올리기만 하면 된다.

```
SynergyTooltip_Pf     [Image(배경 synPanel_info), VerticalLayoutGroup, ContentSizeFitter, SynergyTooltipUI]
  ├ Header_Panel      [HorizontalLayoutGroup]
  │   ├ Symbol_Image  [Image]      40×40, 런타임에 SynergyData.icon으로 교체됨
  │   └ NameText      [TMP 24pt]
  ├ TierLine_1 … _4   [TMP 18pt]   단계 수만큼만 켜진다
  └ UnitSlot_Root     [GridLayoutGroup]  cell 64×64 / 5열 / spacing 6

UnitSlot_Pf           [Image(코스트 테두리), SynergyTooltipUnitSlot]   64×64
  └ Unit_Icon         [Image]      Preserve Aspect, 여백 6
```

- 폭 380 고정, 피벗 (0, 1) — 위쪽 기준으로 아래로만 자란다
- 루트 `ContentSizeFitter`: Horizontal Unconstrained / **Vertical Preferred Size**
- 루트 `VerticalLayoutGroup`: Control Child Size W/H 켬, **Force Expand Height 끔**
- `UnitSlot_Root`에는 `ContentSizeFitter`를 붙이지 않는다 — 부모 VLG가 GridLayoutGroup의 preferred height를 그대로 쓰기 때문에 중복이다
- 코스트 테두리는 `Art/UI/Frame/cardframe_sheet`의 `_0/_2/_4/_8/_5`(1~5코스트, ShopCardUI와 같은 매핑), 흑백은 `ui_Grayscale_mat` — 둘 다 배선돼 있다
- `SynergyRow_Pf` 루트 Image의 **Raycast Target은 켜둔 상태로 커밋**돼 있다(호버 판정용, 알파 0이라 보이지 않음)

배경·테두리 스프라이트는 임시다. 전용 아트가 나오면 Image의 Sprite만 갈아끼우면 된다.

### 손대면 깨지는 값

- 툴팁 루트의 **부모에 레이아웃 그룹을 두지 말 것** — 컨트롤러가 잡은 위치를 매 프레임 되돌린다
- 단계 줄 TMP의 **Auto Size는 켜지 말 것** — 줄이 늘어도 패널이 안 길어진다
- 툴팁에 `CanvasGroup`을 붙였다면 `Blocks Raycasts`를 꺼둘 것 — 툴팁이 커서를 가리면 깜빡인다

## 씬에서 할 일 (남은 배선 3개)

씬 기준 위치: 시너지 패널 = `Canvas/Left_PanelGroup/Inventory_Panel/SynergyPanel`,
컨트롤러류가 모여 있는 곳 = `Canvas` 오브젝트(`StatInfoController`, `AugmentInfoPanel`이 여기 붙어 있다).

1. **툴팁 배치** — `SynergyTooltip_Pf`를 `Canvas/Info_Panel` 아래에 놓는다(레이아웃 그룹이 없는 자리라야 한다).
   가로 위치를 시너지 패널 오른쪽으로 잡고, **오브젝트를 끈 채로 저장**한다.
2. **상단 기준선** — 빈 오브젝트 `Tooltip_TopAnchor`를 만들어 툴팁이 시작할 높이에 위쪽 모서리를 맞춘다
   (시너지 패널 첫 행을 그대로 지정해도 된다).
3. **컨트롤러** — `Canvas`에 `SynergyTooltipController`를 추가하고 Panel = 1번, Top Anchor = 2번,
   Bottom Limit = 비움, Bottom Margin = 8. 마지막으로 `SynergyPanel`의 `SynergyPanelUI` → **Tooltip**에 이 컨트롤러를 넣는다.

## 위치 규칙

가로는 프리팹에 잡아둔 자리를 그대로 쓰고 세로만 계산한다.

1. 툴팁 위쪽 모서리를 `Top Anchor`의 위쪽 모서리에 맞춘다 → 내용 길이와 무관하게 항상 같은 높이에서 시작
2. 그러고도 아래가 `Bottom Limit`(비우면 루트 Canvas = 화면) 밖으로 넘치면 넘친 만큼만 위로 밀어 올린다

2번이 발동하면 상단 고정이 깨진다. 잘려 나가는 것보다는 낫다는 판단이라 이렇게 뒀고,
자주 발동하면 툴팁 폭을 넓히거나 단계 줄 폰트를 줄여 높이를 낮출 것.

## 유닛 아이콘 칸

- **칸 수 = 시너지 카운트의 최대치.** `SynergyManager`와 같은 키로 묶는다 — 기본은 진화 계열(이상해씨·이상해풀·이상해꽃 = 1칸), 돌연변이(`countPerSpecies`)만 종 단위(이브이 9종 = 9칸).
- 대표 포켓몬은 **그 시너지를 실제로 가진** 멤버 중 코스트 → 진화단계 → 도감번호 순. 계열 루트를 그냥 쓰면 비행 툴팁에 캐터피(비행 없음)가 뜬다. 같은 상황 4건: 캐터피→버터플(비행), 별가사리→아쿠스타(정령), 롱스톤→강철톤·스라크→핫삼(파괴).
- 채색 판정은 **필드 배치 기준**(벤치 제외) — 매니저의 카운트 기준과 같아야 "채색된 아이콘 수 = 시너지 카운트"가 맞는다.
- 칸은 `Unit Slot Root` 아래에 미리 깔아둔 것부터 쓰고, 모자라면 `Unit Slot Prefab`을 복제해 채운다. 현재 데이터 기준 최대 13칸(독).
- 흑백은 `Art/UI/Shaders/ui_Grayscale_mat`을 칸의 `Grayscale Material`에 물리면 된다. TMP 텍스트에는 물리지 말 것(SDF가 깨진다).

## 데이터 현황 (2026-08-02)

| 시너지 | 단계 | 아이콘 칸 |
|---|---|---|
| 풀 | 2/4/6/8 | 12 |
| 독 | 3/5/7/9 | 13 |
| 물 | 3/5/7/9 | 12 |
| 돌연변이 | 2/3/4/5 | 9 (종 단위) |
| 불꽃 | 2/4/6 | 8 |
| 정령 | 2/4/6 | 9 |
| 노말 | 2/4/6 | 8 |
| 비행 | 2/4/6 | 6 |
| 전기 | 2/3/4 | 8 |
| 대지 | 2/3/4 | 5 |
| 파괴 | 2/3/4 | 5 |
| 벌레 | 2/3/4 | 4 |
| 드래곤 | 1/2 | 2 |
| 치어리더 | 1 | 3 |
| 얼음 | 1 | 1 |
| 악 | 1 | 1 |

## 남은 것

- **유닛 전용 아이콘 제작** — 지금은 상점 카드와 같은 `PokemonData.icon`(일러스트)을 그대로 쓴다. 전용 아이콘이 나오면 슬롯의 `_icon`에 물리는 소스만 바꾸면 된다.
- 단계별 효과 설명 문구는 시트 원본이 아직 `atkSpeed 고정값 증가` 같은 임시값이다. 시트가 갱신되면 임포트만 다시 하면 된다.
