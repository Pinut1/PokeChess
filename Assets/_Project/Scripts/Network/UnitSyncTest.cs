using UnityEngine;

#if PHOTON_UNITY_NETWORKING
using Photon.Pun;

/// <summary>
/// RPC 기반 위치 동기화 패턴 디버그용. MasterClient가 주기적으로 각 유닛(큐브)의 목표 위치를 랜덤으로 정해 RPC로 전체에 브로드캐스트.
/// 모든 클라이언트는 수신한 목표 위치로만 이동 — 직접 랜덤을 굴리지 않음.
/// (6/10 검증 완료, 추후 RPC 동작 확인용으로 유지)
/// </summary>
public class UnitSyncTest : MonoBehaviourPun
{
    [Header("이동시킬 유닛(큐브)들")]
    [SerializeField] private Transform[] _units;

    [Header("이동 범위 (Plane 위 X/Z)")]
    [SerializeField] private float _minX = -4f;
    [SerializeField] private float _maxX = 4f;
    [SerializeField] private float _minZ = -4f;
    [SerializeField] private float _maxZ = 4f;

    [Header("설정")]
    [SerializeField] private float _moveInterval = 2f;
    [SerializeField] private float _smoothTime   = 0.5f;

    private Vector3[] _targets;
    private Vector3[] _velocities;
    private float _timer;

    /// <summary>인스펙터에 _units 자체를 비워두면 Unity가 "요소 1개, 미할당 참조" 배열을 만들어
    /// _units == null/Length == 0 검사를 통과해버리므로 각 요소를 직접 검사해야 함.</summary>
    private bool IsValid()
    {
        if (_units == null || _units.Length == 0) return false;
        foreach (var t in _units)
            if (t == null) return false;
        return true;
    }

    private void Start()
    {
        if (!IsValid()) return;

        _targets = new Vector3[_units.Length];
        _velocities = new Vector3[_units.Length];
        for (int i = 0; i < _units.Length; i++)
            _targets[i] = _units[i].position;
    }

    private void Update()
    {
        if (!IsValid()) return;

        // 이동 적용 (양쪽 클라이언트 공통) — 에어하키 퍽처럼 감속하며 도착
        for (int i = 0; i < _units.Length; i++)
            _units[i].position = Vector3.SmoothDamp(_units[i].position, _targets[i], ref _velocities[i], _smoothTime);

        // 목표 결정은 MasterClient만
        if (!PhotonNetwork.IsMasterClient) return;

        _timer += Time.deltaTime;
        if (_timer < _moveInterval) return;
        _timer = 0f;

        for (int i = 0; i < _units.Length; i++)
        {
            float x = Random.Range(_minX, _maxX);
            float z = Random.Range(_minZ, _maxZ);
            photonView.RPC(nameof(RPC_SetTarget), RpcTarget.All, i, x, z);
        }
    }

    [PunRPC]
    private void RPC_SetTarget(int unitIndex, float x, float z)
    {
        if (unitIndex < 0 || unitIndex >= _units.Length) return;
        _targets[unitIndex] = new Vector3(x, _units[unitIndex].position.y, z);
    }

    private void OnGUI()
    {
        if (!IsValid()) return;

        GUILayout.BeginArea(new Rect(10, 140, 320, 150));
        GUILayout.Label($"Is Master : {PhotonNetwork.IsMasterClient}");
        for (int i = 0; i < _units.Length; i++)
            GUILayout.Label($"Unit {i} : {_units[i].position:F2}");
        GUILayout.EndArea();
    }
}

#else

public class UnitSyncTest : MonoBehaviour
{
    [SerializeField] private Transform[] _units;

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 140, 320, 60));
        GUILayout.Label("PUN2 미설치 — 오프라인 모드");
        GUILayout.EndArea();
    }
}

#endif
