# 인수인계 — 아이템 보호막 VFX 배선 (2026-08-18)

장비(규살열매/조개껍질방울/의문열매)로 얻은 보호막에 전용 VFX를 붙이는 작업입니다.
**코드 한 군데 + VfxDatabase 한 엔트리**면 끝납니다.

- **프리팹**: `Assets/Art/VFX/_Shield/VFX_Item_White_SHIELD.prefab` (황해인 제작)
- **건드릴 파일**: `BattleManager.cs`, `VfxDatabase.asset`
- **소요**: 15분 + 눈으로 확인
- **작성**: 황해인

---

## 1. 무엇을 바꾸나

지금은 보호막이 생기면 **출처와 무관하게** `Shield_gold`(에셋스토어 금색 쉴드)가 상태 VFX로 뜹니다.
이건 장비 보호막 시스템을 만들 때 **쓸 아트가 없어서 임시로 갖다 쓴 것**입니다
(`BattleManager.cs`에 `// ── 아이템 고유효과 VFX(2026-08, 기존 VFX 재사용만) ──` 라고 적혀 있습니다).

이제 정식 아트가 나왔으니 아래처럼 바꿉니다.

| 보호막 출처 | 지금 | **바꾼 뒤** |
|---|---|---|
| 스킬 (`Skill`) | 시전 시 `_Shield/` 8종 캐스트 VFX + **금색 상태 VFX** | 캐스트 VFX만 (**금색 제거**) |
| 장비 (`Apicot`·`ShellBell`·`Micle`) | 금색 상태 VFX | **`VFX_Item_White_SHIELD`** |

- **`Shield_gold`는 어디서도 안 쓰이게 됩니다.** 프리팹을 지울 필요는 없고 그냥 참조만 끊깁니다
- **둘이 동시에 걸려도 문제없습니다.** 스킬은 시전 순간 1회 재생되고 끝나는 캐스트 VFX, 장비는 보호막이 남아 있는 동안 계속 떠 있는 상태 VFX라 서로 겹쳐도 됩니다

> 출처 구분은 `ShieldSource.cs`의 `ShieldSourceType { Skill, Apicot, ShellBell, Micle }` 입니다.
> `Skill`만 빼면 나머지가 전부 장비입니다.

---

## 2. 코드 수정 — `BattleManager.cs`

### 2-1. vfxId 상수

```csharp
// 기존
private const string SHIELD_STATE_VFX_ID = "VFX_Shield_State"; // Shield_gold.prefab, VfxDatabase 등록

// 이렇게 (이름·값 둘 다 변경)
private const string ITEM_SHIELD_STATE_VFX_ID = "VFX_Item_Shield_State"; // VFX_Item_White_SHIELD.prefab
```

`SHIELD_STATE_VFX_ID`를 쓰는 곳이 `SyncShieldVfx` 안에 **1군데** 더 있습니다(경고 로그). 같이 바꿔 주세요.

### 2-2. 판정을 장비 출처로 한정

`HasActiveShieldSource`를 아래로 **교체**합니다.

```csharp
/// <summary>
/// 장비로 얻은 보호막이 살아 있는지. 스킬 SHIELD는 일부러 제외한다 —
/// 스킬은 시전 순간 _Shield/ 8종 캐스트 VFX가 따로 재생되므로 상태 VFX를 겹쳐 띄우지 않는다.
/// </summary>
private static bool HasActiveItemShield(BattleUnit bu)
{
    foreach (var source in bu.shieldSources)
    {
        if (source.remainingAmount <= 0f) continue;
        if (source.type != ShieldSourceType.Skill) return true;
    }

    return false;
}
```

그리고 `SyncShieldVfx` 안의 호출부를 바꿉니다. 아래 한 줄만 고치면 나머지 분기는 그대로 동작합니다.

```csharp
// 기존
bool hasActiveShieldSource = HasActiveShieldSource(bu);

// 이렇게
bool hasActiveShieldSource = HasActiveItemShield(bu);
```

> 변수 이름까지 `hasItemShield`로 바꾸면 더 읽기 좋습니다(같은 함수 안 3곳에서 씁니다).

### 2-3. 주석 정리 (선택)

`SyncShieldVfx` 위의 `<summary>`가 아직 "스킬 SHIELD/Apicot/Shell Bell/Micle 기준"이라고 적혀 있습니다.
"장비 출처 기준, 스킬은 제외"로 고쳐 주시면 다음 사람이 헷갈리지 않습니다.

---

## 3. VfxDatabase 엔트리 수정

`Assets/Resources/VfxDatabase.asset` 선택 → Inspector에서 **`VFX_Shield_State`** 엔트리를 찾습니다.
**새로 추가하는 게 아니라 기존 엔트리를 고치는 것**입니다.

