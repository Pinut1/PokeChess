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
    private static PartnerBattleMirrorController _instance;

    private BattleManager _mirrorBattleManager;

    /// <summary>지금 미러 전투가 실행 중인지.</summary>
    public bool IsRunning => _mirrorBattleManager != null && _mirrorBattleManager.IsMirrorBattleRunning;

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
    /// OnEnable에서 걸린 GameEvents 구독을 즉시 해제한다(실제 게임 이벤트 격리).
    /// </summary>
    private void EnsureMirrorBattleManager()
    {
        if (_mirrorBattleManager != null) return;

        var mirrorGO = new GameObject("MirrorBattleManager");
        mirrorGO.transform.SetParent(transform);

        _mirrorBattleManager = mirrorGO.AddComponent<BattleManager>();
        _mirrorBattleManager.enabled = false;
    }

    private void OnEnable()
    {
        GameEvents.OnOpponentDisconnected += HandleOpponentDisconnected;
    }

    private void OnDisable()
    {
        GameEvents.OnOpponentDisconnected -= HandleOpponentDisconnected;
    }

    /// <summary>
    /// 파트너 이탈 시 실행 중인 미러 전투를 조용히 중단한다 — 별도 결과 보고 없음.
    /// 기존 파트너 대기 팝업/게임 일시정지 흐름은 건드리지 않는다(그건 기존 시스템 몫).
    /// </summary>
    private void HandleOpponentDisconnected(float graceSeconds)
    {
        if (IsRunning) _mirrorBattleManager.AbortMirrorBattle();
    }

    /// <summary>
    /// 파트너 BattleSnapshot으로 미러 전투를 시작한다. 이미 실행 중이면 onFailed만 호출한다.
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

        _mirrorBattleManager.RunMirrorBattle(snapshot, onComplete, onFailed);
    }
}
