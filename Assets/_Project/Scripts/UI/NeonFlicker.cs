using UnityEngine;

/// <summary>
/// 지정한 Renderer의 Emission을 시간에 따라 흔들어 네온 간판처럼 번쩍이게 한다(포켓스톱 등).
///
/// 두 가지를 겹쳐 쓴다.
///  - <b>맥동</b>: 사인파로 은은하게 밝아졌다 어두워지는 기본 리듬.
///  - <b>불규칙 깜빡임</b>: 가끔 짧게 꺼졌다 켜지는 형광등 같은 흔들림(Flicker Chance로 조절, 0이면 끔).
///
/// ⚠️ Emission은 <b>Bloom이 켜져 있어야</b> 네온처럼 번져 보인다.
/// 지금 씬은 카메라의 Render Post Processing이 꺼져 있고 DefaultVolumeProfile의 Bloom intensity도 0이라,
/// 그대로면 "밝은 색"까지만 보인다. 카메라 Post Processing을 켜고 Bloom intensity를 0.5~1 정도 주면 된다.
///
/// 머티리얼 인스턴스를 하나 만들어 쓰므로 같은 머티리얼을 공유하는 다른 오브젝트에는 영향이 없다.
/// </summary>
public class NeonFlicker : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("빛낼 Renderer들. 비워두면 이 오브젝트 아래의 Renderer를 전부 쓴다(간판 부분만 넣는 걸 권장).")]
    [SerializeField] private Renderer[] _targets;

    [Header("켜지는 시점")]
    [Tooltip("켜면 증강을 고르기 전까지 발광이 꺼져 있다가, 증강 선택 순간부터 깜빡이기 시작한다. " +
             "끄면 시작하자마자 계속 깜빡인다.")]
    [SerializeField] private bool _waitForAugmentSelected = true;

    [Header("색")]
    [Tooltip("네온 색. 밝기는 아래 세기 값으로 정하므로 여기서는 색만 잡으면 된다.")]
    [SerializeField] private Color _color = new Color(0.3f, 0.9f, 1f);

    [Tooltip("가장 어두울 때의 발광 세기.")]
    [SerializeField] private float _minIntensity = 0.6f;

    [Tooltip("가장 밝을 때의 발광 세기. Bloom을 켰다면 1~3 사이가 무난하다.")]
    [SerializeField] private float _maxIntensity = 2.5f;

    [Header("맥동")]
    [Tooltip("초당 밝아졌다 어두워지는 횟수. 0이면 맥동 없이 최대 밝기로 고정.")]
    [SerializeField] private float _pulseSpeed = 1.2f;

    [Header("불규칙 깜빡임")]
    [Tooltip("초당 깜빡임이 시작될 확률. 0이면 맥동만 하고 깜빡이지 않는다.")]
    [Range(0f, 10f)]
    [SerializeField] private float _flickerChance = 1.5f;

    [Tooltip("한 번 깜빡일 때 꺼져 있는 시간(초). 짧을수록 형광등처럼 탁탁 튄다.")]
    [SerializeField] private float _flickerDuration = 0.06f;

    [Tooltip("깜빡이는 동안의 발광 세기. 0이면 완전히 꺼진다.")]
    [SerializeField] private float _flickerIntensity = 0.05f;

    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private Material[] _materials;
    private float _flickerUntil;   // 이 시각까지는 꺼진 상태로 둔다
    private float _phaseOffset;    // 여러 개를 배치했을 때 같은 박자로 뛰지 않게
    private bool _glowing;         // 발광이 시작됐는지(증강 선택 전이면 false)

    private void Awake()
    {
        CacheMaterials();

        // 시작 위상을 흩어 두 개 이상 놓아도 한 몸처럼 깜빡이지 않게 한다.
        _phaseOffset = Random.value * Mathf.PI * 2f;

        _glowing = !_waitForAugmentSelected;
        ApplyIntensity(_glowing ? _maxIntensity : 0f);
    }

    private void OnEnable()
    {
        GameEvents.OnAugmentSelected += HandleAugmentSelected;
    }

    private void OnDisable()
    {
        GameEvents.OnAugmentSelected -= HandleAugmentSelected;
    }

    private void Start()
    {
        // 이 오브젝트가 증강 선택보다 늦게 켜졌다면 OnAugmentSelected를 놓쳤을 수 있다
        // (씬 재진입·오브젝트 토글). 현재 보유 상태를 한 번 확인해 그 구멍을 막는다.
        if (_glowing || !_waitForAugmentSelected) return;

        var augment = GameManager.TryGet(out var gm) ? gm.Augment : null;
        if (augment != null && augment.ActiveAugments.Count > 0) StartGlow();
    }

    private void HandleAugmentSelected(AugmentData _) => StartGlow();

    /// <summary>발광을 시작한다. 이미 켜져 있으면 아무 일도 하지 않는다.</summary>
    public void StartGlow() => _glowing = true;

    /// <summary>발광을 끈다(라운드 리셋 등에서 되돌릴 때).</summary>
    public void StopGlow()
    {
        _glowing = false;
        _flickerUntil = 0f;
        ApplyIntensity(0f);
    }

    private void CacheMaterials()
    {
        Renderer[] targets = _targets != null && _targets.Length > 0
            ? _targets
            : GetComponentsInChildren<Renderer>(includeInactive: true);

        var materials = new System.Collections.Generic.List<Material>();

        foreach (Renderer renderer in targets)
        {
            if (renderer == null) continue;

            // renderer.material은 접근할 때마다 인스턴스를 만든다 — 여기서 한 번만 확보해 재사용.
            Material material = renderer.material;
            if (material == null || !material.HasProperty(EmissionId)) continue;

            // 머티리얼에서 Emission 체크를 안 켜뒀어도 발광하도록 키워드를 직접 켠다.
            material.EnableKeyword("_EMISSION");
            materials.Add(material);
        }

        _materials = materials.ToArray();

        if (_materials.Length == 0)
            Debug.LogWarning("[NeonFlicker] Emission을 지원하는 Renderer가 없다 — 깜빡일 대상이 없다", this);
    }

    private void OnDestroy()
    {
        // renderer.material 접근으로 만들어진 인스턴스는 자동 정리되지 않는다.
        if (_materials == null) return;

        foreach (Material material in _materials)
            if (material != null) Destroy(material);
    }

    private void Update()
    {
        if (!_glowing) return; // 증강 선택 전 — Awake에서 꺼둔 상태 그대로 둔다
        if (_materials == null || _materials.Length == 0) return;

        ApplyIntensity(CurrentIntensity());
    }

    /// <summary>이번 프레임의 발광 세기. 깜빡이는 중이면 꺼진 값, 아니면 맥동값.</summary>
    private float CurrentIntensity()
    {
        if (Time.time < _flickerUntil)
            return _flickerIntensity;

        // 깜빡임 시작 판정. Time.deltaTime을 곱해 프레임 수와 무관하게 "초당 확률"이 되게 한다.
        if (_flickerChance > 0f && Random.value < _flickerChance * Time.deltaTime)
        {
            _flickerUntil = Time.time + _flickerDuration;
            return _flickerIntensity;
        }

        if (_pulseSpeed <= 0f) return _maxIntensity;

        // 사인파를 0~1로 옮겨 최소~최대 사이를 오가게 한다.
        float t = (Mathf.Sin(Time.time * _pulseSpeed * Mathf.PI * 2f + _phaseOffset) + 1f) * 0.5f;
        return Mathf.Lerp(_minIntensity, _maxIntensity, t);
    }

    private void ApplyIntensity(float intensity)
    {
        Color emission = _color * intensity;

        foreach (Material material in _materials)
            if (material != null) material.SetColor(EmissionId, emission);
    }

    private void OnValidate()
    {
        _minIntensity = Mathf.Max(0f, _minIntensity);
        _maxIntensity = Mathf.Max(_minIntensity, _maxIntensity);
        _flickerDuration = Mathf.Max(0f, _flickerDuration);
        _flickerIntensity = Mathf.Max(0f, _flickerIntensity);
    }
}
