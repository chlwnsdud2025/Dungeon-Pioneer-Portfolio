using UnityEngine;

public class PushState_Player : PlayerBaseState
{
    private readonly int pushReadyHash = Animator.StringToHash("Push_Idle");
    private readonly int pushMoveHash = Animator.StringToHash("Push_Move");

    private enum PushPhase { Ready, Pushing }
    private PushPhase currentPhase;

    private Transform targetObject;
    private Vector3 playerStartPos, playerTargetPos;
    private Vector3 objectStartPos, objectTargetPos;

    // 밀기 방향 (그리드에 딱 맞춘 상하좌우)
    private Vector3 snappedPushDirection;

    private float holdTimer = 0f;
    private float requiredHoldTime = 0.3f;
    private float pushDuration = 0.5f;
    private float moveTimer = 0f;

    public PushState_Player(PlayerController player, PlayerStateMachine stateMachine) : base(player, stateMachine) { }

    public override void Enter()
    {
        Debug.Log("PushState");
        targetObject = player.focusedObject;

        if (targetObject == null)
        {
            stateMachine.ChangeState<IdleState_Player>();
            return;
        }

        // 1. 캐릭터가 바라보는 방향을 완벽한 상/하/좌/우(X, Z축)로 보정합니다.
        snappedPushDirection = GetSnappedDirection(player.transform.forward);

        // 2. 에러 수정: 보정된 방향을 매개변수로 넣어 목표 타일이 비었는지 검사합니다.
        if (!CanPushObject(snappedPushDirection))
        {
            stateMachine.ChangeState<IdleState_Player>();
            return;
        }

        currentPhase = PushPhase.Ready;
        holdTimer = 0f;

        // 3. 밀기 준비 애니메이션 재생 및 밀기 방향으로 캐릭터 회전 확정
        player.animator.CrossFadeInFixedTime(pushMoveHash, 0.1f);
        player.transform.rotation = Quaternion.LookRotation(snappedPushDirection);
    }

    public override void Update()
    {
        if (currentPhase == PushPhase.Ready)
        {
            if (player.moveInput == Vector2.zero)
            {
                stateMachine.ChangeState<IdleState_Player>();
                return;
            }

            holdTimer += Time.deltaTime;

            if (holdTimer >= requiredHoldTime)
            {
                StartActualPush();
            }
        }
        else if (currentPhase == PushPhase.Pushing)
        {
            moveTimer += Time.deltaTime;
            float normalizedTime = moveTimer / pushDuration;

            // 보간 이동
            player.transform.position = Vector3.Lerp(playerStartPos, playerTargetPos, normalizedTime);
            targetObject.position = Vector3.Lerp(objectStartPos, objectTargetPos, normalizedTime);

            if (normalizedTime >= 1f)
            {
                // 도착 후 오차 없는 완벽한 좌표 스냅
                player.transform.position = playerTargetPos;
                targetObject.position = objectTargetPos;

                stateMachine.ChangeState<IdleState_Player>();
            }
        }
    }

    public override void Exit()
    {
        // Lerp 이동이 끝난 후 다시 CharacterController를 켜줍니다.
        player.characterController.enabled = true;
    }

    private void StartActualPush()
    {
        currentPhase = PushPhase.Pushing;
        moveTimer = 0f;

        // ★ 핵심: 강제 Transform 이동을 위해 CharacterController를 잠시 끕니다.
        player.characterController.enabled = false;

        player.animator.CrossFadeInFixedTime(pushMoveHash, 0.1f);

        playerStartPos = player.transform.position;
        playerTargetPos = playerStartPos + (snappedPushDirection * player.tileSize);

        objectStartPos = targetObject.position;
        objectTargetPos = objectStartPos + (snappedPushDirection * player.tileSize);
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

    public override void OnInteractInput()
    {
        // 밀기 상태 진행 중에는 상호작용 키(F)를 눌러도 무시하도록 덮어씌움
    }
}