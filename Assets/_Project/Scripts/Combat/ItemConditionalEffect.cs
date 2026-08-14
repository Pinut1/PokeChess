using UnityEngine;

/// <summary>
/// 장착템의 조건부/반사형 보너스(흡혈·보호막·반사·전이형 화상·매틱 스탯)를 처리(기둥B 2단계).
/// 평타스탯형(ItemStatEffect)과 달리 매틱/피격 시점에 반복 평가가 필요한 효과들.
/// 화상 도트 자체는 "옮은 모든 유닛"이 받아야 해서 여기가 아니라 BattleManager의 공통 처리(TickBurn)가 담당하고,
/// 이 클래스는 화상을 "거는" 트리거(burnNearOnPhysHit)만 맡는다.
/// </summary>
public class ItemConditionalEffect : ICombatEffect
{
    private const float BURN_DAMAGE_PER_TICK = 1f;  // PLACEHOLDER(기획확정 전): True 고정딜/틱
    private const int   BURN_TICK_COUNT = 30;        // PLACEHOLDER(기획확정 전): 0.1s*30 = 3초
    private const int   BURN_RADIUS = 1;              // PLACEHOLDER(기획확정 전): 피격자 주변 1칸

    private readonly ItemData _item;
    private readonly BattleManager _battle;

    private float _appliedDefBonus; // defSpDefPerAttacker 누적 가산분(매틱 제거 후 재가산용)

    // ── 기합의머리띠(hpConditionalSpAtkPct) 전용 — HP>50%면 2배가 되는 주문력 보너스 ──
    // base는 "이 아이템의 보너스가 없었다면의 spellPower"를 첫 OnTick(=OnCombatStart 완료 후) 시점에
    // 1회만 캡처해 고정한다. 매틱 이 값 기준으로 목표 보너스를 다시 계산해 "이전에 넣은 만큼 빼고 다시
    // 더하는" 방식(defSpDefPerAttacker와 동일 패턴)으로 갱신하므로, 다른 아이템/시너지의 주문력을
    // 건드리지 않고 매틱 중복 가산도 발생하지 않는다.
    private float _conditionalApBase;
    private bool  _conditionalApBaseCaptured;
    private float _appliedConditionalApBonus;

    // ── 보호막 출처 추적(2026-08 확정) ──
    // BattleUnit.shield는 여전히 모든 출처가 합산되는 공유 풀(권위값)이지만, 이 아래 두 아이템군은
    // 이제 자기 몫을 근사(Mathf.Min(owned,shield))하지 않고 전용 ShieldSource 객체를 직접 만들어
    // BattleUnit.shieldSources에 등록한다 — 실제 흡수·소진은 BattleUnit.AbsorbShieldDamage(공용,
    // ResolveDamage가 아이템 보유 여부와 무관하게 항상 1회 호출)가 이 객체의 remainingAmount를 정확히
    // 차감하므로, 여기서는 그 결과(객체 자체, 또는 ctx.depletedShieldSources)를 읽기만 하면 된다.

    // ── 규살열매(Apicot, combatStartShieldPct) 전용 ──
    private bool  _apicotShieldGranted;
    private bool  _apicotAmpApplied;
    private ShieldSource _apicotSource;

    // ── 조개껍질방울(Shell Bell)/의문열매(Micle) 공용 — HP% 임계값 진입 시 1회 보호막(hpThresholdPct) ──
    // 부여 후 동작은 데이터로 갈린다: shieldDecayDuration>0이면 선형 decay(의문열매),
    // shieldDuration>0이면 고정시간 후 제거(조개껍질방울). 두 필드가 동시에 0보다 크지 않다(현재 데이터).
    private bool  _thresholdShieldTriggered;
    private bool  _thresholdShieldActive;
    private ShieldSource _thresholdSource;

    // ── 초점렌즈(Scope Lens, periodicSkillDamageAmpPct) 전용 ──
    private float _scopeLensElapsed;
    private float _scopeLensAppliedAmp;

