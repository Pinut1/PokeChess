using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 파트너(상대 클라이언트)의 보드를 읽기 전용 미러로 렌더하는 표현 레이어.
/// NetworkManager가 RPC로 받은 보드 스냅샷을 GameEvents.OnOpponentBoardChanged로 흘려주면,
/// 이를 받아 내 보드 옆(오프셋)에 그린다. 게임 로직 없음 — 순수 시각화.
///
/// speciesId를 PokemonDatabase로 해석해 modelPrefab이 있으면 실제 종 모델을,
/// 없거나 ID가 잘못됐으면 기존 캡슐 폴백(성급 색상)을 표시한다.
/// 미러는 표시 전용이므로 모델의 콜라이더는 전부 제거해 드래그 레이캐스트에 개입하지 않는다.
///
/// 위치는 내 BoardManager의 좌표 변환을 그대로 빌려 쓴다(같은 그리드 형상이므로).
/// 더블업처럼 쇼핑 중에도 파트너 판이 옆에 보이게 한다.
/// 규모가 작아(보드 ≤28 + 벤치 ≤9) 스냅샷마다 전체 재배치로 충분하며,
/// 비주얼 오브젝트는 종별 풀로 재사용해 스냅샷마다 Instantiate가 반복되지 않게 한다.
/// </summary>
public class OpponentBoardView : MonoBehaviour
{
    /// <summary>
    /// 쇼핑 중 파트너 보드 유닛 1기의 표시 정보(읽기 전용). UnitStatusBarHud가 쇼핑 단계 파트너
    /// HP/마나 바를 그릴 때만 이 값을 읽는다. species/items는 ScriptableObject 에셋 참조라 값을
    /// 바꿔도 게임 데이터에 영향이 없고(에셋 자체를 바꾸지 않는 한), visual은 이 오브젝트가 이미
    /// 관리하는 미러 전용 비주얼(콜라이더 제거·PartnerSpectateVisual Layer)이라 실제 PokemonUnit/
    /// Board는 어디에도 노출하지 않는다.
    /// </summary>
    public readonly struct PartnerBoardUnitView
    {
        public readonly Transform visual;
        public readonly PokemonData species;
        public readonly int starLevel;
        public readonly IReadOnlyList<ItemData> items;

        /// <summary>영웅증강 등으로 바뀐 런타임 역할(BoardSnapshot.Entry.roleOverride 그대로). ""=오버라이드 없음.</summary>
        public readonly string roleOverride;
        /// <summary>영웅증강 등으로 바뀐 런타임 사거리. PokemonUnit.NoRangeOverride(-1)=오버라이드 없음.</summary>
        public readonly int attackRangeOverride;
        /// <summary>영웅증강 스탯 배수. 기본값 1f=배수 없음.</summary>
        public readonly float heroStatMultiplier;
        /// <summary>통신진화로 얻은 종인지(BoardSnapshot.Entry.isTradeEvolved 그대로). PokemonUnit.IsSpecialEvolved
        /// 판정(특수 진화 배율표 1.0/2.0/3.0 적용 여부)에 그대로 대응한다.</summary>
        public readonly bool isTradeEvolved;
        /// <summary>장착 중인 진화의 돌(BoardSnapshot.Entry.equippedStoneId를 EvolutionStoneDatabase로 해석).
        /// 미장착이면 null.</summary>
        public readonly EvolutionStoneData equippedStone;
        /// <summary>PokemonUnit.EffectiveManaCost(BoardSnapshot.Entry.effectiveManaCost 그대로). 0이면
        /// 마나바를 표시하지 않는다 — 영웅증강으로 스킬을 주입받은 유닛(원본 종 manaCost=0)도 정확히
        /// 반영하기 위해 종의 manaCost 대신 이 값을 쓴다.</summary>
        public readonly int effectiveManaCost;

        public PartnerBoardUnitView(Transform visual, PokemonData species, int starLevel, IReadOnlyList<ItemData> items,
                                    string roleOverride, int attackRangeOverride, float heroStatMultiplier,
                                    bool isTradeEvolved, EvolutionStoneData equippedStone, int effectiveManaCost)
        {
            this.visual = visual;
            this.species = species;
            this.starLevel = starLevel;
            this.items = items;
            this.roleOverride = roleOverride;
            this.attackRangeOverride = attackRangeOverride;
            this.heroStatMultiplier = heroStatMultiplier;
            this.isTradeEvolved = isTradeEvolved;
            this.equippedStone = equippedStone;
            this.effectiveManaCost = effectiveManaCost;
        }
    }

