using UnityEngine;

/// <summary>
/// 유닛을 드래그해서 놓으면 판매되는 영역. HexTile/BenchTile과 같은 IDropTarget 패턴.
/// 씬에 Collider가 있는 오브젝트(예: Quad/Cube)에 붙여 사용한다. 호버 시 빨갛게 표시.
/// 실제 제거/환급은 BoardManager.SellUnit → GameEvents.OnUnitSold → ShopManager가 처리.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SellZone : MonoBehaviour, IDropTarget
{
    [SerializeField] private Color _hoverColor = new Color(1f, 0.3f, 0.3f);

    private Renderer _renderer;
    private Color _defaultColor;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer != null) _defaultColor = _renderer.material.color;
    }

    public void OnHoverEnter() { if (_renderer != null) _renderer.material.color = _hoverColor; }
    public void OnHoverExit()  { if (_renderer != null) _renderer.material.color = _defaultColor; }

    public void OnDropUnit(PokemonUnit unit)
    {
        var board = GameManager.Instance != null ? GameManager.Instance.Board : null;
        if (board != null) board.SellUnit(unit);
    }
}
