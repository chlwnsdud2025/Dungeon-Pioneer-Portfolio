using System.Collections.Generic;
using UnityEngine;

public class EnemyController : MonoBehaviour
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

    [HideInInspector] public Vector3 spawnPosition;
    [HideInInspector] public Vector2Int spawnCell;
    [HideInInspector] public Quaternion spawnRotation;

    private bool spawnCaptured;

    [HideInInspector] public Transform target;
    [HideInInspector] public float lastAttackTime = -999f;

    [Range(0, 360)]
    public float viewAngle = 120f;

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
    public EnemyGridMover Mover { get; private set; }
    public Vector2Int CurrentCell => LocationGrid.WorldToCell(transform.position);

    private static readonly HashSet<EnemyController> ActiveEnemies = new HashSet<EnemyController>();

    public static IEnumerable<EnemyController> EnumerateActiveEnemies() => ActiveEnemies;

    private void Awake()
    {
        stateMachine = new EnemyStateMachine();
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        Mover = new EnemyGridMover(this);

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

    /// <summary>월드 위치가 전방 FOV 각도 안에 있는지 (장애물 무시).</summary>
    public bool IsInFieldOfView(Vector3 worldPosition)
    {
        Vector3 dirToTarget = worldPosition - transform.position;
        dirToTarget.y = 0f;
        if (dirToTarget.sqrMagnitude < 0.0001f) return true;

        return Vector3.Angle(transform.forward, dirToTarget.normalized) <= viewAngle * 0.5f;
    }

    /// <summary>타겟이 FOV 안에 있고 벽으로 시야가 막히지 않았는지.</summary>
    public bool CanSeeTarget(Transform target)
    {
        if (target == null) return false;
        if (!IsInFieldOfView(target.position)) return false;

        Vector3 eyePosition = transform.position + Vector3.up * 1.5f;
        Vector3 targetPos = target.position + Vector3.up * 1.5f;
        Vector3 rayDir = (targetPos - eyePosition).normalized;
        float distanceToTarget = Vector3.Distance(eyePosition, targetPos);

        if (!Physics.Raycast(eyePosition, rayDir, out RaycastHit hit, distanceToTarget, obstacleLayer,
                QueryTriggerInteraction.Ignore))
            return true;

        // Pushable/Chest 등 Interactable은 시야를 가리지 않고, Wall만 가립니다.
        return !(hit.collider.CompareTag("Wall") || hit.collider.GetComponentInParent<WallObject>() != null);
    }

    /// <summary>수평면에서 특정 위치를 바라보도록 즉시 회전합니다.</summary>
    public void FaceToward(Vector3 worldPosition)
    {
        Vector3 dir = worldPosition - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    public void RotateToward(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion desired = Quaternion.LookRotation(direction.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, turnSpeed * Time.deltaTime);
    }

    /// <summary>위치가 FOV 안이면 전체 탐지 반경, 밖이면 1/3 반경을 사용합니다.</summary>
    public float GetDetectionRangeForPosition(Vector3 worldPosition)
    {
        return IsInFieldOfView(worldPosition) ? detectionRadius : detectionRadius / 3f;
    }

    public float GetHorizontalDistanceTo(Vector3 worldPosition)
    {
        Vector3 delta = worldPosition - transform.position;
        delta.y = 0f;
        return delta.magnitude;
    }

    private Transform FindNearestPlayerInRange(float maxRange)
    {
        Collider[] hitPlayers = Physics.OverlapSphere(transform.position, maxRange, playerLayer);
        if (hitPlayers.Length == 0) return null;

        Transform nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider hit in hitPlayers)
        {
            if (hit == null) continue;

            PlayerController controller = hit.GetComponentInParent<PlayerController>();
            Transform candidate = controller != null ? controller.transform : hit.transform;
            float distance = GetHorizontalDistanceTo(candidate.position);
            float allowedRange = GetDetectionRangeForPosition(candidate.position);
            if (distance > allowedRange) continue;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = candidate;
            }
        }

        return nearest;
    }

    /// <summary>플레이어 탐지: FOV 방향은 전체 반경 + 시야선, 성공 시 target에 할당.</summary>
    public bool TryDetectPlayer()
    {
        Transform potentialTarget = FindNearestPlayerInRange(detectionRadius);
        if (potentialTarget == null) return false;
        if (!IsInFieldOfView(potentialTarget.position)) return false;
        if (!CanSeeTarget(potentialTarget)) return false;

        target = potentialTarget;
        return true;
    }

    /// <summary>
    /// FOV 밖에서 플레이어가 달리며 접근했을 때 반응합니다.
    /// 탐지 반경은 전방 FOV의 1/3입니다.
    /// </summary>
    public bool TryDetectRunningPlayerNearby(out Transform player)
    {
        player = null;

        float peripheralRange = detectionRadius / 3f;
        Collider[] hitPlayers = Physics.OverlapSphere(transform.position, peripheralRange, playerLayer);
        if (hitPlayers.Length == 0) return false;

        Transform nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider hit in hitPlayers)
        {
            if (hit == null) continue;

            Transform candidate = hit.transform;
            if (IsInFieldOfView(candidate.position)) continue;

            PlayerController controller = candidate.GetComponentInParent<PlayerController>();
            if (controller == null || !controller.isRunning) continue;

            float distance = GetHorizontalDistanceTo(candidate.position);
            if (distance > peripheralRange || distance >= nearestDistance) continue;

            nearestDistance = distance;
            nearest = candidate;
        }

        if (nearest == null) return false;

        player = nearest;
        target = nearest;
        return true;
    }

    /// <summary>현재 추적 대상이 감지 범위를 벗어났는지 확인합니다. 추격 중에는 FOV와 무관하게 전체 반경을 유지합니다.</summary>
    public bool IsTargetWithinDetectionRange()
    {
        if (target == null) return false;

        float distance = GetHorizontalDistanceTo(target.position);
        return distance <= detectionRadius * 1.5f;
    }

    // 데미지 처리
    public void TakeDamage(float damage)
    {
        TakeDamage(damage, null);
    }

    public void TakeDamage(float damage, Transform attacker)
    {
        if (stateMachine.CurrentState is EnemyDeadState) return;

        float finalDamage = damage;
        if (attacker != null)
        {
            if (attacker.GetComponentInParent<PlayerController>() != null)
                target = attacker;

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
