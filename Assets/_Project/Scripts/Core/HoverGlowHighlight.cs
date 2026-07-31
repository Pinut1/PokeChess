using UnityEngine;

/// <summary>
/// 지정한 오브젝트를 켜서 강조하는 호버 표시. 통신기 화면이 "켜지는" 연출이 이 방식이다.
///
/// 외곽선처럼 모델 전체를 훑지 않으므로 조각이 여러 개인 모델(linktrade는 메시 6개)에서도
/// 안쪽에 잡선이 생기지 않는다. 대신 무엇을 밝힐지는 씬에서 직접 지정한다.
///
/// 쓰는 법: 화면 자리에 판(Quad/Plane 등)을 하나 두고 <b>꺼둔 채로</b> 저장한 뒤
/// Glow Objects에 넣는다. 호버하면 켜지고 벗어나면 꺼진다.
/// 색까지 맡기고 싶으면 Use Hover Color를 켠다 — 판매(빨강)/교환(하늘) 색이 그대로 들어온다.
/// 판의 머티리얼을 Unlit이나 Emission 켠 Lit으로 만들어 두면 발광하는 화면처럼 보인다.
///
/// SellZone이 IHoverHighlight로 찾아 Show/Hide를 호출한다. 배선은 자식 어디에 있든 잡힌다.
/// </summary>
public class HoverGlowHighlight : MonoBehaviour, IHoverHighlight
{
    [Header("켜고 끌 대상")]
    [Tooltip("호버 동안만 켜질 오브젝트들(화면 판 등). 씬에는 꺼둔 상태로 저장할 것.")]
    [SerializeField] private GameObject[] _glowObjects;

    [Header("색")]
    [Tooltip("켜면 호출측이 넘긴 색(판매=빨강/교환=하늘)으로 물들인다. " +
             "끄면 아래 Color를 쓰거나, 머티리얼 색을 그대로 둔다.")]
    [SerializeField] private bool _useHoverColor = true;

    [Tooltip("Use Hover Color가 꺼져 있을 때 쓸 색.")]
    [SerializeField] private Color _color = Color.white;

    [Tooltip("Emission을 지원하는 머티리얼일 때 발광 세기. 0이면 발광을 건드리지 않는다.")]
    [SerializeField] private float _emissionIntensity = 1.5f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId     = Shader.PropertyToID("_Color");
    private static readonly int EmissionId  = Shader.PropertyToID("_EmissionColor");

    // 호버할 때마다 renderer.material을 부르면 그때마다 인스턴스가 하나씩 생긴다. 한 번만 만들어 재사용.
    private Material[] _materials;

    private void Awake()
    {
        CacheMaterials();

        // 씬에 켜둔 채 저장했더라도 시작은 꺼진 상태로 맞춘다.
        SetActive(false);

        if (_glowObjects == null || _glowObjects.Length == 0)
            Debug.LogWarning("[HoverGlowHighlight] 켤 오브젝트가 지정되지 않았다 — 호버해도 아무 변화가 없다", this);
    }

    /// <summary>지정된 오브젝트들의 Renderer 머티리얼 인스턴스를 미리 확보한다.</summary>
    private void CacheMaterials()
    {
        if (_glowObjects == null) return;

        var materials = new System.Collections.Generic.List<Material>();

        foreach (GameObject glow in _glowObjects)
        {
            if (glow == null) continue;

            // 꺼져 있는 오브젝트도 포함해야 한다 — 평소 상태가 '꺼짐'이라서.
            foreach (Renderer renderer in glow.GetComponentsInChildren<Renderer>(includeInactive: true))
                if (renderer != null) materials.Add(renderer.material);
        }

        _materials = materials.ToArray();
    }

    private void OnDestroy()
    {
        // renderer.material 접근으로 만들어진 인스턴스는 자동 정리되지 않는다.
        if (_materials == null) return;

        foreach (Material material in _materials)
            if (material != null) Destroy(material);
    }

    public void Show(Color color)
    {
        ApplyColor(_useHoverColor ? color : _color);
        SetActive(true);
    }

    public void Hide() => SetActive(false);

    private void ApplyColor(Color color)
    {
        if (_materials == null) return;

        foreach (Material material in _materials)
        {
            if (material == null) continue;

            // URP Lit/Unlit은 _BaseColor, 구형·일부 셰이더는 _Color를 쓴다.
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color);
            else if (material.HasProperty(ColorId)) material.SetColor(ColorId, color);

            if (_emissionIntensity <= 0f || !material.HasProperty(EmissionId)) continue;

            // 머티리얼에서 Emission 체크를 안 켜뒀어도 발광하도록 키워드를 직접 켠다.
            material.EnableKeyword("_EMISSION");
            material.SetColor(EmissionId, color * _emissionIntensity);
        }
    }

    private void SetActive(bool active)
    {
        if (_glowObjects == null) return;

        foreach (GameObject glow in _glowObjects)
            if (glow != null && glow.activeSelf != active) glow.SetActive(active);
    }
}
