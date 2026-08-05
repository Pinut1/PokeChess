/// <summary>
/// 재생 가능한 사운드의 의미 ID. AudioClip 참조나 재생 로직은 갖지 않는다 —
/// 실제 클립 매핑은 SoundCatalog, 재생은 SoundManager가 담당한다.
/// 새 사운드가 필요하면 이 enum에 값만 추가하고 SoundCatalog에 클립을 등록하면 된다.
/// </summary>
public enum SoundId
{
    None = 0,

    // ── BGM ──
    TitleBgm,
    GameBgm,

    // ── SFX ──
    UiClick,
    ShopReroll,
    UnitBuy,
    UnitSell,
    ItemBuy,
    ItemReroll,
    BattleStart,
    RoundStart,
    Victory,
    Defeat,
    Evolution,
    Reward,
}
