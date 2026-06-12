using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전장(Board) 헥스 타일을 생성하고, 타일/벤치 위에 놓인 유닛들의 배치를 통제하는 매니저.
/// 보드 상태(_battleField)와 벤치 상태(_bench)의 단일 진실 공급원.
/// 다른 매니저는 GameEvents 트리거 수신 후 GetUnitsOnBoard()/GetUnitsInBench()로 pull.
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

    // 벤치 슬롯. 인덱스 = 슬롯 번호. null = 빈 슬롯.
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
                // TryPlaceUnit은 bool을 반환하므로 Action으로 받기 위해 람다로 감싼다(반환값 무시).
                newTile.Initialize(coords, (unit, c) => TryPlaceUnit(unit, c), $"Tile_{row}_{col}");

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

    // ──────────────────────────────────────────
    // 조회 API (pull)
    // ──────────────────────────────────────────

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

    /// <summary>현재 벤치에 있는 유닛 목록을 반환합니다.</summary>
    public List<PokemonUnit> GetUnitsInBench()
    {
        List<PokemonUnit> units = new List<PokemonUnit>();
        foreach (PokemonUnit unit in _bench)
        {
            if (unit != null)
                units.Add(unit);
        }
        return units;
    }

    /// <summary>보드 위 빈 좌표→유닛 매핑(읽기 전용 스냅샷). 전투 스냅샷 등에서 위치가 필요할 때 사용.</summary>
    public IReadOnlyDictionary<HexCoords, PokemonUnit> GetBoardSnapshot() => _battleField;

    // ──────────────────────────────────────────
    // 보드 배치
    // ──────────────────────────────────────────

    /// <summary>
    /// 타일에서 마우스 드롭이 일어났을 때 콜백으로 호출되거나, 외부(샵 등)에서 직접 호출.
    /// 빈 타일이면 이동/배치, 점유된 타일이면 스왑. 성공 시 true.
    /// </summary>
    public bool TryPlaceUnit(PokemonUnit unit, HexCoords targetCoords)
    {
        if (unit == null) return false;
        if (!_battleField.ContainsKey(targetCoords)) return false;

        PokemonUnit occupant = _battleField[targetCoords];
        if (occupant == unit) return true; // 자기 자리에 그대로 드롭 — 변화 없음

        // 들어오는 유닛이 원래 보드 위에 있었다면 그 좌표를 기억(스왑/비우기용)
        bool fromBoard = TryFindBoardCoords(unit, out HexCoords fromCoords);

        if (occupant == null)
        {
            // 빈 타일: 단순 이동/신규 배치
            if (fromBoard) _battleField[fromCoords] = null;
            else RemoveFromBenchByRef(unit); // 벤치에서 온 경우 벤치 슬롯 비움

            _battleField[targetCoords] = unit;
            unit.isOnBoard = true;
        }
        else if (fromBoard)
        {
            // 보드 ↔ 보드 스왑 (둘 다 보드에 남음)
            _battleField[fromCoords] = occupant;
            _battleField[targetCoords] = unit;
        }
        else
        {
            // 벤치 → 점유된 보드 타일: 벤치 슬롯과 보드 유닛 교체
            int benchSlot = FindBenchSlot(unit);
            _battleField[targetCoords] = unit;
            unit.isOnBoard = true;

            if (benchSlot >= 0)
            {
                _bench[benchSlot] = occupant;
                occupant.isOnBoard = false;
            }
        }

        GameEvents.UnitPlaced(unit);
        return true;
    }

    // ──────────────────────────────────────────
    // 벤치 배치
    // ──────────────────────────────────────────

    /// <summary>지정한 슬롯에 유닛을 놓습니다. 슬롯이 비어있어야 성공(같은 유닛은 멱등).</summary>
    public bool TryPlaceInBench(PokemonUnit unit, int slot)
    {
        if (unit == null) return false;
        if (slot < 0 || slot >= _benchSize) return false;

        PokemonUnit occupant = _bench[slot];
        if (occupant == unit) return true;        // 같은 자리 멱등
        if (occupant != null) return false;       // 점유됨 — 스왑은 호출측 책임

        RemoveUnitFromCurrentLocation(unit);
        _bench[slot] = unit;
        unit.isOnBoard = false;

        GameEvents.UnitBenched(unit);
        return true;
    }

    /// <summary>빈 슬롯 아무 곳에나 유닛을 놓습니다(샵 구매 등). 벤치가 가득 차면 실패.</summary>
    public bool TryPlaceInBench(PokemonUnit unit)
    {
        int slot = FirstEmptyBenchSlot();
        if (slot < 0)
        {
            Debug.Log("[BoardManager] 벤치가 가득 찼습니다.");
            return false;
        }
        return TryPlaceInBench(unit, slot);
    }

    /// <summary>지정 슬롯을 비웁니다(판매/제거 시). 반환된 유닛 파괴 등은 호출측 책임.</summary>
    public PokemonUnit RemoveFromBench(int slot)
    {
        if (slot < 0 || slot >= _benchSize) return null;
        PokemonUnit removed = _bench[slot];
        _bench[slot] = null;
        return removed;
    }

    /// <summary>벤치에 빈 슬롯이 있는지.</summary>
    public bool HasBenchSpace() => FirstEmptyBenchSlot() >= 0;

    // ──────────────────────────────────────────
    // 내부 헬퍼
    // ──────────────────────────────────────────

    /// <summary>보드에서 유닛의 현재 좌표를 역으로 찾음.</summary>
    private bool TryFindBoardCoords(PokemonUnit unit, out HexCoords coords)
    {
        foreach (var kv in _battleField)
        {
            if (kv.Value == unit)
            {
                coords = kv.Key;
                return true;
            }
        }
        coords = default;
        return false;
    }

    /// <summary>벤치에서 유닛의 슬롯 인덱스를 찾음. 없으면 -1.</summary>
    private int FindBenchSlot(PokemonUnit unit)
    {
        for (int i = 0; i < _bench.Length; i++)
            if (_bench[i] == unit) return i;
        return -1;
    }

    private int FirstEmptyBenchSlot()
    {
        for (int i = 0; i < _bench.Length; i++)
            if (_bench[i] == null) return i;
        return -1;
    }

    /// <summary>벤치에서 해당 유닛 참조를 찾아 비움(있을 때만).</summary>
    private void RemoveFromBenchByRef(PokemonUnit unit)
    {
        int slot = FindBenchSlot(unit);
        if (slot >= 0) _bench[slot] = null;
    }

    /// <summary>유닛이 현재 어디(보드/벤치)에 있든 그 자리를 비움.</summary>
    private void RemoveUnitFromCurrentLocation(PokemonUnit unit)
    {
        if (TryFindBoardCoords(unit, out HexCoords coords))
            _battleField[coords] = null;
        else
            RemoveFromBenchByRef(unit);
    }
}
