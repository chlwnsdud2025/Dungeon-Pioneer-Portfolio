using UnityEngine;

public class RegionMarker : MonoBehaviour, IRegionProvider
{
    [SerializeField] private RegionType region = RegionType.Overworld;

    public RegionType Region => region;

    private void Awake()
    {
        RegionState.SetCurrent(this);
    }
}
