using UnityEngine;

public class JumpState_Player : PlayerBaseState
{
    private readonly int jumpingHash = Animator.StringToHash("Jumping"); // 애니메이션 하나로 통일

    private bool isLongJump;
    private Vector3 fixedJumpDirection;
    private float fixedJumpSpeed;

    public JumpState_Player(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

    public override void Enter()
    {
        isLongJump = player.isRunning;

        // 시작하자마자 바로 Jumping 애니메이션 하나만 실행
        player.animator.CrossFadeInFixedTime(jumpingHash, 0.1f);

        // 1. 위로 뛰는 힘 적용
        player.verticalVelocity = player.jumpForce;
        fixedJumpDirection = CalculateMoveDirection();

        if (player.moveInput == Vector2.zero)
        {
            fixedJumpSpeed = 0f;
        }
        else
        {
            // 핵심: 그리드(Tile) 기반 점프 거리 계산 로직

            // 기획서 반영: 숏점프 = 바닥타일 1, 건너뛰기 = 바닥타일 1.5
            float targetTiles = isLongJump ? 1.5f : 1.0f;
            float targetDistance = player.tileSize * targetTiles;

            // 물리 공식 (체공 시간 계산): v = v0 + at 에서 최고점 도달 시간(t) = v0 / a
            // 총 체공 시간 = 최고점 도달 시간 * 2
            float gravityAccel = Mathf.Abs(player.gravity * player.gravityMultiplier);
            float estimatedAirTime = (player.jumpForce / gravityAccel) * 2f;

            // 속력 = 거리 / 시간
            // 이 속도로 수평 이동하면 바닥에 닿는 순간 정확히 targetDistance에 도달함
            fixedJumpSpeed = targetDistance / estimatedAirTime;
        }
    }

    public override void Update()
    {
        // --- 착지 판정 (Land) ---
        // 수직 속도가 0 이하(떨어지는 중)이고, 캐릭터 컨트롤러가 바닥에 닿았을 때
        if (player.verticalVelocity <= 0f && player.characterController.isGrounded)
        {
            player.MoveCharacter(Vector3.zero);

            // 착지 애니메이션(End)을 기다릴 필요 없이 즉시 다음 상태로 전환하여 조작감 극대화
            if (player.moveInput == Vector2.zero) stateMachine.ChangeState<IdleState_Player>();
            else if (player.isRunning) stateMachine.ChangeState<RunState_Player>();
            else stateMachine.ChangeState<WalkState_Player>();

            return;
        }

        // --- 공중 조작 (Enter에서 정해둔 고정 속도로 날아감) ---
        if (fixedJumpSpeed > 0f)
        {
            HandleRotation(fixedJumpDirection);
        }

        player.MoveCharacter(fixedJumpDirection * fixedJumpSpeed * Time.deltaTime);
    }
}