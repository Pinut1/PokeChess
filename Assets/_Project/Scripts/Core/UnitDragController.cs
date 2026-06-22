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
        var phase = GameManager.Instance != null ? GameManager.Instance.Phase : null;
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
    }

    private void Drop()
    {
        _hovered?.OnHoverExit();
        IDropTarget target = RaycastDropTarget();

        if (target != null)
            target.OnDropUnit(_held); // 콜백 → BoardManager → 이벤트 → BoardView.Resync
        else if (BoardView.Instance != null)
            BoardView.Instance.Resync(); // 무효 드롭 — 논리 상태 불변이므로 원위치 복귀

        _held = null;
        _hovered = null;
    }

    private void CancelDrag()
    {
        _hovered?.OnHoverExit();
        if (BoardView.Instance != null) BoardView.Instance.Resync();
        _held = null;
        _hovered = null;
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
