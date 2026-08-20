using UnityEngine;

/// <summary>
/// 유닛 드롭 영역.
/// 판매 모드에서는 BoardManager.SellUnit을 호출하고,
/// 통신교환 모드에서는 유닛을 드롭하는 즉시
/// NetworkManager.SendTradeUnit을 호출한다.
/// HexTile / BenchTile과 동일한 IDropTarget 패턴을 사용한다.
///
/// 표시는 세 갈래다.
///   · 드래그 호버(유닛을 들고 올렸을 때) — 판매=빨강, 교환=하늘. "여기 놓으면 이렇게 된다"는 예고다
///   · 커서 호버(그냥 올렸을 때) — 흰색. 놓을 게 없으니 예고가 아니라 "여기 뭔가 있다" 정도의 표시다
///   · 도착 대기(통신기 전용) — 파트너가 보낸 유닛이 대기 중인 동안 계속 켜둔다
///   · 설명 툴팁 — 커서를 올리면 "뭐 하는 물건인지"를 글로 띄운다
/// 어느 색을 칠할지는 <see cref="RefreshHighlight"/> 한 곳에서만 정한다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SellZone : MonoBehaviour, IDropTarget
{
    private enum DropZoneType
    {
        Sell,
        Trade
    }

    [Header("드롭 존 설정")]
    [SerializeField] private DropZoneType _zoneType = DropZoneType.Sell;

    [Header("호버 표시")]
    [Tooltip("유닛을 드래그해 올렸을 때의 판매 존 색.")]
    [SerializeField]
    private Color _sellHoverColor =
        new Color(1f, 0.3f, 0.3f);

    [Tooltip("유닛을 드래그해 올렸을 때의 통신기 색.")]
    [SerializeField]
    private Color _tradeHoverColor =
        new Color(0.3f, 0.9f, 1f);

    [Tooltip("유닛을 들지 않고 커서만 올렸을 때의 색. 위의 드래그 색과 구분한다 — " +
             "놓을 유닛이 없는데 같은 색을 쓰면 '지금 놓을 수 있다'는 신호가 흐려진다.")]
    [SerializeField]
    private Color _pointerHoverColor = Color.white;

    [Header("도착 알림 (통신기 전용)")]
    [Tooltip("파트너가 보낸 유닛이 대기 중인 동안 켜둘 색. 커서를 올리면 위의 교환 색이 잠시 덮는다.")]
    [SerializeField]
    private Color _tradePendingColor =
        new Color(1f, 0.8f, 0.2f);

    [Tooltip("끄면 유닛이 도착해도 색이 바뀌지 않는다(도착 연출을 다른 쪽에 맡길 때).")]
    [SerializeField] private bool _highlightWhenTradePending = true;

    [Header("설명 창")]
    [Tooltip("통신기 전용 안내창(TradeMachinePanel_Pf). 물려 두면 아래 글자 툴팁 대신 이쪽이 열린다 — " +
             "전송 가능 여부·통신진화 목록·수령 버튼이 들어 있는 정식 창이다.\n" +
             "창은 커서가 아니라 이 통신기 위에 고정되어 뜬다(높이 조절은 창 쪽 '자리' 항목).")]
    [SerializeField] private TradeMachinePanelUI _tradePanel;

    [Tooltip("전용 창이 없을 때 쓸 글자 툴팁 문구. 비워두면 존 종류에 맞는 기본 문구가 나온다.\n" +
             "통신기는 아래에 '도착한 유닛 N마리'가 자동으로 덧붙는다.")]
    [TextArea(1, 4)]
    [SerializeField] private string _tooltipText;

    [Tooltip("글자 툴팁의 설명창 컨트롤러. 비워두면 씬에서 찾지만, 씬에 설명창이 둘 이상이면 직접 물려야 한다.")]
    [SerializeField] private RoleTooltipController _tooltip;

    // 강조 표시 방식(화면 발광 등)은 IHoverHighlight 구현체가 담당한다.
    // 자식에 하나라도 있으면 그쪽에 맡기고, 없으면 예전처럼 첫 Renderer의 색을 직접 바꾼다.
    private IHoverHighlight _highlight;

    private Renderer _renderer;
    private Color _defaultColor;

    // 호버는 두 갈래로 들어온다 — 드래그 중에는 UnitDragController가 OnHoverEnter/Exit를 부르고,
    // 그냥 커서만 올렸을 때는 Unity가 OnMouseEnter/Exit를 부른다. 한쪽이 켠 강조를 다른 쪽 Exit가
    // 끄지 않도록 따로 들고 OR로 합친다.
    private bool _dragHovered;
    private bool _pointerHovered;

    /// <summary>지금 내 통신기에 도착해 대기 중인 유닛 수(GameEvents.OnTradeQueueChanged 기준).</summary>
    private int _pendingTradeCount;

    private bool IsHovered => _dragHovered || _pointerHovered;

    private bool IsTradePending =>
        _zoneType == DropZoneType.Trade &&
        _highlightWhenTradePending &&
        _pendingTradeCount > 0;

    private void Awake()
    {
        _highlight = GetComponentInChildren<IHoverHighlight>();

        // 전용 창은 스스로를 끄지 않는다(그러면 처음 열릴 때 자기를 다시 꺼버린다) — 켠 채로
        // 저장된 씬을 대비해 여기서 한 번 닫아준다.
        if (_tradePanel != null)
        {
            // 창이 내 위에 붙어 뜬다. 창 쪽 인스펙터에 앵커를 직접 물렸으면 그쪽이 우선이다.
            _tradePanel.SetOwnerAnchor(transform);
            _tradePanel.CloseOnStartup();
        }

        // 글자 툴팁이 필요한 존만 찾는다 — 전용 창이 물려 있으면 쓰지 않고, 설명창이 씬에 여럿이면
        // 경고가 뜨는데 툴팁을 안 쓰는 판매 존까지 매번 경고를 남길 이유가 없다.
        if (_tradePanel == null && _tooltip == null && !string.IsNullOrWhiteSpace(BuildTooltipText()))
            _tooltip = RoleTooltipController.FindInScene(this);

        // 전용 강조가 있으면 원본 머티리얼을 건드리지 않는다 — 색 캐시도 필요 없다.
        if (_highlight != null) return;

        _renderer = GetComponentInChildren<Renderer>();

        if (_renderer != null)
            _defaultColor = _renderer.material.color;
    }

    private void OnEnable()
    {
        if (_zoneType != DropZoneType.Trade) return;

        GameEvents.OnTradeQueueChanged += HandleTradeQueueChanged;

        // 구독 전에 이미 도착해 있던 유닛을 놓치지 않도록 현재 값으로 맞춘다
        // (씬 로드 순서에 따라 이벤트가 먼저 지나갔을 수 있다).
        NetworkManager network =
            GameManager.TryGet(out var gm) ? gm.Network : null;

        _pendingTradeCount = network != null ? network.PendingTradeUnitCount : 0;
        RefreshHighlight();
    }

    private void OnDisable()
    {
        if (_zoneType == DropZoneType.Trade)
            GameEvents.OnTradeQueueChanged -= HandleTradeQueueChanged;

        // 커서를 올린 채 꺼지면 Exit이 오지 않아 설명창만 화면에 남는다.
        _dragHovered = false;
        _pointerHovered = false;
        HideTooltip();
    }

    /// <summary>이 존의 강조 색(판매=빨강 / 교환=하늘).</summary>
    /// <summary>드래그로 유닛을 올렸을 때 칠할 색. 존 종류에 따라 다르다.</summary>
    private Color DragHoverColor =>
        _zoneType == DropZoneType.Trade
            ? _tradeHoverColor
            : _sellHoverColor;

    // ─────────────────────────────────────────
    // 강조 표시
    // ─────────────────────────────────────────

    /// <summary>
    /// 지금 상태에 맞는 색을 칠한다. 우선순위는 드래그 호버 &gt; 커서 호버 &gt; 도착 대기 &gt; 평소.
    /// 켜고 끄는 판단을 여기 하나로 모아, 호버를 벗어났을 때 도착 알림이 그대로 되살아나게 한다.
    /// </summary>
    private void RefreshHighlight()
    {
        // 드래그가 먼저다 — 유닛을 들고 있을 때만 "놓으면 이렇게 된다"를 색으로 예고한다.
        if (_dragHovered)
        {
            ApplyColor(DragHoverColor);
            return;
        }

        if (_pointerHovered)
        {
            ApplyColor(_pointerHoverColor);
            return;
        }

        if (IsTradePending)
        {
            ApplyColor(_tradePendingColor);
            return;
        }

        ClearColor();
    }

    private void ApplyColor(Color color)
    {
        if (_highlight != null)
        {
            _highlight.Show(color);
            return;
        }

        if (_renderer != null)
            _renderer.material.color = color;
    }

    private void ClearColor()
    {
        if (_highlight != null)
        {
            _highlight.Hide();
            return;
        }

        if (_renderer != null)
            _renderer.material.color = _defaultColor;
    }

    /// <summary>통신기 대기열 수가 바뀌었다. 색과(열려 있다면) 설명창 문구를 함께 갱신한다.</summary>
    private void HandleTradeQueueChanged(int count)
    {
        _pendingTradeCount = Mathf.Max(0, count);

        RefreshHighlight();

        // 설명창을 띄운 채로 유닛이 도착·수령되면 "도착한 유닛 N마리" 줄이 옛 숫자로 남는다.
        if (IsHovered) ShowTooltip();
    }

    public void OnHoverEnter()
    {
        _dragHovered = true;
        RefreshHighlight();
        ShowTooltip();
    }

    public void OnHoverExit()
    {
        _dragHovered = false;
        RefreshHighlight();

        if (!IsHovered) HideTooltip();
    }

    // ─────────────────────────────────────────
    // 설명 툴팁
    //
    // 드래그 중에는 위의 OnHoverEnter/Exit가, 그냥 커서만 올렸을 때는 아래 OnMouseEnter/Exit가
    // 부른다. 드래그 중에는 들고 있는 유닛이 커서 아래를 가려 OnMouseEnter가 오지 않으므로
    // 두 갈래가 다 필요하다.
    // ─────────────────────────────────────────

    private void OnMouseEnter()
    {
        // 파트너 전체화면 관전 중에는 화면 뒤 오브젝트를 건드릴 수 없다 — OnMouseDown과 같은 기준.
        // UI 위에 커서가 있을 때도 마찬가지(OnMouseDown의 IsPointerOverGameObject 가드와 같은 이유
        // — 견본덱 창 등이 통신기를 가리고 있어도 물리 레이캐스트는 그대로 들어와 안내창이 UI를
        // 뚫고 뜨는 버그가 있었다, 2026-08 QA 리포트).
        if (IsSpectateBlocking() || PointerUtil.IsOverUI()) return;

        _pointerHovered = true;
        RefreshHighlight();
        ShowTooltip();
    }

    /// <summary>
    /// 관전·UI 차단이 걸려 있는 동안엔 OnMouseEnter가 상태를 세우지 않는다. 그게 풀렸을 때 커서가
    /// 이미 올라와 있으면 Unity는 Enter를 다시 보내지 않으므로(이미 '진입'으로 보고 있다) 여기서
    /// 따라잡는다. 커서가 올라와 있는 동안만 도는 데다 대부분 bool 하나 보고 빠져나간다.
    ///
    /// 반대 순서(호버가 먼저 시작돼 안내창이 이미 떠 있는 상태에서, 커서를 안 뗀 채로 나중에
    /// 관전이 시작되거나 UI가 그 위를 덮는 경우)도 여기서 잡는다 — OnMouseExit은 순수 물리
    /// 레이캐스트라 관전 오버레이·UI에 가려도 오지 않으므로, 이 매 프레임 폴링이 유일한 탈출구다
    /// (2026-08 QA 리포트 재리뷰 지적 — _pointerHovered만 보고 조기 리턴하면 이미 뜬 안내창을
    /// 다시 닫을 기회가 없었다).
    ///
    /// 닫을지는 OnMouseExit과 똑같이 IsHovered(=드래그 호버 포함)로 판단한다(2026-08 재재리뷰
    /// 지적) — 이 물리 레이캐스트 이벤트는 원래 유닛을 드는 손이 커서를 가려 드래그 중엔 안
    /// 온다는 전제지만, 그 전제가 어떤 이유로든 깨졌을 때도 드래그 강조가 실수로 같이 꺼지지
    /// 않도록 OnMouseExit과 같은 기준을 그대로 맞춘다.
    /// </summary>
    private void OnMouseOver()
    {
        if (IsSpectateBlocking() || PointerUtil.IsOverUI())
        {
            if (_pointerHovered) ClearPointerHover();
            return;
        }

        if (_pointerHovered) return;

        _pointerHovered = true;
        RefreshHighlight();
        ShowTooltip();
    }

    private void OnMouseExit() => ClearPointerHover();

    /// <summary>
    /// 커서 호버 상태를 끈다 — OnMouseExit(실제로 커서가 벗어남)과 OnMouseOver의 차단 분기(관전·UI가
    /// 가로막아 더 이상 호버로 인정 못 함)가 똑같이 쓴다(2026-08 재재리뷰 지적, 중복 제거). 닫을지는
    /// IsHovered(=드래그 호버 포함)로 판단해, 드래그 중엔 툴팁을 그대로 열어둔다.
    /// </summary>
    private void ClearPointerHover()
    {
        _pointerHovered = false;
        RefreshHighlight();

        if (!IsHovered) HideTooltip();
    }

    private void ShowTooltip()
    {
        // 전용 창이 있으면 그쪽이 우선이다. 창은 스스로 상태를 읽어 그리므로 문구를 넘기지 않는다.
        // zoneType까지 보는 이유 — 판매존에 실수로 물려도 통신기 창이 뜨지 않게 한다(OnDropUnit·
        // OnMouseDown과 같은 기준). 에디터 도구는 Trade존에만 배선하므로 수동 오배선 대비다.
        if (_tradePanel != null && _zoneType == DropZoneType.Trade)
        {
            // 통신기가 둘 이상인 씬을 대비해 열 때마다 다시 넘긴다 — 지금 커서를 올린 쪽에 붙어야 한다.
            _tradePanel.SetOwnerAnchor(transform);
            _tradePanel.SetOwnerHovered(true);
            return;
        }

        if (_tooltip == null) return;

        string text = BuildTooltipText();
        if (string.IsNullOrWhiteSpace(text)) return;

        // 소유자를 자신으로 넘긴다 — 다른 대상으로 커서가 옮겨간 뒤 도착하는 Exit이
        // 남의 설명창을 끄지 않는다(TextTooltipTrigger와 같은 규약).
        _tooltip.Show(this, text);
    }

    private void HideTooltip()
    {
        // 커서가 통신기를 벗어났다고 바로 닫지 않는다 — 창 안 버튼을 누르러 가는 중일 수 있어
        // 실제로 닫을지는 창이 판단한다(창 위에 커서가 있으면 열어 둔다).
        if (_tradePanel != null && _zoneType == DropZoneType.Trade)
        {
            _tradePanel.SetOwnerHovered(false);
            return;
        }

        if (_tooltip != null) _tooltip.Hide(this);
    }

    /// <summary>인스펙터 문구(없으면 존 종류별 기본 문구) + 통신기의 도착 대기 줄.</summary>
    private string BuildTooltipText()
    {
        string text = string.IsNullOrWhiteSpace(_tooltipText)
            ? DefaultTooltipText()
            : _tooltipText;

        if (_zoneType != DropZoneType.Trade) return text;

        return _pendingTradeCount > 0
            ? $"{text}\n도착한 유닛 {_pendingTradeCount}마리 (클릭해서 수령)"
            : $"{text}\n도착한 유닛 없음";
    }

    /// <summary>
    /// 기본 문구는 통신기에만 준다. 판매 존은 문구를 적어 넣었을 때만 뜬다 —
    /// 원래 설명창이 없던 자리에 마음대로 띄우지 않기 위해서다.
    /// </summary>
    private string DefaultTooltipText() =>
        _zoneType == DropZoneType.Trade
            ? "통신교환기\n유닛을 올려두면 파트너에게 전송(라운드당 1회)\n클릭하면 도착한 유닛을 벤치로 수령"
            : "";

    // ─────────────────────────────────────────
    // 드롭 / 클릭
    // ─────────────────────────────────────────

    public void OnDropUnit(PokemonUnit unit)
    {
        if (unit == null || unit.data == null)
            return;

        switch (_zoneType)
        {
            case DropZoneType.Sell:
                SellUnit(unit);
                break;

            case DropZoneType.Trade:
                TradeUnit(unit);
                break;
        }
    }

    private static void SellUnit(PokemonUnit unit)
    {
        BoardManager board =
            GameManager.TryGet(out var gm) ? gm.Board : null;

        if (board == null)
        {
            Debug.LogWarning(
                "[DropZone] BoardManager가 없어 판매할 수 없습니다."
            );
            return;
        }

        board.SellUnit(unit);
    }

    private static void TradeUnit(PokemonUnit unit)
    {
        NetworkManager network =
            GameManager.TryGet(out var gm) ? gm.Network : null;

        if (network == null)
        {
            Debug.LogWarning(
                "[TradeZone] NetworkManager가 없어 통신교환할 수 없습니다."
            );

            GameEvents.TradeRejected();
            return;
        }

        Debug.Log(
            $"[TradeZone] {unit.data.pokemonName} ★{unit.starLevel} 즉시 전송 요청"
        );

        network.SendTradeUnit(unit);
    }

    private void OnMouseDown()
    {
        if (_zoneType != DropZoneType.Trade)
            return;

        if (IsSpectateBlocking())
            return;

        // 안내창이 통신기 위에 겹쳐 뜬다. OnMouseDown은 물리 레이캐스트라 UI에 가려도 그대로
        // 들어오는데, 창의 [포켓몬 받기] 버튼과 이 클릭이 같은 TryReceiveNextTradeUnit을 부르므로
        // 가드가 없으면 한 번 눌러 두 마리가 수령된다.
        if (PointerUtil.IsOverUI())
            return;

        NetworkManager network =
            GameManager.TryGet(out var gm) ? gm.Network : null;

        if (network == null)
        {
            Debug.LogWarning(
                "[TradeZone] NetworkManager가 없어 유닛을 수령할 수 없습니다."
            );
            return;
        }

        network.TryReceiveNextTradeUnit();
    }

    /// <summary>
    /// 파트너 전체화면 관전 중엔 화면 뒤 실제 통신기가 클릭되지 않도록 차단한다. OnMouseDown/OnMouseEnter는
    /// UnitDragController와 별개로 Main Camera 기준 자체 히트테스트를 쓰므로 관전 오버레이(Canvas
    /// RawImage)로는 막히지 않는다 — UnitDragController와 같은 기준(UIManager.IsPartnerSpectateExpanded)
    /// 으로 관전 상태 판단을 하나로 통일한다(2026-08 입력 관통 버그 대응).
    /// </summary>
    private static bool IsSpectateBlocking() =>
        GameManager.TryGet(out var gm) &&
        gm.UI != null &&
        gm.UI.IsPartnerSpectateExpanded;
}
