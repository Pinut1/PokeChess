/// <summary>
/// 재생 가능한 사운드의 의미 ID. AudioClip 참조나 재생 로직은 갖지 않는다 —
/// 실제 클립 매핑은 SoundCatalog, 재생은 SoundManager가 담당한다.
/// 새 사운드가 필요하면 이 enum에 값만 추가하고 SoundCatalog에 클립을 등록하면 된다.
/// </summary>
public enum SoundId
{
    None = 0,

    // ── BGM ──
    TitleBgmIntro,  // 한 번만 재생 후 TitleBgm으로 이어짐(SoundManager.PlayBgmWithIntro)
    TitleBgm,       // 타이틀→게임까지 끊김 없이 계속 재생 — 별도 게임용 BGM 없음

    // ── SFX: 버튼(1) ──
    UiClick,
    ShopReroll,
    XpBuy,
    ShopLock,
    ShopUnlock,
    SettingApply,
    PlusleMinunChoiceUIOpen,
    PlusleMinunChoiceClick,

    // ── SFX: 상호작용(2) ──
    UnitBuy,
    UnitSell,
    ItemBuy,
    ItemReroll,
    PickUp,
    Drop,
    Evolution2Star,
    Evolution3Star,
    ItemEquip,
    Error,
    EnemyDeath,
    AugmentOpen,
    AugmentChoice,
    InterestUp,

    // ── SFX: 전투 ──
    BattleStart,
    RoundStart,
    Victory,
    Defeat,
    Reward,
}
