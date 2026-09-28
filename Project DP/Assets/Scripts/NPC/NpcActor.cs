using UnityEngine;

/// <summary>
/// NPC의 '몸'. 브레인이 무엇이든 이 부분은 모든 NPC가 동일하게 씁니다.
///
/// 적(EnemyController)과 같은 인터페이스를 구현하므로 GridMover·Perception·FactionTable을
/// 그대로 재사용합니다 — 이동과 시야 코드를 한 줄도 다시 쓰지 않습니다.
///
/// 그리드는 던전에서는 DungeonGenerator가, 오버월드·마을에서는 SceneLocation이 만듭니다.
/// Grid가 null인 상태도 정상입니다 — 씬에 SceneLocation이 없거나 던전 층 전환 중일 때입니다.
/// GridMover가 모든 경로에서 null을 방어하므로 그때는 그냥 움직이지 않습니다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class NpcActor : MonoBehaviour, IGridAgent, IPerceptionAgent, IFactionMember, ITarget, IInteractable
{
    private const float GravityAcceleration = -9.81f;
    private const float GravityMultiplier = 2f;

    // GridMover는 웨이포인트를 넘기는 프레임에 잠깐 IsMoving이 false가 됩니다.
    // 그때마다 Idle로 넘어가면 걷는 동안 모션이 끊기므로, 이 시간만큼 멈춰 있어야 Idle로 바꿉니다.
    private const float IdleSwitchDelay = 0.15f;
    private const float LocomotionCrossFade = 0.15f;
    private const string IdleStateName = "Idle";
    private const string WalkStateName = "Walk";

    [Header("정의")]
    [Tooltip("스탯·진영·지각 범위. 같은 종류의 NPC는 이 애셋 하나를 공유합니다.")]
    public NpcDefinition definition;

    [Header("Components")]
    public Animator animator;
    public CharacterController characterController;

    [Header("지각")]
    [Tooltip("벽 등 시야를 가리는 장애물 레이어. 적(EnemyController.obstacleLayer)과 같은 값을 넣으면 됩니다.")]
    public LayerMask obstacleLayer;

    [HideInInspector] public Vector3 spawnPosition;
    [HideInInspector] public Vector2Int spawnCell;
    [HideInInspector] public Quaternion spawnRotation;

    private bool spawnCaptured;
    private bool locationReady;
    private float currentHealth;
    private bool downed;
    private float verticalVelocity;
    private NpcBrain brain;
    private NpcInteractionModule[] modules;
    private bool walkAnimPlaying;
    private float stillTime = IdleSwitchDelay;
    private bool warnedMissingAnimState;

    public GridMover Mover { get; private set; }
    public Perception Perception { get; private set; }
    public LocationGrid Grid { get; private set; }
    public FactionOverride Overrides { get; private set; }

    public float CurrentHealth => currentHealth;
    public float MaxHealth => definition != null ? definition.maxHealth : 1f;
    public float MoveSpeed => definition != null ? definition.moveSpeed : 2f;
    public string DisplayName => definition != null ? definition.displayName : name;

    /// <summary>
    /// 플레이어와 대화 중처럼 모듈이 NPC를 붙잡고 있는지. 브레인은 이때 이동을 멈춥니다.
    /// 멈추지 않으면 NPC가 걸어가 버려 대화가 거리 조건으로 끊깁니다.
    /// </summary>
    public bool IsEngaged
    {
        get
        {
            if (modules == null) return false;
            foreach (NpcInteractionModule module in modules)
                if (module != null && module.enabled && module.IsEngaged) return true;

            return false;
        }
    }

    private void Awake()
    {
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        Mover = new GridMover(this);
        Perception = new Perception(this);
        Overrides = GetComponent<FactionOverride>();
        brain = GetComponent<NpcBrain>();
        modules = GetComponents<NpcInteractionModule>();

        // definition이 없어도 기본값으로 버팁니다. 프리팹 배선을 하나 잊었다고
        // 씬 전체가 멈추는 것보다 로그 한 줄이 낫습니다.
        if (definition == null)
            Debug.LogError($"[NpcActor] '{name}'에 NpcDefinition이 연결되지 않아 기본 수치로 동작합니다.", this);

        currentHealth = MaxHealth;
    }

    private void OnEnable() => NpcRegistry.Add(this);
    private void OnDisable() => NpcRegistry.Remove(this);

    /// <summary>
    /// 던전은 DungeonManager가 층 준비 후 NotifyLocationReady를 불러줍니다.
    /// 마을·오버월드에는 그런 관리자가 없어 여기서 스스로 준비합니다.
    /// </summary>
    private void Start()
    {
        if (!locationReady) NotifyLocationReady();
    }

    private void Update()
    {
        // 전투불능이어도 중력은 계속 적용해 공중에 멈추지 않게 합니다.
        if (downed)
        {
            Move(Vector3.zero);
            return;
        }

        brain?.Tick();

        // 목적지가 없으면 GridMover가 Move를 부르지 않으므로 여기서 중력만 적용합니다.
        // 브레인마다 챙기게 두면 새 브레인을 만들 때 잊기 쉽고, 목적지가 있을 때 브레인이 또 부르면
        // 한 프레임에 Move가 두 번 불려 중력이 두 배가 됩니다.
        if (Mover.HasDestination) Mover.Tick();
        else Move(Vector3.zero);

        UpdateLocomotionAnimation();
    }

    /// <summary>
    /// 걷기/대기 모션 전환. 브레인이 아니라 몸이 맡아야 어떤 브레인을 끼워도 같은 모션이 나옵니다.
    /// 애니메이터의 상태 이름(Idle/Walk)으로 전환합니다 — 적이 "Chase" 상태를 이름으로 부르는 것과 같은 방식입니다.
    /// </summary>
    private void UpdateLocomotionAnimation()
    {
        if (animator == null) return;

        if (Mover.IsMoving) stillTime = 0f;
        else stillTime += Time.deltaTime;

        bool shouldWalk = stillTime < IdleSwitchDelay;
        if (shouldWalk == walkAnimPlaying) return;

        walkAnimPlaying = shouldWalk;
        string stateName = shouldWalk ? WalkStateName : IdleStateName;

        int shortHash = Animator.StringToHash(stateName);
        int fullHash = Animator.StringToHash("Base Layer." + stateName);
        if (!animator.HasState(0, shortHash) && !animator.HasState(0, fullHash))
        {
            if (!warnedMissingAnimState)
            {
                Debug.LogWarning($"[NpcActor] '{name}'의 애니메이터에 '{stateName}' 상태가 없어 모션을 바꾸지 않습니다. " +
                                 $"Base Layer에 '{IdleStateName}'과 '{WalkStateName}' 상태를 만들어주세요.", this);
                warnedMissingAnimState = true;
            }
            return;
        }

        animator.CrossFadeInFixedTime(shortHash, LocomotionCrossFade);
    }

    /// <summary>
    /// 장소가 준비됐음을 알립니다. 던전에서는 층에 들어설 때마다 불리므로
    /// 스폰 위치로 되돌리는 역할도 겸합니다. (EnemyController와 같은 생명주기)
    /// </summary>
    public void NotifyLocationReady()
    {
        locationReady = true;

        LocationRoot root = GetComponentInParent<LocationRoot>();
        if (root == null) root = SceneLocation.FindInScene(gameObject.scene);   // 씬 맵 밖에 놓인 NPC
        Grid = root != null ? root.Grid : null;

        if (!spawnCaptured)
        {
            // 적처럼 셀 중앙에 맞춥니다. 어긋난 채로 출발하면 첫 경로가 비틀리고,
            // 그리드가 없는 장소에서는 기준이 되는 칸이 없으므로 그대로 둡니다.
            if (Grid != null)
            {
                Vector3 center = LocationGrid.CellToWorld(CurrentCell);
                Teleport(new Vector3(center.x, transform.position.y, center.z), transform.rotation);
            }

            spawnPosition = transform.position;
            spawnCell = CurrentCell;
            spawnRotation = transform.rotation;
            spawnCaptured = true;
        }

        ResetToSpawn();
    }

    public void ResetToSpawn()
    {
        Mover.Stop();
        Teleport(spawnPosition, spawnRotation);

        currentHealth = MaxHealth;
        downed = false;
        verticalVelocity = 0f;

        brain?.OnLocationReady(this);
    }

    private void Teleport(Vector3 position, Quaternion rotation)
    {
        bool ccWasEnabled = characterController != null && characterController.enabled;
        if (characterController != null)
            characterController.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        if (characterController != null)
            characterController.enabled = ccWasEnabled;
    }

    /// <summary>중력을 포함한 이동. EnemyController.MoveEnemy와 같은 식입니다.</summary>
    public void Move(Vector3 motion)
    {
        if (characterController == null || !characterController.enabled)
            return;

        if (characterController.isGrounded)
        {
            if (verticalVelocity < 0.0f) verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += GravityAcceleration * GravityMultiplier * Time.deltaTime;
        }

        motion.y = verticalVelocity * Time.deltaTime;
        characterController.Move(motion);
    }

    public void RotateToward(Vector3 direction)
        => ActorMath.RotateToward(transform, direction, definition != null ? definition.turnSpeed : 540f);

    /// <summary>수평면에서 특정 위치를 바라보도록 즉시 회전합니다.</summary>
    public void FaceToward(Vector3 worldPosition) => ActorMath.FaceToward(transform, worldPosition);

    // ── IGridAgent ───────────────────────────────────────────────────
    // Transform / CurrentCell / Grid / RotateToward / Move / IsAlive는 위의 public 멤버가
    // 그대로 계약을 만족합니다. 나머지는 definition에서 읽어오는 얇은 껍데기라 명시적 구현으로 둡니다.

    public Transform Transform => transform;
    public Vector2Int CurrentCell => LocationGrid.WorldToCell(transform.position);

    CharacterController IGridAgent.Controller => characterController;
    float IGridAgent.PathTrapCost => definition != null ? definition.trapPathCost : 1f;
    float IGridAgent.PitClimbDuration => definition != null ? definition.pitClimbDuration : 0.6f;

    float IGridAgent.VerticalVelocity
    {
        get => verticalVelocity;
        set => verticalVelocity = value;
    }

    /// <summary>구덩이 탈출 지점의 바닥을 잴 때 자기 자신은 바닥으로 치지 않습니다.</summary>
    int IGridAgent.GroundProbeMask => ~(1 << gameObject.layer);

    // ── IPerceptionAgent ─────────────────────────────────────────────
    float IPerceptionAgent.ViewAngle => definition != null ? definition.viewAngle : 120f;
    float IPerceptionAgent.DetectionRadius => definition != null ? definition.detectionRadius : 7f;
    LayerMask IPerceptionAgent.ObstacleLayer => obstacleLayer;
    float IPerceptionAgent.EyeHeight => EyeHeight;

    private float EyeHeight => definition != null ? definition.eyeHeight : 1.5f;

    // ── IFactionMember ───────────────────────────────────────────────
    // definition이 없으면 Neutral입니다. 진영을 모르는 NPC가 아무도 공격하지 않는 쪽이 안전합니다.
    public FactionId Faction => definition != null ? definition.faction : FactionId.Neutral;

    // ── ITarget / IDamageable ────────────────────────────────────────
    public bool IsAlive => !downed;
    public Vector3 AimPoint => transform.position + Vector3.up * EyeHeight;
    public Vector2Int ApproachCell => CurrentCell;
    public bool IsMobile => true;

    /// <summary>
    /// 피해를 받습니다. 체력이 0이 되면 '사망'이 아니라 '전투불능(downed)'입니다 —
    /// 파티 동료가 일으켜 세우는 경우를 염두에 둔 이름입니다.
    /// </summary>
    public void TakeDamage(float damage, Transform attacker)
    {
        if (downed) return;

        currentHealth -= damage;
        if (currentHealth > 0f) return;

        currentHealth = 0f;
        downed = true;
        Mover.Stop();
        brain?.OnDowned();
    }

    // ── IInteractable ────────────────────────────────────────────────
    // 플레이어가 상대하는 창구는 NpcActor 하나입니다. 실제 동작은 지금 사용 가능한 모듈 중
    // 우선순위가 가장 높은 것이 맡습니다. 모듈이 하나도 없으면 말을 걸 수 없는 일반 NPC입니다.

    public bool CanInteract(PlayerController player) => FindModule(player) != null;

    public void Interact(PlayerController player) => FindModule(player)?.Interact(player);

    private NpcInteractionModule FindModule(PlayerController player)
    {
        if (modules == null || downed) return null;   // 전투불능 NPC에게는 말을 걸 수 없습니다

        NpcInteractionModule best = null;
        foreach (NpcInteractionModule module in modules)
        {
            if (module == null || !module.enabled) continue;
            if (!module.CanInteract(player)) continue;
            if (best != null && module.Priority <= best.Priority) continue;

            best = module;
        }

        return best;
    }
}
