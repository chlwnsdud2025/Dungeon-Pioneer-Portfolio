using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기 프리팹에 붙는 타격 판정 콜라이더.
/// 평소에는 꺼져 있고, 공격 모션의 타격 구간에만 켜집니다(AttackState_Player가 Begin/End 호출).
///
/// ★ Rigidbody가 필요한 이유: 트리거 이벤트는 두 콜라이더 중 최소 하나에 Rigidbody가 있어야 발생합니다.
///   히트박스는 플레이어의 자식이라 Rigidbody가 없으면 PhysX가 '정적 콜라이더'로 취급하고,
///   정적 트리거가 가만히 서 있는 적을 지나갈 때는 enter 이벤트가 안정적으로 발생하지 않습니다
///   (= 검을 휘둘러도 안 맞는 증상). isKinematic이라 플레이어 이동에는 영향이 없습니다.
///
/// 데미지와 대상 레이어는 휘두르는 쪽이 넘겨주므로 무기 프리팹에는 따로 설정할 값이 없습니다.
/// 그래서 검 여러 자루에 같은 수치를 맞춰둘 필요가 없고, 나중에 적이 무기를 들어도 그대로 재사용됩니다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class WeaponHitbox : MonoBehaviour
{
    private Collider hitCollider;

    // 한 번 휘두르는 동안 같은 대상을 여러 번 때리지 않도록 기억한다.
    private readonly HashSet<EnemyController> hitThisSwing = new HashSet<EnemyController>();

    private float damage;
    private LayerMask targetLayers;

    /// <summary>지금 타격 판정이 열려 있는지.</summary>
    public bool IsActive => hitCollider != null && hitCollider.enabled;

    private void Awake()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        hitCollider = GetComponent<Collider>();
        if (hitCollider == null)
        {
            Debug.LogError($"[WeaponHitbox] '{name}'에 Collider가 없어 타격 판정이 동작하지 않습니다. " +
                           "BoxCollider 등을 추가하고 칼날 부분에 맞춰주세요.", this);
            return;
        }

        hitCollider.isTrigger = true;
        hitCollider.enabled = false;   // 평소에는 꺼둔다
    }

    /// <summary>타격 구간 시작. 이번 스윙의 피격 기록을 비우고 콜라이더를 켠다.</summary>
    public void Begin(float damage, LayerMask targetLayers)
    {
        this.damage = damage;
        this.targetLayers = targetLayers;
        hitThisSwing.Clear();

        if (hitCollider != null) hitCollider.enabled = true;
    }

    /// <summary>타격 구간 종료.</summary>
    public void End()
    {
        if (hitCollider != null) hitCollider.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if ((targetLayers.value & (1 << other.gameObject.layer)) == 0) return;

        // 콜라이더가 자식에 달려 있어도 본체를 찾도록 부모까지 훑는다.
        EnemyController enemy = other.GetComponentInParent<EnemyController>();
        if (enemy == null) return;

        if (!hitThisSwing.Add(enemy)) return;   // 이번 스윙에 이미 맞은 대상

        enemy.TakeDamage(damage);
    }
}
