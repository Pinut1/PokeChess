using UnityEngine;

/// <summary>
/// 역할 설명창(RoleTooltipUI)을 여닫고 커서 오른쪽 위에 붙이는 쪽.
///
/// ⚠️ <b>항상 활성인 오브젝트</b>(Canvas 등)에 붙일 것. 툴팁 자신에 붙이면
/// 닫힌 동안 호출을 받지 못한다 — 다른 툴팁 컨트롤러와 같은 규약이다.
///
/// 툴팁 루트의 <b>부모에는 레이아웃 그룹을 두지 말 것</b> — 여기서 잡은 위치를 매 프레임 되돌린다.
/// </summary>
public class RoleTooltipController : MonoBehaviour
{
    [Header("툴팁")]
    [Tooltip("씬에 배치해둔 설명창(RoleTooltip_Pf). 씬에는 꺼둔 상태로 저장할 것.")]
    [SerializeField] private RoleTooltipUI _panel;

    [Header("커서 기준 위치")]
    [Tooltip("커서로부터 떨어뜨릴 거리(픽셀). 오른쪽 위로 띄우려면 둘 다 양수.")]
    [SerializeField] private Vector2 _cursorOffset = new(18f, 18f);

    [Tooltip("화면 가장자리에서 최소한 남길 여백(픽셀).")]
    [SerializeField] private float _screenMargin = 8f;

    // 지금 툴팁을 띄운 쪽. 다른 카드로 커서가 옮겨간 뒤 도착하는 Exit이
    // 새로 연 툴팁을 꺼버리는 걸 막는다.
    private object _owner;

    private Canvas _canvas;

    private void Awake()
    {
        if (_panel == null)
        {
            Debug.LogError("[RoleTooltipController] 툴팁 미배선 — 카드에 올려도 역할이 뜨지 않는다", this);
            return;
        }

        _canvas = _panel.GetComponentInParent<Canvas>();
        if (_canvas != null) _canvas = _canvas.rootCanvas;

        _panel.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────
    // 호버 대상이 부르는 API
    // ─────────────────────────────────────────

    /// <summary>
    /// 아직 사지 않은 종(상점 카드)의 역할을 띄운다.
    /// 보유 중인 영웅증강이 이 종의 역할을 바꾼다면 <b>바뀔 역할</b>을 보여준다 —
    /// 사는 순간 탱커가 되는데 카드에 서포터라고 적혀 있으면 거짓 정보가 된다.
    /// </summary>
    public void Show(object owner, PokemonData data)
    {
        if (data == null)
        {
            Hide(owner);
            return;
        }

        Show(owner, EffectiveRoleOf(data));
    }

    /// <summary>이미 산 유닛의 역할을 띄운다. PokemonUnit.Role이 증강 오버라이드를 이미 반영한다.</summary>
    public void Show(object owner, PokemonUnit unit)
    {
        if (unit == null)
        {
            Hide(owner);
            return;
        }

        Show(owner, unit.Role);
    }

    /// <summary>역할 문자열을 직접 넘기는 진입점.</summary>
    public void Show(object owner, string role)
    {
        if (_panel == null) return;

        _panel.gameObject.SetActive(true);

        if (!_panel.Bind(role))
        {
            // Hide(owner)를 쓰면 안 된다 — 아직 _owner를 이 owner로 바꾸기 전이라
            // "네 것이 아니다"라는 가드에 걸려 무시되고, 이전 유닛의 역할이 화면에 남는다.
            Close();
            return;
        }

        _owner = owner;
        Place(Input.mousePosition);
    }

    /// <summary>커서가 나갔을 때. 이미 다른 쪽이 띄운 툴팁은 건드리지 않는다.</summary>
    public void Hide(object owner)
    {
        if (owner != null && _owner != owner) return;

        Close();
    }

    /// <summary>
    /// 소유자와 무관하게 무조건 닫는다.
    /// ⚠️ 컨트롤러를 여러 쪽이 공유한다면 이걸 쓰지 말 것 — 남이 띄운 툴팁까지 꺼버린다.
    /// 자기 것만 닫으려면 소유자별로 <see cref="Hide"/>를 부르면 된다.
    /// </summary>
    public void HideAll() => Close();

    private void Close()
    {
        _owner = null;
        if (_panel != null) _panel.gameObject.SetActive(false);
    }

    // 열려 있는 동안 커서를 따라간다.
    private void LateUpdate()
    {
        if (_panel != null && _panel.gameObject.activeSelf)
            Place(Input.mousePosition);
    }

    /// <summary>보유 증강까지 반영한 역할. 바꾸는 증강이 없으면 종 데이터의 역할 그대로.</summary>
    private static string EffectiveRoleOf(PokemonData data)
    {
        var augment = GameManager.TryGet(out var gm) ? gm.Augment : null;
        if (augment != null)
        {
            foreach (var owned in augment.ActiveAugments)
                if (owned != null && owned.TryGetRoleOverride(data, out string overridden))
                    return overridden;
        }

        return data.role;
    }

    // ─────────────────────────────────────────
    // 배치
    // ─────────────────────────────────────────

    private void Place(Vector2 screenPos)
    {
        RectTransform rect = _panel.Rect;
        if (rect == null) return;

        float scale = _canvas != null ? _canvas.scaleFactor : 1f;
        Vector2 size = rect.rect.size * scale;
        float margin = _screenMargin * scale;

        // 기본은 커서 오른쪽 위. 피벗과 무관하게 다루려고 좌하단 기준으로 계산한다.
        Vector2 bottomLeft = screenPos + new Vector2(_cursorOffset.x, _cursorOffset.y) * scale;

        // 오른쪽이 넘치면 커서 왼쪽으로 뒤집는다.
        if (bottomLeft.x + size.x > Screen.width - margin)
            bottomLeft.x = screenPos.x - _cursorOffset.x * scale - size.x;

        // 위가 넘치면 커서 아래로 뒤집는다.
        if (bottomLeft.y + size.y > Screen.height - margin)
            bottomLeft.y = screenPos.y - _cursorOffset.y * scale - size.y;

        // 뒤집고도 반대쪽이 넘치면(창이 화면보다 클 때) 화면 안으로 밀어 넣는다.
        bottomLeft.x = Mathf.Clamp(bottomLeft.x, margin, Mathf.Max(margin, Screen.width - margin - size.x));
        bottomLeft.y = Mathf.Clamp(bottomLeft.y, margin, Mathf.Max(margin, Screen.height - margin - size.y));

        // 좌하단 → 피벗 기준 좌표로 되돌린다.
        Vector2 pivot = rect.pivot;
        rect.position = new Vector3(bottomLeft.x + size.x * pivot.x,
                                    bottomLeft.y + size.y * pivot.y,
                                    rect.position.z);
    }
}
