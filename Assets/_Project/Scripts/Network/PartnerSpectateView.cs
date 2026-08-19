using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 파트너 미러 전투(PartnerBattleMirrorController)를 별도 카메라로 찍어
/// 씬에 이미 배치된 PartnerPipPanel/PartnerPipRawImage/PartnerPipLabel/PartnerViewButton에
/// 표시하는 뷰. 계산에는 전혀 관여하지 않는 순수 표시 레이어다.
///
/// ⚠️ 이 스크립트는 <b>항상 활성인 오브젝트(Canvas 등)</b>에 붙여야 한다(AugmentInfoPanel과 동일한
/// 이유) — _pipPanel을 이 스크립트가 직접 켜고 끄는데, 만약 이 스크립트 자신이 _pipPanel 위에
/// 붙어 있으면 팬널을 끄는 순간 Update가 멈춰 미러 상태 폴링·카메라 갱신이 통째로 죽는다.
///
/// 디버그 PIP(_debugShowPip)는 평상시 화면 우측 상단 미니 프리뷰를 켤지 여부만 결정하고,
/// 카메라/RenderTexture 생성이나 미러 전투 폴링에는 전혀 영향을 주지 않는다 — 항상 같은 경로로
/// 만들어지고 갱신된다(요구사항: "디버그 PIP 활성 여부가 미러 전투 계산이나 카메라 생성에
/// 영향을 주지 않도록 한다").
/// </summary>
public class PartnerSpectateView : MonoBehaviour
{
    private const int MIN_RENDER_TEXTURE_WIDTH = 1280;
    private const int MIN_RENDER_TEXTURE_HEIGHT = 720;
    private const int MAX_RENDER_TEXTURE_WIDTH = 1920;
    private const int MAX_RENDER_TEXTURE_HEIGHT = 1080;

    [Header("PIP 대상 (Inspector에서 직접 연결)")]
    [Tooltip("PartnerPipPanel의 RectTransform. 확대/복귀 시 이 RectTransform을 직접 조작한다.")]
    [SerializeField] private RectTransform _pipPanel;

    [Tooltip("PartnerPipRawImage — 관전 카메라의 RenderTexture를 표시할 RawImage.")]
    [SerializeField] private RawImage _pipRawImage;

    [Tooltip("PartnerPipLabel — 미러 전투가 없을 때 \"준비 중\" 문구를 표시할 텍스트.")]
    [SerializeField] private TextMeshProUGUI _pipLabel;

    [Tooltip("(구) PartnerViewButton — 누를 때마다 내 화면 ↔ 파트너 화면을 번갈아 연다.\n" +
             "아래 프로필 버튼 두 개로 옮겨가는 중이라, 새 버튼을 물렸으면 이 칸은 비워 두면 된다.")]
    [SerializeField] private Button _externalViewButton;

    [Header("프로필 버튼 (내 화면 / 파트너 화면)")]
    [Tooltip("내 프로필 버튼. 누르면 항상 내 화면으로 돌아온다 — 이미 내 화면이면 아무 일도 하지 않는다.")]
    [SerializeField] private Button _myViewButton;

    [Tooltip("파트너 프로필 버튼. 누르면 항상 파트너 화면을 연다 — 이미 열려 있으면 아무 일도 하지 않는다.")]
    [SerializeField] private Button _partnerViewButton;

    [Tooltip("(선택) 내 프로필 버튼의 테두리 프레임. 지금 보고 있는 쪽만 아래 '활성 색'으로 칠한다.")]
    [SerializeField] private Image _myViewFrame;

    [Tooltip("(선택) 파트너 프로필 버튼의 테두리 프레임.")]
    [SerializeField] private Image _partnerViewFrame;

    [Tooltip("방을 만든 사람(방장)의 프로필 색.")]
    [SerializeField] private Color _hostColor = new(0.62f, 0.40f, 0.90f);   // 보라

    [Tooltip("방에 들어온 사람(참가자)의 프로필 색.")]
    [SerializeField] private Color _guestColor = new(0.30f, 0.60f, 0.95f);  // 파랑

    [Tooltip("방에 들어가기 전(로비·솔로·오프라인)의 프로필 색. 누가 방장인지 아직 정해지지 않은 상태다.")]
    [SerializeField] private Color _offlineColor = new(0.55f, 0.55f, 0.58f);  // 회색

    [Tooltip("보고 있지 않은 쪽을 얼마나 어둡게 할지. 1이면 구분 없음, 낮출수록 어두워진다(0.3 아래로 내리면 색이 거의 검게 보인다). 알파는 건드리지 않아 항상 불투명하다. 색(보라/파랑/회색)은 누구인지를, 밝기는 지금 어느 화면을 보고 있는지를 나타낸다.")]
    [Range(0f, 1f)]
    [SerializeField] private float _inactiveDim = 0.6f;

    [Header("디버그")]
    [Tooltip("켜면 평상시에도 우측 상단에 작은 PIP 미리보기를 띄운다. 끄면(기본) 최종 플레이 화면처럼 " +
             "PartnerViewButton으로 전체화면을 열 때만 파트너 화면이 보인다. QA/포트폴리오 확인용으로 유지.")]
    [SerializeField] private bool _debugShowPip = false;

    private Camera _spectatorCamera;
    private RenderTexture _spectatorTexture;

    // 마지막으로 확인한 역할. 바뀌면 RefreshFramesOnRoleChange가 프로필 색을 다시 칠한다.
    // null = 아직 한 번도 확인 안 함(첫 프레임에 무조건 칠한다).
    private ProfileRole? _lastKnownRole;

    // 마지막으로 확인한 파트너 유무. 입장/퇴장 때 흐리기가 바뀌므로 같이 감시한다.
    private bool _lastKnownHasPartner;

    // 마지막으로 RenderTexture 크기를 맞춘 Main Camera 렌더 크기. 화면(Screen) 크기가 아니다 —
    // 이유는 RefreshTextureOnScreenChange 참고. 바뀌면 텍스처를 다시 만든다.
    private int _lastViewWidth;
    private int _lastViewHeight;
    private PartnerBattleMirrorController _mirrorController;
    private TextMeshProUGUI _externalViewButtonLabel;

    private bool _wasMirrorRunning;
    private bool _isExpanded;

    /// <summary>이번 라운드 팀 결과(GameEvents.OnTeamRoundResolved)가 이미 도착했는지. RoundPhaseManager의
    /// 동명 필드(_teamRoundResolved)와 같은 의미를 이 클래스 안에서 독립적으로 추적한다 — 매니저를 직접
    /// 참조하지 않고 이벤트만으로 판단하기 위함(CLAUDE.md 원칙). true면 이번 라운드 캐시 스냅샷으로
    /// MirrorBattle을 재시작하지 않는다(TryStartMirrorBattleFromCache). 다음 라운드 진입 시
    /// (HandleStageEntered) false로 리셋된다.</summary>
    private bool _teamRoundResolved;

    /// <summary>지금 파트너 화면이 전체화면으로 열려 있는지(PIP 축소 상태는 포함하지 않음).
    /// UnitStatusBarHud 등 "지금 내 로컬 화면 전체가 파트너 관전으로 덮여 있는지"만 알면 되는
    /// 다른 컴포넌트가 참조한다 — 이 클래스가 그 UI들을 직접 켜고 끄지는 않는다.</summary>
    public bool IsExpanded => _isExpanded;

