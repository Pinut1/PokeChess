using UnityEngine;

/// <summary>
/// 역할 설명창(RoleTooltipUI)을 여닫고 자리를 잡아 주는 쪽.
///
/// 자리 잡는 방식이 둘이다(<see cref="_followCursor"/>).
///   켜짐(기본) — 커서 오른쪽 위에 붙어 따라다닌다. 상점 카드·유닛처럼 <b>대상이 여럿</b>이라
///                 어디에 띄울지 미리 정할 수 없는 툴팁용.
///   꺼짐       — 씬에 배치해둔 자리에 그대로 뜬다. 화면에 버튼이 한 곳뿐이라
///                 <b>뜰 자리가 정해져 있는</b> 안내 툴팁용(XP 구매 설명 등).
///
/// ⚠️ <b>항상 활성인 오브젝트</b>(Canvas 등)에 붙일 것. 툴팁 자신에 붙이면
/// 닫힌 동안 호출을 받지 못한다 — 다른 툴팁 컨트롤러와 같은 규약이다.
///
/// 툴팁 루트의 <b>부모에는 레이아웃 그룹을 두지 말 것</b> — 여기서 잡은 위치를 매 프레임 되돌린다.
/// (고정 자리 모드에서는 여기서 위치를 건드리지 않으므로 이 제약도 없다.)
/// </summary>
public class RoleTooltipController : MonoBehaviour
{
    [Header("툴팁")]
    [Tooltip("씬에 배치해둔 설명창(RoleTooltip_Pf). 씬에는 꺼둔 상태로 저장할 것.")]
    [SerializeField] private RoleTooltipUI _panel;

    [Header("자리 잡기")]
    [Tooltip("켜면 커서를 따라다닌다(기존 동작). 끄면 씬에 배치해둔 자리에 그대로 뜬다 — " +
             "아래 커서 오프셋·화면 여백은 이 값이 켜져 있을 때만 쓰인다.")]
    [SerializeField] private bool _followCursor = true;

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

    /// <summary>
    /// 인스펙터에 안 물렸을 때의 폴백 탐색. 씬에 설명창이 <b>여러 개일 수 있다</b> —
    /// 상점 카드용(역할 이름만)과 스탯창용(추천 아이템까지)을 따로 두기 때문이다.
    /// 어느 것을 잡을지 보장할 수 없으므로, 조용히 아무거나 쓰지 않고 경고를 남긴다.
    ///
    /// 폴백을 쓰는 쪽이 여럿이라(스탯창·안내 툴팁) 여기 한 곳에 모아 뒀다 —
    /// 한쪽만 경고를 남기면 다른 쪽은 잘못 물린 줄도 모르고 지나간다.
    /// </summary>
    /// <param name="context">경고 로그를 클릭했을 때 선택될 오브젝트.</param>
    public static RoleTooltipController FindInScene(Object context)
    {
        var found = FindObjectsByType<RoleTooltipController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (found == null || found.Length == 0) return null;

        if (found.Length > 1)
        {
            Debug.LogWarning(
                $"[RoleTooltipController] 역할군 설명창이 씬에 {found.Length}개 있습니다 — " +
                "어느 것을 쓸지 정할 수 없어 첫 번째를 씁니다. " +
                "상점용(RoleTooltip_Pf)과 스탯창용(RoleItemTooltip_Pf)을 나눠 뒀다면 " +
                "인스펙터에 직접 물려 주세요.", context);
        }

        return found[0];
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

        // 진화잠금(나인이볼부스트 이브이 등)이면 돌 추천을 빼야 하므로 유닛 상태를 같이 넘긴다.
        Show(owner, unit.Role, unit.data, unit.evolutionLocked);
    }

    /// <summary>역할 문자열을 직접 넘기는 진입점.</summary>
    public void Show(object owner, string role) => Show(owner, role, null, false);

    /// <summary>
    /// <b>인스펙터에서 부르는 진입점</b>(EventTrigger의 PointerEnter 등). 인자가 하나뿐이라
    /// UnityEvent에 그대로 물릴 수 있다 — 스크립트를 새로 만들지 않고 간단한 안내 툴팁을 붙일 때 쓴다.
    ///
    /// 넘긴 문구는 <b>그대로</b> 나온다. 역할군 대응표에 없는 문자열은 형식을 입히지 않기 때문에
    /// "옵션", "1-3 야생동물" 같은 안내문을 그대로 띄울 수 있다.
    ///
    /// 닫을 때는 <see cref="HideAll"/>을 PointerExit에 물리면 된다(인자가 없어 인스펙터에서 부를 수 있다).
    /// </summary>
    public void ShowText(string text) => Show(this, text, null, false);

    /// <summary>
    /// 역할과 함께 <b>지금 보고 있는 유닛</b>을 넘기는 진입점.
    /// 설명창에 아이템 줄이 물려 있으면 그 유닛이 쓸 수 있는 진화의 돌까지 뒤에 붙는다.
    /// </summary>
    public void Show(object owner, string role, PokemonData pokemon, bool evolutionLocked)
    {
        if (_panel == null) return;

        _panel.gameObject.SetActive(true);

        if (!_panel.Bind(role, pokemon, evolutionLocked))
        {
            // Hide(owner)를 쓰면 안 된다 — 아직 _owner를 이 owner로 바꾸기 전이라
            // "네 것이 아니다"라는 가드에 걸려 무시되고, 이전 유닛의 역할이 화면에 남는다.
            Close();
            return;
        }

        _owner = owner;

        // 고정 자리 모드에서는 씬에 잡아둔 위치를 그대로 둔다.
        if (_followCursor) Place(Input.mousePosition);
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

    // 열려 있는 동안 커서를 따라간다. 고정 자리 모드면 아무 것도 하지 않는다.
    private void LateUpdate()
    {
        if (!_followCursor) return;

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
