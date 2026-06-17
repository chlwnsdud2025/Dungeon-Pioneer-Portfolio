using UnityEngine;

public class IdleState_Player : PlayerBaseState
{
    private readonly int idleAnimHash = Animator.StringToHash("Idle_Player");
    public IdleState_Player(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

    public override void Enter()
    {
        // 대기 애니메이션 실행
        player.animator.CrossFadeInFixedTime(idleAnimHash, 0.1f);
    }



    public override void Update()
    {
        // 이동 입력이 들어온 경우
        if (player.moveInput != Vector2.zero)
        {
            // 컨트롤러에서 연타로 판정했다면 뛰기로, 아니면 걷기로 전이
            if (player.isRunning)
            {
                stateMachine.ChangeState<RunState_Player>();
            }
            else
            {
                stateMachine.ChangeState<WalkState_Player>();
            }
            return;
        }

        // 제자리에 있을 때 밀림 방지 등을 위해 0 벡터 전달
        player.MoveCharacter(Vector3.zero);
    }

    

}