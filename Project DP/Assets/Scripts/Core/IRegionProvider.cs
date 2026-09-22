public enum RegionType
{
    Town,       // 마을
    Overworld,  // 던전 밖 지형
    Dungeon     // 던전 내부
}

public interface IRegionProvider
{
    RegionType Region { get; }
}