    /// <summary>
    /// 지금 PIP/전체화면 어느 쪽으로든 관전 화면에 실제로 내용이 그려지고 있는지.
    /// UpdateCameraState가 관전 카메라를 켤지 정할 때 쓰는 것과 같은 기준(_spectatorCamera.enabled)을
    /// 그대로 읽는다 — 조건을 다시 계산하지 않고 이미 계산된 카메라 상태를 재사용한다.
    /// UnitStatusBarHud가 미러 유닛 HP/마나 바를 그릴지 판단하는 데 쓴다.
    /// </summary>
    public bool IsShowingContent => _spectatorCamera != null && _spectatorCamera.enabled;

    /// <summary>관전 카메라. UnitStatusBarHud가 미러 유닛 HP/마나 바 위치를 이 카메라 기준으로
    /// 투영할 때 쓴다(WorldToViewportPoint). 아직 만들어지기 전(첫 미러 전투 전)이면 null.</summary>
    public Camera SpectatorCamera => _spectatorCamera;

    /// <summary>관전 화면을 그리는 RawImage. UnitStatusBarHud가 미러 HP/마나 바를 이 RawImage의
    /// 자식으로 배치해 PIP/전체화면 전환·표시/숨김을 별도 처리 없이 그대로 따라가게 한다.</summary>
    public RawImage PipRawImage => _pipRawImage;

    /// <summary>미러 전투 컨트롤러. UnitStatusBarHud가 미러 BattleUnit 목록(MirrorUnits)을 읽을 때 쓴다.</summary>
    public PartnerBattleMirrorController MirrorController => _mirrorController;

    // GetWorldCorners 호출마다 새 배열을 만들지 않으려고 재사용하는 버퍼(TryProjectWorldToScreen 전용).
    private readonly Vector3[] _pipCornersBuffer = new Vector3[4];

    /// <summary>
    /// 월드 좌표를 관전 카메라 뷰포트 → PipRawImage 사각형으로 투영한다. 카메라 뒤(z&lt;=0)거나
    /// 카메라/RawImage가 아직 준비 안 됐으면(첫 미러 전투 전 등) 실패. AugmentInfoTrigger/
    /// StatInfoController/UnitStatusBarHud가 공용으로 쓴다 — 이 계산의 유일한 소스로 둬서 세 곳 중
    /// 하나만 고치고 나머지를 놓치는 일을 막는다.
    /// </summary>
    public bool TryProjectWorldToScreen(Vector3 worldPos, out Vector2 screenPos)
    {
        if (_spectatorCamera == null || _pipRawImage == null) { screenPos = default; return false; }

        Vector3 viewport = _spectatorCamera.WorldToViewportPoint(worldPos);
        if (viewport.z <= 0f) { screenPos = default; return false; }

        _pipRawImage.rectTransform.GetWorldCorners(_pipCornersBuffer);

        screenPos = new Vector2(
            Mathf.Lerp(_pipCornersBuffer[0].x, _pipCornersBuffer[2].x, viewport.x),
            Mathf.Lerp(_pipCornersBuffer[0].y, _pipCornersBuffer[2].y, viewport.y));
        return true;
    }

    private Vector2 _originalAnchorMin;
    private Vector2 _originalAnchorMax;
    private Vector2 _originalPivot;
    private Vector2 _originalAnchoredPosition;
    private Vector2 _originalSizeDelta;

    // PartnerPipRawImage(PartnerPipPanel의 자식)는 PIP 상태에서 자체 테두리 여백(sizeDelta 인셋)을
    // 갖는 별도 RectTransform이다. 전체화면에서 이 인셋이 남아 있으면 RenderTexture 내용이 화면보다
    // 살짝 작게(중앙 기준 축소) 그려져 내 화면 ↔ 관전 화면 전환 시 지형·타일·유닛 전체가 미세하게
    // 움직이고 작아 보인다(실측 확인된 원인). Inspector 값을 하드코딩하지 않고 Awake에서 실행 중
    // 실제 값을 읽어 캐시해 그대로 복원한다.
    private Vector2 _originalRawImageAnchorMin;
    private Vector2 _originalRawImageAnchorMax;
    private Vector2 _originalRawImagePivot;
    private Vector2 _originalRawImageAnchoredPosition;
    private Vector2 _originalRawImageSizeDelta;

    private void Awake()
    {
        if (_pipPanel == null || _pipRawImage == null || _pipLabel == null)
        {
            Debug.LogError("[PartnerSpectateView] Inspector 연결 누락 — _pipPanel/_pipRawImage/_pipLabel을 모두 연결해야 한다");
            enabled = false;
            return;
        }

        // 루트 Canvas 안에서 _pipPanel을 맨 앞 형제로 고정한다 — PartnerPipPanel 내부 자식 순서는
        // 그대로 두고, 루트 Canvas 레벨의 형제 순서만 바꾼다. 전체화면으로 늘어났을 때(ExpandPip)
        // 이 패널이 QA 패널/설정 버튼/PartnerViewButton/OptionsPanel 같은 다른 루트 Canvas 형제를
        // 가리지 않도록 하기 위함(형제 인덱스가 낮을수록 먼저 그려져 뒤에 깔린다).
        _pipPanel.transform.SetAsFirstSibling();

        CacheOriginalLayout();
        _mirrorController = PartnerBattleMirrorController.GetOrCreate();
        _externalViewButtonLabel = _externalViewButton != null
            ? _externalViewButton.GetComponentInChildren<TextMeshProUGUI>()
            : null;

        ApplyMainCameraCullingMask();

        UpdateViewButtonLabel();
        UpdateViewFrames();
        UpdatePipContent();
        RefreshPanelVisibility();
    }

    private void OnEnable()
    {
        // 관전 열기/닫기는 _externalViewButton 하나로만 한다 — _pipPanel 자신에 달린 Button은
        // 더 이상 연결하지 않는다. 전체화면으로 확장되면(ExpandPip) 이 패널의 배경 Image가 화면
        // 전체를 덮는 레이캐스트 타깃이 되어, 여기 리스너를 걸어두면 화면 아무 곳이나 클릭해도
        // ToggleExpanded가 호출돼 버린다(실측 확인된 버그) — 그래서 이 버튼은 더 이상 쓰지 않는다.
        if (_externalViewButton != null) _externalViewButton.onClick.AddListener(ToggleExpanded);

        // 토글이 아니라 "그 화면으로 간다"다 — 이미 그 상태면 SetExpanded가 스스로 빠져나간다.
        if (_myViewButton != null) _myViewButton.onClick.AddListener(ShowMyView);
        if (_partnerViewButton != null) _partnerViewButton.onClick.AddListener(ShowPartnerView);

        GameEvents.OnStageEntered += HandleStageEntered;
        GameEvents.OnPartnerBattleSnapshotChanged += HandlePartnerBattleSnapshotChanged;
        GameEvents.OnOpponentDisconnected += HandleOpponentDisconnected;
        GameEvents.OnTeamRoundResolved += HandleTeamRoundResolved;
    }

    private void OnDisable()
    {
        if (_externalViewButton != null) _externalViewButton.onClick.RemoveListener(ToggleExpanded);

        if (_myViewButton != null) _myViewButton.onClick.RemoveListener(ShowMyView);
        if (_partnerViewButton != null) _partnerViewButton.onClick.RemoveListener(ShowPartnerView);

        GameEvents.OnStageEntered -= HandleStageEntered;
        GameEvents.OnPartnerBattleSnapshotChanged -= HandlePartnerBattleSnapshotChanged;
        GameEvents.OnOpponentDisconnected -= HandleOpponentDisconnected;
        GameEvents.OnTeamRoundResolved -= HandleTeamRoundResolved;

        // 방어 처리 — ToggleExpanded()를 거치지 않고(씬 전환 등으로) 이 컴포넌트가 켜진 채로
        // 비활성화/파괴되면 OnPartnerSpectateExpandedChanged(false)가 영영 발행되지 않아
        // UIManager의 상점 HUD 숨김 상태가 고착될 수 있다. SetExpanded가 이미 false면 아무 일도
        // 하지 않으므로(중복 발행 없음) 정상 종료 경로와 겹쳐도 안전하다. Destroy 시에는 OnDisable이
        // OnDestroy보다 먼저 호출되므로 이 한 곳만으로 Disable/Destroy 양쪽을 커버한다.
        if (_isExpanded) SetExpanded(false);
    }

