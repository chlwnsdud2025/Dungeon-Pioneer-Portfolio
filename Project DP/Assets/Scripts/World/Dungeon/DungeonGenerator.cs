using System.Collections.Generic;
using UnityEngine;

public class DungeonGenerator : MonoBehaviour
{
    [Header("Dungeon Prefabs")]
    [Tooltip("에디터로 구워둔 맵 프리팹들을 배열에 넣어주세요. 런 하나당 중복 없이 배정되므로, " +
        "GameManager의 maxFloor보다 개수가 많거나 같아야 합니다.")]
    public GameObject[] mapPrefabs;

    // ── 테스트용 임시 옵션 ────────────────────────────────────────
    // 제거할 때는 아래 3곳만 지우면 됩니다:
    //   ① 이 블록  ② PrepareNewRun의 FixFirstFloorPrefabForTest() 호출 1줄  ③ 그 헬퍼 메서드
    [Header("테스트용 (커밋 전 반드시 OFF)")]
    [Tooltip("켜면 1층 맵을 아래 인덱스의 프리팹으로 고정합니다. 2층부터는 평소대로 셔플됩니다.")]
    public bool fixFirstFloorPrefab = false;
    [Tooltip("1층에 고정할 mapPrefabs의 인덱스. 0이 첫 번째 프리팹입니다.")]
    public int fixedFirstFloorPrefabIndex = 0;
    // ─────────────────────────────────────────────────────────────

    private List<int> shuffledPrefabIndices;

    /// <summary>런 시작(재시작 포함) 시 DungeonManager.StartRun에서 호출해서 층 배정을 새로 섞습니다.</summary>
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