    // Render()가 스냅샷마다 새로 채운다 — _active와 동일한 순서/생명주기(ReleaseActive에서 함께 비움).
    private readonly List<PartnerBoardUnitView> _activeUnitViews = new();

    /// <summary>지금 화면에 떠 있는 파트너 보드 유닛들의 표시 정보(읽기 전용). 미러 전투(BattleUnit)와
    /// 달리 쇼핑 단계는 PokemonUnit이 아예 없으므로, 이 목록이 그 자리를 대신한다.</summary>
    public IReadOnlyList<PartnerBoardUnitView> ActiveUnitViews => _activeUnitViews;


    [Header("파트너 보드 표시 위치(월드 오프셋)")]
    [Tooltip("내 보드 기준 파트너 보드를 띄울 오프셋. BattleManager의 적 미러(Z+10)와 겹치지 않게.")]
    [SerializeField] private Vector3 _boardOffset = new Vector3(0f, 0f, 14f);
    [SerializeField] private float _unitHeight = 0.5f;

    [Header("성급 색상 (1/2/3성) — 캡슐 폴백 전용")]
    [SerializeField] private Color[] _starColors =
    {
        new Color(0.6f, 0.8f, 1f),  // 1성
        new Color(0.4f, 0.6f, 1f),  // 2성
        new Color(0.2f, 0.3f, 1f),  // 3성
    };

    /// <summary>캡슐 폴백을 종별 풀에 넣을 때 쓰는 키(실제 speciesId와 충돌하지 않는 음수).</summary>
    private const int FALLBACK_POOL_KEY = -1;

    /// <summary>종별 비활성 비주얼 풀. 키 = speciesId(모델) 또는 FALLBACK_POOL_KEY(캡슐).</summary>
    private readonly Dictionary<int, Stack<GameObject>> _poolBySpecies = new();

    /// <summary>현재 스냅샷으로 활성화된 비주얼과 풀 키.</summary>
    private readonly List<(int poolKey, GameObject go)> _active = new();

    /// <summary>해석 실패를 이미 경고한 speciesId — 스냅샷마다 반복 경고(스팸) 방지.</summary>
    private readonly HashSet<int> _warnedSpecies = new();

    /// <summary>파트너 미러 전투 QA 테스트 등 다른 곳에서 같은 위치 기준을 쓰도록 공개. 두 관전 Layer
    /// (LocalGameplayVisual/PartnerSpectateVisual)가 모두 준비되면 Layer/CullingMask로 화면을 분리하므로
    /// 더 이상 좌표를 밀 필요가 없어 0을 반환하고, 하나라도 없으면 기존 오프셋을 그대로 반환한다
    /// (겹침 방지 폴백). PartnerSpectateView의 카메라 위치, BattleManager 미러 인스턴스의
    /// _visualOffset(PartnerBattleMirrorController.BoardOffset → FindBoardOffset 경유)이 전부 이
    /// 값 하나로 연동되므로, 오프셋을 각 호출부에서 따로 지우지 않고 이 한 곳에서만 0/실값을 가른다.</summary>
    public Vector3 BoardOffset => ArePartnerLayersReady(out _, out _) ? Vector3.zero : _boardOffset;

    private const string LOCAL_LAYER_NAME = "LocalGameplayVisual";
    private const string PARTNER_LAYER_NAME = "PartnerSpectateVisual";