    /// <summary>
    /// 파트너 연결 끊김 감지 시, 전체화면 관전 중이었다면 자동으로 닫는다(상점 HUD 복원).
    /// PIP(_debugShowPip)는 건드리지 않는다 — 일반 플레이에서는 항상 false로 꺼져 있는 QA 전용
    /// 값이고, 상점 HUD 숨김도 PIP과는 무관(전체화면 전용)이라 강제로 끌 이유가 없다.
    /// 재접속 판정/NetworkManager 로직에는 관여하지 않는다 — 여기서는 표시 상태만 정리한다.
    /// </summary>
    private void HandleOpponentDisconnected(float graceSeconds)
    {
        if (_isExpanded) SetExpanded(false);
    }

    /// <summary>
    /// 양쪽 플레이어의 실제 전투 결과가 모두 확정되는 순간(GameEvents.OnTeamRoundResolved) 발행되며,
    /// 이번 라운드의 파트너 관전(MirrorBattle 재현)을 실제 라운드 진행보다 먼저 정리한다. MirrorBattle은
    /// 실시간 중계가 아니라 로컬 재현이라 관전을 늦게 시작하면 실제 전투가 끝난 뒤에도 계속 재생될 수
    /// 있는데, "양쪽 실제 전투 종료 확정" 자체는 이미 NetworkManager가 판정해 이 이벤트로 알려주므로
    /// 그대로 신호로 재사용한다(새 판정 로직 없음).
    ///
    /// MirrorBattle 정지는 관전 패널이 닫혀 있어도(_isExpanded==false) 항상 수행한다 — 패널을 닫아도
    /// 미러 전투 자체는 배경에서 계속 시뮬레이션될 수 있으므로(StartOrReplaceMirrorBattle 주석 참고),
    /// 화면에 보이지 않아도 이제 의미가 없어진 재현을 정리한다. 화면 복귀(SetExpanded(false))는 실제로
    /// 관전 중일 때만 실행한다 — 이미 꺼져 있으면 SetExpanded가 스스로 no-op 처리한다.
    ///
    /// 실제 BattleManager/라운드 진행/전투 결과 판정에는 전혀 관여하지 않는다 — 이 클래스는 그 결과를
    /// 구독만 할 뿐 아무것도 발행하지 않는다.
    /// </summary>
    private void HandleTeamRoundResolved(TeamRoundOutcome outcome)
    {
        _teamRoundResolved = true;

        if (_mirrorController != null) _mirrorController.StopMirrorBattle();
        if (_isExpanded) SetExpanded(false);
    }

    /// <summary>
    /// 관전 패널이 열려 있는 동안 라운드/스테이지가 바뀌면 파트너 적 프리뷰를 최신 스테이지로 다시
    /// 그린다. 닫혀 있으면 무시한다 — 다음에 열 때(ToggleExpanded) 항상 그 시점의 CurrentStage로
    /// 새로 그리므로 놓치지 않는다.
    ///
    /// RoundPhaseManager.HandleRoundChanged가 자신의 _teamRoundResolved를 리셋하는 것과 같은 타이밍에
    /// (OnRoundChanged → ResolveCurrentStage → OnStageEntered, RoundPhaseManager.cs 참고) 이 클래스의
    /// _teamRoundResolved도 함께 리셋한다 — 새 이벤트 구독을 추가하지 않고 이미 구독 중인 OnStageEntered를
    /// 재사용한다. _isExpanded 여부와 무관하게 항상 리셋해야 다음 라운드에 정상적으로 재관전할 수 있다.
    /// </summary>
    private void HandleStageEntered(StageData stage)
    {
        _teamRoundResolved = false;

        if (!_isExpanded) return;
        if (_mirrorController != null) _mirrorController.ShowPartnerEnemyPreview(stage);
    }

    /// <summary>
    /// 새 파트너 BattleSnapshot을 수신했을 때(전투 시작 자동 전송 등) 관전 중이면 곧바로 미러 전투로
    /// 전환한다. 관전 중이 아니면 아무 것도 하지 않는다 — 스냅샷은 NetworkManager가 이미 캐시해뒀으므로
    /// (RPC_OnBattleSnapshot) 나중에 관전을 열 때 ToggleExpanded의 캐시 확인 경로(TryStartMirrorBattleFromCache)가
    /// 따라잡는다. 내 매치가 이미 끝났으면(IsMatchEnded) 새 스냅샷이 와도 미러를 새로 시작/교체하지
    /// 않는다(2026-08 코드리뷰 대응 — 버튼 경로만 막고 이 이벤트 경로를 열어두면 우회로가 된다).
    /// </summary>
    private void HandlePartnerBattleSnapshotChanged(BattleSnapshot snapshot)
    {
        if (!_isExpanded) return;
        if (GameManager.TryGet(out var gm) && gm.Phase != null && IsMatchEnded(gm.Phase.CurrentPhase)) return;
        StartOrReplaceMirrorBattle(snapshot);
    }

    private void OnDestroy()
    {
        if (_spectatorCamera != null) Destroy(_spectatorCamera.gameObject);
        if (_spectatorTexture != null)
        {
            _spectatorTexture.Release();
            Destroy(_spectatorTexture);
        }

        // _mirrorController(PartnerBattleMirrorController)는 DontDestroyOnLoad라 이 컴포넌트가
        // 씬 전환으로 파괴돼도 그 자식 MirrorBattleManager 아래 남은 적 프리뷰 타일/모델과 진행 중
        // 미러 전투 비주얼은 저절로 사라지지 않는다(2026-08 QA: 타이틀 화면에 이전 게임 보드/적군
        // 타일이 잔존하는 버그의 원인). 이 컴포넌트는 항상 활성 오브젝트에 붙어 있어(클래스 주석)
        // OnDestroy가 오직 씬 언로드 시에만 호출되므로, 여기서 정리해도 단순 패널 닫기 동작에는
        // 영향이 없다.
        if (_mirrorController != null)
        {
            _mirrorController.ClearPartnerEnemyPreview();
            _mirrorController.StopMirrorBattle();
        }
    }

    private void Update()
    {
        bool running = _mirrorController != null && _mirrorController.IsRunning;
        if (running && !_wasMirrorRunning) HandleMirrorStarted();
        else if (!running && _wasMirrorRunning) HandleMirrorEnded();
        _wasMirrorRunning = running;

        RefreshTextureOnScreenChange();
        RefreshFramesOnRoleChange();
    }

    /// <summary>
    /// 역할이나 파트너 유무가 바뀌면 프로필 색을 다시 칠한다. UpdateViewFrames는 시작 시점과 화면 전환에서만
    /// 불리는데, 그 시점엔 대개 아직 방 밖(Offline=회색)이라 입장 후 한 번은 다시 칠해야 한다.
    /// 호스트 마이그레이션이나 방 퇴장(다시 회색)도 같은 경로로 따라간다.
    /// </summary>
    private void RefreshFramesOnRoleChange()
    {
        ProfileRole role = ResolveLocalRole();
        bool hasPartner = HasPartner();

        if (_lastKnownRole.HasValue && _lastKnownRole.Value == role &&
            _lastKnownHasPartner == hasPartner)
            return;

        _lastKnownRole = role;
        _lastKnownHasPartner = hasPartner;
        UpdateViewFrames();
    }

