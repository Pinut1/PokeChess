using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 마우스 커서 텍스처 교체. 씬 진입 시 기본 커서를 적용하고, 좌클릭 누름/뗌에 맞춰 전환한다.
/// Singleton&lt;T&gt;로 DontDestroyOnLoad — 씬을 넘어가도 유지되므로 최초 진입 씬에만 배치하면 된다.
/// 입력은 UnitDragController/StatInfoController와 같은 규약(새 Input System의 임베드 InputAction)을 따른다.
/// </summary>
public class CursorController : Singleton<CursorController>
{
    [SerializeField] private Texture2D _normalCursor;
    [SerializeField] private Texture2D _pressedCursor;
    [SerializeField] private Vector2 _hotspot = Vector2.zero;

    [SerializeField] private InputAction _clickAction =
        new InputAction("Click", InputActionType.Button, "<Pointer>/press");

    private void OnEnable()  => _clickAction.Enable();
    private void OnDisable() => _clickAction.Disable();

    private void Start()
    {
        ApplyCursor(_normalCursor);
    }

    /// <summary>엣지(눌리는/떼지는 순간) 감지 대신 매 프레임 현재 버튼 상태를 그대로 반영한다.
    /// 같은 프레임에 눌림+뗌이 동시에 감지되는 경우(저프레임/빠른 클릭)나 창 포커스를 잃었다
    /// 되찾는 경우처럼 엣지를 놓칠 수 있는 상황에서도 항상 실제 상태와 어긋나지 않는다.</summary>
    private void Update()
    {
        ApplyCursor(_clickAction.IsPressed() ? _pressedCursor : _normalCursor);
    }

    private Texture2D _appliedTexture;

    private void ApplyCursor(Texture2D texture)
    {
        if (texture == null || texture == _appliedTexture) return;
        Cursor.SetCursor(texture, _hotspot, CursorMode.Auto);
        _appliedTexture = texture;
    }
}
