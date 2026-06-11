using UnityEngine;

/// <summary>
/// 보드 위 육각 타일 하나. BoardManager가 생성·관리.
/// </summary>
public class HexTile : MonoBehaviour
{
    [Header("좌표")]
    public int col;
    public int row;

    [Header("상태")]
    public PokemonUnit occupant;

    [Header("색상")]
    [SerializeField] private Color normalColor    = new Color(0.4f, 0.6f, 0.4f, 1f);
    [SerializeField] private Color highlightColor = new Color(0.8f, 0.9f, 0.5f, 1f);
    [SerializeField] private Color occupiedColor  = new Color(0.3f, 0.5f, 0.8f, 1f);

    private Renderer _renderer;
    private MaterialPropertyBlock _mpb;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    public bool IsOccupied => occupant != null;

    private void Awake()
    {
        _renderer = GetComponentInChildren<Renderer>();
        _mpb = new MaterialPropertyBlock();
        RefreshColor();
    }

    /// <summary>마우스 호버 등 임시 하이라이트.</summary>
    public void SetHighlight(bool on)
    {
        ApplyColor(on ? highlightColor : (IsOccupied ? occupiedColor : normalColor));
    }

    /// <summary>점유 상태에 맞는 색으로 복원.</summary>
    public void RefreshColor()
    {
        ApplyColor(IsOccupied ? occupiedColor : normalColor);
    }

    private void ApplyColor(Color color)
    {
        if (_renderer == null) return;
        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(BaseColor, color);
        _renderer.SetPropertyBlock(_mpb);
    }
}
