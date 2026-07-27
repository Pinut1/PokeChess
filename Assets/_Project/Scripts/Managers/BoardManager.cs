using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전장(Board) 헥스 타일을 생성하고, 타일/벤치 위에 놓인 유닛들의 배치를 통제하는 매니저.
/// 보드 상태(_battleField)와 벤치 상태(_bench)의 단일 진실 공급원.
/// 다른 매니저는 GameEvents 트리거 수신 후 GetUnitsOnBoard()/GetUnitsInBench()로 pull.
/// </summary>
public class BoardManager : MonoBehaviour
{
    [Header("Board Prefab")]
    [Tooltip("전장 한 칸에 사용할 HexTile 프리팹입니다. 아트 교체 시 이 필드만 변경합니다.")]
    [SerializeField] private HexTile _tilePrefab;

    [Header("Board Layout")]
    [Tooltip("헥스 좌표 간격의 기준 크기입니다.")]
    [SerializeField] private float _hexSize = 1.0f;
    [Tooltip("전장 가로 칸 수입니다.")]
    [SerializeField] private int _cols = 7;
    [Tooltip("전장 세로 칸 수입니다.")]
    [SerializeField] private int _rows = 4;
    [Tooltip("BoardManager 기준 전장 루트의 로컬 위치입니다.")]
    [SerializeField] private Vector3 _boardPosition = Vector3.zero;
    [Tooltip("전장 루트의 로컬 회전값입니다.")]
    [SerializeField] private Vector3 _boardRotation = Vector3.zero;
    [Tooltip("전장 전체에 적용할 스케일입니다.")]
    [SerializeField] private Vector3 _boardScale = Vector3.one;

    [Header("Bench Prefab")]
    [Tooltip("벤치 한 칸에 사용할 BenchTile 프리팹입니다. 비어 있으면 기존 원기둥 임시 타일을 생성합니다.")]
    [SerializeField] private BenchTile _benchTilePrefab;

    [Header("Bench Layout")]
    [Tooltip("벤치 슬롯 수입니다.")]
    [SerializeField] private int _benchSize = 9;
    [SerializeField] private float _benchXOffset = 0f;
    [SerializeField] private float _benchYOffset = 0f;
    [SerializeField] private float _benchZOffset = -4f;
    [Tooltip("벤치 루트의 로컬 회전값입니다.")]
    [SerializeField] private Vector3 _benchRotation = Vector3.zero;
    [Tooltip("벤치 전체에 적용할 스케일입니다.")]
    [SerializeField] private Vector3 _benchScale = Vector3.one;
    [Tooltip("슬롯 간격입니다. Hex Size에 이 값을 곱해 실제 간격을 계산합니다.")]
    [SerializeField] private float _benchSpacingMultiplier = 1.1f;

    // 💡 금고: 타일의 논리적 위치와 그 위 앉아있는 유닛을 매핑하는 딕셔너리
    private Dictionary<HexCoords, PokemonUnit> _battleField = new Dictionary<HexCoords, PokemonUnit>();

    // 벤치 슬롯. 인덱스 = 슬롯 번호. null = 빈 슬롯.
    private PokemonUnit[] _bench;

    // 벤치 시각 타일 + 슬롯별 월드 좌표(인덱스 = 슬롯).
    private BenchTile[] _benchTiles;
    private Vector3[] _benchSlotLocalPositions;

    // 인스펙터의 위치/회전/스케일을 좌표 변환에도 동일하게 적용하기 위한 런타임 루트.
    private Transform _boardAnchor;
    private Transform _benchAnchor;

    // 진화(합체) 처리 중 재진입 방지 플래그.
    private bool _isEvolving;

    // 보드 중앙 정렬에 사용된 오프셋. CoordsToWorldPosition에서 재사용.
    private Vector3 _centerOffset;

    // 보드 배치 가능 기물 수(캡). 캡 산정의 단일 소스는 ShopManager이며,
    // BoardManager는 레벨에서 캡을 재유도하지 않고 GameEvents.OnUnitCapChanged로 받은 값을 그대로 사용한다.
    private int _unitCap = 1;

