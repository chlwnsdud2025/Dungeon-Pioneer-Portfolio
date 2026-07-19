using System.Collections.Generic;
using UnityEngine;

// ArrowTrap이 오브젝트 풀에서 꺼내 발사하는 화살 하나.
// Launch()로 방향/속도를 받아 직선으로 날아가며, 벽(wallLayer)에 닿으면 풀로 반환됩니다.
// 이동/충돌은 캐스트(SphereCast/OverlapSphere)로 처리하므로 Rigidbody/Collider가 필요 없습니다.
// (RollingRock과 구조가 동일하되, 구르는 회전 연출이 없고 대상을 맞히면 사라지는 점만 다릅니다.)
public class Arrow : MonoBehaviour
{
    [Header("Collision")]
    [Tooltip("화살 충돌 반경(가늘게). 벽 감지 및 데미지 판정에 사용됩니다.")]
    public float hitRadius = 0.2f;
    [Tooltip("이 레이어에 닿으면 사라집니다(벽 레이어).")]
    public LayerMask wallLayer;
    [Tooltip("벽에 안 맞아도 이 거리만큼 날아가면 자동 회수(풀 고갈/맵 이탈 방지).")]
    public float maxTravelDistance = 30f;

    [Header("Damage")]
    [Tooltip("맞은 대상에게 주는 데미지. 0이면 데미지 없음.")]
    public float damage = 15f;
    [Tooltip("데미지 판정 대상 레이어(Player/Enemy).")]
    public LayerMask victimLayer = ~0;
    [Tooltip("true면 대상을 맞히는 즉시 사라집니다(관통 없음).")]
    public bool despawnOnHit = true;

    private Vector3 moveDirection;   // 월드 기준 진행 방향(발사 시 고정)
    private float speed;
    private ArrowTrap owner;
    private bool active;
    private float traveled;
    private readonly HashSet<int> damagedIds = new HashSet<int>(); // 이번 발사에서 이미 때린 대상

    // ArrowTrap.Fire()에서 호출됩니다.
    public void Launch(Vector3 worldDir, float moveSpeed, ArrowTrap pool)
    {
        moveDirection = worldDir.normalized;
        speed = moveSpeed;
        owner = pool;
        traveled = 0f;
        active = true;
        damagedIds.Clear();

        // [DEBUG] 이 로그가 뜨면 화살은 실제로 발사·활성화된 것. 이후 화면에 안 보이면 소멸/비주얼 문제.
        Debug.Log($"[Arrow] Launch: dir={moveDirection}, speed={speed}, pos={transform.position}", this);
    }

    private void Update()
    {
        if (!active) return;

        float step = speed * Time.deltaTime;

        // 1. 벽 감지: 진행 방향으로 캐스트해서, 이번 프레임에 닿을 벽이 있으면 사라짐
        if (Physics.SphereCast(transform.position, hitRadius, moveDirection,
                out RaycastHit wallHit, step + hitRadius, wallLayer, QueryTriggerInteraction.Ignore))
        {
            // [DEBUG] 발사 직후(traveled≈0)에 사라지면 Wall Layer에 바닥/발사기 자신 레이어가 포함된 것.
            Debug.Log($"[Arrow] 벽 감지로 소멸 (맞은 것={wallHit.collider.name}, 이동거리={traveled:F2})", this);
            Despawn();
            return;
        }

        // 2. 이동 (화살 메쉬는 발사 시 진행 방향으로 향하도록 회전되어 있으므로 별도 회전 불필요)
        transform.position += moveDirection * step;
        traveled += step;

        // 3. 데미지 판정
        if (damage > 0f)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, hitRadius, victimLayer);
            foreach (Collider col in hits)
            {
                PlayerController player = col.GetComponent<PlayerController>();
                if (player != null && damagedIds.Add(player.GetInstanceID()))
                {
                    player.TakeDamage(damage, transform);
                    if (despawnOnHit) { Despawn(); return; }
                    continue;
                }

                EnemyController enemy = col.GetComponent<EnemyController>();
                if (enemy != null && damagedIds.Add(enemy.GetInstanceID()))
                {
                    enemy.TakeDamage(damage);
                    if (despawnOnHit) { Despawn(); return; }
                }
            }
        }

        // 4. 최대 이동 거리 초과 시 회수
        if (traveled >= maxTravelDistance) Despawn();
    }

    private void Despawn()
    {
        active = false;
        if (owner != null) owner.ReturnToPool(this);
        else gameObject.SetActive(false);
    }
}
