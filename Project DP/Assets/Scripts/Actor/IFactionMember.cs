using UnityEngine;

/// <summary>
/// 진영을 가진 액터. 플레이어·적·NPC가 구현합니다.
///
/// Transform을 함께 요구하는 이유: 진영 검사를 통과한 뒤 TakeDamage에 넘길 '공격자'가 필요한데,
/// 이걸 얻으려고 구현체를 Component로 캐스팅하는 코드가 여기저기 생기는 것을 막습니다.
/// PlayerController는 ITarget으로, EnemyController는 IGridAgent로 이미 Transform을 노출하고 있어
/// 추가 비용이 0입니다.
///
/// Overrides를 인터페이스에 둔 이유: GetComponentInParent를 판정 때마다 부르면
/// 탐지 루프에서 매 프레임 수십 번 호출됩니다. 구현체가 Awake에서 한 번 캐시하면 조회가 O(1)이 됩니다.
/// </summary>
public interface IFactionMember
{
    FactionId Faction { get; }
    Transform Transform { get; }

    /// <summary>개체 단위 진영 예외. 붙어 있지 않으면 null이며, 그 경우 기본 관계표만 씁니다.</summary>
    FactionOverride Overrides { get; }
}
