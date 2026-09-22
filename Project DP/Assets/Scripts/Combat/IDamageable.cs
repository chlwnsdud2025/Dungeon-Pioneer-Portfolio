using UnityEngine;

/// <summary>
/// 피해를 받을 수 있는 모든 것(플레이어, 적, 횃불 …)의 공통 인터페이스.
///
/// 때리는 쪽(WeaponHitbox, EnemyAttackState)은 콜라이더에서 GetComponentInParent&lt;IDamageable&gt;()로
/// 이 인터페이스만 찾아 TakeDamage를 부르고, 그 피해를 어떻게 해석할지는 맞는 쪽이 정합니다.
/// (플레이어는 방어·무적을 거쳐 체력을 깎고, 횃불은 남은 시간을 깎는 식)
/// 덕분에 새로 부술 수 있는 오브젝트가 생겨도 공격 코드는 손대지 않습니다.
/// </summary>
public interface IDamageable
{
    /// <summary>아직 피해를 받을 수 있는 상태인지. (죽었거나 이미 소멸했으면 false)</summary>
    bool IsAlive { get; }

    /// <param name="attacker">피해를 준 쪽. 방어 방향 판정·백스탭·타겟 전환에 쓰이며 null일 수 있습니다.</param>
    void TakeDamage(float damage, Transform attacker);
}
