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

    [Header("Aggro & Combat Settings")]
    public float detectionRadius = 7f;   // 플레이어 인지 범위
    public float attackRange = 1.5f;     // 공격 사거리
    public float attackCooldown = 2f;    // 공격 쿨타임
    public LayerMask playerLayer;        // 플레이어 레이어
    public LayerMask obstacleLayer;      // 벽 등 시야를 가리는 장애물 레이어

    [HideInInspector] public Transform target;
    [HideInInspector] public float lastAttackTime = -999f;

    [Range(0, 360)]
    public float viewAngle = 120f;

    [Header("Gravity Settings")]
    public float gravity = -9.81f;
    public float gravityMultiplier = 2f;
    [HideInInspector] public float verticalVelocity;

    private void Awake()
    {
        stateMachine = new EnemyStateMachine();
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        currentHealth = maxHealth;

        // TODO: 나중에 상태 클래스들을 만들면 여기서 AddState 해줍니다.
        stateMachine.AddState(new EnemyIdleState(this, stateMachine));
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