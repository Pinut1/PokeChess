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
/// 좌클릭을 직접 폴링해, 이 오브젝트(씬에 단 하나뿐인 실제 증강 오브젝트)가 관전 카메라 화면에
/// 투영되는 위치 근처를 클릭했는지 판정한다 — StatInfoController.TryHandlePartnerOpen과 같은 패턴.
/// 이 경로는 파트너가 실제로 선택한 증강(GameEvents.OnPartnerAugmentsChanged로 캐시)만 표시하며
/// 로컬 AugmentManager 상태는 전혀 건드리지 않는다.
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

    [Tooltip("파트너 관전 화면에서 이 오브젝트를 클릭했다고 인정할 화면 반경(픽셀). StatInfoController의 " +
             "_battlePickRadius 기본값과 동일하게 맞췄다.")]
    [SerializeField] private float _partnerClickRadius = 60f;

    // 파트너 전체화면 관전 판정용 참조(지연 탐색, 씬 배선 없음 — StatInfoController와 동일 패턴).
    private PartnerSpectateView _partnerSpectateView;

    // 파트너(상대 클라이언트)가 선택한 증강의 영문명 배열 — GameEvents.OnPartnerAugmentsChanged로
    // 갱신되는 표시 전용 캐시. 로컬 AugmentManager 상태와는 완전히 분리돼 있다.
    private string[] _partnerAugmentNamesEn = System.Array.Empty<string>();

    private void Awake()
    {
        if (_panel == null)
            Debug.LogError("[AugmentInfoTrigger] AugmentInfoPanel 미배선 — 클릭해도 창이 열리지 않는다", this);
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

        // 창 위를 클릭했는데 뒤에 있는 오브젝트까지 눌리는 것을 막는다.
        if (PointerUtil.IsOverUI()) return;

        if (_toggleOnClick) _panel.Toggle();
        else _panel.Open();
    }

    private void Update()
    {
        if (!_partnerClickAction.WasPressedThisFrame()) return;
        if (_panel == null) return;

        PartnerSpectateView spectateView = EnsurePartnerSpectateView();
        if (spectateView == null || !spectateView.IsExpanded) return;

        if (!spectateView.TryProjectWorldToScreen(transform.position, out Vector2 point)) return;

        Vector2 screenPos = _pointAction.ReadValue<Vector2>();
        if (Vector2.Distance(screenPos, point) > _partnerClickRadius) return;

        AugmentData data = ResolvePartnerAugmentData();
        if (_toggleOnClick) _panel.TogglePartner(data);
        else _panel.OpenPartner(data);
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
