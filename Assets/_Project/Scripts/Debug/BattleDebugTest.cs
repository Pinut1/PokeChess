#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// BattleManager 플레이 모드 검증용.
/// 전투 시작/종료 이벤트를 로그로 출력하고, Battle 페이즈가 아니어도 수동으로 전투를 트리거할 수 있는 버튼 제공.
/// </summary>
public class BattleDebugTest : MonoBehaviour
{
    private void OnEnable()
    {
        GameEvents.OnBattleStart += LogBattleStart;
        GameEvents.OnBattleEnd   += LogBattleEnd;
    }

    private void OnDisable()
    {
        GameEvents.OnBattleStart -= LogBattleStart;
        GameEvents.OnBattleEnd   -= LogBattleEnd;
    }

    private void OnGUI()
    {
        if (GUI.Button(new Rect(10, 300, 200, 40), "전투 시작 (디버그)"))
            GameEvents.BattleStart();

        var gm = GameManager.Instance;
        if (gm == null) return;

        // 체력 표시 + 강제 승/패(게임오버 경로 테스트용 — 실제 전투 없이 BattleEnd 발화)
        if (gm.PlayerHealth != null)
            GUI.Label(new Rect(10, 345, 200, 24), $"체력: {gm.PlayerHealth.Health}/{gm.PlayerHealth.MaxHealth}");

        if (GUI.Button(new Rect(10, 372, 200, 30), "강제 승리 (BattleEnd true)"))
            GameEvents.BattleEnd(true);
        if (GUI.Button(new Rect(10, 406, 200, 30), "강제 패배 (BattleEnd false)"))
            GameEvents.BattleEnd(false);

        if (gm.Phase != null && gm.Phase.CurrentPhase == GamePhase.GameOver)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 28, normal = { textColor = Color.red } };
            GUI.Label(new Rect(10, 445, 400, 40), "=== GAME OVER ===", style);
        }
    }

    private static void LogBattleStart() => Debug.Log("[BattleTest] 전투 시작 — 보드 미러링 후 시뮬레이션 진행");

    private static void LogBattleEnd(bool isWin) =>
        Debug.Log($"[BattleTest] 전투 종료 — 결과: {(isWin ? "승리" : "패배")}");
}
#endif
