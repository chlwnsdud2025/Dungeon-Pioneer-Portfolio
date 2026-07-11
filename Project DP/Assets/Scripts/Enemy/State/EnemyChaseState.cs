using UnityEngine;
using UnityEngine.AI;

public class EnemyChaseState : EnemyBaseState
{
    private readonly int chaseAnimHash = Animator.StringToHash("Chase");

    public EnemyChaseState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.animator.CrossFadeInFixedTime(chaseAnimHash, 0.1f);

        // ★ 추적 시작: NavMeshAgent를 켜고 목표 추적을 시작합니다.
        if (enemy.agent != null)
        {
            enemy.agent.enabled = true;
            if (enemy.agent.isOnNavMesh)
            {
                enemy.agent.isStopped = false;
                enemy.agent.speed = enemy.moveSpeed; // 인스펙터의 이동 속도와 동기화
                enemy.agent.stoppingDistance = enemy.attackRange * 0.8f; // 사거리 조금 앞에서 멈추도록 설정

                // ★ 추격 중에는 함정을 꺼리지 않고 그대로 밟고 지나가도록 허용 (TrapTile이 알아서 데미지를 줌)
                enemy.SetTrapAvoidance(false);
            }
        }
    }

    public override void Update()
    {
        if (enemy.target == null)
        {
            stateMachine.ChangeState<EnemyIdleState>();
            return;
        }

        float distanceToPlayer = Vector3.Distance(enemy.transform.position, enemy.target.position);

        // 1. 어그로 해제 판정
        if (distanceToPlayer > enemy.detectionRadius * 1.5f)
        {
            enemy.target = null;
            stateMachine.ChangeState<EnemyIdleState>();
            return;
        }

        // 2. 공격 사거리 도달 검사
        if (distanceToPlayer <= enemy.attackRange && Time.time >= enemy.lastAttackTime + enemy.attackCooldown)
        {
            stateMachine.ChangeState<EnemyAttackState>();
            return;
        }

        // 3. NavMeshAgent에게 플레이어 위치 갱신 전달
        if (enemy.agent != null && enemy.agent.isOnNavMesh)
        {
            enemy.agent.SetDestination(enemy.target.position);
        }
    }

    public override void Exit()
    {
        // ★ 추적 종료: 다른 상태의 수동 이동 로직(MoveEnemy)과 충돌하지 않도록 에이전트의 전원을 완전히 차단합니다.
        if (enemy.agent != null && enemy.agent.isOnNavMesh)
        {
            // ★ 추격이 끝나면 다시 함정을 회피하도록 되돌립니다 (에이전트를 끄기 전, 아직 NavMesh 위에 있을 때 호출).
            enemy.SetTrapAvoidance(true);

            enemy.agent.isStopped = true;
            enemy.agent.ResetPath();
            enemy.agent.enabled = false;
        }
    }
}
