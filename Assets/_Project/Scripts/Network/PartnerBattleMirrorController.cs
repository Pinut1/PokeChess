using UnityEngine;

/// <summary>
/// 파트너 BattleSnapshot으로 미러 전투를 실행·관리하는 컨트롤러.
///
/// 실제 게임 이벤트(GameEvents.BattleStart/BattleEnd 등)와 완전히 격리된, 씬·프리팹 배선이 없는
/// 런타임 전용 컴포넌트다 — QAManager가 필요한 시점에 GetOrCreate()로 동적 생성해서 쓴다.
///
/// 실제 전투에 쓰이는 BattleManager(GameManager.Instance.Battle)와는 별도로 자체 BattleManager
/// 컴포넌트를 하나 들고 있어 _units/코루틴이 완전히 분리된다. 그 컴포넌트는 만들자마자 enabled를
/// 꺼서(OnEnable→즉시 OnDisable) GameEvents 구독을 해제한다 — 실제 전투 시작/종료 이벤트에
/// 이 미러 인스턴스가 반응하는 일이 없다(코루틴은 enabled와 무관하게 정상 동작).
/// </summary>
public class PartnerBattleMirrorController : MonoBehaviour
{
    /// <summary>
    /// OpponentBoardView를 씬에서 못 찾았을 때만 쓰는 폴백 오프셋. OpponentBoardView.cs의
    /// _boardOffset 필드 기본값(Inspector 미조정 시)과 동일하게 맞춰뒀다 — 실제 씬 값과
    /// 다를 수 있으므로 이 경로를 타면 반드시 경고 로그를 남긴다(추측값 조용히 사용 금지).
    /// </summary>
    private static readonly Vector3 FALLBACK_BOARD_OFFSET = new Vector3(0f, 0f, 14f);

    private static PartnerBattleMirrorController _instance;

    private BattleManager _mirrorBattleManager;
    private OpponentBoardView _opponentBoardView;

    /// <summary>지금 미러 전투가 실행 중인지.</summary>
    public bool IsRunning => _mirrorBattleManager != null && _mirrorBattleManager.IsMirrorBattleRunning;

    /// <summary>현재 표시 중인 미러 visual 개수(QA 상태 표시용). 미러 BattleManager가 없으면 0.</summary>
    public int MirrorVisualCount => _mirrorBattleManager != null ? _mirrorBattleManager.VisualUnitCount : 0;

    /// <summary>
    /// 미러 BattleManager의 현재 BattleUnit 목록(읽기 전용). 미러 HP/마나 바(UnitStatusBarHud)가
    /// 이 값만 읽어 화면에 표시한다 — BattleManager.Units(기존 공개 API)를 그대로 재사용하며 새
    /// 데이터 경로를 만들지 않는다. 미러 BattleManager가 아직 없으면 null.
    /// 전투가 끝나면(Cleanup) 이 목록도 비므로, 폴링하는 쪽은 별도 이벤트 없이 빈 목록만 보고
    /// 잔여 UI를 스스로 정리할 수 있다.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<BattleUnit> MirrorUnits =>
        _mirrorBattleManager != null ? _mirrorBattleManager.Units : null;

    /// <summary>파트너 관전 카메라 등 외부에서도 같은 파트너 보드 오프셋을 쓰도록 공개.</summary>
    public Vector3 BoardOffset => FindBoardOffset();

    /// <summary>파트너 보드 미리보기 뷰(쇼핑 중 파트너 유닛 표시). UnitStatusBarHud가 쇼핑 단계
    /// 파트너 HP/마나 바를 그릴 때 ActiveUnitViews를 읽는 경로로 쓴다. 기존 EnsureOpponentBoardView
    /// lazy-init 패턴을 그대로 타므로 별도 캐시를 새로 만들지 않는다. 아직 씬에서 못 찾았으면 null.</summary>
    public OpponentBoardView BoardView
    {
        get
        {
            EnsureOpponentBoardView();
            return _opponentBoardView;
        }
    }

    /// <summary>두 관전 전용 Layer(LocalGameplayVisual/PartnerSpectateVisual)가 Unity Editor에 모두
    /// 추가돼 있는지. OpponentBoardView의 판정을 그대로 전달한다(판정 기준을 한 곳에서만 계산) —
    /// PartnerSpectateView가 Main/Spectator Camera의 CullingMask를 조정할지 결정할 때 쓴다.</summary>
    public bool ArePartnerLayersReady(out int localLayer, out int partnerLayer)
    {
        EnsureOpponentBoardView();
        if (_opponentBoardView != null)
            return _opponentBoardView.ArePartnerLayersReady(out localLayer, out partnerLayer);

        localLayer = LayerMask.NameToLayer("LocalGameplayVisual");
        partnerLayer = LayerMask.NameToLayer("PartnerSpectateVisual");
        return localLayer >= 0 && partnerLayer >= 0;
    }

