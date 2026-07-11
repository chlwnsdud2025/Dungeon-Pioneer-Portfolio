using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>
/// LocationType.Dungeon 장소를 만들어내는 provider.
/// 런 시작 시 등록된 맵 프리팹들의 순서를 한 번 섞어서 층마다 서로 다른 맵이 배정되도록 하고,
/// 실제 인스턴스 생성/좌표 정규화/스폰 지점 등록을 담당합니다.
///
/// 이 클래스는 "어떻게 던전을 만들지"만 알고, "지금 어느 장소에 있는지" "플레이어를 어디로 옮길지"는
/// 전혀 모릅니다 (그 책임은 WorldManager에 있습니다). 그래야 나중에 마을/오버월드가 추가돼도
/// 이 클래스나 WorldManager를 서로 건드리지 않고 StaticLocationProvider 하나만 새로 만들면 됩니다.
/// </summary>
public class DungeonGenerator : MonoBehaviour, ILocationProvider
{
    [Header("Dungeon Prefabs")]
    [Tooltip("에디터로 구워둔 맵 프리팹들을 배열에 넣어주세요. 런 하나당 중복 없이 배정되므로, " +
        "GameManager의 maxFloor보다 개수가 많거나 같아야 합니다.")]
    public GameObject[] mapPrefabs;

    private List<int> shuffledPrefabIndices;

    public bool CanProvide(LocationId id) => id.type == LocationType.Dungeon;

    /// <summary>런 시작(재시작 포함) 시 GameManager.StartRun에서 호출해서 층 배정을 새로 섞습니다.</summary>
    public void PrepareNewRun(int totalFloors)
    {
        if (mapPrefabs.Length < totalFloors)
        {
            Debug.LogWarning($"[DungeonGenerator] 등록된 맵 프리팹({mapPrefabs.Length}개)이 " +
                $"필요한 층 수({totalFloors})보다 적어 일부 층의 맵이 중복될 수 있습니다.");
        }

        shuffledPrefabIndices = new List<int>();
        for (int i = 0; i < mapPrefabs.Length; i++) shuffledPrefabIndices.Add(i);

        for (int i = shuffledPrefabIndices.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (shuffledPrefabIndices[i], shuffledPrefabIndices[j]) = (shuffledPrefabIndices[j], shuffledPrefabIndices[i]);
        }
    }

    public GameObject CreateLocation(LocationId id)
    {
        if (mapPrefabs.Length == 0)
        {
            Debug.LogError("맵 프리팹이 등록되지 않았습니다! 인스펙터를 확인해주세요.");
            return null;
        }

        int floor = id.index;
        int prefabIndex = (shuffledPrefabIndices != null && floor - 1 >= 0 && floor - 1 < shuffledPrefabIndices.Count)
            ? shuffledPrefabIndices[floor - 1]
            : Random.Range(0, mapPrefabs.Length); // 배정 목록보다 층이 많을 때의 안전장치 (중복 가능)

        GameObject spawnedMap = Instantiate(mapPrefabs[prefabIndex], Vector3.zero, Quaternion.identity);
        spawnedMap.name = $"Floor_{floor}_{mapPrefabs[prefabIndex].name}";

        // ★ MapBuilder로 에디터에서 손수 제작한 프리팹은 실제 타일들의 로컬 좌표가 (0,0,0)에서
        //   시작하지 않을 수 있습니다. 그 상태로 스폰하면 GridSystem 기준 좌표(WorldToCell)가 어긋나서
        //   미니맵/상호작용 판정 등이 맵의 일부만 인식하는 문제가 생기므로, 실제 타일들의 최소 좌표를
        //   찾아서 GridSystem의 셀 (0,0)에 정확히 맞도록 맵 전체를 이동시켜 정규화합니다.
        NormalizeMapOrigin(spawnedMap);

        LocationRoot root = spawnedMap.AddComponent<LocationRoot>();
        root.Initialize(id);
        RegisterEntranceExitSpawnPoints(spawnedMap, root);

        // ★ 파여있는 TrapTile은 주변 바닥과 높이 차이 때문에 NavMesh가 별개의 섬으로 구워집니다.
        //   Trap 타일을 "Trap" NavMesh 영역으로 표시해둡니다 (베이크 전에 붙어있어야 반영됨).
        //   실제로 다리(NavMeshLink)를 놓는 건 베이크된 표면 좌표가 필요해서 OnNavMeshBaked에서 합니다.
        TagTrapAreas(spawnedMap);

        Debug.Log($"<color=cyan>{floor}층 던전 생성 완료: {spawnedMap.name}</color>");

        return spawnedMap;
    }

