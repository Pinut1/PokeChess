using TMPro;
using UnityEngine;

/// <summary>
/// 보드에 올린 유닛 수 / 레벨별 상한을 맵 위에 표시한다(롤체식 "5/6").
///
/// 월드 공간에 두는 표시라 Canvas가 아니라 3D TextMeshPro를 쓴다 —
/// 맵 텍스처 위에 눕혀두면 보드의 일부처럼 보인다.
/// (TMP_Text로 받으므로 UGUI TextMeshProUGUI를 꽂아도 그대로 동작한다.)
///
/// 값의 단일 소스는 BoardManager다. 폴링하지 않고 보드 구성이 바뀔 때만 다시 그린다.
/// </summary>
public class BoardCapacityLabel : MonoBehaviour
{
    [Tooltip("표시할 텍스트. 맵에 눕힐 거면 3D TextMeshPro를 쓴다.")]
    [SerializeField] private TMP_Text _text;

    [Tooltip("{0}=현재 배치 수, {1}=상한. 예: \"{0}/{1}\"")]
    [SerializeField] private string _format = "{0}/{1}";

    [Header("가득 찼을 때")]
    [Tooltip("상한에 도달하면 발광색을 바꿀지. 끄면 머티리얼 설정 그대로 둔다.")]
    [SerializeField] private bool _tintWhenFull = true;
    [Tooltip("가득 찼을 때의 Glow Color. 평상시 색은 머티리얼의 값을 그대로 쓴다.")]
    [SerializeField] private Color _fullGlowColor = new Color(1f, 0.35f, 0.25f);

    [Tooltip("자리가 남아 있는 동안에만 깜빡일 발광 펄스. 가득 차면 멈춰서 " +
             "'더 못 올린다'가 분명해진다. 같은 오브젝트에 있으면 비워둬도 자동으로 찾는다.")]
    [SerializeField] private TextGlowPulse _glowPulse;

    private int _shownCount = -1;
    private int _shownCap   = -1;
    private bool _visible = true; // 전투 중에는 false — 문자열만 비운다

    // 글자색 대신 머티리얼 발광색을 바꾼다. 공유본을 건드리면 같은 프리셋을 쓰는
    // 다른 텍스트까지 물들므로 인스턴스(fontMaterial)를 잡는다.
    private Material _material;
    private Color _normalGlowColor;
    private bool _glowReady;

    private void OnEnable()
    {
        if (_glowPulse == null) _glowPulse = GetComponent<TextGlowPulse>();

        CacheGlowMaterial();

        // 보드 위 기물 수가 바뀌는 경로들. 보드 내 이동은 수가 안 변하므로 구독하지 않는다.
        GameEvents.OnUnitPlaced   += HandleRosterChanged;
        GameEvents.OnUnitBenched  += HandleRosterChanged;
        GameEvents.OnUnitSold     += HandleRosterChanged;
        GameEvents.OnUnitCapChanged += HandleCapChanged;

        // 전투 중에는 배치를 못 바꾸니 표시할 이유가 없다 — 준비 단계로 돌아오면 다시 켠다.
        GameEvents.OnPhaseChanged += HandlePhaseChanged;

        Refresh();
    }

    private void OnDisable()
    {
        GameEvents.OnUnitPlaced   -= HandleRosterChanged;
        GameEvents.OnUnitBenched  -= HandleRosterChanged;
        GameEvents.OnUnitSold     -= HandleRosterChanged;
        GameEvents.OnUnitCapChanged -= HandleCapChanged;
        GameEvents.OnPhaseChanged   -= HandlePhaseChanged;
    }

    private void Start() => Refresh(); // 매니저 초기화가 OnEnable보다 늦을 수 있어 한 번 더.

    private void HandleRosterChanged(PokemonUnit _) => Refresh();
    private void HandleCapChanged(int _)           => Refresh();

    /// <summary>
    /// 배치를 바꿀 수 있는 쇼핑 단계에서만 표시한다.
    /// 오브젝트를 끄지 않고 문자열만 비우는 이유 — 이 컴포넌트가 같은 오브젝트에 붙어 있어서,
    /// 오브젝트를 끄면 OnDisable로 구독이 풀려 다시 켤 방법이 없어진다.
    /// </summary>
    private void HandlePhaseChanged(GamePhase phase)
    {
        _visible = phase == GamePhase.Shopping;
        Refresh();
    }

    private void Refresh()
    {
        if (_text == null) return;

        if (!_visible)
        {
            _text.text = string.Empty;
            if (_glowPulse != null) _glowPulse.enabled = false;

            // 다시 보일 때 값이 그대로여도 새로 쓰도록 캐시를 무효화한다.
            _shownCount = -1;
            _shownCap   = -1;
            return;
        }

        if (!GameManager.TryGet(out var gm) || gm.Board == null) return;

        int count = gm.Board.BoardUnitCount;
        int cap   = gm.Board.UnitCap;

        // 값이 그대로면 건드리지 않는다 — TMP는 같은 문자열을 넣어도 메시를 다시 만든다.
        if (count == _shownCount && cap == _shownCap) return;

        _shownCount = count;
        _shownCap   = cap;

        _text.text = string.Format(_format, count, cap);

        bool isFull = count >= cap;

        if (_tintWhenFull && _glowReady)
            _material.SetColor(ShaderUtilities.ID_GlowColor,
                               isFull ? _fullGlowColor : _normalGlowColor);

        // 자리가 남았을 때만 깜빡인다. 끄면 펄스가 스스로 최소 밝기로 가라앉는다.
        if (_glowPulse != null)
            _glowPulse.enabled = !isFull;
    }

    /// <summary>발광색을 바꿀 머티리얼 인스턴스와 평상시 색을 확보한다.</summary>
    private void CacheGlowMaterial()
    {
        _glowReady = false;
        if (_text == null || !_tintWhenFull) return;

        _material = _text.fontMaterial; // 접근하는 순간 인스턴스가 만들어진다
        if (_material == null) return;

        if (!_material.IsKeywordEnabled("GLOW_ON"))
        {
            Debug.LogWarning(
                $"[BoardCapacityLabel] '{name}'의 머티리얼에 Glow가 꺼져 있어 " +
                "가득 찼을 때 색이 바뀌지 않습니다. 머티리얼 인스펙터의 Glow를 켜세요.",
                this);
            return;
        }

        _normalGlowColor = _material.GetColor(ShaderUtilities.ID_GlowColor);
        _glowReady = true;
    }
}
