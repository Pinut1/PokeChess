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
    [Tooltip("벤치 슬롯 수입니다. 바닥 헥스 타일은 이 수만큼 생성됩니다.")]
    [SerializeField] private int _benchSize = 9;
    [Tooltip("그중 실제로 유닛을 놓을 수 있는 슬롯 수입니다(앞에서부터). " +
             "나머지 뒤쪽 칸은 타일만 남고 구매·교환·드래그 어느 경로로도 채워지지 않습니다.")]
    [SerializeField] private int _usableBenchSize = 8;
    [SerializeField] private float _benchXOffset = 0f;
    [SerializeField] private float _benchYOffset = 0f;
    [SerializeField] private float _benchZOffset = -4f;
    [Tooltip("벤치 루트의 로컬 회전값입니다.")]
    [SerializeField] private Vector3 _benchRotation = Vector3.zero;
    [Tooltip("벤치 전체에 적용할 스케일입니다.")]
    [SerializeField] private Vector3 _benchScale = Vector3.one;
    [Tooltip("슬롯 간격입니다. Hex Size에 이 값을 곱해 실제 간격을 계산합니다.")]
    [SerializeField] private float _benchSpacingMultiplier = 1.1f;

    [Header("Managers")]
    [SerializeField] private ItemManager _itemManager;

    // 💡 금고: 타일의 논리적 위치와 그 위 앉아있는 유닛을 매핑하는 딕셔너리
    private Dictionary<HexCoords, PokemonUnit> _battleField = new Dictionary<HexCoords, PokemonUnit>();

    // 좌표 → 시각 타일. 빈 칸/유닛 칸 색을 갈아끼우기 위한 조회용이며 배치 판정에는 쓰지 않는다.
    private readonly Dictionary<HexCoords, HexTile> _tiles = new Dictionary<HexCoords, HexTile>();

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

    /// <summary>현재 레벨 기준 보드 배치 상한. 표시용(BoardCapacityLabel 등) 읽기 전용 노출.</summary>
    public int UnitCap => _unitCap;

    /// <summary>아군 헥스 타일 프리팹(읽기 전용 노출). 파트너 관전용 타일 복제(OpponentBoardView)가
    /// 실제 좌표 API(CoordsToWorldPosition)와 함께 재사용한다. 필드 자체는 계속 private [SerializeField]로 유지.</summary>
    public HexTile TilePrefab => _tilePrefab;

    /// <summary>벤치 받침 프리팹(읽기 전용 노출). 용도는 TilePrefab과 동일.</summary>
    public BenchTile BenchTilePrefab => _benchTilePrefab;

    /// <summary>벤치 받침 앵커(_benchAnchor)의 월드 회전. GenerateBench()는 각 BenchTile의 로컬 회전을
    /// 건드리지 않으므로(위치만 대입), 실제 벤치 타일의 월드 회전은 이 값과 프리팹 자체의 로컬 회전을
    /// 곱한 값이다. 파트너 관전용 벤치 복제(OpponentBoardView)가 BenchSlotWorldPosition(위치)과 함께
    /// 실제 로컬 벤치와 동일한 회전을 재현하는 데 쓴다.</summary>
    public Quaternion BenchAnchorRotation => _benchAnchor != null ? _benchAnchor.rotation : Quaternion.identity;

    /// <summary>벤치 받침 앵커(_benchAnchor)의 월드 스케일(_benchScale 인스펙터 값 반영). GenerateBench()가
    /// 각 BenchTile의 로컬 스케일을 건드리지 않으므로, 실제 벤치 타일의 월드 스케일은 이 값과 프리팹
    /// 자체의 로컬 스케일을 곱한 값이다. 용도는 BenchAnchorRotation과 동일.</summary>
    public Vector3 BenchAnchorScale => _benchAnchor != null ? _benchAnchor.lossyScale : Vector3.one;

    /// <summary>지금 보드 위에 올라간 유닛 수. 표시용 읽기 전용 노출.</summary>
    public int BoardUnitCount => CountUnitsOnBoard();

    private readonly List<PendingStarEvolution> _pendingStarEvolutions = new();
    private bool _isBattlePhase;

    // 플러시와마이농 필드 전용 폼 전환. 신규 유틸 클래스를 만들지 않고 이 클래스 안에서만 쓴다.
    private const int PLUSLE_MINUN_ID = 310;
    private const int PLUSLE_ID       = 311;
    private const int MINUN_ID        = 312;

    private struct PendingStarEvolution
    {
        public int SpeciesId;
        public int StarLevel;
    }

    /// <summary>
    /// 재접속 복원용 유닛 스냅샷(1차 구현 — 저장 데이터 구조만). NetworkManager가 이 구조체 목록을
    /// JSON으로 직렬화해 Photon Player CustomProperties에 저장/복원한다. HP/마나 등 전투 임시값은
    /// ResetForBattle()로 매 라운드 재계산되므로 포함하지 않는다.
    /// </summary>
    [System.Serializable]
    public struct UnitSaveData
    {
        public int PokemonId;          // data.id (도감번호) — PokemonDatabase.GetById로 재조회
        public int StarLevel;          // 1~3
        public bool IsOnBoard;         // true=필드, false=벤치
        public int Q;                  // IsOnBoard일 때만 유효(HexCoords, s는 -Q-R로 유도)
        public int R;
        public int BenchSlot;          // IsOnBoard가 false일 때만 유효(배열 인덱스)
        public bool IsTradeEvolved;
        public int EquippedStoneId;    // 0=돌 없음 (EvolutionStoneDatabase.GetById 키)
        public int PreStoneSpeciesId;  // 돌 있을 때만 유효 — 돌 해제 시 되돌아갈 베이스 종
        public int[] ItemIds;          // 장착 일반 아이템(최대 MaxItemSlots=2, 돌과 슬롯 공유)
    }

    private void Awake()
    {
        // OnValidate는 에디터에서만 도는 데다, 씬을 저장한 뒤 _benchSize를 줄인 경우엔
        // 사용 가능 슬롯이 배열 밖을 가리킬 수 있어 런타임에서도 한 번 맞춘다.
        _usableBenchSize = Mathf.Clamp(_usableBenchSize, 1, Mathf.Max(1, _benchSize));

        _bench = new PokemonUnit[_benchSize];

        if (_itemManager == null)
            _itemManager = FindFirstObjectByType<ItemManager>();
    }

    private void OnValidate()
    {
        _hexSize = Mathf.Max(0.01f, _hexSize);
        _cols = Mathf.Max(1, _cols);
        _rows = Mathf.Max(1, _rows);
        _benchSize = Mathf.Max(1, _benchSize);
        _usableBenchSize = Mathf.Clamp(_usableBenchSize, 1, _benchSize);
        _benchSpacingMultiplier = Mathf.Max(0.01f, _benchSpacingMultiplier);
    }

    private void OnEnable()
    {
        GameEvents.OnUnitCapChanged += HandleUnitCapChanged;
        GameEvents.OnUnitChanged += HandleUnitChanged;
        GameEvents.OnPhaseChanged += HandlePhaseChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnUnitCapChanged -= HandleUnitCapChanged;
        GameEvents.OnUnitChanged -= HandleUnitChanged;
        GameEvents.OnPhaseChanged -= HandlePhaseChanged;
    }

    private void HandleUnitCapChanged(int cap)
    {
        // 캡 값은 ShopManager가 레벨별 테이블 기준으로 산정해 전달한다. 여기서는 그대로 반영만.
        _unitCap = Mathf.Max(1, cap);
        Debug.Log($"[BoardManager] 배치 가능 기물 수 변경 반영: {_unitCap}");
    }

    private void HandlePhaseChanged(GamePhase phase)
    {
        _isBattlePhase = phase == GamePhase.Battle;

        // 전투 중에는 빈 칸/유닛 칸 구분을 끄고 전부 Default Color로 고정한다.
        // (전투 중 자동 승격 등으로 점유가 바뀌어도 기록만 되고, 전투가 끝나면 그 배치대로 다시 칠해진다)
        foreach (HexTile tile in _tiles.Values)
            if (tile != null) tile.SetBattleLocked(_isBattlePhase);

        if (phase == GamePhase.Shopping)
            ApplyPendingStarEvolutions();
        else if (phase == GamePhase.Battle)
            AutoPromoteBenchToBoard(); // 전투 시작 시점에만 부족한 만큼 벤치 앞쪽 기물을 빈 보드로 자동 승격한다.
    }

    private void AddPendingStarEvolution(int speciesId, int starLevel)
    {
        foreach (PendingStarEvolution pending in _pendingStarEvolutions)
        {
            if (pending.SpeciesId == speciesId &&
                pending.StarLevel == starLevel)
            {
                return;
            }
        }

        _pendingStarEvolutions.Add(new PendingStarEvolution
        {
            SpeciesId = speciesId,
            StarLevel = starLevel
        });

        Debug.Log($"[Evolve] 전투 중 합체 대기 등록: {speciesId} / {starLevel}성");
    }

    private void ApplyPendingStarEvolutions()
    {
        if (_pendingStarEvolutions.Count == 0)
            return;

        var pendingList =
            new List<PendingStarEvolution>(_pendingStarEvolutions);

        _pendingStarEvolutions.Clear();

        foreach (PendingStarEvolution pending in pendingList)
        {
            CheckEvolution(
                pending.SpeciesId,
                pending.StarLevel,
                false
            );
        }
    }

    /// <summary>
    /// 진화의 돌 장착·해제나 외부 효과로 유닛의 종/성급이 바뀌었을 때
    /// 변경된 현재 종과 성급을 기준으로 합체를 다시 검사한다.
    /// </summary>
    private void HandleUnitChanged(PokemonUnit unit)
    {
        RecheckEvolution(unit);
    }

    /// <summary>
    /// 보드에 여유 슬롯(현재 배치 수 &lt; 캡)이 있으면 벤치 앞쪽(슬롯 0,1,2…) 유닛부터
    /// 빈 보드 타일로 자동 승격한다(롤체식). 쇼핑 종료 후 Battle 페이즈 진입 시 호출(HandlePhaseChanged).
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
        _tiles.Clear();

        // 1. 쟁반(Anchor) 생성
        GameObject boardAnchor = new GameObject("BoardAnchor");
        _boardAnchor = boardAnchor.transform;
        _boardAnchor.SetParent(transform, false);
        _boardAnchor.SetLocalPositionAndRotation(_boardPosition, Quaternion.Euler(_boardRotation));
        _boardAnchor.localScale = _boardScale;

        Vector3 battleAreaPositionSum = Vector3.zero;
        int battleAreaPositionCount = 0;

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
                ApplyLocalVisualLayer(newTile.gameObject);

                // 금고에 빈 타일 등록
                _battleField.Add(coords, null);
                _tiles[coords] = newTile;

                // 평균 위치 계산을 위해 누적
                battleAreaPositionSum += worldPos;
                battleAreaPositionSum += GetEnemyBattleCoords(coords).ToWorldPosition(_hexSize);
                battleAreaPositionCount += 2;
            }
        }

        // 3. 중앙 정렬 (Centering)
        // 타일들의 평균 무게중심(Center)을 구한 뒤, 쟁반 전체를 그 반대 방향으로 밀어줍니다.
        if (battleAreaPositionCount > 0)
        {
            _centerOffset = battleAreaPositionSum / battleAreaPositionCount;
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

            // 예약 슬롯도 바닥 타일은 그대로 만든다(줄이 비어 보이지 않게). 이름을 구분해
            // 하이어라키에서 다른 용도로 쓸 칸을 바로 찾을 수 있게 한다.
            // 드롭 콜백은 그대로 걸어두고 TryDropOnBench가 거부한다 — 판정을 한 곳에 모으기 위함.
            string tileName = IsUsableBenchSlot(slot) ? $"BenchTile_{slot}" : $"BenchTile_{slot}_Reserved";
            tile.Initialize(slot, (unit, s) => TryDropOnBench(unit, s), tileName);
            ApplyLocalVisualLayer(tile.gameObject);
            _benchTiles[i] = tile;

            if (!warnedMissingCollider && tile.GetComponentInChildren<Collider>() == null)
            {
                Debug.LogWarning($"[BoardManager] BenchTile 프리팹에 Collider가 없어 드롭을 받을 수 없습니다: {tile.name}", tile);
                warnedMissingCollider = true;
            }
        }
    }

    /// <summary>
    /// 로컬(내) 보드 시각 오브젝트(아군 타일/벤치 받침) 전용 Layer 태깅. Default(0)였던 자식만
    /// LocalGameplayVisual로 바꾸고, 이미 UI/Ignore Raycast/Outline 등 다른 특수 Layer가 지정된
    /// 자식은 보존한다. LocalGameplayVisual/PartnerSpectateVisual 둘 중 하나라도 Unity Editor에
    /// 아직 추가되지 않았으면 아무 것도 바꾸지 않는다 — 부분 적용(카메라 CullingMask는 그대로인데
    /// Layer만 바뀌어 오히려 안 보이게 되는 상태)을 막기 위함. Instantiate 직후 1회만 호출한다.
    /// </summary>
    private static void ApplyLocalVisualLayer(GameObject root)
    {
        int localLayer = LayerMask.NameToLayer("LocalGameplayVisual");
        int partnerLayer = LayerMask.NameToLayer("PartnerSpectateVisual");
        if (localLayer < 0 || partnerLayer < 0 || root == null) return;

        SetDefaultLayerRecursive(root.transform, localLayer);
    }

    private static void SetDefaultLayerRecursive(Transform node, int targetLayer)
    {
        if (node.gameObject.layer == 0) node.gameObject.layer = targetLayer;
        for (int i = 0; i < node.childCount; i++)
            SetDefaultLayerRecursive(node.GetChild(i), targetLayer);
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

    /// <summary>
    /// 적 진영을 아군 보드 "너머"(rows _rows ~ 2*_rows-1)의 연속된 좌표로 변환합니다.
    /// GetMirroredCoords로 대칭 배치한 뒤 행을 _rows만큼 평행이동해, 아군 rows 0~(_rows-1) 과
    /// 겹치지 않는 8행(4+4) 단일 전장을 만든다. 이러면 근접이 미들라인((_rows-1)↔_rows)을
    /// 실제로 걸어서 넘어 적 진영으로 진입한다(TFT식). 전투 시뮬은 자유 HexCoords 기반이라 무수정.
    /// 시각화도 CoordsToWorldPosition을 그대로 써서 별도 오프셋 없이 한 보드처럼 이어 그린다.
    /// </summary>
    public HexCoords GetEnemyBattleCoords(HexCoords coords)
    {
        HexCoords mirrored = GetMirroredCoords(coords);
        // row = r + floor(q/2) 이므로, 같은 q에서 r을 _rows만큼 더하면 행이 정확히 _rows칸 밀린다.
        return new HexCoords(mirrored.q, mirrored.r + _rows);
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
    // 재접속 복원(1차 구현 — 저장 데이터 구조 + 생성/복원 기반만)
    // ──────────────────────────────────────────

    /// <summary>현재 보드+벤치의 모든 유닛을 UnitSaveData 목록으로 뽑아낸다(NetworkManager가 직렬화해 저장).</summary>
    public List<UnitSaveData> BuildSnapshot()
    {
        var result = new List<UnitSaveData>();

        foreach (var kvp in _battleField)
        {
            if (kvp.Value != null)
                result.Add(BuildUnitSaveData(kvp.Value, true, kvp.Key.q, kvp.Key.r, -1));
        }

        for (int slot = 0; slot < _bench.Length; slot++)
        {
            if (_bench[slot] != null)
                result.Add(BuildUnitSaveData(_bench[slot], false, 0, 0, slot));
        }

        return result;
    }

    private static UnitSaveData BuildUnitSaveData(PokemonUnit unit, bool isOnBoard, int q, int r, int benchSlot)
    {
        var itemIds = new int[unit.items.Count];
        for (int i = 0; i < unit.items.Count; i++)
            itemIds[i] = unit.items[i] != null ? unit.items[i].id : 0;

        return new UnitSaveData
        {
            PokemonId = unit.data != null ? unit.data.id : 0,
            StarLevel = unit.starLevel,
            IsOnBoard = isOnBoard,
            Q = q,
            R = r,
            BenchSlot = benchSlot,
            IsTradeEvolved = unit.isTradeEvolved,
            EquippedStoneId = unit.equippedStone != null ? unit.equippedStone.id : 0,
            PreStoneSpeciesId = unit.preStoneData != null ? unit.preStoneData.id : 0,
            ItemIds = itemIds
        };
    }

    /// <summary>
    /// UnitSaveData 목록으로부터 유닛을 재생성해 원래 위치(필드 좌표/벤치 슬롯)에 배치한다.
    /// 기존 배치 로직(TryPlaceUnit/TryPlaceInBench)과 UnitFactory.Create를 그대로 재사용하며,
    /// 이 메서드 자체는 수정하지 않는다(호출부인 NetworkManager가 저장 이벤트 재실행을 막는 책임을 짐).
    /// </summary>
    public void RestoreFromSnapshot(IReadOnlyList<UnitSaveData> units)
    {
        if (units == null) return;

        var pokemonDb = PokemonDatabase.Instance;

        foreach (var save in units)
        {
            PokemonData data = pokemonDb != null ? pokemonDb.GetById(save.PokemonId) : null;
            if (data == null)
            {
                Debug.LogWarning($"[BoardManager] 복원 실패 — PokemonId {save.PokemonId}를 PokemonDatabase에서 찾지 못함");
                continue;
            }

            PokemonUnit unit = UnitFactory.Create(data, save.StarLevel);
            if (unit == null) continue;

            unit.isTradeEvolved = save.IsTradeEvolved;

            if (save.ItemIds != null)
            {
                var itemDb = ItemDatabase.Instance;
                foreach (int itemId in save.ItemIds)
                {
                    if (itemId <= 0) continue;
                    ItemData item = itemDb != null ? itemDb.GetById(itemId) : null;
                    if (item != null) unit.TryEquipItem(item);
                }
            }

            if (save.EquippedStoneId > 0)
            {
                var stoneDb = EvolutionStoneDatabase.Instance;
                EvolutionStoneData stone = stoneDb != null ? stoneDb.GetById(save.EquippedStoneId) : null;
                PokemonData originalData = save.PreStoneSpeciesId > 0 && pokemonDb != null
                    ? pokemonDb.GetById(save.PreStoneSpeciesId) : null;

                if (stone != null && originalData != null)
                    unit.RestoreStoneStateAfterMerge(stone, originalData);
            }

            bool placed = save.IsOnBoard
                ? TryPlaceUnit(unit, new HexCoords(save.Q, save.R, -save.Q - save.R))
                : TryPlaceInBench(unit, save.BenchSlot);

            if (!placed)
            {
                Debug.LogWarning($"[BoardManager] 복원 배치 실패 — PokemonId {save.PokemonId} (OnBoard={save.IsOnBoard})");
                Destroy(unit.gameObject);
            }
        }
    }

    // ──────────────────────────────────────────
    // 보드 배치
    // ──────────────────────────────────────────

    /// <summary>
    /// TryPlaceInBench(unit)과 대칭되는 좌표 미지정 오버로드. 빈 보드 타일을 자동으로 찾아
    /// 배치한다(구매 시 벤치가 가득 찼을 때의 필드 폴백 등). 빈 타일이 없으면 실패.
    /// </summary>
    public bool TryPlaceUnit(PokemonUnit unit)
    {
        if (!TryGetFirstEmptyBoardCoords(out HexCoords coords))
            return false;

        return TryPlaceUnit(unit, coords);
    }

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

        AutoConvertPlusleMinunOnPlace(unit);

        GameEvents.UnitPlaced(unit);

        if (unit.data != null)
        {
            CheckEvolution(
                unit.data.id,
                unit.starLevel,
                unit.isTradeEvolved
            );
        }

        return true;
    }

    /// <summary>빈 보드 타일로 단순 이동/신규 배치. 들어온 출처(보드/벤치)에 따라 원위치를 비운다.</summary>
    private bool PlaceOnEmptyTile(PokemonUnit unit, HexCoords targetCoords, bool fromBoard, HexCoords fromCoords)
    {
        if (fromBoard) SetOccupant(fromCoords, null);
        else RemoveFromBenchByRef(unit); // 벤치에서 온 경우 벤치 슬롯 비움

        SetOccupant(targetCoords, unit);
        unit.isOnBoard = true;
        return true;
    }

    /// <summary>보드 ↔ 보드 스왑. 두 유닛 모두 보드에 남는다.</summary>
    private bool SwapOnBoard(PokemonUnit unit, HexCoords targetCoords, HexCoords fromCoords, PokemonUnit occupant)
    {
        SetOccupant(fromCoords, occupant);
        SetOccupant(targetCoords, unit);
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

        SetOccupant(targetCoords, unit);
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
        if (!IsUsableBenchSlot(slot)) return false; // 예약 슬롯에는 놓을 수 없다

        PokemonUnit occupant = _bench[slot];
        if (occupant == unit) return true;        // 같은 자리 멱등
        if (occupant != null) return false;       // 점유됨 — 스왑은 호출측 책임

        RemoveUnitFromCurrentLocation(unit);
        _bench[slot] = unit;
        unit.isOnBoard = false;

        RevertPlusleMinunOnBench(unit);

        GameEvents.UnitBenched(unit);

        if (unit.data != null)
        {
            CheckEvolution(
                unit.data.id,
                unit.starLevel,
                unit.isTradeEvolved
            );
        }

        return true;
    }

    /// <summary>
    /// 벤치 슬롯에 드롭됐을 때(드래그 콜백). 빈 슬롯이면 배치, 점유 슬롯이면 들어오는 유닛의
    /// 원위치(보드 좌표/벤치 슬롯)와 교체(스왑)한다. 성공 시 true.
    /// </summary>
    public bool TryDropOnBench(PokemonUnit unit, int slot)
    {
        if (unit == null) return false;
        if (!IsUsableBenchSlot(slot)) return false; // 예약 슬롯에 드롭하면 원위치로 돌아간다

        PokemonUnit occupant = _bench[slot];
        if (occupant == unit) return true;            // 같은 자리 멱등
        if (occupant == null) return TryPlaceInBench(unit, slot); // 빈 슬롯 — 일반 배치(+진화 검사)

        // 점유됨 — 들어오는 유닛의 원위치로 occupant를 보내고 교체
        if (TryFindBoardCoords(unit, out HexCoords fromCoords))
        {
            SetOccupant(fromCoords, occupant);
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

        RevertPlusleMinunOnBench(unit);

        GameEvents.UnitBenched(unit);

        if (unit.data != null)
        {
            CheckEvolution(
                unit.data.id,
                unit.starLevel,
                unit.isTradeEvolved
            );
        }

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

    /// <summary>필드(보드)에 유닛 캡 여유가 있는지.</summary>
    public bool HasBoardSpace() => CountUnitsOnBoard() < _unitCap;

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

        if (onBoard) SetOccupant(coords, null);
        else         _bench[slot] = null;

        if (SoundManager.TryGet(out var sellSoundManager)) sellSoundManager.PlaySfx(SoundId.UnitSell);

        GameEvents.UnitSold(unit); // ShopManager(환급) · SynergyManager(재계산) · BoardView(resync)
        Destroy(unit.gameObject);
        return true;
    }

    /// <summary>
    /// 통신교환 전송 성공 시 유닛을 보드/벤치에서 제거한다.
    /// 판매가 아니므로 OnUnitSold를 발행하지 않고,
    /// 장착 아이템과 진화의 돌도 인벤토리로 반환하지 않는다.
    /// 유닛 오브젝트 파괴는 호출한 NetworkManager가 처리한다.
    /// </summary>
    public bool RemoveUnitForTrade(PokemonUnit unit)
    {
        if (unit == null)
            return false;

        if (TryFindBoardCoords(unit, out HexCoords coords))
        {
            SetOccupant(coords, null);
            GameEvents.BoardResyncRequested();

            Debug.Log(
                $"[BoardManager] 통신교환 유닛 보드에서 제거: " +
                $"{unit.data?.pokemonName} ★{unit.starLevel}"
            );

            return true;
        }

        int slot = FindBenchSlot(unit);

        if (slot < 0)
        {
            Debug.LogWarning(
                "[BoardManager] 통신교환 유닛의 보드/벤치 위치를 찾지 못했습니다."
            );

            return false;
        }

        _bench[slot] = null;

        GameEvents.BoardResyncRequested();

        Debug.Log(
            $"[BoardManager] 통신교환 유닛 벤치에서 제거: " +
            $"{unit.data?.pokemonName} ★{unit.starLevel}"
        );

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
        // 예약 슬롯(뒤쪽 _benchSize - _usableBenchSize칸)은 건너뛴다 —
        // 구매·증강 즉시지급·통신교환 수령이 전부 이 메서드를 통해 자리를 찾으므로
        // 여기서 한 번만 막으면 "빈 공간"으로 쓰이는 경로가 전부 닫힌다.
        int limit = UsableBenchSize;
        for (int i = 0; i < limit; i++)
            if (_bench[i] == null) return i;
        return -1;
    }

    /// <summary>유닛을 놓을 수 있는 슬롯 수. 배열 길이를 넘지 않도록 보정해 반환한다.</summary>
    public int UsableBenchSize =>
        _bench != null ? Mathf.Min(_usableBenchSize, _bench.Length) : _usableBenchSize;

    /// <summary>해당 슬롯이 유닛을 놓을 수 있는 칸인지. 예약 슬롯이면 false.</summary>
    public bool IsUsableBenchSlot(int slot) => slot >= 0 && slot < UsableBenchSize;

    /// <summary>벤치에서 해당 유닛 참조를 찾아 비움(있을 때만).</summary>
    private void RemoveFromBenchByRef(PokemonUnit unit)
    {
        int slot = FindBenchSlot(unit);
        if (slot >= 0) _bench[slot] = null;
    }

    /// <summary>
    /// 보드 한 칸의 점유자를 바꾸는 <b>유일한 통로</b>. 논리 상태(_battleField)와 타일 색을 같이 갱신한다.
    /// <c>_battleField[coords] = unit</c>을 직접 쓰면 타일 색이 옛 상태로 남으므로 반드시 이 함수를 쓸 것.
    /// (배치/스왑/판매/교환/합체 등 상태를 바꾸는 경로가 열 곳이 넘어 한 곳으로 모았다)
    /// </summary>
    private void SetOccupant(HexCoords coords, PokemonUnit unit)
    {
        _battleField[coords] = unit;

        if (_tiles.TryGetValue(coords, out HexTile tile) && tile != null)
            tile.SetOccupied(unit != null);
    }

    /// <summary>유닛이 현재 어디(보드/벤치)에 있든 그 자리를 비움.</summary>
    private void RemoveUnitFromCurrentLocation(PokemonUnit unit)
    {
        if (TryFindBoardCoords(unit, out HexCoords coords))
            SetOccupant(coords, null);
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

    /// <summary>
    /// 외부 시스템이 유닛의 종이나 진화 상태를 변경한 뒤
    /// 동일 종·동일 성급 3마리 합체를 다시 검사한다.
    /// </summary>
    public void RecheckEvolution(PokemonUnit unit)
    {
        if (unit == null || unit.data == null)
            return;

        CheckEvolution(
            unit.data.id,
            unit.starLevel,
            unit.isTradeEvolved
        );
    }

    // ──────────────────────────────────────────
    // 합체 (일반 진화 = 동일 종 3개 → 별업, GDD 4.2)
    // ──────────────────────────────────────────

    /// <summary>
    /// 성급 합체에 소비될 유닛과 원래 위치 정보.
    /// </summary>
    private struct EvolutionCandidate
    {
        public PokemonUnit Unit;
        public bool IsOnBoard;
        public HexCoords BoardCoords;
        public int BenchSlot;

        public EvolutionCandidate(
            PokemonUnit unit,
            HexCoords boardCoords)
        {
            Unit = unit;
            IsOnBoard = true;
            BoardCoords = boardCoords;
            BenchSlot = -1;
        }

        public EvolutionCandidate(
            PokemonUnit unit,
            int benchSlot)
        {
            Unit = unit;
            IsOnBoard = false;
            BoardCoords = default;
            BenchSlot = benchSlot;
        }
    }

    /// <summary>310/311/312를 합체 판정에서 같은 유닛으로 취급하기 위한 정규화. 그 외 ID는 그대로 반환.</summary>
    private static int NormalizeSpeciesId(int id)
        => (id == PLUSLE_ID || id == MINUN_ID) ? PLUSLE_MINUN_ID : id;

    /// <summary>
    /// 구매 합체 예외(벤치·필드가 모두 찬 상태에서 부족분만 상점에서 구매) 후보를
    /// 최대 2마리까지 수집한다. CheckEvolution과 동일한 후보 수집 순서·우선순위를 재사용한다.
    ///   - 쇼핑 페이즈: 필드+벤치 모두 후보, 발견 순서(보드 우선)로 최대 2마리.
    ///   - 전투 페이즈: 보호 대상은 필드 유닛뿐이므로 필드는 후보에서 제외하고 벤치만 사용.
    /// 2마리를 채우면 그 이상은 스캔하지 않는다(이번 합체에 쓸 만큼만 확보).
    /// 반환 리스트가 비어있으면 합체 예외 불가, 1개면 신규 2마리, 2개면 신규 1마리가 필요하다는 뜻.
    /// 나머지 동일 종 유닛은 후보로 잡히지 않아 그대로 유지된다(새로운 우선순위 추가 없음).
    /// </summary>
    public List<PokemonUnit> FindPurchaseMergeCandidates(int speciesId, int starLevel)
    {
        var found = new List<PokemonUnit>();

        if (!_isBattlePhase)
        {
            foreach (var kv in _battleField)
            {
                PokemonUnit unit = kv.Value;

                if (unit == null || unit.data == null)
                    continue;

                if (NormalizeSpeciesId(unit.data.id) != NormalizeSpeciesId(speciesId) ||
                    unit.starLevel != starLevel)
                {
                    continue;
                }

                found.Add(unit);

                if (found.Count >= 2)
                    return found;
            }
        }

        for (int slot = 0; slot < _bench.Length; slot++)
        {
            PokemonUnit unit = _bench[slot];

            if (unit == null || unit.data == null)
                continue;

            if (NormalizeSpeciesId(unit.data.id) != NormalizeSpeciesId(speciesId) ||
                unit.starLevel != starLevel)
            {
                continue;
            }

            found.Add(unit);

            if (found.Count >= 2)
                return found;
        }

        return found;
    }

    /// <summary>
    /// 구매 합체 예외 실행. 이미 board/bench에 있는 existingUnits(1~2마리)와 아직 어디에도
    /// 놓이지 않은 신규 구매 유닛 newUnits(합쳐서 총 3마리가 되는 나머지)를 묶어,
    /// existingUnits 중 CheckEvolution과 동일한 목적지 우선순위(필드 후보 우선, 그 안에서 무작위)로
    /// 고른 자리에 상위 성급 유닛을 생성한다. 실제 병합은 CheckEvolution과 공유하는
    /// ExecuteMerge로 처리한다(대규모 병합 로직 중복 없음).
    /// existingUnits 중 하나라도 현재 위치를 찾지 못하면(호출 사이 상태가 바뀐 경우) 실패한다.
    /// </summary>
    public bool TryMergePurchasedCopies(
        IReadOnlyList<PokemonUnit> existingUnits,
        IReadOnlyList<PokemonUnit> newUnits)
    {
        if (_isEvolving)
            return false;

        if (existingUnits == null || newUnits == null)
            return false;

        if (existingUnits.Count < 1 || existingUnits.Count > 2)
            return false;

        if (existingUnits.Count + newUnits.Count != 3)
            return false;

        var realCandidates = new List<EvolutionCandidate>();

        foreach (PokemonUnit existing in existingUnits)
        {
            if (existing == null)
                return false;

            if (TryFindBoardCoords(existing, out HexCoords coords))
            {
                realCandidates.Add(new EvolutionCandidate(existing, coords));
                continue;
            }

            int slot = FindBenchSlot(existing);

            if (slot < 0)
                return false;

            realCandidates.Add(new EvolutionCandidate(existing, slot));
        }

        foreach (PokemonUnit fresh in newUnits)
        {
            if (fresh == null)
                return false;
        }

        // CheckEvolution과 동일한 목적지 우선순위: 필드 후보가 있으면 그중에서, 없으면
        // 기존 유닛(실좌표가 있는 후보) 중에서만 무작위로 고른다 — 가상 후보는 목적지가 될 수 없다.
        var boardDestinations = new List<EvolutionCandidate>();

        foreach (EvolutionCandidate candidate in realCandidates)
        {
            if (candidate.IsOnBoard)
                boardDestinations.Add(candidate);
        }

        EvolutionCandidate destination =
            boardDestinations.Count > 0
                ? boardDestinations[Random.Range(0, boardDestinations.Count)]
                : realCandidates[Random.Range(0, realCandidates.Count)];

        _isEvolving = true;

        var consumed = new List<EvolutionCandidate>(realCandidates);

        foreach (PokemonUnit fresh in newUnits)
            consumed.Add(new EvolutionCandidate(fresh, -1));

        ExecuteMerge(consumed, destination, existingUnits[0].starLevel);

        return true;
    }

    /// <summary>
    /// 벤치의 플러시와마이농(310)을 필드에 올릴 때, 이미 필드에 확정된 폼(플러시/마이농)이
    /// 있으면 그 폼으로 자동전환한다. 없으면 그대로 두어(310 유지) 선택 대기 상태로 남긴다.
    ///
    /// GameEvents.UnitChanged는 발행하지 않는다(notifyChange:false) — 바로 뒤에 이어지는
    /// UnitPlaced 발행/CheckEvolution 호출과 순서가 꼬이거나 중복 실행되지 않도록
    /// 호출측인 여기서 억제한다.
    /// </summary>
    private void AutoConvertPlusleMinunOnPlace(PokemonUnit unit)
    {
        if (unit == null || unit.data == null || unit.data.id != PLUSLE_MINUN_ID)
            return;

        int existingFormId = 0;

        foreach (var kv in _battleField)
        {
            PokemonUnit other = kv.Value;

            if (other == null || other == unit || other.data == null || !other.isOnBoard)
                continue;

            if (other.data.id == PLUSLE_ID || other.data.id == MINUN_ID)
            {
                existingFormId = other.data.id;
                break;
            }
        }

        if (existingFormId == 0)
            return;

        PokemonData formData =
            PokemonDatabase.Instance != null
                ? PokemonDatabase.Instance.GetById(existingFormId)
                : null;

        if (formData != null)
            unit.TrySetForm(formData, notifyChange: false);
    }

    /// <summary>
    /// 필드의 플러시(311)/마이농(312)을 벤치로 내리면 플러시와마이농(310)으로 복원한다.
    ///
    /// GameEvents.UnitChanged는 발행하지 않는다(notifyChange:false) — 바로 뒤에 이어지는
    /// UnitBenched 발행/CheckEvolution 호출과 순서가 꼬이거나 중복 실행되지 않도록
    /// 호출측인 여기서 억제한다.
    /// </summary>
    private void RevertPlusleMinunOnBench(PokemonUnit unit)
    {
        if (unit == null || unit.data == null)
            return;

        if (unit.data.id != PLUSLE_ID && unit.data.id != MINUN_ID)
            return;

        PokemonData baseData =
            PokemonDatabase.Instance != null
                ? PokemonDatabase.Instance.GetById(PLUSLE_MINUN_ID)
                : null;

        if (baseData != null)
            unit.TrySetForm(baseData, notifyChange: false);
    }

    /// <summary>
    /// 보드와 벤치에서 현재 종 ID와 성급이 같은 유닛 3마리를 찾아
    /// 모두 제거한 뒤 다음 성급 유닛을 신규 생성한다.
    ///
    /// 페이즈별 규칙:
    /// - 쇼핑 페이즈: 위치 무관, 발견 순서(보드 우선) 앞쪽 3마리를 즉시 소비.
    /// - 전투 페이즈: 보호 대상은 "필드에서 싸우는 유닛"뿐이므로, 후보 중 벤치 유닛만으로
    ///   3마리를 채울 수 있으면 벤치 유닛 3마리를 즉시 합체한다. 벤치만으로는 3마리를
    ///   못 채워 필드 유닛이 반드시 섞여야 하는 경우엔 AddPendingStarEvolution으로 등록해
    ///   쇼핑 페이즈 진입 시(ApplyPendingStarEvolutions) 일괄 처리한다.
    ///
    /// 위치 선정 규칙(결과 유닛 배치 위치):
    /// - 소비 대상 중 보드 유닛이 있으면 보드 위치 중 무작위
    /// - 전부 벤치라면 벤치 슬롯 중 무작위
    ///
    /// 장착물 처리:
    /// - 진화의 돌이 있으면 1개는 결과 유닛에 반드시 유지
    /// - 중복 진화의 돌은 인벤토리 반환
    /// - 남은 슬롯에 일반 아이템을 무작위 장착
    /// - 장착되지 않은 일반 아이템은 인벤토리 반환
    ///
    /// 반환값 = 이번 호출에서 실제로 합체가 일어났는지. 연쇄 진화(1성×3 → 2성 →
    /// 그 결과 2성이 3마리 → 3성)에서 중간 단계를 건너뛰고 최종 결과만 연출하기
    /// 위해 호출측이 이 값을 본다.
    /// </summary>
    private bool CheckEvolution(
        int speciesId,
        int starLevel,
        bool isTradeEvolved)
    {
        if (_isEvolving)
            return false;

        if (starLevel >= 3)
            return false;

        var candidates = new List<EvolutionCandidate>();

        // 현재 pokemonId와 성급만 합체 조건으로 사용한다.
        // 돌 진화 여부와 통신진화 여부는 합체 자체를 막지 않는다.
        foreach (var kv in _battleField)
        {
            PokemonUnit unit = kv.Value;

            if (unit == null || unit.data == null)
                continue;

            if (NormalizeSpeciesId(unit.data.id) != NormalizeSpeciesId(speciesId) ||
                unit.starLevel != starLevel)
            {
                continue;
            }

            candidates.Add(
                new EvolutionCandidate(unit, kv.Key)
            );
        }

        for (int slot = 0; slot < _bench.Length; slot++)
        {
            PokemonUnit unit = _bench[slot];

            if (unit == null || unit.data == null)
                continue;

            if (NormalizeSpeciesId(unit.data.id) != NormalizeSpeciesId(speciesId) ||
                unit.starLevel != starLevel)
            {
                continue;
            }

            candidates.Add(
                new EvolutionCandidate(unit, slot)
            );
        }

        if (candidates.Count < 3)
            return false;

        // 전투 중 보호 대상은 "필드에서 싸우는 유닛"뿐이다.
        // 후보 중 벤치 유닛만으로 3마리가 채워지면 필드 유닛을 건드리지 않고 즉시 합체하고,
        // 벤치만으로 3마리를 못 채워 필드 유닛이 반드시 섞여야 한다면 쇼핑 페이즈까지 대기 등록한다.
        List<EvolutionCandidate> consumed;

        if (_isBattlePhase)
        {
            var benchOnlyCandidates = new List<EvolutionCandidate>();

            foreach (EvolutionCandidate candidate in candidates)
            {
                if (!candidate.IsOnBoard)
                    benchOnlyCandidates.Add(candidate);
            }

            if (benchOnlyCandidates.Count < 3)
            {
                AddPendingStarEvolution(speciesId, starLevel);
                return false;
            }

            // 벤치 후보를 우선 선택해 필드 유닛을 건드리지 않고 즉시 합체한다.
            consumed = new List<EvolutionCandidate>
            {
                benchOnlyCandidates[0],
                benchOnlyCandidates[1],
                benchOnlyCandidates[2]
            };
        }
        else
        {
            // 쇼핑 페이즈: 위치 무관, 기존과 동일하게 발견 순서(보드 우선) 앞쪽 3마리를 소비한다.
            consumed = new List<EvolutionCandidate>
            {
                candidates[0],
                candidates[1],
                candidates[2]
            };
        }

        _isEvolving = true;

        // 결과 유닛이 생성될 위치 후보.
        var boardDestinations = new List<EvolutionCandidate>();

        foreach (EvolutionCandidate candidate in consumed)
        {
            if (candidate.IsOnBoard)
                boardDestinations.Add(candidate);
        }

        EvolutionCandidate destination;

        if (boardDestinations.Count > 0)
        {
            int randomIndex = Random.Range(
                0,
                boardDestinations.Count
            );

            destination = boardDestinations[randomIndex];
        }
        else
        {
            int randomIndex = Random.Range(0, consumed.Count);
            destination = consumed[randomIndex];
        }

        ExecuteMerge(consumed, destination, starLevel);

        return true;
    }

    /// <summary>
    /// CheckEvolution과 구매 합체 예외(TryMergePurchasedCopies) 양쪽이 공유하는 실제 병합 실행부.
    /// consumed 3마리(장착물·상태 수거 후 소멸)와 destination(결과 유닛이 들어갈 자리)을
    /// 그대로 받아 처리한다 — 후보 수집·목적지 선정 로직은 각 호출측 책임이다.
    /// </summary>
    private void ExecuteMerge(
        List<EvolutionCandidate> consumed,
        EvolutionCandidate destination,
        int starLevel)
    {
        PokemonUnit templateUnit = destination.Unit;
        PokemonData currentSpeciesData = templateUnit.data;

        // 세 유닛의 일반 아이템과 진화의 돌을 모두 수거한다.
        var collectedItems = new List<ItemData>();
        var collectedStones = new List<EvolutionStoneData>();

        EvolutionStoneData retainedStone = null;
        PokemonData retainedPreStoneData = null;

        bool resultTradeEvolved = false;
        bool resultEvolutionLocked = false;

        float resultHeroStatMultiplier = 1f;
        string resultRoleOverride = null;
        PokemonSkillData resultGrantedSkill = null;
        int resultGrantedSkillManaCost = 0;
        bool resultHasHeroBerry = false;
        string resultAttackVfxIdOverride = null;
        int resultAttackRangeOverride = PokemonUnit.NoRangeOverride;

        foreach (EvolutionCandidate candidate in consumed)
        {
            PokemonUnit unit = candidate.Unit;

            if (unit == null)
                continue;

            unit.DetachEquipmentForMerge(
                out EvolutionStoneData stone,
                out PokemonData preStoneData,
                out List<ItemData> items
            );

            if (items != null)
            {
                foreach (ItemData item in items)
                {
                    if (item != null)
                        collectedItems.Add(item);
                }
            }

            if (stone != null)
            {
                collectedStones.Add(stone);

                // 결과 유닛에 유지할 첫 번째 돌과 원본 종 정보.
                if (retainedStone == null)
                {
                    retainedStone = stone;
                    retainedPreStoneData = preStoneData;
                }
            }

            // 현재 종이 같다면 통신진화 상태가 하나라도 있을 경우 유지한다.
            if (unit.isTradeEvolved)
                resultTradeEvolved = true;

            // 영웅증강 상태도 합체 결과가 잃지 않도록 유지한다.
            if (unit.evolutionLocked)
                resultEvolutionLocked = true;

            if (unit.heroStatMultiplier > resultHeroStatMultiplier)
                resultHeroStatMultiplier = unit.heroStatMultiplier;

            if (!string.IsNullOrEmpty(unit.roleOverride))
                resultRoleOverride = unit.roleOverride;

            if (unit.grantedSkill != null)
            {
                resultGrantedSkill = unit.grantedSkill;
                resultGrantedSkillManaCost =
                    unit.grantedSkillManaCost;
            }

            if (unit.hasHeroBerry)
                resultHasHeroBerry = true;

            if (!string.IsNullOrEmpty(unit.attackVfxIdOverride))
                resultAttackVfxIdOverride = unit.attackVfxIdOverride;

            if (unit.attackRangeOverride != PokemonUnit.NoRangeOverride)
                resultAttackRangeOverride = unit.attackRangeOverride;
        }

        // 신규 생성 전에 기존 3마리의 논리 위치를 전부 비운다.
        foreach (EvolutionCandidate candidate in consumed)
        {
            if (candidate.IsOnBoard)
            {
                SetOccupant(candidate.BoardCoords, null);
            }
            else if (candidate.BenchSlot >= 0 &&
                     candidate.BenchSlot < _bench.Length)
            {
                _bench[candidate.BenchSlot] = null;
            }
        }

        /*
         * 목적지로 선정된 기존 유닛을 템플릿으로 복제한다.
         *
         * 기존 유닛을 생존시키는 것이 아니라,
         * 새로운 GameObject와 PokemonUnit 인스턴스를 생성한다.
         */
        PokemonUnit evolvedUnit = Instantiate(
            templateUnit,
            templateUnit.transform.parent
        );

        evolvedUnit.name =
            $"{currentSpeciesData.pokemonName}_Star_{starLevel + 1}";

        // Instantiate가 복사한 장착 상태를 확실히 초기화한다.
        evolvedUnit.items = new List<ItemData>();
        evolvedUnit.equippedStone = null;
        evolvedUnit.preStoneData = null;

        evolvedUnit.starLevel = Mathf.Clamp(
            starLevel + 1,
            1,
            3
        );

        evolvedUnit.isTradeEvolved = resultTradeEvolved;
        evolvedUnit.evolutionLocked = resultEvolutionLocked;
        evolvedUnit.heroStatMultiplier =
            resultHeroStatMultiplier;
        evolvedUnit.roleOverride =
            resultRoleOverride;
        evolvedUnit.grantedSkill =
            resultGrantedSkill;
        evolvedUnit.grantedSkillManaCost =
            resultGrantedSkillManaCost;
        evolvedUnit.hasHeroBerry =
            resultHasHeroBerry;
        evolvedUnit.attackVfxIdOverride =
            resultAttackVfxIdOverride;
        evolvedUnit.attackRangeOverride =
            resultAttackRangeOverride;

        /*
         * 진화의 돌로 만들어진 현재 종이면 종을 추가로 변경하지 않는다.
         *
         * 예:
         * 라이츄 1성 × 3
         * → 라이츄 2성
         *
         * 피카츄나 다음 evolvesInto 종으로 바뀌면 안 된다.
         */
        PokemonData resultData = currentSpeciesData;

        if (retainedStone == null &&
            !resultEvolutionLocked &&
            !resultTradeEvolved)
        {
            // 합체되는 종 자신(currentSpeciesData, 예: 골뱃)이 이미 활성화된 통신진화 원본인지
            // evolvesIntoEn 유무보다 먼저 확인한다(2026-08 코드리뷰 대응). 골뱃/윤겔라/데구리/야돈/
            // 고우스트/롱스톤/스라크처럼 통신진화 대상 중간체는 pokemon_data의 evolvesIntoEn이
            // 비어 있어(자연진화 다음 단계가 없음) 아래 evolvesIntoEn 분기에 아예 도달하지 못한다.
            // 전투 중 필드 보호로 미변환(pending) 상태로 남아 있던 같은 종 3마리가 먼저 합체되면
            // (AddPendingStarEvolution 경로가 AddPendingTradeEvolution보다 먼저 처리되는 경우 등,
            // 두 대기열은 서로 다른 GameEvents.OnPhaseChanged 구독자라 순서 보장이 없다) 이 분기가
            // 없으면 리다이렉트를 영영 못 타 예: 골뱃×3 → (의도한 크로뱃이 아니라) 그냥 3성 골뱃이
            // 나온다. ShopManager 내부 Dictionary는 직접 참조하지 않고 기존 공개 조회 API만
            // 재사용한다 — 새 시스템/새 API 없음.
            if (GameManager.TryGet(out var gm) && gm.Shop != null &&
                gm.Shop.TryResolveActiveTradeEvolution(currentSpeciesData.id, out PokemonData ownTradeEvolvedData))
            {
                resultData = ownTradeEvolvedData;
                evolvedUnit.isTradeEvolved = true;
            }
            else
            {
                string evolvedEn =
                    currentSpeciesData.evolvesIntoEn;

                if (!string.IsNullOrEmpty(evolvedEn))
                {
                    PokemonData nextData =
                        PokemonDatabase.Instance != null
                            ? PokemonDatabase.Instance
                                .GetByNameEn(evolvedEn)
                            : null;

                    if (nextData != null)
                    {
                        resultData = nextData;

                        // 방금 결정된 일반 진화 결과(nextData, 예: 골뱃)가 현재 플레이어에게 활성화된
                        // 통신진화 원본이면(예: 골뱃→크로뱃 활성화) 그 통신진화체로 최종 결과를 대신한다 —
                        // 주뱃 3마리 합체가 골뱃을 거치지 않고 곧바로 크로뱃이 되는 지점(상점 하위 1성이
                        // 통신진화 대상인 계열: 주뱃/캐이시/꼬마돌/고오스 등 전부 이 한 분기로 처리됨,
                        // 포켓몬별 하드코딩 없음). ShopManager 내부 Dictionary는 직접 참조하지 않고
                        // 공개 조회 API만 쓴다. 이 블록 자체가 이미 retainedStone==null &&
                        // !resultEvolutionLocked && !resultTradeEvolved 안에서만 실행되므로(위 조건문),
                        // 돌 진화·영웅증강 고정·이미 통신진화된 경우에는 이 분기가 절대 개입하지 않는다.
                        if (GameManager.TryGet(out var gm2) && gm2.Shop != null &&
                            gm2.Shop.TryResolveActiveTradeEvolution(nextData.id, out PokemonData activeTradeEvolvedData))
                        {
                            resultData = activeTradeEvolvedData;
                            evolvedUnit.isTradeEvolved = true;
                        }
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[Evolve] 진화체 '{evolvedEn}'가 " +
                            "PokemonDatabase에 없습니다. 종을 유지합니다."
                        );
                    }
                }
            }
        }

        evolvedUnit.data = resultData;

        /*
         * 돌 진화체라면 진화의 돌 하나를 먼저 복원한다.
         * 돌이 장비 슬롯 하나를 차지하므로 일반 아이템은 최대 1개만 장착된다.
         */
        if (retainedStone != null)
        {
            bool restored =
                evolvedUnit.RestoreStoneStateAfterMerge(
                    retainedStone,
                    retainedPreStoneData
                );

            if (!restored)
            {
                Debug.LogError(
                    "[Evolve] 신규 유닛에 진화의 돌 상태 복원 실패."
                );

                if (_itemManager != null)
                    _itemManager.RecoverMergedStone(
                        retainedStone
                    );

                retainedStone = null;
            }
        }

        // 유지한 돌을 제외한 나머지 돌은 모두 인벤토리로 반환한다.
        bool skippedRetainedStone = false;

        foreach (EvolutionStoneData stone in collectedStones)
        {
            if (stone == null)
                continue;

            if (!skippedRetainedStone &&
                retainedStone != null &&
                stone == retainedStone)
            {
                skippedRetainedStone = true;
                continue;
            }

            if (_itemManager != null)
            {
                _itemManager.RecoverMergedStone(stone);
            }
            else
            {
                Debug.LogError(
                    $"[Evolve] ItemManager가 없어 " +
                    $"진화의 돌 '{stone.stoneName}' 반환 실패"
                );
            }
        }

        // 일반 아이템 순서를 무작위로 섞는다.
        ShuffleItems(collectedItems);

        // 돌이 있으면 빈 슬롯 1개, 돌이 없으면 빈 슬롯 2개.
        int itemEquipCount = Mathf.Min(
                PokemonUnit.MaxItemSlots - evolvedUnit.UsedSlots,
                collectedItems.Count
            );

        for (int i = 0; i < collectedItems.Count; i++)
        {
            ItemData item = collectedItems[i];

            if (item == null)
                continue;

            if (i < itemEquipCount)
            {
                bool equipped =
                    evolvedUnit.TryEquipItem(item);

                if (!equipped)
                {
                    Debug.LogWarning(
                        $"[Evolve] '{item.itemName}' 재장착 실패 — " +
                        "인벤토리로 반환합니다."
                    );

                    if (_itemManager != null)
                        _itemManager.RecoverMergedItem(item);
                }
            }
            else
            {
                if (_itemManager != null)
                {
                    _itemManager.RecoverMergedItem(item);
                }
                else
                {
                    Debug.LogError(
                        $"[Evolve] ItemManager가 없어 " +
                        $"아이템 '{item.itemName}' 반환 실패"
                    );
                }
            }
        }

        // 신규 유닛을 선택된 위치에 등록한다.
        if (destination.IsOnBoard)
        {
            SetOccupant(destination.BoardCoords, evolvedUnit);

            evolvedUnit.isOnBoard = true;
        }
        else
        {
            _bench[destination.BenchSlot] =
                evolvedUnit;

            evolvedUnit.isOnBoard = false;
        }

        // 기존 유닛 3마리는 모두 제거한다.
        foreach (EvolutionCandidate candidate in consumed)
        {
            if (candidate.Unit != null)
                Destroy(candidate.Unit.gameObject);
        }

        // evolvedUnit은 진화 전 유닛의 Instantiate 복제라 시각 자식이 이전 모델 그대로다.
        // data 스왑(위 resultData 대입) 이후 반드시 다시 만들어야 진화체 모델이 보인다.
        // (master의 survivor 방식에서 dca63495로 고친 문제 — 신규 유닛 방식에도 동일하게 필요)
        evolvedUnit.RefreshVisual();
        evolvedUnit.ResetForBattle();

        _isEvolving = false;

        Debug.Log(
            $"[Evolve] {currentSpeciesData.pokemonName} " +
            $"{starLevel}성 3개 제거 → " +
            $"{evolvedUnit.data.pokemonName} " +
            $"{evolvedUnit.starLevel}성 신규 생성 / " +
            $"돌 {(evolvedUnit.equippedStone != null ? 1 : 0)}개 / " +
            $"일반 장비 {evolvedUnit.items.Count}개"
        );

        // 이벤트 발화(시너지 재계산 + BoardView 위치 재배치. 모델 교체는 위 RefreshVisual에서 이미 처리)
        if (evolvedUnit.isOnBoard)
            GameEvents.UnitPlaced(evolvedUnit);
        else
            GameEvents.UnitBenched(evolvedUnit);

        // 연쇄(예: 데구리 2성 3개 → 딱구리 3성). 진화로 바뀐 새 종 id로 재검사.
        bool chained = CheckEvolution(
            evolvedUnit.data.id,
            evolvedUnit.starLevel,
            evolvedUnit.isTradeEvolved
        );

        // 연출은 (1) 재배치 뒤에, (2) 연쇄가 끝난 뒤에 한 번만.
        //  (1) UnitPlaced가 BoardView 위치를 갱신하므로 그 전에 발화하면 합체 전 좌표에 뜬다.
        //  (2) 연쇄가 더 이어졌다면 더 깊은 호출이 최종 결과로 이미 발화했다.
        //      여기서 또 발화하면 3성 달성 시 중간 2성 자리에서도 이펙트가 난다.
        if (!chained) GameEvents.UnitEvolved(evolvedUnit, true);
    }

    /// <summary>
    /// 일반 아이템 목록을 Fisher-Yates 방식으로 무작위 섞는다.
    /// 중복 아이템도 각 항목을 개별 장비로 취급한다.
    /// </summary>
    private void ShuffleItems(List<ItemData> items)
    {
        if (items == null)
            return;

        for (int i = items.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);

            ItemData temp = items[i];
            items[i] = items[randomIndex];
            items[randomIndex] = temp;
        }
    }
}
