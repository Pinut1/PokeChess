using UnityEngine;

/// <summary>
/// GameScene 진입 시 1회 실행되는 부트스트랩. 내 클라이언트가 게임 씬 로드를 마쳤음을
/// NetworkManager에 통지한다(NotifySceneReady). 두 클라가 모두 통지하면 MasterClient가
/// 라운드 1을 시작한다.
///
/// 왜 필요한가: 로비→게임 씬 전환 직후엔 게임 씬의 매니저들이 아직 살아있지 않아,
/// 로비에서 라운드 시작 RPC를 보내면 유실된다. 씬 로드 완료 후 핸드셰이크로 시작 시점을 맞춘다.
///
/// 배치: GameSceneTest 씬에만 GameObject 하나에 붙인다(로비 씬엔 두지 않음).
/// </summary>
public class GameSceneBootstrap : MonoBehaviour
{
    private void Start()
    {
        var net = GameManager.Instance != null ? GameManager.Instance.Network : null;
        if (net == null)
        {
            Debug.LogError("[GameSceneBootstrap] NetworkManager를 찾을 수 없음 — 라운드 시작 통지 실패");
            return;
        }
        net.NotifySceneReady();
    }
}
