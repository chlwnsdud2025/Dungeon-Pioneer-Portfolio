using UnityEngine;

public class OpenChestState_Player : PlayerBaseState
{
    // 캐릭터의 상자 열기 애니메이션 (Animator에 해당 모션이 있어야 합니다!)
    private readonly int interactAnimHash = Animator.StringToHash("Interact_Chest");

    private float timer = 0f;
    private float openDuration = 1.0f; // 상자를 여는 데 걸리는 시간(애니메이션 길이와 맞춤)
    private TreasureChest targetChest;

    public OpenChestState_Player(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

    public override void Enter()
    {
        // 1. 대상 상자 정보 가져오기
        targetChest = player.focusedObject.GetComponent<TreasureChest>();
        timer = 0f;

        // 2. 상자 쪽으로 캐릭터 회전 확정 (정확히 마주보게 함)
        Vector3 dirToChest = (player.focusedObject.position - player.transform.position).normalized;
        dirToChest.y = 0f;
        if (dirToChest != Vector3.zero)
        {
            player.transform.rotation = Quaternion.LookRotation(dirToChest);
        }

        // 3. 상자 여는 애니메이션 재생
        player.animator.CrossFadeInFixedTime(interactAnimHash, 0.1f);

        // 4. 상자 쪽에 열렸다는 신호 보내기
        targetChest.Open();

        // 상호작용 중 이동 멈춤
        player.MoveCharacter(Vector3.zero);
    }

    public override void Update()
    {
        timer += Time.deltaTime;

        // 지정된 시간이 지나면(상자 여는 모션이 끝나면) 대기 상태로 복귀
        if (timer >= openDuration)
        {
            stateMachine.ChangeState<IdleState_Player>();
        }
    }

    // 상자를 여는 도중에는 이동, 공격, 점프, 추가 상호작용 등 모든 조작을 막습니다.
    public override void OnInteractInput() { }
    public override void OnJumpInput() { }
    public override void OnAttackInput() { }
}