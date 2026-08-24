using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 3D 오브젝트를 클릭해 증강 확인창(AugmentInfoPanel)을 여닫는다.
/// 캡슐 등 아무 Collider나 붙은 오브젝트에 달면 된다 — SellZone의 수령 클릭과 같은 방식이다.
///
/// ⚠️ Collider는 <b>이 스크립트와 같은 GameObject</b>에 있어야 한다.
/// OnMouseDown은 콜라이더가 붙은 오브젝트에만 전달되며 부모로 올라가지 않는다.
///
/// 파트너 전체화면 관전 중에는 OnMouseDown이 호출되지 않는다 — PartnerSpectateView의 풀스크린
/// 배경 Image가 화면 전체 레이캐스트를 흡수해 EventSystem.IsPointerOverGameObject()가 항상 true가
/// 되기 때문(PartnerSpectateView.cs 주석 참고). 그래서 관전 중에는 Update()에서 New Input System으로
/// 좌클릭을 직접 폴링해, 이 오브젝트(씬에 단 하나뿐인 실제 증강 오브젝트)의 <b>콜라이더 중심</b>이
/// 관전 카메라 화면에 투영되는 위치에서 반경 안을 클릭했는지 판정한다.
/// 이 경로는 파트너가 실제로 선택한 증강(GameEvents.OnPartnerAugmentsChanged로 캐시)만 표시하며
/// 로컬 AugmentManager 상태는 전혀 건드리지 않는다.
///
/// 🚨 <b>이 판정에서 두 가지를 되돌리지 말 것</b>(2026-08-24, 실측으로 잡은 버그).
/// 원래는 "transform.position을 투영한 점 + 고정 60px"이었고, 증상은 <b>관전 중 증강창이 열렸다
/// 안 열렸다 하는 것</b>이었다. 원인이 둘 겹쳐 있었다.
///
/// ① <b>기준점은 transform.position이 아니라 _collider.bounds.center다.</b> 포켓스톱의 피벗은
///    발밑(y≈0.02)인데 실제로 보이는 몸통은 캡슐 height 2짜리라, 화면에서는 피벗과 몸통이 한참
///    떨어져 있다(카메라 pitch 55° 기준 캡슐 꼭대기는 1080p에서 피벗으로부터 약 79px). 그래서
///    <b>아래쪽을 찍으면 열리고 위쪽을 찍으면 조용히 무시</b>됐다. 콜라이더 중심을 쓰면 필요한
///    반경이 79px → 약 50px로 줄고, 판정원이 오브젝트를 대칭으로 감싼다(지면 아래로 새지 않는다).
///
/// ② <b>반경은 렌더 높이에 비례해야 한다</b>(ScaledClickRadius). 투영 결과도 커서 위치도 실제
///    화면 픽셀이라, 같은 월드 거리가 해상도가 올라갈수록 더 많은 픽셀이 된다. 고정 60px이면
///    720p에선 넉넉하고 1440p 이상에선 몸통조차 못 잡는다(실측: 몸통 클릭 57px / 허용 60px —
///    3px 남기고 걸쳐 있었다). 옵션창 해상도 목록만 보고 값을 정하면 안 된다. Editor Game 뷰나
///    창모드 드래그로 목록에 없는 크기가 얼마든지 나온다.
///
/// StatInfoController.TryHandlePartnerOpen은 아직 유닛 피벗 + 고정 _battlePickRadius 방식이라
/// 같은 함정을 그대로 안고 있다(유닛은 키가 작아 아직 안 터졌을 뿐).
///
/// ⚠️ 이 좌표 비교 방식은 "그 위치에 실제로 뭐가 떠 있는지"는 보지 않는다 — 반경 안에만 들어오면
/// 무조건 클릭으로 인정하므로, 그 근처에 다른 UI(설정창 버튼 등)가 우연히 겹쳐 있으면 그걸 눌러도
/// 증강창까지 같이 열렸다(2026-08 재리뷰 지적). PointerUtil.IsOverUI()는 여기서 못 쓴다 — 관전 중엔
/// 배경 Image 자체가 항상 "UI 위"로 잡히므로 그걸 그대로 쓰면 증강창이 아예 안 열리게 된다. 대신
/// PointerUtil.IsBlockedByOtherUI로 그 지점의 맨 위 UI가 배경 Image(PartnerSpectateView.PipRawImage)
/// 소속(자신 또는 그 자식 — 파트너 HP/마나 바도 배경의 자식이라 여기 포함됨)인지를 확인해, 배경
/// 소속이 아닌 다른 UI가 위에 있을 때만 막는다. StatInfoController.TryHandlePartnerOpen도 같은
/// 헬퍼를 쓴다(2026-08 재재재리뷰 지적으로 공용 유틸로 옮김).
/// </summary>
[RequireComponent(typeof(Collider))]
public class AugmentInfoTrigger : MonoBehaviour
{
    [Tooltip("열고 닫을 확인창. Canvas에 붙여둔 AugmentInfoPanel을 넣는다.")]
    [SerializeField] private AugmentInfoPanel _panel;

