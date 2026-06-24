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

    public ItemConditionalEffect(ItemData item, BattleManager battle)
    {
        _item = item;
        _battle = battle;
    }

    public void OnTick(BattleUnit self, float deltaTime)
    {
        if (_item == null) return;

        if (_item.hpRegenPercent > 0f)
        {
            self.currentHp = Mathf.Min(self.maxHp, self.currentHp + self.maxHp * _item.hpRegenPercent * deltaTime);
        }

        if (_item.defSpDefPerAttacker > 0f)
        {
            self.defense -= _appliedDefBonus;
            _appliedDefBonus = _battle.CountAttackersInRange(self) * _item.defSpDefPerAttacker;
            self.defense += _appliedDefBonus;
        }
    }

    public void OnTakeDamage(BattleUnit self, DamageContext ctx)
    {
        if (_item == null) return;

        // ① 흡혈 — 보호막으로 막히기 전, 원본 피해량 기준.
        if (_item.healTakenDmgPct > 0f)
            self.currentHp = Mathf.Min(self.maxHp, self.currentHp + ctx.amount * _item.healTakenDmgPct);

        // ② 치명적 피해 시 보호막 생성(아직 보호막이 없을 때만).
        if (_item.shieldPctOnFatalHit > 0f && self.shield <= 0f && ctx.amount >= self.currentHp + self.shield)
            self.shield = self.maxHp * _item.shieldPctOnFatalHit;

        // ③ 보호막 흡수.
        if (self.shield > 0f)
        {
            float absorbed = Mathf.Min(self.shield, ctx.amount);
            self.shield -= absorbed;
            ctx.amount -= absorbed;
        }

        // ④ 반사 — 남은 피해량을 타입별로 가해자에게 직접 적용(ResolveDamage 재귀 금지 — 무한루프 방지).
        float reflectPct = ctx.type == DamageType.Physical ? _item.reflectPhysPct
                          : ctx.type == DamageType.Magic    ? _item.reflectSpPct
                          : 0f;
        if (reflectPct > 0f && ctx.source != null && ctx.source != self)
            ctx.source.currentHp -= ctx.amount * reflectPct;

        // ⑤ 물리 피격 시 주변 적에게 화상 전이.
        if (_item.burnNearOnPhysHit && ctx.type == DamageType.Physical)
            _battle.ApplyBurnAround(self, BURN_RADIUS, BURN_DAMAGE_PER_TICK, BURN_TICK_COUNT);
    }

    public void OnKill(BattleUnit self, BattleUnit victim)
    {
        if (_item == null) return;
        if (_item.moveSpdPctOnKill > 0f)
            self.moveSpeedMultiplier += _item.moveSpdPctOnKill; // PLACEHOLDER(기획확정 전): 영구 누적형
    }
}
