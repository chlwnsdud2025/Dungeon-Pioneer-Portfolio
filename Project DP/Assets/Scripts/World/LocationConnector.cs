using UnityEngine;

/// <summary>
/// 장소와 장소를 잇는 범용 트리거. 마을 ↔ 오버월드 ↔ 던전 입구처럼,
/// 층 번호 증감이나 승리 조건과 무관하게 그냥 "다른 장소로 이동"만 하면 되는 연결에 사용합니다.
///
/// (던전 층 사이의 이동은 층 카운트/승리 판정이 얽혀 있어 ExitPoint/EntrancePoint가 GameManager를
///  거쳐서 처리합니다. 이 컴포넌트는 그 외의 장소 간 이동 전용입니다.)
/// </summary>
[RequireComponent(typeof(Collider))]
public class LocationConnector : MonoBehaviour
{
    [Header("이동할 목적지")]
    public LocationType destinationType;
    [Tooltip("Dungeon일 때만 의미 있음(층 번호). Town/Overworld는 0으로 두세요.")]
    public int destinationIndex = 0;

    [Tooltip("목적지 장소의 LocationRoot에 등록된 스폰 지점 ID (예: \"Entrance\", \"TownGate\")")]
    public string arrivalSpawnPointId;

    [Tooltip("이 커넥터 자기 자신도 하나의 스폰 지점입니다. 다른 장소에서 여기로 돌아올 때 쓸 ID를 지정하세요.")]
    public string selfSpawnPointId;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Awake()
    {
        LocationRoot root = GetComponentInParent<LocationRoot>();
        if (root != null && !string.IsNullOrEmpty(selfSpawnPointId))
        {
            root.RegisterSpawnPoint(selfSpawnPointId, transform);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (WorldManager.Instance != null)
        {
            WorldManager.Instance.TravelTo(new LocationId(destinationType, destinationIndex), arrivalSpawnPointId);
        }
        else
        {
            Debug.LogWarning("[LocationConnector] 씬에 WorldManager가 없어서 장소 이동을 처리할 수 없습니다.");
        }
    }
}
