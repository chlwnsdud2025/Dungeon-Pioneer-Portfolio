using System.Collections.Generic;
using UnityEngine;

public class EnemyController : MonoBehaviour, IDamageable, IGridAgent, IPerceptionAgent, IFactionMember
{
    // [핵심] 상태 머신
    public EnemyStateMachine stateMachine { get; private set; }

    [Header("Components")]
    public Animator animator;
    public CharacterController characterController;

    [Header("Enemy Stats")]
    public float maxHealth = 20f;
    public float currentHealth;
    public float moveSpeed = 2f;
    public float turnSpeed = 540f;

    [Header("Aggro & Combat Settings")]
    public float detectionRadius = 7f;   // 플레이어 인지 범위
    public float attackRange = 1.5f;     // 공격 사거리
    public float attackCooldown = 2f;    // 공격 쿨타임
    public float attackDamage = 5f;      // 공격 데미지
    public LayerMask playerLayer;        // 플레이어 레이어
    public LayerMask obstacleLayer;      // 벽 등 시야를 가리는 장애물 레이어

    [Tooltip("이 적이 속한 진영. 기본은 Monster이며, 아군이 되는 특수 몬스터 등에만 바꿉니다.")]
    public FactionId faction = FactionId.Monster;

    [Header("Torch Hunting")]
    [Tooltip("순찰 중 시야에 들어온 횃불을 찾아가 꺼뜨릴지. 플레이어가 보이면 언제나 플레이어가 우선입니다.")]
    public bool huntsTorches = true;
    [Tooltip("횃불 앞에 도착했는데도 사거리에 들지 못한 채 이 시간(초)이 지나거나, 경로가 없으면 그 횃불을 포기합니다.")]
    public float torchGiveUpSeconds = 3f;
    [Tooltip("포기한 횃불을 다시 노리기까지 무시하는 시간(초). 벽 너머 횃불에 계속 매달리는 것을 막습니다.")]
    public float torchIgnoreSeconds = 10f;

    [HideInInspector] public Vector3 spawnPosition;
    [HideInInspector] public Vector2Int spawnCell;
    [HideInInspector] public Quaternion spawnRotation;

    private bool spawnCaptured;

    /// <summary>
    /// 지금 쫓는 대상. 플레이어일 수도, 횃불일 수도 있습니다. 상태 머신은 이 인터페이스만 보고
    /// '누구'인지는 구분하지 않습니다 — 플레이어 우선순위는 상태 전환이 아니라 이 값의 교체로 구현됩니다.
    /// </summary>
    [HideInInspector] public ITarget target;
    [HideInInspector] public float lastAttackTime = -999f;

    // 포기한 대상을 잠시 무시하는 목록 (대상 → 다시 노려도 되는 시각)
    private readonly Dictionary<ITarget, float> ignoreUntil = new Dictionary<ITarget, float>();

    // "Torch" 레이어는 프로젝트에 하나뿐이라 인스펙터 배선 대신 이름으로 찾습니다. 없으면 0(아무것도 안 맞음).
    private static int torchLayerMask = -1;

    /// <summary>공격 히트박스가 때릴 수 있는 레이어 = 플레이어 + 횃불.</summary>
    public int AttackTargetMask => playerLayer.value | TorchLayerMask;

    private static int TorchLayerMask
    {
        get
        {
            if (torchLayerMask < 0)
            {
                int layer = LayerMask.NameToLayer("Torch");
                torchLayerMask = layer >= 0 ? 1 << layer : 0;
                if (layer < 0)
                    Debug.LogWarning("[EnemyController] 'Torch' 레이어가 없어 적이 횃불을 때리지 못합니다. Tags and Layers에서 추가하세요.");
            }
            return torchLayerMask;
        }
    }

    [Range(0, 360)]
    public float viewAngle = 120f;

    [Tooltip("시야선(Raycast)을 쏘는 눈높이. 기존에 하드코딩돼 있던 값이 1.5였습니다.")]
    public float eyeHeight = 1.5f;

    [Header("Backstab")]
    [Tooltip("시야(FOV) 밖에서 맞았을 때 받는 피해 배율")]
    public float backstabDamageMultiplier = 1.5f;

    [Header("Gravity Settings")]
    public float gravity = -9.81f;
    public float gravityMultiplier = 2f;
    [HideInInspector] public float verticalVelocity;

    [Header("Pathfinding")]
    [Tooltip("추적 중 바닥함정 셀의 이동 비용. 1이면 일반 바닥과 같게 취급해 최단 경로로 밟고 지나갑니다.")]
    public float trapPathCost = 1f;
    [Tooltip("구덩이에서 인접 셀로 기어오르는 데 걸리는 시간(초)")]
    public float pitClimbDuration = 0.6f;

