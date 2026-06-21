using UnityEngine;

public class GuardState_Player : PlayerBaseState
{
    private readonly int guardLoopHash = Animator.StringToHash("Guard_Loop");
    private readonly int guardHitHash = Animator.StringToHash("Guard_Hit"); // 막고 밀리는 애니메이션

    private bool isInputReleased = false;

    // 방어 성공 시 밀림(Knockback) 처리용 변수
    private float impactTimer = 0f;
    private float impactDuration = 0.2f;
    private Vector3 impactDirection;

    public GuardState_Player(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

    public override void Enter()
    {
        isInputReleased = false;
        player.isGuarding = true; // 컨트롤러에 방어 중임을 알림
        impactTimer = 0f;

        // 방어 대기 자세 애니메이션 재생
        player.animator.CrossFadeInFixedTime(guardLoopHash, 0.1f);
    }

    public override void Update()
    {
        // 밀림(Impact) 처리 중일 때
        if (impactTimer > 0f)
        {
            impactTimer -= Time.deltaTime;
            // 뒤로 살짝 밀려나는 물리력 적용 (밀리는 속도 2f)
            player.MoveCharacter(impactDirection * 2f * Time.deltaTime);
            return; // 밀리는 동안에는 다른 조작 제한
        }

        // 방어 키를 뗐다면 대기 상태로 복귀
        if (isInputReleased)
        {
            stateMachine.ChangeState<IdleState_Player>();
            return;
        }

        // 방어 중 이동 (기획에 따라 0으로 막거나 걷기 속도의 절반으로 허용)
        if (player.moveInput != Vector2.zero)
        {
            Vector3 moveDir = CalculateMoveDirection();
            HandleRotation(moveDir);
            // 방어 중에는 속도를 절반으로 줄임
            player.MoveCharacter(moveDir * (player.walkSpeed * 0.5f) * Time.deltaTime);
        }
        else
        {
            player.MoveCharacter(Vector3.zero);
        }
    }

    public override void Exit()
    {
        player.isGuarding = false;
    }

    // Input System에서 Guard를 취소(버튼 뗌)했을 때 불리는 함수
    public override void OnGuardReleased()
    {
        isInputReleased = true;
    }

    // PlayerController에서 방어 성공 시 호출해주는 함수
    public void OnBlockSuccess(Vector3 attackerPos)
    {
        // 1. 막혔을 때 밀리는 충격 애니메이션 재생 (Guard_Hit)
        player.animator.CrossFadeInFixedTime(guardHitHash, 0.05f);

        // 2. 적의 반대 방향으로 밀려날 벡터 계산
        Vector3 pushDir = (player.transform.position - attackerPos).normalized;
        pushDir.y = 0f;
        impactDirection = pushDir;

        // 3. 밀림 타이머 리셋
        impactTimer = impactDuration;
    }

    public override void OnJumpInput() { }
    public override void OnAttackInput() { }



}