    /// <summary>
    /// 그리는 크기가 바뀌면 RenderTexture를 다시 만든다. EnsureSpectatorTexture는 미러 전투 시작과
    /// 관전 열기/닫기에서만 불리므로, 관전 중에 옵션창에서 해상도를 바꾸면 종횡비가 안 맞는
    /// 텍스처가 그대로 남아 화면이 늘어나 보인다(레터박스가 붙으면서 더 눈에 띈다).
    /// 텍스처가 아직 없으면(관전을 한 번도 안 열었으면) 아무 것도 하지 않는다 — 필요할 때 만들어진다.
    /// </summary>
    private void RefreshTextureOnScreenChange()
    {
        if (_spectatorTexture == null) return;

        // 감시 대상은 화면(Screen)이 아니라 Main Camera가 실제로 그리는 크기다 —
        // ComputeDesiredRenderTextureSize가 쓰는 값이 바로 그것이기 때문이다.
        //
        // Screen을 기준으로 삼으면 안 되는 이유: CameraLetterbox는 LateUpdate에서 rect를 고치는데
        // 이 메서드는 Update에서 돈다. 해상도가 바뀐 그 프레임에는 Screen만 새 값이고 rect는 아직
        // 옛 값이라, 틀린 종횡비로 텍스처를 만든다. 그리고 다음 프레임엔 Screen 크기가 그대로라
        // 조건에 걸리지 않아 영영 다시 만들 기회가 오지 않는다.
        // 렌더 크기를 보면 rect가 고쳐진 다음 프레임에 자연히 잡힌다(1프레임 지연은 체감되지 않는다).
        Camera mainCamera = Camera.main;
        int viewWidth  = mainCamera != null ? mainCamera.pixelWidth  : Screen.width;
        int viewHeight = mainCamera != null ? mainCamera.pixelHeight : Screen.height;

        if (viewWidth == _lastViewWidth && viewHeight == _lastViewHeight) return;

        _lastViewWidth = viewWidth;
        _lastViewHeight = viewHeight;
        EnsureSpectatorTexture(); // 크기가 같으면 내부에서 그대로 빠져나간다
    }

    private void CacheOriginalLayout()
    {
        _originalAnchorMin = _pipPanel.anchorMin;
        _originalAnchorMax = _pipPanel.anchorMax;
        _originalPivot = _pipPanel.pivot;
        _originalAnchoredPosition = _pipPanel.anchoredPosition;
        _originalSizeDelta = _pipPanel.sizeDelta;

        var rawImageRect = _pipRawImage.rectTransform;
        _originalRawImageAnchorMin = rawImageRect.anchorMin;
        _originalRawImageAnchorMax = rawImageRect.anchorMax;
        _originalRawImagePivot = rawImageRect.pivot;
        _originalRawImageAnchoredPosition = rawImageRect.anchoredPosition;
        _originalRawImageSizeDelta = rawImageRect.sizeDelta;
    }

    // ─────────────────────────────────────────
    // 미러 전투 시작/종료 — 카메라·RenderTexture는 최초 1회만 만들고 이후로는 재사용한다
    // (요구사항 5: 종료 때마다 파괴하지 않는다).
    // ─────────────────────────────────────────

    private void HandleMirrorStarted()
    {
        EnsureSpectatorCamera();
        PositionSpectatorCameraAtPartnerBoard();
        EnsureSpectatorTexture();
        UpdatePipContent();
    }

    private void HandleMirrorEnded()
    {
        // 미러 전투가 끝나도 관전 패널이 열려 있으면(_isExpanded) BoardSnapshot 프리뷰로 계속 보여줘야
        // 하므로, 여기서 카메라를 직접 끄지 않는다(UpdateCameraState가 판단) — 위치는 어차피 미러
        // 전투 중에도 파트너 보드 고정 구도라 다시 잡을 필요 없지만, 방어적으로 한 번 더 맞춘다.
        if (_isExpanded) PositionSpectatorCameraAtPartnerBoard();
        UpdatePipContent();
    }

    /// <summary>
    /// RawImage/"준비 중" 라벨 중 표시할 내용이 있는지에 맞는 쪽만 켠다(패널 자체의 열림 여부와는 별개).
    /// "내용이 있다" = 미러 전투 실행 중 이거나, 파트너 BoardSnapshot을 한 번이라도 수신했음(빈 필드
    /// 포함 — HasPartnerBoardSnapshot은 entries.Count가 아니라 수신 이력 기준이라 Units=0도 정상
    /// 내용으로 취급된다). 마지막에 카메라 표시 여부도 같은 갱신 지점에서 함께 판단한다(UpdateCameraState).
    /// </summary>
    private void UpdatePipContent()
    {
        bool running = _mirrorController != null && _mirrorController.IsRunning;
        bool hasBoardPreview = _mirrorController != null && _mirrorController.HasPartnerBoardSnapshot;
        bool hasContent = running || hasBoardPreview;

        _pipRawImage.gameObject.SetActive(hasContent);
        _pipLabel.gameObject.SetActive(!hasContent);
        if (!hasContent) _pipLabel.text = "준비 중";

        UpdateCameraState();
    }

    /// <summary>
    /// 관전 카메라 활성 여부를 판단하는 유일한 지점. HandleMirrorStarted/HandleMirrorEnded/ToggleExpanded는
    /// 전부 UpdatePipContent()를 거쳐 이 메서드를 호출하며 _spectatorCamera.enabled를 각자 직접 정하지
    /// 않는다. "패널을 띄워야 하는 상태인지"(shouldPresent)와 "보여줄 내용이 있는지"(hasContent)를
    /// 독립적으로 판단해 AND로 묶는다.
    /// shouldPresent에는 _isExpanded/_debugShowPip 외에 _mirrorController.IsRunning도 포함한다 —
    /// 그래야 관전 패널을 열지 않은 상태에서 QA로 미러 전투만 시작해도(기존 HandleMirrorStarted의
    /// 자동 카메라 활성화) 회귀 없이 카메라가 켜진다. 반대로 미러 전투가 끝나도 _isExpanded가 true고
    /// BoardSnapshot을 받은 적 있으면 hasContent가 유지돼 카메라를 끄지 않는다.
    /// </summary>
    private void UpdateCameraState()
    {
        if (_spectatorCamera == null || _mirrorController == null) return;

        bool shouldPresent = _isExpanded || _debugShowPip || _mirrorController.IsRunning;
        bool hasContent = _mirrorController.IsRunning || _mirrorController.HasPartnerBoardSnapshot;

        _spectatorCamera.enabled = shouldPresent && hasContent;
    }

    /// <summary>패널 자체를 보일지 말지. 확대 중이거나 디버그 PIP가 켜져 있을 때만 보인다.</summary>
    private void RefreshPanelVisibility()
    {
        _pipPanel.gameObject.SetActive(_isExpanded || _debugShowPip);
    }

