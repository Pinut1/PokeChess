# 전투 깊이 설계 — 효과 엔진 + 타겟팅 (Phase2)

작성: 김영욱 · 2026-06-23 · 대상: BattleManager(영욱) + ItemData/Item(태욱) 조율

## 배경
현재 전투는 위치기반 오토배틀러 기반은 있으나(틱/이동/사거리/공속/마나/스킬), 데미지가 `위력×경감×크리` 한 줄이고 타겟팅이 "최근접"뿐 → **전략성=스탯 핑퐁**. 시너지/아이템/스킬효과가 전부 미적용. 재미 레이어를 채우되, **기획 수치(지속/증가율)는 placeholder 상수로 선행 개발 → 수치 오면 튜닝만.**

## 현재 상태 (BattleManager)
- `SimulateTick`: 유닛별 `FindNearestEnemy` → 사거리 안이면 쿨다운 공격(마나 차면 `CastSkill`), 아니면 `MoveTowards`(충돌회피 `IsOccupied`). 쿨다운=1/attackSpeed.
- 데미지: `BasicAttack`/`CastSkill`에서 `power × Mitigation(defense) × CritFactor` 직접 계산.
- 스킬: effectType 분기 Phase1(ATTACK/SPELL 데미지만), 지원/CC는 no-op.

## 확정 결정 (2026-06-23)
1. **훅 기반 효과 엔진** — ICombatEffect + 전투 훅. 아이템/스킬/시너지/증강이 전부 효과 제공자.
2. **단일 경감 + 데미지 타입 플래그** — 경감은 defense 하나(기획), 단 Physical/Magic/True 플래그로 아이템 조건(스킬크리·관통 등) 표현.
3. **role 기반 타겟팅 + taunt** — 역할별 타겟 정책 + 도발 강제 + 타겟 유지(flip-flop 제거).

---

## 기둥 A — 데미지 파이프라인
한 줄 계산을 `DamageContext`가 단계를 통과하는 구조로 교체.

```csharp
public enum DamageType { Physical, Magic, True }
public class DamageContext {
    public BattleUnit source, target;
    public float amount;          // 단계마다 가공
    public DamageType type;
    public bool isCrit;
    public bool isBasicAttack;    // 평타 vs 스킬
}
```
`DealDamage(ctx)` 순서:
1. **source 효과 `OnDealDamage(ctx)`** — 거인학살자(+target.maxHp%), 무한대검(crit↑은 스탯), 관통 태그 등
2. **크리 적용** — isCrit 판정 → amount × critMultiplier (보석건틀릿: 스킬도 크리 허용 플래그)
3. **target 효과 `OnTakeDamage(ctx)`** — 경감 `Mitigation(defense)`(True는 스킵), 보호막 흡수, 피해감쇠
4. **적용** — target.currentHp -= amount; 마나 획득
5. 사망 시 **source 효과 `OnKill`**

## 기둥 B — 전투 효과 훅 시스템 (핵심)
```csharp
public interface ICombatEffect {
    void OnCombatStart(BattleUnit self) {}
    void OnTick(BattleUnit self, float dt) {}
    void OnBasicAttack(BattleUnit self, BattleUnit target) {}
    void OnSkillCast(BattleUnit self) {}
    void OnDealDamage(BattleUnit self, DamageContext ctx) {}
    void OnTakeDamage(BattleUnit self, DamageContext ctx) {}
    void OnKill(BattleUnit self, BattleUnit victim) {}
}
```
- `BattleUnit`에 `List<ICombatEffect> effects` 보유. BattleManager가 훅 지점에서 호출.
- **제공자**: ItemData→`ItemEffectFactory`(이미 있는 `AugmentFactory` 패턴 복제), PokemonSkillData→effectType별 효과, SynergyManager→활성 시너지(기획 수치 오면), Augment→전투 훅 확장.
- 예시:
  - 무한의 대검 → `OnCombatStart: self.critChance += X`
  - 보석 건틀릿 → `flag: spellCanCrit = true`
  - 수은 → `OnCombatStart: self.ccImmuneUntil = T+X`
  - 거인 학살자 → `OnDealDamage: ctx.amount += ctx.target.maxHp * P`
  - 구인수 → `OnDealDamage(평타): heal self by Y%`
  - 스킬 HP_REGEN/SHIELD → 시전 시 효과 적용(spellPower 기반)

## 기둥 C — 타겟팅 / CC
- **TargetPolicy(role별)**: Tanker/Warrior=최근접, Assassin=최저HP(또는 후열 키딜), Archer/Magician=최근접(사거리 김), Supporter=아군(지원 스킬).
- **taunt 강제**: `self.tauntedBy != null`이면 그 대상 고정.
- **타겟 유지**: 죽거나 도달불가 전까지 재타겟 안 함(매 틱 갈아타기 제거).
- **CC 상태**(BattleUnit 필드): `stunnedUntil`, `tauntedBy`, `ccImmuneUntil`. SimulateTick에서 스턴이면 행동 스킵. CC 부여는 기둥 B 효과(STUN/TAUNT/SLOW)로 들어옴.

---

## 구현 순서 (기획 수치 없이 가능)
1. **A 데미지 파이프라인** — DamageContext 도입, BasicAttack/CastSkill을 파이프라인으로. (선행, 단독 동작)
2. **B 효과 훅** — ICombatEffect + BattleUnit.effects + 훅 호출 지점. 먼저 **아이템 평타스탯**(이미 ItemData에 필드 있음)부터 OnCombatStart로 적용 → 즉시 체감.
3. **B 조건부 효과 + 스킬 지원/CC** — 거인학살자/구인수/HP_REGEN/SHIELD/STUN 등. **지속·증가율은 placeholder 상수**(예: STUN 1.5초), 기획 오면 교체.
4. **C 타겟팅** — role 정책 + taunt + 타겟유지.

## 조율 / 데이터
- **태욱**: ItemData에 "효과 descriptor"(조건부 효과 표현) 추가 — 평타 스탯은 기존 필드 유지, 조건부(X일때Y)는 effect 타입+파라미터 필드 필요. `ItemEffectFactory`는 영욱이 BattleManager쪽에 둘지 조율.
- **기획**: STUN/TAUNT/SLOW/AS_BUFF/MANA_REGEN 지속·증가율(현재 placeholder), 아이템 수치, role별 타겟 우선순위 세부.
- **황해인**: 시너지 effect 구조(effectType/value) — 확정 시 SynergyManager가 효과 제공자로 합류.

## 영향 파일
`BattleManager.cs`(파이프라인+훅+타겟팅), `BattleUnit`(effects/CC 필드), 신규 `ICombatEffect`/`DamageContext`/`ItemEffectFactory`, `ItemData`(효과 descriptor, 태욱), `PokemonSkillData`(effectType는 이미 있음).