    /// <summary>WorldManager가 NavMesh를 굽고 난 직후 호출됩니다. Trap 타일마다 벽이 아닌 인접 칸으로 다리를 놓습니다.</summary>
    public void OnNavMeshBaked(GameObject locationInstance)
    {
        CreateTrapNavMeshBridges(locationInstance);
    }

    /// <summary>맵 안의 모든 Trap 타일에 NavMeshModifier를 붙여 "Trap" NavMesh 영역으로 표시합니다.</summary>
    private void TagTrapAreas(GameObject mapInstance)
    {
        int trapArea = NavMesh.GetAreaFromName("Trap");
        if (trapArea < 0)
        {
            Debug.LogWarning("[DungeonGenerator] NavMesh Areas에 \"Trap\" 영역이 없습니다. " +
                "Window > AI > Navigation의 Areas 탭에서 빈 슬롯 이름을 \"Trap\"으로 설정해주세요.");
            return;
        }

        foreach (Transform child in mapInstance.GetComponentsInChildren<Transform>(true))
        {
            if (!child.CompareTag("Trap")) continue;

            NavMeshModifier modifier = child.GetComponent<NavMeshModifier>();
            if (modifier == null) modifier = child.gameObject.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = trapArea;
        }
    }

    /// <summary>
    /// 맵 안의 모든 Trap 타일에 대해, 벽이 아닌 상하좌우 인접 칸마다 NavMeshLink를 놓아
    /// 높이 차이로 끊긴 NavMesh를 이어줍니다. 타일 오브젝트의 피벗이나 그리드 계산으로 높이를
    /// 추측하지 않고, 각 타일 내부의 실제 모델링(자식 Renderer)의 바운즈 윗면을 직접 구해서 사용합니다.
    /// </summary>
    private void CreateTrapNavMeshBridges(GameObject mapInstance)
    {
        int trapArea = NavMesh.GetAreaFromName("Trap");
        if (trapArea < 0) return; // TagTrapAreas에서 이미 경고를 남겼으므로 여기선 조용히 종료

        float tileSize = GridSystem.Instance.TileSize;
        float sampleRadius = tileSize * 0.5f;
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

        var trapTiles = new List<Transform>();
        foreach (Transform child in mapInstance.GetComponentsInChildren<Transform>(true))
        {
            if (child.CompareTag("Trap")) trapTiles.Add(child);
        }

        foreach (Transform trap in trapTiles)
        {
            if (!TryGetTileTopSurface(trap, out Vector3 trapSurfaceSeed))
            {
                Debug.LogWarning($"[DungeonGenerator] {trap.name}에서 모델링(Renderer)을 찾지 못해 표면 높이를 계산할 수 없습니다.");
                continue;
            }

            if (!NavMesh.SamplePosition(trapSurfaceSeed, out NavMeshHit trapHit, sampleRadius, NavMesh.AllAreas))
            {
                Debug.LogWarning($"[DungeonGenerator] {trap.name}(표면 시드={trapSurfaceSeed}) 근처에서 " +
                    $"NavMesh 표면을 찾지 못해 다리를 만들지 못했습니다.");
                continue;
            }

            Vector3Int trapCell = GridSystem.Instance.WorldToCell(trap.position);

            foreach (Vector3 dir in directions)
            {
                Vector3Int neighborCell = trapCell + new Vector3Int(Mathf.RoundToInt(dir.x), 0, Mathf.RoundToInt(dir.z));
                if (!TryFindOpenNeighborTile(mapInstance, neighborCell, out Transform neighborTileObj)) continue; // 벽이거나 타일이 없으면 다리가 필요 없음
                if (neighborTileObj == null) continue;

                if (!TryGetTileTopSurface(neighborTileObj, out Vector3 neighborSurfaceSeed))
                {
                    Debug.LogWarning($"[DungeonGenerator] {neighborTileObj.name}에서 모델링(Renderer)을 찾지 못해 표면 높이를 계산할 수 없습니다.");
                    continue;
                }

                if (!NavMesh.SamplePosition(neighborSurfaceSeed, out NavMeshHit neighborHit, sampleRadius, NavMesh.AllAreas))
                {
                    Debug.LogWarning($"[DungeonGenerator] {trap.name}의 {dir} 방향 이웃(표면 시드={neighborSurfaceSeed}) " +
                        $"근처에서 NavMesh 표면을 찾지 못해 그쪽 다리를 만들지 못했습니다.");
                    continue;
                }

                GameObject bridgeObj = new GameObject($"TrapBridge_{trap.name}_{dir}");
                bridgeObj.transform.SetParent(mapInstance.transform);
                bridgeObj.transform.position = trapHit.position;

                NavMeshLink link = bridgeObj.AddComponent<NavMeshLink>();
                link.startPoint = Vector3.zero;
                link.endPoint = bridgeObj.transform.InverseTransformPoint(neighborHit.position);
                link.width = tileSize * 0.5f;
                link.bidirectional = true;
                link.area = trapArea;
            }
        }
    }

