using UnityEngine;

/// <summary>
/// 마우스 커서 텍스처 교체. 씬 진입 시 기본 커서를 적용하고, 좌클릭 누름/뗌에 맞춰 전환한다.
/// </summary>
public class CursorController : MonoBehaviour
{
    [SerializeField] private Texture2D _normalCursor;
    [SerializeField] private Texture2D _pressedCursor;
    [SerializeField] private Vector2 _hotspot = Vector2.zero;

    private void Start()
    {
        ApplyCursor(_normalCursor);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            ApplyCursor(_pressedCursor);
        else if (Input.GetMouseButtonUp(0))
            ApplyCursor(_normalCursor);
    }

    private void ApplyCursor(Texture2D texture)
    {
        if (texture == null) return;
        Cursor.SetCursor(texture, _hotspot, CursorMode.Auto);
    }
}
