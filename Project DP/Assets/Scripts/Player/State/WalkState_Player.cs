using UnityEngine;

public class WalkState_Player : PlayerBaseState
{
    private readonly int walkAnimHash = Animator.StringToHash("Walk_Player");

    public WalkState_Player(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

    public override void Enter()
    {
        // 걷기 애니메이션 실행
        player.animator.CrossFadeInFixedTime(walkAnimHash, 0.1f);
    }

    public override void Update()
    {
        // 입력이 멈추면 대기 상태로 전이
        if (player.moveInput == Vector2.zero)
        {
            stateMachine.ChangeState<IdleState_Player>();
            return;
        }

        // (선택) 걷는 도중 연타 판정이 들어왔을 때 즉시 뛰기로 바꾸고 싶다면 추가
        if (player.isRunning)
        {
            stateMachine.ChangeState<RunState_Player>();
            return;
        }

        // 카메라 기준 이동 방향 계산 (기존 방식 유지)
        Vector3 moveDir = CalculateMoveDirection();

        HandleRotation(moveDir);

        // 걷기 속도 적용하여 이동
        player.MoveCharacter(moveDir * player.walkSpeed * Time.deltaTime);
    }

   

    // 카메라 방향을 기준으로 한 이동 벡터 계산 편의 메서드
    
}