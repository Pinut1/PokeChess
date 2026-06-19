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
    [SerializeField] private RewardManager     _rewardManager;
    [SerializeField] private SynergyManager    _synergyManager;
    [SerializeField] private ItemManager       _itemManager;
    [SerializeField] private AugmentManager    _augmentManager;
    [SerializeField] private UIManager         _uiManager;
    [SerializeField] private PlayerHealthManager _playerHealthManager;

    // 외부에서 읽기 전용 접근
    public NetworkManager    Network   => _networkManager;
    public RoundPhaseManager Phase     => _roundPhaseManager;
    public BoardManager      Board     => _boardManager;
    public BattleManager     Battle    => _battleManager;
    public ShopManager       Shop      => _shopManager;
    public RewardManager     Reward    => _rewardManager;
    public SynergyManager    Synergy   => _synergyManager;
    public ItemManager       Item      => _itemManager;
    public AugmentManager    Augment   => _augmentManager;
    public UIManager         UI        => _uiManager;
    public PlayerHealthManager PlayerHealth => _playerHealthManager;

    // GameManager는 매니저들을 같은 GameObject에 컴포넌트로 들고 있다. DontDestroyOnLoad로 유지하면
    // 씬 전환 시 중복 인스턴스가 통째로 Destroy되며 게임플레이 매니저까지 사라지므로 씬 로컬로 둔다.
    // (Photon 연결 자체는 PUN 내부 PhotonHandler가 씬을 넘어 유지하므로 영속 불필요)
    protected override bool KeepAcrossScenes => false;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return; // 중복 인스턴스는 base.Awake에서 이미 Destroy됨

        ValidateReferences();
        // 각 매니저는 Awake/OnEnable/Start로 자체 초기화하며 GameEvents로만 통신하므로
        // 별도의 Init() 순서 제어는 불필요. 매니저 간 직접 의존이 생기면 그때 도입.
    }

    private void ValidateReferences()
    {
        // 네트워크 계층은 모든 씬(로비/게임)에 필수.
        if (_networkManager    == null) Debug.LogError("[GameManager] NetworkManager 참조 누락");
        if (_roundPhaseManager == null) Debug.LogError("[GameManager] RoundPhaseManager 참조 누락");

        // 게임플레이 매니저는 GameScene에서만 존재. 로비 씬에선 비어 있는 게 정상이라 경고로만.
        if (_boardManager      == null) Debug.LogWarning("[GameManager] BoardManager 미연결 (게임 씬이 아니면 정상)");
        if (_battleManager     == null) Debug.LogWarning("[GameManager] BattleManager 미연결 (게임 씬이 아니면 정상)");
        if (_shopManager       == null) Debug.LogWarning("[GameManager] ShopManager 미연결 (게임 씬이 아니면 정상)");
        if (_rewardManager     == null) Debug.LogWarning("[GameManager] RewardManager 미연결 (게임 씬이 아니면 정상)");
        if (_synergyManager    == null) Debug.LogWarning("[GameManager] SynergyManager 미연결 (게임 씬이 아니면 정상)");
        if (_playerHealthManager == null) Debug.LogWarning("[GameManager] PlayerHealthManager 미연결 (게임 씬이 아니면 정상)");
        // ItemManager/AugmentManager/UIManager는 아직 스텁 — 검증 생략.
    }
}
