using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

/// <summary>
/// "지금 플레이어가 어느 장소에 있는가"와 "장소 사이를 어떻게 이동하는가"를 총괄하는 매니저.
///
/// 한 번 만들어진 장소(던전 층, 마을, 오버월드)는 절대 파괴하지 않고 Dictionary에 보관해뒀다가
/// SetActive로만 켜고 끕니다. 이렇게 하면 비활성화된 동안 적 AI/애니메이션이 멈추고, 체력/위치/
/// 열린 상자/설치된 횃불 같은 필드 값들이 그대로 남아있기 때문에 "탐색 상태 저장"이 별도 저장 로직
/// 없이 자연스럽게 이루어집니다.
///
/// 플레이어 역시 런(Run) 전체에서 단 하나의 인스턴스만 유지하고, 장소 이동 시 위치만 옮깁니다.
///
/// 실제로 각 장소를 만들어내는 방법(절차적 던전 / 고정 프리팹)은 ILocationProvider 구현체에 위임하므로,
/// 마을/오버월드가 추가돼도 이 클래스는 수정할 필요가 없습니다(townProvider/overworldProvider만 연결).
/// </summary>
public class WorldManager : MonoBehaviour
{
    public static WorldManager Instance { get; private set; }

    [Header("Providers (ILocationProvider 구현체를 드래그하세요)")]
    [Tooltip("던전(미로형) 장소를 만들어내는 provider. 씬의 DungeonGenerator를 연결하세요.")]
    public MonoBehaviour dungeonProvider;
    [Tooltip("마을 장소를 만들어내는 provider. 아직 없다면 비워두세요.")]
    public MonoBehaviour townProvider;
    [Tooltip("던전 밖 지형(오버월드) 장소를 만들어내는 provider. 아직 없다면 비워두세요.")]
    public MonoBehaviour overworldProvider;

    [Header("Player")]
    public GameObject playerPrefab;

    [Header("AI Navigation")]
    [Tooltip("현재 활성화된 장소 기준으로 장소를 옮길 때마다 다시 굽습니다. 비활성 오브젝트는 자동으로 베이킹 대상에서 제외됩니다.")]
    public NavMeshSurface navMeshSurface;

    private readonly Dictionary<LocationId, GameObject> loadedLocations = new Dictionary<LocationId, GameObject>();
    private GameObject persistentPlayer;
    private LocationId currentLocationId;
    private bool hasCurrentLocation;