    /// <summary>Layer 누락 경고를 최초 1회만 남기기 위한 플래그(스냅샷마다 반복 로그 방지).</summary>
    private bool _layerWarningLogged;

    /// <summary>
    /// 두 관전 전용 Layer가 Unity Editor에 모두 추가돼 있는지 확인한다. PartnerBattleMirrorController가
    /// 이 판정을 그대로 가져다 써서(카메라 CullingMask 조정 등) 판정 기준을 한 곳에서만 계산한다.
    /// 하나라도 없으면 Layer/CullingMask 분리와 오프셋 제거를 전혀 적용하지 않고 기존 BoardOffset
    /// 방식으로 안전하게 계속 동작한다(부분 적용으로 인한 로컬/파트너 비주얼 겹침 방지).
    /// </summary>
    public bool ArePartnerLayersReady(out int localLayer, out int partnerLayer)
    {
        localLayer = LayerMask.NameToLayer(LOCAL_LAYER_NAME);
        partnerLayer = LayerMask.NameToLayer(PARTNER_LAYER_NAME);
        bool ready = localLayer >= 0 && partnerLayer >= 0;

        if (!ready && !_layerWarningLogged)
        {
            Debug.LogError($"[OpponentBoardView] Layer 누락 — '{LOCAL_LAYER_NAME}'({localLayer}), " +
                            $"'{PARTNER_LAYER_NAME}'({partnerLayer}). Unity Editor에 두 Layer를 추가할 " +
                            "때까지 기존 BoardOffset 오프셋 방식으로 동작합니다(Layer/CullingMask 분리 미적용).");
            _layerWarningLogged = true;
        }

        return ready;
    }

    /// <summary>true면 Render가 필드(onBoard=true) 엔트리만 그리지 않는다(파트너 미러 전투와 필드가
    /// 동시 표시되는 것 방지용). 벤치(onBoard=false)는 미러 전투 중에도 계속 BoardSnapshot으로
    /// 실시간 표시해야 하므로 이 플래그의 영향을 받지 않는다.</summary>
    private bool _suppressed;

    /// <summary>Render가 마지막으로 받은 스냅샷. 억제 해제 시 다시 그리기 위해 보관.</summary>
    private BoardSnapshot _lastSnapshot;

    /// <summary>파트너 프리뷰 시각 오브젝트 전용 루트(런타임 생성). 이 컴포넌트가 붙은 GameObject(씬에서는
    /// NetworkManager와 같은 오브젝트)를 그대로 부모로 쓰면 그 오브젝트에 달린 다른 컴포넌트/자식과 섞일 수
    /// 있어, 풀링된 비주얼만 담는 전용 자식 하나를 따로 둔다.</summary>
    private Transform _visualRoot;

    /// <summary>파트너 관전용 아군 헥스 타일 + 벤치 받침 전용 루트(런타임 생성). 유닛 풀링 루트
    /// (_visualRoot/_active)와 완전히 분리해, SetSuppressed(파트너 "유닛"만 숨김)나 스냅샷 재배치가
    /// 이 정적인 보드 배경에 영향을 주지 않게 한다. transform의 자식이라 컴포넌트 파괴 시(Destroy)
    /// Unity가 자동으로 함께 정리한다(_visualRoot와 동일 원칙 — 별도 OnDestroy 불필요).</summary>
    private Transform _boardVisualRoot;

    /// <summary>파트너 아군 타일/벤치 받침을 이미 만들었는지. OnEnable이 여러 번 호출되거나
    /// Render가 스냅샷마다 반복 호출돼도 중복 생성되지 않도록 막는다.</summary>
    private bool _boardVisualsBuilt;

    /// <summary>스냅샷을 한 번이라도 수신했는지. entries.Count(빈 스냅샷 여부)와는 다른 축이다 —
    /// PartnerSpectateView가 "아직 수신 전(준비 중)"과 "빈 필드 정상 수신"을 구분하는 데 쓴다.</summary>
    public bool HasSnapshot => _lastSnapshot != null;