    public LocationGrid Grid { get; private set; }
    public GridMover Mover { get; private set; }
    public Vector2Int CurrentCell => LocationGrid.WorldToCell(transform.position);

    /// <summary>시야·시야선 계산. 누구를 쫓을지(정책)는 아래 TryDetect~들이 정하고, 이쪽은 보이는지만 답합니다.</summary>
    public Perception Perception { get; private set; }

    // ── IGridAgent / IPerceptionAgent 구현 ───────────────────────────
    // 대부분 기존 필드를 이름만 바꿔 노출하는 껍데기라 값도 로직도 그대로입니다.
    // 명시적 구현(IGridAgent.XXX)을 쓴 이유: trapPathCost 같은 기존 public 필드와 이름만 다른
    // public 프로퍼티가 나란히 생기면 어느 쪽이 진짜인지 헷갈립니다. 명시적 구현은
    // GridMover/Perception에게만 보이고, EnemyController의 공개 API는 지금 그대로 유지됩니다.

    /// <summary>IGridAgent와 IPerceptionAgent가 함께 씁니다. 그래서 이것만 암시적 구현입니다.</summary>
    public Transform Transform => transform;

    CharacterController IGridAgent.Controller => characterController;
    float IGridAgent.PathTrapCost => trapPathCost;
    float IGridAgent.PitClimbDuration => pitClimbDuration;

    float IGridAgent.VerticalVelocity
    {
        get => verticalVelocity;
        set => verticalVelocity = value;
    }

    /// <summary>구덩이 탈출 지점의 바닥을 잴 때 자기 자신과 플레이어는 바닥으로 치지 않습니다.</summary>
    int IGridAgent.GroundProbeMask => ~((1 << gameObject.layer) | playerLayer.value);

    /// <summary>기존 MoveEnemy로 그대로 넘깁니다. 상태 클래스들은 계속 MoveEnemy를 부르면 됩니다.</summary>
    void IGridAgent.Move(Vector3 motion) => MoveEnemy(motion);

    float IPerceptionAgent.ViewAngle => viewAngle;
    float IPerceptionAgent.DetectionRadius => detectionRadius;
    LayerMask IPerceptionAgent.ObstacleLayer => obstacleLayer;
    float IPerceptionAgent.EyeHeight => eyeHeight;

    // ── 진영 (IFactionMember) ─────────────────────────────────
    public FactionId Faction => faction;
    public FactionOverride Overrides { get; private set; }

    private static readonly HashSet<EnemyController> ActiveEnemies = new HashSet<EnemyController>();

    public static IEnumerable<EnemyController> EnumerateActiveEnemies() => ActiveEnemies;

    private void Awake()
    {
        stateMachine = new EnemyStateMachine();
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        Mover = new GridMover(this);
        Perception = new Perception(this);
        Overrides = GetComponent<FactionOverride>();

        currentHealth = maxHealth;

        // TODO: 나중에 상태 클래스들을 만들면 여기서 AddState 해줍니다.
        stateMachine.AddState(new EnemyIdleState(this, stateMachine));
        stateMachine.AddState(new EnemyPatrolState(this, stateMachine));
        stateMachine.AddState(new EnemyAlertState(this, stateMachine));
        stateMachine.AddState(new EnemyChaseState(this, stateMachine));
        stateMachine.AddState(new EnemyAttackState(this, stateMachine));
        stateMachine.AddState(new EnemyHitState(this, stateMachine));
        stateMachine.AddState(new EnemyDeadState(this, stateMachine));
    }

    private void OnEnable() => ActiveEnemies.Add(this);
    private void OnDisable() => ActiveEnemies.Remove(this);

    public void NotifyLocationReady()
    {
        LocationRoot root = GetComponentInParent<LocationRoot>();
        if (root == null) root = SceneLocation.FindInScene(gameObject.scene);   // 씬 맵 밖에 놓인 적
        Grid = root != null ? root.Grid : null;

        if (!spawnCaptured)
        {
            Vector3 center = LocationGrid.CellToWorld(CurrentCell);
            Teleport(new Vector3(center.x, transform.position.y, center.z), transform.rotation);
            spawnPosition = transform.position;
            spawnCell = CurrentCell;
            spawnRotation = transform.rotation;
            spawnCaptured = true;
        }

        if (stateMachine.CurrentState is EnemyDeadState) return;

        ResetToSpawn();
    }

