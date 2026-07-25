using UnityEngine;

/// <summary>
/// 전용 매니저가 없는 씬(오버월드/마을)에 두는 지역 표식. 인스펙터에서 지역을 지정하고,
/// 씬이 로드되면 자신을 현재 지역(RegionState)으로 등록합니다.
/// (던전 씬은 DungeonManager가 직접 IRegionProvider를 구현하므로 이 컴포넌트가 필요 없습니다.)
///
/// 사용법: 오버월드 씬에 빈 오브젝트를 하나 만들고 이 컴포넌트를 붙인 뒤 region = Overworld로 설정.
///        마을 씬이 생기면 똑같이 두고 region = Town으로 설정.
/// </summary>
public class RegionMarker : MonoBehaviour, IRegionProvider
{
    [SerializeField] private RegionType region = RegionType.Overworld;

    public RegionType Region => region;

    private void Awake()
    {
        RegionState.SetCurrent(this);
    }
}
