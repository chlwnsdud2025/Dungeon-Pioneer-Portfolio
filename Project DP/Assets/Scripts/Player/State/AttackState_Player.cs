using UnityEngine;

public class AttackState_Player : PlayerBaseState
{
    private readonly int attackAnimHash = Animator.StringToHash("Attack_Player");

    // 애니메이션 전이 시간(초). 0이면 즉시 공격 상태로 전환됩니다.
    private const float TransitionDuration = 0f;

    // 모션이 거의 끝났다고 보고 다음 상태로 넘어가는 지점.
    private const float EndNormalized = 0.95f;

    private float enterTime;
    private bool hitboxActive;

    public AttackState_Player(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

    public override void Enter()
    {
        enterTime = Time.time;
        hitboxActive = false;

        // ★ 레이어(0)와 시작 오프셋(0f)을 반드시 명시합니다.
        //   2인자 오버로드는 오프셋 기본값이 float.NegativeInfinity라 "그 상태가 이미 재생 중이면
        //   현재 시간에서 이어서" 시작합니다. 연속 공격 시 직전 스윙의 끝(진행률 0.95 부근)에서
        //   이어져 판정 구간을 통째로 건너뛰게 됩니다.
        player.animator.CrossFadeInFixedTime(attackAnimHash, TransitionDuration, 0, 0f);

        // 방향키를 누르고 공격했다면 그 방향으로 즉시 회전
        Vector3 attackDir = CalculateMoveDirection();
        if (attackDir != Vector3.zero)
        {
            player.transform.rotation = Quaternion.LookRotation(attackDir);
        }

        // 공격 중에는 미끄러지지 않도록 제자리에 멈춥니다.
        player.MoveCharacter(Vector3.zero);

        // 지연이 0이면 여기서 바로 엽니다. Update를 기다리면 최소 1프레임이 늦습니다.
        UpdateHitbox();
    }

    public override void Update()
    {
        UpdateHitbox();

        // 상태 종료 판정만 애니메이션 진행률을 씁니다.
        if (TryGetAttackProgress(out float progress) && progress >= EndNormalized)
        {
            if (player.moveInput == Vector2.zero)
                stateMachine.ChangeState<IdleState_Player>();
            else
                stateMachine.ChangeState<WalkState_Player>();
        }
    }

    /// <summary>
    /// 히트박스를 매 프레임 재평가합니다. 한 프레임을 놓쳐도 다음 프레임이 스스로 바로잡습니다.
    ///
    /// ★ 여는 기준은 '애니메이션 진행률'이 아니라 'Enter로부터의 실제 경과 시간'입니다.
    ///   Enter는 입력 콜백에서 돌고 애니메이터 평가는 모든 Update가 끝난 뒤라, 진행률을 기준으로 하면
    ///   값을 0으로 둬도 최소 1프레임이 늦습니다. 시간 기준이면 지연 0에서 정말 즉시 열립니다.
    ///   닫는 기준은 모션과 맞물려야 자연스러워 진행률을 그대로 씁니다.
    /// </summary>
    private void UpdateHitbox()
    {
        bool opened = Time.time - enterTime >= player.hitboxOpenDelay;

        // 진행률을 아직 못 읽는 동안(전이 중)에는 닫지 않습니다. 열자마자 닫히는 것을 막습니다.
        bool closed = TryGetAttackProgress(out float progress) && progress >= player.hitboxCloseAt;

        SetHitboxActive(opened && !closed);
    }

    /// <summary>전이가 끝나 공격 상태로 확정됐을 때만 진행률을 돌려줍니다.</summary>
    private bool TryGetAttackProgress(out float progress)
    {
        Animator animator = player.animator;
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);

        if (info.shortNameHash == attackAnimHash && !animator.IsInTransition(0))
        {
            progress = info.normalizedTime;
            return true;
        }

        progress = 0f;
        return false;
    }

    /// <summary>
    /// 피격·넉백·사망·씬 이동 등으로 스윙 도중 상태가 끊겨도 콜라이더가 켜진 채 남지 않도록 반드시 닫는다.
    /// (PlayerController.PrepareForSceneArrival의 강제 Idle 전환도 이 경로를 탄다)
    /// </summary>
    public override void Exit()
    {
        SetHitboxActive(false);
    }

    private void SetHitboxActive(bool active)
    {
        WeaponHitbox hitbox = player.WeaponHitbox;
        if (hitbox == null)
        {
            // 무기 프리팹에 히트박스가 없음 — EquipmentVisualizer가 이미 경고를 남긴다
            hitboxActive = false;
            return;
        }

        if (active) hitbox.Begin(player.stat.FinalAttackDamage, player.enemyLayer);
        else if (hitboxActive) hitbox.End();   // 켜져 있던 것만 닫는다

        hitboxActive = active;
    }

    // 공격 도중 다른 입력 막기
    public override void OnAttackInput() { /* MVP에서는 콤보 선입력 무시 */ }
    public override void OnInteractInput() { }
    public override void OnJumpInput() { }
}
