using UnityEngine;

/// <summary>
/// 전투 중 유닛 모델이 현재 노리는 대상을 바라보게 회전시킨다.
///
/// 시뮬레이션은 헥스 칸 단위로 즉시 이동하지만 회전만은 프레임 단위로 부드럽게 돌린다.
/// 틱(0.1초)마다 각도를 대입하면 방향 전환이 툭툭 끊겨 보이기 때문.
///
/// 모델 프리팹이 가진 기본 자세(기울기 등)는 _basePose에 따로 보관하고 요(yaw)만 덧씌운다.
/// rotation을 통째로 대입하면 프리팹의 기울기가 사라진다.
/// </summary>
public class UnitFacing : MonoBehaviour
{
    [Tooltip("초당 회전 각도. 낮추면 굼뜨게, 높이면 즉각적으로 돈다.")]
    [SerializeField] private float _turnSpeed = 720f;

    private Quaternion _basePose = Quaternion.identity;
    private Quaternion _desired  = Quaternion.identity;

    /// <summary>
    /// 프리팹 기본 자세와 시작 방향을 지정한다.
    /// 시작 방향은 즉시 반영한다 — 전투 시작 순간에 유닛이 빙 도는 연출은 원치 않으므로.
    /// </summary>
    public void Initialize(Quaternion basePose, Vector3 worldDirection)
    {
        _basePose = basePose;
        _desired  = YawFor(worldDirection);
        transform.rotation = _desired;
    }

    /// <summary>월드 좌표를 향하도록 목표 회전을 갱신한다. 같은 칸이면(방향 0) 직전 방향을 유지.</summary>
    public void FaceTowards(Vector3 worldPoint)
    {
        _desired = YawFor(worldPoint - transform.position);
    }

    private Quaternion YawFor(Vector3 direction)
    {
        direction.y = 0f; // 수평 회전만 — 높이 차가 섞이면 모델이 앞뒤로 눕는다
        if (direction.sqrMagnitude < 0.0001f) return _desired;

        // worldYaw * basePose 순서. SpawnVisual이 쓰던 Rotate(0,180,0,Space.World)와 같은 형태라
        // 프리팹 기울기가 그대로 유지된다.
        return Quaternion.LookRotation(direction, Vector3.up) * _basePose;
    }

    private void Update()
    {
        if (transform.rotation == _desired) return;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, _desired, _turnSpeed * Time.deltaTime);
    }
}