    /// <summary>마지막으로 받은 파트너 BoardSnapshot(읽기 전용). 인벤토리 섹션(inventoryItemIds/
    /// inventoryStoneIds/hasRemover/reforgerCount)을 파트너 인벤토리 표시(ItemInventoryUI)가 읽는다.
    /// 별도 가공 없이 그대로 노출 — 이 값을 바꿔도 실제 파트너 상태에는 전혀 영향이 없다(내가 받은
    /// 사본일 뿐이며, 이 클래스 자신도 값을 바꾸지 않고 오직 다음 스냅샷으로 통째로 교체한다).</summary>
    public BoardSnapshot LastSnapshot => _lastSnapshot;

    private Transform EnsureVisualRoot()
    {
        if (_visualRoot == null)
        {
            var go = new GameObject("OpponentBoardVisualRoot");
            go.transform.SetParent(transform, false);
            _visualRoot = go.transform;
        }
        return _visualRoot;
    }

    private void OnEnable()
    {
        GameEvents.OnOpponentBoardChanged += Render;
        EnsurePartnerBoardVisuals();
    }

    private void OnDisable() => GameEvents.OnOpponentBoardChanged -= Render;

    /// <summary>
    /// 파트너 관전용 아군 헥스 타일 + 벤치 받침을 최초 1회만 생성한다(보드 모양은 정적이라 스냅샷마다
    /// 다시 만들 필요 없음). 실제 BoardManager 좌표 API(GetBoardSnapshot/GetBenchSnapshot/
    /// CoordsToWorldPosition/BenchSlotWorldPosition)와 실제 프리팹(TilePrefab/BenchTilePrefab)을
    /// 그대로 재사용하며 좌표·색상·스케일을 새로 하드코딩하지 않는다. 두 관전 Layer가 준비되지
    /// 않았으면(ArePartnerLayersReady==false) 아무 것도 만들지 않는다 — Layer 없이 만들면 카메라
    /// CullingMask 분리도 안 된 상태라 로컬 타일과 겹쳐 보이기만 하고 아무 이점이 없기 때문이다.
    ///
    /// OnEnable은 Unity 실행 순서상 씬의 모든 오브젝트에 대해 항상 Start()보다 먼저 끝난다 —
    /// 그래서 이 메서드의 최초 호출(OnEnable 경유)은 거의 항상 BoardManager.Start()
    /// (GenerateBoard/GenerateBench)가 실행되기 전에 들어온다. 그 시점엔 _battleField가 비어 있어
    /// GetBoardSnapshot().Keys.Count == 0이므로, 이를 "아직 준비 안 됨" 신호로 보고 아무 것도
    /// 만들지 않은 채 _boardVisualsBuilt도 세우지 않고 반환한다. Render()가 스냅샷을 받을 때마다
    /// 이 메서드를 다시 호출하므로(파트너 첫 스냅샷은 네트워크 왕복이 걸려 BoardManager.Start()보다
    /// 항상 늦게 도착), 보드 좌표가 채워진 뒤 자연히 재시도된다. GenerateBoard/GenerateBench는
    /// 같은 Start() 안에서 연달아 호출되므로 보드 좌표가 찼다는 것은 벤치 좌표도 이미 찼다는 뜻과
    /// 같다 — 벤치 준비 여부를 별도로 확인하지 않는다.
    /// </summary>
    private void EnsurePartnerBoardVisuals()
    {
        if (_boardVisualsBuilt) return;
        if (!ArePartnerLayersReady(out _, out int partnerLayer)) return;

        var board = GameManager.TryGet(out var gm) ? gm.Board : null;
        if (board == null || board.TilePrefab == null || board.BenchTilePrefab == null) return;

        // BoardManager.Start()가 아직 안 돌았으면(보드 좌표 0개) 보류 — _boardVisualsBuilt를 세우지
        // 않아 다음 Render() 호출에서 재시도된다. IReadOnlyDictionary.Keys는 IEnumerable이라
        // Count가 없어 Dictionary 자체의 Count(IReadOnlyCollection)를 쓴다.
        if (board.GetBoardSnapshot().Count == 0) return;

        var rootGO = new GameObject("PartnerBoardVisualRoot");
        rootGO.transform.SetParent(transform, false);
        _boardVisualRoot = rootGO.transform;

        foreach (var coords in board.GetBoardSnapshot().Keys)
        {
            HexTile tile = Instantiate(board.TilePrefab, _boardVisualRoot);
            tile.transform.position = board.CoordsToWorldPosition(coords);
            tile.enabled = false; // HexTile 컴포넌트(드롭/호버) 비활성화 — 표시 전용
            foreach (var col in tile.GetComponentsInChildren<Collider>(true))
                col.enabled = false;
            SetDefaultLayerRecursive(tile.transform, partnerLayer);
        }

        for (int i = 0; i < board.GetBenchSnapshot().Count; i++)
        {
            BenchTile bench = Instantiate(board.BenchTilePrefab, _boardVisualRoot);

            // GenerateBench()는 각 BenchTile의 로컬 회전/스케일을 건드리지 않고 위치만 대입한다 —
            // 그래서 실제 로컬 벤치 타일의 월드 회전/스케일은 "_benchAnchor(월드) × 프리팹 자체의
            // 로컬 값"의 곱이다. Instantiate 직후 지금 시점의 localRotation/localScale은 아직
            // 프리팹 원본 그대로이므로, 그 값에 _benchAnchor의 월드 회전/스케일을 곱해 실제 로컬
            // 벤치와 동일한 최종 월드 회전/스케일을 재현한다(임의 숫자 보정 없음 — 전부 BoardManager
            // 실제 값 기반 계산).
            Quaternion prefabLocalRotation = bench.transform.localRotation;
            Vector3 prefabLocalScale = bench.transform.localScale;
            bench.transform.SetPositionAndRotation(
                board.BenchSlotWorldPosition(i),
                board.BenchAnchorRotation * prefabLocalRotation);
            bench.transform.localScale = Vector3.Scale(prefabLocalScale, board.BenchAnchorScale);

            bench.enabled = false; // BenchTile 상호작용(드롭) 비활성화 — 표시 전용
            foreach (var col in bench.GetComponentsInChildren<Collider>(true))
                col.enabled = false;
            SetDefaultLayerRecursive(bench.transform, partnerLayer);
        }

        // 아군 타일 + 벤치 받침을 모두 성공적으로 만든 뒤에만 세운다. 이 메서드에는 외부 I/O·비동기·
        // 코루틴이 없고, 루프 안에서 쓰는 좌표 API(CoordsToWorldPosition/BenchSlotWorldPosition)는
        // 범위를 벗어나도 예외 대신 안전한 기본값을 반환하도록 이미 만들어져 있어(BoardManager.cs),
        // 루프 도중 예외로 중단돼 일부만 생성된 채 멈추는 경로가 구조적으로 없다 — 그래서 별도
        // 정리/재시도 로직 없이 마지막에 플래그만 세운다.
        _boardVisualsBuilt = true;
    }

