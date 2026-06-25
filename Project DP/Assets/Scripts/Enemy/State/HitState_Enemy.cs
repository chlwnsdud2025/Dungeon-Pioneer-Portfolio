using UnityEngine;

public class EnemyHitState : EnemyBaseState
{
    private readonly int hitAnimHash = Animator.StringToHash("Hit");

    public EnemyHitState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.animator.CrossFadeInFixedTime(hitAnimHash, 0.1f);
    }

    public override void Update()
    {
        // 경직 상태이므로 수평 이동은 0으로 주되, 공중에 떴을 때 중력은 적용받도록 함
        enemy.MoveEnemy(Vector3.zero);

        // 현재 재생 중인 애니메이션 정보 가져오기
        AnimatorStateInfo stateInfo = enemy.animator.GetCurrentAnimatorStateInfo(0);

        // 현재 애니메이션이 'Hit'이고, 재생 진행도가 95% 이상 도달했다면 상태 전환
        if (stateInfo.shortNameHash == hitAnimHash && stateInfo.normalizedTime >= 0.95f)
        {
            if (enemy.currentHealth <= 0)
            {
                stateMachine.ChangeState<EnemyDeadState>();
            }
            else
            {
                // 타겟이 여전히 있다면 추적, 없다면 대기 상태로
                if (enemy.target != null)
                {
                    stateMachine.ChangeState<EnemyChaseState>();
                }
                else
                {
                    stateMachine.ChangeState<EnemyIdleState>();
                }
            }
        }
    }
}