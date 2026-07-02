using UnityEngine;

public class EnemyIdleState : EnemyBaseState
{
    private readonly int idleAnimHash = Animator.StringToHash("Idle");

    private float idleDuration = 2.0f; // 대기 시간 (원하는 만큼 조절하세요)
    private float timer = 0f;

    public EnemyIdleState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.animator.CrossFadeInFixedTime(idleAnimHash, 0.1f);
        timer = 0f; // 대기 상태 진입 시 타이머 초기화
    }

    public override void Update()
    {
        // 1. 대기 중에는 미끄러지지 않게 이동 벡터 0 고정
        enemy.MoveEnemy(Vector3.zero);

        // 2. 타이머 체크: 설정된 대기 시간이 끝나면 순찰 상태로 전환
        timer += Time.deltaTime;
        if (timer >= idleDuration)
        {
            stateMachine.ChangeState<EnemyPatrolState>();
            return; // 상태가 변경되었으므로 아래 탐색 로직은 실행하지 않음
        }

        // 3. 탐지 반경 내 플레이어 탐색
        Collider[] hitPlayers = Physics.OverlapSphere(enemy.transform.position, enemy.detectionRadius, enemy.playerLayer);

        if (hitPlayers.Length > 0)
        {
            Transform potentialTarget = hitPlayers[0].transform;
            Vector3 dirToTarget = (potentialTarget.position - enemy.transform.position).normalized;

            // 4. 시야각(View Angle) 내에 들어왔는지 검사
            if (Vector3.Angle(enemy.transform.forward, dirToTarget) < enemy.viewAngle / 2f)
            {
                // 5. 장애물(벽)에 가려져 있는지 레이캐스트로 최종 검사 (가슴 높이 기준)
                Vector3 eyePosition = enemy.transform.position + Vector3.up * 1.5f;
                Vector3 targetPosition = potentialTarget.position + Vector3.up * 1.5f;
                Vector3 rayDir = (targetPosition - eyePosition).normalized;
                float distanceToTarget = Vector3.Distance(eyePosition, targetPosition);

                // 레이저가 장애물(obstacleLayer)에 닿지 않았다면 (시야가 뚫려있다면)
                if (!Physics.Raycast(eyePosition, rayDir, distanceToTarget, enemy.obstacleLayer))
                {
                    enemy.target = potentialTarget;
                    stateMachine.ChangeState<EnemyChaseState>();
                }
            }
        }
    }
}