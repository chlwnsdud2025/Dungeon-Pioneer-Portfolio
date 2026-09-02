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

    /// <summary>
    /// 타격 구간 시작. 매 프레임 호출해도 안전합니다.
    ///
    /// ★ 피격 기록은 '꺼져 있다가 켜지는 순간'에만 비웁니다. 호출할 때마다 비우면 스윙당 1회 제한이
    ///   매 프레임 초기화되어, OnTriggerStay가 겹쳐 있는 적을 계속 때리게 됩니다.
    /// </summary>
    public void Begin(float damage, LayerMask targetLayers)
    {
        this.damage = damage;
        this.targetLayers = targetLayers;

        if (hitCollider == null) return;
        if (hitCollider.enabled) return;   // 이미 이번 스윙이 진행 중

        hitThisSwing.Clear();
        hitCollider.enabled = true;
    }

    /// <summary>타격 구간 종료.</summary>
    public void End()
    {
        if (hitCollider != null) hitCollider.enabled = false;
    }

    private void OnTriggerEnter(Collider other) => TryHit(other);

    // ★ Enter만으로는 놓치는 경우가 많습니다. 이 게임의 주 상황은 '적과 이미 겹친 채로 휘두르는 것'인데,
    //   콜라이더가 켜지는 순간 이미 캡슐 안에 들어와 있으면 '들어오는 순간'이 없어 Enter가 안 옵니다.
    //   적이 CharacterController만 갖고 있어 트리거 이벤트가 더 불안정한 것도 겹칩니다.
    //   Stay는 겹쳐 있는 동안 매 물리 스텝마다 오므로 그 구멍을 메웁니다.
    //   스윙당 1회 제한(hitThisSwing)이 있어 중복 타격은 생기지 않습니다.
    private void OnTriggerStay(Collider other) => TryHit(other);

    private void TryHit(Collider other)
    {
        if ((targetLayers.value & (1 << other.gameObject.layer)) == 0) return;

        // 콜라이더가 자식에 달려 있어도 본체를 찾도록 부모까지 훑는다.
        EnemyController enemy = other.GetComponentInParent<EnemyController>();
        if (enemy == null) return;

        if (!hitThisSwing.Add(enemy)) return;   // 이번 스윙에 이미 맞은 대상

        Transform attacker = GetComponentInParent<PlayerController>()?.transform;
        enemy.TakeDamage(damage, attacker);
    }
}
