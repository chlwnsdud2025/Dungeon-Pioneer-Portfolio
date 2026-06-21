using UnityEngine;

public class JumpState_Player : PlayerBaseState
{
    private readonly int jumpStartHash = Animator.StringToHash("JumpStart");
    private readonly int jumpingHash = Animator.StringToHash("Jumping");
    private readonly int jumpEndHash = Animator.StringToHash("JumpEnd");

    private enum JumpPhase { Start, Airborne, Land }
    private JumpPhase currentPhase;

    private bool isLongJump;
    private Vector3 fixedJumpDirection;
    private float fixedJumpSpeed;

    public JumpState_Player(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

    public override void Enter()
    {
        isLongJump = player.isRunning;
        currentPhase = JumpPhase.Start;

        player.animator.CrossFadeInFixedTime(jumpStartHash, 0.1f);

        // 1. 위로 뛰는 힘 적용
        player.verticalVelocity = player.jumpForce;
        fixedJumpDirection = CalculateMoveDirection();

        if (player.moveInput == Vector2.zero)
        {
            fixedJumpSpeed = 0f;
        }
        else
        {
            // ★ 핵심: 그리드(Tile) 기반 점프 거리 계산 로직 ★

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
        // --- [페이즈 3] 착지(Land) ---
        if (currentPhase == JumpPhase.Land)
        {
            player.MoveCharacter(Vector3.zero);

            AnimatorStateInfo stateInfo = player.animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.shortNameHash == jumpEndHash && stateInfo.normalizedTime >= 0.8f)
            {
                if (player.moveInput == Vector2.zero) stateMachine.ChangeState<IdleState_Player>();
                else if (player.isRunning) stateMachine.ChangeState<RunState_Player>();
                else stateMachine.ChangeState<WalkState_Player>();
            }
            return;
        }

        // --- [페이즈 1, 2] 공중 조작 (Enter에서 정해둔 고정 속도로 날아감) ---
        if (fixedJumpSpeed > 0f)
        {
            HandleRotation(fixedJumpDirection);
        }

        player.MoveCharacter(fixedJumpDirection * fixedJumpSpeed * Time.deltaTime);

        // --- 페이즈 전환 로직 ---
        if (currentPhase == JumpPhase.Start && player.verticalVelocity <= 0f)
        {
            currentPhase = JumpPhase.Airborne;
            player.animator.CrossFadeInFixedTime(jumpingHash, 0.1f);
        }

        if (player.verticalVelocity <= 0f && player.characterController.isGrounded)
        {
            currentPhase = JumpPhase.Land;
            player.animator.CrossFadeInFixedTime(jumpEndHash, 0.05f);
        }
    }

    
}