    /// <summary>
    /// QA 패널의 "PIP 보기"/"PIP 숨기기" 버튼 전용 진입점. 디버그 PIP 표시 여부만 바꾸고
    /// 카메라·RenderTexture는 새로 만들거나 파괴하지 않는다(기존 것을 그대로 재사용) —
    /// 미러 전투가 실행 중이 아니면 켜도 "준비 중" 문구만 보인다.
    /// 일반 플레이에서는 이 메서드가 호출되지 않는 한 _debugShowPip 기본값(false)이 유지되므로
    /// PIP가 저절로 뜨는 일은 없다.
    /// </summary>
    public void SetDebugPipVisible(bool visible)
    {
        _debugShowPip = visible;
        RefreshPanelVisibility();
        UpdatePipContent();
    }

    // ─────────────────────────────────────────
    // 관전 카메라
    // ─────────────────────────────────────────

    /// <summary>
    /// 관전 전용 카메라를 최초 1회 생성한다. Main Camera는 절대 옮기지 않고, projection 관련 설정만
    /// 복사한다. 위치·회전은 PositionSpectatorCameraAtPartnerBoard가 매번 다시 잡는다.
    /// AudioListener는 붙이지 않는다.
    /// </summary>
    private void EnsureSpectatorCamera()
    {
        if (_spectatorCamera != null) return;

        // UI RectTransform 계층(스케일이 화면비/CanvasScaler에 좌우됨) 밑에 두면 카메라 변환이
        // 오염될 수 있어, 부모 없이 월드 루트에 독립 배치한다. 수명은 OnDestroy에서 직접 정리한다.
        var camGO = new GameObject("PartnerSpectatorCamera");
        _spectatorCamera = camGO.AddComponent<Camera>();
        _spectatorCamera.enabled = false;

        var mainCamera = Camera.main;
        if (mainCamera != null)
        {
            _spectatorCamera.orthographic = mainCamera.orthographic;
            _spectatorCamera.orthographicSize = mainCamera.orthographicSize;
            _spectatorCamera.fieldOfView = mainCamera.fieldOfView;
            _spectatorCamera.nearClipPlane = mainCamera.nearClipPlane;
            _spectatorCamera.farClipPlane = mainCamera.farClipPlane;
            _spectatorCamera.cullingMask = mainCamera.cullingMask;
            _spectatorCamera.clearFlags = mainCamera.clearFlags;
            _spectatorCamera.backgroundColor = mainCamera.backgroundColor;
            camGO.transform.rotation = mainCamera.transform.rotation;

            // Main Camera가 물리 카메라(센서 크기·초점거리·렌즈 시프트·게이트 핏)로 프로젝션을
            // 계산하면 fieldOfView 숫자만 같아도 실제 투영 결과가 미세하게 달라진다(실측 확인된
            // "관전 화면 전환 시 전체 월드가 살짝 움직이고 작아지는" 문제의 원인). usePhysicalProperties를
            // 먼저 켠 뒤 나머지 물리 카메라 값을 복사해야 Unity가 그 값들로 투영을 다시 계산한다.
            // projectionMatrix/worldToCameraMatrix는 직접 대입하지 않는다 — 여기서 위치·회전·물리
            // 속성만 똑같이 맞추면 Unity가 매 프레임 자동으로 계산한다.
            _spectatorCamera.usePhysicalProperties = mainCamera.usePhysicalProperties;
            _spectatorCamera.sensorSize = mainCamera.sensorSize;
            _spectatorCamera.lensShift = mainCamera.lensShift;
            _spectatorCamera.focalLength = mainCamera.focalLength;
            _spectatorCamera.gateFit = mainCamera.gateFit;
            _spectatorCamera.allowDynamicResolution = mainCamera.allowDynamicResolution;
            // rect는 복사하지 않는다. Main Camera는 CameraLetterbox 때문에 화면 가운데 16:9만
            // 그리지만, 관전 카메라는 이미 16:9로 만들어진 RenderTexture 전체에 그린다
            // (EnsureSpectatorTexture). 여기에 레터박스 rect까지 복사하면 RT 안에 검은 띠가 한 번 더
            // 구워져 이중 레터박스가 된다. 투영 일치는 rect가 아니라 "같은 종횡비"로 보장된다 —
            // RT를 16:9로 만들었으므로 관전 카메라의 aspect가 레터박스된 Main Camera와 같아진다.
            _spectatorCamera.rect = new Rect(0f, 0f, 1f, 1f);
            _spectatorCamera.targetDisplay = mainCamera.targetDisplay;
            // depth는 복사하지 않는다 — Spectator Camera는 항상 targetTexture(RenderTexture)로만
            // 그리고 Main Camera는 백버퍼에 직접 그려 같은 렌더 타깃을 공유하지 않으므로, 두 카메라의
            // depth(렌더 순서) 값이 서로 다를 이유가 없다(정렬 대상 자체가 겹치지 않음).

            // 씬에 설정된 Main Camera 마스크(Awake에서 이미 PartnerSpectateVisual이 빠진 상태)를
            // 기준으로 LocalGameplayVisual만 제외하고 PartnerSpectateVisual을 다시 포함시킨다.
            // Everything으로 새로 덮어쓰지 않는다. Layer가 준비되지 않았으면 방금 복사한 Main Camera
            // 마스크 그대로 둔다(기존 BoardOffset 방식과 함께 동작하는 안전한 폴백).
            if (_mirrorController != null &&
                _mirrorController.ArePartnerLayersReady(out int localLayer, out int partnerLayer))
            {
                _spectatorCamera.cullingMask &= ~(1 << localLayer);
                _spectatorCamera.cullingMask |= (1 << partnerLayer);
            }
        }
        else
        {
            Debug.LogWarning("[PartnerSpectateView] Camera.main을 찾지 못해 기본 설정으로 관전 카메라를 생성함");
        }
    }

    /// <summary>
    /// Main Camera의 CullingMask에서 PartnerSpectateVisual만 제외한다. 씬에 설정된 기존 마스크를
    /// 그대로 기준으로 삼고(Everything으로 새로 덮어쓰지 않음) 비트 하나만 뺀다. 두 관전 Layer 중
    /// 하나라도 Unity Editor에 없으면(ArePartnerLayersReady==false) 아무 것도 바꾸지 않는다 —
    /// CullingMask만 바뀌고 오프셋/타일 복제는 그대로인 부분 적용 상태를 막기 위함(요구사항).
    /// </summary>
    private void ApplyMainCameraCullingMask()
    {
        if (_mirrorController == null) return;
        if (!_mirrorController.ArePartnerLayersReady(out _, out int partnerLayer)) return;

        var mainCamera = Camera.main;
        if (mainCamera == null) return;

        mainCamera.cullingMask &= ~(1 << partnerLayer);
    }

    /// <summary>
    /// 관전 카메라를 "내 메인 카메라와 같은 상대 구도"로 파트너 보드에 옮긴다. 회전/FOV/
    /// orthographicSize는 EnsureSpectatorCamera가 이미 Main Camera에서 그대로 복사해 왔으므로
    /// 여기서는 위치만 다시 잡는다.
    ///
    /// offset은 PartnerBattleMirrorController.BoardOffset(OpponentBoardView.BoardOffset을 그대로
    /// 전달)에서 온다 — 두 관전 Layer가 모두 준비되면 그 프로퍼티가 Vector3.zero를 반환하므로
    /// 관전 카메라 위치가 Main Camera 위치와 완전히 동일해진다(같은 좌표에서 Layer/CullingMask로만
    /// 로컬/파트너를 가른다). Layer가 아직 없으면 기존 BoardOffset만큼 평행이동한 위치를 그대로
    /// 반환해 겹침 없이 동작한다(부분 적용 방지 폴백) — 이 메서드 자체는 그 값을 그대로 쓰기만
    /// 하므로 Layer 준비 여부를 따로 조건 분기하지 않는다.
    ///
    /// (이전에는 미러 전투/파트너 프리뷰 Renderer의 Bounds를 동적으로 계산해 카메라 거리를 매번 다시
    /// 잡는 방식이었으나, 아군+벤치+적을 모두 합친 Bounds가 넓어져 카메라가 지나치게 멀어지고 보드가
    /// 작게 보이는 문제가 실측으로 확인돼 이 고정 상대 포즈 방식으로 교체했다.)
    /// </summary>
    private void PositionSpectatorCameraAtPartnerBoard()
    {
        var mainCamera = Camera.main;
        if (mainCamera == null || _spectatorCamera == null) return;

        Vector3 offset = _mirrorController != null ? _mirrorController.BoardOffset : Vector3.zero;
        _spectatorCamera.transform.SetPositionAndRotation(
            mainCamera.transform.position + offset, mainCamera.transform.rotation);
    }

