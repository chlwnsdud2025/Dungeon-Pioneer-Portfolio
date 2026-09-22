using UnityEngine;

/// <summary>
/// 대상(ITarget)에게 다가가 공격 사거리에 들면 Attack으로 넘깁니다. 대상이 플레이어인지 횃불인지는
/// 구분하지 않고, 대상이 알려주는 ApproachCell/IsMobile로만 이동 방식을 정합니다.
///
/// ■ 플레이어 우선
/// 고정 대상(횃불)을 쫓는 중에도 매 프레임 플레이어 탐지를 돌려, 보이면 상태 전환 없이 target만
/// 플레이어로 바꾸고 목적지를 다시 잡습니다. (플레이어에게 맞았을 때는 TakeDamage가 target을 바꿔둠)
///
/// ■ 포기 규칙
///  · 이동 대상: 탐지 반경×1.5를 벗어나면 놓친다 (기존 규칙)
///  · 고정 대상: 경로가 없거나, 다 왔는데도 사거리에 못 든 채 torchGiveUpSeconds가 지나면 잠시 무시 목록에 넣고 순찰로
/// </summary>
public class EnemyChaseState : EnemyBaseState
{
    private readonly int chaseAnimHash = Animator.StringToHash("Chase");

    private float outOfReachTimer;   // 고정 대상 앞에 도착했는데도 사거리에 못 든 시간

    public EnemyChaseState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.ResetAnimatorLocomotionSpeed();
        enemy.animator.CrossFadeInFixedTime(chaseAnimHash, 0.1f);
        outOfReachTimer = 0f;
        SetDestinationToTarget();
    }

    public override void Update()
    {
        ITarget target = enemy.target;
        if (target == null || !target.IsAlive || target.Transform == null)
        {
            enemy.target = null;
            stateMachine.ChangeState<EnemyPatrolState>();
            return;
        }

        // 고정 대상(횃불)을 쫓는 중이라도 플레이어가 보이면 플레이어로 갈아탄다.
        if (!target.IsMobile)
        {
            if (enemy.TryDetectPlayer())
            {
                outOfReachTimer = 0f;
                SetDestinationToTarget();
                return;
            }
            if (enemy.TryDetectRunningPlayerNearby(out _))
            {
                stateMachine.ChangeState<EnemyAlertState>();
                return;
            }
        }

        if (target.IsMobile)
        {
            if (!enemy.IsTargetWithinDetectionRange())
            {
                enemy.target = null;
                stateMachine.ChangeState<EnemyPatrolState>();
                return;
            }
        }
        else if (ShouldGiveUpStaticTarget(target))
        {
            enemy.IgnoreTarget(target, enemy.torchIgnoreSeconds);
            enemy.target = null;
            stateMachine.ChangeState<EnemyPatrolState>();
            return;
        }

        Vector3 targetPosition = target.Transform.position;
        float distanceToTarget = enemy.GetHorizontalDistanceTo(targetPosition);

        if (!enemy.Mover.IsClimbing
            && distanceToTarget <= enemy.attackRange
            && Time.time >= enemy.lastAttackTime + enemy.attackCooldown
            && enemy.IsInFieldOfView(targetPosition))
        {
            stateMachine.ChangeState<EnemyAttackState>();
            return;
        }

        if (!enemy.Mover.IsMoving)
            enemy.RotateToward(targetPosition - enemy.transform.position);
    }

    public override void Exit()
    {
        enemy.Mover.Stop();
    }

    /// <summary>
    /// 이동 대상은 따라가며 재탐색하고 구덩이도 밟고 지나가지만, 고정 대상은 그 앞 셀을 목적지로 못 박는다.
    /// (횃불 앞 셀은 통행 가능 여부를 탐지 단계에서 이미 걸렀다)
    /// </summary>
    private void SetDestinationToTarget()
    {
        ITarget target = enemy.target;
        if (target == null) return;

        if (target.IsMobile)
        {
            enemy.Mover.SetDestination(
                target.ApproachCell, true, enemy.moveSpeed, target.Transform, enemy.attackRange * 0.8f);
        }
        else
        {
            enemy.Mover.SetDestination(target.ApproachCell, false, enemy.moveSpeed);
        }
    }

    /// <summary>
    /// 시야로 판단하지 않는 이유: 이미 위치를 아는 고정 대상이라 걸어가는 동안 모퉁이에 가려지는 건 정상이다.
    /// 대신 '갈 수 없게 됐는지'와 '다 왔는데도 못 때리는지'만 본다.
    /// </summary>
    private bool ShouldGiveUpStaticTarget(ITarget target)
    {
        // 경로가 아예 없거나, 가는 사이 앞 셀이 막혔으면(Pushable이 밀려오는 등) 기다려봐야 소용없다.
        if (enemy.Mover.LastResult == PathResult.None) return true;
        if (enemy.Grid != null && !enemy.Grid.IsPassable(target.ApproachCell, false)) return true;

        // 도착했는데도 사거리에 들지 못하는 상태가 이어지면 포기 (배치가 어긋난 벽 등)
        bool arrivedButOutOfReach = enemy.Mover.HasArrived
            && enemy.GetHorizontalDistanceTo(target.Transform.position) > enemy.attackRange;
        if (!arrivedButOutOfReach)
        {
            outOfReachTimer = 0f;
            return false;
        }

        outOfReachTimer += Time.deltaTime;
        return outOfReachTimer >= enemy.torchGiveUpSeconds;
    }
}
