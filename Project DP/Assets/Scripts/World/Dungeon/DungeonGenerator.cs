using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>
/// 던전 층 프리팹을 만들어내는 생성기. DungeonManager가 소유하며 다음 3가지를 제공합니다:
///  - PrepareNewRun: 런 시작 시 맵 프리팹 순서를 한 번 섞어 층마다 다른 맵이 배정되게 함
///  - CreateLocation: 해당 층 프리팹을 인스턴스화 + 좌표 정규화 + 스폰 지점 등록 + Trap 영역 태깅
///  - OnNavMeshBaked: Trap NavWalkSurface 렌더러 숨김 (베이크용 투명 메시)
/// "어떻게 던전을 만들지"만 알고, "지금 어느 층인지/플레이어를 어디로 옮길지"는 DungeonManager가 담당합니다.
/// </summary>
public class DungeonGenerator : MonoBehaviour
{
    [Header("Dungeon Prefabs")]
    [Tooltip("에디터로 구워둔 맵 프리팹들을 배열에 넣어주세요. 런 하나당 중복 없이 배정되므로, " +
        "GameManager의 maxFloor보다 개수가 많거나 같아야 합니다.")]
    public GameObject[] mapPrefabs;

    private const string NavWalkSurfaceName = "NavWalkSurface";

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
        ApplyGridSystemToMap(spawnedMap);

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

        SnapPushablesToGround(spawnedMap);

        // ★ FloorTrap: 구덩이·가시는 NavMesh에서 제외하고, 일반 Floor와 같은 높이의 보행면을 깔아
        //   함정 칸도 끊기지 않게 합니다. (베이크 후 렌더러는 숨깁니다)
        PrepareFloorTrapNavMesh(spawnedMap);
        PrepareStandardFloorWalkSurfaces(spawnedMap);
        PrepareWallNavMeshBlockers(spawnedMap);
        PrepareWallNavMeshObstacles(spawnedMap);
        PrepareNavMeshExclusions(spawnedMap);

        // 발판 ↔ 화살 함정 연결 (프리팹 수동 참조가 런타임에서 null이 되는 경우 대비)
        ArrowTrapRuntimeWire.WireMap(spawnedMap, onlyIfMissing: false);

        Debug.Log($"<color=cyan>{floor}층 던전 생성 완료: {spawnedMap.name}</color>");

