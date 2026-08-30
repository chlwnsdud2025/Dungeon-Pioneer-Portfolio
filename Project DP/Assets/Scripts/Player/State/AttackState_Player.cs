using UnityEngine;

public class AttackState_Player : PlayerBaseState
{
    private readonly int attackAnimHash = Animator.StringToHash("Attack_Player");

    // 모션이 거의 끝났다고 보고 다음 상태로 넘어가는 지점. (EnemyAttackState와 같은 기준)
    private const float EndNormalized = 0.95f;

    private bool hitboxOpened;   // 이번 스윙에서 판정을 이미 열었는지
    private bool hitboxClosed;

    public AttackState_Player(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

    public override void Enter()
    {
        hitboxOpened = false;
        hitboxClosed = false;

        // 1. 공격 애니메이션 실행
        player.animator.CrossFadeInFixedTime(attackAnimHash, 0.1f);

        // 2. 방향키를 누르고 공격했다면, 그 방향으로 즉시 캐릭터를 회전시킴
        //    ★ 이 상태 클래스에서 따로 CalculateMoveDirection()을 재구현하지 않고
        //      PlayerBaseState의 공통 로직(Camera.main 기준)을 그대로 사용합니다.
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
        AnimatorStateInfo info = player.animator.GetCurrentAnimatorStateInfo(0);

        // CrossFade가 끝나기 전에는 아직 이전 상태가 잡히므로, 공격 상태로 확정된 뒤부터 판정합니다.
        if (info.shortNameHash != attackAnimHash) return;

        // ★ 실시간 초가 아니라 진행률(0~1)로 판단합니다. Attack_Player 상태는 1.5배속으로 돌고 있어서
        //   초 단위로 맞춰두면 배속이나 클립을 건드리는 순간 판정이 어긋납니다.
        float progress = info.normalizedTime;

        if (!hitboxOpened && progress >= player.hitboxOpenAt)
        {
            OpenHitbox();
            hitboxOpened = true;
        }
        else if (hitboxOpened && !hitboxClosed && progress >= player.hitboxCloseAt)
        {
            CloseHitbox();
        }

        if (progress >= EndNormalized)
        {
            if (player.moveInput == Vector2.zero)
                stateMachine.ChangeState<IdleState_Player>();
            else
                stateMachine.ChangeState<WalkState_Player>();
        }
    }

    /// <summary>
    /// 피격·넉백·사망·씬 이동 등으로 스윙 도중 상태가 끊겨도 콜라이더가 켜진 채 남지 않도록 반드시 닫는다.
    /// (PlayerController.PrepareForSceneArrival의 강제 Idle 전환도 이 경로를 탄다)
    /// </summary>
    public override void Exit()
    {
        CloseHitbox();
    }

    private void OpenHitbox()
    {
        WeaponHitbox hitbox = player.WeaponHitbox;
        if (hitbox == null) return;   // 무기 프리팹에 히트박스가 없음 — EquipmentVisualizer가 이미 경고를 남긴다

        hitbox.Begin(player.stat.FinalAttackDamage, player.enemyLayer);
    }

    private void CloseHitbox()
    {
        hitboxClosed = true;

        // 스윙 도중 무기를 바꾸면 히트박스가 파괴됐을 수 있다.
        WeaponHitbox hitbox = player.WeaponHitbox;
        if (hitbox != null) hitbox.End();
    }

    // 공격 도중 다른 입력 막기
    public override void OnAttackInput() { /* MVP에서는 콤보 선입력 무시 */ }
    public override void OnInteractInput() { }
    public override void OnJumpInput() { }
}
