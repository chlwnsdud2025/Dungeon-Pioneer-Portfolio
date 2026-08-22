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

        // 2. 방향키를 누르고 공격했다면, 그 방향으로 즉시 캐릭터를 회전시킴
        //    ★ 수정: 이 상태 클래스에서 따로 CalculateMoveDirection()을 재구현하지 않고
        //      PlayerBaseState의 공통 로직(Camera.main 기준)을 그대로 사용합니다.
        //      (기존에는 player.cameraTransform 기준의 별도 구현이 있어서, cameraTransform이
        //       인스펙터에 연결되어 있지 않으면 공격 시에만 회전 방향이 다른 상태들과 어긋났습니다)
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
            enemy.GetComponent<EnemyController>().TakeDamage(player.stat.FinalAttackDamage);
        }
    }

    // 공격 도중 다른 입력 막기
    public override void OnAttackInput() { /* MVP에서는 콤보 선입력 무시 */ }
    public override void OnInteractInput() { }
    public override void OnJumpInput() { }
}