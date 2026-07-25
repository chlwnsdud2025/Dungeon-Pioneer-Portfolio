/// <summary>
/// 지금 활성 씬의 지역 제공자(IRegionProvider)를 가리키는 전역 접근점.
/// 각 지역 제공자가 Awake에서 SetCurrent로 자신을 등록하고, 씬이 바뀌면 새 제공자가 자신으로 덮어씁니다.
/// 그래서 전역 상주하는 HUD 같은 곳에서도 RegionState.Region 하나로 "지금 어느 지역인지"를 알 수 있습니다.
/// </summary>
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
