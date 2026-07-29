using UnityEngine;

/// <summary>
/// 팀 공통 체력(GDD: 2인 공동 HP) 관리. 두 플레이어가 하나의 HP 풀을 공유하며,
/// 전투에서 패배할 때마다 줄어들고 0이 되면 게임오버(세션 종료)로 이어진다.
///
/// 실제 HP 값은 NetworkManager가 Room CustomProperties("TeamHP")로 보관·동기화한다.
/// (단일 기록자 = MasterClient 권위 → 두 클라가 동시에 깎아 생기는 경합 방지)
/// 이 매니저는 밸런스 상수만 들고, 초기화/패배보고만 NetworkManager에 위임한다.
/// 게임오버 전환은 NetworkManager가 HP 0 감지 시 GameEvents.SessionEnded()로 발행한다.
/// </summary>
public class PlayerHealthManager : MonoBehaviour
{
    [Header("라이프 설정 (공용 라이프 — 밸런스 기획서 §2.1: 1)")]
    // 밸런스 기획서 §2.1: 공유 HP 1 (한 번 지면 게임 종료). 두 플레이어가 모두 패배(BothLose)하면 1 감소 → 즉시 게임오버.
    // 실제 라이프 차감은 NetworkManager가 팀 결과(BothLose) 판정 시 권위로 처리한다.
    // ⚠️ SerializeField — 씬/프리팹 인스펙터 값이 우선. 인스펙터도 1로 맞출 것.
    [SerializeField] private int _maxLives = 1;

    /// <summary>현재 공용 라이프. NetworkManager(Room 속성)에서 읽음.</summary>
    public int Health => GameManager.Instance != null && GameManager.Instance.Network != null
        ? GameManager.Instance.Network.TeamHealth
        : -1;
    public int MaxHealth => _maxLives;
    public bool IsDead => Health == 0; // -1(미초기화)는 사망 아님

    private void OnEnable()  => GameEvents.OnBattleEnd += HandleBattleEnd;
    private void OnDisable() => GameEvents.OnBattleEnd -= HandleBattleEnd;

    private void Start()
    {
        var net = GameManager.Instance != null ? GameManager.Instance.Network : null;
        if (net == null) return;

        // MasterClient가 공용 라이프를 최초 1회 초기화. 비마스터는 동기화된 값을 받기만 함.
        net.InitTeamHealth(_maxLives);

        // 이미 초기화돼 있으면(후입장/재접속) 현재 값으로 UI 동기화.
        if (net.TeamHealth >= 0) GameEvents.HealthChanged(net.TeamHealth);
    }

    private void HandleBattleEnd(BattleEndReason reason)
    {
        // 내 보드 승패를 팀에 보고(승/패 둘 다). 두 플레이어가 모두 보고하면 MasterClient가
        // 팀 결과(BothWin/Split/BothLose)를 판정하고, 둘 다 패배일 때만 라이프 -1을 권위로 처리한다.
        // 라이프 감소/게임오버 통지는 NetworkManager의 Room 속성 변경 콜백에서 발행된다.
        bool isWin = reason == BattleEndReason.Victory || reason == BattleEndReason.DecisionVictory;
        var net = GameManager.Instance != null ? GameManager.Instance.Network : null;
        net?.ReportBattleResult(isWin);
        Debug.Log($"[Health] 전투 결과 보고: {reason} ({(isWin ? "승" : "패")})");
    }
}
