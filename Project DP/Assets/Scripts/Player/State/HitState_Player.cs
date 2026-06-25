using UnityEngine;

public class HitState_Player : PlayerBaseState
{
    private readonly int hitAnimHash = Animator.StringToHash("Hit");

    public HitState_Player(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

    public override void Enter()
    {
        // 피격 애니메이션 재생
        player.animator.CrossFadeInFixedTime(hitAnimHash, 0.1f);
    }

    public override void Update()
    {
        // 경직 중에는 수평 이동을 막고, 중력만 받도록 0 벡터 적용
        player.MoveCharacter(Vector3.zero);

        // 현재 재생 중인 애니메이션 정보 가져오기
        AnimatorStateInfo stateInfo = player.animator.GetCurrentAnimatorStateInfo(0);

        // 현재 애니메이션이 'Hit'이고, 재생 진행도가 95% 이상 도달했다면 상태 전환
        if (stateInfo.shortNameHash == hitAnimHash && stateInfo.normalizedTime >= 0.95f)
        {
            if (player.currentHealth <= 0)
            {
                stateMachine.ChangeState<DeadState_Player>();
            }
            else
            {
                // 살아있다면 다시 대기 상태로 복귀
                stateMachine.ChangeState<IdleState_Player>();
            }
        }
    }

    public override void OnAttackInput() { }
    public override void OnJumpInput() { }
    public override void OnInteractInput() { }
    public override void OnGuardInput() { }
}