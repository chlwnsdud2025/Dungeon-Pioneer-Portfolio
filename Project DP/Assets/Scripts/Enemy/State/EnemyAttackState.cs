using UnityEngine;

public class EnemyAttackState : EnemyBaseState
{
    private readonly int attackAnimHash = Animator.StringToHash("Attack");

    private float hitTime = 0.5f; // 무기를 휘둘러 데미지가 들어가는 시점 (애니메이션에 맞춰 조절 필요)
    private float timer = 0f;
    private bool hasDealtDamage = false;

    public EnemyAttackState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.DeactivateNavMeshAgent();
        enemy.MoveEnemy(Vector3.zero);
        enemy.lastAttackTime = Time.time;

        timer = 0f;
        hasDealtDamage = false;

        enemy.animator.CrossFadeInFixedTime(attackAnimHash, 0.1f);

        // 공격 시작 시 플레이어 방향으로 확실하게 한 번 바라봅니다.
        if (enemy.target != null)
        {
            Vector3 dirToTarget = (enemy.target.position - enemy.transform.position).normalized;
            dirToTarget.y = 0f;
            if (dirToTarget != Vector3.zero)
            {
                enemy.transform.rotation = Quaternion.LookRotation(dirToTarget);
            }
        }
    }

    public override void Update()
    {
        timer += Time.deltaTime;

        // 1. 데미지 판정 처리 (딱 한 번만)
        if (timer >= hitTime && !hasDealtDamage)
        {
            hasDealtDamage = true;
            PerformAttackHitbox();
        }

        // 2. 애니메이션 종료 체크 (대략 95% 이상 재생되었을 때)
        AnimatorStateInfo stateInfo = enemy.animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.shortNameHash == attackAnimHash && stateInfo.normalizedTime >= 0.95f)
        {
            // 공격이 끝나면 다시 추적 상태로 돌아가서 거리를 재평가합니다.
            stateMachine.ChangeState<EnemyChaseState>();
        }
    }

    private void PerformAttackHitbox()
    {
        Vector3 hitCenter = enemy.transform.position + enemy.transform.forward * 1f + Vector3.up * 0.5f;
        Collider[] hitPlayers = Physics.OverlapSphere(hitCenter, enemy.attackRange, enemy.playerLayer);

        foreach (Collider playerCol in hitPlayers)
        {
            PlayerController player = playerCol.GetComponent<PlayerController>();
            if (player == null) continue;
            if (!enemy.IsInFieldOfView(player.transform.position)) continue;

            player.TakeDamage(5f, enemy.transform);
        }
    }
}