    [Tooltip("끄면 클릭할 때마다 열기만 하고 닫히지 않는다(닫기 버튼 전용으로 쓸 때).")]
    [SerializeField] private bool _toggleOnClick = true;

    [Header("파트너 관전 중 클릭 판정 (New Input System)")]
    [SerializeField] private InputAction _pointAction =
        new InputAction("AugmentPartnerPoint", InputActionType.Value, "<Pointer>/position", expectedControlType: "Vector2");

    [Tooltip("파트너 관전 중 이 오브젝트를 여는 입력. 좌클릭.")]
    [SerializeField] private InputAction _partnerClickAction =
        new InputAction("AugmentPartnerClick", InputActionType.Button, "<Mouse>/leftButton");

    [Tooltip("파트너 관전 화면에서 이 오브젝트를 클릭했다고 인정할 화면 반경(픽셀). " +
             "PointerUtil.REFERENCE_VIEW_HEIGHT_PX(1080px) 기준값이고, 실제 판정은 렌더 높이에 비례해 자동으로 늘어난다. " +
             "판정 중심은 발밑 피벗이 아니라 콜라이더 중심이라, 오브젝트를 감싸는 데 반지름 약 50px이면 충분하다.")]
    [SerializeField] private float _partnerClickRadius = 60f;

    [Tooltip("관전 중 클릭이 어느 조건에서 걸러졌는지 Console에 남긴다. 이 판정은 화면 좌표로만 " +
             "이뤄져 눈으로는 원인을 알 수 없으므로, 다시 안 열리는 일이 생기면 여기부터 켤 것.")]
    [SerializeField] private bool _logPartnerClickDiagnostics;

    // 파트너 전체화면 관전 판정용 참조(지연 탐색, 씬 배선 없음 — StatInfoController와 동일 패턴).
    private PartnerSpectateView _partnerSpectateView;

    // 관전 중 클릭 판정에 쓰는 자기 콜라이더(RequireComponent로 존재가 보장된다).
    // 로컬 경로(OnMouseDown)와 관전 경로가 이 하나를 같이 보므로, 클릭 범위를 넓히고 싶으면
    // 인스펙터에서 이 콜라이더만 키우면 양쪽에 똑같이 반영된다.
    private Collider _collider;

    // 파트너(상대 클라이언트)가 선택한 증강의 영문명 배열 — GameEvents.OnPartnerAugmentsChanged로
    // 갱신되는 표시 전용 캐시. 로컬 AugmentManager 상태와는 완전히 분리돼 있다.
    private string[] _partnerAugmentNamesEn = System.Array.Empty<string>();

    // Update()의 설정 오류 진단(_panel/_partnerSpectateView 미배선)이 매 좌클릭마다 재검사되는 탓에
    // 그대로 두면 로그가 도배된다 — 종류당 1회로 제한한다.
    private bool _warnedPanelMissing;
    private bool _warnedSpectateViewMissing;