    public void ResetToSpawn()
    {
        Mover.Stop();
        Teleport(spawnPosition, spawnRotation);

        currentHealth = maxHealth;
        target = null;
        ignoreUntil.Clear();
        lastAttackTime = -999f;
        verticalVelocity = 0f;

        if (stateMachine.CurrentState == null)
            stateMachine.Initialize<EnemyIdleState>();
        else
            stateMachine.ChangeState<EnemyIdleState>();
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

    private void Update()
    {
        stateMachine.Update();
        Mover.Tick();
    }

    private void FixedUpdate()
    {
        stateMachine.FixedUpdate();
    }

    public void SetAnimatorLocomotionSpeed(float speed)
    {
        if (animator != null)
            animator.speed = speed;
    }

    public void ResetAnimatorLocomotionSpeed()
    {
        if (animator != null)
            animator.speed = 1f;
    }

    // 플레이어의 MoveCharacter와 동일한 중력 및 이동 로직 (간단한 형태)
    public void MoveEnemy(Vector3 motion)
    {
        if (characterController == null || !characterController.enabled)
            return;

        if (characterController.isGrounded)
        {
            if (verticalVelocity < 0.0f) verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += gravity * gravityMultiplier * Time.deltaTime;
        }

        motion.y = verticalVelocity * Time.deltaTime;
        characterController.Move(motion);
    }

    // ── 지각·회전 (구현은 Perception / ActorMath에 있고 여기서는 위임만 합니다) ──
    // 상태 클래스들이 계속 enemy.IsInFieldOfView(...) 식으로 부를 수 있게 이름을 유지합니다.

    /// <summary>월드 위치가 전방 FOV 각도 안에 있는지 (장애물 무시).</summary>
    public bool IsInFieldOfView(Vector3 worldPosition) => Perception.IsInFieldOfView(worldPosition);

    /// <summary>
    /// 대상이 FOV 안에 있고 벽으로 시야가 막히지 않았는지.
    /// 레이의 끝점은 대상이 알려주는 AimPoint입니다 — 플레이어는 눈높이, 벽에 붙은 횃불은 벽에서
    /// 살짝 띄운 점이라 자기 벽에 막혀 '안 보임'으로 오판하지 않습니다.
    /// </summary>
    public bool CanSeeTarget(ITarget target) => Perception.CanSee(target);

    /// <summary>수평면에서 특정 위치를 바라보도록 즉시 회전합니다.</summary>
    public void FaceToward(Vector3 worldPosition) => ActorMath.FaceToward(transform, worldPosition);

    public void RotateToward(Vector3 direction) => ActorMath.RotateToward(transform, direction, turnSpeed);

    /// <summary>위치가 FOV 안이면 전체 탐지 반경, 밖이면 1/3 반경을 사용합니다.</summary>
    public float GetDetectionRangeForPosition(Vector3 worldPosition) => Perception.GetDetectionRangeFor(worldPosition);

    public float GetHorizontalDistanceTo(Vector3 worldPosition) => Perception.HorizontalDistanceTo(worldPosition);

    private PlayerController FindNearestPlayerInRange(float maxRange)
    {
        Collider[] hitPlayers = Physics.OverlapSphere(transform.position, maxRange, playerLayer);
        if (hitPlayers.Length == 0) return null;

        PlayerController nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider hit in hitPlayers)
        {
            if (hit == null) continue;

            PlayerController controller = hit.GetComponentInParent<PlayerController>();
            if (controller == null) continue;

            Vector3 candidatePos = controller.transform.position;
            float distance = GetHorizontalDistanceTo(candidatePos);
            float allowedRange = GetDetectionRangeForPosition(candidatePos);
            if (distance > allowedRange) continue;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = controller;
            }
        }

