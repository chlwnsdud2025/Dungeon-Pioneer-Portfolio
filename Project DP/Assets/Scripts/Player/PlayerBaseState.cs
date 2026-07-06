using UnityEngine;

public abstract class PlayerBaseState
{
    protected PlayerController player;
    protected PlayerStateMachine stateMachine;

    public PlayerBaseState(PlayerController player, PlayerStateMachine stateMachine)
    {
        this.player = player;
        this.stateMachine = stateMachine;
    }

    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void FixedUpdate() { }
    public virtual void Exit() { }

    public virtual void OnAttackInput()
    {
        stateMachine.ChangeState<AttackState_Player>();
    }

    public virtual void OnGuardInput() { stateMachine.ChangeState<GuardState_Player>(); }
    public virtual void OnGuardReleased() { }

    public virtual void OnJumpInput() {

        if (player.currentJumps < player.maxJumps)
        {
            stateMachine.ChangeState<JumpState_Player>();
        }

    }
    public virtual void OnInteractInput()
    {
        if (player.focusedObject != null)
        {
            // 1. 보물상자인지 확인
            TreasureChest chest = player.focusedObject.GetComponent<TreasureChest>();
            if (chest != null && !chest.isOpen)
            {
                stateMachine.ChangeState<OpenChestState_Player>();
                return;
            }

            // 2. 밀 수 있는 물체인지 확인
            PushableObject pushObj = player.focusedObject.GetComponent<PushableObject>();
            if (pushObj != null)
            {
                stateMachine.ChangeState<PushState_Player>();
                return;
            }

            // ★ 3. 벽인지 확인 (그리고 횃불이 남아있는지)
            WallObject wall = player.focusedObject.GetComponent<WallObject>();
            if (wall != null && player.torchCount > 0)
            {
                stateMachine.ChangeState<PlaceTorchState_Player>();
                return;
            }
            else if (wall != null && player.torchCount <= 0)
            {
                Debug.Log("<color=red>횃불이 부족합니다!</color>");
            }
        }
    }

    // [추가] 이동 방향으로 캐릭터를 부드럽게 회전시키는 공통 메서드
    protected virtual void HandleRotation(Vector3 moveDir)
    {
        if (moveDir != Vector3.zero)
        {
            // 목표 회전값 계산
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);

            // Slerp를 사용하여 현재 회전에서 목표 회전으로 부드럽게 보간 (15f는 회전 속도)
            player.transform.rotation = Quaternion.Slerp(player.transform.rotation, targetRotation, 15f * Time.deltaTime);
        }
    }

    protected virtual Vector3 CalculateMoveDirection()
    {
        // 씬의 메인 카메라를 기준으로 방향을 계산합니다.
        Transform camTransform = Camera.main.transform;

        if (camTransform == null)
            return new Vector3(player.moveInput.x, 0, player.moveInput.y).normalized;

        Vector3 camForward = camTransform.forward;
        Vector3 camRight = camTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        return (camRight * player.moveInput.x + camForward * player.moveInput.y).normalized;
    }
}