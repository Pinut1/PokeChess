using System;
using UnityEngine;

/// <summary>
/// 전장(Board)의 개별 헥사곤 타일을 담당하는 스크립트.
/// 마우스 호버 시 색상을 변경하고, 유닛 드롭 시 BoardManager에게 콜백으로 보고합니다.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class HexTile : MonoBehaviour, IDropTarget
{
    [Header("Tile Settings")]
    [SerializeField] private Color _defaultColor = Color.white;
    [SerializeField] private Color _hoverColor = Color.cyan;

    private MeshRenderer _renderer;
    private HexCoords _coords;

    // 매니저 직접 참조를 피하기 위한 콜백 대리자 (완벽한 디커플링)
    private Action<PokemonUnit, HexCoords> _onDropCallback;

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _renderer.material.color = _defaultColor;
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

    // ──────────────────────────────────────────
    // IDropTarget 인터페이스 구현부
    // ──────────────────────────────────────────

    public void OnHoverEnter()
    {
        _renderer.material.color = _hoverColor;
    }

    public void OnHoverExit()
    {
        _renderer.material.color = _defaultColor;
    }

    public void OnDropUnit(PokemonUnit unit)
    {
        // 타일 스스로는 배치 룰을 모릅니다. 무작정 매니저에게 "얘가 여기 떨어졌어요!" 하고 콜백으로 던집니다.
        _onDropCallback?.Invoke(unit, _coords);
    }
}
