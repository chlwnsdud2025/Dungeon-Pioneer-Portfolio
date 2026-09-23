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

    // 이 무기를 휘두르는 액터. 진영 판정과 TakeDamage의 '공격자' 인자에 함께 씁니다.
    private IFactionMember owner;

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

        // Awake가 아니라 여기서 찾는 이유: 무기는 Instantiate 후 손 소켓에 붙는 물건이라
        // Awake 시점의 부모가 최종 부모가 아닐 수 있습니다. 스윙 시작 시점에는 계층이 확정돼 있습니다.
        // 캐시해 두지 않고 스윙마다 다시 찾는 이유: 인터페이스로 들고 있는 참조는 대상이 파괴돼도
        // C# null이 되지 않아(Unity의 == 오버로드를 타지 않음) 낡은 참조를 붙들게 됩니다.
        // 스윙당 한 번이라 비용은 무시할 수준입니다.
        owner = GetComponentInParent<IFactionMember>();

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

        // 진영 검사는 '진영을 가진 액터'에게만 적용한다. 횃불·부술 수 있는 오브젝트처럼
        // 진영이 없는 대상은 예전처럼 targetLayers만으로 판정한다.
        // owner가 null인 무기(소유자 없는 함정 칼날 등)도 검사를 건너뛴다.
        if (owner != null && damageable is IFactionMember targetMember
            && !FactionTable.IsHostile(owner, targetMember))
            return;

        if (!hitThisSwing.Add(damageable)) return;   // 이번 스윙에 이미 맞은 대상

        damageable.TakeDamage(damage, owner?.Transform);
    }
}
