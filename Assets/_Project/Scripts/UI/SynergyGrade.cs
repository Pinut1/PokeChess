/// <summary>
/// 시너지 패널 표시용 등급. 정렬 우선순위와 프레임 스프라이트 선택에 함께 쓴다.
/// enum 값이 클수록 위에 표시된다(고유가 최상단, 비활성이 최하단).
///
/// 판정 규칙(2026-07-28 확정):
/// - tiers가 1개뿐이면 고유 시너지(치어리더/악/얼음) — 문턱이 1이라 비활성 상태가 존재하지 않는다.
/// - 그 외는 activeTierIndex를 그대로 매핑(0=브론즈, 1=실버, 2=골드, 3=프리즘).
///   문턱 3개인 시너지는 인덱스가 2까지만 가므로 자연히 골드가 최대가 된다.
/// </summary>
public enum SynergyGrade
{
    Inactive = 0,
    Bronze   = 1,
    Silver   = 2,
    Gold     = 3,
    Prism    = 4,
    Unique   = 5,
}

public static class SynergyGradeUtil
{
    public static SynergyGrade Of(SynergyStatus status)
    {
        if (status == null || status.data == null || !status.IsActive)
            return SynergyGrade.Inactive;

        var tiers = status.data.tiers;
        if (tiers != null && tiers.Count == 1)
            return SynergyGrade.Unique;

        switch (status.activeTierIndex)
        {
            case 0:  return SynergyGrade.Bronze;
            case 1:  return SynergyGrade.Silver;
            case 2:  return SynergyGrade.Gold;
            default: return SynergyGrade.Prism;
        }
    }
}
