using UnityEngine;

/// <summary>
/// FOV 밖에서 플레이어가 달려 접근했을 때, 플레이어 방향으로 회전하며 이동합니다.
/// </summary>
public class EnemyAlertState : EnemyBaseState
{
    private readonly int chaseAnimHash = Animator.StringToHash("Chase");

    private float stuckTimer;
    private Vector3 lastTrackedPosition;
    private Vector3 lastSetDestination;
    private bool useDirectMove;

    public EnemyAlertState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }

    public override void Enter()
    {
        stuckTimer = 0f;
        lastTrackedPosition = enemy.transform.position;
        lastSetDestination = enemy.transform.position;
        useDirectMove = false;

        enemy.ResetAnimatorLocomotionSpeed();
        enemy.animator.CrossFadeInFixedTime(chaseAnimHash, 0.1f);

        if (!enemy.TryActivateNavMeshAgent(enemy.moveSpeed, 0.25f))
        {
            useDirectMove = true;
            return;
        }

        if (enemy.agent != null)
            enemy.agent.updateRotation = false;

        UpdateAlertDestination(force: true);
    }

    public override void Update()
    {
        if (enemy.TryDetectPlayer())
        {
            stateMachine.ChangeState<EnemyChaseState>();
            return;
        }

        if (enemy.target == null || !enemy.IsTargetWithinDetectionRange())
        {
            enemy.target = null;
            stateMachine.ChangeState<EnemyPatrolState>();
            return;
        }

        PlayerController controller = enemy.target.GetComponentInParent<PlayerController>();
        if (controller == null || !controller.isRunning)
        {
            enemy.target = null;
            stateMachine.ChangeState<EnemyPatrolState>();
            return;
        }

        enemy.FaceToward(enemy.target.position);

        if (useDirectMove || enemy.agent == null || !enemy.agent.isOnNavMesh)
        {
            useDirectMove = true;
            enemy.DeactivateNavMeshAgent();
            UpdateDirectMove();
            return;
        }

        UpdateAlertDestination(force: false);
        enemy.TryRecoverFromMovementStuck(enemy.target.position, ref stuckTimer, ref lastTrackedPosition);
    }

    public override void Exit()
    {
        enemy.DeactivateNavMeshAgent();
    }

    private void UpdateDirectMove()
    {
        if (enemy.target == null) return;

        Vector3 dir = enemy.target.position - enemy.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        enemy.MoveEnemy(dir.normalized * enemy.moveSpeed * Time.deltaTime);
    }

    private void UpdateAlertDestination(bool force)
    {
        if (enemy.agent == null || !enemy.agent.isOnNavMesh || enemy.target == null)
            return;

        Vector3 destination = enemy.GetNavMeshPointNear(enemy.target.position);

        if (!force && (destination - lastSetDestination).sqrMagnitude < 0.09f)
            return;

        lastSetDestination = destination;
        enemy.agent.SetDestination(destination);
    }
}
