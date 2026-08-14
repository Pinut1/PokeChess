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

    [Tooltip("PartnerViewButton — 일반 플레이에서 파트너 전투 전체화면을 여닫는 버튼. 항상 활성 상태로 둔다. " +
             "관전 열기/닫기는 이 버튼 하나로만 한다 — 전체화면 패널 자체를 클릭해도 닫히지 않는다.")]
    [SerializeField] private Button _externalViewButton;

    [Header("디버그")]
    [Tooltip("켜면 평상시에도 우측 상단에 작은 PIP 미리보기를 띄운다. 끄면(기본) 최종 플레이 화면처럼 " +
             "PartnerViewButton으로 전체화면을 열 때만 파트너 화면이 보인다. QA/포트폴리오 확인용으로 유지.")]
    [SerializeField] private bool _debugShowPip = false;

    private Camera _spectatorCamera;
    private RenderTexture _spectatorTexture;
    private PartnerBattleMirrorController _mirrorController;
    private TextMeshProUGUI _externalViewButtonLabel;

    private bool _wasMirrorRunning;
    private bool _isExpanded;

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

        GameEvents.OnStageEntered += HandleStageEntered;
        GameEvents.OnPartnerBattleSnapshotChanged += HandlePartnerBattleSnapshotChanged;
        GameEvents.OnOpponentDisconnected += HandleOpponentDisconnected;
    }

    private void OnDisable()
    {
        if (_externalViewButton != null) _externalViewButton.onClick.RemoveListener(ToggleExpanded);

        GameEvents.OnStageEntered -= HandleStageEntered;
        GameEvents.OnPartnerBattleSnapshotChanged -= HandlePartnerBattleSnapshotChanged;
        GameEvents.OnOpponentDisconnected -= HandleOpponentDisconnected;

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
    /// 관전 패널이 열려 있는 동안 라운드/스테이지가 바뀌면 파트너 적 프리뷰를 최신 스테이지로 다시
    /// 그린다. 닫혀 있으면 무시한다 — 다음에 열 때(ToggleExpanded) 항상 그 시점의 CurrentStage로
    /// 새로 그리므로 놓치지 않는다.
    /// </summary>
    private void HandleStageEntered(StageData stage)
    {
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
            _spectatorCamera.rect = mainCamera.rect;
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
    private static (int width, int height) ComputeDesiredRenderTextureSize()
    {
        float screenW = Mathf.Max(Screen.width, 1);
        float screenH = Mathf.Max(Screen.height, 1);
        float aspect = screenW / screenH;

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
    /// </summary>
    private void TryStartMirrorBattleFromCache()
    {
        if (_mirrorController == null || _mirrorController.IsRunning) return;
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
