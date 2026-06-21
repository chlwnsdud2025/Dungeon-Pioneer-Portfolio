using UnityEngine;

public class RunState_Player : PlayerBaseState
{
    private readonly int runAnimHash = Animator.StringToHash("Run_Player");

    public RunState_Player(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

    public override void Enter()
    {
        // 뛰기 애니메이션 실행
        player.animator.CrossFadeInFixedTime(runAnimHash, 0.1f);
    }

    public override void Update()
    {
        // 입력이 멈추면 대기 상태로 전이 (이때 컨트롤러의 isRunning도 자연스럽게 false가 됨)
        if (player.moveInput == Vector2.zero)
        {
            stateMachine.ChangeState<IdleState_Player>();
            return;
        }

        Vector3 moveDir = CalculateMoveDirection();

        HandleRotation(moveDir);

        // 뛰기 속도 적용하여 이동
        player.MoveCharacter(moveDir * player.runSpeed * Time.deltaTime);
    }

    

   
}