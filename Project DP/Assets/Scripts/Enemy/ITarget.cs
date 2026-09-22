using UnityEngine;

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
