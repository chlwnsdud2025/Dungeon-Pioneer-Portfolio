using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    // [핵심] 상태 머신
    public PlayerStateMachine stateMachine { get; private set; }

    [Header("Grid System")]
    // ★ 변경: 더 이상 이 스크립트가 tileSize 값을 직접 들고 있지 않고,
    //   프로젝트 공통 GridSystem(Assets/Resources/GridSystem.asset)에서 읽어옵니다.
    //   (기존엔 기본값이 1이라 던전의 실제 타일 크기 3과 어긋나 있었습니다.
    //    Push/Jump 이동 거리, GetCurrentGridPosition() 판정이 전부 이제 정확히 동기화됩니다)
    public float tileSize => GridSystem.Instance.TileSize;

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
    public int maxJumps = 1; // 추후 유틸리티 스킬 해금 시 값 상승
    [HideInInspector] public int currentJumps = 0;

    [Header("Interaction System")]
    public float interactRange = 1.5f;     // 상호작용 탐색 길이
    public float sphereCastRadius = 0.5f;  // 탐색 구체의 반지름 (두께)
    public LayerMask interactableLayer;

    [Header("Combat System")]
    public float attackRange = 1.2f;    // 무기 휘두르는 범위 반경 (추후 무기 Collider로 교체 예정)
    public LayerMask enemyLayer;        // 몬스터를 판별할 레이어

    [Header("Light & Torch System")]
    public GameObject torchPrefab;   // 벽에 붙일 횃불 프리팹
    public int torchCount = 5;       // 가지고 있는 횃불 개수
    public bool enableMultiTorch = false;

    [HideInInspector] public Vector3 focusedHitPoint;  // 레이저가 닿은 정확한 점
    [HideInInspector] public Vector3 focusedHitNormal; // 벽이 바라보는 방향

    [HideInInspector] public Transform focusedObject;

    [HideInInspector] public bool isRunning = false;
    [HideInInspector] public Vector2 moveInput;

    [Header("Stats")]
    public PlayerStat stat;

    [HideInInspector] public bool isGuarding = false; // 현재 방어 상태인지 판별

    [Header("Knockback System")]
    [Tooltip("넉백으로 밀려날 총 거리(월드 단위). 이 거리만큼 감속하며 밀려납니다.")]
    public float knockbackDistance = 2f;
    [Tooltip("넘어져 있는 시간(초). 이 시간 동안 무적이며, 넘어지는 애니메이션 길이에 맞추세요.")]
    public float knockbackTime = 0.6f;

    [HideInInspector] public bool isInvincible = false;   // 무적(피해 무시) 상태 - 넉백 중 등
    [HideInInspector] public Vector3 knockbackDirection;  // 이번 넉백에서 밀려날 방향(수평)

    [Header("Components")]
    public Animator animator;
    public CharacterController characterController;

    [Header("Camera")]
    public Transform cameraTransform;

    private PlayerInputActions controls;

    private void Awake()
    {
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
        stateMachine.AddState(new OpenChestState_Player(this, stateMachine));
        stateMachine.AddState(new AttackState_Player(this, stateMachine));
        stateMachine.AddState(new PlaceTorchState_Player(this, stateMachine));
        stateMachine.AddState(new HitState_Player(this, stateMachine));
        stateMachine.AddState(new KnockbackState_Player(this, stateMachine));
        stateMachine.AddState(new DeadState_Player(this, stateMachine));


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

        Shader.SetGlobalVector("_PlayerPos", transform.position);
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
                currentJumps = 0; // "땅에 붙어있음"이 아니라 "떨어지다가 착지함"일 때만 리셋
            }
        }
        else
        {
            verticalVelocity += gravity * gravityMultiplier * Time.deltaTime;
        }

        motion.y = verticalVelocity * Time.deltaTime;
        characterController.Move(motion);
    }

    public void TakeDamage(float damage, Transform attacker)
    {
        if (stateMachine.CurrentState is DeadState_Player) return;
        if (isInvincible) return; // 무적(넉백 i-frame 등) 중에는 모든 피해 무시

        // 1. 방어 상태인지 먼저 확인
        if (isGuarding && attacker != null)
        {
            Vector3 dirToAttacker = (attacker.position - transform.position).normalized;
            dirToAttacker.y = 0f;
            float angle = Vector3.Angle(transform.forward, dirToAttacker);

            if (angle <= stat.FinalBlockAngle / 2f)
            {
                Debug.Log("<color=cyan>방어 성공! 챙-!</color>");
                if (stateMachine.CurrentState is GuardState_Player guardState)
                    guardState.OnBlockSuccess(attacker.position);
                return;
            }
        }

        // 방어 중이 아니거나 뒤통수를 맞았다면 데미지 적용 (방어력 차감은 PlayerStat 내부에서 처리)
        stat.ApplyDamage(damage);
        Debug.Log($"<color=red>피격! 남은 체력: {stat.currentHealth}</color>");

        if (stat.IsDead)
        {
            Debug.Log("플레이어 사망");
            stateMachine.ChangeState<DeadState_Player>();
            if (DungeonManager.Instance != null)
                DungeonManager.Instance.ReportPlayerDeath();
        }
        else
        {
            stateMachine.ChangeState<HitState_Player>();
        }
    }

    /// <summary>
    /// RollingRock 등에 부딪혔을 때 호출. 피해를 주고, 죽지 않았다면 넉백(넘어짐) 상태로 전환합니다.
    /// knockDir(수평) 방향으로 살짝 밀리며, 넉백 상태 동안은 무적이라 추가 피해를 받지 않습니다.
    /// </summary>
    public void ApplyKnockback(float damage, Vector3 knockDir)
    {
        if (stateMachine.CurrentState is DeadState_Player) return;
        if (isInvincible) return; // 이미 넉백/무적 중이면 무시 (RollingRock 재충돌 무시 효과)

        stat.ApplyRawDamage(damage);  // 넉백은 방어력 무시
        Debug.Log($"<color=orange>넉백! 남은 체력: {stat.currentHealth}</color>");

        if (stat.IsDead)
        {
            stateMachine.ChangeState<DeadState_Player>();
            if (DungeonManager.Instance != null) DungeonManager.Instance.ReportPlayerDeath();
            return;
        }

        Vector3 dir = knockDir;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = -transform.forward;
        knockbackDirection = dir.normalized;

        stateMachine.ChangeState<KnockbackState_Player>();
    }

    // 현재 플레이어가 밟고 있는 타일의 좌표를 반환하는 함수
    public Vector2Int GetCurrentGridPosition()
    {
        // ★ 변경: GridSystem 기준으로 통일 (기존엔 로컬 tileSize 필드를 써서 던전 생성 좌표계와 어긋날 수 있었음)
        Vector3Int cell = GridSystem.Instance.WorldToCell(transform.position);
        return new Vector2Int(cell.x, cell.z);
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