using UnityEngine;

public class EnemyAlertState : EnemyBaseState
{
    private readonly int chaseAnimHash = Animator.StringToHash("Chase");

    public EnemyAlertState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.ResetAnimatorLocomotionSpeed();
        enemy.animator.CrossFadeInFixedTime(chaseAnimHash, 0.1f);
        enemy.Mover.FaceMovementDirection = false;

        if (enemy.target != null)
        {
            enemy.Mover.SetDestination(
                enemy.target.ApproachCell, false, enemy.moveSpeed, enemy.target.Transform, 0.25f);
        }
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

        // 이 상태는 '달리는 플레이어'에게만 반응하므로 대상은 항상 플레이어여야 한다.
        PlayerController controller = enemy.target as PlayerController;
        if (controller == null || !controller.isRunning)
        {
            enemy.target = null;
            stateMachine.ChangeState<EnemyPatrolState>();
            return;
        }

        enemy.FaceToward(enemy.target.Transform.position);
    }

    public override void Exit()
    {
        enemy.Mover.FaceMovementDirection = true;
        enemy.Mover.Stop();
    }
}
