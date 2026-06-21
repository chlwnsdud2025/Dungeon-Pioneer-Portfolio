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
        // 1. 피격 시 밀림(Knockback) 처리 중일 때
        // (이때는 플레이어의 의지와 상관없이 적의 공격 충격으로 강제로 뒤로 밀려나야 합니다)
        if (impactTimer > 0f)
        {
            impactTimer -= Time.deltaTime;
            player.MoveCharacter(impactDirection * 2f * Time.deltaTime);
            return;
        }

        // 2. 방어 키를 뗐다면 대기 상태로 복귀
        if (isInputReleased)
        {
            stateMachine.ChangeState<IdleState_Player>();
            return;
        }

        // ★ 3. 핵심 수정 부분: 방향키 입력이 들어오면 캐릭터 회전만 수행합니다.
        if (player.moveInput != Vector2.zero)
        {
            Vector3 moveDir = CalculateMoveDirection();
            HandleRotation(moveDir); // 바라보는 방향 꺾기
        }

        // 위치는 철저하게 제자리에 고정시킵니다.
        // (단, 수직 중력 연산은 MoveCharacter 내부에서 정상 처리되므로 공중 가드 낙하 등도 안전합니다)
        player.MoveCharacter(Vector3.zero);
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
        // 막혔을 때 밀리는 충격 애니메이션 재생 (Guard_Hit)
        player.animator.CrossFadeInFixedTime(guardHitHash, 0.05f);

        // 적의 반대 방향으로 밀려날 벡터 계산
        Vector3 pushDir = (player.transform.position - attackerPos).normalized;
        pushDir.y = 0f;
        impactDirection = pushDir;

        // 밀림 타이머 리셋
        impactTimer = impactDuration;
    }

    // 방어 자세를 굳건히 유지하기 위해 제어권 차단
    public override void OnJumpInput() { }
    public override void OnAttackInput() { }
}