    /// <summary>
    /// 필드 표시를 일시적으로 끄거나 켠다(벤치는 영향받지 않음). 마지막 스냅샷이 있으면 새 억제
    /// 상태를 반영해 즉시 재렌더한다 — suppressed=false면 필드+벤치 전체를, suppressed=true면
    /// 필드만 제외하고 벤치는 그대로 다시 그린다(Render() 내부 onBoard 필터링). 이번 QA 테스트
    /// 범위에서만 쓰는 용도라 GameEvents/자동 페이즈 연결은 하지 않는다(호출측 책임).
    /// </summary>
    public void SetSuppressed(bool suppressed)
    {
        _suppressed = suppressed;
        if (_lastSnapshot != null) Render(_lastSnapshot);
    }

    private void Render(BoardSnapshot snap)
    {
        // snap == null은 BoardSnapshot.Decode() 실패(버전 불일치/손상 payload, 2026-08 코드리뷰
        // 대응)를 뜻한다. Decode 문서가 "마지막 렌더 상태 유지"를 약속하므로, _lastSnapshot을
        // 덮어쓰거나 ReleaseActive()로 기존 표시를 지우기 전에 여기서 걸러야 한다(PR #89 리뷰 후속 수정).
        if (snap == null) return;

        _lastSnapshot = snap;

        EnsurePartnerBoardVisuals();

        var board = GameManager.TryGet(out var gm) ? gm.Board : null;

        // 활성분 전부 풀로 반환 후 스냅샷 기준으로 다시 배치.
        ReleaseActive();
        if (board == null) return;

        foreach (BoardSnapshot.Entry e in snap.entries)
        {
            // 미러 전투 중(_suppressed)엔 필드만 건너뛴다 — 벤치는 계속 BoardSnapshot으로 실시간 표시.
            if (_suppressed && e.onBoard) continue;

            Vector3 basePos = e.onBoard
                ? board.CoordsToWorldPosition(new HexCoords(e.a, e.b))
                : board.BenchSlotWorldPosition(e.a);

            PokemonData data = ResolveSpecies(e.speciesId);
            int poolKey = data != null && data.modelPrefab != null ? e.speciesId : FALLBACK_POOL_KEY;

            GameObject go = RentVisual(poolKey, data);
            go.SetActive(true);
            go.transform.position = basePos + BoardOffset + Vector3.up * _unitHeight;

            // 실제 로컬 유닛(PokemonUnit.RefreshVisual/StarVisualScale)과 같은 기준으로 크기를 맞춘다 —
            // 종마다 프리팹 원본 스케일이 다르므로 Vector3.one 같은 고정값을 강제하지 않고, 그 원본에
            // 성급 배율만 곱한다. 성급 배율 계산 자체는 PokemonUnit.ComputeStarVisualScale로 위임해
            // 실제 로컬 유닛과 동일한 계산을 재사용한다(값을 이 파일에 별도로 다시 적지 않음).
            float starFactor = PokemonUnit.ComputeStarVisualScale(e.starLevel);

            if (poolKey == FALLBACK_POOL_KEY)
            {
                // 캡슐 폴백은 참조할 프리팹이 없어 기존 그대로 성급 배율만으로 크기를 정한다.
                go.transform.localScale = new Vector3(starFactor, 0.5f, starFactor);
                var rend = go.GetComponent<Renderer>();
                if (rend != null) rend.material.color = StarColor(e.starLevel);
            }
            else
            {
                go.transform.localScale = data.modelPrefab.transform.localScale * starFactor;
            }

            string speciesName = data != null ? data.pokemonNameEn : e.speciesId.ToString();
            go.name = $"PartnerUnit_{speciesName}_{e.starLevel}star";

            _active.Add((poolKey, go));
            _activeUnitViews.Add(new PartnerBoardUnitView(go.transform, data, e.starLevel, ResolveEquippedItems(e),
                e.roleOverride, e.attackRangeOverride, e.heroStatMultiplier,
                e.isTradeEvolved, ResolveEquippedStone(e), e.effectiveManaCost));
        }
    }

