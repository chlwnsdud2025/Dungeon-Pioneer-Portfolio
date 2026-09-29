using UnityEngine;

public class PushState_Player : PlayerBaseState
{
    private readonly int pushMoveHash = Animator.StringToHash("Push_Move");

    private Transform targetObject;
    private PushableObject targetPushable;
    private Vector3 objectStartPos, objectTargetPos;
    private Vector3 snappedPushDirection;

    private float pushDuration = 0.5f;

    public PushState_Player(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

    public override void Enter()
    {
        Debug.Log("PushState - 플레이어는 고정, 상자만 이동");
        targetObject = player.focusedObject;

        if (targetObject == null)
        {
            if (!TryResumeAirborne()) stateMachine.ChangeState<IdleState_Player>();
            return;
        }

        targetPushable = targetObject.GetComponent<PushableObject>();

        snappedPushDirection = GetSnappedDirection(player.transform.forward);

        if (!CanPushObject(snappedPushDirection))
        {
            Debug.Log("<color=red>벽이나 장애물에 막혀 밀 수 없습니다!</color>");
            if (!TryResumeAirborne()) stateMachine.ChangeState<IdleState_Player>();
            return;
        }

        player.transform.rotation = Quaternion.LookRotation(snappedPushDirection);
        player.animator.CrossFadeInFixedTime(pushMoveHash, 0.1f);

        objectStartPos = targetObject.position;
        objectTargetPos = objectStartPos + (snappedPushDirection * player.tileSize);

        // ★ 실제 이동(수평 밀기 + 중력 합산)은 PushableObject가 전담합니다.
        //   여기서는 "이 위치까지, 이 시간 동안 밀어줘"라고 요청만 합니다.
        if (targetPushable != null)
        {
            targetPushable.BeginPush(objectTargetPos, pushDuration);
        }
    }

    public override void Update()
    {
        // 점프 중에 밀었으면 중력과 점프 관성을 그대로 받으며 떨어집니다. (땅에서는 airMomentum이 0이라 제자리)
        player.MoveCharacter(player.airMomentum * Time.deltaTime);

        // ★ 더 이상 여기서 Lerp/MovePosition을 직접 하지 않고, PushableObject에게 완료 여부만 물어봅니다.
        //   (Rigidbody.MovePosition을 두 스크립트가 각자 호출하면 서로의 이동을 덮어써버리기 때문에,
        //    이동의 소유권을 PushableObject 한 곳으로 합쳤습니다.)
        if (targetPushable == null || targetPushable.IsPushComplete)
        {
            if (!TryResumeAirborne()) stateMachine.ChangeState<IdleState_Player>();
        }
    }

    public override void Exit()
    {
        // IsBeingPushed(깔기 판정 on/off)는 PushableObject가 자기 밀기 상태(isPushing)로 스스로 관리하므로
        // 여기서 따로 꺼줄 것이 없습니다.
    }

    // 목표 위치에 벽이나 장애물이 없는지 검사
    private bool CanPushObject(Vector3 pushDirection)
    {
        Vector3 targetPosition = targetObject.position + (pushDirection * player.tileSize);
        targetPosition.y += 0.5f; // 바닥에 붙어있을 경우를 대비해 검사 위치를 약간 위로 올림

        // 타일 사이즈보다 약간 작은 박스로 겹침 검사
        Vector3 halfExtents = new Vector3(player.tileSize * 0.45f, 0.45f, player.tileSize * 0.45f);

        // ※ 주의: Inspector에서 interactableLayer 외에 '벽(Wall)'을 뜻하는 레이어도 포함해야 막힙니다.
        bool isBlocked = Physics.CheckBox(targetPosition, halfExtents, Quaternion.identity, player.interactableLayer);

        return !isBlocked;
    }

    // 바라보는 방향을 상하좌우(X, Z축) 4방향으로 딱 맞게 꺾어주는 수학 함수
    private Vector3 GetSnappedDirection(Vector3 rawDirection)
    {
        if (Mathf.Abs(rawDirection.x) > Mathf.Abs(rawDirection.z))
        {
            return rawDirection.x > 0 ? Vector3.right : Vector3.left;
        }
        else
        {
            return rawDirection.z > 0 ? Vector3.forward : Vector3.back;
        }
    }

    // 밀기 상태 진행 중에는 다른 액션 차단
    public override void OnInteractInput() { }
    public override void OnJumpInput() { }
    public override void OnAttackInput() { }
}