    /// <summary>
    /// Screen.width/height 기준으로 RenderTexture 해상도를 정한다(최종 플레이는 전체화면이 주
    /// 사용처라 숨겨진 PartnerPipRawImage의 작은 RectTransform 크기를 기준으로 삼지 않는다).
    /// [1280x720, 1920x1080] 박스 안에서 화면 비율을 유지해 맞춘다.
    /// </summary>
    /// <summary>
    /// RenderTexture 크기. 종횡비는 <b>화면이 아니라 Main Camera가 실제로 그리는 영역</b>을 따른다 —
    /// CameraLetterbox가 뷰포트를 16:9로 줄여 놓으면 화면이 16:10이어도 실제 그림은 16:9다.
    /// 화면 비율로 만들면 16:9 그림이 16:10 텍스처에 담겨 RawImage에서 늘어난다.
    /// 레터박스가 없는 구성에서는 pixelRect가 곧 화면 크기라 종전과 같은 값이 나온다.
    /// </summary>
    private static (int width, int height) ComputeDesiredRenderTextureSize()
    {
        float screenW = Mathf.Max(Screen.width, 1);
        float screenH = Mathf.Max(Screen.height, 1);

        Camera mainCamera = Camera.main;
        float viewW = mainCamera != null ? Mathf.Max(mainCamera.pixelWidth, 1) : screenW;
        float viewH = mainCamera != null ? Mathf.Max(mainCamera.pixelHeight, 1) : screenH;

        float aspect = viewW / viewH;

        float width = Mathf.Clamp(screenW, MIN_RENDER_TEXTURE_WIDTH, MAX_RENDER_TEXTURE_WIDTH);
        float height = width / aspect;

        if (height > MAX_RENDER_TEXTURE_HEIGHT) { height = MAX_RENDER_TEXTURE_HEIGHT; width = height * aspect; }
        if (height < MIN_RENDER_TEXTURE_HEIGHT) { height = MIN_RENDER_TEXTURE_HEIGHT; width = height * aspect; }
        width = Mathf.Clamp(width, MIN_RENDER_TEXTURE_WIDTH, MAX_RENDER_TEXTURE_WIDTH);

        return (Mathf.RoundToInt(width), Mathf.RoundToInt(height));
    }

    /// <summary>
    /// 원하는 해상도가 현재 RenderTexture와 다를 때만 Release/Destroy 후 재생성한다. 같으면 아무 일도
    /// 하지 않는다 — 매 프레임 호출돼도(호출 지점 자체는 프레임마다 부르지 않지만) 안전하다.
    /// 디버그 PIP와 전체화면이 같은 텍스처를 공유하므로 PIP는 이 고해상도 텍스처가 축소돼 보인다.
    /// </summary>
    private void EnsureSpectatorTexture()
    {
        (int width, int height) = ComputeDesiredRenderTextureSize();

        if (_spectatorTexture != null && _spectatorTexture.width == width && _spectatorTexture.height == height)
            return;

        if (_spectatorTexture != null)
        {
            if (_spectatorCamera != null) _spectatorCamera.targetTexture = null;
            _spectatorTexture.Release();
            Destroy(_spectatorTexture);
        }

        _spectatorTexture = new RenderTexture(width, height, 24, RenderTextureFormat.Default) { name = "PartnerSpectatorRT" };
        if (_spectatorCamera != null) _spectatorCamera.targetTexture = _spectatorTexture;
        _pipRawImage.texture = _spectatorTexture;
    }

    // ─────────────────────────────────────────
    // PIP 확대(전체화면)/복귀
    // ─────────────────────────────────────────

    /// <summary>PartnerViewButton 클릭 핸들러 — 관전 열기/닫기는 이 경로 하나뿐이다.</summary>
    private void ToggleExpanded() => SetExpanded(!_isExpanded);

    /// <summary>내 프로필 버튼 — 항상 내 화면으로. 이미 내 화면이면 SetExpanded가 그대로 빠져나간다.</summary>
    private void ShowMyView() => SetExpanded(false);

    /// <summary>파트너 프로필 버튼 — 항상 파트너 화면으로. 이미 열려 있으면 아무 일도 없다.</summary>
    private void ShowPartnerView() => SetExpanded(true);

    /// <summary>
    /// 프로필 색을 칠한다. 두 가지 정보를 한 번에 담는다 —
    ///   <b>색상</b>   누구인지: 방장=보라(_hostColor), 참가자=파랑(_guestColor)
    ///   <b>진하기</b> 지금 어느 화면을 보고 있는지: 보고 있는 쪽은 그대로, 아닌 쪽은 _inactiveDim만큼 흐리게
    ///
    /// 두 신호를 한 메서드에서 합치는 이유 — _myViewFrame/_partnerViewFrame은 프로필 버튼의
    /// Profile_Image 그 자체다. 다른 컴포넌트가 같은 Image의 color를 따로 칠하면 서로 덮어써서
    /// 어느 쪽이 이겼는지에 따라 색이 깜빡인다. 색을 쓰는 곳은 여기 하나로 유지한다.
    /// </summary>
    private void UpdateViewFrames()
    {
        ProfileRole localRole = ResolveLocalRole();

        // 파트너가 실제로 들어와 있을 때만 색이 정해진다. 아직 안 들어왔거나 도중에 나갔으면
        // 회색이다 — 없는 사람 자리에 파랑/보라를 칠하면 있는 것처럼 보인다.
        // 2인 협동이라 파트너가 있으면 내 역할의 반대가 곧 파트너 역할이다.
        bool hasPartner = HasPartner();

        ProfileRole partnerRole = hasPartner
            ? localRole switch
              {
                  ProfileRole.Host  => ProfileRole.Guest,
                  ProfileRole.Guest => ProfileRole.Host,
                  _                 => ProfileRole.Offline
              }
            : ProfileRole.Offline;

        Color myColor      = ColorFor(localRole);
        Color partnerColor = ColorFor(partnerRole);

        // 흐리기는 파트너 유무와 무관하게 "지금 보고 있지 않은 쪽"에 항상 들어간다.
        // (혼자 있을 때 파트너 프로필이 사라져 보이던 문제는 Dim이 알파를 낮추던 것이 원인이었고,
        //  그건 Dim에서 알파를 빼는 것으로 해결됐다 — 여기서 Dim 자체를 건너뛸 이유는 없다.)

        // _isExpanded == 파트너 화면을 보는 중.
        if (_myViewFrame != null)
            _myViewFrame.color = _isExpanded ? Dim(myColor) : myColor;

        if (_partnerViewFrame != null)
            _partnerViewFrame.color = _isExpanded ? partnerColor : Dim(partnerColor);
    }

