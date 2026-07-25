using System;

/// <summary>
/// 장소의 큰 분류. 현재는 Dungeon만 실제로 사용됩니다(층 번호를 index로 구분).
/// </summary>
public enum LocationType
{
    Town,       // 마을 (추가 예정)
    Overworld,  // 던전 밖 지형 맵 (추가 예정)
    Dungeon     // 절차적으로 생성되는 미로형 던전 (층별로 index 사용)
}

/// <summary>
/// 플레이어가 있을 수 있는 "장소"를 식별하는 값 타입.
/// 마을/오버월드처럼 인스턴스가 하나뿐인 장소는 index를 0으로 고정하고,
/// 던전처럼 같은 타입 안에서도 여러 인스턴스(층)가 필요한 장소만 index로 구분합니다.
/// </summary>
[Serializable]
public struct LocationId : IEquatable<LocationId>
{
    public LocationType type;
    public int index;

    public LocationId(LocationType type, int index = 0)
    {
        this.type = type;
        this.index = index;
    }

    public bool Equals(LocationId other) => type == other.type && index == other.index;
    public override bool Equals(object obj) => obj is LocationId other && Equals(other);
    public override int GetHashCode() => (type, index).GetHashCode();
    public override string ToString() => index > 0 ? $"{type}_{index}" : type.ToString();

    public static bool operator ==(LocationId a, LocationId b) => a.Equals(b);
    public static bool operator !=(LocationId a, LocationId b) => !a.Equals(b);
}
