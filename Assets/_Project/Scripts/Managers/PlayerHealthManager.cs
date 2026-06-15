using UnityEngine;

/// <summary>
/// 플레이어 체력 관리. 전투 패배 시 체력이 깎이고, 0이 되면 게임오버(세션 종료)로 이어진다.
/// 매니저 간 직접 참조 없이 GameEvents.OnBattleEnd 구독 → 결과 반영.
/// 게임오버는 별도 페이즈 도입 대신 기존 GameEvents.SessionEnded → RoundPhaseManager의
/// GamePhase.GameOver 전환을 재사용한다.
/// </summary>
public class PlayerHealthManager : MonoBehaviour
{
    [Header("체력 설정 (밸런스 — 인스펙터 조정 가능, 현재 값은 임시 기본값)")]
    [SerializeField] private int _maxHealth = 100;
    // TODO: TFT처럼 "생존한 적 유닛 수/성급 합"만큼 가변 데미지로 확장 가능. v1은 고정값.
    [SerializeField] private int _damagePerLoss = 10;

    public int Health { get; private set; }
    public int MaxHealth => _maxHealth;
    public bool IsDead => Health <= 0;

    private void Awake() => Health = _maxHealth;

    private void OnEnable()  => GameEvents.OnBattleEnd += HandleBattleEnd;
    private void OnDisable() => GameEvents.OnBattleEnd -= HandleBattleEnd;

    private void Start() => GameEvents.HealthChanged(Health); // 초기 체력 UI 동기화

    private void HandleBattleEnd(bool isWin)
    {
        if (isWin || IsDead) return; // 승리 또는 이미 사망 시 변화 없음

        Health = Mathf.Max(0, Health - _damagePerLoss);
        GameEvents.HealthChanged(Health);
        Debug.Log($"[Health] 패배 — 체력 -{_damagePerLoss} → {Health}/{_maxHealth}");

        if (Health <= 0)
        {
            Debug.Log("[Health] 체력 0 — 게임오버");
            GameEvents.SessionEnded(); // → RoundPhaseManager가 GamePhase.GameOver로 전환
        }
    }
}