    /// <summary>
    /// 방에 파트너가 실제로 들어와 있는지. 2인 협동이라 PlayerCount가 2 이상이면 파트너가 있다.
    /// 네트워크가 없거나 솔로모드면 PlayerCount가 1이라 자연스럽게 false가 된다.
    /// </summary>
    private static bool HasPartner()
        => GameManager.TryGet(out var gm) && gm.Network != null && gm.Network.PlayerCount >= 2;

    /// <summary>
    /// 보고 있지 않은 쪽 표현. <b>밝기만</b> 낮추고 알파는 원본 그대로 둔다.
    ///
    /// 알파를 같이 낮추면 프로필이 반투명해져 뒤가 비치는데, 그러면 "흐린 회색"이 아니라
    /// "색이 빠진 것"처럼 보인다. 특히 혼자 있을 때(파트너 슬롯이 회색 + 비활성) 두 효과가 겹쳐
    /// 이미지가 거의 사라진 것처럼 보였다. 색이 무엇인지는 언제나 또렷해야 하므로 불투명을 유지한다.
    /// </summary>
    private Color Dim(Color color)
        => new(color.r * _inactiveDim, color.g * _inactiveDim, color.b * _inactiveDim, color.a);

    /// <summary>프로필 색이 나타내는 역할. Offline은 "아직 방에 안 들어가 정해지지 않음"이다.</summary>
    private enum ProfileRole { Offline, Host, Guest }

    private Color ColorFor(ProfileRole role) => role switch
    {
        ProfileRole.Host  => _hostColor,
        ProfileRole.Guest => _guestColor,
        _                 => _offlineColor
    };

    /// <summary>
    /// 이 클라이언트의 역할. <b>방에 들어가 있을 때만</b> 방장/참가자가 정해진다 —
    /// 로비·솔로모드·오프라인 스텁은 전부 Offline(회색)이다.
    ///
    /// IsMasterClient만 보면 안 되는 이유: 방 밖에서도 true를 돌려주는 경로가 있어
    /// (오프라인 스텁은 항상 true, 솔로모드도 true) 접속 전부터 방장 색이 칠해진다.
    /// IsInRoom을 먼저 확인해야 "아직 모른다"와 "내가 방장이다"가 구분된다.
    /// </summary>
    private static ProfileRole ResolveLocalRole()
    {
        if (!GameManager.TryGet(out var gm) || gm.Network == null) return ProfileRole.Offline;
        if (!gm.Network.IsInRoom) return ProfileRole.Offline;

        return gm.Network.IsMasterClient ? ProfileRole.Host : ProfileRole.Guest;
    }

    /// <summary>
    /// 전체화면 관전 상태를 바꾸는 단일 진입점. _isExpanded 대입과
    /// GameEvents.OnPartnerSpectateExpandedChanged 발행이 이 메서드 한 곳에만 있다 — 버튼 토글
    /// (ToggleExpanded), 방어적 종료(OnDisable), 파트너 이탈 자동 종료(HandleOpponentDisconnected)가
    /// 전부 이 메서드를 거치므로 상태 변경과 이벤트 발행이 어긋날 수 없다.
    /// 이미 같은 상태면 아무 것도 하지 않는다 — 여러 경로가 동시에 "닫아라"를 요청해도
    /// (예: 버튼으로 이미 닫은 뒤 파트너 이탈까지 겹쳐도) 이벤트가 중복 발행되지 않는다.
    /// </summary>
    private void SetExpanded(bool expanded)
    {
        if (_isExpanded == expanded) return;
        _isExpanded = expanded;

        if (_isExpanded)
        {
            // 미러가 아직 안 돌고 있어도 버튼만으로 열 수 있으니 방어적으로 보장.
            EnsureSpectatorCamera();
            EnsureSpectatorTexture();
            RefreshPartnerEnemyPreview();
            TryStartMirrorBattleFromCache();
            PositionSpectatorCameraAtPartnerBoard();
            ExpandPip();
            RequestPartnerBoardResync();
        }
        else
        {
            // 전체 정리(ClearPartnerEnemyPreview)를 쓰면 안 된다 — 미러 전투가 아직 배경에서 실행
            // 중일 수 있는데(패널을 닫아도 전투 자체는 멈추지 않음), 그 상태에서 타일까지 지우면
            // 나중에 다시 열었을 때 ShowEnemyPreview가 "_units.Count > 0"(전투 진행 중) 가드에 막혀
            // 타일을 다시 만들지 못해 적 유닛이 바닥 없이 떠 있게 된다. 유닛만 정리한다.
            if (_mirrorController != null) _mirrorController.ClearPartnerEnemyPreviewUnitsOnly();
            RestorePip();
        }

        RefreshPanelVisibility();
        UpdatePipContent();
        UpdateViewButtonLabel();
        UpdateViewFrames();

        // PIP(축소) 상태는 포함하지 않는다 — 전체화면 진입/종료만 알린다(UIManager의 상점 HUD 숨김/복원용).
        GameEvents.PartnerSpectateExpandedChanged(_isExpanded);
    }

    /// <summary>
    /// 관전을 열 때마다 파트너에게 현재 BoardSnapshot을 다시 보내달라고 요청한다(pull). 게임 최초
    /// 진입 시의 자동 push(BoardSyncBroadcaster)가 어떤 이유로든 유실·지연되더라도 관전을 여는
    /// 시점에 최신 상태를 한 번 더 확보하기 위함(2026-08 파트너 화면 초기 동기화 문제 대응).
    /// 이미 받은 스냅샷이 있어도 매번 다시 요청한다 — 요청 자체는 최신 상태 확보용일 뿐이고,
    /// 응답이 올 때까지 기존 화면(OpponentBoardView._lastSnapshot)은 그대로 유지된다(요청을 보낸다고
    /// 지금 보이는 파트너 보드를 지우지 않는다 — 새 스냅샷이 도착했을 때만 Render가 다시 그린다).
    /// </summary>
    private void RequestPartnerBoardResync()
    {
        if (GameManager.TryGet(out var gm) && gm.Network != null)
            gm.Network.RequestPartnerBoardSnapshot();
    }

    /// <summary>
    /// 현재 라운드 CurrentStage로 파트너 관전용 적 프리뷰를 (다시) 그린다. GameManager는 핵심 매니저
    /// 접근 규칙대로 TryGet으로만 조회한다(Singleton.Instance null 검사 금지).
    /// </summary>
    private void RefreshPartnerEnemyPreview()
    {
        if (_mirrorController == null) return;
        if (!GameManager.TryGet(out var gm) || gm.Phase == null) return;
        _mirrorController.ShowPartnerEnemyPreview(gm.Phase.CurrentStage);
    }

