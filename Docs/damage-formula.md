# 데미지 결정 방식

작성: 김영욱 · 2026-06-23 · 구현: `BattleManager.ResolveDamage` + `DamageContext`

모든 평타·스킬 피해는 **단일 진입점 `ResolveDamage(DamageContext)`** 를 통과한다. 한 줄 계산이 아니라 단계 파이프라인 — 효과(아이템/스킬/시너지)가 중간에 끼어들 수 있다.

## 파이프라인 단계
```
DamageContext { source, target, amount, type, isBasicAttack }
  amount = 기본 위력으로 시작
   │
   ① 공격자 효과 OnDealDamage(ctx)   ← [기둥B] 거인학살자(+target.maxHp%)·관통 등이 amount/type 가공
   │
   ② 크리        amount *= CritFactor(source)
   │
   ③ 경감        type != True 이면  amount *= Mitigation(target.defense)
   │
   ④ 피격자 효과 OnTakeDamage(ctx)   ← [기둥B] 보호막 흡수·추가 경감
   │
   ⑤ 적용        target.currentHp -= amount;  피격자 마나 획득
   │
   ⑥ 사망 시     공격자 효과 OnKill(victim)   ← [기둥B]
```
①④⑥은 기둥B(`ICombatEffect`/`BattleUnit.effects`, 2026-06-24 구현완료)로 채워짐 — 아이템(`ItemStatEffect`/`ItemConditionalEffect`)이 OnDealDamage/OnTakeDamage/OnKill에서 ctx를 가공한다.

## 기본 위력 (단계 시작값)
| 출처 | 위력 | 타입 |
| --- | --- | --- |
| 평타 | `source.attack` | Physical |
| 스킬 ATTACK effectType | `source.attack` | Physical |
| 스킬 SPELL effectType | `source.spellPower` | Magic |

> 신규 모델(SkillSystem_DevGuide): 평타=attack, 스킬=spellPower. 특공/특방 폐지.

## 경감 공식 (③)
```
Mitigation(def) = 100 / (100 + max(0, def))
```
- 방어 1당 유효체력 +1% (체감), 항상 양수 → 별 배수와 곱셈으로 공존.
- **경감은 항상 `defense` 하나**(기획: 단일 방어 스탯). 물리/마법 별도 방어 없음.
- `True` 타입은 경감 무시(고정 데미지).
- 관통은 추후 ① 단계에서 `target.defense`를 깎는 식으로 확장.

## 크리 (②)
```
CritFactor(a) = 1 + a.critChance * (a.critMultiplier - 1)
```
- **현재 결정론적 기대값 모델** — 난수 없이 기대 피해를 곱한다. critChance=0이면 ×1(무영향).
- 기본값: critChance=0, critMultiplier=1.5. 크리 스탯은 아이템(무한의 대검 등)으로 부여 예정(기둥B).
- 각자 보드 로컬 시뮬이라 동기화 제약 없음 → 필요 시 **RNG 크리(isCrit 판정)로 교체 가능**. 그 경우 `ctx.isCrit`를 채우고 보석 건틀릿(스킬 크리 허용) 등 효과가 참조.

## 데미지 타입 (플래그)
경감은 단일(defense)이지만 **타입은 효과 조건 표현용**으로 구분한다.
- `Physical` / `Magic` / `True`
- 용도 예: "스킬(Magic) 크리만 허용", "물리 관통", "고정딜(True)".

## 마나 (⑤)
- 피격자: `min(받은피해 × 0.05, 20)` 획득 (피격당 상한 20).
- 공격자: 평타 1회당 `+10` 고정(`BasicAttack`에서 별도 처리).
- 마나 ≥ manaCost → 다음 행동에서 평타 대신 스킬 시전 후 마나 0.

## 예시
1코 평타: attack 50, 대상 defense 30, 크리 없음
```
50 × Mitigation(30) = 50 × (100/130) ≈ 50 × 0.769 = 38.5
```
스킬(SPELL) spellPower 180, 대상 defense 20
```
180 × (100/120) = 180 × 0.833 = 150
```

## 미확정 / 추후
- RNG 크리 전환 여부(결정론 유지 vs 보이는 크리 연출) — 기획 결정 대기, 코드는 양쪽 다 작은 변경.
- 관통/방어 감소(① 단계 확장) — `ItemData`에 관통 필드가 없어 데이터 정의 전엔 구현 표면이 없음.
- 기둥C(role 타겟팅/Stun·Slow·Taunt, 2026-06-24 구현완료)도 같은 파이프라인을 공유 — 지속시간 등은 전부 placeholder, 설계: `docs/combat-depth-design.md`.
