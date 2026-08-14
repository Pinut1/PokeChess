/// <summary>추적되는 보호막 출처 종류. Skill=유닛의 SHIELD 타입 스킬, 나머지는 장비 3종.</summary>
public enum ShieldSourceType { Skill, Apicot, ShellBell, Micle }

/// <summary>출처의 잔량이 시간에 따라 어떻게 줄어드는지. None=시간 만료 없음(피해로만 소진, 스킬 전용),
/// FixedDuration=정해진 시간 뒤 잔량 전체 제거(규살열매/조개껍질방울), LinearDecay=시간에 비례해
/// 선형으로 상한이 줄어듦(의문열매).</summary>
public enum ShieldDecayType { None, FixedDuration, LinearDecay }

/// <summary>
/// 하나의 보호막 출처(스킬 SHIELD 또는 장비 1개)가 BattleUnit.shield(공유 총량, 권위값)에 기여한 몫을
/// 정확히 추적하는 데이터 클래스. BattleUnit.shieldSources 리스트에 담겨 BattleUnit.AbsorbShieldDamage가
/// 피해를 순서대로 분배해 remainingAmount를 직접 차감한다 — 각 출처가 셀 필요 없이 항상 정확한 값이다.
/// 소유자(ItemConditionalEffect 등)는 자기가 만든 인스턴스의 참조를 직접 들고 있다가 시간 경과/decay를
/// 이 객체에 직접 반영한다. 순수 데이터 홀더라 로직(종료 이벤트 등)은 소유자 쪽에서 판단한다.
/// </summary>
public sealed class ShieldSource
{
    public readonly ShieldSourceType type;
    public float remainingAmount;
    public readonly float initialAmount;      // LinearDecay 상한 계산용(최초 부여량 고정)
    public float elapsedDuration;             // 기존 프로젝트 관례(_apicotElapsed 등)와 동일한 누적 방식
    public readonly float totalDuration;      // decayType==None이면 미사용
    public readonly ShieldDecayType decayType;
    public readonly int sequence;             // 이 유닛 내 생성 순서(FIFO 판정용, BattleUnit.NextShieldSequence 발급)

    public ShieldSource(ShieldSourceType type, float amount, ShieldDecayType decayType, float totalDuration, int sequence)
    {
        this.type = type;
        remainingAmount = amount;
        initialAmount = amount;
        this.decayType = decayType;
        this.totalDuration = totalDuration;
        this.sequence = sequence;
    }
}

/// <summary>BattleUnit.AbsorbShieldDamage 1회 호출의 결과. remainingDamage는 보호막 흡수 후 남은 피해량
/// (기존 ctx.amount -= absorbed와 동일 의미), depletedSources는 이번 호출로 remainingAmount가 정확히
/// 0에 도달한 추적 출처들(항상 non-null — 없으면 공유 빈 리스트).</summary>
public readonly struct ShieldAbsorbResult
{
    public readonly float remainingDamage;
    public readonly System.Collections.Generic.IReadOnlyList<ShieldSource> depletedSources;

    public ShieldAbsorbResult(float remainingDamage, System.Collections.Generic.IReadOnlyList<ShieldSource> depletedSources)
    {
        this.remainingDamage = remainingDamage;
        this.depletedSources = depletedSources;
    }
}