        return nearest;
    }

    /// <summary>플레이어 탐지: FOV 방향은 전체 반경 + 시야선, 성공 시 target에 할당.</summary>
    public bool TryDetectPlayer()
    {
        PlayerController potentialTarget = FindNearestPlayerInRange(detectionRadius);
        if (potentialTarget == null) return false;
        if (!IsInFieldOfView(potentialTarget.transform.position)) return false;
        if (!CanSeeTarget(potentialTarget)) return false;

        target = potentialTarget;
        return true;
    }

    /// <summary>
    /// FOV 밖에서 플레이어가 달리며 접근했을 때 반응합니다.
    /// 탐지 반경은 전방 FOV의 1/3입니다.
    /// </summary>
    public bool TryDetectRunningPlayerNearby(out PlayerController player)
    {
        player = null;

        float peripheralRange = detectionRadius / 3f;
        Collider[] hitPlayers = Physics.OverlapSphere(transform.position, peripheralRange, playerLayer);
        if (hitPlayers.Length == 0) return false;

        PlayerController nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider hit in hitPlayers)
        {
            if (hit == null) continue;

            Vector3 candidatePos = hit.transform.position;
            if (IsInFieldOfView(candidatePos)) continue;

            PlayerController controller = hit.GetComponentInParent<PlayerController>();
            if (controller == null || !controller.isRunning) continue;

            float distance = GetHorizontalDistanceTo(candidatePos);
            if (distance > peripheralRange || distance >= nearestDistance) continue;

            nearestDistance = distance;
            nearest = controller;
        }

        if (nearest == null) return false;

        player = nearest;
        target = nearest;
        return true;
    }

    /// <summary>
    /// 횃불 탐지: FOV 안 + 탐지 반경 안 + 시야선이 닿고 + 횃불 앞 바닥 셀에 설 수 있고 + 포기 목록에
    /// 없는 것 중 가장 가까운 횃불을 target에 넣습니다. 플레이어 탐지가 실패했을 때만 부르세요.
    /// </summary>
    public bool TryDetectTorch()
    {
        if (!huntsTorches || Grid == null) return false;

        TorchInstance nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (TorchInstance torch in TorchInstance.EnumerateActiveTorches())
        {
            if (torch == null || !torch.IsAlive) continue;
            if (IsTargetIgnored(torch)) continue;

            float distance = GetHorizontalDistanceTo(torch.transform.position);
            if (distance > detectionRadius || distance >= nearestDistance) continue;
            if (!IsInFieldOfView(torch.transform.position)) continue;
            if (!Grid.IsPassable(torch.ApproachCell, false)) continue;
            if (!CanSeeTarget(torch)) continue;

            nearestDistance = distance;
            nearest = torch;
        }

        if (nearest == null) return false;

        target = nearest;
        return true;
    }

    /// <summary>대상을 잠시 포기합니다. (경로가 없거나 시야를 오래 잃은 횃불 등)</summary>
    public void IgnoreTarget(ITarget ignored, float seconds)
    {
        if (ignored == null) return;
        ignoreUntil[ignored] = Time.time + seconds;
    }

    private bool IsTargetIgnored(ITarget candidate)
    {
        if (!ignoreUntil.TryGetValue(candidate, out float until)) return false;
        if (Time.time < until) return true;

        ignoreUntil.Remove(candidate);   // 기한이 지났으면 목록에서 정리
        return false;
    }

    /// <summary>현재 추적 대상이 감지 범위를 벗어났는지 확인합니다. 추격 중에는 FOV와 무관하게 전체 반경을 유지합니다.</summary>
    public bool IsTargetWithinDetectionRange()
    {
        if (target == null || target.Transform == null) return false;

        float distance = GetHorizontalDistanceTo(target.Transform.position);
        return distance <= detectionRadius * 1.5f;
    }

    // ── 피격 (IDamageable) ───────────────────────────────────

    public bool IsAlive => stateMachine == null || !(stateMachine.CurrentState is EnemyDeadState);

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, null);
    }

    public void TakeDamage(float damage, Transform attacker)
    {
        if (!IsAlive) return;

        float finalDamage = damage;
        if (attacker != null)
        {
            // 플레이어에게 맞으면 무엇을 쫓고 있었든(횃불 포함) 플레이어가 우선이 된다.
            PlayerController attackingPlayer = attacker.GetComponentInParent<PlayerController>();
            if (attackingPlayer != null)
                target = attackingPlayer;

            if (!IsInFieldOfView(attacker.position))
            {
                finalDamage = damage * backstabDamageMultiplier;
                FaceToward(attacker.position);
            }
        }

        currentHealth -= finalDamage;

        if (currentHealth <= 0)
            stateMachine.ChangeState<EnemyDeadState>();
        else if (!Mover.IsClimbing)
            stateMachine.ChangeState<EnemyHitState>();
    }

    // 씬 뷰에서 인지 범위와 공격 범위를 확인하기 위한 디버그 드로잉
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius / 3f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (Mover == null || Grid == null) return;

        IReadOnlyList<Vector2Int> path = Mover.CurrentPath;
        Vector3 previous = transform.position + Vector3.up * 0.2f;
        for (int i = Mover.PathIndex; i < path.Count; i++)
        {
            Vector3 point = LocationGrid.CellToWorld(path[i]);
            point.y = previous.y;

            Gizmos.color = Grid.Get(path[i]) == CellType.Pit ? Color.magenta : Color.cyan;
            Gizmos.DrawLine(previous, point);
            Gizmos.DrawWireCube(point, Vector3.one * 0.3f);
            previous = point;
        }
    }
}
