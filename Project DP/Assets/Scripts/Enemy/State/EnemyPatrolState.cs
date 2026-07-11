using UnityEngine;
using UnityEngine.AI; // NavMesh 사용

public class EnemyPatrolState : EnemyBaseState
{
    private readonly int walkAnimHash = Animator.StringToHash("Walk");

    private bool hasTarget = false;
    public float tileSize = 1f; // 타일 크기에 맞게 설정

    public EnemyPatrolState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.animator.CrossFadeInFixedTime(walkAnimHash, 0.1f);

        // ★ 순찰 시작: NavMeshAgent 켜기
        if (enemy.agent != null)
        {
            enemy.agent.enabled = true;
            if (enemy.agent.isOnNavMesh)
            {
                enemy.agent.isStopped = false;
                enemy.agent.speed = enemy.moveSpeed * 0.5f; // 순찰은 추적보다 천천히 걷게 설정
                enemy.agent.stoppingDistance = 0f; // 목표 타일에 정확히 서도록 설정

                // ★ 순찰 중에는 함정을 피해서 우회하도록 설정 (에이전트가 NavMesh에 올라간 뒤에만 호출 가능)
                enemy.SetTrapAvoidance(true);
            }
        }

        SetNewPatrolDestination();
    }

    public override void Update()
    {
        DetectPlayer();

        if (enemy.target != null)
        {
            stateMachine.ChangeState<EnemyChaseState>();
            return;
        }

        if (hasTarget && enemy.agent != null)
        {
            // 에이전트가 경로 끝(목표 타일)에 도착했는지 검사
            if (!enemy.agent.pathPending && enemy.agent.remainingDistance <= enemy.agent.stoppingDistance)
            {
                if (!enemy.agent.hasPath || enemy.agent.velocity.sqrMagnitude == 0f)
                {
                    hasTarget = false;
                    stateMachine.ChangeState<EnemyIdleState>();
                }
            }
        }
    }

    public override void Exit()
    {
        // ★ 순찰 종료: 수동 이동(Idle 등)과 꼬이지 않도록 Agent 끄기
        if (enemy.agent != null && enemy.agent.isOnNavMesh)
        {
            enemy.agent.isStopped = true;
            enemy.agent.ResetPath();
            enemy.agent.enabled = false;
        }
    }

    private void SetNewPatrolDestination()
    {
        // 1. 현재 위치 기준으로 상하좌우 중 한 곳 타일 중앙 계산
        float snapX = Mathf.Round(enemy.transform.position.x / tileSize) * tileSize;
        float snapZ = Mathf.Round(enemy.transform.position.z / tileSize) * tileSize;
        Vector3 gridCenterPos = new Vector3(snapX, enemy.transform.position.y, snapZ);

        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

        // 2. 방향 배열을 무작위로 섞음 (랜덤 방향 탐색용)
        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 temp = directions[i];
            int randomIndex = Random.Range(i, directions.Length);
            directions[i] = directions[randomIndex];
            directions[randomIndex] = temp;
        }

        hasTarget = false;

        // 3. 섞인 방향들을 하나씩 검사
        foreach (Vector3 dir in directions)
        {
            Vector3 potentialTarget = gridCenterPos + (dir * tileSize);

            NavMeshHit hit;
            // ★ 핵심: Raycast로 벽을 쏘지 않고, NavMesh에게 "이 좌표 근처(0.5f 반경)가 네가 구워놓은 길(NavMesh) 위냐?" 라고 물어봅니다.
            if (NavMesh.SamplePosition(potentialTarget, out hit, tileSize * 0.5f, NavMesh.AllAreas))
            {
                if (enemy.agent != null && enemy.agent.isOnNavMesh)
                {
                    enemy.agent.SetDestination(hit.position);
                }
                hasTarget = true;
                break; // 갈 수 있는 길을 하나 찾았으면 반복문 종료
            }
        }

        // 사방이 다 막혀있거나 절벽이라면 포기하고 다시 대기 상태로 들어갑니다.
        if (!hasTarget)
        {
            stateMachine.ChangeState<EnemyIdleState>();
        }
    }

    private void DetectPlayer()
    {
        Collider[] hitPlayers = Physics.OverlapSphere(enemy.transform.position, enemy.detectionRadius, enemy.playerLayer);
        if (hitPlayers.Length > 0)
        {
            Transform potentialTarget = hitPlayers[0].transform;
            Vector3 dirToTarget = (potentialTarget.position - enemy.transform.position).normalized;

            if (Vector3.Angle(enemy.transform.forward, dirToTarget) < enemy.viewAngle / 2f)
            {
                Vector3 eyePosition = enemy.transform.position + Vector3.up * 1.5f;
                Vector3 targetPos = potentialTarget.position + Vector3.up * 1.5f;
                float distanceToTarget = Vector3.Distance(eyePosition, targetPos);

                if (!Physics.Raycast(eyePosition, dirToTarget, distanceToTarget, enemy.obstacleLayer))
                {
                    enemy.target = potentialTarget;
                }
            }
        }
    }
}
