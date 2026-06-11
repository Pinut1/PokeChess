using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전장(Board) 헥스 타일을 생성하고, 타일 위에 놓인 유닛들의 배치를 통제하는 매니저.
/// </summary>
public class BoardManager : MonoBehaviour
{
    [Header("Board Settings")]
    [SerializeField] private HexTile _tilePrefab;
    [SerializeField] private float _hexSize = 1.0f; // 인스펙터에서 수정 가능
    [SerializeField] private int _cols = 7; // 가로 (TFT 기준 7)
    [SerializeField] private int _rows = 4; // 세로 (TFT 기준 4)

    [Header("Bench Settings")]
    [SerializeField] private int _benchSize = 9; // TFT 기준 벤치 슬롯 수

    // 💡 금고: 타일의 논리적 위치와 그 위 앉아있는 유닛을 매핑하는 딕셔너리
    private Dictionary<HexCoords, PokemonUnit> _battleField = new Dictionary<HexCoords, PokemonUnit>();

    // TODO(UnitField): 벤치 슬롯. 1차원 배열로 시작 (인덱스 = 슬롯 번호).
    // 다음 작업: TryPlaceInBench/RemoveFromBench/GetUnitsInBench 구현 + BenchTile(IDropTarget) 연동
    private PokemonUnit[] _bench;

    private void Awake()
    {
        _bench = new PokemonUnit[_benchSize];
    }

    private void Start()
    {
        GenerateBoard();
    }

    /// <summary>
    /// 씬에 육각형 맵을 찍어내고, 카메라 정중앙(0,0,0)에 예쁘게 정렬합니다.
    /// </summary>
    public void GenerateBoard()
    {
        if (_tilePrefab == null)
        {
            Debug.LogError("[BoardManager] HexTile 프리팹이 연결되지 않았습니다!");
            return;
        }

        _battleField.Clear();

        // 1. 쟁반(Anchor) 생성
        GameObject boardAnchor = new GameObject("BoardAnchor");
        boardAnchor.transform.SetParent(this.transform);

        Vector3 sumPosition = Vector3.zero;
        int totalTiles = 0;

        // 2. 타일 생성 루프
        for (int row = 0; row < _rows; row++)
        {
            // 하이어라키 정리를 위한 행(Row) 폴더 생성
            GameObject rowFolder = new GameObject($"Row_{row}");
            rowFolder.transform.SetParent(boardAnchor.transform);

            for (int col = 0; col < _cols; col++)
            {
                // 직사각형(col, row)을 헥사곤 큐브(q, r) 좌표로 변환 (Flat-top 기준)
                int q = col;
                int r = row - Mathf.FloorToInt(col / 2f);
                HexCoords coords = new HexCoords(q, r);

                // 프리팹 생성 및 위치 지정
                HexTile newTile = Instantiate(_tilePrefab, rowFolder.transform);
                Vector3 worldPos = coords.ToWorldPosition(_hexSize);
                newTile.transform.localPosition = worldPos;

                // 타일에게 콜백 무전기 주입! (이름도 예쁘게 세팅)
                newTile.Initialize(coords, TryPlaceUnit, $"Tile_{row}_{col}");

                // 금고에 빈 타일 등록
                _battleField.Add(coords, null);

                // 평균 위치 계산을 위해 누적
                sumPosition += worldPos;
                totalTiles++;
            }
        }

        // 3. 중앙 정렬 (Centering)
        // 타일들의 평균 무게중심(Center)을 구한 뒤, 쟁반 전체를 그 반대 방향으로 밀어줍니다.
        if (totalTiles > 0)
        {
            Vector3 centerOffset = sumPosition / totalTiles;
            boardAnchor.transform.position = -centerOffset;
        }
    }

    /// <summary>
    /// 현재 보드 위에 배치된 유닛 목록을 반환합니다.
    /// SynergyManager 등이 OnUnitPlaced/OnUnitBenched/OnUnitSold 트리거 수신 시 이 API로 직접 조회(pull)합니다.
    /// </summary>
    public List<PokemonUnit> GetUnitsOnBoard()
    {
        List<PokemonUnit> units = new List<PokemonUnit>();
        foreach (PokemonUnit unit in _battleField.Values)
        {
            if (unit != null)
                units.Add(unit);
        }
        return units;
    }

    /// <summary>
    /// 타일에서 마우스 드롭이 일어났을 때 콜백(Action)으로 호출되는 함수입니다.
    /// </summary>
    public void TryPlaceUnit(PokemonUnit unit, HexCoords targetCoords)
    {
        // 아직 PlayerController나 PokemonUnit 더미가 없으므로 로그만 찍습니다.
        Debug.Log($"[BoardManager] 유닛을 {targetCoords} 좌표에 배치하려고 시도합니다!");

        // 1. targetCoords가 딕셔너리에 존재하는지 확인
        if (!_battleField.ContainsKey(targetCoords)) return;

        // 2. 이미 누군가 있는지 확인 (추후 Swap 로직 연결)
        PokemonUnit occupant = _battleField[targetCoords];
        if (occupant != null)
        {
            Debug.Log($"[BoardManager] 해당 타일에는 이미 누군가 있습니다. (스왑 필요)");
            // Swap 로직 추가 예정
        }
        else
        {
            Debug.Log($"[BoardManager] 빈 타일입니다. 유닛을 배치합니다.");
            _battleField[targetCoords] = unit;
            GameEvents.UnitPlaced(unit);
        }
    }
}
