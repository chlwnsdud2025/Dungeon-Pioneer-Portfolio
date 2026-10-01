using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour, ITarget, IFactionMember
{
    // [핵심] 상태 머신
    public PlayerStateMachine stateMachine { get; private set; }
    public StairTransitionState_Player StairTransitionState { get; private set; }
    private bool transitionInputLocked;

    public void SetTransitionInputLocked(bool locked) => transitionInputLocked = locked;

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
    [HideInInspector] public Vector3 airMomentum;
    [HideInInspector] public bool resumeAirborne;

    [Header("Interaction System")]
    public float interactRange = 1.5f;     // 상호작용 탐색 길이
    public float sphereCastRadius = 0.5f;  // 탐색 구체의 반지름 (두께)
    public LayerMask interactableLayer;

    [Header("Combat System")]
    public LayerMask enemyLayer;        // 몬스터를 판별할 레이어 (무기 히트박스에 넘겨줌)
    [Tooltip("공격 입력 후 타격 판정을 열기까지의 지연(초). 0이면 공격과 동시에 즉시 열립니다.\n" +
             "애니메이션 진행률이 아니라 실제 시간 기준입니다 — 진행률은 애니메이터가 갱신되는 " +
             "다음 프레임까지 읽을 수 없어 아무리 0으로 둬도 최소 1프레임이 늦습니다.")]
    [Range(0f, 0.5f)] public float hitboxOpenDelay = 0f;
    [Tooltip("타격 판정을 닫는 시점 (애니메이션 진행률 0~1). 닫기는 모션과 맞물려야 자연스러워 " +
             "진행률 기준을 유지합니다.")]
    [Range(0f, 1f)] public float hitboxCloseAt = 0.8f;

    [Header("Light & Torch System")]
    public GameObject torchPrefab;   // 벽에 붙일 횃불 프리팹(3D 오브젝트)
    [Tooltip("설치할 때 인벤토리에서 소모할 횃불 아이템. 개수는 인벤토리 재고를 그대로 씁니다.")]
    public ConsumableItemData torchItem;
    public bool enableMultiTorch = false;

    [HideInInspector] public Vector3 focusedHitPoint;  // 레이저가 닿은 정확한 점
    [HideInInspector] public Vector3 focusedHitNormal; // 벽이 바라보는 방향

    /// <summary>지금 바라보는 상호작용 대상. 없으면 null. (인터페이스라 인스펙터에는 안 뜹니다)</summary>
    public IInteractable focusedInteractable;
    // focusedInteractable.transform과 항상 같은 값. 기존 상태(OpenChest/Push/PlaceTorch)들이
    // Transform으로 대상을 다시 찾는 구조라 호환용으로 함께 유지합니다.
    [HideInInspector] public Transform focusedObject;

    [HideInInspector] public bool isRunning = false;
    [HideInInspector] public Vector2 moveInput;

    [Header("Stats")]
    public PlayerStat stat;
    public PlayerWallet wallet;

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

    // ★ 입력은 InputManager가 소유합니다. 여기서 new PlayerInputActions()를 하면 전역 액션과 별개의
    //   인스턴스가 생겨서, 인벤토리를 열 때 부르는 InputManager.SetGameplayEnabled(false)가 먹지 않고
    //   캐릭터가 계속 움직입니다. 반드시 전역 인스턴스의 액션을 빌려 씁니다.
    private bool inputSubscribed;
    private EquipmentVisualizer equipmentVisualizer;
    private EquipmentHolder equipmentHolder;

    /// <summary>현재 장착 무기의 타격 판정 콜라이더. 무기가 없거나 히트박스가 없으면 null.</summary>
    public WeaponHitbox WeaponHitbox =>
        equipmentVisualizer != null ? equipmentVisualizer.CurrentWeaponHitbox : null;

    public bool HasWeapon => equipmentHolder != null && equipmentHolder.GetEquipped(EquipmentSlot.Weapon) != null;
    public bool HasShield => equipmentHolder != null && equipmentHolder.GetEquipped(EquipmentSlot.Shield) != null;

    private void Awake()
    {
        stateMachine = new PlayerStateMachine();
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        equipmentVisualizer = GetComponent<EquipmentVisualizer>();
        equipmentHolder = GetComponent<EquipmentHolder>();
        Overrides = GetComponent<FactionOverride>();

        stateMachine.AddState(new IdleState_Player(this, stateMachine));
        stateMachine.AddState(new WalkState_Player(this, stateMachine));
        StairTransitionState = new StairTransitionState_Player(this, stateMachine);
        stateMachine.AddState(StairTransitionState);
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
    }

    private void Start()
    {
        // 생성 직후 같은 프레임에 이미 다른 상태로 들어갔을 수 있습니다. (던전 씬에서 바로 시작하면
        // DungeonManager가 플레이어를 만들자마자 계단 도착 연출 상태로 바꿈) 그 상태를 Idle로 덮어쓰면
        // 연출 상태의 Exit가 불리지 않아 CharacterController가 꺼진 채 남으므로, 상태가 없을 때만 시작합니다.
        if (stateMachine.CurrentState == null) stateMachine.Initialize<IdleState_Player>();
    }

    private void Update()
    {
        HandleMovementInput();
        if (!transitionInputLocked && !(stateMachine.CurrentState is StairTransitionState_Player))
            UpdateFocus(); // 계단 연출 중에는 상호작용 포커스를 갱신하지 않음
        stateMachine.Update();

        Shader.SetGlobalVector("_PlayerPos", transform.position);
    }

    private void FixedUpdate()
    {
        stateMachine.FixedUpdate();
    }

    private void HandleMovementInput()
    {
        if (transitionInputLocked)
        {
            moveInput = Vector2.zero;
            isRunning = false;
            isMoveKeyPressed = false;
            return;
        }

        // 인벤토리를 열면 InputManager가 PlayerControll 맵을 통째로 꺼서 여기서 자동으로 (0,0)이 읽힙니다.
        InputManager input = InputManager.Instance;
        Vector2 currentMoveInput = input != null ? input.Player.Movement.ReadValue<Vector2>() : Vector2.zero;

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
                airMomentum = Vector3.zero;
            }
        }
        else
        {
            verticalVelocity += gravity * gravityMultiplier * Time.deltaTime;
        }

        motion.y = verticalVelocity * Time.deltaTime;
        characterController.Move(motion);
    }

    // ── 적의 추격·공격 대상 (ITarget) ─────────────────────────
    // 적은 플레이어를 이 인터페이스로만 봅니다. 시야선은 눈높이(1.5)를 겨누고, 접근 셀은 지금 서 있는 셀,
    // 움직이는 대상이라 적은 따라가며 경로를 다시 찾고 구덩이도 밟고 지나옵니다.

    public bool IsAlive => stateMachine == null || !(stateMachine.CurrentState is DeadState_Player);
    public Transform Transform => transform;

    // ── 진영 (IFactionMember) ─────────────────────────────────
    // 플레이어는 언제나 Player 진영이라 필드로 두지 않습니다.
    public FactionId Faction => FactionId.Player;
    public FactionOverride Overrides { get; private set; }

    public Vector3 AimPoint => transform.position + Vector3.up * 1.5f;
    public Vector2Int ApproachCell => LocationGrid.WorldToCell(transform.position);
    public bool IsMobile => true;

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

        // 묘비 콜라이더는 trigger라 기본 SphereCast에 안 잡힐 수 있어 Collide를 명시합니다.
        if (Physics.SphereCast(rayStart, sphereCastRadius, transform.forward, out RaycastHit hit,
                interactRange, interactableLayer, QueryTriggerInteraction.Collide))
        {
            // 콜라이더가 자식에 있어도 부모 체인에서 가장 가까운 상호작용 컴포넌트를 포커스로 잡습니다.
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                focusedInteractable = interactable;
                focusedObject = interactable.transform;
                focusedHitPoint = hit.point;
                focusedHitNormal = hit.normal;
                return;
            }
        }

        focusedInteractable = null;
        focusedObject = null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;
        Gizmos.DrawWireSphere(rayStart + transform.forward * interactRange, sphereCastRadius);
        Gizmos.DrawRay(rayStart, transform.forward * interactRange);
    }



    // ── 인벤토리 연동 ───────────────────────────────────────
    // Inventory는 UI 프리팹에 있어서 씬에 배치된 참조로 연결할 수 없습니다.
    // EquipmentHolder/QuickSlotHolder와 마찬가지로 항상 살아 있는 UIInGameHUD가 런타임에 넣어줍니다.

    /// <summary>플레이어가 쓰는 인벤토리. 아직 연결 전이면 null.</summary>
    public Inventory Inventory { get; private set; }

    public void BindInventory(Inventory inventory) => Inventory = inventory;

    /// <summary>인벤토리에 남은 횃불 개수.</summary>
    public int TorchStock => (Inventory != null && torchItem != null) ? Inventory.CountOf(torchItem) : 0;

    /// <summary>
    /// 지금 바라보는 벽면에 횃불을 설치할 수 있는지. (TorchItemData.CanUse가 이 값을 본다)
    /// 다중 설치 모드에서는 이미 붙은 면이어도 주변 벽에 붙일 수 있으므로 통과시킨다.
    /// </summary>
    public bool CanPlaceTorchNow()
    {
        if (focusedObject == null) return false;

        WallObject wall = focusedObject.GetComponentInParent<WallObject>();
        if (wall == null) return false;

        return enableMultiTorch || !wall.HasTorchOnFace(focusedHitNormal);
    }

    /// <summary>
    /// 바라보는 벽에 횃불을 설치한다. 설치와 모션은 PlaceTorchState_Player가, 개수 차감은
    /// Inventory.UseItem이 처리하므로 소모 경로가 한 곳으로 모인다(퀵슬롯으로 써도 같은 길).
    /// </summary>
    public bool TryPlaceTorch()
    {
        if (torchItem == null)
        {
            Debug.LogWarning("[PlayerController] torchItem이 연결되지 않아 횃불을 설치할 수 없습니다.", this);
            return false;
        }
        if (Inventory == null) return false;   // UI 초기화 전 — 곧 HUD가 연결해준다

        if (TorchStock <= 0)
        {
            Debug.Log("<color=red>횃불이 부족합니다!</color>");
            return false;
        }
        if (!CanPlaceTorchNow())
        {
            Debug.Log("<color=yellow>이 벽면에는 이미 횃불이 설치되어 있습니다.</color>");
            return false;
        }

        return Inventory.UseItem(torchItem, this);
    }

    /// <summary>
    /// 씬을 넘어온 직후 PlayerManager가 호출. 이전 씬에서 남은 상태를 정리하고 Idle로 되돌립니다.
    /// Player가 씬마다 새로 생성되던 시절엔 이 초기화가 공짜였지만, 이제는 전역 상주라 명시적으로 해야 합니다.
    /// (체력 복구는 PlayerStat.ResetHealth로, 이 함수보다 '먼저' 끝나 있어야 합니다)
    /// </summary>
    public void PrepareForSceneArrival()
    {
        verticalVelocity = 0f;
        currentJumps = 0;
        airMomentum = Vector3.zero;
        resumeAirborne = false;
        isInvincible = false;
        isGuarding = false;
        isRunning = false;
        isMoveKeyPressed = false;
        moveInput = Vector2.zero;
        focusedInteractable = null;
        focusedObject = null;

        // ★ DeadState_Player.Enter()가 꺼둔 CharacterController를 반드시 되살립니다.
        //   안 그러면 부활해도 조작이 전혀 먹지 않습니다.
        if (characterController != null) characterController.enabled = true;

        stateMachine.ChangeState<IdleState_Player>();
    }

    // ── 입력 구독 ───────────────────────────────────────────────
    // 씬 전환 때 PlayerManager가 SetActive를 토글하므로, 구독은 반드시 OnEnable/OnDisable 쌍으로 둡니다.
    // (Awake에서 구독하면 씬을 넘을 때마다 핸들러가 중복으로 쌓입니다)

    private void OnEnable() { SubscribeInput(); }
    private void OnDisable() { UnsubscribeInput(); }

    private void SubscribeInput()
    {
        if (inputSubscribed) return;

        InputManager input = InputManager.Instance;
        if (input == null)
        {
            Debug.LogError("[PlayerController] InputManager가 없어 입력을 구독하지 못했습니다. " +
                           "씬에 InputManager를 배치해주세요.", this);
            return;
        }

        input.Player.Jump.performed += OnJumpPerformed;
        input.Player.Interact.performed += OnInteractPerformed;
        input.Player.Attack.performed += OnAttackPerformed;
        input.Player.Guard.started += OnGuardStarted;
        input.Player.Guard.canceled += OnGuardCanceled;

        inputSubscribed = true;
    }

    private void UnsubscribeInput()
    {
        if (!inputSubscribed) return;

        InputManager input = InputManager.Instance;
        if (input != null)
        {
            input.Player.Jump.performed -= OnJumpPerformed;
            input.Player.Interact.performed -= OnInteractPerformed;
            input.Player.Attack.performed -= OnAttackPerformed;
            input.Player.Guard.started -= OnGuardStarted;
            input.Player.Guard.canceled -= OnGuardCanceled;
        }

        inputSubscribed = false;
    }

    private void OnJumpPerformed(InputAction.CallbackContext _) { if (!transitionInputLocked) stateMachine.CurrentState?.OnJumpInput(); }
    private void OnInteractPerformed(InputAction.CallbackContext _) { if (!transitionInputLocked) stateMachine.CurrentState?.OnInteractInput(); }
    private void OnAttackPerformed(InputAction.CallbackContext _) { if (!transitionInputLocked) stateMachine.CurrentState?.OnAttackInput(); }
    private void OnGuardStarted(InputAction.CallbackContext _) { if (!transitionInputLocked) stateMachine.CurrentState?.OnGuardInput(); }
    private void OnGuardCanceled(InputAction.CallbackContext _) { if (!transitionInputLocked) stateMachine.CurrentState?.OnGuardReleased(); }
}
