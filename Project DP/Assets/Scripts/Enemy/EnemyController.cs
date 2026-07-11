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
    public LayerMask playerLayer;        // 플레이어 레이어
    public LayerMask obstacleLayer;      // 벽 등 시야를 가리는 장애물 레이어

    [HideInInspector] public Vector3 spawnPosition;

    [HideInInspector] public Transform target;
    [HideInInspector] public float lastAttackTime = -999f;

    [Range(0, 360)]
    public float viewAngle = 120f;

    [Header("Gravity Settings")]
    public float gravity = -9.81f;
    public float gravityMultiplier = 2f;
    [HideInInspector] public float verticalVelocity;

    [Header("Trap 회피/허용 (NavMesh Area Cost)")]
    [Tooltip("순찰 중 함정을 얼마나 꺼릴지 (클수록 회피). NavMesh Areas에 \"Trap\" 영역이 있어야 동작합니다.")]
    public float trapAvoidCost = 10f;
    [Tooltip("추격 중 함정을 지나갈 때의 비용 (평범한 바닥과 비슷하게 낮게 둠)")]
    public float trapAllowCost = 1f;

    private int trapAreaIndex;

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
        if (agent != null) agent.enabled = false;

        // TODO: 나중에 상태 클래스들을 만들면 여기서 AddState 해줍니다.
        stateMachine.AddState(new EnemyIdleState(this, stateMachine));
        stateMachine.AddState(new EnemyPatrolState(this, stateMachine));
        stateMachine.AddState(new EnemyChaseState(this, stateMachine));
        stateMachine.AddState(new EnemyAttackState(this, stateMachine));
        stateMachine.AddState(new EnemyHitState(this, stateMachine));
        stateMachine.AddState(new EnemyDeadState(this, stateMachine));
    }

    private void Start()
    {
        stateMachine.Initialize<EnemyIdleState>();
    }

    private void Update()
    {
        stateMachine.Update();
    }

    private void FixedUpdate()
    {
        stateMachine.FixedUpdate();
    }

    // 플레이어의 MoveCharacter와 동일한 중력 및 이동 로직 (간단한 형태)
    public void MoveEnemy(Vector3 motion)
    {
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
    /// true면 함정을 꺼려서 우회하고(순찰 중 기본), false면 평범한 바닥처럼 취급해서 밟고 지나갑니다(추격 중).
    /// NavMesh Areas 설정(Window > AI > Navigation > Areas)에 "Trap"이라는 이름의 영역이 있어야 동작합니다.
    /// </summary>
    public void SetTrapAvoidance(bool avoid)
    {
        // ★ SetAreaCost는 에이전트가 켜져 있고 실제로 NavMesh 위에 배치된 상태에서만 호출할 수 있습니다.
        //   (Awake 시점처럼 아직 꺼져있거나 NavMesh에 놓이기 전이면 예외가 발생하므로 반드시 가드해야 합니다.)
        if (trapAreaIndex < 0 || agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        agent.SetAreaCost(trapAreaIndex, avoid ? trapAvoidCost : trapAllowCost);
    }

    // 데미지 처리 (간이 버전)
    public void TakeDamage(float damage)
    {
        // 이미 죽은 상태(DeadState)라면 데미지를 무시
        if (stateMachine.CurrentState is EnemyDeadState) return;

        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            stateMachine.ChangeState<EnemyDeadState>();
        }
        else
        {
            stateMachine.ChangeState<EnemyHitState>();
        }
    }

    // 씬 뷰에서 인지 범위와 공격 범위를 확인하기 위한 디버그 드로잉
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
