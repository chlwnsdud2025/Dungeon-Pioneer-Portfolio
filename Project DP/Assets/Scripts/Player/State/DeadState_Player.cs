using UnityEngine;

public class DeadState_Player : PlayerBaseState
{
    private readonly int dieAnimHash = Animator.StringToHash("Die");

    public DeadState_Player(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

    public override void Enter()
    {
        // 사망 애니메이션 재생
        player.animator.CrossFadeInFixedTime(dieAnimHash, 0.1f);

        // 플레이어의 CharacterController를 꺼서 더 이상 물리 충돌이나 이동이 일어나지 않게 합니다.
        if (player.characterController != null)
        {
            player.characterController.enabled = false;
        }

        Debug.Log("<color=red>게임 오버! 플레이어가 사망했습니다.</color>");

        // TODO: 게임 오버 UI 호출, 씬 재시작 등 게임 오버 로직을 이곳에 추가합니다.
    }

    public override void Update()
    {
        // 죽은 상태에서는 업데이트 로직이 필요 없습니다. (CharacterController도 꺼졌으므로 MoveCharacter도 호출 안 함)
    }

    // ★ 사망 상태에서는 모든 조작 입력을 완벽하게 차단합니다.
    public override void OnAttackInput() { }
    public override void OnJumpInput() { }
    public override void OnInteractInput() { }
    public override void OnGuardInput() { }
}