    private void Awake()
    {
        _bench = new PokemonUnit[_benchSize];
    }

    private void OnValidate()
    {
        _hexSize = Mathf.Max(0.01f, _hexSize);
        _cols = Mathf.Max(1, _cols);
        _rows = Mathf.Max(1, _rows);
        _benchSize = Mathf.Max(1, _benchSize);
        _benchSpacingMultiplier = Mathf.Max(0.01f, _benchSpacingMultiplier);
    }

    private void OnEnable()
    {
        GameEvents.OnUnitCapChanged += HandleUnitCapChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnUnitCapChanged -= HandleUnitCapChanged;
    }

    private void HandleUnitCapChanged(int cap)
    {
        // 캡 값은 ShopManager가 레벨별 테이블 기준으로 산정해 전달한다. 여기서는 그대로 반영만.
        _unitCap = Mathf.Max(1, cap);
        Debug.Log($"[BoardManager] 배치 가능 기물 수 변경 반영: {_unitCap}");

        // 레벨업으로 캡이 늘면 롤체처럼 벤치 앞쪽 기물을 빈 보드로 자동 승격한다.
        AutoPromoteBenchToBoard();
    }

    /// <summary>
    /// 보드에 여유 슬롯(현재 배치 수 &lt; 캡)이 있으면 벤치 앞쪽(슬롯 0,1,2…) 유닛부터
    /// 빈 보드 타일로 자동 승격한다(롤체식). 레벨업(캡 증가) 시 호출.
    /// TryPlaceUnit을 재사용하므로 캡 검사·UnitPlaced 이벤트·합체 검사가 그대로 적용된다.
    /// 각자 보드 각자 권위라 로컬 처리로 충분하며 파트너 미러는 UnitPlaced로 갱신된다.
    /// </summary>
    /// <returns>실제로 보드에 올린 유닛 수.</returns>
    public int AutoPromoteBenchToBoard()
    {
        if (_bench == null) return 0;

        int promoted = 0;
        for (int slot = 0; slot < _bench.Length; slot++)
        {
            if (CountUnitsOnBoard() >= _unitCap) break; // 보드가 캡까지 참
            PokemonUnit unit = _bench[slot];
            if (unit == null) continue;
            if (!TryGetFirstEmptyBoardCoords(out HexCoords coords)) break; // 빈 보드 타일 없음
            if (TryPlaceUnit(unit, coords)) promoted++;
            else break; // 예기치 못한 배치 실패 시 무한 시도 방지
        }

        if (promoted > 0)
            Debug.Log($"[BoardManager] 벤치→보드 자동 승격 {promoted}마리 (캡 {_unitCap})");
        return promoted;
    }

    /// <summary>보드에서 비어 있는 첫 좌표를 찾는다(_battleField 삽입 순서 = 결정적). 없으면 false.</summary>
    private bool TryGetFirstEmptyBoardCoords(out HexCoords coords)
    {
        foreach (var kv in _battleField)
        {
            if (kv.Value == null)
            {
                coords = kv.Key;
                return true;
            }
        }
        coords = default;
        return false;
    }

    private void Start()
    {
        GenerateBoard();
        GenerateBench();
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
        _boardAnchor = boardAnchor.transform;
        _boardAnchor.SetParent(transform, false);
        _boardAnchor.SetLocalPositionAndRotation(_boardPosition, Quaternion.Euler(_boardRotation));
        _boardAnchor.localScale = _boardScale;

        Vector3 sumPosition = Vector3.zero;
        int totalTiles = 0;

        // 2. 타일 생성 루프
        for (int row = 0; row < _rows; row++)
        {
            // 하이어라키 정리를 위한 행(Row) 폴더 생성
            GameObject rowFolder = new GameObject($"Row_{row}");
            rowFolder.transform.SetParent(_boardAnchor, false);

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
            _centerOffset = sumPosition / totalTiles;
            // Anchor의 인스펙터 Transform은 유지하고, 타일 묶음만 로컬 좌표에서 중앙 정렬합니다.
            foreach (Transform rowFolder in _boardAnchor)
                foreach (Transform tile in rowFolder)
                    tile.localPosition -= _centerOffset;
        }
    }

