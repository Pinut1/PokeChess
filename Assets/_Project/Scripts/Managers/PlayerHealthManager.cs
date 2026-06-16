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
    [Header("체력 설정 (밸런스 — 인스펙터 조정 가능, 현재 값은 임시 기본값)")]
    [SerializeField] private int _maxHealth = 100;
    // TODO: TFT처럼 "생존한 적 유닛 수/성급 합"만큼 가변 데미지로 확장 가능. v1은 고정값.
    [SerializeField] private int _damagePerLoss = 10;

    /// <summary>현재 팀 공통 HP. NetworkManager(Room 속성)에서 읽음.</summary>
    public int Health => GameManager.Instance != null && GameManager.Instance.Network != null
        ? GameManager.Instance.Network.TeamHealth
        : -1;
    public int MaxHealth => _maxHealth;
    public bool IsDead => Health == 0; // -1(미초기화)는 사망 아님

    private void OnEnable()  => GameEvents.OnBattleEnd += HandleBattleEnd;
    private void OnDisable() => GameEvents.OnBattleEnd -= HandleBattleEnd;

    private void Start()
    {
        var net = GameManager.Instance != null ? GameManager.Instance.Network : null;
        if (net == null) return;

        // MasterClient가 팀 HP를 최초 1회 초기화. 비마스터는 동기화된 값을 받기만 함.
        net.InitTeamHealth(_maxHealth);

        // 이미 초기화돼 있으면(후입장/재접속) 현재 값으로 UI 동기화.
        if (net.TeamHealth >= 0) GameEvents.HealthChanged(net.TeamHealth);
    }

    private void HandleBattleEnd(bool isWin)
    {
        if (isWin) return;

        // 내 전투 패배 → 팀 공통 HP에 데미지 반영 요청(마스터 권위로 처리됨).
        // HP 감소/게임오버 통지는 NetworkManager의 Room 속성 변경 콜백에서 발행된다.
        var net = GameManager.Instance != null ? GameManager.Instance.Network : null;
        net?.ReportBattleLoss(_damagePerLoss);
        Debug.Log($"[Health] 패배 — 팀 HP -{_damagePerLoss} 요청");
    }
}
