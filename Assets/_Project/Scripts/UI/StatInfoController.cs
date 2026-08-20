using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 유닛을 <b>우클릭</b>하면 그 자리에 상세창(StatInfoPanelUI)을 띄운다.
/// 좌클릭하거나 유닛이 아닌 곳을 우클릭하면 닫는다.
///
/// ⚠️ <b>항상 활성인 오브젝트</b>(Canvas 등)에 붙일 것. 패널은 프리팹에서 하나만 만들어 재사용한다.
///
/// 레이캐스트·입력 방식은 UnitDragController와 같은 규약을 따른다 —
/// 새 Input System의 임베드 InputAction을 쓰고, 유닛 판정은 콜라이더에서
/// GetComponentInParent&lt;PokemonUnit&gt;으로 올라가며 찾는다(모델이 자식에 있어서).
/// </summary>
public class StatInfoController : MonoBehaviour
{
    [Header("패널 — 둘 중 하나만 채우면 된다")]
    [Tooltip("씬에 이미 배치해둔 패널. 이걸 넣으면 생성하지 않고 그대로 쓴다(배치해둔 자리에 그대로 뜬다). " +
             "씬에는 꺼둔 상태로 저장할 것.")]
    [SerializeField] private StatInfoPanelUI _panelInstance;

    [Tooltip("씬 인스턴스를 안 쓸 때만 사용. 처음 열 때 하나만 생성해 재사용한다.")]
    [SerializeField] private StatInfoPanelUI _panelPrefab;

    [Tooltip("생성 방식일 때 패널이 담길 부모. 보통 최상위 Canvas 아래의 빈 오브젝트.")]
    [SerializeField] private RectTransform _panelRoot;

    [Header("배치")]
    [Tooltip("비우면 Camera.main을 쓴다.")]
    [SerializeField] private Camera _camera;

    [Tooltip("켜면 클릭한 지점 옆에 패널을 띄운다. 끄면 프리팹에 잡아둔 자리에 그대로 나온다(기본).")]
    [SerializeField] private bool _openAtPointer;

    [Tooltip("클릭 지점에서 패널을 띄울 오프셋(화면 픽셀). Open At Pointer가 켜졌을 때만 쓴다.")]
    [SerializeField] private Vector2 _screenOffset = new(140f, 0f);

    [Tooltip("화면 가장자리에서 최소한 띄울 여백(픽셀). 패널이 화면 밖으로 잘리지 않게 한다.")]
    [SerializeField] private float _screenMargin = 10f;

    [Header("입력 (인스펙터에서 바인딩 편집·추가 가능)")]
    [SerializeField] private InputAction _pointAction =
        new InputAction("Point", InputActionType.Value, "<Pointer>/position", expectedControlType: "Vector2");

    [Tooltip("상세창을 여는 입력. 기본 마우스 우클릭.")]
    [SerializeField] private InputAction _openAction =
        new InputAction("StatInfo", InputActionType.Button, "<Mouse>/rightButton");

    [Tooltip("상세창을 닫는 입력. 기본 좌클릭(드래그 시작과 겹쳐도 닫히기만 하므로 무해).")]
    [SerializeField] private InputAction _closeAction =
        new InputAction("CloseStatInfo", InputActionType.Button, "<Mouse>/leftButton");

    [SerializeField] private LayerMask _raycastMask = ~0;

    [Header("전투 중 유닛 선택")]
    // 전투 전용 시각화(BattleUnit.visual)는 모델 프리팹 그대로라 Collider가 없다.
    // 콜라이더를 붙이는 대신 화면 좌표 거리로 고른다 — 물리에 아무 영향을 주지 않고
    // 벤치 유닛 드래그를 가리지도 않는다.
    [Tooltip("클릭 지점에서 이 거리(화면 픽셀) 안에 있는 전투 유닛까지 인정한다.")]
    [SerializeField] private float _battlePickRadius = 60f;

    [Tooltip("전투 유닛의 클릭 기준 높이(월드). 발밑이 아니라 몸통을 겨냥하도록 올린다.")]
    [SerializeField] private float _battlePickHeight = 1f;

    private StatInfoPanelUI _panel;

