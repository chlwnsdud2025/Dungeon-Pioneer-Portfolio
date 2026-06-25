using UnityEngine;

public class PushState_Player : PlayerBaseState
{
    private readonly int pushMoveHash = Animator.StringToHash("Push_Move");

    private Transform targetObject;
    private Vector3 objectStartPos, objectTargetPos;
    private Vector3 snappedPushDirection;

    private float pushDuration = 0.5f; // 상자가 밀리는 데 걸리는 시간
    private float moveTimer = 0f;

    public PushState_Player(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

    public override void Enter()
    {
        Debug.Log("PushState - 플레이어는 고정, 상자만 이동");
        targetObject = player.focusedObject;

        if (targetObject == null)
        {
            stateMachine.ChangeState<IdleState_Player>();
            return;
        }

        // 1. 캐릭터가 바라보는 방향을 완벽한 상/하/좌/우(X, Z축)로 보정
        snappedPushDirection = GetSnappedDirection(player.transform.forward);

        // 2. 보정된 방향을 매개변수로 넣어 목표 타일이 비었는지 검사
        if (!CanPushObject(snappedPushDirection))
        {
            Debug.Log("<color=red>벽이나 장애물에 막혀 밀 수 없습니다!</color>");
            stateMachine.ChangeState<IdleState_Player>();
            return;
        }

        moveTimer = 0f;

        // 3. 플레이어는 위치 이동 없이 제자리에서 방향만 맞추고 애니메이션 재생
        player.transform.rotation = Quaternion.LookRotation(snappedPushDirection);
        player.animator.CrossFadeInFixedTime(pushMoveHash, 0.1f);

        // 4. 상자(밀리는 오브젝트)의 시작/목표 좌표만 계산
        objectStartPos = targetObject.position;
        objectTargetPos = objectStartPos + (snappedPushDirection * player.tileSize);
    }

    public override void Update()
    {
        // 미는 동안 플레이어가 다른 곳으로 움직이지 않도록 제자리에 묶어둠 (중력은 유지)
        player.MoveCharacter(Vector3.zero);

        moveTimer += Time.deltaTime;
        float normalizedTime = moveTimer / pushDuration;

        // ★ 플레이어 이동 코드를 삭제하고 상자만 보간 이동(Lerp) 처리
        targetObject.position = Vector3.Lerp(objectStartPos, objectTargetPos, normalizedTime);

        if (normalizedTime >= 1f)
        {
            // 상자 도착 후 오차 없는 완벽한 좌표 스냅
            targetObject.position = objectTargetPos;

            // 밀기 완료 후 다시 대기 상태로
            stateMachine.ChangeState<IdleState_Player>();
        }
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