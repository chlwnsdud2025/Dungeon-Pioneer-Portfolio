using UnityEngine;

/// <summary>
/// 특정 LocationId에 해당하는 장소를 실제로 만들어내는 역할을 하는 인터페이스.
/// 던전은 프리팹 중 하나를 무작위로(중복 없이) 골라 절차적으로 배치하고(DungeonGenerator),
/// 마을/오버월드처럼 고정된 장소는 미리 만들어둔 프리팹 하나를 그대로 생성하면 됩니다(StaticLocationProvider).
/// WorldManager는 이 인터페이스만 알고 있으면 되므로, 새로운 장소 종류가 추가돼도
/// WorldManager나 MinimapManager 쪽 코드를 건드릴 필요가 없습니다.
/// </summary>
public interface ILocationProvider
{
    /// <summary>이 provider가 해당 LocationId를 만들어낼 수 있는지 여부</summary>
    bool CanProvide(LocationId id);

    /// <summary>
    /// 해당 LocationId의 장소를 새로 생성합니다. 반환된 GameObject의 루트에는
    /// 반드시 LocationRoot 컴포넌트가 붙어 있어야 하며, 도착 지점(Entrance/Exit 등)이
    /// 스폰 포인트로 등록되어 있어야 합니다.
    /// </summary>
    GameObject CreateLocation(LocationId id);
}