    /// <summary>Entry.itemId0/itemId1 → ItemData 목록(0 또는 미등록 id는 건너뜀). BattleManager의
    /// CreateMirrorBattleUnit이 itemId0/1을 ItemDatabase로 되찾는 것과 같은 방식.</summary>
    private static List<ItemData> ResolveEquippedItems(BoardSnapshot.Entry e)
    {
        var items = new List<ItemData>(2);
        ItemDatabase db = ItemDatabase.Instance;
        if (db == null) return items;

        if (e.itemId0 != 0)
        {
            ItemData item0 = db.GetById(e.itemId0);
            if (item0 != null) items.Add(item0);
        }
        if (e.itemId1 != 0)
        {
            ItemData item1 = db.GetById(e.itemId1);
            if (item1 != null) items.Add(item1);
        }
        return items;
    }

    /// <summary>Entry.equippedStoneId → EvolutionStoneData(0 또는 미등록 id면 null). ResolveEquippedItems와
    /// 동일한 방식으로 EvolutionStoneDatabase를 그대로 조회한다.</summary>
    private static EvolutionStoneData ResolveEquippedStone(BoardSnapshot.Entry e)
    {
        if (e.equippedStoneId == 0) return null;
        EvolutionStoneDatabase db = EvolutionStoneDatabase.Instance;
        return db != null ? db.GetById(e.equippedStoneId) : null;
    }

