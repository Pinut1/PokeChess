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

    private void Update()
    {
        if (_clickAction.WasPressedThisFrame())
            ApplyCursor(_pressedCursor);
        else if (_clickAction.WasReleasedThisFrame())
            ApplyCursor(_normalCursor);
    }

    /// <summary>창 밖에서 마우스 버튼을 놓고 돌아오면 눌림 커서에 갇힐 수 있다 —
    /// 포커스를 되찾을 때 실제 버튼 상태를 다시 확인해 맞춰준다(여전히 누른 채로
    /// 포커스가 돌아오는 경우까지 정확히 처리하기 위함).</summary>
    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) ApplyCursor(_clickAction.IsPressed() ? _pressedCursor : _normalCursor);
    }

    private void ApplyCursor(Texture2D texture)
    {
        if (texture == null) return;
        Cursor.SetCursor(texture, _hotspot, CursorMode.Auto);
    }
}
