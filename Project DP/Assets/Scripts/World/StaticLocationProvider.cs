using UnityEngine;

/// <summary>
/// 마을/오버월드처럼 절차적 생성 없이 미리 만들어둔 프리팹 하나를 그대로 배치하는 provider.
/// 프리팹 루트 하위에 LocationConnector를 미리 배치해두면, 생성 시점에 각 커넥터가 자기 자신을
/// 스폰 지점으로 자동 등록합니다 (LocationConnector.Awake 참고).
///
/// 마을/오버월드는 인스턴스가 하나뿐이므로 WorldManager.TravelTo를 호출할 때 LocationId.index는
/// 항상 0을 사용하세요.
/// </summary>
public class StaticLocationProvider : MonoBehaviour, ILocationProvider
{
    [Tooltip("이 provider가 담당하는 장소 타입 (Town 또는 Overworld)")]
    public LocationType locationType;

    [Tooltip("고정 배치된 장소 프리팹.")]
    public GameObject locationPrefab;

    public bool CanProvide(LocationId id) => id.type == locationType;

    public GameObject CreateLocation(LocationId id)
    {
        if (locationPrefab == null)
        {
            Debug.LogError($"[StaticLocationProvider] {locationType}용 프리팹이 등록되지 않았습니다!");
            return null;
        }

        GameObject instance = Instantiate(locationPrefab, Vector3.zero, Quaternion.identity);
        instance.name = locationType.ToString();

        LocationRoot root = instance.AddComponent<LocationRoot>();
        root.Initialize(id);

        return instance;
    }
}
