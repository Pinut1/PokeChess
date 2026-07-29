using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 마우스로 유닛을 집어 IDropTarget(보드 타일/벤치 슬롯)에 놓는 드래그앤드롭 컨트롤러.
/// 아키텍처 문서의 "PlayerController" 역할. 쇼핑 페이즈에서만 동작한다.
/// 레이캐스트를 사용하므로 유닛/타일/벤치에 Collider가 있어야 한다.
///
/// 입력은 새 Input System의 "임베드된 InputAction"을 사용한다.
/// - 코드는 장치가 아니라 "Point"/"Click"이라는 추상 액션만 읽는다.
/// - 두 액션은 [SerializeField]라 인스펙터에서 바인딩을 직접 편집/추가할 수 있고(예: 패드·터치),
///   런타임 리바인딩(PerformInteractiveRebinding)도 가능하다. 별도 .inputactions 자산은 불필요.
/// </summary>
public class UnitDragController : MonoBehaviour
{
    [Header("Camera / Raycast")]
    [SerializeField] private Camera _camera;
    [SerializeField] private float _dragHeight = 1.0f;
    [SerializeField] private LayerMask _raycastMask = ~0;

    [Header("Input Actions (인스펙터에서 바인딩 편집·추가 가능)")]
    // <Pointer>는 마우스·펜·터치를 모두 포괄하는 추상 장치 →
    // 에디터(마우스)와 Android(터치)가 동일한 바인딩/코드로 동작한다.
    [SerializeField] private InputAction _pointAction =
        new InputAction("Point", InputActionType.Value, "<Pointer>/position", expectedControlType: "Vector2");
    [SerializeField] private InputAction _clickAction =
        new InputAction("Click", InputActionType.Button, "<Pointer>/press");

    [Header("상점 판매 영역 (Canvas UI)")]
    [Tooltip("여기에 유닛을 놓으면 판매된다. 비어 있으면 활성 상점 패널(UnitStore_Panel / ItemStore_Panel)을 런타임에 찾는다.")]
    [SerializeField] private RectTransform _shopSellArea;
    [Tooltip("판매 가능할 때만 켜지는 안내 이미지. 평소엔 꺼둔 채로 저장한다.")]
    [SerializeField] private GameObject _sellHighlightImage;
    [Tooltip("안내 이미지 안의 판매가 표시 텍스트. 비워두면 금액 없이 이미지만 켜진다.")]
    [SerializeField] private TextMeshProUGUI _sellPriceText;
    [Tooltip("판매가 표시 형식. {0}에 환급 골드가 들어간다.")]
    [SerializeField] private string _sellPriceFormat = "판매 가격: {0}골드";