    /// <summary>파트너 BoardSnapshot을 한 번이라도 수신했는지. OpponentBoardView.HasSnapshot을 그대로 전달 —
    /// PartnerSpectateView가 "아직 수신 전(준비 중)"과 "빈 필드 정상 수신"을 구분하는 데 쓴다.</summary>
    public bool HasPartnerBoardSnapshot
    {
        get
        {
            EnsureOpponentBoardView();
            return _opponentBoardView != null && _opponentBoardView.HasSnapshot;
        }
    }

    /// <summary>
    /// 미러 유닛 visual들이 실제로 자식으로 매달리는 루트(=미러 BattleManager 자신의 Transform).
    /// 관전 카메라가 "현재 미러 전장에 실제로 그려진 visual들의 월드 바운즈"를 계산할 때 쓴다.
    /// 미러 BattleManager가 아직 없으면 null.
    /// </summary>
    public Transform MirrorVisualRoot => _mirrorBattleManager != null ? _mirrorBattleManager.transform : null;

    /// <summary>씬에 배치돼 있지 않으므로 최초 호출 시 런타임에 생성한다.</summary>
    public static PartnerBattleMirrorController GetOrCreate()
    {
        if (_instance != null) return _instance;

        var go = new GameObject("PartnerBattleMirrorController");
        Object.DontDestroyOnLoad(go);
        _instance = go.AddComponent<PartnerBattleMirrorController>();
        return _instance;
    }

    /// <summary>
    /// 미러 전용 BattleManager를 최초 1회 생성한다. 생성 직후 enabled=false로 전환해
    /// OnEnable에서 걸린 GameEvents 구독을 즉시 해제하고(실제 게임 이벤트 격리),
    /// OpponentBoardView가 쓰는 것과 같은 파트너 보드 오프셋으로 시각 표시를 설정한다.
    /// 이미 생성돼 있으면(재사용) 새로 만들지 않지만, 아래 InjectEnemyTilePrefabIfAvailable는
    /// 이 메서드가 호출될 때마다(최초 생성이든 재사용이든) 항상 다시 시도한다.
    /// </summary>
    private void EnsureMirrorBattleManager()
    {
        if (_mirrorBattleManager == null)
        {
            var mirrorGO = new GameObject("MirrorBattleManager");
            mirrorGO.transform.SetParent(transform);

            _mirrorBattleManager = mirrorGO.AddComponent<BattleManager>();
            _mirrorBattleManager.enabled = false;
            _mirrorBattleManager.ConfigureMirrorVisuals(FindBoardOffset(), mirrorGO.transform);
        }

        InjectEnemyTilePrefabIfAvailable();
    }

    /// <summary>
    /// 실제 로컬 BattleManager(GameManager.Battle)의 _enemyTilePrefab을 미러 인스턴스에 주입한다.
    /// 런타임 AddComponent로 생성되는 미러 인스턴스는 어떤 Inspector 값도 물려받지 않으므로,
    /// 그대로 두면 CreateEnemyTile()이 항상 폴백(연빨강 원기둥)으로 빠진다.
    /// EnsureMirrorBattleManager가 호출될 때마다(최초 생성/기존 인스턴스 재사용 모두) 매번 다시
    /// 시도한다 — 씬/초기화 순서상 최초 호출 시점엔 GameManager.Battle이 아직 준비되지 않았을 수
    /// 있는데, 나중 호출에서 준비되면 그때 채워지도록 하기 위함. BattleManager.ConfigureMirrorEnemyTilePrefab
    /// 자체가 null 인자를 무시하므로 이미 주입된 값을 실수로 지우는 일은 없다.
    /// </summary>
    private void InjectEnemyTilePrefabIfAvailable()
    {
        if (_mirrorBattleManager == null) return;
        if (!GameManager.TryGet(out var gm) || gm.Battle == null) return;
        if (gm.Battle.EnemyTilePrefab == null) return;

        _mirrorBattleManager.ConfigureMirrorEnemyTilePrefab(gm.Battle.EnemyTilePrefab);
    }

    /// <summary>
    /// OpponentBoardView가 실제로 쓰는 오프셋을 그대로 가져온다(재사용 원칙). 씬에 없으면
    /// 폴백 상수를 쓰되, 실제 씬 값과 다를 수 있음을 경고로 남긴다.
    /// </summary>
    private Vector3 FindBoardOffset()
    {
        EnsureOpponentBoardView();
        if (_opponentBoardView != null) return _opponentBoardView.BoardOffset;

        Debug.LogWarning("[MirrorBattle] OpponentBoardView를 씬에서 못 찾음 — 폴백 오프셋(0,0,14) 사용(실제 씬 값과 다를 수 있음)");
        return FALLBACK_BOARD_OFFSET;
    }

    /// <summary>
    /// OpponentBoardView 참조를 찾아 캐시한다. DontDestroyOnLoad로 씬 전환에도 이 컨트롤러는
    /// 유지되지만 OpponentBoardView는 씬마다 새로 생기므로, 캐시가 비었거나(파괴됨 포함)
    /// 다시 찾는다.
    /// </summary>
    private void EnsureOpponentBoardView()
    {
        if (_opponentBoardView != null) return;
        _opponentBoardView = FindFirstObjectByType<OpponentBoardView>();
    }

