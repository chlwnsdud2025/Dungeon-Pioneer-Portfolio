/// <summary>플레이어가 현재 있는 지역의 큰 분류.</summary>
public enum RegionType
{
    Town,       // 마을
    Overworld,  // 던전 밖 지형
    Dungeon     // 던전 내부
}

/// <summary>
/// 현재 지역을 알려주는 인터페이스.
/// 각 씬(마을/오버월드/던전)에 이 인터페이스를 구현한 컴포넌트를 하나 두고, 그 컴포넌트가 자신을
/// RegionState에 등록하면(SetCurrent), HUD 등 어디서든 RegionState.Region으로 현재 지역을 참조할 수 있습니다.
///
///  - 던전 씬: DungeonManager가 직접 구현 (Region = Dungeon)
///  - 오버월드/마을 씬: RegionMarker 컴포넌트를 배치 (인스펙터에서 지역 지정)
/// </summary>
public interface IRegionProvider
{
    RegionType Region { get; }
}
