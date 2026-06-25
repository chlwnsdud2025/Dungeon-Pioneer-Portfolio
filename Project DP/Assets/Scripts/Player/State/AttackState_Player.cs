using UnityEngine;

public class AttackState_Player : PlayerBaseState
{
    private readonly int attackAnimHash = Animator.StringToHash("Attack_Player");

    private float attackDuration = 0.6f;  // 공격 애니메이션의 총 길이
    private float hitTime = 0.2f;         // 실제 데미지가 들어가는 시점 (애니메이션이 무기를 휘두르는 순간)

    private float timer = 0f;
    private bool hasDealtDamage = false;

    public AttackState_Player(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

    public override void Enter()
    {
        timer = 0f;
        hasDealtDamage = false;

        // 1. 공격 애니메이션 실행
        player.animator.CrossFadeInFixedTime(attackAnimHash, 0.1f);

        // 2. 방향키를 누르고 공격했다면, 그 방향으로 즉시 캐릭터를 회전시킴 (이전 코드의 장점 반영)
        Vector3 attackDir = CalculateMoveDirection();
        if (attackDir != Vector3.zero)
        {
            player.transform.rotation = Quaternion.LookRotation(attackDir);
        }

        // 공격 중에는 미끄러지지 않도록 제자리에 멈춥니다.
        player.MoveCharacter(Vector3.zero);
    }

    public override void Update()
    {
        timer += Time.deltaTime;

        // 3. 타격 판정 타이밍 (휘두르는 순간 딱 1번만 데미지 처리)
        if (timer >= hitTime && !hasDealtDamage)
        {
            PerformAttackHitbox();
            hasDealtDamage = true;
        }

        // 4. 애니메이션이 끝나면 상태 전환
        if (timer >= attackDuration)
        {
            if (player.moveInput == Vector2.zero)
            {
                stateMachine.ChangeState<IdleState_Player>();
            }
            else
            {
                stateMachine.ChangeState<WalkState_Player>();
            }
        }
    }

    // 공격 범위 내의 적을 찾아 데미지를 주는 함수
    private void PerformAttackHitbox()
    {
        // 플레이어 앞쪽으로 공격 범위(가상의 구체) 생성
        Vector3 hitCenter = player.transform.position + player.transform.forward * 1f + Vector3.up * 1f;

        // enemyLayer에 해당하는 콜라이더들을 모두 가져옴
        Collider[] hitEnemies = Physics.OverlapSphere(hitCenter, player.attackRange, player.enemyLayer);

        foreach (Collider enemy in hitEnemies)
        {
            // TODO: 나중에 슬라임(몬스터) 스크립트가 완성되면 아래처럼 데미지 함수를 호출합니다.
            enemy.GetComponent<EnemyController>().TakeDamage(player.attackDamage);

            //Debug.Log($"<color=red>{enemy.name} 에게 기본 타격 적중!</color>");
        }
    }

    private Vector3 CalculateMoveDirection()
    {
        if (player.cameraTransform == null) return new Vector3(player.moveInput.x, 0, player.moveInput.y).normalized;

        Vector3 camForward = player.cameraTransform.forward;
        Vector3 camRight = player.cameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        return (camRight * player.moveInput.x + camForward * player.moveInput.y).normalized;
    }

    // 공격 도중 다른 입력 막기
    public override void OnAttackInput() { /* MVP에서는 콤보 선입력 무시 */ }
    public override void OnInteractInput() { }
    public override void OnJumpInput() { }
}