    /// <summary>
    /// 벤치 슬롯들을 보드 아래쪽(_benchZOffset)에 한 줄로 생성합니다.
    /// 각 타일은 BenchTile(IDropTarget)이며, 드롭 시 TryDropOnBench로 위임합니다.
    /// </summary>
    private void GenerateBench()
    {
        _benchTiles = new BenchTile[_benchSize];
        _benchSlotLocalPositions = new Vector3[_benchSize];

        GameObject benchAnchor = new GameObject("BenchAnchor");
        _benchAnchor = benchAnchor.transform;
        _benchAnchor.SetParent(transform, false);
        _benchAnchor.SetLocalPositionAndRotation(
            new Vector3(_benchXOffset, _benchYOffset, _benchZOffset),
            Quaternion.Euler(_benchRotation));
        _benchAnchor.localScale = _benchScale;

        float spacing = _hexSize * _benchSpacingMultiplier;
        float startX = -(_benchSize - 1) * spacing * 0.5f;
        bool warnedMissingCollider = false;

        for (int i = 0; i < _benchSize; i++)
        {
            Vector3 localPos = new Vector3(startX + i * spacing, 0f, 0f);
            _benchSlotLocalPositions[i] = localPos;

            BenchTile tile;
            if (_benchTilePrefab != null)
            {
                tile = Instantiate(_benchTilePrefab, _benchAnchor);
            }
            else
            {
                GameObject tileGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tileGo.transform.SetParent(_benchAnchor, false);
                tileGo.transform.localScale = new Vector3(_hexSize * 0.9f, 0.05f, _hexSize * 0.9f);
                tile = tileGo.AddComponent<BenchTile>();
            }

            tile.transform.localPosition = localPos;
            int slot = i; // 클로저 캡처 주의 — 루프 변수 복사
            tile.Initialize(slot, (unit, s) => TryDropOnBench(unit, s), $"BenchTile_{slot}");
            _benchTiles[i] = tile;

            if (!warnedMissingCollider && tile.GetComponentInChildren<Collider>() == null)
            {
                Debug.LogWarning($"[BoardManager] BenchTile 프리팹에 Collider가 없어 드롭을 받을 수 없습니다: {tile.name}", tile);
                warnedMissingCollider = true;
            }
        }
    }

    /// <summary>
    /// 임의의 헥스 좌표(보드 범위 밖 포함)를 보드 정렬 기준 월드 좌표로 변환합니다.
    /// BattleManager가 적 팀 미러 좌표를 시각화할 때 사용.
    /// </summary>
    public Vector3 CoordsToWorldPosition(HexCoords coords)
    {
        Vector3 localPosition = coords.ToWorldPosition(_hexSize) - _centerOffset;
        return _boardAnchor != null ? _boardAnchor.TransformPoint(localPosition) : localPosition;
    }

    /// <summary>벤치 슬롯의 월드 좌표. BoardView가 유닛 위치 재배치에 사용.</summary>
    public Vector3 BenchSlotWorldPosition(int slot)
    {
        if (_benchSlotLocalPositions == null || slot < 0 || slot >= _benchSlotLocalPositions.Length)
            return Vector3.zero;

        Vector3 localPosition = _benchSlotLocalPositions[slot];
        return _benchAnchor != null ? _benchAnchor.TransformPoint(localPosition) : localPosition;
    }

