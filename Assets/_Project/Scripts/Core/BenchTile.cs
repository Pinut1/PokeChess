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

        EnsureDropCollider();
    }

    /// <summary>
    /// Imported tile meshes are not guaranteed to accept a raycast on their top face.
    /// A root collider keeps bench drops independent from the visual mesh topology.
    /// </summary>
    private void EnsureDropCollider()
    {
        if (GetComponent<Collider>() != null) return;

        Renderer[] childRenderers = GetComponentsInChildren<Renderer>();
        if (childRenderers.Length == 0) return;

        Bounds worldBounds = childRenderers[0].bounds;
        for (int i = 1; i < childRenderers.Length; i++)
            worldBounds.Encapsulate(childRenderers[i].bounds);

        Vector3 localMin = transform.InverseTransformPoint(worldBounds.min);
        Vector3 localMax = transform.InverseTransformPoint(worldBounds.max);

        BoxCollider dropCollider = gameObject.AddComponent<BoxCollider>();
        dropCollider.center = (localMin + localMax) * 0.5f;
        dropCollider.size = new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(localMax.x - localMin.x)),
            Mathf.Max(0.1f, Mathf.Abs(localMax.y - localMin.y)),
            Mathf.Max(0.01f, Mathf.Abs(localMax.z - localMin.z)));
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