        return spawnedMap;
    }

    /// <summary>NavMesh 베이크 직후 Trap 보행면(베이크 전용 메시) 렌더러를 숨깁니다.</summary>
    public void OnNavMeshBaked(GameObject locationInstance)
    {
        HideNavWalkSurfaceRenderers(locationInstance);
    }

    /// <summary>NavMesh 재베이크 직전 준비: Floor/Trap 보행면 보장 + 비보행 Renderer 제외.</summary>
    public void PrepareNavMeshForBake(GameObject mapInstance)
    {
        PrepareStandardFloorWalkSurfaces(mapInstance);
        PrepareWallNavMeshBlockers(mapInstance);
        PrepareWallNavMeshObstacles(mapInstance);
        PrepareNavWalkSurfacesForBake(mapInstance);
        PrepareNavMeshExclusions(mapInstance);
        PrepareNonMapNavMeshExclusions();
    }

    /// <summary>
    /// FloorTrap(TrapTile)마다 구덩이·장식 메시는 NavMesh 베이크에서 제외하고,
    /// 일반 FloorTile과 같은 높이의 얇은 보행면(NavWalkSurface)을 추가해 NavMesh가 이어지게 합니다.
    /// </summary>
    private void PrepareFloorTrapNavMesh(GameObject mapInstance)
    {
        if (!TryGetStandardWalkSurfaceY(mapInstance, out float walkSurfaceY))
        {
            Debug.LogWarning("[DungeonGenerator] 일반 Floor 타일에서 보행면 높이를 찾지 못했습니다. " +
                "GridSystem 기준 높이로 Trap 보행면을 깝니다.");
            walkSurfaceY = GridSystem.Instance.GetCellCenterWorld(Vector3Int.zero).y + GridSystem.Instance.TileHeight;
        }

        float tileSize = GridSystem.Instance.TileSize;
        const float walkSurfaceThickness = 0.05f;
        const float walkSurfaceInset = 0.72f;

        foreach (Transform trap in mapInstance.GetComponentsInChildren<Transform>(true))
        {
            if (!trap.CompareTag("Trap")) continue;
            if (trap.GetComponent<TrapTile>() == null) continue;

            foreach (Renderer renderer in trap.GetComponentsInChildren<Renderer>(true))
            {
                NavMeshModifier exclude = renderer.gameObject.GetComponent<NavMeshModifier>();
                if (exclude == null) exclude = renderer.gameObject.AddComponent<NavMeshModifier>();
                exclude.ignoreFromBuild = true;
            }

            Transform walkSurface = trap.Find(NavWalkSurfaceName);
            if (walkSurface == null)
            {
                GameObject walkObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                walkObj.name = NavWalkSurfaceName;
                walkObj.transform.SetParent(trap, false);

                Collider walkCollider = walkObj.GetComponent<Collider>();
                if (walkCollider != null) Destroy(walkCollider);
            }
            else
            {
                walkSurface.gameObject.name = NavWalkSurfaceName;
            }

            walkSurface = trap.Find(NavWalkSurfaceName);
            walkSurface.position = new Vector3(trap.position.x, walkSurfaceY, trap.position.z);
            walkSurface.localScale = new Vector3(tileSize * walkSurfaceInset, walkSurfaceThickness, tileSize * walkSurfaceInset);

            Renderer walkRenderer = walkSurface.GetComponent<Renderer>();
            if (walkRenderer != null) walkRenderer.enabled = true;

            NavMeshModifier walkModifier = walkSurface.GetComponent<NavMeshModifier>();
            if (walkModifier == null) walkModifier = walkSurface.gameObject.AddComponent<NavMeshModifier>();
            int trapArea = NavMesh.GetAreaFromName("Trap");
            walkModifier.area = trapArea >= 0 ? trapArea : NavMesh.GetAreaFromName("Walkable");
            walkModifier.ignoreFromBuild = false;
        }
    }

    private static void HideNavWalkSurfaceRenderers(GameObject mapInstance)
    {
        SetNavWalkSurfaceRenderersEnabled(mapInstance, false);
    }

    private static void SetNavWalkSurfaceRenderersEnabled(GameObject mapInstance, bool enabled)
    {
        foreach (Transform child in mapInstance.GetComponentsInChildren<Transform>(true))
        {
            if (child.name != NavWalkSurfaceName) continue;

            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = enabled;
        }
    }

    private void PrepareNavWalkSurfacesForBake(GameObject mapInstance)
    {
        SetNavWalkSurfaceRenderersEnabled(mapInstance, true);
    }

    /// <summary>
    /// Floor 태그 타일(Road 등)마다 NavWalkSurface 큐브를 깔아 NavMesh를 만듭니다.
    /// gravel·floor_tile_small처럼 Read/Write가 꺼진 장식 메시만으로는 NavMesh가 생성되지 않습니다.
    /// </summary>
    private void PrepareStandardFloorWalkSurfaces(GameObject mapInstance)
    {
        float tileSize = GridSystem.Instance.TileSize;
        const float walkSurfaceThickness = 0.05f;
        const float walkSurfaceInset = 0.72f;
        int walkableArea = NavMesh.GetAreaFromName("Walkable");

        foreach (Transform tile in mapInstance.GetComponentsInChildren<Transform>(true))
        {
            if (!tile.CompareTag("Floor")) continue;
            if (tile.GetComponentInParent<TrapTile>() != null) continue;
            if (tile.name == NavWalkSurfaceName) continue;

            Transform walkSurface = tile.Find(NavWalkSurfaceName);
            if (walkSurface == null)
            {
                GameObject walkObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                walkObj.name = NavWalkSurfaceName;
                walkObj.transform.SetParent(tile, false);

                Collider walkCollider = walkObj.GetComponent<Collider>();
                if (walkCollider != null) Destroy(walkCollider);

                walkSurface = walkObj.transform;
            }

            if (!TryGetTileTopSurface(tile, out Vector3 topSurfacePoint))
                topSurfacePoint = tile.position;

            walkSurface.position = topSurfacePoint;
            walkSurface.localScale = new Vector3(tileSize * walkSurfaceInset, walkSurfaceThickness, tileSize * walkSurfaceInset);

            Renderer walkRenderer = walkSurface.GetComponent<Renderer>();
            if (walkRenderer != null) walkRenderer.enabled = true;

            NavMeshModifier walkModifier = walkSurface.GetComponent<NavMeshModifier>();
            if (walkModifier == null) walkModifier = walkSurface.gameObject.AddComponent<NavMeshModifier>();
            walkModifier.area = walkableArea;
            walkModifier.ignoreFromBuild = false;
        }
    }

    /// <summary>베이크 시 Wall 셀에 Not Walkable 볼륨을 깔아 바닥 NavMesh가 벽을 관통하지 않게 합니다.</summary>
    private static void PrepareWallNavMeshBlockers(GameObject mapInstance)
    {
        float tileSize = GridSystem.Instance.TileSize;
        int notWalkable = NavMesh.GetAreaFromName("Not Walkable");
        if (notWalkable < 0) notWalkable = 1;
        Transform mapRoot = mapInstance.transform;

        foreach (Transform wall in mapInstance.GetComponentsInChildren<Transform>(true))
        {
            if (!wall.CompareTag("Wall") || !IsMapTileRoot(wall, mapRoot)) continue;

            NavMeshModifierVolume volume = wall.GetComponent<NavMeshModifierVolume>();
            if (volume == null) volume = wall.gameObject.AddComponent<NavMeshModifierVolume>();

            volume.center = new Vector3(0f, tileSize * 0.5f, 0f);
            volume.size = new Vector3(tileSize, tileSize, tileSize);
            volume.area = notWalkable;
        }
    }

    /// <summary>Wall 타일에 NavMeshObstacle을 붙여 벽을 통과하는 경로를 막습니다.</summary>
    private static void PrepareWallNavMeshObstacles(GameObject mapInstance)
    {
        float tileSize = GridSystem.Instance.TileSize;
        Transform mapRoot = mapInstance.transform;

        foreach (Transform wall in mapInstance.GetComponentsInChildren<Transform>(true))
        {
            if (!wall.CompareTag("Wall") || !IsMapTileRoot(wall, mapRoot)) continue;

            NavMeshObstacle obstacle = wall.GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = wall.gameObject.AddComponent<NavMeshObstacle>();

            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0f, tileSize * 0.5f, 0f);
            obstacle.size = new Vector3(tileSize * 0.95f, tileSize, tileSize * 0.95f);
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
        }
    }

    private bool TryGetStandardWalkSurfaceY(GameObject mapInstance, out float walkSurfaceY)
    {
        walkSurfaceY = 0f;

        foreach (Transform child in mapInstance.GetComponentsInChildren<Transform>(true))
        {
            if (!child.CompareTag("Floor")) continue;
            if (child.GetComponentInParent<TrapTile>() != null) continue;

            if (TryGetTileTopSurface(child, out Vector3 topSurfacePoint))
            {
                walkSurfaceY = topSurfacePoint.y;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// NavMesh 베이크 대상에서 장식·함정·적 등 보행면이 아닌 Renderer를 제외합니다.
    /// Read/Write가 꺼진 임포트 메시(arrow_bow, floor_tile_small 등) 경고도 방지합니다.
    /// </summary>
    private static void PrepareNavMeshExclusions(GameObject mapInstance)
    {
        foreach (Renderer renderer in mapInstance.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.gameObject.name == NavWalkSurfaceName) continue;

            if (ShouldExcludeRendererFromNavMesh(renderer))
                SetIgnoreFromNavMeshBuild(renderer.gameObject, true);
        }
    }

    private static bool ShouldExcludeRendererFromNavMesh(Renderer renderer)
    {
        Transform t = renderer.transform;

        if (t.GetComponentInParent<ArrowTrap>() != null) return true;
        if (t.GetComponentInParent<PressurePlate>() != null) return true;
        if (t.GetComponentInParent<WallTrap>() != null) return true;
        if (t.GetComponentInParent<RollingRockTrap>() != null) return true;
        if (t.GetComponentInParent<EnemyController>() != null) return true;
        if (t.GetComponentInParent<TreasureChest>() != null) return true;

        if (t.CompareTag("Wall")) return true;

        // TrapTile 보행면(NavWalkSurface)만 남기고 Trap 태그 장식 메시는 제외
        if (t.CompareTag("Trap") && t.GetComponentInParent<TrapTile>() == null)
            return true;

        // Floor 장식 메시는 NavWalkSurface로 대체 (gravel 등 Read/Write 꺼진 메시)
        if (t.CompareTag("Floor") && t.name != NavWalkSurfaceName)
            return true;

        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            string meshName = meshFilter.sharedMesh.name;
            if (meshName.Contains("torch") || meshName.Contains("arrow_bow") || meshName.Contains("floor_tile_small"))
                return true;

            if (!meshFilter.sharedMesh.isReadable)
                return true;
        }

        return false;
    }

    /// <summary>플레이어·장착 무기 등 맵 밖 오브젝트를 NavMesh 베이크에서 제외합니다.</summary>
    private static void PrepareNonMapNavMeshExclusions()
    {
        foreach (PlayerController player in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true))
                SetIgnoreFromNavMeshBuild(renderer.gameObject, true);
        }
    }

    private static void SetIgnoreFromNavMeshBuild(GameObject target, bool ignore)
    {
        NavMeshModifier modifier = target.GetComponent<NavMeshModifier>();
        if (modifier == null) modifier = target.AddComponent<NavMeshModifier>();
        modifier.ignoreFromBuild = ignore;
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

    private void NormalizeMapOrigin(GameObject mapInstance)
    {
        float minX = float.MaxValue;
        float minZ = float.MaxValue;
        bool foundAnyTile = false;
        Transform mapRoot = mapInstance.transform;

        // 장식 자식/오프셋 타일(≈0.97)이 원점을 오염시키지 않도록 타일 루트만 사용합니다.
        foreach (Transform child in mapInstance.GetComponentsInChildren<Transform>(true))
        {
            if (!IsMapTileRoot(child, mapRoot)) continue;
            // 원점은 바닥 계열만 — Wall 돌출이 min을 밀지 않게 합니다.
            if (!(child.CompareTag("Floor") || child.CompareTag("Entrance") ||
                  child.CompareTag("Exit") || child.CompareTag("Trap")))
                continue;

            foundAnyTile = true;
            if (child.position.x < minX) minX = child.position.x;
            if (child.position.z < minZ) minZ = child.position.z;
        }

        if (!foundAnyTile) return;

        // 미세 오차(0.97 등)를 제거하고 정수/반칸 격자에 스냅
        minX = SnapCoordToHalf(minX);
        minZ = SnapCoordToHalf(minZ);

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

    private static float SnapCoordToHalf(float value)
    {
        return Mathf.Round(value * 2f) / 2f;
    }

    /// <summary>
    /// Demomap(간격 2)처럼 애셋 기본 tileSize(3)와 다른 맵이면 GridSystem을 맵 실측 간격에 맞춥니다.
    /// Push/Latch/Restore가 미니맵·타일과 같은 칸을 쓰게 됩니다.
    /// </summary>
    public void SyncGridSystemToLocation(GameObject mapInstance)
    {
        ApplyGridSystemToMap(mapInstance);
    }

    private void ApplyGridSystemToMap(GameObject mapInstance)
    {
        if (!TryInferMapTileSpacing(mapInstance, out float spacing, out Vector3 origin))
            return;

        GridSystem.Instance.ApplyActiveMapGrid(spacing, origin);
        Debug.Log($"<color=yellow>[DungeonGenerator] GridSystem을 맵에 맞춤: tileSize={spacing}, origin={origin}</color>");
    }

    private static bool TryInferMapTileSpacing(GameObject mapInstance, out float spacing, out Vector3 origin)
    {
        float fallback = 3f;
        // 이전 런의 잘못된 덮어쓰기(예: 0.97)에 물리지 않도록 애셋 기본값을 우선 사용합니다.
        GridSystem.Instance.ClearActiveMapGrid();
        fallback = GridSystem.Instance.TileSize;
        spacing = fallback;
        origin = GridSystem.Instance.Origin;

        var xs = new List<float>();
        var zs = new List<float>();
        float minX = float.MaxValue;
        float minZ = float.MaxValue;
        Transform mapRoot = mapInstance.transform;

        foreach (Transform child in mapInstance.GetComponentsInChildren<Transform>(true))
        {
            if (!IsMapTileRoot(child, mapRoot)) continue;

            Vector3 pos = child.position;
            if (pos.x < minX) minX = pos.x;
            if (pos.z < minZ) minZ = pos.z;
            xs.Add(pos.x);
            zs.Add(pos.z);
        }

        if (xs.Count == 0) return false;

        origin = new Vector3(SnapCoordToHalf(minX), 0f, SnapCoordToHalf(minZ));

        float inferredX = InferDominantTileSpacing(xs, fallback);
        float inferredZ = InferDominantTileSpacing(zs, fallback);

        float inferred = ChooseTileSpacing(inferredX, inferredZ, fallback);
        spacing = inferred > 0f ? inferred : (SnapTileSpacing(fallback) > 0f ? SnapTileSpacing(fallback) : 3f);
        return true;
    }

    private static bool IsMapTileRoot(Transform obj, Transform mapRoot)
    {
        if (obj == mapRoot) return false;
        if (!(obj.CompareTag("Floor") || obj.CompareTag("Wall") || obj.CompareTag("Entrance") ||
              obj.CompareTag("Exit") || obj.CompareTag("Trap")))
            return false;

        Transform parent = obj.parent;
        while (parent != null && parent != mapRoot)
        {
            if (parent.CompareTag("Floor") || parent.CompareTag("Wall") || parent.CompareTag("Entrance") ||
                parent.CompareTag("Exit") || parent.CompareTag("Trap") || parent.CompareTag("Chest") ||
                parent.CompareTag("Pushable"))
                return false;
            parent = parent.parent;
        }

        return true;
    }

    private static float SnapTileSpacing(float spacing)
    {
        if (Mathf.Abs(spacing - 2f) <= 0.35f) return 2f;
        if (Mathf.Abs(spacing - 3f) <= 0.35f) return 3f;
        // 2/3이 아니면 애셋 기본값을 쓰기 위해 NaN 대신 0을 돌려 호출부에서 fallback 처리
        return 0f;
    }

    private static float ChooseTileSpacing(float a, float b, float fallback)
    {
        float snappedA = SnapTileSpacing(a);
        float snappedB = SnapTileSpacing(b);
        bool aOk = snappedA > 0f;
        bool bOk = snappedB > 0f;

        if (!aOk && !bOk) return SnapTileSpacing(fallback) > 0f ? SnapTileSpacing(fallback) : 3f;
        if (!aOk) return snappedB;
        if (!bOk) return snappedA;
        if (Mathf.Approximately(snappedA, snappedB)) return snappedA;

        // Demomap(2) 우선: 둘 다 타당하면 더 작은 간격
        return Mathf.Min(snappedA, snappedB);
    }

    /// <summary>최소값이 아니라 가장 자주 나오는 타일 간격을 고릅니다. (장식 메시 오프셋 ~0.97 노이즈 무시)</summary>
    private static float InferDominantTileSpacing(List<float> values, float fallback)
    {
        if (values == null || values.Count < 2) return fallback;

        values.Sort();
        var buckets = new Dictionary<int, int>();

        for (int i = 1; i < values.Count; i++)
        {
            float delta = values[i] - values[i - 1];
            if (delta < 1.5f || delta > 8f) continue;

            int key = Mathf.RoundToInt(delta * 10f); // 0.1 단위
            buckets.TryGetValue(key, out int count);
            buckets[key] = count + 1;
        }

        if (buckets.Count == 0) return fallback;

        int bestKey = 0;
        int bestCount = -1;
        foreach (KeyValuePair<int, int> pair in buckets)
        {
            if (pair.Value <= bestCount) continue;
            bestCount = pair.Value;
            bestKey = pair.Key;
        }

        return bestKey / 10f;
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
            if (child.CompareTag("Entrance")) root.RegisterSpawnPoint("Entrance", child);
            else if (child.CompareTag("Exit")) root.RegisterSpawnPoint("Exit", child);
        }
    }
}
