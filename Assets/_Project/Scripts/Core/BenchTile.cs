using System;
using UnityEngine;

/// <summary>
/// 벤치의 개별 슬롯. 유닛 드롭 시 BoardManager에 (유닛, 슬롯번호)로 콜백한다.
/// HexTile과 동일한 IDropTarget 콜백 패턴 — 타일은 배치 룰을 모르고 매니저에 위임만 한다.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class BenchTile : MonoBehaviour, IDropTarget
{
    [SerializeField] private Color _defaultColor = new Color(0.8f, 0.8f, 0.6f);
    [SerializeField] private Color _hoverColor = Color.yellow;

    private MeshRenderer _renderer;
    private int _slot;
    private Action<PokemonUnit, int> _onDropCallback;

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        if (_renderer != null) _renderer.material.color = _defaultColor;
    }

    public void Initialize(int slot, Action<PokemonUnit, int> onDropCallback, string customName = null)
    {
        _slot = slot;
        _onDropCallback = onDropCallback;
        gameObject.name = string.IsNullOrEmpty(customName) ? $"BenchTile_{slot}" : customName;
    }

    public int GetSlot() => _slot;

    public void OnHoverEnter() { if (_renderer != null) _renderer.material.color = _hoverColor; }
    public void OnHoverExit()  { if (_renderer != null) _renderer.material.color = _defaultColor; }
    public void OnDropUnit(PokemonUnit unit) => _onDropCallback?.Invoke(unit, _slot);
}
