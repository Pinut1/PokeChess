using UnityEngine;

/// <summary>
/// 게임 전체 매니저 허브.
/// 각 매니저는 여기서 참조. 매니저끼리는 직접 참조 금지 — GameEvents 사용.
/// </summary>
public class GameManager : Singleton<GameManager>
{
    [Header("매니저 참조")]
    [SerializeField] private NetworkManager    _networkManager;
    [SerializeField] private RoundPhaseManager _roundPhaseManager;
    [SerializeField] private BoardManager      _boardManager;
    [SerializeField] private BattleManager     _battleManager;
    [SerializeField] private ShopManager       _shopManager;
    [SerializeField] private SynergyManager    _synergyManager;
    [SerializeField] private ItemManager       _itemManager;
    [SerializeField] private AugmentManager    _augmentManager;
    [SerializeField] private UIManager         _uiManager;

    // 외부에서 읽기 전용 접근
    public NetworkManager    Network   => _networkManager;
    public RoundPhaseManager Phase     => _roundPhaseManager;
    public BoardManager      Board     => _boardManager;
    public BattleManager     Battle    => _battleManager;
    public ShopManager       Shop      => _shopManager;
    public SynergyManager    Synergy   => _synergyManager;
    public ItemManager       Item      => _itemManager;
    public AugmentManager    Augment   => _augmentManager;
    public UIManager         UI        => _uiManager;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return; // 중복 인스턴스는 base.Awake에서 이미 Destroy됨

        ValidateReferences();
        // TODO: 매니저 초기화 순서 확정 후 Init() 호출 추가
    }

    private void ValidateReferences()
    {
        if (_networkManager    == null) Debug.LogError("[GameManager] NetworkManager 참조 누락");
        if (_roundPhaseManager == null) Debug.LogError("[GameManager] RoundPhaseManager 참조 누락");
        if (_boardManager      == null) Debug.LogError("[GameManager] BoardManager 참조 누락");
        if (_battleManager     == null) Debug.LogError("[GameManager] BattleManager 참조 누락");
        if (_shopManager       == null) Debug.LogError("[GameManager] ShopManager 참조 누락");
        if (_synergyManager    == null) Debug.LogError("[GameManager] SynergyManager 참조 누락");
        if (_itemManager       == null) Debug.LogError("[GameManager] ItemManager 참조 누락");
        if (_augmentManager    == null) Debug.LogError("[GameManager] AugmentManager 참조 누락");
        if (_uiManager         == null) Debug.LogError("[GameManager] UIManager 참조 누락");
    }
}
