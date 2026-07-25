using UnityEngine;

/// <summary>
/// RollingRock 등에 부딪혔을 때 진입하는 넉백(넘어짐) 상태.
///  - 넘어지는 애니메이션("Knockback")을 재생하고
///  - 부딪힌 반대 방향으로 살짝(점점 감속하며) 밀리며
///  - 상태가 지속되는 동안 무적(isInvincible)이라 RollingRock에 다시 스쳐도 피해를 받지 않습니다.
///
/// 진입은 PlayerController.ApplyKnockback()을 통해서만 이뤄지며, 밀림 방향은 player.knockbackDirection로 전달됩니다.
/// </summary>
public class KnockbackState_Player : PlayerBaseState
{
    private readonly int knockbackAnimHash = Animator.StringToHash("Knockback"); // 넘어지는 애니메이션

    private float timer;
    private Vector3 knockDir;

    public KnockbackState_Player(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

    public override void Enter()
    {
        player.isInvincible = true;   // 무적 + RollingRock 재피격 무시
        player.isGuarding = false;    // 넉백 시 방어 자세는 강제로 풀림

        knockDir = player.knockbackDirection;
        knockDir.y = 0f;

        timer = player.knockbackDuration;

        // 넘어지는 애니메이션 재생
        player.animator.CrossFadeInFixedTime(knockbackAnimHash, 0.1f);
    }

    public override void Update()
    {
        if (timer > 0f)
        {
            timer -= Time.deltaTime;

            // 처음엔 빠르게, 끝으로 갈수록 느리게 감속하며 밀림 (timer/duration : 1 → 0)
            float factor = player.knockbackDuration > 0f ? (timer / player.knockbackDuration) : 0f;
            player.MoveCharacter(knockDir * player.knockbackSpeed * factor * Time.deltaTime);
            return;
        }

        // 넉백 종료 → 대기 상태로 복귀 (Exit에서 무적 해제)
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