    // 파트너 전체화면 관전 중 파트너 유닛 우클릭 판정용 참조(지연 탐색, 씬 배선 없음 —
    // ItemInventoryUI/SynergyPanelUI와 동일 패턴).
    private PartnerSpectateView _partnerSpectateView;

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;

        if (_panelInstance == null && _panelPrefab == null)
            Debug.LogError("[StatInfoController] 패널 미배선 — 우클릭해도 창이 뜨지 않는다. " +
                           "Panel Instance(씬 배치본) 또는 Panel Prefab을 채울 것", this);
    }

    private void OnEnable()
    {
        _pointAction.Enable();
        _openAction.Enable();
        _closeAction.Enable();

        GameEvents.OnPartnerSpectateExpandedChanged += HandlePartnerSpectateExpandedChanged;
    }

    private void OnDisable()
    {
        _pointAction.Disable();
        _openAction.Disable();
        _closeAction.Disable();

        GameEvents.OnPartnerSpectateExpandedChanged -= HandlePartnerSpectateExpandedChanged;
    }

    /// <summary>
    /// 전체화면 관전 진입/종료 어느 쪽이든 지금 열려 있는 상세창을 닫는다. 종료 시엔 파트너 데이터가
    /// 더 이상 유효하지 않으므로 당연히 닫아야 하고, 진입 시에도 내 화면이 파트너 화면으로 덮이므로
    /// (전체화면 배경 Image가 화면 전체 레이캐스트를 흡수 — PartnerSpectateView 주석 참고) 열려 있던
    /// 내 유닛 정보가 그대로 남아 있을 이유가 없다. 실제 준비 상태/네트워크는 건드리지 않는다.
    /// </summary>
    private void HandlePartnerSpectateExpandedChanged(bool expanded) => Close();

    private void Update()
    {
        if (_openAction.WasPressedThisFrame()) HandleOpenPressed();
        else if (_closeAction.WasPressedThisFrame()) Close();
    }

    // ─────────────────────────────────────────
    // 열기 / 닫기
    // ─────────────────────────────────────────

    private void HandleOpenPressed()
    {
        // 전체화면 관전 중엔 파트너 유닛만 대상으로 한다 — 내 화면은 어차피 관전 배경 Image에
        // 덮여 있고(위 HandlePartnerSpectateExpandedChanged 주석 참고), 관전 중 co-located Layer
        // 구조상 3D Physics.Raycast는 Layer 분리와 무관하게 내 보드 콜라이더도 그대로 맞힐 수 있어
        // 로컬 판정을 그대로 두면 잘못된(내) 유닛이 열릴 위험이 있다 — 그래서 관전 중이면 로컬
        // 판정 자체를 시도하지 않고 여기서 완전히 갈라진다.
        if (TryHandlePartnerOpen()) return;

        // 보드/벤치 유닛(Collider 있음)이 먼저다.
        if (TryRaycast(out RaycastHit hit))
        {
            PokemonUnit unit = hit.collider.GetComponentInParent<PokemonUnit>();
            if (unit != null)
            {
                // 같은 대상을 다시 우클릭하면 토글로 닫는다.
                if (_panel != null && _panel.Unit == unit) Close();
                else Open(p => p.Bind(unit));
                return;
            }
        }

        // 전투 중에는 화면에 보이는 게 전투 전용 인스턴스라 PokemonUnit도 Collider도 없다.
        // 적도 여기로 걸린다.
        BattleUnit battleUnit = PickBattleUnit(PointerScreenPos());
        if (battleUnit != null)
        {
            if (_panel != null && _panel.BattleUnit == battleUnit) Close();
            else Open(p => p.Bind(battleUnit));
            return;
        }

        // 빈 곳을 우클릭하면 닫는다.
        Close();
    }

    /// <summary>
    /// 전체화면 파트너 관전 중일 때만 동작한다(PIP는 대상 아님 — PartnerSpectateView.IsExpanded 기준).
    /// 아니면 false를 반환해 기존 로컬 판정으로 넘어간다. 전체화면 중이면(파트너 유닛을 맞혔든 못
    /// 맞혔든) 항상 true를 반환해 로컬 판정을 완전히 대체한다.
    ///
    /// ⚠️ 이 아래 좌표-거리 판정도 AugmentInfoTrigger의 관전 클릭 판정과 같은 함정이 있었다 —
    /// 그 지점에 실제로 뭐가 떠 있는지 안 보고 반경/거리만 비교하므로, 그 근처를 덮은 다른 UI를
    /// 눌러도 파트너 유닛 정보창이 같이 열렸다(2026-08 재재리뷰 지적). PointerUtil.IsBlockedByOtherUI로
    /// 후보를 찾기 전에 먼저 걸러낸다 — AugmentInfoTrigger와 같은 헬퍼(공용 유틸로 옮김).
    /// </summary>
    private bool TryHandlePartnerOpen()
    {
        PartnerSpectateView spectateView = EnsurePartnerSpectateView();
        if (spectateView == null || !spectateView.IsExpanded) return false;

        PartnerBattleMirrorController mirror = spectateView.MirrorController;

        if (mirror == null) { Close(); return true; }

        // "준비 중"(파트너 컨텐츠를 아직 한 번도 못 받음) 상태면 PipRawImage 자체가 꺼져있다 —
        // 꺼진 오브젝트는 RaycastAll에 절대 안 잡히므로 아래 IsBlockedByOtherUI가 항상 "안 막힘"으로
        // 오판할 수 있다(AugmentInfoTrigger.Update()와 같은 함정, 2026-08 재재재재리뷰 지적 —
        // 그때 AugmentInfoTrigger엔 넣어놓고 여기는 빠뜨렸음). 화면에 실제로 아무것도 안 보이는
        // 상태라 클릭을 받을 이유도 없으니 여기서 먼저 걸러낸다.
        if (!spectateView.IsShowingContent) { Close(); return true; }

        Vector2 screenPos = PointerScreenPos();

        if (PointerUtil.IsBlockedByOtherUI(screenPos, spectateView.PipRawImage.gameObject)) { Close(); return true; }

        // 로컬 GamePhase가 아니라 미러 자신의 실행 상태로 판단한다 — 내 전투가 먼저 끝나 Result로
        // 넘어가도 파트너 미러 전투는 계속 실행 중일 수 있는데(RoundPhaseManager가 양쪽 결과가
        // 모일 때까지 다음 라운드로 넘어가지 않음), 내 GamePhase를 기준으로 삼으면 그 사이 BoardSnapshot
        // 정적 프리뷰로 잘못 전환돼 영웅증강 override(roleOverride 등)가 반영 안 된 원본값이 뜨는
        // 문제가 있었다(2026-08 확인 — UnitStatusBarHud.DrawPartnerBars와 동일한 원인·동일한 기준으로 통일).
        //
        // 벤치(OpponentBoardView.ActiveUnitViews)는 미러 전투 중에도 BoardSnapshot으로 계속 표시되므로
        // (OpponentBoardView.Render가 필드만 억제하고 벤치는 그대로 둠 — SetSuppressed 주석 참고) 전투
        // 여부와 무관하게 항상 후보로 조회한다. 전투 중이면 미러 전투 유닛(파트너 필드+적)을, 아니면
        // 쇼핑 중 적 프리뷰를 조회한다 — 이 둘은 mirror.IsRunning으로 서로 배타적이라 한 번만 조회한다.
        OpponentBoardView.PartnerBoardUnitView? shopUnit =
            PickPartnerShopUnit(mirror, spectateView, screenPos, out float shopDistance);

        float battleDistance;
        BattleUnit battleCandidate = mirror.IsRunning
            ? PickPartnerBattleUnit(mirror.MirrorUnits, spectateView, screenPos, out battleDistance)
            : PickPartnerBattleUnit(mirror.PreviewEnemies, spectateView, screenPos, out battleDistance);

        // 두 후보가 화면상 가까이 있어도 클릭 지점에 더 가까운 쪽을 선택한다(PickPartnerBattleUnit/
        // PickPartnerShopUnit 각각의 반경 판정을 그대로 따름 — 먼저 찾은 쪽을 무조건 우선하지 않는다).
        if (shopUnit.HasValue && (battleCandidate == null || shopDistance <= battleDistance))
        {
            Open(p => p.Bind(shopUnit.Value));
            return true;
        }
        if (battleCandidate != null)
        {
            if (_panel != null && _panel.BattleUnit == battleCandidate) Close();
            else Open(p => p.Bind(battleCandidate));
            return true;
        }

        Close();
        return true;
    }

    private PartnerSpectateView EnsurePartnerSpectateView()
    {
        if (_partnerSpectateView == null) _partnerSpectateView = FindFirstObjectByType<PartnerSpectateView>();
        return _partnerSpectateView;
    }

    /// <summary>주어진 파트너 관전용 BattleUnit 목록 중 클릭 지점에 가장 가까운 것. PickBattleUnit(로컬)과
    /// 같은 반경 기준의 화면 거리 판정이되, 위치를 PartnerSpectateView.TryProjectWorldToScreen으로 투영한다
    /// (UnitStatusBarHud.PlaceMirror와 동일한 좌표 변환 — 관전 화면이 실제로 보여주는 자리에 맞춘다).
    /// mirror.MirrorUnits(전투 중)와 mirror.PreviewEnemies(쇼핑/프리뷰 중 적) 양쪽 호출에서 공용으로 쓴다.
    /// distance에는 찾은 거리(못 찾았으면 _battlePickRadius)를 반환해 호출부가 다른 후보와 비교할 수 있게 한다.</summary>
    private BattleUnit PickPartnerBattleUnit(IReadOnlyList<BattleUnit> units, PartnerSpectateView spectateView,
                                             Vector2 screenPos, out float distance)
    {
        distance = _battlePickRadius;
        if (units == null) return null;

        BattleUnit nearest = null;

        foreach (var bu in units)
        {
            if (bu == null || !bu.IsAlive || bu.visual == null) continue;

            Vector3 worldPos = bu.visual.transform.position + Vector3.up * _battlePickHeight;
            if (!spectateView.TryProjectWorldToScreen(worldPos, out Vector2 point)) continue;

            float d = Vector2.Distance(screenPos, point);
            if (d > distance) continue;

            distance = d;
            nearest = bu;
        }

        return nearest;
    }

    /// <summary>쇼핑 중 파트너 보드 유닛 중 클릭 지점에 가장 가까운 것. 콜라이더가 없는 미러
    /// 비주얼이라(OpponentBoardView가 전부 제거함) PickPartnerBattleUnit과 같은 화면 거리 판정을 쓴다.
    /// distance에는 찾은 거리(못 찾았으면 _battlePickRadius)를 반환해 호출부가 다른 후보와 비교할 수 있게 한다.</summary>
    private OpponentBoardView.PartnerBoardUnitView? PickPartnerShopUnit(
        PartnerBattleMirrorController mirror, PartnerSpectateView spectateView, Vector2 screenPos, out float distance)
    {
        distance = _battlePickRadius;

        OpponentBoardView boardView = mirror.BoardView;
        if (boardView == null) return null;

        OpponentBoardView.PartnerBoardUnitView? nearest = null;

        foreach (var view in boardView.ActiveUnitViews)
        {
            if (view.visual == null) continue;

            Vector3 worldPos = view.visual.position + Vector3.up * _battlePickHeight;
            if (!spectateView.TryProjectWorldToScreen(worldPos, out Vector2 point)) continue;

            float d = Vector2.Distance(screenPos, point);
            if (d > distance) continue;

            distance = d;
            nearest = view;
        }

        return nearest;
    }

    private void Open(System.Action<StatInfoPanelUI> bind)
    {
        if (_panel == null) _panel = ResolvePanel();
        if (_panel == null) return;

        bind(_panel);

        // 끄면 프리팹에 잡아둔 자리에 그대로 둔다 — 위치를 아예 건드리지 않는다.
        if (_openAtPointer) PlaceAtPointer(_panel);
    }

    /// <summary>
    /// 클릭 지점에 가장 가까운 전투 유닛. 반경 밖이면 null.
    ///
    /// 물리 레이캐스트를 쓰지 않는 이유: 전투 시각화에는 Collider가 없고, 붙이면
    /// 매 틱 움직이는 정적 콜라이더가 되어 물리 트리를 계속 갱신하는 데다,
    /// 앞을 가릴 때 벤치 유닛 드래그(첫 히트만 보고 판단한다)까지 막을 수 있다.
    /// </summary>
    private BattleUnit PickBattleUnit(Vector2 screenPos)
    {
        if (_camera == null) return null;
        if (!GameManager.TryGet(out var gm)) return null;

        var battle = gm.Battle;
        if (battle == null) return null;

        BattleUnit nearest = null;
        float nearestDistance = _battlePickRadius;

        // 전투 중이면 Units, 준비 단계면 PreviewEnemies 쪽이 채워져 있다(둘이 동시에 차지 않는다).
        PickNearest(battle.Units, screenPos, ref nearest, ref nearestDistance);
        PickNearest(battle.PreviewEnemies, screenPos, ref nearest, ref nearestDistance);

        return nearest;
    }

    private void PickNearest(IReadOnlyList<BattleUnit> units, Vector2 screenPos,
                             ref BattleUnit nearest, ref float nearestDistance)
    {
        if (units == null) return;

        foreach (var bu in units)
        {
            if (bu == null || !bu.IsAlive || bu.visual == null) continue;

            Vector3 point = _camera.WorldToScreenPoint(
                bu.visual.transform.position + Vector3.up * _battlePickHeight);

            if (point.z <= 0f) continue; // 카메라 뒤

            float distance = Vector2.Distance(screenPos, point);
            if (distance > nearestDistance) continue;

            nearestDistance = distance;
            nearest = bu;
        }
    }

    private void Close()
    {
        if (_panel != null) _panel.Hide();
    }

    /// <summary>
    /// 쓸 패널을 정한다. 씬에 배치해둔 인스턴스가 있으면 그걸 쓰고(배치해둔 자리 유지),
    /// 없으면 프리팹에서 하나 생성한다.
    /// </summary>
    private StatInfoPanelUI ResolvePanel()
    {
        if (_panelInstance != null) return _panelInstance;

        if (_panelPrefab == null || _panelRoot == null)
        {
            Debug.LogError("[StatInfoController] 패널 미배선 — Panel Instance 또는 (Panel Prefab + Panel Root)를 채울 것", this);
            return null;
        }

        // worldPositionStays: false — 켜두면(기본값) 월드 좌표를 유지하려고 로컬 위치·스케일을
        // 다시 계산해서, 프리팹에 잡아둔 RectTransform 값이 틀어진다.
        return Instantiate(_panelPrefab, _panelRoot, false);
    }

    // ─────────────────────────────────────────
    // 위치
    // ─────────────────────────────────────────

    /// <summary>
    /// 클릭한 화면 좌표 옆에 패널을 놓되, 화면 밖으로 잘리지 않게 안쪽으로 밀어 넣는다.
    /// 화면 오른쪽 유닛을 눌렀을 때 패널이 잘려 나가는 걸 막는다.
    /// </summary>
    private void PlaceAtPointer(StatInfoPanelUI panel)
    {
        Vector2 screenPos = PointerScreenPos() + _screenOffset;

        Rect rect = panel.Rect.rect;
        Vector2 pivot = panel.Rect.pivot;
        float scale = _panelRoot != null ? _panelRoot.lossyScale.x : 1f;

        float width  = rect.width  * scale;
        float height = rect.height * scale;

        // 피벗 기준이라 좌우/상하로 얼마나 뻗는지가 달라진다.
        float left   = screenPos.x - width  * pivot.x;
        float bottom = screenPos.y - height * pivot.y;

        screenPos.x += Mathf.Clamp(_screenMargin - left, 0f, float.MaxValue);
        screenPos.x -= Mathf.Clamp(left + width - (Screen.width - _screenMargin), 0f, float.MaxValue);
        screenPos.y += Mathf.Clamp(_screenMargin - bottom, 0f, float.MaxValue);
        screenPos.y -= Mathf.Clamp(bottom + height - (Screen.height - _screenMargin), 0f, float.MaxValue);

        panel.Rect.position = screenPos;
    }

    private Vector2 PointerScreenPos() => _pointAction.ReadValue<Vector2>();

    /// <summary>포인터 아래로 레이를 쏜다. 맞은 게 없으면 false.</summary>
    private bool TryRaycast(out RaycastHit hit)
    {
        hit = default;
        if (_camera == null) return false;

        Ray ray = _camera.ScreenPointToRay(PointerScreenPos());
        return Physics.Raycast(ray, out hit, 1000f, _raycastMask);
    }
}
