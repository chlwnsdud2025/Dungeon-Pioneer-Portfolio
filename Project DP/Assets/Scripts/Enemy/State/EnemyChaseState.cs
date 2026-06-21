using UnityEngine;

public class EnemyChaseState : EnemyBaseState
{
    private readonly int chaseAnimHash = Animator.StringToHash("Chase"); // 뛰기 애니메이션 이름 확인 필요!

    public EnemyChaseState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.animator.CrossFadeInFixedTime(chaseAnimHash, 0.1f);
    }

    public override void Update()
    {
        // 타겟 상실 시 대기 상태로
        if (enemy.target == null)
        {
            stateMachine.ChangeState<EnemyIdleState>();
            return;
        }

        float distanceToPlayer = Vector3.Distance(enemy.transform.position, enemy.target.position);

        // 1. 어그로 해제 판정 (탐지 반경의 1.5배 이상 멀어지면 포기)
        if (distanceToPlayer > enemy.detectionRadius * 1.5f)
        {
            enemy.target = null;
            stateMachine.ChangeState<EnemyIdleState>();
            return;
        }

        // 2. 공격 사거리 도달 및 쿨타임 검사
        if (distanceToPlayer <= enemy.attackRange && Time.time >= enemy.lastAttackTime + enemy.attackCooldown)
        {
            stateMachine.ChangeState<EnemyAttackState>();
            return;
        }

        // 3. 타겟을 향해 회전하며 이동
        Vector3 dirToTarget = (enemy.target.position - enemy.transform.position).normalized;
        dirToTarget.y = 0f;

        if (dirToTarget != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(dirToTarget);
            enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, targetRotation, 10f * Time.deltaTime);
        }

        // 앞으로 이동 (MoveEnemy 내부에서 중력 자동 계산)
        enemy.MoveEnemy(enemy.transform.forward * enemy.moveSpeed * Time.deltaTime);
    }
}