using System.Collections.Generic;
using UnityEngine;

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
    private PressurePlate sourcePlate;
    private bool active;
    private float traveled;
    private readonly HashSet<int> damagedIds = new HashSet<int>(); // 이번 발사에서 이미 때린 대상

    // ArrowTrap.Fire()에서 호출됩니다.
    public void Launch(Vector3 worldDir, float moveSpeed, ArrowTrap pool, PressurePlate plate = null)
    {
        moveDirection = worldDir.normalized;
        speed = moveSpeed;
        owner = pool;
        sourcePlate = plate;
        traveled = 0f;
        active = true;
        damagedIds.Clear();
    }

    private void Update()
    {
        if (!active) return;

        float step = speed * Time.deltaTime;

        // 1. 벽 감지: 진행 방향으로 캐스트해서, 이번 프레임에 닿을 벽이 있으면 사라짐
        if (Physics.SphereCast(transform.position, hitRadius, moveDirection,
                out RaycastHit wallHit, step + hitRadius, wallLayer, QueryTriggerInteraction.Ignore))
        {
            Despawn();
            return;
        }

        // 2. 이동 (화살 메쉬는 발사 시 진행 방향으로 향하도록 회전되어 있으므로 별도 회전 불필요)
        transform.position += moveDirection * step;
        traveled += step;

        // 3. 데미지 판정 (CharacterController는 Collider가 없어 OverlapSphere만으로는 맞지 않음)
        if (damage > 0f && TryDamageVictims())
            return;

        // 4. 최대 이동 거리 초과 시 회수
        if (traveled >= maxTravelDistance) Despawn();
    }

    private bool TryDamageVictims()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, hitRadius, victimLayer);
        foreach (Collider col in hits)
        {
            if (TryDamage(col.GetComponentInParent<PlayerController>(), col.GetComponentInParent<EnemyController>()))
                return despawnOnHit;
        }

        PlayerController player = PlayerManager.Instance != null ? PlayerManager.Instance.Controller : null;
        if (player != null && IsWithinCharacterController(transform.position, player, hitRadius))
        {
            if (damagedIds.Add(player.GetInstanceID()))
            {
                player.TakeDamage(damage, transform);
                if (despawnOnHit) { Despawn(); return true; }
            }
        }

        return false;
    }

    private bool TryDamage(PlayerController player, EnemyController enemy)
    {
        if (player != null && damagedIds.Add(player.GetInstanceID()))
        {
            player.TakeDamage(damage, transform);
            if (despawnOnHit) { Despawn(); return true; }
            return false;
        }

        if (enemy != null && damagedIds.Add(enemy.GetInstanceID()))
        {
            enemy.TakeDamage(damage);
            if (despawnOnHit) { Despawn(); return true; }
        }

        return false;
    }

    private static bool IsWithinCharacterController(Vector3 point, PlayerController player, float radius)
    {
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc == null) return false;

        Vector3 center = player.transform.position + cc.center;
        float verticalReach = cc.height * 0.5f;
        float dx = point.x - center.x;
        float dz = point.z - center.z;
        float horizontal = Mathf.Sqrt(dx * dx + dz * dz);
        float dy = Mathf.Abs(point.y - center.y);
        return horizontal <= cc.radius + radius && dy <= verticalReach + radius;
    }

    private void Despawn()
    {
        active = false;
        sourcePlate?.NotifyArrowResolved();
        sourcePlate = null;
        if (owner != null) owner.ReturnToPool(this);
        else gameObject.SetActive(false);
    }
}
