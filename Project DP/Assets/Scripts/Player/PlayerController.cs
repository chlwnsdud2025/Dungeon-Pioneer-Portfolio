using NUnit.Framework.Interfaces;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    // [핵심] 상태 머신
    public PlayerStateMachine stateMachine { get; private set; }

    [Header("Grid System")]
    public float tileSize = 1f;

    [Header("Movement System")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;

    [Header("Input System (Double Tap)")]
    public float doubleTapThreshold = 0.5f;
    private float lastMovePressTime = 0f;
    private bool isMoveKeyPressed = false;

    [Header("Jump & Gravity Settings")]
    public float jumpForce = 5f;        // 점프 뛰는 힘 (높이)
    public float gravity = -9.81f;      // 기본 중력
    public float gravityMultiplier = 2f;// 중력 가속도 배수
    [HideInInspector] public float verticalVelocity;

    [Header("Interaction System")]
    public float interactRange = 1.5f;     // 상호작용 탐색 길이
    public float sphereCastRadius = 0.5f;  // 탐색 구체의 반지름 (두께)
    public LayerMask interactableLayer;

    [Header("Combat System")]
    public float attackRange = 1.2f;    // 무기 휘두르는 범위 반경
    public float attackDamage = 5f;     // 공격력
    public LayerMask enemyLayer;        // 몬스터를 판별할 레이어

    [Header("Light & Torch System")]
    public GameObject torchPrefab;   // 벽에 붙일 횃불 프리팹
    public int torchCount = 5;       // 가지고 있는 횃불 개수

    [HideInInspector] public Vector3 focusedHitPoint;  // 레이저가 닿은 정확한 점
    [HideInInspector] public Vector3 focusedHitNormal; // 벽이 바라보는 방향

    [HideInInspector] public Transform focusedObject;

    [HideInInspector] public bool isRunning = false;
    [HideInInspector] public Vector2 moveInput;

    [Header("Defense System")]
    public float maxHealth = 100f;
    public float currentHealth;
    [Range(0, 180)] public float blockAngle = 120f; // 정면 기준 방어 가능 각도 (보통 90~120도)

    [HideInInspector] public bool isGuarding = false; // 현재 방어 상태인지 판별

    [Header("Components")]
    public Animator animator;
    public CharacterController characterController;

    [Header("Camera")]
    public Transform cameraTransform;

    private PlayerInputActions controls;

    private void Awake()
    {
        currentHealth = maxHealth;
        controls = new PlayerInputActions();

        stateMachine = new PlayerStateMachine();
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        stateMachine.AddState(new IdleState_Player(this, stateMachine));
        stateMachine.AddState(new WalkState_Player(this, stateMachine));
        stateMachine.AddState(new RunState_Player(this, stateMachine));
        stateMachine.AddState(new JumpState_Player(this, stateMachine));
        stateMachine.AddState(new PushState_Player(this, stateMachine));
        stateMachine.AddState(new GuardState_Player(this, stateMachine));

        // 상자 열기와 공격 State 추가
        stateMachine.AddState(new OpenChestState_Player(this, stateMachine));
        stateMachine.AddState(new AttackState_Player(this, stateMachine));
        stateMachine.AddState(new PlaceTorchState_Player(this, stateMachine));


        // 입력 이벤트 구독
        controls.PlayerControll.Jump.performed += ctx => stateMachine.CurrentState.OnJumpInput();
        controls.PlayerControll.Interact.performed += ctx => stateMachine.CurrentState.OnInteractInput();

        // 공격 입력 구독 
        controls.PlayerControll.Attack.performed += ctx => stateMachine.CurrentState.OnAttackInput();

        // 방어 입력 구독
        controls.PlayerControll.Guard.started += ctx => stateMachine.CurrentState.OnGuardInput();
        controls.PlayerControll.Guard.canceled += ctx => stateMachine.CurrentState.OnGuardReleased();
    }

    private void Start()
    {
        stateMachine.Initialize<IdleState_Player>();
    }

    private void Update()
    {
        HandleMovementInput();
        UpdateFocus(); // 매 프레임 내가 무엇을 쳐다보는지 업데이트
        stateMachine.Update();
    }

    private void FixedUpdate()
    {
        stateMachine.FixedUpdate();
    }

    private void HandleMovementInput()
    {
        Vector2 currentMoveInput = controls.PlayerControll.Movement.ReadValue<Vector2>();

        if (currentMoveInput != Vector2.zero && !isMoveKeyPressed)
        {
            isMoveKeyPressed = true;

            if (Time.time - lastMovePressTime <= doubleTapThreshold)
            {
                isRunning = true;
            }
            else
            {
                isRunning = false;
            }
            lastMovePressTime = Time.time;
        }
        else if (currentMoveInput == Vector2.zero && isMoveKeyPressed)
        {
            isMoveKeyPressed = false;
            isRunning = false;
        }

        moveInput = currentMoveInput;
    }

    public void MoveCharacter(Vector3 motion)
    {
        if (characterController.isGrounded)
        {
            if (verticalVelocity < 0.0f)
            {
                verticalVelocity = -2f;
            }
        }
        else
        {
            verticalVelocity += gravity * gravityMultiplier * Time.deltaTime;
        }

        motion.y = verticalVelocity * Time.deltaTime;
        characterController.Move(motion);
    }

    // ★ 적의 공격을 받았을 때 호출될 피격 함수 (방어 각도 계산 포함)
    public void TakeDamage(float damage, Transform attacker)
    {
        // 1. 방어 상태인지 먼저 확인
        if (isGuarding && attacker != null)
        {
            // 2. 각도 계산 (내 정면과 적의 위치 사이의 수평 각도)
            Vector3 dirToAttacker = (attacker.position - transform.position).normalized;
            dirToAttacker.y = 0f;

            float angle = Vector3.Angle(transform.forward, dirToAttacker);

            // 3. 방어 가능 각도 이내라면 방어 성공!
            if (angle <= blockAngle / 2f)
            {
                Debug.Log("<color=cyan>방어 성공! 챙-!</color>");

                // 현재 상태가 방어(GuardState)라면, 밀림(Impact) 애니메이션과 로직을 실행하도록 호출
                if (stateMachine.CurrentState is GuardState_Player guardState)
                {
                    guardState.OnBlockSuccess(attacker.position);
                }
                return; // 방어했으므로 데미지는 입지 않고 종료
            }
        }

        // 방어 중이 아니거나 뒤통수를 맞았다면 데미지 적용
        currentHealth -= damage;
        Debug.Log($"<color=red>피격! 남은 체력: {currentHealth}</color>");

        // 체력이 0 이하면 사망 처리
        if (currentHealth <= 0)
        {
            Debug.Log("플레이어 사망 (Dead State 전환 필요)");
            // stateMachine.ChangeState<DeadState_Player>();
        }
        else
        {
            // stateMachine.ChangeState<HitState_Player>();
        }
    }

    // 현재 플레이어가 밟고 있는 타일의 좌표를 반환하는 함수
    public Vector2Int GetCurrentGridPosition()
    {
        int x = Mathf.RoundToInt(transform.position.x / tileSize);
        int z = Mathf.RoundToInt(transform.position.z / tileSize);
        return new Vector2Int(x, z);
    }

    private void UpdateFocus()
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;

        if (Physics.SphereCast(rayStart, sphereCastRadius, transform.forward, out RaycastHit hit, interactRange, interactableLayer))
        {
            // ★ 수정됨: WallObject 감지 추가
            if (hit.collider.GetComponent<PushableObject>() != null ||
                hit.collider.GetComponent<TreasureChest>() != null ||
                hit.collider.GetComponent<WallObject>() != null)
            {
                focusedObject = hit.transform;

                // 감지된 곳의 정확한 충돌 지점과 벽의 표면 방향을 저장합니다.
                focusedHitPoint = hit.point;
                focusedHitNormal = hit.normal;
            }
            else
            {
                focusedObject = null;
            }
        }
        else
        {
            focusedObject = null;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;
        Gizmos.DrawWireSphere(rayStart + transform.forward * interactRange, sphereCastRadius);
        Gizmos.DrawRay(rayStart, transform.forward * interactRange);
    }

    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying) return;

        Gizmos.color = Color.red;
        Vector3 hitCenter = transform.position + transform.forward * 1f + Vector3.up * 0.5f;
        Gizmos.DrawWireSphere(hitCenter, attackRange);
    }

    private void OnEnable() { controls.Enable(); }
    private void OnDisable() { controls.Disable(); }
}