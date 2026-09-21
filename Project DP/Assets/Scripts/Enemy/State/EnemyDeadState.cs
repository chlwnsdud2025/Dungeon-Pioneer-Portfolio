using UnityEngine;

public class EnemyDeadState : EnemyBaseState
{
    private readonly int deadAnimHash = Animator.StringToHash("Die"); // 애니메이터 사망 모션 이름에 맞추세요 ("Dead" 등)
    private float timer = 0f;
    private float destroyDelay = 3f; // 3초 후 시체 삭제

    public EnemyDeadState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.Mover.Stop();
        timer = 0f;
        enemy.animator.CrossFadeInFixedTime(deadAnimHash, 0.1f);

        if (enemy.characterController != null)
            enemy.characterController.enabled = false;

        Debug.Log("적 사망!");

        // TODO: 나중에 아이템을 떨어뜨리려면 여기에 드롭 로직 추가
    }

    public override void Update()
    {
        timer += Time.deltaTime;

        // CharacterController가 꺼졌으므로 MoveEnemy는 호출하지 않습니다.

        // 일정 시간 후 오브젝트 제거
        if (timer >= destroyDelay)
        {
            Object.Destroy(enemy.gameObject);
        }
    }
}