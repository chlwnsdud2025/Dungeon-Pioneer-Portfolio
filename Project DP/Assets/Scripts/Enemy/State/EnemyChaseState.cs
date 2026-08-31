using UnityEngine;

public class EnemyChaseState : EnemyBaseState
{
    private readonly int chaseAnimHash = Animator.StringToHash("Chase");

    private float stuckTimer;
    private Vector3 lastTrackedPosition;
    private Vector3 lastSetDestination;
    private bool useDirectMove;

    public EnemyChaseState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }

    public override void Enter()
    {
        stuckTimer = 0f;
        lastTrackedPosition = enemy.transform.position;
        lastSetDestination = enemy.transform.position;
        useDirectMove = false;

        enemy.ResetAnimatorLocomotionSpeed();
        enemy.animator.CrossFadeInFixedTime(chaseAnimHash, 0.1f);

        if (!enemy.TryActivateNavMeshAgent(enemy.moveSpeed, enemy.attackRange * 0.8f))
        {
            // NavMesh가 없어도 FOV로 감지한 플레이어 방향으로는 직접 이동합니다.
            useDirectMove = true;
            return;
        }

        UpdateChaseDestination(force: true);
    }

    public override void Update()
    {
        if (enemy.target == null)
        {
            stateMachine.ChangeState<EnemyPatrolState>();
            return;
        }

        float distanceToPlayer = enemy.GetHorizontalDistanceTo(enemy.target.position);

        if (!enemy.IsTargetWithinDetectionRange())
        {
            enemy.target = null;
            stateMachine.ChangeState<EnemyPatrolState>();
            return;
        }

        if (distanceToPlayer <= enemy.attackRange
            && Time.time >= enemy.lastAttackTime + enemy.attackCooldown
            && enemy.IsInFieldOfView(enemy.target.position))
        {
            stateMachine.ChangeState<EnemyAttackState>();
            return;
        }

        if (useDirectMove)
        {
            UpdateDirectChase();
            return;
        }

        if (enemy.agent == null || !enemy.agent.isOnNavMesh)
        {
            useDirectMove = true;
            enemy.DeactivateNavMeshAgent();
            UpdateDirectChase();
            return;
        }

        UpdateChaseDestination(force: false);
        enemy.TryRecoverFromMovementStuck(enemy.target.position, ref stuckTimer, ref lastTrackedPosition);
    }

    public override void Exit()
    {
        if (!useDirectMove)
            enemy.DeactivateNavMeshAgent();
        else
            enemy.DeactivateNavMeshAgent();
    }

    private void UpdateDirectChase()
    {
        if (enemy.target == null) return;

        enemy.FaceToward(enemy.target.position);

        Vector3 dir = enemy.target.position - enemy.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        enemy.MoveEnemy(dir.normalized * enemy.moveSpeed * Time.deltaTime);
    }

    private void UpdateChaseDestination(bool force)
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