    // ── 캄라열매(Salac, periodicAttackSpeedPct) 전용 ──
    // base는 "이 아이템의 매초 보너스가 없었다면의 attackSpeed"를 첫 OnTick(정적 아이템 효과가 이미
    // 반영된 뒤) 시점에 1회만 캡처한다. 매틱 stacks(경과시간/주기, 내림)를 다시 계산해 "이전에 넣은
    // 만큼 빼고 새로 계산한 만큼 더하는" 방식으로 갱신하므로 ×1.07 복리가 아니라 "기준값의 7%씩
    // 가산형 누적"이 되고, 매틱 중복 가산도 발생하지 않는다.
    private bool  _salacBaseCaptured;
    private float _salacBase;
    private float _salacElapsed;
    private float _salacAppliedBonus;

    // ── 왕의징표석(King's Rock)/선제공격손톱(Quick Claw) 공용 — 평타(항상)/피격(gainStackOnTakeDamage인
    // 아이템만) 시 스택 증가. 스택 1개당 basicAttackDamageAmpPerStackPct(및 King's Rock만
    // skillDamageAmpPerStackPct)를 BattleUnit에 가산하고, maxCombatStacks 도달 시 1회 추가 보너스
    // (King's Rock=damageAmpPct, Quick Claw=attackSpeed)를 적용한다. 두 아이템을 동시 장착하면 각자
    // 별도의 ItemConditionalEffect 인스턴스(CreateBattleUnit이 아이템마다 새로 생성)가 독립적으로
    // 자기 스택만 추적하므로 서로의 카운트에 영향을 주지 않는다 — 그 결과가 합쳐지는 지점은
    // BattleUnit.basicAttackDamageAmpPct 하나뿐이며, 여기엔 항상 += 로만 가산한다(덮어쓰기 없음).
    private int  _hitStacks;
    private bool _stackCapBonusApplied;

    public ItemConditionalEffect(ItemData item, BattleManager battle)
    {
        _item = item;
        _battle = battle;
    }

    /// <summary>구애머리띠/라즈열매 — 전투 시작 시 hex distance==1인 같은 팀 아군(자신 제외)에게
    /// 오라 보너스를 준다. BattleManager.ApplyOnCombatStartEffects 2단계(모든 유닛의 ItemStatEffect가
    /// 먼저 끝난 뒤)에서 호출되므로, 여기서 건드리는 다른 유닛의 spellPower/attackSpeed는 이미 그
    /// 유닛 자신의 정적 아이템 스탯이 반영된 최종값이다. 공격속도%는 즉시 곱하지 않고
    /// pendingAuraAttackSpeedPct에 누적만 해 3단계에서 1번만 소비되게 한다(중첩 시 복리 방지).
    /// 주문력(flat)과 마나 지급은 곱셈이 아니라 즉시 적용해도 중첩 시 문제없다.</summary>
    public void OnCombatStart(BattleUnit self)
    {
        if (_item == null) return;
        if (_item.adjacentAllyAttackSpeedPct <= 0f &&
            _item.adjacentAllySpellPowerBonus <= 0f &&
            _item.adjacentAllyManaBonus <= 0f)
            return;

        foreach (var other in _battle.Units)
        {
            if (other == self || other.team != self.team || !other.IsAlive) continue;
            if (self.coords.DistanceTo(other.coords) != 1) continue;

            if (_item.adjacentAllyAttackSpeedPct > 0f)
            {
                other.pendingAuraAttackSpeedPct += _item.adjacentAllyAttackSpeedPct;
                // 구애머리띠 — 기존 Celebrate(공용 서포터 AS 오라) VFX 재사용. 오라 발동마다 1회씩
                // 재생(2개 중첩 시 같은 대상에 2번 재생돼도 의도된 동작 — 중복 제거 안 함).
                _battle.PlaySingleTargetVfx("VFX_Normal_Supporter_AS_BUFF", other);
            }

            if (_item.adjacentAllySpellPowerBonus > 0f)
                other.spellPower += _item.adjacentAllySpellPowerBonus;

            if (_item.adjacentAllyManaBonus > 0f)
                BattleManager.GainMana(other, _item.adjacentAllyManaBonus);
        }
    }

