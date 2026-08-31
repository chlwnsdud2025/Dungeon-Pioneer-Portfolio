using System.Collections.Generic;
using UnityEngine;

// RollingRockTrap이 오브젝트 풀에서 꺼내 굴리는 돌 하나.
// Launch()로 방향/속도를 받아 직선으로 굴러갑니다.
//
// [하이브리드 충돌 처리]
//  - 벽(wallLayer) 감지: SphereCast로 '예측' 감지 → 벽에 닿기 직전에 깔끔하게 사라짐(파고들지 않음).
//  - 피격 판정: 트리거 콜라이더 + OnTriggerEnter로 처리.
//    ★ 이 프리팹에는 [Collider(IsTrigger=true)] + [Kinematic Rigidbody]가 필요합니다.
//      (돌은 transform으로 이동하므로 Rigidbody는 반드시 Kinematic이어야 하며, Awake에서 강제로 맞춰줍니다.)
public class RollingRock : MonoBehaviour
{
    [Header("Roll Visual")]
    [Tooltip("굴러가는 시각 표현 대상(비우면 자기 자신). 이동 방향은 별도로 저장하므로 회전시켜도 경로에 영향 없음.")]
    public Transform visual;
    [Tooltip("돌 반지름. 벽 감지 반경 및 구르는 회전량 계산에 사용됩니다. (트리거 콜라이더 크기도 이에 맞추세요)")]
    public float rockRadius = 0.5f;

    [Header("Wall Collision")]
    [Tooltip("이 레이어에 닿으면 사라집니다(벽 레이어).")]
    public LayerMask wallLayer;
    [Tooltip("벽에 부딪히지 않아도 이 거리만큼 굴러가면 자동 회수(풀 고갈/맵 이탈 방지).")]
    public float maxTravelDistance = 30f;

    [Header("Damage")]
    [Tooltip("지나가며 주는 데미지. 0이면 데미지 없음. (플레이어는 넉백, 적은 일반 피격)")]
    public float damage = 20f;

    private Vector3 moveDirection;   // 월드 기준 진행 방향(발사 시 고정)
    private float speed;
    private RollingRockTrap owner;
    private bool active;
    private float traveled;
    private readonly HashSet<int> damagedIds = new HashSet<int>(); // 이번 발사에서 이미 때린 대상

    private void Awake()
    {
        // 트리거 피격 판정에는 Rigidbody가 필요하고, 이동은 transform으로 하므로 반드시 Kinematic이어야 합니다.
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning("[RollingRock] 트리거 피격 판정을 위해 Kinematic Rigidbody가 필요합니다. 프리팹에 추가해주세요.", this);
        }
        else
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    // RollingRockTrap.SpawnRock()에서 호출됩니다.
    public void Launch(Vector3 worldDir, float moveSpeed, RollingRockTrap pool)
    {
        moveDirection = worldDir.normalized;
        speed = moveSpeed;
        owner = pool;
        traveled = 0f;
        active = true;
        damagedIds.Clear();
    }

    private void Update()
    {
        if (!active) return;

        float step = speed * Time.deltaTime;

        // 1. 벽 감지: 진행 방향으로 돌 반경만큼 캐스트해서, 이번 프레임에 닿을 벽이 있으면 사라짐 (예측 → 벽 파고들지 않음)
        if (Physics.SphereCast(transform.position, rockRadius, moveDirection,
                out RaycastHit wallHit, step + rockRadius, wallLayer, QueryTriggerInteraction.Ignore))
        {
            Despawn();
            return;
        }

        // Pushable 솔리드 콜라이더에도 부딪히면 돌만 회수 (Pushable은 유지)
        if (Physics.SphereCast(transform.position, rockRadius, moveDirection,
                out RaycastHit pushHit, step + rockRadius, ~0, QueryTriggerInteraction.Ignore)
            && pushHit.collider != null
            && pushHit.collider.GetComponentInParent<PushableObject>() != null)
        {
            Despawn();
            return;
        }

        // 2. 이동
        transform.position += moveDirection * step;
        traveled += step;

        // 3. 시각적 굴림 (이동 방향을 축으로 회전)
        Transform v = visual != null ? visual : transform;
        if (rockRadius > 0.0001f)
        {
            Vector3 rollAxis = Vector3.Cross(Vector3.up, moveDirection);
            float deg = (step / rockRadius) * Mathf.Rad2Deg;
            v.Rotate(rollAxis, deg, Space.World);
        }

        // 4. 최대 이동 거리 초과 시 회수
        if (traveled >= maxTravelDistance) Despawn();
    }

    // 피격 판정: 트리거 콜라이더로 처리. 같은 대상은 이번 발사에서 한 번만 맞습니다.
    private void OnTriggerEnter(Collider other)
    {
        if (!active) return;

        // Pushable과 충돌하면 돌만 사라지고 Pushable은 유지됩니다.
        if (other.GetComponentInParent<PushableObject>() != null)
        {
            Despawn();
            return;
        }

        if (damage <= 0f) return;

        // 콜라이더가 자식에 있을 수도 있으므로 부모까지 탐색
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null && damagedIds.Add(player.GetInstanceID()))
        {
            // RollingRock에 맞으면 일반 피격이 아니라 '넉백(넘어짐)'으로 처리.
            // 굴러가는 방향(moveDirection)으로 밀리고, 넉백 동안 무적이라 재충돌은 무시됩니다.
            player.ApplyKnockback(damage, moveDirection);
            return;
        }

        EnemyController enemy = other.GetComponentInParent<EnemyController>();
        if (enemy != null && damagedIds.Add(enemy.GetInstanceID()))
        {
            enemy.TakeDamage(damage);
        }
    }

    private void Despawn()
    {
        active = false;
        if (owner != null) owner.ReturnToPool(this);
        else gameObject.SetActive(false);
    }
}