    private PokemonUnit _held;
    private IDropTarget _hovered;
    private readonly Plane _groundPlane = new Plane(Vector3.up, Vector3.zero);

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;
    }

    private void OnEnable()
    {
        _pointAction.Enable();
        _clickAction.Enable();
    }

    private void OnDisable()
    {
        _pointAction.Disable();
        _clickAction.Disable();
    }

    private void Update()
    {
        if (_camera == null) return;

        // 쇼핑 페이즈에서만 드래그 허용
        var phase = GameManager.TryGet(out var gm) ? gm.Phase : null;
        if (phase != null && phase.CurrentPhase != GamePhase.Shopping)
        {
            if (_held != null) CancelDrag();
            return;
        }

        if (_clickAction.WasPressedThisFrame()) TryPickUp();
        else if (_clickAction.IsPressed() && _held != null) DragUpdate();
        else if (_clickAction.WasReleasedThisFrame() && _held != null) Drop();
    }

    /// <summary>현재 포인터 스크린 좌표(Point 액션).</summary>
    private Vector3 PointerScreenPos()
    {
        Vector2 p = _pointAction.ReadValue<Vector2>();
        return new Vector3(p.x, p.y, 0f);
    }

    private void TryPickUp()
    {
        Ray ray = _camera.ScreenPointToRay(PointerScreenPos());
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, _raycastMask))
        {
            var unit = hit.collider.GetComponentInParent<PokemonUnit>();
            if (unit != null) _held = unit;
        }
    }

    private void DragUpdate()
    {
        Ray ray = _camera.ScreenPointToRay(PointerScreenPos());
        if (_groundPlane.Raycast(ray, out float enter))
        {
            Vector3 p = ray.GetPoint(enter);
            _held.transform.position = new Vector3(p.x, _dragHeight, p.z);
        }

        IDropTarget target = RaycastDropTarget();
        if (!ReferenceEquals(target, _hovered))
        {
            _hovered?.OnHoverExit();
            _hovered = target;
            _hovered?.OnHoverEnter();
        }

        SetSellHighlight(target == null && IsPointerOverShopSellArea());
    }

    private void Drop()
    {
        _hovered?.OnHoverExit();
        SetSellHighlight(false);

        IDropTarget target = RaycastDropTarget();

        if (target != null)
        {
            target.OnDropUnit(_held);
        }
        else if (IsPointerOverShopSellArea())
        {
            // 놓을 타일이 없고 포인터가 상점 위일 때만 판매한다.
            // 타일을 우선하므로 상점 패널이 벤치와 겹쳐도 실수로 팔리지 않는다.
            SellHeldUnit();
        }

        // BoardManager가 배치를 거부하면 갱신 이벤트가 발생하지 않는다.
        // 성공/실패와 관계없이 논리 상태를 다시 그려 실패한 기물을 원래 슬롯으로 돌린다.
        if (BoardView.Instance != null)
            BoardView.Instance.Resync();

        _held = null;
        _hovered = null;
    }

    private void CancelDrag()
    {
        _hovered?.OnHoverExit();
        SetSellHighlight(false);
        if (BoardView.Instance != null) BoardView.Instance.Resync();
        _held = null;
        _hovered = null;
    }

    // ──────────────────────────────────────────
    // 상점 드롭 판매 (TFT 방식)
    //
    // 상점은 Canvas(ScreenSpaceOverlay) UI라 3D 물리 레이캐스트로는 잡히지 않는다.
    // 따라서 IDropTarget이 아니라 포인터의 스크린 좌표가 상점 패널 Rect 안인지로 판정한다.
    // ──────────────────────────────────────────

    private void SellHeldUnit()
    {
        if (_held == null) return;

        var board = GameManager.TryGet(out var gm) ? gm.Board : null;
        if (board != null) board.SellUnit(_held);
    }

    /// <summary>
    /// 포인터가 상점 판매 영역 위에 있는지. 유닛을 들고 있지 않으면 항상 false.
    ///
    /// 상점 패널의 Rect는 실제로 보이는 바보다 훨씬 커서(1600x400) 위로는 벤치 행,
    /// 옆으로는 전투시작 버튼까지 덮는다. 그래서 상점 패널과 하단 바(Bottom_PanelGroup)의
    /// 교집합만 판매 영역으로 인정한다. 벤치는 하단 바 위쪽이라, 버튼은 하단 바 오른쪽
    /// 끝이라 각각 자연히 빠진다.
    /// </summary>
    private bool IsPointerOverShopSellArea()
    {
        if (_held == null) return false;

        RectTransform area = ResolveShopSellArea();
        if (area == null) return false;

        Vector3 pointer = PointerScreenPos();
        if (!ContainsPointer(area, pointer)) return false;

        RectTransform bar = ResolveShopBar();
        return bar == null || ContainsPointer(bar, pointer);
    }

    private static bool ContainsPointer(RectTransform rect, Vector3 screenPoint)
    {
        // ScreenSpaceOverlay 캔버스는 카메라를 null로 넘겨야 좌표가 맞는다.
        var canvas = rect.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, uiCamera);
    }

    /// <summary>실제로 화면에 보이는 하단 상점 바. 판매 영역을 이 안으로 제한한다.</summary>
    private RectTransform ResolveShopBar()
    {
        RectTransform area = ResolveShopSellArea();
        if (area == null) return null;

        var parent = area.parent as RectTransform;
        return parent != null && parent.name == "Bottom_PanelGroup" ? parent : null;
    }

    /// <summary>
    /// 인스펙터 지정 영역이 있으면 그것을, 없으면 현재 활성 상점 패널을 쓴다.
    /// 유닛/아이템 탭이 전환되므로 매번 활성 쪽을 다시 찾는다.
    /// 전투시작 버튼은 상점 패널의 형제라 자연히 판매 영역에서 제외된다.
    /// </summary>
    private RectTransform ResolveShopSellArea()
    {
        if (_shopSellArea != null && _shopSellArea.gameObject.activeInHierarchy)
            return _shopSellArea;

        foreach (RectTransform t in Resources.FindObjectsOfTypeAll<RectTransform>())
        {
            if (!t.gameObject.activeInHierarchy) continue;
            if (t.name == "UnitStore_Panel" || t.name == "ItemStore_Panel")
                return t;
        }

        return null;
    }

    /// <summary>
    /// 판매 가능 상태를 안내 이미지로 알린다. 드롭·취소 시 반드시 꺼진다.
    /// 켜질 때 들고 있는 유닛의 환급액을 함께 표시한다(합성 단계에 따라 달라져 고정값이 아님).
    /// </summary>
    private void SetSellHighlight(bool active)
    {
        if (active && _sellPriceText != null)
            _sellPriceText.text = string.Format(_sellPriceFormat, HeldSellValue());

        if (_sellHighlightImage != null && _sellHighlightImage.activeSelf != active)
            _sellHighlightImage.SetActive(active);
    }

    /// <summary>들고 있는 유닛의 판매 환급액. 상점을 못 찾거나 든 게 없으면 0.</summary>
    private int HeldSellValue()
    {
        if (_held == null) return 0;

        var shop = GameManager.TryGet(out var gm) ? gm.Shop : null;
        return shop != null ? shop.SellValue(_held) : 0;
    }

    /// <summary>포인터 아래의 가장 가까운 IDropTarget(들고 있는 유닛 자신은 제외).</summary>
    private IDropTarget RaycastDropTarget()
    {
        Ray ray = _camera.ScreenPointToRay(PointerScreenPos());
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, _raycastMask);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            if (_held != null && h.collider.GetComponentInParent<PokemonUnit>() == _held) continue;
            var target = h.collider.GetComponentInParent<IDropTarget>();
            if (target != null) return target;
        }
        return null;
    }
}
