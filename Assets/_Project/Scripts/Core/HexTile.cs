using System;
using UnityEngine;

/// <summary>
/// 전장(Board)의 개별 헥사곤 타일을 담당하는 스크립트.
/// 상태(빈 칸 / 유닛 있음 / 드래그 호버)에 따라 색을 바꾸고, 유닛 드롭 시 BoardManager에게 콜백으로 보고합니다.
///
/// <b>색은 곱셈 틴트</b>입니다 — URP/Lit의 `_BaseColor`에 값을 넣으면 타일 텍스처(_BaseMap) 위에 곱해집니다.
/// 그래서 흰색(1,1,1,1)이 "원래 색 그대로"이고, 어둡거나 색이 섞인 값일수록 그만큼 물듭니다.
///
/// ⚠️ <b>루트 MeshRenderer에는 머티리얼이 없습니다</b>(Hexlands 프리팹 구조 — 실제 그림은 자식
/// HexTop_*/HexBase에 있음). 그래서 루트 렌더러만 건드리면 화면에는 아무 변화가 없습니다.
/// 자식 렌더러를 전부 모아 칠하는 이유가 이것입니다.
///
/// 머티리얼 대신 <see cref="MaterialPropertyBlock"/>을 쓰는 이유: `renderer.material`은 접근하는
/// 순간 타일마다 머티리얼 사본을 만들어(28칸 × 자식 수) 배칭이 깨지고 메모리에 남습니다.
/// 프로퍼티 블록은 사본을 만들지 않습니다.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class HexTile : MonoBehaviour, IDropTarget
{
    [Header("Tile Colors")]
    [Tooltip("빈 타일. 흰색이면 타일 원래 색 그대로다(틴트는 곱셈).")]
    [SerializeField] private Color _defaultColor = Color.white;

    [Tooltip("유닛이 올라가 있는 타일. 빈 칸과 한눈에 구분되게 잡을 것.\n" +
             "Use Occupied Color를 끄면 이 값은 무시되고 빈 칸과 같은 색이 된다.")]
    [SerializeField] private Color _occupiedColor = new(0.62f, 0.78f, 1f, 1f);

    [Tooltip("유닛을 끌고 와 커서가 올라간 타일. 점유 여부보다 우선한다.")]
    [SerializeField] private Color _hoverColor = Color.cyan;

    [Tooltip("점유 색을 쓸지. 끄면 예전처럼 빈 칸/유닛 칸이 같은 색이 된다(연출 비교용 스위치).")]
    [SerializeField] private bool _useOccupiedColor = true;

    // URP/Lit은 _BaseColor, 내장 Standard·Unlit은 _Color를 쓴다. 어느 쪽이 와도 되게 둘 다 넣는다.
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId     = Shader.PropertyToID("_Color");

    // 루트 + 자식 전부. 루트는 머티리얼이 없어 실제로는 자식이 칠해진다.
    private Renderer[] _renderers;
    private MaterialPropertyBlock _block;

    private HexCoords _coords;
    private bool _isOccupied;
    private bool _isHovered;
    private bool _battleLocked;

    // 매니저 직접 참조를 피하기 위한 콜백 대리자 (완벽한 디커플링)
    private Action<PokemonUnit, HexCoords> _onDropCallback;

    /// <summary>지금 이 타일에 유닛이 올라가 있는지. 판정의 주인은 BoardManager이고 여기엔 표시용 사본만 둔다.</summary>
    public bool IsOccupied => _isOccupied;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _block = new MaterialPropertyBlock();
        ApplyColor();
    }

    /// <summary>
    /// BoardManager가 타일을 생성할 때 좌표와 콜백 함수, 그리고 하이어라키 표시용 이름을 주입해줍니다.
    /// </summary>
    public void Initialize(HexCoords coords, Action<PokemonUnit, HexCoords> onDropCallback, string customName = null)
    {
        _coords = coords;
        _onDropCallback = onDropCallback;

        // 유니티 씬(Scene) 하이어라키 창에서 보기 편하게 이름 강제 변경
        gameObject.name = string.IsNullOrEmpty(customName) ? $"HexTile_{coords.q}_{coords.r}_{coords.s}" : customName;
    }

    public HexCoords GetCoords()
    {
        return _coords;
    }

    /// <summary>
    /// 유닛이 올라왔는지 여부를 알려준다. BoardManager가 _battleField를 바꿀 때마다 불러준다
    /// (타일은 배치 룰을 모른다는 기존 규약 그대로 — 상태를 받기만 한다).
    /// </summary>
    public void SetOccupied(bool occupied)
    {
        if (_isOccupied == occupied) return;

        _isOccupied = occupied;
        ApplyColor();
    }

    /// <summary>
    /// 전투 중 색 고정. 켜면 점유·호버를 모두 무시하고 Default Color만 쓴다.
    /// 점유 상태 자체는 계속 기록되므로 전투가 끝나고 풀면 그 시점 배치대로 다시 칠해진다.
    ///
    /// 호버까지 막는 이유: 전투 중에는 UnitDragController가 보드 타일 드롭을 거부하는데
    /// 타일만 하이라이트되면 "여기 놓을 수 있다"고 잘못 읽힌다.
    /// </summary>
    public void SetBattleLocked(bool locked)
    {
        if (_battleLocked == locked) return;

        _battleLocked = locked;
        ApplyColor();
    }

    // ──────────────────────────────────────────
    // IDropTarget 인터페이스 구현부
    // ──────────────────────────────────────────

    public void OnHoverEnter()
    {
        _isHovered = true;
        ApplyColor();
    }

    public void OnHoverExit()
    {
        // 예전에는 무조건 _defaultColor로 되돌렸다. 이제는 점유 상태로 되돌려야
        // 유닛이 올라간 칸을 스쳐 지나갈 때 그 칸만 빈 칸 색으로 남는 일이 없다.
        _isHovered = false;
        ApplyColor();
    }

    public void OnDropUnit(PokemonUnit unit)
    {
        // 타일 스스로는 배치 룰을 모릅니다. 무작정 매니저에게 "얘가 여기 떨어졌어요!" 하고 콜백으로 던집니다.
        _onDropCallback?.Invoke(unit, _coords);
    }

    // ──────────────────────────────────────────
    // 색 적용
    // ──────────────────────────────────────────

    /// <summary>전투 고정 &gt; 호버 &gt; 점유 &gt; 빈 칸 순으로 색을 고르고 렌더러 전체에 씌운다.</summary>
    private void ApplyColor()
    {
        if (_renderers == null || _block == null) return;

        Color color = _battleLocked                     ? _defaultColor
                    : _isHovered                        ? _hoverColor
                    : _isOccupied && _useOccupiedColor  ? _occupiedColor
                                                        : _defaultColor;

        _block.SetColor(BaseColorId, color);
        _block.SetColor(ColorId, color);

        foreach (Renderer renderer in _renderers)
            if (renderer != null) renderer.SetPropertyBlock(_block);
    }

#if UNITY_EDITOR
    /// <summary>
    /// 플레이 중 인스펙터에서 색을 만지면 바로 반영된다(값 맞출 때 껐다 켤 필요 없음).
    /// 하이어라키에서 타일을 여러 개 선택해두면 선택한 타일에 한꺼번에 적용된다.
    /// </summary>
    private void OnValidate()
    {
        if (Application.isPlaying) ApplyColor();
    }
#endif
}