    /// <summary>
    /// 보드를 행(row) 기준으로 뒤집은 좌표를 반환합니다. 같은 (col, row) 좌표 집합을 재사용하므로
    /// 항상 유효한 보드 타일 좌표이며, 평행이동만으로도 모양이 정확히 대칭인 "상대 보드"를 만들 수 있음.
    /// </summary>
    public HexCoords GetMirroredCoords(HexCoords coords)
    {
        int row = coords.r + Mathf.FloorToInt(coords.q / 2f);
        int mirroredRow = (_rows - 1) - row;
        int mirroredR = mirroredRow - Mathf.FloorToInt(coords.q / 2f);
        return new HexCoords(coords.q, mirroredR);
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

    /// <summary>벤치 슬롯 배열(읽기 전용, 인덱스=슬롯). null=빈 슬롯. BoardView 위치 재배치에 사용.</summary>
    public IReadOnlyList<PokemonUnit> GetBenchSnapshot() => _bench;

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

        // 벤치 → 빈 보드 타일로 새로 올리는 경우에만 배치 가능 기물 수 제한 검사.
        // 보드 → 보드 이동은 기물 수가 늘지 않으므로 허용.
        // 벤치 → 점유된 보드 타일 스왑도 보드 위 기물 수가 늘지 않으므로 허용.
        if (!fromBoard && occupant == null)
        {
            int currentBoardCount = CountUnitsOnBoard();

            if (currentBoardCount >= _unitCap)
            {
                const string reason = "배치 가능한 유닛 수를 초과했습니다.";
                Debug.LogWarning($"[BoardManager] {reason} ({currentBoardCount}/{_unitCap})");
                GameEvents.UnitPlacementRejected(reason);
                return false;
            }
        }

        // 분기별 배치 처리. 실패(벤치 슬롯 확보 불가 등) 시 상태 변경 없이 거부.
        bool placed;
        if (occupant == null)
            placed = PlaceOnEmptyTile(unit, targetCoords, fromBoard, fromCoords);
        else if (fromBoard)
            placed = SwapOnBoard(unit, targetCoords, fromCoords, occupant);
        else
            placed = SwapBenchIntoBoard(unit, targetCoords, occupant);

        if (!placed) return false;

        GameEvents.UnitPlaced(unit);
        if (unit.data != null) CheckEvolution(unit.data.id, unit.starLevel);
        return true;
    }

    /// <summary>빈 보드 타일로 단순 이동/신규 배치. 들어온 출처(보드/벤치)에 따라 원위치를 비운다.</summary>
    private bool PlaceOnEmptyTile(PokemonUnit unit, HexCoords targetCoords, bool fromBoard, HexCoords fromCoords)
    {
        if (fromBoard) _battleField[fromCoords] = null;
        else RemoveFromBenchByRef(unit); // 벤치에서 온 경우 벤치 슬롯 비움

        _battleField[targetCoords] = unit;
        unit.isOnBoard = true;
        return true;
    }

    /// <summary>보드 ↔ 보드 스왑. 두 유닛 모두 보드에 남는다.</summary>
    private bool SwapOnBoard(PokemonUnit unit, HexCoords targetCoords, HexCoords fromCoords, PokemonUnit occupant)
    {
        _battleField[fromCoords] = occupant;
        _battleField[targetCoords] = unit;
        return true;
    }

    /// <summary>
    /// 벤치 → 점유된 보드 타일: 벤치 슬롯과 보드 유닛을 교체.
    /// unit이 보드에도 벤치에도 없는 경우(현재 호출부에서는 발생하지 않지만 방어) —
    /// occupant를 보낼 빈 벤치 슬롯을 새로 찾고, 그마저 없으면 occupant가 추적 불가능한 채로
    /// 유실되는 걸 막기 위해 배치 자체를 거부한다.
    /// </summary>
    private bool SwapBenchIntoBoard(PokemonUnit unit, HexCoords targetCoords, PokemonUnit occupant)
    {
        int benchSlot = FindBenchSlot(unit);
        if (benchSlot < 0) benchSlot = FirstEmptyBenchSlot();
        if (benchSlot < 0)
        {
            Debug.LogWarning("[BoardManager] 배치 거부 — 점유 중인 유닛을 보낼 빈 벤치 슬롯이 없습니다.");
            return false;
        }

        _battleField[targetCoords] = unit;
        unit.isOnBoard = true;
        _bench[benchSlot] = occupant;
        occupant.isOnBoard = false;
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
        if (unit.data != null) CheckEvolution(unit.data.id, unit.starLevel);
        return true;
    }