    private void Awake()
    {
        if (_panel == null)
            Debug.LogError("[AugmentInfoTrigger] AugmentInfoPanel 미배선 — 클릭해도 창이 열리지 않는다", this);

        _collider = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        _pointAction.Enable();
        _partnerClickAction.Enable();
        GameEvents.OnPartnerAugmentsChanged += HandlePartnerAugmentsChanged;

        // OnPartnerAugmentsChanged는 파트너가 증강을 고른 그 순간에만 한 번 발행된다(한 판에 한 번뿐).
        // 이 구독이 그 시점보다 늦게 시작되면(재접속이 아닌 컴포넌트 재활성화 등) 다시 맞춰줄 이벤트가
        // 영영 안 와서 그 판 내내 "미선택"으로 고정된다 — 구독 직후 현재 값을 한 번 직접 당겨온다.
        if (GameManager.TryGet(out var gm) && gm.Network != null)
            _partnerAugmentNamesEn = gm.Network.GetPartnerAugmentNamesNow();
    }

    private void OnDisable()
    {
        _pointAction.Disable();
        _partnerClickAction.Disable();
        GameEvents.OnPartnerAugmentsChanged -= HandlePartnerAugmentsChanged;
    }

    private void OnMouseDown()
    {
        if (_panel == null) return;

        // 관전 중에는 이 경로가 호출되지 않아야 정상이다(클래스 doc 참고).
        // 여기에 로그가 찍히면 로컬 경로가 파트너 폴링 경로를 덮어쓰고 있다는 뜻이다.
        if (_logPartnerClickDiagnostics)
        {
            PartnerSpectateView view = EnsurePartnerSpectateView();
            if (view != null && view.IsExpanded)
                Diag($"OnMouseDown이 관전 중에도 호출됨 (IsOverUI={PointerUtil.IsOverUI()})");
        }

        // 창 위를 클릭했는데 뒤에 있는 오브젝트까지 눌리는 것을 막는다.
        if (PointerUtil.IsOverUI()) return;

        if (_toggleOnClick) _panel.Toggle();
        else _panel.Open();
    }

    private void Update()
    {
        if (!_partnerClickAction.WasPressedThisFrame()) return;

        // 이 둘은 관전 여부와 무관하게 매 좌클릭마다 거치는 설정 오류 진단이라, 아래처럼 관전 중에만
        // 남기는 가드를 적용할 수 없다 — 대신 종류당 1회로 도배를 막는다(SynergyTooltipUI._warnedTierOverflow
        // 와 동일 패턴). _panel 미배선은 Awake에서 이미 LogError로 한 번 알리므로 여기선 보조 신호일 뿐이다.
        if (_panel == null)
        {
            if (_logPartnerClickDiagnostics && !_warnedPanelMissing) { _warnedPanelMissing = true; Diag("_panel 미배선"); }
            return;
        }

        PartnerSpectateView spectateView = EnsurePartnerSpectateView();
        if (spectateView == null)
        {
            if (_logPartnerClickDiagnostics && !_warnedSpectateViewMissing) { _warnedSpectateViewMissing = true; Diag("PartnerSpectateView를 씬에서 못 찾음"); }
            return;
        }

        // 관전 중이 아닐 때의 평범한 클릭까지 로그로 도배하지 않도록, 아래 진단들은 전체화면 관전 중에만 남긴다.
        if (!spectateView.IsExpanded) return;

        // "준비 중"(파트너 컨텐츠를 아직 한 번도 못 받음) 상태면 PipRawImage 자체가 꺼져있다 —
        // 꺼진 오브젝트는 RaycastAll에 절대 안 잡히므로 아래 IsBlockedByOtherUI의 "배경 자신인지"
        // 비교가 이 상태에서는 항상 실패해 정상 클릭까지 막아버린다(2026-08 재재리뷰 지적). 화면에
        // 실제로 아무것도 안 보이는 상태라 클릭을 받을 이유도 없으니, 여기서 먼저 걸러낸다.
        if (!spectateView.IsShowingContent)
        {
            if (_logPartnerClickDiagnostics)
                Diag($"IsShowingContent=false (관전 카메라 꺼짐 — 카메라 {(spectateView.SpectatorCamera == null ? "없음" : "있음")})");
            return;
        }

        if (_collider == null) { Diag("Collider 없음 — 판정 기준점을 잡을 수 없다"); return; }

        // 🚨 transform.position(발밑 피벗)이 아니라 콜라이더 중심을 투영한다 — 클래스 doc의 🚨 참고.
        if (!spectateView.TryProjectWorldToScreen(_collider.bounds.center, out Vector2 point))
        {
            if (_logPartnerClickDiagnostics)
                Diag($"화면 투영 실패 (콜라이더 중심 {_collider.bounds.center} — 카메라 뒤이거나 RawImage 미준비)");
            return;
        }

        Vector2 screenPos = _pointAction.ReadValue<Vector2>();
        float radius = ScaledClickRadius();
        float distance = Vector2.Distance(screenPos, point);
        if (distance > radius)
        {
            if (_logPartnerClickDiagnostics)
                Diag($"반경 밖 — 커서 {screenPos}, 콜라이더 중심 투영 {point}, 거리 {distance:F0}px > 허용 {radius:F0}px");
            return;
        }

        // 반경 안이어도 그 지점에 배경(PipRawImage) 소속이 아닌 다른 UI가 맨 위에 있으면 그 UI를
        // 클릭한 것으로 보고 넘긴다 — 클래스 doc 참고.
        if (PointerUtil.IsBlockedByOtherUI(screenPos, spectateView.PipRawImage.gameObject, out GameObject topMost))
        {
            if (_logPartnerClickDiagnostics)
                Diag($"다른 UI에 막힘 — 맨 위 UI '{(topMost == null ? "(없음)" : topMost.name)}', " +
                     $"허용 루트 '{spectateView.PipRawImage.gameObject.name}'");
            return;
        }

        AugmentData data = ResolvePartnerAugmentData();
        if (_logPartnerClickDiagnostics)
            Diag($"열기 — 파트너 증강 {(data == null ? "없음(미선택 표시)" : data.augmentNameEn)}, " +
                 $"거리 {distance:F0}px / 허용 {radius:F0}px");
        if (_toggleOnClick) _panel.TogglePartner(data);
        else _panel.OpenPartner(data);
    }

