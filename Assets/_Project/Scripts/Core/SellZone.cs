using UnityEngine;

/// <summary>
/// 유닛 드롭 영역.
/// 판매 모드에서는 BoardManager.SellUnit을 호출하고,
/// 통신교환 모드에서는 NetworkManager.SendTradeUnit을 호출한다.
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

    private Renderer _renderer;
    private Color _defaultColor;

    private void Awake()
    {
        _renderer = GetComponentInChildren<Renderer>();

        if (_renderer != null)
            _defaultColor = _renderer.material.color;
    }

    public void OnHoverEnter()
    {
        if (_renderer == null)
            return;

        _renderer.material.color =
            _zoneType == DropZoneType.Trade
                ? _tradeHoverColor
                : _sellHoverColor;
    }

    public void OnHoverExit()
    {
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
        var board =
            GameManager.Instance != null
                ? GameManager.Instance.Board
                : null;

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
        // 현재 NetworkManager 구현은 보낸 유닛을
        // 성공 ACK 후 벤치에서 제거하므로 벤치 유닛만 허용한다.
        if (unit.isOnBoard)
        {
            Debug.LogWarning(
                "[TradeZone] 보드 위 유닛은 통신교환할 수 없습니다."
            );

            GameEvents.TradeRejected();
            return;
        }

        var network =
            GameManager.Instance != null
                ? GameManager.Instance.Network
                : null;

        if (network == null)
        {
            Debug.LogWarning(
                "[TradeZone] NetworkManager가 없어 통신교환할 수 없습니다."
            );

            GameEvents.TradeRejected();
            return;
        }

        network.SendTradeUnit(unit);
    }
}