    /// <summary>
    /// 벤치 슬롯에 드롭됐을 때(드래그 콜백). 빈 슬롯이면 배치, 점유 슬롯이면 들어오는 유닛의
    /// 원위치(보드 좌표/벤치 슬롯)와 교체(스왑)한다. 성공 시 true.
    /// </summary>
    public bool TryDropOnBench(PokemonUnit unit, int slot)
    {
        if (unit == null) return false;
        if (slot < 0 || slot >= _benchSize) return false;

        PokemonUnit occupant = _bench[slot];
        if (occupant == unit) return true;            // 같은 자리 멱등
        if (occupant == null) return TryPlaceInBench(unit, slot); // 빈 슬롯 — 일반 배치(+진화 검사)

        // 점유됨 — 들어오는 유닛의 원위치로 occupant를 보내고 교체
        if (TryFindBoardCoords(unit, out HexCoords fromCoords))
        {
            _battleField[fromCoords] = occupant;
            occupant.isOnBoard = true;
        }
        else
        {
            int fromSlot = FindBenchSlot(unit);
            if (fromSlot < 0)
            {
                // 들어온 유닛이 보드에도 벤치에도 없음 — occupant를 되돌려놓을 원위치가 없으므로
                // 스왑을 거부한다. (그대로 진행하면 occupant를 덮어써 유실됨)
                Debug.LogWarning($"[Board] TryDropOnBench 거부: 들어온 유닛의 원위치를 찾지 못해 스왑 불가(occupant 보호)");
                return false;
            }
            _bench[fromSlot] = occupant; // 벤치↔벤치 스왑
        }

        _bench[slot] = unit;
        unit.isOnBoard = false;

        GameEvents.UnitBenched(unit);
        if (unit.data != null) CheckEvolution(unit.data.id, unit.starLevel);
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

    /// <summary>
    /// 유닛을 보드/벤치 어디에 있든 제거하고 판매 처리. 골드 환급은 ShopManager가 OnUnitSold를 받아 처리한다.
    /// UnitSold 발행 시점엔 unit이 아직 유효(Destroy는 프레임 끝에 적용)하므로 구독자가 data를 읽을 수 있음.
    /// </summary>
    public bool SellUnit(PokemonUnit unit)
    {
        if (unit == null) return false;

        bool onBoard = TryFindBoardCoords(unit, out HexCoords coords);
        int slot = onBoard ? -1 : FindBenchSlot(unit);
        if (!onBoard && slot < 0) return false; // 보드/벤치 어디에도 없음

        if (onBoard) _battleField[coords] = null;
        else         _bench[slot] = null;

        GameEvents.UnitSold(unit); // ShopManager(환급) · SynergyManager(재계산) · BoardView(resync)
        Destroy(unit.gameObject);
        return true;
    }

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

    /// <summary>현재 보드 위에 있는 유닛 수만 계산한다. List를 만들지 않아 배치 제한 체크용으로 가볍다.</summary>
    private int CountUnitsOnBoard()
    {
        int count = 0;

        foreach (var unit in _battleField.Values)
        {
            if (unit != null)
                count++;
        }

        return count;
    }

    // ──────────────────────────────────────────
    // 합체 (일반 진화 = 동일 종 3개 → 별업, GDD 4.2)
    // ──────────────────────────────────────────

    /// <summary>
    /// 보드+벤치 통틀어 같은 종(data.id)·같은 성(starLevel) 유닛이 3개 이상이면 1개로 합쳐 별업.
    /// 별업 시 종이 진화체(evolvesIntoEn)로 바뀐다 — 꼬마돌1성×3 → 데구리2성, 데구리2성×3 → 딱구리3성.
    /// 1성→2성→3성, 3성이 상한. 진화 대상이 없으면(최종형/데이터 미비) 종은 유지하고 별만 올림.
    /// 생존 위치: 셋 중 보드에 있던 게 있으면 보드(첫 좌표), 아니면 벤치(첫 슬롯).
    /// </summary>
    private void CheckEvolution(int speciesId, int starLevel)
    {
        if (_isEvolving) return;
        if (starLevel >= 3) return;

        var boardMatches = new List<HexCoords>();
        var benchMatches = new List<int>();

        // 돌 낀 유닛은 머지 후보 제외 — 안 그러면 합체 시 소비된 유닛의 돌이 Destroy로 같이 증발한다.
        // "머지하려면 돌부터 빼라"가 의도된 흐름(진화의 돌 설계 문서, 2026-06-22).
        foreach (var kv in _battleField)
            if (kv.Value != null && kv.Value.data != null && !kv.Value.IsStoneEvolved &&
                kv.Value.data.id == speciesId && kv.Value.starLevel == starLevel)
                boardMatches.Add(kv.Key);

        for (int i = 0; i < _bench.Length; i++)
            if (_bench[i] != null && _bench[i].data != null && !_bench[i].IsStoneEvolved &&
                _bench[i].data.id == speciesId && _bench[i].starLevel == starLevel)
                benchMatches.Add(i);

        if (boardMatches.Count + benchMatches.Count < 3) return;

        _isEvolving = true;

        // 소비할 3개 모으기(보드 우선). consumed[0]이 생존자 = 첫 위치.
        var consumed = new List<PokemonUnit>();
        foreach (var c in boardMatches) { if (consumed.Count < 3) consumed.Add(_battleField[c]); }
        foreach (var s in benchMatches) { if (consumed.Count < 3) consumed.Add(_bench[s]); }

        bool survivorOnBoard = boardMatches.Count > 0;
        HexCoords survivorCoords = survivorOnBoard ? boardMatches[0] : default;
        int survivorSlot = survivorOnBoard ? -1 : benchMatches[0];
        PokemonUnit survivor = consumed[0];

        // 소비된 3개의 위치 비우기
        foreach (var c in boardMatches)
            if (consumed.Contains(_battleField[c])) _battleField[c] = null;
        for (int i = 0; i < _bench.Length; i++)
            if (_bench[i] != null && consumed.Contains(_bench[i])) _bench[i] = null;

        // 생존자 외 2개 파괴
        foreach (var u in consumed)
            if (u != survivor && u != null) Destroy(u.gameObject);

        // 별업 + 종 진화(evolvesIntoEn으로 스왑) + 재배치
        survivor.starLevel = Mathf.Clamp(starLevel + 1, 1, 3);

        // 상위 성급은 진화체로 종을 교체(꼬마돌→데구리→딱구리). data 스왑만으로 스탯/스킬/시너지 전부 전환됨.
        // 진화 대상이 없거나(최종형) DB에 진화체가 없으면 종 유지(같은 종 별업으로 폴백).
        // 진화잠금(이브이 영웅증강 등)이면 종 스왑을 건너뛰고 별만 올린다 — 3성까지 원본 종 유지.
        string evolvedEn = survivor.evolutionLocked ? null : survivor.data.evolvesIntoEn;
        if (!string.IsNullOrEmpty(evolvedEn))
        {
            var evolved = PokemonDatabase.Instance != null ? PokemonDatabase.Instance.GetByNameEn(evolvedEn) : null;
            if (evolved != null) survivor.data = evolved;
            else Debug.LogWarning($"[Evolve] 진화체 '{evolvedEn}' 가 PokemonDatabase에 없음 — 종 유지(별만 상승). 데이터 보강 필요");
        }

        survivor.RefreshVisual();    // 진화체 모델로 교체 (data 스왑 후 호출 — BoardView는 위치만 갱신한다)
        survivor.ResetForBattle();   // 진화체 MaxHp 기준 풀회복 (data 스왑 후 호출)
        if (survivorOnBoard) { _battleField[survivorCoords] = survivor; survivor.isOnBoard = true; }
        else                 { _bench[survivorSlot] = survivor;        survivor.isOnBoard = false; }

        _isEvolving = false;

        Debug.Log($"[Evolve] {starLevel}성 3개 합체 → {survivor.data.pokemonName} {survivor.starLevel}성");

        // 이벤트 발화(시너지 재계산 + BoardView 위치 재배치. 모델 교체는 위 RefreshVisual에서 이미 처리)
        if (survivorOnBoard) GameEvents.UnitPlaced(survivor);
        else                 GameEvents.UnitBenched(survivor);

        // 연쇄(예: 데구리 2성 3개 → 딱구리 3성). 진화로 바뀐 새 종 id로 재검사.
        CheckEvolution(survivor.data.id, survivor.starLevel);
    }
}
