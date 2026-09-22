using System.Collections.Generic;
using UnityEngine;

public class EnemyAttackState : EnemyBaseState
{
    private readonly int attackAnimHash = Animator.StringToHash("Attack");

    private float hitTime = 0.5f; // 무기를 휘둘러 데미지가 들어가는 시점 (애니메이션에 맞춰 조절 필요)
    private float timer = 0f;
    private bool hasDealtDamage = false;

    // 한 번의 휘두름에 콜라이더가 여러 개인 대상을 중복으로 때리지 않도록
    private readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();

    public EnemyAttackState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.Mover.Stop();
        enemy.MoveEnemy(Vector3.zero);
        enemy.lastAttackTime = Time.time;

        timer = 0f;
        hasDealtDamage = false;

        enemy.animator.CrossFadeInFixedTime(attackAnimHash, 0.1f);

        // 공격 시작 시 대상 방향으로 확실하게 한 번 바라봅니다.
        if (enemy.target != null && enemy.target.Transform != null)
            enemy.FaceToward(enemy.target.Transform.position);
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
            // 대상이 아직 살아 있으면 추적으로 돌아가 거리를 재평가하고(쿨타임 뒤 재타격),
            // 이번 타격으로 없어졌으면(횃불 소멸 등) 순찰로 돌아갑니다.
            if (enemy.target != null && enemy.target.IsAlive)
                stateMachine.ChangeState<EnemyChaseState>();
            else
            {
                enemy.target = null;
                stateMachine.ChangeState<EnemyPatrolState>();
            }
        }
    }

    private void PerformAttackHitbox()
    {
        hitThisSwing.Clear();

        Vector3 hitCenter = enemy.transform.position + enemy.transform.forward * 1f + Vector3.up * 0.5f;
        Collider[] hits = Physics.OverlapSphere(hitCenter, enemy.attackRange, enemy.AttackTargetMask,
                                                QueryTriggerInteraction.Collide);

        foreach (Collider col in hits)
        {
            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive) continue;
            if (damageable == (IDamageable)enemy) continue;             // 자기 자신 제외
            if (!enemy.IsInFieldOfView(col.bounds.center)) continue;
            if (!hitThisSwing.Add(damageable)) continue;

            damageable.TakeDamage(enemy.attackDamage, enemy.transform);
        }
    }
}
