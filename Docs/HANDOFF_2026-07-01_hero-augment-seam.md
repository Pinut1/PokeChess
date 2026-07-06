# 핸드오프 — 영웅증강 전투/유닛 seam (2026-07-01)

이브이·파치리스 영웅증강의 **전투/유닛 쪽 메커니즘을 미리 구현**함(영욱). 증강 시스템(해인)은 아래 API만 호출하면 동작한다. 증강 자체(선택 흐름·즉시지급·전용리롤·풀)는 미구현 — 해인 영역.

## 이브이 영웅증강

증강 선택 시 대상 이브이 유닛(즉시지급분 + 이후 구매/획득분)에 대해:

```csharp
eeveeUnit.ApplyEeveeHeroAugment(1.4f);   // 진화잠금 + 스탯 ×1.4
```

효과(자동 반영, 추가 배선 불필요):
- **진화잠금**: `evolutionLocked=true` → 별 3개 합체 시 진화체(리피아/샤미드 등)로 스왑하지 않고 이브이 종 유지. (`BoardManager.CheckEvolution`)
- **스탯 ×1.4**: `heroStatMultiplier` → MaxHp/Attack/SpellPower에 곱. 특수진화 ×1.5와 독립 누적.
- **3성 봇 전원소환**: 진화잠금 이브이가 3성으로 보드에 있으면 전투 시작 시 돌연변이 봇 4마리(Espeon/Umbreon/Glaceon/Sylveon) 즉시 소환. `BattleManager.HasHeroEeveeThreeStar()` 자동 판정 — 이브이 단독이라 일반 돌연변이 시너지 카운트로는 티어가 안 올라 전용 경로로 처리.

주의:
- **모든 이브이 유닛에 태그해야 함**(구매/합체 대비). OnUnitPlaced/구매 훅에서 종=Eevee면 `ApplyEeveeHeroAugment` 호출 권장. 합체 생존자는 인스턴스 유지라 플래그 보존됨.
- 봇소환 판정은 종이 `"Eevee"`(영문명)여야 함. `evolutionLocked`+3성+Eevee 3조건.

## 파치리스 영웅증강

```csharp
parichisuUnit.ApplyParichisuHeroAugment(PokemonRole.Tanker, tauntSkill, manaCost);
```

효과(자동 반영):
- **역할 변경**: `roleOverride` → 전투 타겟 우선순위가 탱커로(적이 가장 늦게 노림). `PokemonUnit.Role`이 오버라이드 반환. 시너지는 원본 유지.
- **도발 스킬 주입**: `grantedSkill` → 전투에서 원본 스킬 대신 이 스킬 시전. `effectType=Taunt`인 `PokemonSkillData`를 넘기면 됨(도발 로직 `BattleManager`/`BattleUnit.ApplyTaunt` 이미 구현). 마나비용 0이면 data.manaCost 사용.

해인이 준비할 것:
- **Taunt용 `PokemonSkillData` 인스턴스**. skill_table에 파치리스 도발 스킬 행을 추가해 임포트하거나, 코드로 `new PokemonSkillData { skillId="PARICHISU_TAUNT", effectType=SkillEffectType.Taunt, targetType=SkillTargetType.EnemyArea, areaRadius=2 }` 식으로 생성. (targetType은 기획 확정 대상 — 단일/광역 도발)

## 건드린 파일
- `Core/PokemonUnit.cs` — 필드 4종 + `EffectiveSkill`/`EffectiveManaCost` + `ApplyEeveeHeroAugment`/`ApplyParichisuHeroAugment`, 스탯·Role 프로퍼티에 배수/오버라이드 반영
- `Managers/BoardManager.cs` — CheckEvolution 진화잠금 가드
- `Managers/BattleManager.cs` — 스냅샷이 EffectiveSkill 사용, `HasHeroEeveeThreeStar` 봇 전원소환 경로, **MutantBots 배열 오타 수정(Eevee→Espeon)**

## 미해결 / 확인 필요
- **MutantBots Eevee→Espeon 수정**은 기존 돌연변이 시너지에도 영향(첫 봇이 이제 Espeon). 덱기획 기준으론 맞음. 리그레션 시 확인.
- 파치리스 도발 스킬의 targetType(단일/광역), 마나비용은 기획 미확정 — 임시값으로 검증 후 조정.
- 이브이 ×1.4 vs 특수진화 ×1.5 동시 적용 여부: 이브이 영웅증강은 "진화 불가"라 스톤/통신진화가 안 붙는 게 정상 → 실제로는 ×1.4만. 두 배수는 독립이라 논리상 충돌 없음.
