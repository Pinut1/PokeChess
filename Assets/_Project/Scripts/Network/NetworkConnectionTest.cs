#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 연결 테스트용 임시 스크립트. 검증 후 삭제 예정.
/// </summary>
public class NetworkConnectionTest : MonoBehaviour
{
    private NetworkManager _network;

    private void Awake()
    {
        _network = GetComponent<NetworkManager>();
        if (_network == null)
            _network = gameObject.AddComponent<NetworkManager>();
    }

    private void Start()
    {
        GameEvents.OnRoundChanged += round =>
            Debug.Log($"[Test] OnRoundChanged → {round}");

        _network.Connect();
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));

        GUILayout.Label($"Connected : {_network.IsConnected}");
        GUILayout.Label($"In Room   : {_network.IsInRoom}");
        GUILayout.Label($"Players   : {_network.PlayerCount}");
        GUILayout.Label($"Is Master : {_network.IsMasterClient}");

        GUILayout.Space(10);

        if (!_network.IsInRoom)
        {
            if (GUILayout.Button("JoinOrCreate Room")) _network.JoinOrCreateRoom("TestRoom");
        }
        else
        {
            if (GUILayout.Button("Broadcast Round 1")) _network.BroadcastRoundStart(1);
            if (GUILayout.Button("Leave Room"))        _network.LeaveRoom();
        }

        GUILayout.EndArea();
    }
}
#endif
