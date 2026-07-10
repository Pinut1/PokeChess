#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// 연결 테스트용 임시 스크립트. 검증 후 삭제 예정.
/// </summary>
public class NetworkConnectionTest : MonoBehaviourPunCallbacks
{
    private NetworkManager _network;
    private List<RoomInfo> _roomList = new();
    private Vector2 _roomListScroll;
    private string _nicknameInput = "";

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

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        _roomList = roomList;
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 320, 420));

        GUILayout.Label($"Connected : {_network.IsConnected}");
        GUILayout.Label($"In Room   : {_network.IsInRoom}");
        GUILayout.Label($"Players   : {_network.PlayerCount}");
        GUILayout.Label($"Is Master : {_network.IsMasterClient}");
        GUILayout.Label($"AutoSync  : {PhotonNetwork.AutomaticallySyncScene}");
        GUILayout.Label($"Scene     : {SceneManager.GetActiveScene().name}");

        GUILayout.Label($"Nickname  : {_network.LocalNickname}");
        GUILayout.Label($"Partner   : {_network.PartnerNickname}");

        GUILayout.Space(10);

        if (!_network.IsInRoom)
        {
            GUILayout.Label("Nickname");
            _nicknameInput = GUILayout.TextField(_nicknameInput, 16);

            if (GUILayout.Button("JoinOrCreate Room (TestRoom)"))
            {
                if (_network.TrySetLocalNickname(_nicknameInput))
                    _network.JoinOrCreateRoom("TestRoom");
            }

            if (GUILayout.Button("Find Random Room"))
            {
                if (_network.TrySetLocalNickname(_nicknameInput))
                    _network.JoinRandomRoom();
            }

            GUILayout.Space(10);
            GUILayout.Label($"Room List ({_roomList.Count})");

            _roomListScroll = GUILayout.BeginScrollView(
                _roomListScroll,
                GUILayout.Height(150)
            );

            foreach (var room in _roomList)
            {
                GUILayout.Label(
                    $"- {room.Name}  ({room.PlayerCount}/{room.MaxPlayers})  Open:{room.IsOpen}"
                );
            }

            GUILayout.EndScrollView();
        }
        else
        {
            if (GUILayout.Button("Broadcast Round 1"))
                _network.BroadcastRoundStart(1);

            if (GUILayout.Button("Leave Room"))
                _network.LeaveRoom();
        }

        GUILayout.EndArea();
    }
}
#endif