    /// <summary>먹다남은음식 등 "잃은 체력 기준" 회복이 아니라 매초 최대체력의 N%씩 회복하는 아이템이
    /// 이후 추가되면 별도 필드로 분리해야 한다 — 현재(5차분)는 hpRegenPercent를 쓰는 아이템이 먹다남은음식
    /// 하나뿐이라(다른 18종 전부 0) 이 필드를 잃은체력 기준 공식으로 재해석해도 안전하다(기획 확인).</summary>
    public void OnTick(BattleUnit self, float deltaTime)
    {
        if (_item == null) return;

        if (_item.hpRegenPercent > 0f)
        {
            float missingHp = self.maxHp - self.currentHp;
            if (missingHp > 0f)
                self.currentHp = Mathf.Min(self.maxHp, self.currentHp + missingHp * (_item.hpRegenPercent * 0.01f) * deltaTime);
        }

        if (_item.defSpDefPerAttacker > 0f)
        {
            self.defense -= _appliedDefBonus;
            _appliedDefBonus = _battle.CountUnitsTargeting(self) * _item.defSpDefPerAttacker;
            self.defense += _appliedDefBonus;
        }

        if (_item.hpConditionalSpAtkPct > 0f)
        {
            if (!_conditionalApBaseCaptured)
            {
                // 첫 OnTick은 항상 ApplyOnCombatStartEffects()가 전부 끝난 뒤에 돈다(전투 시작 시
                // 1회, SimulateTick 이전) — 그 시점의 spellPower를 "이 아이템 보너스 제외 기준값"으로
                // 고정한다. hpConditionalSpAtkPct는 spAtkPct/ApplyAll 경로를 타지 않으므로(기획 확정),
                // 이 스냅샷에는 애초에 이 아이템의 몫이 섞여 있지 않다.
                _conditionalApBase = self.spellPower;
                _conditionalApBaseCaptured = true;
            }

            self.spellPower -= _appliedConditionalApBonus;
            bool boosted = self.maxHp > 0f && self.currentHp / self.maxHp > 0.5f;
            float ratePct = boosted ? _item.hpConditionalSpAtkPct * 2f : _item.hpConditionalSpAtkPct;
            _appliedConditionalApBonus = _conditionalApBase * (ratePct * 0.01f);
            self.spellPower += _appliedConditionalApBonus;
        }

        // 규살열매 — 전투 시작(첫 틱) 즉시 보호막 부여, 8초 경과 시 남은 몫 제거 + 스킬피해 +N% 1회
        // 영구 적용. 피해로 인한 소진·후속 효과 발동은 OnTakeDamage(ctx.depletedShieldSources 기반)가
        // 담당 — 여기서는 시간 만료 경로만 본다(둘 다 같은 _apicotAmpApplied 가드를 공유해 중복 없음).
        if (_item.combatStartShieldPct > 0f && !_apicotAmpApplied)
        {
            if (!_apicotShieldGranted)
            {
                float grant = self.maxHp * (_item.combatStartShieldPct * 0.01f);
                self.ApplyShield(grant);
                _apicotSource = new ShieldSource(ShieldSourceType.Apicot, grant,
                    ShieldDecayType.FixedDuration, _item.shieldDuration, self.NextShieldSequence());
                self.shieldSources.Add(_apicotSource);
                _apicotShieldGranted = true;
            }

            _apicotSource.elapsedDuration += deltaTime;
            if (_apicotSource.elapsedDuration >= _item.shieldDuration)
            {
                self.shield = Mathf.Max(0f, self.shield - _apicotSource.remainingAmount);
                _apicotSource.remainingAmount = 0f;
                FireApicotAmp(self);
            }
        }

        // 조개껍질방울/의문열매 — HP 비율이 임계값 이하로 내려가는 순간 전투당 1회 보호막 부여.
        if (_item.hpThresholdPct > 0f && !_thresholdShieldTriggered)
        {
            if (self.maxHp > 0f && self.currentHp / self.maxHp <= _item.hpThresholdPct * 0.01f)
            {
                float grant = self.maxHp * (_item.thresholdShieldPct * 0.01f);
                self.ApplyShield(grant);
                bool isDecay = _item.shieldDecayDuration > 0f; // 데이터 기반 판정(의문열매=decay/조개껍질방울=고정시간) — 이름/ID 분기 아님
                _thresholdSource = new ShieldSource(
                    isDecay ? ShieldSourceType.Micle : ShieldSourceType.ShellBell,
                    grant,
                    isDecay ? ShieldDecayType.LinearDecay : ShieldDecayType.FixedDuration,
                    isDecay ? _item.shieldDecayDuration : _item.shieldDuration,
                    self.NextShieldSequence());
                self.shieldSources.Add(_thresholdSource);
                _thresholdShieldTriggered = true;
                _thresholdShieldActive = true;
            }
        }

        if (_thresholdShieldActive)
        {
            _thresholdSource.elapsedDuration += deltaTime;

            if (_thresholdSource.decayType == ShieldDecayType.LinearDecay)
            {
                // 의문열매 — 선형 decay: 최초 부여량 기준 "허용 상한"을 시간에 따라 낮추고, 현재
                // 보유량이 그 상한을 넘을 때만 초과분을 뺀다(상한이 다시 높아져도 되돌리지 않음).
                float cap = _thresholdSource.initialAmount *
                            Mathf.Max(0f, 1f - _thresholdSource.elapsedDuration / _thresholdSource.totalDuration);
                if (_thresholdSource.remainingAmount > cap)
                {
                    float excess = _thresholdSource.remainingAmount - cap;
                    self.shield = Mathf.Max(0f, self.shield - excess);
                    _thresholdSource.remainingAmount = cap;
                }
                if (_thresholdSource.remainingAmount <= 0f) _thresholdShieldActive = false;
            }
            else if (_thresholdSource.decayType == ShieldDecayType.FixedDuration &&
                     (_thresholdSource.elapsedDuration >= _thresholdSource.totalDuration || _thresholdSource.remainingAmount <= 0f))
            {
                // 조개껍질방울 — 고정시간 후(또는 이미 소진됐으면) 남은 내 몫만 제거. 후속 효과 없음.
                self.shield = Mathf.Max(0f, self.shield - _thresholdSource.remainingAmount);
                _thresholdSource.remainingAmount = 0f;
                _thresholdShieldActive = false;
            }
        }

        // 초점렌즈 — N초마다 스킬피해 +M% 누적(전투 종료까지 상한 없음).
        if (_item.periodicSkillDamageAmpPct > 0f && _item.periodicInterval > 0f)
        {
            _scopeLensElapsed += deltaTime;
            int stacks = Mathf.FloorToInt(_scopeLensElapsed / _item.periodicInterval);

            self.skillDamageAmpPct -= _scopeLensAppliedAmp;
            _scopeLensAppliedAmp = stacks * _item.periodicSkillDamageAmpPct;
            self.skillDamageAmpPct += _scopeLensAppliedAmp;
        }

        // 캄라열매 — 매 주기마다 공격속도 +N%를 "기준값(정적 아이템 효과 반영 후) 대비 가산형"으로 누적.
        if (_item.periodicAttackSpeedPct > 0f && _item.periodicInterval > 0f)
        {
            if (!_salacBaseCaptured)
            {
                _salacBase = self.attackSpeed; // 정적 스탯(ApplyAll) 반영 후, 이 아이템의 매초 보너스 제외 기준값
                _salacBaseCaptured = true;
            }

            _salacElapsed += deltaTime;
            int stacks = Mathf.FloorToInt(_salacElapsed / _item.periodicInterval);

            self.attackSpeed -= _salacAppliedBonus;
            _salacAppliedBonus = _salacBase * (stacks * _item.periodicAttackSpeedPct * 0.01f);
            self.attackSpeed += _salacAppliedBonus;
        }
    }