        FixFirstFloorPrefabForTest();
    }

    /// <summary>
    /// 테스트용. 셔플 결과에서 지정한 프리팹을 1층 자리로 끌어옵니다.
    /// 자리를 '교환'하므로 전순열이 유지되어, 런 안에서 맵이 중복되지 않는 성질은 그대로입니다.
    /// </summary>
    private void FixFirstFloorPrefabForTest()
    {
        if (!fixFirstFloorPrefab) return;
        if (shuffledPrefabIndices == null || shuffledPrefabIndices.Count == 0) return;

        if (fixedFirstFloorPrefabIndex < 0 || fixedFirstFloorPrefabIndex >= mapPrefabs.Length)
        {
            Debug.LogWarning($"[DungeonGenerator] 테스트 옵션이 켜져 있지만 고정 인덱스({fixedFirstFloorPrefabIndex})가 " +
                $"mapPrefabs 범위(0~{mapPrefabs.Length - 1})를 벗어나 무시합니다.");
            return;
        }

        int slot = shuffledPrefabIndices.IndexOf(fixedFirstFloorPrefabIndex);
        if (slot < 0) return;

        (shuffledPrefabIndices[0], shuffledPrefabIndices[slot]) =
            (shuffledPrefabIndices[slot], shuffledPrefabIndices[0]);

        GameObject fixedPrefab = mapPrefabs[fixedFirstFloorPrefabIndex];
        string mapName = fixedPrefab != null ? fixedPrefab.name : "(비어 있음)";
        Debug.LogWarning($"[DungeonGenerator] 테스트 옵션 ON — 1층을 '{mapName}'" +
            $"(인덱스 {fixedFirstFloorPrefabIndex})으로 고정했습니다. 랜덤 배정이 아닙니다.");
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
        root.SetMapId(mapPrefabs[prefabIndex].name);   // 상호작용 기록을 묶을 키
        RegisterEntranceExitSpawnPoints(spawnedMap, root);

        // ★ 좌표 정규화가 끝난 '뒤에' 각 오브젝트가 자기 셀을 확정합니다.
        //   Awake에서 읽으면 NormalizeMapOrigin 이전 좌표가 잡혀 식별자가 어긋납니다.
        foreach (IFloorPersistent persistent in spawnedMap.GetComponentsInChildren<IFloorPersistent>(true))
        {
            persistent.Latch();
        }

        root.SetGrid(LocationGridBuilder.Build(spawnedMap));

        SnapPushablesToGround(spawnedMap);

        // 발판 ↔ 화살 함정 연결 (프리팹 수동 참조가 런타임에서 null이 되는 경우 대비)
        ArrowTrapRuntimeWire.WireMap(spawnedMap, onlyIfMissing: false);

        Debug.Log($"<color=cyan>{floor}층 던전 생성 완료: {spawnedMap.name}</color>");

        return spawnedMap;
    }

    private void NormalizeMapOrigin(GameObject mapInstance)
    {
        float minX = float.MaxValue;
        float minZ = float.MaxValue;
        bool foundAnyTile = false;
        Transform mapRoot = mapInstance.transform;

        // 장식 자식/오프셋 타일(≈0.97)이 원점을 오염시키지 않도록 타일 루트만 사용합니다.
        foreach (Transform child in mapInstance.GetComponentsInChildren<Transform>(true))
        {
            if (!LocationGrid.IsTileRoot(child, mapRoot)) continue;
            // 원점은 바닥 계열만 — Wall 돌출이 min을 밀지 않게 합니다.
            if (!(child.CompareTag("Floor") || child.CompareTag("Entrance") ||
                  child.CompareTag("Exit") || child.CompareTag("Trap")))
                continue;

            foundAnyTile = true;
            if (child.position.x < minX) minX = child.position.x;
            if (child.position.z < minZ) minZ = child.position.z;
        }

        if (!foundAnyTile) return;

        // 미세 오차(0.97 등)를 제거하고 타일 격자에 스냅
        minX = SnapToTileGrid(minX);
        minZ = SnapToTileGrid(minZ);

        Vector3 origin = GridSystem.Instance.Origin;
        Vector3 offset = new Vector3(origin.x - minX, 0f, origin.z - minZ);

        if (offset.sqrMagnitude > 0.0001f)
        {
            Debug.Log($"<color=yellow>[DungeonGenerator] 맵 좌표를 GridSystem 원점에 맞춰 보정합니다. offset={offset}</color>");

            CharacterController[] controllers = mapInstance.GetComponentsInChildren<CharacterController>(true);
            bool[] wasEnabled = new bool[controllers.Length];
            for (int i = 0; i < controllers.Length; i++)
            {
                wasEnabled[i] = controllers[i].enabled;
                controllers[i].enabled = false;
            }

            mapInstance.transform.position += offset;

            for (int i = 0; i < controllers.Length; i++)
                controllers[i].enabled = wasEnabled[i];
        }
    }

    /// <summary>맵 원점을 타일 격자(2 단위)에 맞춥니다. 장식 오브젝트의 미세 오차를 흡수합니다.</summary>
    private static float SnapToTileGrid(float value)
    {
        float tile = GridSystem.Instance.TileSize;
        return Mathf.Round(value / tile) * tile;
    }

    /// <summary>바닥 레이어 불일치로 Pushable이 맵 아래로 빠지지 않도록 스폰 직후 바닥에 붙입니다.</summary>
    private static void SnapPushablesToGround(GameObject mapInstance)
    {
        foreach (PushableObject pushable in mapInstance.GetComponentsInChildren<PushableObject>(true))
            pushable.SnapToGround();
    }

    private void RegisterEntranceExitSpawnPoints(GameObject mapInstance, LocationRoot root)
    {
        foreach (Transform child in mapInstance.GetComponentsInChildren<Transform>(true))
        {
            if (!LocationGrid.IsTileRoot(child, mapInstance.transform)) continue;
            if (child.CompareTag("Entrance")) root.RegisterSpawnPoint("Entrance", child);
            else if (child.CompareTag("Exit")) root.RegisterSpawnPoint("Exit", child);
        }
    }
}