    /// <summary>렌더 높이에 비례해 보정한 클릭 반경(PointerUtil.ScaledRadius 참고 — StatInfoController도
    /// 같은 함정을 안고 있어 공용 헬퍼로 뽑아뒀다).</summary>
    private float ScaledClickRadius() => PointerUtil.ScaledRadius(_partnerClickRadius);

    /// <summary>관전 중 클릭이 어디서 걸렀는지 남긴다(<see cref="_logPartnerClickDiagnostics"/>가 켜졌을 때만).</summary>
    private void Diag(string message)
    {
        if (!_logPartnerClickDiagnostics) return;
        Debug.Log($"[AugmentInfoTrigger] 관전 클릭 — {message}", this);
    }

    private PartnerSpectateView EnsurePartnerSpectateView()
    {
        if (_partnerSpectateView == null) _partnerSpectateView = FindFirstObjectByType<PartnerSpectateView>();
        return _partnerSpectateView;
    }

    private void HandlePartnerAugmentsChanged(string[] augmentNamesEn)
    {
        _partnerAugmentNamesEn = augmentNamesEn ?? System.Array.Empty<string>();
    }

    /// <summary>캐시된 파트너 영문명 배열의 첫 항목을 AugmentCatalog에서 찾아 표시용 AugmentData로
    /// 변환한다. AugmentManager.RestoreAugmentByNameEn과 같은 조회 방식이되, 상태를 바꾸지 않는 읽기
    /// 전용 버전이다(_activeAugments에 추가하지 않음). 목록이 비었거나 카탈로그에서 못 찾으면 null —
    /// AugmentInfoPanel이 "미선택"과 같은 방식으로 표시한다.</summary>
    private AugmentData ResolvePartnerAugmentData()
    {
        if (_partnerAugmentNamesEn.Length == 0) return null;

        string nameEn = _partnerAugmentNamesEn[0];
        foreach (var data in AugmentCatalog.All)
            if (data != null && data.augmentNameEn == nameEn) return data;

        Debug.LogWarning($"[AugmentInfoTrigger] 파트너 증강 '{nameEn}'을 AugmentCatalog에서 찾지 못함");
        return null;
    }
}