    /// <summary>
    /// 쇼핑 단계 파트너 보드 필드 표시(OpponentBoardView)와 미러 전투 필드 유닛이 같은 자리에
    /// 겹쳐 보이지 않도록, 미러 전투가 실행되는 동안만 필드 렌더만 잠깐 끈다(벤치는
    /// OpponentBoardView.SetSuppressed 내부에서 영향받지 않아 계속 표시된다). RoundPhaseManager/
    /// 자동 페이즈 전환과는 연결하지 않는다 — 이 QA 테스트 흐름 안에서만 켜고 끈다.
    /// </summary>
    private void SetOpponentBoardSuppressed(bool suppressed)
    {
        EnsureOpponentBoardView();
        if (_opponentBoardView != null) _opponentBoardView.SetSuppressed(suppressed);
    }

    private void OnEnable()
    {
        GameEvents.OnOpponentDisconnected += HandleOpponentDisconnected;
    }

    private void OnDisable()
    {
        GameEvents.OnOpponentDisconnected -= HandleOpponentDisconnected;
    }

    /// <summary>파트너 이탈 시 실행 중인 미러 전투를 조용히 중단한다 — 별도 결과 보고 없음.
    /// 기존 파트너 대기 팝업/게임 일시정지 흐름은 건드리지 않는다(그건 기존 시스템 몫).</summary>
    private void HandleOpponentDisconnected(float graceSeconds) => StopMirrorBattle();

    /// <summary>
    /// 실행 중인 미러 전투를 즉시 중단한다(결과 콜백 없음). 외부(관전 시스템이 새 BattleSnapshot으로
    /// 갈아탈 때 등)에서 "지금 도는 미러 전투를 멈추고 싶다"는 의도를 이름으로 드러내는 공개 진입점 —
    /// 내부적으로 BattleManager.AbortMirrorBattle() 하나만 그대로 재사용하고 새 정리 로직을 만들지
    /// 않는다. 실행 중이 아니면 아무 일도 하지 않는다(중복 호출 안전).
    /// </summary>
    public void StopMirrorBattle()
    {
        if (!IsRunning) return;
        _mirrorBattleManager.AbortMirrorBattle();
        SetOpponentBoardSuppressed(false);
    }

    /// <summary>
    /// 쇼핑 중 파트너 관전용 적 프리뷰를 표시한다(BattleSnapshot 미러 전투와는 완전히 별개 경로).
    /// 미러 BattleManager.ShowMirrorEnemyPreview를 그대로 전달 — ConfigureMirrorVisuals로 설정된
    /// 오프셋 덕분에 파트너 보드 위치에 그려진다. 미러 전투가 실행 중(_units 채워짐)이면
    /// BattleManager.ShowEnemyPreview 자체의 가드(_units.Count > 0 → return)로 무시된다.
    /// </summary>
    public void ShowPartnerEnemyPreview(StageData stage)
    {
        EnsureMirrorBattleManager();
        _mirrorBattleManager.ShowMirrorEnemyPreview(stage);
    }

    /// <summary>
    /// 파트너 관전용 적 프리뷰만 정리한다. 이 컨트롤러가 소유한 미러 BattleManager 인스턴스
    /// (_mirrorBattleManager)의 프리뷰만 지운다 — 로컬 실제 BattleManager(GameManager.Instance.Battle)의
    /// 적 프리뷰와는 완전히 별도 인스턴스라 영향이 없다.
    /// </summary>
    public void ClearPartnerEnemyPreview()
    {
        if (_mirrorBattleManager != null) _mirrorBattleManager.ClearMirrorEnemyPreview();
    }

    /// <summary>
    /// 미러 전투 시작 시 호출. 쇼핑 중 보여주던 적 프리뷰 "유닛"만 제거하고 빨간 육각 타일은 유지한다
    /// (ClearPartnerEnemyPreview는 타일까지 함께 지운다) — 타일 위에서 실제 미러 전투 적이 싸우는
    /// 모습을 그대로 보여주기 위함.
    /// </summary>
    public void ClearPartnerEnemyPreviewUnitsOnly()
    {
        if (_mirrorBattleManager != null) _mirrorBattleManager.ClearMirrorEnemyPreviewUnitsOnly();
    }

    /// <summary>
    /// 파트너 BattleSnapshot으로 미러 전투를 시작한다. 이미 실행 중이면 onFailed만 호출한다.
    /// 시작 직전 OpponentBoardView 표시를 잠깐 끄고, 성공/실패 어느 쪽으로 끝나든 다시 켠다.
    /// </summary>
    public void StartMirrorBattle(
        BattleSnapshot snapshot,
        System.Action<MirrorBattleResult> onComplete,
        System.Action<string> onFailed)
    {
        EnsureMirrorBattleManager();

        if (IsRunning)
        {
            onFailed?.Invoke("이미 미러 전투가 실행 중입니다");
            return;
        }

        SetOpponentBoardSuppressed(true);

        _mirrorBattleManager.RunMirrorBattle(
            snapshot,
            result =>
            {
                SetOpponentBoardSuppressed(false);
                onComplete?.Invoke(result);
            },
            error =>
            {
                SetOpponentBoardSuppressed(false);
                onFailed?.Invoke(error);
            });
    }
}
