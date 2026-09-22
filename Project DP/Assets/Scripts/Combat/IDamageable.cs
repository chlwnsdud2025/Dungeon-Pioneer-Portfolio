using UnityEngine;

public interface IDamageable
{
    /// <summary>아직 피해를 받을 수 있는 상태인지. (죽었거나 이미 소멸했으면 false)</summary>
    bool IsAlive { get; }

    /// <param name="attacker">피해를 준 쪽. 방어 방향 판정·백스탭·타겟 전환에 쓰이며 null일 수 있습니다.</param>
    void TakeDamage(float damage, Transform attacker);
}