    /// <summary>
    /// 관전을 여는 시점에 NetworkManager에 캐시된 파트너 BattleSnapshot이 "현재 라운드" 것이면
    /// 그 스냅샷으로 즉시 미러 전투를 시작한다. 이 경로가 없으면 전투가 시작된 뒤에 관전을 연
    /// 사용자는 다음 라운드 스냅샷이 올 때까지 아무것도 못 본다.
    ///
    /// 판단 기준은 내 로컬 GamePhase.Battle 여부가 아니라 스냅샷의 roundIndex다 — 내 전투가
    /// 먼저 끝나 Result로 넘어가도, RoundPhaseManager는 양쪽 팀 결과(OnTeamRoundResolved)가 모일
    /// 때까지 다음 라운드로 넘어가지 않으므로(RoundPhaseManager.cs 참고) 그 사이엔 파트너가 아직
    /// 이 스냅샷 그대로의 전투를 진행 중일 수 있다. 내 GamePhase로 막으면 이 구간에서 미러 전투가
    /// 아예 시작되지 않는 문제가 있었다(2026-08 확인). 반대로 라운드가 이미 바뀌었으면(내가 다음
    /// 라운드 쇼핑에 진입) 캐시된 스냅샷은 이전 라운드 것이므로 재생하지 않는다(stale 재생 방지).
    /// 이미 미러 전투가 실행 중이면(예: 방금 HandlePartnerBattleSnapshotChanged로 시작됐거나 QA로
    /// 이미 돌고 있는 경우) 중복 시작하지 않는다.
    ///
    /// 단, 내 매치가 이미 완전히 끝난 상태(Victory/GameOver)라면 roundIndex가 우연히 일치해도 막는다
    /// (2026-08 코드리뷰 대응) — CurrentRound는 종료 후에도 바뀌지 않고 _partnerBattleSnapshot도
    /// 클리어되지 않으므로, 이 가드가 없으면 게임이 끝난 뒤 PartnerViewButton으로 마지막 라운드
    /// 전투가 계속 재생 가능했다. GamePhase.Battle만 허용하는 화이트리스트가 아니라 종료 상태
    /// 두 개만 거르는 blacklist다 — "내 Phase가 Shopping/Battle이고 파트너가 아직 전투 중"인
    /// 정상 케이스(위 문단)는 그대로 통과해야 하기 때문이다.
    ///
    /// 이번 라운드 팀 결과가 이미 확정됐으면(_teamRoundResolved) roundIndex가 CurrentRound와 같아도
    /// 시작하지 않는다 — 양쪽 실제 전투가 이미 끝난 라운드의 캐시 스냅샷으로 끝난 MirrorBattle을
    /// 다시 재생하는 것을 막기 위함(HandleTeamRoundResolved가 이미 한 번 정지시킨 라운드).
    /// 패널 자체는 그대로 열리며(SetExpanded는 이 메서드 호출과 별개로 계속 진행), 남아 있는
    /// BoardSnapshot 프리뷰가 있으면 UpdatePipContent가 그걸 대신 보여준다.
    /// </summary>
    private void TryStartMirrorBattleFromCache()
    {
        if (_mirrorController == null || _mirrorController.IsRunning) return;
        if (_teamRoundResolved) return;
        if (!GameManager.TryGet(out var gm) || gm.Phase == null || gm.Network == null) return;
        if (IsMatchEnded(gm.Phase.CurrentPhase)) return;
        if (!gm.Network.HasPartnerBattleSnapshot) return;
        if (gm.Network.PartnerBattleSnapshot.roundIndex != gm.Phase.CurrentRound) return;

        StartOrReplaceMirrorBattle(gm.Network.PartnerBattleSnapshot);
    }

    /// <summary>매치가 완전히 끝난 상태인지 — TryStartMirrorBattleFromCache/HandlePartnerBattleSnapshotChanged
    /// 공용 blacklist 가드(2026-08 코드리뷰 대응). Shopping/Battle 등 진행 중 페이즈는 전부 허용해야
    /// 하므로 화이트리스트가 아니라 종료 상태 두 개만 명시적으로 거른다.</summary>
    private static bool IsMatchEnded(GamePhase phase) =>
        phase == GamePhase.Victory || phase == GamePhase.GameOver;

    /// <summary>
    /// 최신 파트너 BattleSnapshot으로 미러 전투를 (다시) 시작한다. 쇼핑 적 프리뷰 중 "유닛"만 정리하고
    /// 빨간 육각 타일은 남겨둔다(ClearPartnerEnemyPreviewUnitsOnly) — 그래야 미러 전투 적이 그 타일
    /// 위에서 싸우는 것처럼 보인다. 전체 정리(ClearPartnerEnemyPreview, 타일까지 지움)는 쇼핑 단계로
    /// 완전히 돌아갈 때(ShowEnemyPreview가 선행 호출)만 쓴다. 이미 다른 미러 전투가 돌고 있으면
    /// PartnerBattleMirrorController.StopMirrorBattle로 먼저 멈춘 뒤 새 스냅샷으로 시작한다
    /// (StopMirrorBattle은 실행 중이 아니면 아무 일도 하지 않아 중복 호출에 안전하다). 종료/실패
    /// 콜백에서는 그 시점에도 관전이 열려 있을 때만(_isExpanded) 쇼핑 적 프리뷰로 되돌린다 — 관전을
    /// 닫고 내 화면으로 돌아간 뒤에 뒤늦게 콜백이 와서 프리뷰를 다시 만드는 것을 막기 위함이다.
    /// </summary>
    private void StartOrReplaceMirrorBattle(BattleSnapshot snapshot)
    {
        if (_mirrorController == null || snapshot == null) return;

        _mirrorController.ClearPartnerEnemyPreviewUnitsOnly();
        _mirrorController.StopMirrorBattle();

        _mirrorController.StartMirrorBattle(
            snapshot,
            result => { if (_isExpanded) RefreshPartnerEnemyPreview(); },
            error => { if (_isExpanded) RefreshPartnerEnemyPreview(); });
    }

    /// <summary>PartnerViewButton 하위 TMP 텍스트를 관전 상태에 맞게 갱신한다. 새 Inspector 참조를
    /// 추가하지 않고 Awake에서 GetComponentInChildren로 1회 캐시해둔 것만 쓴다.</summary>
    private void UpdateViewButtonLabel()
    {
        if (_externalViewButtonLabel == null) return;
        _externalViewButtonLabel.text = _isExpanded ? "내 화면 보기" : "파트너 화면 보기";
    }

    /// <summary>Canvas 전체를 정확히 채운다(고정 크기 팝업이 아니라 실제 전체 화면).</summary>
    private void ExpandPip()
    {
        _pipPanel.anchorMin = Vector2.zero;
        _pipPanel.anchorMax = Vector2.one;
        _pipPanel.pivot = new Vector2(0.5f, 0.5f);
        _pipPanel.offsetMin = Vector2.zero;
        _pipPanel.offsetMax = Vector2.zero;

        // PartnerPipRawImage 자체의 PIP 테두리 인셋도 전체화면 동안만 제거해 부모(PartnerPipPanel)를
        // 정확히 꽉 채운다. localScale은 건드리지 않는다(기존 값 유지).
        var rawImageRect = _pipRawImage.rectTransform;
        rawImageRect.anchorMin = Vector2.zero;
        rawImageRect.anchorMax = Vector2.one;
        rawImageRect.offsetMin = Vector2.zero;
        rawImageRect.offsetMax = Vector2.zero;
    }

    private void RestorePip()
    {
        _pipPanel.anchorMin = _originalAnchorMin;
        _pipPanel.anchorMax = _originalAnchorMax;
        _pipPanel.pivot = _originalPivot;
        _pipPanel.anchoredPosition = _originalAnchoredPosition;
        _pipPanel.sizeDelta = _originalSizeDelta;

        // ExpandPip에서 지웠던 PartnerPipRawImage의 PIP 테두리 인셋을 CacheOriginalLayout이 실행 중
        // 읽어둔 실제 값 그대로 복원한다(상수 하드코딩 없음 — Inspector 원본이 바뀌어도 자동 반영).
        var rawImageRect = _pipRawImage.rectTransform;
        rawImageRect.anchorMin = _originalRawImageAnchorMin;
        rawImageRect.anchorMax = _originalRawImageAnchorMax;
        rawImageRect.pivot = _originalRawImagePivot;
        rawImageRect.anchoredPosition = _originalRawImageAnchoredPosition;
        rawImageRect.sizeDelta = _originalRawImageSizeDelta;
    }
}