    /// <summary>speciesId → PokemonData 해석. 실패 시 null(캡슐 폴백) + 종당 1회만 경고.</summary>
    private PokemonData ResolveSpecies(int speciesId)
    {
        var db = PokemonDatabase.Instance;
        PokemonData data = db != null ? db.GetById(speciesId) : null;

        if (data == null && _warnedSpecies.Add(speciesId))
            Debug.LogWarning($"[OpponentBoardView] speciesId {speciesId} 해석 실패 — 캡슐 폴백 (PokemonDatabase 미임포트?)");

        return data;
    }

    /// <summary>종별 풀에서 비주얼을 꺼내거나 새로 만든다. 미러 전용이라 콜라이더는 전부 제거.</summary>
    private GameObject RentVisual(int poolKey, PokemonData data)
    {
        if (_poolBySpecies.TryGetValue(poolKey, out var stack) && stack.Count > 0)
            return stack.Pop();

        GameObject go;
        if (poolKey != FALLBACK_POOL_KEY)
        {
            go = Instantiate(data.modelPrefab, EnsureVisualRoot());
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.SetParent(EnsureVisualRoot());
        }

        // 미러는 표시 전용 — 드래그 레이캐스트에 안 걸리도록 콜라이더 제거.
        foreach (var col in go.GetComponentsInChildren<Collider>(true))
            Destroy(col);

        // 최초 생성 시 1회만 태깅 — 이후 풀에서 꺼내 재사용해도 GameObject.layer는 그대로 유지된다.
        if (ArePartnerLayersReady(out _, out int partnerLayer))
            SetDefaultLayerRecursive(go.transform, partnerLayer);

        return go;
    }

    /// <summary>Default(0)였던 자식만 targetLayer로 바꾸고 UI/Ignore Raycast/Outline 등 이미 특수
    /// Layer가 지정된 자식은 보존한다. BoardManager/PokemonUnit/BattleManager와 동일한 규칙.</summary>
    private static void SetDefaultLayerRecursive(Transform node, int targetLayer)
    {
        if (node.gameObject.layer == 0) node.gameObject.layer = targetLayer;
        for (int i = 0; i < node.childCount; i++)
            SetDefaultLayerRecursive(node.GetChild(i), targetLayer);
    }

    /// <summary>활성 비주얼을 전부 비활성화하고 각자의 종별 풀로 반환.</summary>
    private void ReleaseActive()
    {
        foreach ((int poolKey, GameObject go) in _active)
        {
            if (go == null) continue;
            go.SetActive(false);

            if (!_poolBySpecies.TryGetValue(poolKey, out var stack))
            {
                stack = new Stack<GameObject>();
                _poolBySpecies[poolKey] = stack;
            }
            stack.Push(go);
        }
        _active.Clear();
        _activeUnitViews.Clear();
    }

    private Color StarColor(int star)
    {
        if (_starColors == null || _starColors.Length == 0) return Color.cyan;
        int idx = Mathf.Clamp(star - 1, 0, _starColors.Length - 1);
        return _starColors[idx];
    }
}
