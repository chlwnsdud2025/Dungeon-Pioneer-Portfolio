using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class WeaponHitbox : MonoBehaviour
{
    private Collider hitCollider;

    // 한 번 휘두르는 동안 같은 대상을 여러 번 때리지 않도록 기억한다.
    private readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();

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
        // 적/횃불/그 밖의 부술 수 있는 것 모두 IDamageable로만 다룬다. 무엇을 맞힐지는 targetLayers가 정한다.
        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null || !damageable.IsAlive) return;

        if (!hitThisSwing.Add(damageable)) return;   // 이번 스윙에 이미 맞은 대상

        Transform attacker = GetComponentInParent<PlayerController>()?.transform;
        damageable.TakeDamage(damage, attacker);
    }
}