| 필드 | 현재 | 바꿀 값 |
|---|---|---|
| `vfxId` | `VFX_Shield_State` | **`VFX_Item_Shield_State`** (2-1의 문자열과 정확히 일치해야 함) |
| `prefab` | `Shield_gold` | **`VFX_Item_White_SHIELD`** |
| `positionOffset` | `(0, 0, 0)` | **`(0, -0.097, 0)`** |

`scale`, `lifetime` 등 나머지는 건드리지 마세요.

---

## 4. 확인

- **장비만**: 규살열매를 장착하고 전투 시작 → 흰 쉴드가 뜨는가
- **스킬만**: SHIELD 스킬 유닛(물/독/땅/치어 탱커 등)이 스킬을 쓸 때 → **금색 쉴드가 더 이상 안 뜨는가**. 기존 캐스트 VFX는 그대로 터져야 합니다
- **둘 다**: 규살열매를 낀 탱커가 스킬을 쓰면 캐스트 VFX가 터지고 흰 쉴드는 계속 떠 있는가
- 유닛이 이동할 때 흰 쉴드가 따라오는가
- 유닛이 죽거나 전투가 끝나면 흰 쉴드가 사라지는가

---

## `-0.097`은 어디서 나온 값인가

눈대중이 아니라 계산값입니다. 프리팹을 수정하게 되면 아래 식으로 다시 뽑으세요.

**쉴드 본체**(`shield_AB` / `shield_add` / `stroke`)가 모든 쉴드 프리팹에서 로컬 y `1.94`에 있습니다.
그리고 **프리팹 루트의 Position Y는 런타임에 무시됩니다** — 코드가 유닛 위치로 덮어쓰기 때문입니다. 따라서:

```
화면에 보이는 쉴드 중심 높이 = 1.94 x (프리팹 루트 Scale) + positionOffset.y
```

| | 루트 Scale | offset 0일 때 중심 높이 |
|---|---|---|
| 스킬 캐스트 쉴드 8종 (`_Shield/` 폴더) | 0.4 | 0.776 |
| `VFX_Item_White_SHIELD` | 0.45 | 0.873 |

`0.776 - 0.873 = -0.097` → 흰 쉴드를 이만큼 내리면 스킬 쉴드와 중심이 맞습니다.
크기는 0.45라 조금 더 커서 **바깥에 겹칩니다**(의도된 모양입니다).

> 눈으로 보고 스킬 쉴드 자체가 낮거나 높아 보이면 알려 주세요. 그건 8종을 전부 같이 조정해야 하는 별도 건입니다.

### 프리팹 루트 Y는 왜 안 먹나

코드가 매 틱 위치를 다시 씁니다.

```csharp
bu.shieldVfxInstance.transform.position = bu.visual.transform.position + entry.positionOffset;
```

유닛을 따라다녀야 해서 그렇습니다. **높이 보정은 반드시 `positionOffset`으로** 주세요.
프리팹 루트 Y는 에디터에서 눈으로 볼 때만 의미가 있습니다(그쪽도 나란히 보고 싶으면 `0.053`으로 두면 됩니다).

---

## 참고

- **크기와 높이를 코드가 정하지 않습니다.** 크기는 프리팹 루트 scale, 높이는 VfxDatabase의 `positionOffset`이 정합니다. 스킬 VFX 경로(`BattleVfxPlayer.Create`)와 같은 규칙이라, 쉴드가 늘어나도 코드를 건드릴 일이 없습니다
- **WATER 시너지 보호막은 `shieldSources`에 안 들어갑니다** — 추적되지 않는 보호막이라 흰 쉴드가 뜨지 않습니다. 정상 동작입니다
- 배경은 PR #101의 `3061fd48` 커밋 메시지에 적혀 있습니다

## 막히면

| 증상 | 원인 |
|---|---|
| 흰 쉴드가 아예 안 뜬다 | Console에 `[Vfx] 'VFX_Item_Shield_State' 미등록` 경고 확인 → 코드 상수와 `vfxId` 문자열이 다름 |
| 스킬 쓸 때도 흰 쉴드가 뜬다 | 2-2를 빠뜨림 (`HasActiveShieldSource`가 아직 스킬까지 세고 있음) |
| 금색 쉴드가 계속 보인다 | 3번에서 `prefab`을 안 바꿈 |
| 크기가 이상하다 | 프리팹 루트 Scale 확인 (`VFX_Item_White_SHIELD`는 `0.45`가 정상) |
| 높이가 안 맞는다 | 위 계산식으로 다시 뽑거나 해인에게 문의 |
