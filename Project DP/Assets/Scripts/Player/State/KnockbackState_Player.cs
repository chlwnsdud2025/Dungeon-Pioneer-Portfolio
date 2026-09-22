using UnityEngine;

public class KnockbackState_Player : PlayerBaseState
{
    private readonly int knockbackAnimHash = Animator.StringToHash("Knockback"); // 넘어지는 애니메이션

    private float timer;         // 남은 넉백(넘어져 있는) 시간
    private float initialSpeed;  // 감속 시작 속도 (총 이동거리가 knockbackDistance가 되도록 계산)
    private Vector3 knockDir;

    public KnockbackState_Player(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

    public override void Enter()
    {
        player.isInvincible = true;   // 무적 + RollingRock 재피격 무시
        player.isGuarding = false;    // 넉백 시 방어 자세는 강제로 풀림

        knockDir = player.knockbackDirection;
        knockDir.y = 0f;

        timer = player.knockbackTime;

        // 선형 감속(속도 v0 → 0)에서 총 이동거리 = 0.5 * v0 * T 이므로,
        // 원하는 거리(knockbackDistance)를 정확히 밀려나려면 v0 = 2 * 거리 / 시간.
        initialSpeed = player.knockbackTime > 0f
            ? (3f * player.knockbackDistance / player.knockbackTime)   // 2f → 3f
            : 0f;

        // 넘어지는 애니메이션 재생
        player.animator.CrossFadeInFixedTime(knockbackAnimHash, 0.1f);
    }

    public override void Update()
    {
        if (timer > 0f)
        {
            timer -= Time.deltaTime;

            // 남은 시간 비율의 '제곱'으로 감속 → 처음에 훨씬 빠르게 밀리고 빠르게 멈춰 눕습니다.
            float u = Mathf.Clamp01(player.knockbackTime > 0f ? timer / player.knockbackTime : 0f);
            float factor = u * u;   // ← 제곱 (front-load 강화)
            float curSpeed = initialSpeed * factor;
            player.MoveCharacter(knockDir * curSpeed * Time.deltaTime);
            return;
        }

        // 넘어져 있는 시간 종료 → 일어나서 대기 상태로 복귀 (Exit에서 무적 해제)
        stateMachine.ChangeState<IdleState_Player>();
    }

    public override void Exit()
    {
        player.isInvincible = false; // 무적 해제
    }

    // 넉백 도중에는 모든 조작 입력을 차단합니다.
    public override void OnAttackInput() { }
    public override void OnJumpInput() { }
    public override void OnInteractInput() { }
    public override void OnGuardInput() { }
    public override void OnGuardReleased() { }
}