    /// <summary>
    /// 타일 오브젝트(빈 부모) 내부의 실제 모델링(자식 Renderer)을 찾아, 그 렌더링 바운즈의 윗면 중심
    /// 좌표를 반환합니다. 부모의 피벗이 어디에 있든 상관없이 "실제로 밟는 표면"을 정확히 구합니다.
    /// </summary>
    private bool TryGetTileTopSurface(Transform tile, out Vector3 topSurfacePoint)
    {
        Renderer renderer = tile.GetComponentInChildren<Renderer>();
        if (renderer == null)
        {
            topSurfacePoint = tile.position;
            return false;
        }

        Bounds bounds = renderer.bounds;
        topSurfacePoint = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        return true;
    }

    /// <summary>
    /// targetCell 칸에 Wall이 없고 실제 타일(Floor/Entrance/Exit/Chest/Trap/Pushable)이 있는지 확인합니다.
    /// 발견한 타일 오브젝트도 반환합니다(Floor/Trap을 우선으로 선택 - 바닥 높이에 있을 가능성이 가장 높음).
    /// </summary>
    private bool TryFindOpenNeighborTile(GameObject mapInstance, Vector3Int targetCell, out Transform tileObject)
    {
        tileObject = null;
        bool hasWall = false;
        bool hasTile = false;

        foreach (Transform child in mapInstance.GetComponentsInChildren<Transform>(true))
        {
            if (child == mapInstance.transform) continue;

            Vector3Int cell = GridSystem.Instance.WorldToCell(child.position);
            if (cell.x != targetCell.x || cell.z != targetCell.z) continue;

            // ★ 같은 칸에 Wall이 있으면 스캔 순서와 무관하게 무조건 막힘으로 취급
            if (child.CompareTag("Wall")) { hasWall = true; tileObject = null; break; }

            if (child.CompareTag("Floor") || child.CompareTag("Trap"))
            {
                hasTile = true;
                tileObject = child; // 바닥 높이에 있을 가능성이 가장 높은 태그를 우선으로 채택
            }
            else if (child.CompareTag("Entrance") || child.CompareTag("Exit") ||
                     child.CompareTag("Chest") || child.CompareTag("Pushable"))
            {
                hasTile = true;
                if (tileObject == null) tileObject = child; // Floor/Trap을 못 찾았을 때의 대안
            }
        }

        return hasTile && !hasWall;
    }

    private void NormalizeMapOrigin(GameObject mapInstance)
    {
        float minX = float.MaxValue;
        float minZ = float.MaxValue;
        bool foundAnyTile = false;

        foreach (Transform child in mapInstance.GetComponentsInChildren<Transform>())
        {
            if (child == mapInstance.transform) continue;

            // 맵을 구성하는 타일/오브젝트 태그 기준으로 실제 범위를 판단
            if (!(child.CompareTag("Floor") || child.CompareTag("Wall") || child.CompareTag("Entrance") ||
                  child.CompareTag("Exit") || child.CompareTag("Trap") || child.CompareTag("Chest") ||
                  child.CompareTag("Pushable")))
            {
                continue;
            }

            foundAnyTile = true;
            if (child.position.x < minX) minX = child.position.x;
            if (child.position.z < minZ) minZ = child.position.z;
        }

        if (!foundAnyTile) return;

        Vector3 origin = GridSystem.Instance.Origin;
        Vector3 offset = new Vector3(origin.x - minX, 0f, origin.z - minZ);

        if (offset.sqrMagnitude > 0.0001f)
        {
            Debug.Log($"<color=yellow>[DungeonGenerator] 맵 좌표를 GridSystem 원점에 맞춰 보정합니다. offset={offset}</color>");
            mapInstance.transform.position += offset;
        }
    }

    private void RegisterEntranceExitSpawnPoints(GameObject mapInstance, LocationRoot root)
    {
        foreach (Transform child in mapInstance.GetComponentsInChildren<Transform>(true))
        {
            if (child.CompareTag("Entrance")) root.RegisterSpawnPoint("Entrance", child);
            else if (child.CompareTag("Exit")) root.RegisterSpawnPoint("Exit", child);
        }
    }
}
