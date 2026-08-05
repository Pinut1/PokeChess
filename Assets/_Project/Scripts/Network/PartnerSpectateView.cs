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

    /// <summary>바운딩 스피어를 프레임에 딱 맞추면 가장자리가 잘려 보이니 살짝 여유를 둔다.</summary>
    private const float FRAME_MARGIN = 1.15f;

    [Header("PIP 대상 (Inspector에서 직접 연결)")]
    [Tooltip("PartnerPipPanel의 RectTransform. 확대/복귀 시 이 RectTransform을 직접 조작한다.")]
    [SerializeField] private RectTransform _pipPanel;

    [Tooltip("PartnerPipRawImage — 관전 카메라의 RenderTexture를 표시할 RawImage.")]
    [SerializeField] private RawImage _pipRawImage;

    [Tooltip("PartnerPipLabel — 미러 전투가 없을 때 \"준비 중\" 문구를 표시할 텍스트.")]
    [SerializeField] private TextMeshProUGUI _pipLabel;

    [Tooltip("PartnerViewButton — 일반 플레이에서 파트너 전투 전체화면을 여닫는 버튼. 항상 활성 상태로 둔다.")]
    [SerializeField] private Button _externalViewButton;

    [Header("디버그")]
    [Tooltip("켜면 평상시에도 우측 상단에 작은 PIP 미리보기를 띄운다. 끄면(기본) 최종 플레이 화면처럼 " +
             "PartnerViewButton으로 전체화면을 열 때만 파트너 화면이 보인다. QA/포트폴리오 확인용으로 유지.")]
    [SerializeField] private bool _debugShowPip = false;

    private Camera _spectatorCamera;
    private RenderTexture _spectatorTexture;
    private PartnerBattleMirrorController _mirrorController;

    private bool _wasMirrorRunning;
    private bool _isExpanded;

    private Vector2 _originalAnchorMin;
    private Vector2 _originalAnchorMax;
    private Vector2 _originalPivot;
    private Vector2 _originalAnchoredPosition;
    private Vector2 _originalSizeDelta;

    private void Awake()
    {
        if (_pipPanel == null || _pipRawImage == null || _pipLabel == null)
        {
            Debug.LogError("[PartnerSpectateView] Inspector 연결 누락 — _pipPanel/_pipRawImage/_pipLabel을 모두 연결해야 한다");
            enabled = false;
            return;
        }

        CacheOriginalLayout();
        _mirrorController = PartnerBattleMirrorController.GetOrCreate();

        UpdatePipContent();
        RefreshPanelVisibility();
    }

    private void OnEnable()
    {
        var panelButton = _pipPanel != null ? _pipPanel.GetComponent<Button>() : null;
        if (panelButton != null) panelButton.onClick.AddListener(ToggleExpanded);
        if (_externalViewButton != null) _externalViewButton.onClick.AddListener(ToggleExpanded);
    }

    private void OnDisable()
    {
        var panelButton = _pipPanel != null ? _pipPanel.GetComponent<Button>() : null;
        if (panelButton != null) panelButton.onClick.RemoveListener(ToggleExpanded);
        if (_externalViewButton != null) _externalViewButton.onClick.RemoveListener(ToggleExpanded);
    }

    private void OnDestroy()
    {
        if (_spectatorCamera != null) Destroy(_spectatorCamera.gameObject);
        if (_spectatorTexture != null)
        {
            _spectatorTexture.Release();
            Destroy(_spectatorTexture);
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
    }

    // ─────────────────────────────────────────
    // 미러 전투 시작/종료 — 카메라·RenderTexture는 최초 1회만 만들고 이후로는 재사용한다
    // (요구사항 5: 종료 때마다 파괴하지 않는다).
    // ─────────────────────────────────────────

    private void HandleMirrorStarted()
    {
        EnsureSpectatorCamera();
        FrameCameraOnMirrorBounds();
        EnsureSpectatorTexture();
        _spectatorCamera.enabled = true;
        UpdatePipContent();
    }

    private void HandleMirrorEnded()
    {
        if (_spectatorCamera != null) _spectatorCamera.enabled = false;
        UpdatePipContent();
    }

    /// <summary>RawImage/"준비 중" 라벨 중 미러 실행 여부에 맞는 쪽만 켠다. 패널 자체의 표시 여부와는 별개.</summary>
    private void UpdatePipContent()
    {
        bool running = _mirrorController != null && _mirrorController.IsRunning;
        _pipRawImage.gameObject.SetActive(running);
        _pipLabel.gameObject.SetActive(!running);
        if (!running) _pipLabel.text = "준비 중";
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
    /// 복사한다. 위치·회전은 FrameCameraOnMirrorBounds가 매 미러 시작마다 다시 잡는다.
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
        }
        else
        {
            Debug.LogWarning("[PartnerSpectateView] Camera.main을 찾지 못해 기본 설정으로 관전 카메라를 생성함");
        }
    }

    /// <summary>
    /// 미러 전장에 실제로 그려진 visual들의 월드 바운즈를 구해 그 중심을 프레임 정중앙에 오도록
    /// 카메라를 옮긴다. 회전은 Main Camera 것을 그대로 유지한다 — 실전투와 같은 시야각을 쓰므로
    /// "아군은 카메라에 가까운 쪽(아래), 적은 먼 쪽(위)"이라는 기존 구도가 그대로 보장된다.
    /// 논리 좌표(BattleUnit.coords)나 visual 위치 계산은 전혀 건드리지 않고, 카메라 위치·거리만 조정한다.
    /// </summary>
    private void FrameCameraOnMirrorBounds()
    {
        var mainCamera = Camera.main;
        if (mainCamera == null || _spectatorCamera == null) return;

        Quaternion rotation = mainCamera.transform.rotation;
        Vector3 forward = rotation * Vector3.forward;

        Bounds? bounds = ComputeMirrorVisualBounds();
        if (bounds.HasValue)
        {
            Bounds b = bounds.Value;
            float radius = Mathf.Max(b.extents.magnitude, 0.5f);

            float vFovRad = Mathf.Max(mainCamera.fieldOfView, 1f) * Mathf.Deg2Rad;
            float aspect = mainCamera.aspect > 0.01f ? mainCamera.aspect : 16f / 9f;
            float hFovRad = 2f * Mathf.Atan(Mathf.Tan(vFovRad * 0.5f) * aspect);

            float distance = Mathf.Max(
                radius / Mathf.Sin(vFovRad * 0.5f),
                radius / Mathf.Sin(hFovRad * 0.5f)) * FRAME_MARGIN;

            _spectatorCamera.transform.SetPositionAndRotation(b.center - forward * distance, rotation);
        }
        else
        {
            // 미러 visual을 하나도 못 찾았을 때만 쓰는 폴백 — 평행이동 근사(예전 방식)로라도 화면을 채운다.
            Vector3 offset = _mirrorController != null ? _mirrorController.BoardOffset : Vector3.zero;
            _spectatorCamera.transform.SetPositionAndRotation(mainCamera.transform.position + offset, rotation);
            Debug.LogWarning("[PartnerSpectateView] 미러 visual 바운즈를 계산하지 못해 폴백 평행이동 위치를 사용함");
        }
    }

    /// <summary>
    /// PartnerBattleMirrorController.MirrorVisualRoot(=미러 BattleManager 자신의 Transform) 아래
    /// 실제로 매달린 유닛 visual들의 Renderer 바운즈를 전부 합친다. 죽어서 비활성화된 visual은
    /// GetComponentsInChildren(false)라 자동으로 제외된다.
    /// </summary>
    private Bounds? ComputeMirrorVisualBounds()
    {
        Transform root = _mirrorController != null ? _mirrorController.MirrorVisualRoot : null;
        if (root == null) return null;

        var renderers = root.GetComponentsInChildren<Renderer>(false);
        if (renderers.Length == 0) return null;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
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

    /// <summary>PIP 클릭(디버그 PIP 켜져 있을 때)/PartnerViewButton 클릭 공용 핸들러.</summary>
    private void ToggleExpanded()
    {
        _isExpanded = !_isExpanded;

        if (_isExpanded)
        {
            // 미러가 아직 안 돌고 있어도 버튼만으로 열 수 있으니 방어적으로 보장.
            EnsureSpectatorCamera();
            EnsureSpectatorTexture();
            ExpandPip();
        }
        else
        {
            RestorePip();
        }

        RefreshPanelVisibility();
        UpdatePipContent();
    }

    /// <summary>Canvas 전체를 정확히 채운다(고정 크기 팝업이 아니라 실제 전체 화면).</summary>
    private void ExpandPip()
    {
        _pipPanel.anchorMin = Vector2.zero;
        _pipPanel.anchorMax = Vector2.one;
        _pipPanel.pivot = new Vector2(0.5f, 0.5f);
        _pipPanel.offsetMin = Vector2.zero;
        _pipPanel.offsetMax = Vector2.zero;
    }

    private void RestorePip()
    {
        _pipPanel.anchorMin = _originalAnchorMin;
        _pipPanel.anchorMax = _originalAnchorMax;
        _pipPanel.pivot = _originalPivot;
        _pipPanel.anchoredPosition = _originalAnchoredPosition;
        _pipPanel.sizeDelta = _originalSizeDelta;
    }
}
