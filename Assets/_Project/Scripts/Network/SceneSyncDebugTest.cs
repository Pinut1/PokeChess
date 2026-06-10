using UnityEngine;
using UnityEngine.SceneManagement;

#if PHOTON_UNITY_NETWORKING
using Photon.Pun;

/// <summary>
/// 씬 전환 동기화 확인용 임시 스크립트. 검증 후 삭제 예정.
/// GameSceneTest 씬에 배치 — 양쪽 클라이언트가 같은 씬으로 전환됐는지 화면에 표시.
/// </summary>
public class SceneSyncDebugTest : MonoBehaviour
{
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 320, 120));
        GUILayout.Label($"Scene      : {SceneManager.GetActiveScene().name}");
        GUILayout.Label($"Is Master  : {PhotonNetwork.IsMasterClient}");
        GUILayout.Label($"Players    : {PhotonNetwork.CurrentRoom?.PlayerCount ?? 0}");
        GUILayout.EndArea();
    }
}

#else

public class SceneSyncDebugTest : MonoBehaviour
{
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 320, 120));
        GUILayout.Label($"Scene : {SceneManager.GetActiveScene().name}");
        GUILayout.Label("PUN2 미설치 — 오프라인 모드");
        GUILayout.EndArea();
    }
}

#endif