    /// <summary>규살열매 보호막이 끝났을 때(8초 만료 — OnTick, 또는 피해로 소진 — OnTakeDamage) 스킬피해
    /// 증폭을 정확히 1회만 적용한다. _apicotAmpApplied 가드 하나를 두 호출측이 공유하므로, 시간 만료와
    /// 피해 소진이 같은 틱에 겹쳐도 먼저 도달한 쪽만 발동하고 나머지는 조용히 no-op된다.</summary>
    private void FireApicotAmp(BattleUnit self)
    {
        if (_apicotAmpApplied) return;
        self.skillDamageAmpPct += _item.onShieldEndSkillDamageAmpPct;
        _apicotAmpApplied = true;
    }

    /// <summary>self가 실제로 평타를 가했을 때(ResolveDamage 완료 후, 사거리/쿨다운 체크만으로는
    /// 호출되지 않음) 왕의징표석/선제공격손톱 스택을 올린다.</summary>
    public void OnBasicAttack(BattleUnit self, BattleUnit target)
    {
        if (_item == null) return;
        GainHitStack(self);
    }

    /// <summary>스택 1개 증가 + 스택형 보너스(평타/스킬 피해 증폭, 최대 스택 1회 보너스) 갱신.
    /// maxCombatStacks 도달 후에는 더 이상 증가하지 않는다(재적용 방지).</summary>
    private void GainHitStack(BattleUnit self)
    {
        if (_item.basicAttackDamageAmpPerStackPct <= 0f || _item.maxCombatStacks <= 0f) return;
        if (_hitStacks >= _item.maxCombatStacks) return;

        _hitStacks++;

        self.basicAttackDamageAmpPct += _item.basicAttackDamageAmpPerStackPct;
        if (_item.skillDamageAmpPerStackPct > 0f)
            self.skillDamageAmpPct += _item.skillDamageAmpPerStackPct;

        if (_hitStacks >= _item.maxCombatStacks && !_stackCapBonusApplied)
        {
            _stackCapBonusApplied = true;

            if (_item.maxStackDamageAmpPct > 0f)
                self.damageAmpPct += _item.maxStackDamageAmpPct;

            // "현재 정적/누적 attackSpeed 기준으로 +N% 1회 가산"(기획 확정) — 캄라열매의 매틱 추적
            // (_salacAppliedBonus)과는 완전히 별개 필드/이벤트라 서로의 값을 덮어쓰거나 재계산하지
            // 않는다. 곱셈(×1.15)이 아니라 트리거 시점 값 기준 덧셈이라 그 뒤 캄라열매가 자기 몫만
            // 빼고 다시 더해도(가감 델타 패턴) 이 가산분은 그대로 유지된다.
            if (_item.maxStackAttackSpeedPct > 0f)
            {
                self.attackSpeed += self.attackSpeed * (_item.maxStackAttackSpeedPct * 0.01f);
                // 선제공격손톱 — 최초 15스택 도달 순간에만(위 _stackCapBonusApplied 가드로 이미 1회 보장).
                // 기존 SelfASBuff 스킬 VFX 재사용.
                _battle.PlaySingleTargetVfx("VFX_AS_BUFF", self);
            }
        }
    }

