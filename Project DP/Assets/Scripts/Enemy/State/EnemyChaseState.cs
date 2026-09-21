using UnityEngine;

public class EnemyChaseState : EnemyBaseState
{
    private readonly int chaseAnimHash = Animator.StringToHash("Chase");

    public EnemyChaseState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.ResetAnimatorLocomotionSpeed();
        enemy.animator.CrossFadeInFixedTime(chaseAnimHash, 0.1f);

        if (enemy.target != null)
        {
            enemy.Mover.SetDestination(
                LocationGrid.WorldToCell(enemy.target.position), true, enemy.moveSpeed,
                enemy.target, enemy.attackRange * 0.8f);
        }
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

        if (!enemy.Mover.IsClimbing
            && distanceToPlayer <= enemy.attackRange
            && Time.time >= enemy.lastAttackTime + enemy.attackCooldown
            && enemy.IsInFieldOfView(enemy.target.position))
        {
            stateMachine.ChangeState<EnemyAttackState>();
            return;
        }

        if (!enemy.Mover.IsMoving)
            enemy.RotateToward(enemy.target.position - enemy.transform.position);
    }

    public override void Exit()
    {
        enemy.Mover.Stop();
    }
}
