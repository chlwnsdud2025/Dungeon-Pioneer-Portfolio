using System;

public enum LocationType
{
    Town,       // 마을 (추가 예정)
    Overworld,  // 던전 밖 지형 맵 (추가 예정)
    Dungeon     // 절차적으로 생성되는 미로형 던전 (층별로 index 사용)
}

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