    public GameObject CurrentLocationInstance =>
        hasCurrentLocation && loadedLocations.TryGetValue(currentLocationId, out GameObject go) ? go : null;
    public GameObject CurrentPlayer => persistentPlayer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>새 런을 시작(재시작 포함)할 때 호출. 기존에 로드된 모든 장소와 플레이어를 파괴합니다.</summary>
    public void ResetWorld()
    {
        foreach (GameObject loc in loadedLocations.Values)
        {
            if (loc != null) Destroy(loc);
        }
        loadedLocations.Clear();
        hasCurrentLocation = false;

        if (persistentPlayer != null)
        {
            Destroy(persistentPlayer);
            persistentPlayer = null;
        }

        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.ResetAllLocations();
        }
    }

    /// <summary>지정한 장소로 이동합니다. 처음 방문하면 새로 생성하고, 재방문이면 저장된 상태를 그대로 복원합니다.</summary>
    public void TravelTo(LocationId destination, string arrivalSpawnPointId)
    {
        foreach (KeyValuePair<LocationId, GameObject> kvp in loadedLocations)
        {
            if (kvp.Value != null) kvp.Value.SetActive(kvp.Key.Equals(destination));
        }

        bool isNewLocation = !loadedLocations.TryGetValue(destination, out GameObject instance);

        if (isNewLocation)
        {
            ILocationProvider provider = GetProvider(destination);
            if (provider == null)
            {
                Debug.LogError($"[WorldManager] {destination}을(를) 생성할 provider가 연결되어 있지 않습니다.");
                return;
            }

            instance = provider.CreateLocation(destination);
            loadedLocations[destination] = instance;
        }
        else
        {
            instance.SetActive(true);
        }

        currentLocationId = destination;
        hasCurrentLocation = true;

        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
        }

        PlacePlayer(instance, arrivalSpawnPointId);

        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.SwitchLocation(destination, instance, isNewLocation, persistentPlayer.transform);
        }
    }

    private ILocationProvider GetProvider(LocationId id)
    {
        MonoBehaviour provider = id.type switch
        {
            LocationType.Dungeon => dungeonProvider,
            LocationType.Town => townProvider,
            LocationType.Overworld => overworldProvider,
            _ => null
        };

        return provider as ILocationProvider;
    }

    private void PlacePlayer(GameObject locationInstance, string spawnPointId)
    {
        LocationRoot root = locationInstance != null ? locationInstance.GetComponent<LocationRoot>() : null;
        Transform spawnPoint = root != null ? root.GetSpawnPoint(spawnPointId) : null;

        Vector3 spawnPosition;
        if (spawnPoint != null)
        {
            // ★ Entrance/Exit 타일 위에 그대로 스폰하면 그 타일의 트리거 콜라이더(ExitPoint/EntrancePoint)와
            //   곧바로 겹쳐서 도착하자마자 다시 반대 방향으로 이동해버립니다. 타일 자체가 아니라
            //   그 주변 4방향 중 실제로 타일이 있고 벽이 없는 칸 위에 스폰합니다.
            spawnPosition = GetSpawnPositionNearTile(locationInstance, spawnPoint.position);
        }
        else
        {
            Debug.LogWarning($"[WorldManager] '{spawnPointId}' 스폰 지점을 찾을 수 없어 맵 원점 근처에 강제 스폰합니다.");
            spawnPosition = (locationInstance != null ? locationInstance.transform.position : Vector3.zero) + Vector3.up * 1.5f;
        }

        if (persistentPlayer == null)
        {
            persistentPlayer = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            // ★ CharacterController가 켜진 채로 transform.position을 직접 바꾸면 충돌 보정이 끼어들 수 있어
            //   순간이동 직전에 잠깐 꺼줍니다.
            CharacterController cc = persistentPlayer.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            persistentPlayer.transform.position = spawnPosition;

            if (cc != null) cc.enabled = true;
        }

        CameraFollow camFollow = Camera.main.GetComponent<CameraFollow>();
        if (camFollow != null)
        {
            camFollow.target = persistentPlayer.transform;
            Camera.main.transform.position = persistentPlayer.transform.position + camFollow.offset;
        }
    }

    /// <summary>
    /// tilePosition(Entrance/Exit 타일 자체)의 상하좌우 4방향 중, 실제로 타일이 있고(Wall이 아닌 태그가 있고)
    /// 그 위에 벽이 없는 칸을 찾아 그 위의 스폰 좌표를 반환합니다. 타일 자체 위에 스폰시키면 그 타일의
    /// 트리거 콜라이더와 바로 겹쳐서 도착하자마자 다시 반대 방향으로 이동해버리기 때문입니다.
    /// </summary>
    private Vector3 GetSpawnPositionNearTile(GameObject locationInstance, Vector3 tilePosition)
    {
        float tileSize = GridSystem.Instance.TileSize;
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

        foreach (Vector3 dir in directions)
        {
            Vector3 candidate = tilePosition + dir * tileSize;
            if (IsWalkableTile(locationInstance, candidate))
            {
                return candidate + Vector3.up * 1.5f;
            }
        }

        // 4방향 모두 실패하면(사방이 막힌 예외적인 경우) 타일 자체 위치라도 반환
        Debug.LogWarning("[WorldManager] 스폰 타일 주변 4방향에서 걸어갈 수 있는 타일을 찾지 못해 타일 위에 그대로 스폰합니다.");
        return tilePosition + Vector3.up * 1.5f;
    }

    /// <summary>
    /// worldPos가 속한 그리드 칸(x, z만 비교. Wall/Entrance/Exit는 바닥보다 한 칸 높게 배치되므로 y는 무시)에
    /// Wall 태그 오브젝트가 없고, 실제로 타일(Floor/Entrance/Exit/Trap/Chest/Pushable)이 존재하는지 확인합니다.
    /// </summary>
    private bool IsWalkableTile(GameObject locationInstance, Vector3 worldPos)
    {
        if (locationInstance == null) return false;

        Vector3Int cell = GridSystem.Instance.WorldToCell(worldPos);
        bool hasWall = false;
        bool hasTile = false;

        foreach (Transform child in locationInstance.GetComponentsInChildren<Transform>(true))
        {
            if (child == locationInstance.transform) continue;

            Vector3Int childCell = GridSystem.Instance.WorldToCell(child.position);
            if (childCell.x != cell.x || childCell.z != cell.z) continue;

            if (child.CompareTag("Wall"))
            {
                hasWall = true;
                break; // 벽이 확인되면 이 칸은 무조건 스폰 불가
            }

            if (child.CompareTag("Floor") || child.CompareTag("Entrance") || child.CompareTag("Exit") ||
                child.CompareTag("Trap") || child.CompareTag("Chest") || child.CompareTag("Pushable"))
            {
                hasTile = true;
            }
        }

        return hasTile && !hasWall;
    }
}
