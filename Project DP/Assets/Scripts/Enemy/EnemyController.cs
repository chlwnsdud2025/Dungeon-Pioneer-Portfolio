using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyController : MonoBehaviour
{
    // [핵심] 상태 머신
    public EnemyStateMachine stateMachine { get; private set; }

    [Header("Components")]
    public Animator animator;
    public CharacterController characterController;
    [HideInInspector] public NavMeshAgent agent;

    [Header("Enemy Stats")]
    public float maxHealth = 20f;
    public float currentHealth;
    public float moveSpeed = 2f;

    [Header("Aggro & Combat Settings")]
    public float detectionRadius = 7f;   // 플레이어 인지 범위
    public float attackRange = 1.5f;     // 공격 사거리
    public float attackCooldown = 2f;    // 공격 쿨타임
    public float attackDamage = 5f;      // 공격 데미지
    public LayerMask playerLayer;        // 플레이어 레이어
    public LayerMask obstacleLayer;      // 벽 등 시야를 가리는 장애물 레이어

    [HideInInspector] public Vector3 spawnPosition;

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

    [Header("Trap NavMesh")]
    [Tooltip("함정 NavMesh 영역 이동 비용. Walkable(1)과 같게 두면 최단 경로로 함정을 밟고 지나갑니다.")]
    public float trapPathCost = 1f;

    private int trapAreaIndex;
    private bool trapPathCostApplied;
    private float pitRescueLockUntil;

    private const float NavMeshSampleRadius = 6f;

    private static readonly HashSet<EnemyController> ActiveEnemies = new HashSet<EnemyController>();

    public static IEnumerable<EnemyController> EnumerateActiveEnemies() => ActiveEnemies;

    private void Awake()
    {
        // ★ NavMesh.GetAreaFromName처럼 엔진 API를 호출하는 코드는 필드 초기화식/정적 생성자에서
        //   실행하면 안 되고(Unity가 막음), 반드시 Awake/Start 같은 인스턴스 메서드 안에서 호출해야 합니다.
        trapAreaIndex = NavMesh.GetAreaFromName("Trap");

        stateMachine = new EnemyStateMachine();
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        currentHealth = maxHealth;
        if (agent != null)
            agent.enabled = false;

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

    /// <summary>DungeonManager가 NavMesh 베이크 직후 호출합니다. 이전 층 NavMesh에 먼저 붙는 레이스를 막습니다.</summary>
    public void NotifyNavMeshReady()
    {
        if (TrySampleNavMeshPosition(transform.position, out NavMeshHit hit))
            spawnPosition = hit.position;
        else
            spawnPosition = transform.position;

        AlignToNavMesh();

        if (stateMachine.CurrentState is EnemyDeadState) return;

        if (stateMachine.CurrentState == null)
            stateMachine.Initialize<EnemyIdleState>();
        else
            stateMachine.ChangeState<EnemyIdleState>();
    }

    private void Update()
    {
        TryApplyTrapPathCost();
        stateMachine.Update();
        // 상태 머신(중력/MoveEnemy) 이후에 탈출해야 Hit/Idle이 다시 구덩이로 끌어내리지 않습니다.
        TryRecoverFromPit();
        ApplyNavMeshLocomotion();
    }

    /// <summary>구덩이에 빠졌을 때 함정 NavMesh 보행면(또는 가장 가까운 NavMesh)으로 복귀합니다.</summary>
    private void TryRecoverFromPit()
    {
        if (stateMachine.CurrentState is EnemyDeadState) return;

        bool rescuedFromTrap = false;
        Vector3 rescuePos = transform.position;

        // 매 프레임 전체 검색 대신, 발 아래/근처에 함정이 있을 때만 구조
        Collider[] nearby = Physics.OverlapSphere(transform.position, 2.5f, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < nearby.Length; i++)
        {
            TrapTile trap = nearby[i].GetComponentInParent<TrapTile>();
            if (trap == null) continue;
            if (!trap.IsWorldPositionInsidePit(transform.position)) continue;
            if (!trap.TryGetRescueWorldPosition(out rescuePos)) continue;
            rescuedFromTrap = true;
            break;
        }

        if (!rescuedFromTrap)
        {
            if (!TryGetNearestNavMeshPosition(out NavMeshHit hit))
                return;

            bool fallenBelowSurface = transform.position.y < hit.position.y - 0.35f;
            bool lostNavMeshWhileLocomoting = IsLocomoting()
                && agent != null && agent.enabled && !agent.isOnNavMesh;

            if (!fallenBelowSurface && !lostNavMeshWhileLocomoting)
                return;

            rescuePos = hit.position;
        }

        bool ccWasEnabled = characterController != null && characterController.enabled;
        if (characterController != null)
            characterController.enabled = false;

        transform.position = rescuePos;
        verticalVelocity = -2f;
        pitRescueLockUntil = Time.time + 0.4f;

        if (characterController != null)
            characterController.enabled = ccWasEnabled;

        if (agent != null && agent.enabled)
        {
            if (TrySampleNavMeshPosition(rescuePos, out NavMeshHit warpHit))
            {
                agent.Warp(warpHit.position);
                agent.nextPosition = warpHit.position;
                transform.position = warpHit.position;
            }
            else
            {
                agent.Warp(rescuePos);
                agent.nextPosition = rescuePos;
            }
        }
    }

    private bool TryGetNearestNavMeshPosition(out NavMeshHit hit)
    {
        Vector3 probe = transform.position + Vector3.up * 3f;
        if (NavMesh.SamplePosition(probe, out hit, NavMeshSampleRadius, NavMesh.AllAreas))
            return true;

        return NavMesh.SamplePosition(transform.position, out hit, NavMeshSampleRadius, NavMesh.AllAreas);
    }

    /// <summary>NavMeshAgent 경로를 따라 CharacterController로 이동해 벽 충돌을 적용합니다.</summary>
    private void ApplyNavMeshLocomotion()
    {
        if (!IsLocomoting() || agent == null || !agent.enabled)
            return;

        if (!agent.isOnNavMesh)
        {
            TryRecoverFromPit();
            if (!agent.isOnNavMesh) return;
        }

        Vector3 desired = agent.nextPosition;
        if (NavMesh.SamplePosition(desired, out NavMeshHit navHit, 1f, NavMesh.AllAreas))
            desired = navHit.position;

        Vector3 delta = desired - transform.position;
        delta.y = 0f;
        verticalVelocity = -2f;

        if (characterController != null && characterController.enabled)
            characterController.Move(delta);
        else
            transform.position = new Vector3(desired.x, transform.position.y, desired.z);

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit groundHit, 1f, NavMesh.AllAreas))
        {
            float yAdjust = groundHit.position.y - transform.position.y;
            if (Mathf.Abs(yAdjust) > 0.001f)
            {
                if (characterController != null && characterController.enabled)
                    characterController.Move(new Vector3(0f, yAdjust, 0f));
                else
                    transform.position = new Vector3(transform.position.x, groundHit.position.y, transform.position.z);
            }
        }

        agent.nextPosition = transform.position;
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

    /// <summary>NavMesh 베이크 직후 호출해 스폰 위치를 바닥 NavMesh에 맞춥니다.</summary>
    public void AlignToNavMesh()
    {
        if (!TrySampleNavMeshPosition(transform.position, out NavMeshHit hit))
            return;

        DeactivateNavMeshAgent();

        bool ccWasEnabled = characterController != null && characterController.enabled;
        if (characterController != null)
            characterController.enabled = false;

        transform.position = hit.position;
        spawnPosition = hit.position;

        if (characterController != null)
            characterController.enabled = ccWasEnabled;
    }

    /// <summary>NavMeshAgent를 켜고 CharacterController와 역할을 분리합니다.</summary>
    public bool TryActivateNavMeshAgent(float speed, float stoppingDistance)
    {
        if (agent == null) return false;

        if (!TrySampleNavMeshPosition(transform.position, out NavMeshHit hit))
            return false;

        DeactivateNavMeshAgent();

        if (characterController != null)
            characterController.enabled = true;

        agent.enabled = true;
        agent.Warp(hit.position);

        if (!agent.isOnNavMesh)
        {
            DeactivateNavMeshAgent();
            return false;
        }

        agent.updatePosition = false;
        agent.updateRotation = true;
        agent.speed = speed;
        agent.acceleration = speed * 10f;
        agent.angularSpeed = 420f;
        agent.autoRepath = true;
        agent.stoppingDistance = stoppingDistance;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.isStopped = false;
        agent.nextPosition = transform.position;

        AllowTrapPathing();
        return true;
    }

    public void DeactivateNavMeshAgent()
    {
        if (agent == null) return;

        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        agent.enabled = false;
        trapPathCostApplied = false;

        if (characterController != null && stateMachine.CurrentState is not EnemyDeadState)
            characterController.enabled = true;
    }

    private static bool TrySampleNavMeshPosition(Vector3 worldPosition, out NavMeshHit hit)
    {
        return NavMesh.SamplePosition(worldPosition, out hit, NavMeshSampleRadius, NavMesh.AllAreas);
    }

    /// <summary>from에서 도달 가능한 NavMesh 위치를 찾습니다. 벽 너머로 스냅되는 것을 줄입니다.</summary>
    public Vector3 SampleReachableNavPoint(Vector3 from, Vector3 desired, float sampleRadius = -1f)
    {
        if (sampleRadius < 0f)
            sampleRadius = Mathf.Max(GridSystem.Instance.TileSize * 0.5f, 2f);

        if (!NavMesh.SamplePosition(from, out NavMeshHit fromHit, NavMeshSampleRadius, NavMesh.AllAreas))
            return from;

        if (!NavMesh.SamplePosition(desired, out NavMeshHit desiredHit, sampleRadius, NavMesh.AllAreas))
            return fromHit.position;

        if (IsNavMeshPathComplete(fromHit.position, desiredHit.position))
            return desiredHit.position;

        return fromHit.position;
    }

    public bool IsNavMeshPathComplete(Vector3 from, Vector3 to)
    {
        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path))
            return false;

        return path.status == NavMeshPathStatus.PathComplete;
    }

    /// <summary>이동 중인 NavMeshAgent 속도(벽 충돌 반영).</summary>
    public float GetNavMeshLocomotionSpeed()
    {
        if (agent == null || !agent.enabled) return 0f;

        Vector3 velocity = agent.desiredVelocity;
        velocity.y = 0f;
        if (velocity.sqrMagnitude > 0.01f)
            return velocity.magnitude;

        velocity = agent.velocity;
        velocity.y = 0f;
        return velocity.magnitude;
    }

    public bool IsNavMeshAgentMoving(float minSpeed = 0.08f)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        if (agent.pathPending) return false;
        if (agent.remainingDistance <= agent.stoppingDistance + 0.05f) return false;

        return GetNavMeshLocomotionSpeed() >= minSpeed;
    }

    /// <summary>월드 좌표를 NavMesh 위의 가장 가까운 지점으로 스냅합니다.</summary>
    public Vector3 GetNavMeshPointNear(Vector3 worldPosition, float sampleRadius = -1f)
    {
        if (sampleRadius < 0f)
            sampleRadius = NavMeshSampleRadius;

        return NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas)
            ? hit.position
            : worldPosition;
    }

    /// <summary>현재 위치에서 목표까지 NavMesh 경로를 계산해 이동할 다음 지점을 반환합니다.</summary>
    public bool TryGetNavMoveDestination(Vector3 worldTarget, out Vector3 destination)
    {
        destination = transform.position;

        if (!TrySampleNavMeshPosition(transform.position, out NavMeshHit fromHit))
            return false;

        destination = GetNavMeshPointNear(worldTarget);

        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(fromHit.position, destination, NavMesh.AllAreas, path))
            return false;

        if (path.corners.Length > 1)
            destination = path.corners[1];
        else if (path.corners.Length == 1)
            destination = path.corners[0];

        return path.status is NavMeshPathStatus.PathComplete or NavMeshPathStatus.PathPartial;
    }

    /// <summary>제자리에서 움직이지 않을 때 경로를 재계산하거나 우회 지점을 시도합니다.</summary>
    public bool TryRecoverFromMovementStuck(Vector3 worldTarget, ref float stuckTimer, ref Vector3 lastTrackedPosition)
    {
        Vector3 pos = transform.position;
        float movedSq = (pos - lastTrackedPosition).sqrMagnitude;
        lastTrackedPosition = pos;

        if (movedSq > 0.0025f || IsNavMeshAgentMoving())
        {
            stuckTimer = 0f;
            return false;
        }

        stuckTimer += Time.deltaTime;
        if (stuckTimer < 0.75f || agent == null || !agent.enabled || !agent.isOnNavMesh)
            return false;

        stuckTimer = 0f;
        agent.ResetPath();

        if (TryGetNavMoveDestination(worldTarget, out Vector3 repathDestination))
        {
            agent.SetDestination(repathDestination);
            return true;
        }

        Vector3 fallback = GetNavMeshPointNear(transform.position + transform.right * GridSystem.Instance.TileSize);
        if (TryGetNavMoveDestination(fallback, out Vector3 flankDestination))
        {
            agent.SetDestination(flankDestination);
            return true;
        }

        fallback = GetNavMeshPointNear(transform.position - transform.right * GridSystem.Instance.TileSize);
        if (TryGetNavMoveDestination(fallback, out flankDestination))
        {
            agent.SetDestination(flankDestination);
            return true;
        }

        return false;
    }

    /// <summary>순찰/추적처럼 NavMesh를 따라 이동 중인 상태인지.</summary>
    public bool IsLocomoting()
    {
        if (stateMachine.CurrentState is EnemyHitState) return false;

        EnemyBaseState state = stateMachine.CurrentState;
        return state is EnemyPatrolState or EnemyChaseState or EnemyAlertState;
    }

    private void FixedUpdate()
    {
        stateMachine.FixedUpdate();
    }

    // 플레이어의 MoveCharacter와 동일한 중력 및 이동 로직 (간단한 형태)
    public void MoveEnemy(Vector3 motion)
    {
        if (characterController == null || !characterController.enabled)
            return;

        // 구덩이 탈출 직후 짧은 시간 동안은 중력으로 다시 빠지지 않게 합니다.
        if (Time.time < pitRescueLockUntil)
        {
            verticalVelocity = -2f;
            motion.y = 0f;
            characterController.Move(motion);
            return;
        }

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

    /// <summary>
    /// 함정 NavMesh 영역 비용을 Walkable과 동일하게 맞춥니다.
    /// Agent가 NavMesh에 올라온 뒤 한 번 적용합니다 (Enable 직후 isOnNavMesh가 false일 수 있음).
    /// </summary>
    public void AllowTrapPathing()
    {
        TryApplyTrapPathCost(force: true);
    }

    private void TryApplyTrapPathCost(bool force = false)
    {
        if (agent == null || !agent.enabled)
        {
            trapPathCostApplied = false;
            return;
        }

        if (!force && trapPathCostApplied) return;
        if (trapAreaIndex < 0 || !agent.isOnNavMesh) return;

        agent.SetAreaCost(trapAreaIndex, trapPathCost);
        trapPathCostApplied = true;
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
        else
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
    }
}
