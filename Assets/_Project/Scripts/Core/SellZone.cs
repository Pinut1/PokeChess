using UnityEngine;

/// <summary>
/// 유닛 드롭 영역.
/// 판매 모드에서는 BoardManager.SellUnit을 호출하고,
/// 통신교환 모드에서는 유닛을 드롭하는 즉시
/// NetworkManager.SendTradeUnit을 호출한다.
/// HexTile / BenchTile과 동일한 IDropTarget 패턴을 사용한다.
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
    [SerializeField]
    private Color _sellHoverColor =
        new Color(1f, 0.3f, 0.3f);

    [SerializeField]
    private Color _tradeHoverColor =
        new Color(0.3f, 0.9f, 1f);

    // 강조 표시 방식(화면 발광 등)은 IHoverHighlight 구현체가 담당한다.
    // 자식에 하나라도 있으면 그쪽에 맡기고, 없으면 예전처럼 첫 Renderer의 색을 직접 바꾼다.
    private IHoverHighlight _highlight;

    private Renderer _renderer;
    private Color _defaultColor;

    private void Awake()
    {
        _highlight = GetComponentInChildren<IHoverHighlight>();

        // 전용 강조가 있으면 원본 머티리얼을 건드리지 않는다 — 색 캐시도 필요 없다.
        if (_highlight != null) return;

        _renderer = GetComponentInChildren<Renderer>();

        if (_renderer != null)
            _defaultColor = _renderer.material.color;
    }

    /// <summary>이 존의 강조 색(판매=빨강 / 교환=하늘).</summary>
    private Color HoverColor =>
        _zoneType == DropZoneType.Trade
            ? _tradeHoverColor
            : _sellHoverColor;

    public void OnHoverEnter()
    {
        if (_highlight != null)
        {
            _highlight.Show(HoverColor);
            return;
        }

        if (_renderer == null)
            return;

        _renderer.material.color = HoverColor;
    }

    public void OnHoverExit()
    {
        if (_highlight != null)
        {
            _highlight.Hide();
            return;
        }

        if (_renderer != null)
            _renderer.material.color = _defaultColor;
    }

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

        // 파트너 전체화면 관전 중엔 화면 뒤 실제 통신기가 클릭되지 않도록 차단한다. OnMouseDown은
        // UnitDragController와 별개로 Main Camera 기준 자체 히트테스트를 쓰므로 관전 오버레이(Canvas
        // RawImage)로는 막히지 않는다 — UnitDragController와 같은 기준(UIManager.IsPartnerSpectateExpanded)
        // 으로 관전 상태 판단을 하나로 통일한다(2026-08 입력 관통 버그 대응).
        if (GameManager.TryGet(out var uiGm) && uiGm.UI != null && uiGm.UI.IsPartnerSpectateExpanded)
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


}