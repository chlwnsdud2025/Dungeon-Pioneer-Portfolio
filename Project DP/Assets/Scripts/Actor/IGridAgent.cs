using UnityEngine;

/// <summary>
/// GridMover가 움직일 수 있는 액터. 적(EnemyController)과 NPC가 공통으로 구현합니다.
///
/// GridMover가 EnemyController를 직접 알지 않게 하려고 만든 경계입니다. 멤버는 GridMover가
/// 실제로 쓰는 것만 두고 그 이상은 넣지 않습니다 — 인터페이스가 커질수록 구현체가
/// 쓰지도 않을 빈칸을 억지로 채워야 합니다.
/// </summary>
public interface IGridAgent
{
    Transform Transform { get; }

    /// <summary>지금 서 있는 장소의 그리드. 층 전환 중에는 null일 수 있습니다.</summary>
    LocationGrid Grid { get; }

    Vector2Int CurrentCell { get; }

    /// <summary>구덩이를 오르내릴 때 잠시 껐다 켜야 해서 필요합니다. 반경(radius)은 경로 평활화에 씁니다.</summary>
    CharacterController Controller { get; }

    /// <summary>
    /// 구덩이 탈출이 끝난 뒤 CharacterController를 다시 켜도 되는지.
    /// 구덩이 안에서 죽은 액터의 컨트롤러를 되살리면 시체가 일어서는 것처럼 보입니다.
    /// </summary>
    bool IsAlive { get; }

    /// <summary>구덩이 탈출 직후 낙하 속도를 초기화하기 위해 쓰기가 필요합니다.</summary>
    float VerticalVelocity { get; set; }

    /// <summary>바닥함정 셀의 경로 비용. 1이면 일반 바닥과 같게 취급해 최단 경로로 밟고 지나갑니다.</summary>
    float PathTrapCost { get; }

    /// <summary>구덩이에서 인접 셀로 기어오르는 데 걸리는 시간(초).</summary>
    float PitClimbDuration { get; }

    /// <summary>
    /// 구덩이 탈출 지점의 바닥 높이를 잴 때 쓸 레이어 마스크.
    /// 액터마다 '바닥으로 치지 않을 것'이 달라서 구현체가 정합니다.
    /// </summary>
    int GroundProbeMask { get; }

    /// <summary>중력을 포함한 실제 이동. 한 프레임의 변위를 받습니다.</summary>
    void Move(Vector3 motion);

    /// <summary>진행 방향으로 서서히 회전. 회전 속도는 구현체가 정합니다.</summary>
    void RotateToward(Vector3 direction);
}
