public static class RegionState
{
    public static IRegionProvider Current { get; private set; }

    /// <summary>지역 제공자를 현재 지역으로 등록합니다. (각 씬의 제공자가 Awake에서 호출)</summary>
    public static void SetCurrent(IRegionProvider provider)
    {
        Current = provider;
    }

    /// <summary>현재 지역. 아직 등록된 제공자가 없으면 null.</summary>
    public static RegionType? Region => Current != null ? Current.Region : (RegionType?)null;
}
