using UnityEngine;

/// <summary>
/// 적이 "쫓아가서 때릴 수 있는 것"(플레이어, 횃불 …)의 공통 인터페이스.
///
/// 적의 상태 머신은 '무엇을 하는가'(추격/공격)만 알고 '누구를'은 이 인터페이스로 받습니다.
/// 그래서 대상이 늘어도 Chase/Attack 상태를 복제하지 않고, 각 대상이 자기가 어디에 있고
/// 어떻게 접근해야 하는지를 스스로 알려줍니다. 때릴 수 있어야 하므로 IDamageable을 포함합니다.
/// </summary>
public interface ITarget : IDamageable
{
    Transform Transform { get; }

    /// <summary>시야선(Raycast)과 타격 판정이 겨눌 지점. 벽에 붙은 대상은 벽에서 살짝 띄운 점을 줍니다.</summary>
    Vector3 AimPoint { get; }

    /// <summary>때리려면 서 있어야 할 셀. 벽 안에 있는 대상은 자기 앞의 바닥 셀을 줍니다.</summary>
    Vector2Int ApproachCell { get; }

    /// <summary>
    /// 움직이는 대상인지. true면 따라가며 경로를 다시 찾고 구덩이도 밟고 지나가며,
    /// false면 ApproachCell을 고정 목적지로 삼습니다.
    /// </summary>
    bool IsMobile { get; }
}