    public void OnTakeDamage(BattleUnit self, DamageContext ctx)
    {
        if (_item == null) return;

        // 왕의징표석 — "피해를 받을 때" 스택. ctx.preShieldAmount(보호막 흡수 "전" 스냅샷)를 쓴다 —
        // "공격받으면"이라는 의미상 보호막이 전부 막아도 발동해야 하므로, 흡수로 줄어드는 ctx.amount를
        // 쓰면 안 된다(2026-08 보호막 흡수 공용화로 확정). 이미 죽은 유닛/무효화된 공격은 ResolveDamage
        // 최상단(target==null || !IsAlive) 가드로 여기까지 오지 않으므로 별도 검사가 필요 없다.
        if (_item.gainStackOnTakeDamage && ctx.preShieldAmount > 0f)
            GainHitStack(self);

        // ① 치명적 피해 시 보호막 생성(아직 보호막이 없을 때만).
        // TODO(2026-08, 보호막 흡수 공용화): 흡수가 이제 BattleManager.ResolveDamage에서 이 메서드보다
        // 먼저 실행되므로, 여기서 생성한 보호막은 "이번 히트"가 아니라 "다음 히트"부터만 막는다(기존엔
        // 같은 히트를 즉시 막았음). 현재 19개 아이템 중 shieldPctOnFatalHit을 쓰는 아이템이 없어(전수
        // 감사 완료) 당장 회귀는 없으나, 이 필드가 실제로 쓰이게 되면 발동 시점과 "같은 히트 방어 여부"를
        // 반드시 재설계해야 한다(예: ResolveDamage에 흡수 전 트리거 단계를 별도로 추가하는 방향 검토).
        if (_item.shieldPctOnFatalHit > 0f && self.shield <= 0f && ctx.amount >= self.currentHp + self.shield)
            self.shield = self.maxHp * (_item.shieldPctOnFatalHit * 0.01f);

        // ② 보호막 흡수는 BattleManager.ResolveDamage가 BattleUnit.AbsorbShieldDamage를 통해 이미
        // 처리했다(아이템 보유 여부와 무관하게 항상 1회) — 여기서는 그 결과(ctx.amount는 이미
        // 흡수 후 값, ctx.depletedShieldSources는 이번 피해로 소진된 추적 출처 목록)만 읽는다.

        // ③ 흡혈 — 보호막 흡수 이후, 실제로 HP에 들어갈 최종 피해량 기준(기획 확정, 2026-08 —
        // 기합의머리띠 "받은 피해량의 12% 회복"이 보호막으로 막힌 만큼은 제외한 실피해 기준이어야 함).
        // 여기서 즉시 self.currentHp를 올리면 아직 이번 피해가 적용되기 전이라 MaxHP 클램프에 막혀
        // 회복분이 소실될 수 있다(만피 상태 실측 확인) — ctx.pendingSelfHeal에 예약만 하고, 실제
        // 적용은 ResolveDamage가 피해를 뺀 직후에 한다.
        if (_item.healTakenDmgPct > 0f)
            ctx.pendingSelfHeal += ctx.amount * (_item.healTakenDmgPct * 0.01f);

        // ④ 반사 — 남은 피해량을 타입별로 가해자에게 직접 적용(ResolveDamage 재귀 금지 — 무한루프 방지).
        float reflectPct = ctx.type == DamageType.Physical ? _item.reflectPhysPct
                          : ctx.type == DamageType.Magic    ? _item.reflectSpPct
                          : 0f;
        if (reflectPct > 0f && ctx.source != null && ctx.source != self)
            ctx.source.currentHp -= ctx.amount * (reflectPct * 0.01f);

        // ⑤ 물리 피격 시 주변 적에게 화상 전이.
        if (_item.burnNearOnPhysHit && ctx.type == DamageType.Physical)
            _battle.ApplyBurnAround(self, BURN_RADIUS, BURN_DAMAGE_PER_TICK, BURN_TICK_COUNT);

        // ⑥ 규살열매 보호막이 이번 피해로 정확히 소진됐으면(ctx.depletedShieldSources에 내 출처가
        // 있으면) 즉시 처리한다 — FireApicotAmp의 _apicotAmpApplied 가드가 8초 만료 경로(OnTick)와
        // 겹쳐도 중복 발동을 막는다.
        if (_item.combatStartShieldPct > 0f && _apicotSource != null && Contains(ctx.depletedShieldSources, _apicotSource))
            FireApicotAmp(self);
    }

