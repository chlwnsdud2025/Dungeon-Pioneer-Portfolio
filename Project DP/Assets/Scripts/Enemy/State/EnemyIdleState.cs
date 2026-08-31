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
            return;
        }

        if (enemy.TryDetectPlayer())
        {
            stateMachine.ChangeState<EnemyChaseState>();
            return;
        }

        if (enemy.TryDetectRunningPlayerNearby(out _))
        {
            stateMachine.ChangeState<EnemyAlertState>();
        }
    }
}