using UnityEngine;

/// <summary>
/// 지각의 설정값 공급자. 시리얼라이즈 필드는 소유자(EnemyController/NpcActor)에 그대로 두고
/// 여기서는 읽기만 합니다 — 필드를 다른 컴포넌트로 옮기면 이미 만들어 둔 프리팹의
/// 인스펙터 값이 전부 초기화되기 때문입니다.
/// </summary>
public interface IPerceptionAgent
{
    Transform Transform { get; }
    float ViewAngle { get; }
    float DetectionRadius { get; }
    LayerMask ObstacleLayer { get; }

    /// <summary>시야선 레이를 쏘는 눈높이(로컬 y). 키가 다른 액터가 같은 코드를 쓰게 합니다.</summary>
    float EyeHeight { get; }
}

/// <summary>
/// 액터의 '감각'. 보이는지만 답하고, 움직이거나 상태를 바꾸지 않습니다.
///
/// 누구를 쫓을지(정책)는 소유자가 정합니다 — 적은 플레이어·횃불을, NPC는 전리품·동료를
/// 찾겠지만 "FOV 안인가", "벽에 막혔나"를 재는 식은 모두 같습니다.
/// </summary>
public class Perception
{
    private readonly IPerceptionAgent agent;

    public Perception(IPerceptionAgent agent)
    {
        this.agent = agent;
    }

    private Vector3 Eye => agent.Transform.position + Vector3.up * agent.EyeHeight;

    public float HorizontalDistanceTo(Vector3 worldPosition)
        => ActorMath.HorizontalDistance(agent.Transform.position, worldPosition);

    /// <summary>월드 위치가 전방 FOV 각도 안에 있는지 (장애물 무시).</summary>
    public bool IsInFieldOfView(Vector3 worldPosition)
    {
        Vector3 dirToTarget = worldPosition - agent.Transform.position;
        dirToTarget.y = 0f;
        if (dirToTarget.sqrMagnitude < 0.0001f) return true;

        return Vector3.Angle(agent.Transform.forward, dirToTarget.normalized) <= agent.ViewAngle * 0.5f;
    }

    /// <summary>눈에서 aimPoint까지 벽에 막히지 않았는지. FOV 각도는 보지 않습니다.</summary>
    public bool HasLineOfSight(Vector3 aimPoint)
    {
        Vector3 eye = Eye;
        Vector3 rayDir = (aimPoint - eye).normalized;
        float distanceToTarget = Vector3.Distance(eye, aimPoint);

        if (!Physics.Raycast(eye, rayDir, out RaycastHit hit, distanceToTarget, agent.ObstacleLayer,
                QueryTriggerInteraction.Ignore))
            return true;

        // Pushable/Chest 등 Interactable은 시야를 가리지 않고, Wall만 가립니다.
        return !(hit.collider.CompareTag("Wall") || hit.collider.GetComponentInParent<WallObject>() != null);
    }

    /// <summary>
    /// 대상이 FOV 안에 있고 벽으로 시야가 막히지 않았는지.
    /// 레이의 끝점은 대상이 알려주는 AimPoint입니다 — 벽에 붙은 횃불이 자기 벽에 막혀
    /// '안 보임'으로 오판하지 않게 합니다.
    /// </summary>
    public bool CanSee(ITarget target)
    {
        if (target == null || target.Transform == null) return false;
        if (!IsInFieldOfView(target.Transform.position)) return false;
        return HasLineOfSight(target.AimPoint);
    }

    /// <summary>위치가 FOV 안이면 전체 탐지 반경, 밖이면 1/3 반경을 씁니다.</summary>
    public float GetDetectionRangeFor(Vector3 worldPosition)
        => IsInFieldOfView(worldPosition) ? agent.DetectionRadius : agent.DetectionRadius / 3f;
}