    private static bool Contains(System.Collections.Generic.IReadOnlyList<ShieldSource> list, ShieldSource target)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == target) return true;
        return false;
    }

    public void OnKill(BattleUnit self, BattleUnit victim)
    {
        if (_item == null) return;
        if (_item.moveSpdPctOnKill > 0f)
            self.moveSpeedMultiplier += _item.moveSpdPctOnKill * 0.01f; // PLACEHOLDER(기획확정 전): 영구 누적형
    }

    /// <summary>큰뿌리 — self(공격자)가 실제로 가한 최종 HP 피해량(증폭·크리·경감·보호막 흡수·오버킬
    /// 제외까지 전부 반영된 값)의 N%로 팀 내 최저 HP비율 아군 1명을 회복한다. 대상 탐색은 스킬
    /// HP_REGEN/SHIELD 타겟팅이 이미 쓰는 BattleManager.LowestHpRatioAlly를 그대로 재사용 —
    /// same team/IsAlive/비동률 시 _units 순서 결정론이 전부 그쪽 판정 기준을 그대로 따른다(본인도
    /// 자기 팀이라 후보에 자연히 포함됨). actualHpDamage<=0(피해가 실제로 0이었거나 방어/보호막으로
    /// 전부 막힘)이면 회복도 0이라 아무 것도 하지 않는다.</summary>
    public void OnDealDamageResolved(BattleUnit self, BattleUnit target, float actualHpDamage)
    {
        if (_item == null || _item.healLowestAllyPctOfDamage <= 0f || actualHpDamage <= 0f) return;

        BattleUnit lowest = _battle.LowestHpRatioAlly(self.team);
        if (lowest == null) return; // 살아있는 아군이 아무도 없으면(이론상 self 본인도 없다는 뜻) 대상 없음

        float hpBefore = lowest.currentHp;
        lowest.ApplyHeal(actualHpDamage * (_item.healLowestAllyPctOfDamage * 0.01f));

        // 실제로 HP가 늘어났을 때만 재생(이미 만피라 ApplyHeal이 사실상 no-op였던 경우 제외).
        // 기존 치유방울(Normal Supporter 단일 아군 회복) VFX 재사용.
        if (lowest.currentHp > hpBefore)
            _battle.PlaySingleTargetVfx("VFX_Normal_Supporter_HP_REGEN", lowest);
    }
}
