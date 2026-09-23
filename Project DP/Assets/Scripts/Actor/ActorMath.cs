using UnityEngine;

/// <summary>
/// 수평면(XZ) 기준의 거리·회전 계산. 적·NPC가 같은 식을 쓰도록 모아둡니다.
///
/// y축을 버리는 이유: 이 게임의 판정은 모두 그리드 평면 기준이라, 높이차가 있는
/// 구덩이 안팎에서도 거리와 각도가 같게 나와야 합니다.
/// </summary>
public static class ActorMath
{
    public static float HorizontalDistance(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        delta.y = 0f;
        return delta.magnitude;
    }

    /// <summary>즉시 그 방향을 바라보게 합니다. (수평 회전만)</summary>
    public static void FaceToward(Transform self, Vector3 worldPosition)
    {
        Vector3 dir = worldPosition - self.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            self.rotation = Quaternion.LookRotation(dir.normalized);
    }

    /// <summary>turnSpeed(도/초)만큼 서서히 그 방향으로 돌립니다.</summary>
    public static void RotateToward(Transform self, Vector3 direction, float turnSpeed)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion desired = Quaternion.LookRotation(direction.normalized);
        self.rotation = Quaternion.RotateTowards(self.rotation, desired, turnSpeed * Time.deltaTime);
    }
}
