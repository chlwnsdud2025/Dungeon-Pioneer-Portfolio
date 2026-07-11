using System.Collections.Generic;
using UnityEngine;

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

        Debug.Log($"<color=cyan>{floor}층 던전 생성 완료: {spawnedMap.name}</color>");

        return spawnedMap;
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
