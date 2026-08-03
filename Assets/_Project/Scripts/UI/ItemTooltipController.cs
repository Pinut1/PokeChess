using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 아이템 설명창(ItemTooltipUI)을 여닫고 커서 옆에 붙이는 쪽.
///
/// ⚠️ <b>항상 활성인 오브젝트</b>(Canvas 등)에 붙일 것. 툴팁 자신에 붙이면
/// 닫힌 동안 호출을 받지 못한다 — SynergyTooltipController와 같은 규약이다.
///
/// <b>위치 규칙</b> — 시너지 툴팁은 상단 고정이지만 이쪽은 커서를 따라간다.
/// 커서 오른쪽 아래에 붙이되, 화면 밖으로 나가면 반대쪽으로 뒤집는다(윈도우 툴팁과 같은 동작).
///
/// 툴팁 루트의 <b>부모에는 레이아웃 그룹을 두지 말 것</b> — 여기서 잡은 위치를 매 프레임 되돌린다.
/// </summary>
public class ItemTooltipController : MonoBehaviour
{
    [Header("툴팁")]
    [Tooltip("씬에 배치해둔 설명창(ItemTooltip_Pf). 씬에는 꺼둔 상태로 저장할 것.")]
    [SerializeField] private ItemTooltipUI _panel;

    [Header("커서 기준 위치")]
    [Tooltip("커서로부터 떨어뜨릴 거리(픽셀). x는 오른쪽, y는 위쪽이 양수.")]
    [SerializeField] private Vector2 _cursorOffset = new(18f, -18f);

    [Tooltip("화면 가장자리에서 최소한 남길 여백(픽셀).")]
    [SerializeField] private float _screenMargin = 8f;

    // 지금 툴팁을 띄운 쪽. 다른 칸으로 커서가 옮겨간 뒤 도착하는 Exit이
    // 새로 연 툴팁을 꺼버리는 걸 막는다(시너지 툴팁과 같은 이유).
    private object _owner;

    private Canvas _canvas;

    private void Awake()
    {
        if (_panel == null)
        {
            Debug.LogError("[ItemTooltipController] 툴팁 미배선 — 아이템에 올려도 설명창이 뜨지 않는다", this);
            return;
        }

        _canvas = _panel.GetComponentInParent<Canvas>();
        if (_canvas != null) _canvas = _canvas.rootCanvas;

        // 씬에 켜둔 채 저장했더라도 시작은 닫힌 상태로 맞춘다.
        _panel.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────
    // 호버 대상이 부르는 API
    // ─────────────────────────────────────────

    /// <summary>커서가 아이템에 들어왔을 때. 표시할 게 없으면 열지 않는다.</summary>
    public void Show(object owner, ScriptableObject data)
    {
        if (_panel == null) return;

        if (data == null)
        {
            Hide(owner);
            return;
        }

        _panel.gameObject.SetActive(true);

        if (!_panel.Bind(data)) // 지원하지 않는 타입
        {
            // Hide(owner)를 쓰면 안 된다 — 아직 _owner를 이 owner로 바꾸기 전이라
            // "네 것이 아니다"라는 가드에 걸려 무시되고, 이전 아이템 내용이 화면에 남는다.
            Close();
            return;
        }

        _owner = owner;

        // 형제 중 맨 뒤(= 맨 앞에 그려짐)로 올린다. 툴팁은 무엇을 가리키든 그 위에 떠야 하는데,
        // 씬에서는 상세창(StatInfo_Panel_pf)이 이 툴팁보다 뒤 형제라 그대로 두면 창 뒤로 숨는다.
        _panel.transform.SetAsLastSibling();

        Place(Input.mousePosition);
    }

    /// <summary>커서가 아이템에서 나갔을 때. 이미 다른 칸이 띄운 툴팁은 건드리지 않는다.</summary>
    public void Hide(object owner)
    {
        if (owner != null && _owner != owner) return;

        Close();
    }

    /// <summary>
    /// 소유자와 무관하게 무조건 닫는다.
    /// ⚠️ 이 컨트롤러는 인벤토리와 아이템 상점이 공유하므로, 한쪽이 꺼질 때 이걸 부르면
    /// 다른 쪽이 띄워둔 툴팁까지 꺼진다. 자기 것만 닫으려면 소유자별로 <see cref="Hide"/>를 부를 것.
    /// </summary>
    public void HideAll() => Close();

    private void Close()
    {
        _owner = null;
        if (_panel != null) _panel.gameObject.SetActive(false);
    }

    // 열려 있는 동안 커서를 따라간다. 칸 위에서 마우스를 움직여도 창이 따라붙는다.
    private void LateUpdate()
    {
        if (_panel != null && _panel.gameObject.activeSelf)
            Place(Input.mousePosition);
    }

    // ─────────────────────────────────────────
    // 배치
    // ─────────────────────────────────────────

    private void Place(Vector2 screenPos)
    {
        RectTransform rect = _panel.Rect;
        if (rect == null) return;

        // ContentSizeFitter 결과를 이번 프레임에 반영한다.
        // 안 하면 방금 켠 프레임에는 이전 내용의 크기로 자리를 잡아 창이 한 번 튄다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        float scale = _canvas != null ? _canvas.scaleFactor : 1f;
        Vector2 size = rect.rect.size * scale;

        // 기본은 커서 오른쪽 아래. 피벗과 무관하게 다루려고 좌상단 기준으로 계산한다.
        Vector2 topLeft = screenPos + _cursorOffset * scale;

        float margin = _screenMargin * scale;

        // 오른쪽이 넘치면 커서 왼쪽으로 뒤집는다.
        if (topLeft.x + size.x > Screen.width - margin)
            topLeft.x = screenPos.x - _cursorOffset.x * scale - size.x;

        // 아래가 넘치면 커서 위쪽으로 뒤집는다.
        if (topLeft.y - size.y < margin)
            topLeft.y = screenPos.y - _cursorOffset.y * scale + size.y;

        // 뒤집고도 반대쪽이 넘치면(창이 화면보다 클 때) 화면 안으로 밀어 넣는다.
        topLeft.x = Mathf.Clamp(topLeft.x, margin, Mathf.Max(margin, Screen.width - margin - size.x));
        topLeft.y = Mathf.Clamp(topLeft.y, Mathf.Min(Screen.height - margin, margin + size.y),
                                           Screen.height - margin);

        // 좌상단 → 피벗 기준 좌표로 되돌린다.
        Vector2 pivot = rect.pivot;
        rect.position = new Vector3(topLeft.x + size.x * pivot.x,
                                    topLeft.y - size.y * (1f - pivot.y),
                                    rect.position.z);
    }
}
