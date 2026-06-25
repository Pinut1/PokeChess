/// <summary>
/// 치어리더 시너지의 플레이어 선택(공격속도 / 마나충전). SynergyHud(선택 UI)와 BattleManager(적용)가 공유하는
/// 프로토 단계의 단순 전역 상태. 정식 선택 UI/저장은 추후 AugmentManager(황해인)로 이관.
/// </summary>
public static class CheerleaderChoice
{
    public enum Option { AttackSpeed, ManaRegen }

    /// <summary>현재 선택(기본 공격속도). SynergyHud 토글이 변경, BattleManager가 전투 시작 시 읽음.</summary>
    public static Option Current = Option.AttackSpeed;
}
