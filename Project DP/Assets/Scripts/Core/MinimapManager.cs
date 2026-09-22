using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MinimapManager : MonoBehaviour
{
    public static MinimapManager Instance;

    [Header("UI (오브젝트 단 하나)")]
    [Tooltip("미니맵을 표시할 RawImage. 텍스처는 코드에서 자동으로 만들어 할당합니다.")]
    public RawImage minimapDisplay;
    [Tooltip("RawImage와 같은 오브젝트에 붙여서 연결하세요. 텍스처 비율(gridSize x pixelsPerTile 기준)에 맞춰 " +
        "자동으로 사각형을 조정해, 타일맵 전체가 잘리거나 늘어나지 않고 중앙에 딱 맞게 표시되도록 합니다.")]
    public AspectRatioFitter minimapAspectFitter;

    [Header("Grid Settings")]
    [Tooltip("맵 스캔 실패 시 쓰는 최소 그리드 크기. 실제 크기는 맵 타일 범위에서 자동 계산됩니다.")]
    public Vector2Int gridSize = new Vector2Int(17, 17);
    [Tooltip("타일 1칸을 텍스처 몇 픽셀로 그릴지. 값이 클수록 원형 시야 경계가 부드러워집니다.")]
    public int pixelsPerTile = 8;

    [Header("개척도")]
    [Tooltip("밟을 수 있는 타일 중 이 비율 이상을 밟으면 '개척 완료'로 봅니다. " +
             "상자나 밀 수 있는 오브젝트가 올라간 칸에는 설 수 없어 100%가 안 나올 수 있으니 " +
             "실제로 한 층을 다 돌아본 뒤 도달 가능한 값으로 맞춰주세요.")]
    [Range(0f, 1f)] public float explorationCompleteThreshold = 0.9f;

    [Header("Fog / Vision Settings")]
    [Tooltip("플레이어 기준 실시간으로 밝혀지는 반경 (타일 단위)")]
    public float playerVisionRadius = 1.5f;
    [Tooltip("횃불 기준 영구적으로 밝혀지는 반경 (타일 단위)")]
    public float torchVisionRadius = 1.2f;

    [Header("Tile Colors")]
    public Color unexploredColor = new Color(0f, 0f, 0f, 1f);
    public Color visitedTrailColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    public Color floorColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    public Color wallColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    public Color pushWallColor = new Color(1f, 0.55f, 0.1f, 1f); // 발견한 Pushable (주황)
    public Color trapColor = new Color(0f, 0f, 1f, 1f);

    [Header("Entity Colors")]
    public Color playerColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    public Color enemyColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    public Color chestColor = new Color(0.9f, 0.9f, 0.2f, 1f);
    public Color torchColor = new Color(1f, 0.6f, 0.1f, 1f);
    public Color graveColor = new Color(0.65f, 0.2f, 0.9f, 1f);

    [Header("Entity Marker Sizes (px, 텍스처 해상도 기준)")]
    public int playerDotRadiusPx = 3;
    public int enemyDotRadiusPx = 3;
    public int chestDotRadiusPx = 2;
    public int torchDotRadiusPx = 1;

    // ── 내부 데이터 ─────────────────────────────────────────────
    private class LocationMinimapState
    {
        // 셀 좌표는 GridSystem을 그대로 씁니다. 맵마다 다른 건 '몇 칸짜리 맵인가'뿐입니다.
        public Vector2Int gridSize;
        public LocationGrid grid;
        public bool[,] visited;
        public bool[,] currentlyLitTiles;
        public Texture2D texture;
        public Color[] pixelBuffer;
        public int texWidth, texHeight;

        public int walkableTileCount;
        public int visitedTileCount;
        public readonly List<Vector3> walkableWorldPositions = new List<Vector3>();
        public readonly List<Transform> activePushables = new List<Transform>();
        public readonly HashSet<int> discoveredPushableIds = new HashSet<int>();
        public readonly List<Transform> activeChests = new List<Transform>();
        public readonly List<Transform> activeBonuses = new List<Transform>();
        public readonly List<Transform> activeTorches = new List<Transform>();
        public readonly List<EnemyController> activeEnemies = new List<EnemyController>();
    }

    private readonly Dictionary<LocationId, LocationMinimapState> locationStates = new Dictionary<LocationId, LocationMinimapState>();
    private LocationMinimapState current;
    private GameObject currentLocationInstance;

    private Transform playerTransform;
    private Vector3 lastMarkedPlayerPos;
    private bool hasLastMarkedPlayerPos;
    private readonly HashSet<Vector2Int> pushableTilesThisFrame = new HashSet<Vector2Int>();
    private readonly List<Transform> pushablesToDrawThisFrame = new List<Transform>();

    private void Awake()
    {
        // HUD(전역)와 던전 씬 양쪽에 MinimapManager가 있을 수 있습니다.
        // 먼저 살아 있는 인스턴스를 유지하고, 나중에 로드된 중복은 제거합니다.
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ResetAllLocations()
    {
        locationStates.Clear();
        current = null;
    }

    public Texture CurrentTexture => current != null ? current.texture : null;

    public float CurrentAspect => (current != null && current.texHeight > 0) ? (float)current.texWidth / current.texHeight : 1f;

    public bool HasCurrentLocation => current != null && current.walkableTileCount > 0;

    public float CurrentExploration =>
        HasCurrentLocation ? (float)current.visitedTileCount / current.walkableTileCount : 0f;

    public bool IsCurrentExplored => HasCurrentLocation && CurrentExploration >= explorationCompleteThreshold;

    public bool TryCaptureExploration(LocationId id, out bool[] visited, out int visitedCount, out Vector2Int gridSize)
    {
        visited = null;
        visitedCount = 0;
        gridSize = default;

        if (!locationStates.TryGetValue(id, out LocationMinimapState state) || state.visited == null) return false;

        gridSize = state.gridSize;
        visited = new bool[gridSize.x * gridSize.y];
        for (int y = 0; y < gridSize.y; y++)
            for (int x = 0; x < gridSize.x; x++)
                visited[y * gridSize.x + x] = state.visited[x, y];

        visitedCount = state.visitedTileCount;
        return true;
    }

    public void RestoreExploration(LocationId id, bool[] visited, int visitedCount, Vector2Int savedGridSize)
    {
        if (visited == null) return;
        if (!locationStates.TryGetValue(id, out LocationMinimapState state)) return;
        if (visited.Length != state.gridSize.x * state.gridSize.y) return;

        // 구 스냅샷(minimapGridSize 미저장)은 배열 길이만 맞으면 복원합니다.
        if (savedGridSize != default && savedGridSize != state.gridSize) return;

        for (int y = 0; y < state.gridSize.y; y++)
            for (int x = 0; x < state.gridSize.x; x++)
                state.visited[x, y] = visited[y * state.gridSize.x + x];

        state.visitedTileCount = visitedCount;
    }

    public void SwitchLocation(LocationId id, GameObject locationInstance, bool isNewLocation, Transform playerRef)
    {
        playerTransform = playerRef;
        currentLocationInstance = locationInstance;
        hasLastMarkedPlayerPos = false;

        if (isNewLocation || !locationStates.TryGetValue(id, out current))
        {
            current = BuildLocationState(locationInstance);
            locationStates[id] = current;
        }
        ApplyAspectRatio();
        if (playerTransform != null)
        {
            MarkPlayerVisitedCells();
            RenderMinimap();
        }
    }

    private LocationMinimapState BuildLocationState(GameObject locationInstance)
    {
        LocationGrid grid = ResolveGrid(locationInstance);
        Vector2Int mapGridSize = grid.Size;

        var state = new LocationMinimapState
        {
            gridSize = mapGridSize,
            grid = grid,
            visited = new bool[mapGridSize.x, mapGridSize.y],
            currentlyLitTiles = new bool[mapGridSize.x, mapGridSize.y]
        };

        state.walkableWorldPositions.AddRange(grid.WalkableCellCenters);

        foreach (PushableObject pushable in grid.Pushables)
        {
            if (pushable == null) continue;
            state.activePushables.Add(pushable.transform);
        }

        state.activeChests.AddRange(grid.Chests);

        if (locationInstance != null)
        {
            EnemyController[] enemies = locationInstance.GetComponentsInChildren<EnemyController>(true);
            state.activeEnemies.AddRange(enemies);

            foreach (TorchInstance torch in locationInstance.GetComponentsInChildren<TorchInstance>(true))
                state.activeTorches.Add(torch.transform);

            foreach (Transform obj in locationInstance.GetComponentsInChildren<Transform>(true))
            {
                // 프리팹 루트 이름 Bonus (복제본 "Bonus (1)" 포함). 자식 메시는 제외.
                if (!obj.name.StartsWith("Bonus")) continue;
                if (obj.parent != null && obj.parent.name.StartsWith("Bonus")) continue;
                state.activeBonuses.Add(obj);
            }
        }

        for (int y = 0; y < state.gridSize.y; y++)
        {
            for (int x = 0; x < state.gridSize.x; x++)
            {
                if (LocationGrid.IsWalkableTerrain(grid.Cells[x, y])) state.walkableTileCount++;
            }
        }

        state.texWidth = state.gridSize.x * pixelsPerTile;
        state.texHeight = state.gridSize.y * pixelsPerTile;
        state.texture = new Texture2D(state.texWidth, state.texHeight, TextureFormat.RGBA32, false);
        state.texture.filterMode = FilterMode.Point;
        state.texture.wrapMode = TextureWrapMode.Clamp;
        state.pixelBuffer = new Color[state.texWidth * state.texHeight];
        FillBuffer(state, unexploredColor);

        return state;
    }

    private static void FillBuffer(LocationMinimapState state, Color color)
    {
        for (int i = 0; i < state.pixelBuffer.Length; i++)
            state.pixelBuffer[i] = color;
    }

    private LocationGrid ResolveGrid(GameObject locationInstance)
    {
        LocationRoot root = locationInstance != null ? locationInstance.GetComponent<LocationRoot>() : null;
        if (root != null && root.Grid != null) return root.Grid;

        Debug.LogWarning("[MinimapManager] LocationRoot.Grid가 없어 미니맵이 직접 격자를 만듭니다.");
        LocationGrid grid = LocationGridBuilder.Build(locationInstance, gridSize);
        if (root != null) root.SetGrid(grid);
        return grid;
    }

    private void ApplyAspectRatio()
    {
        if (minimapAspectFitter == null || current == null) return;

        minimapAspectFitter.aspectRatio = (float)current.texWidth / current.texHeight;

        RectTransform rt = minimapAspectFitter.GetComponent<RectTransform>();
        if (rt != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
    }

    public void RegisterTorch(Transform torchTransform)
    {
        if (torchTransform == null || current == null) return;
        if (!current.activeTorches.Contains(torchTransform))
            current.activeTorches.Add(torchTransform);
    }

    public void RegisterGrave(Transform graveTransform)
    {
        if (graveTransform == null || current == null) return;
        RefreshGraves();
    }

    public void UnregisterGrave(Transform graveTransform)
    {
        if (graveTransform == null || current == null) return;
        RefreshGraves();
    }

    /// <summary>묘비 제거 직후 미니맵에서 보라색 표식이 남지 않도록 즉시 다시 그립니다.</summary>
    public void RefreshGraves()
    {
        if (current == null || current.texture == null) return;
        RenderMinimap();
    }

    private void Update()
    {
        if (current == null || current.texture == null || playerTransform == null) return;

        MarkPlayerVisitedCells();
        RenderMinimap();
    }

    private void MarkPlayerVisitedCells()
    {
        Vector3 pos = playerTransform.position;

        if (hasLastMarkedPlayerPos)
        {
            float step = GridSystem.Instance.TileSize * 0.35f;
            Vector3 last = lastMarkedPlayerPos;
            float dist = Vector3.Distance(
                new Vector3(last.x, 0f, last.z),
                new Vector3(pos.x, 0f, pos.z));
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist / Mathf.Max(0.01f, step)));

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector3 sample = Vector3.Lerp(last, pos, t);
                // 밟은 Road/Floor만 회색. 시야 반경으로 주변 칸을 미리 밝히지 않습니다.
                MarkVisitedAtWorldPosition(sample);
            }
        }
        else
        {
            MarkVisitedAtWorldPosition(pos);
        }

        DiscoverNearbyLootMarkers(pos);
        DiscoverNearbyPushables(pos);

        lastMarkedPlayerPos = pos;
        hasLastMarkedPlayerPos = true;
    }

    /// <summary>
    /// Pushable은 칸을 점유해 플레이어가 그 셀을 밟지 못하므로,
    /// 근처에 오면 '발견'으로 등록하고 이후 라이브 위치를 미니맵에 그립니다.
    /// </summary>
    private void DiscoverNearbyPushables(Vector3 playerPos)
    {
        float discoverDist = GridSystem.Instance.TileSize * 1.35f;
        float discoverDistSq = discoverDist * discoverDist;

        for (int i = 0; i < current.activePushables.Count; i++)
        {
            Transform pushable = current.activePushables[i];
            if (pushable == null) continue;

            float dx = playerPos.x - pushable.position.x;
            float dz = playerPos.z - pushable.position.z;
            if (dx * dx + dz * dz > discoverDistSq) continue;

            current.discoveredPushableIds.Add(pushable.GetInstanceID());
        }
    }

    /// <summary>Chest/Bonus는 플레이어가 그 칸 근처를 밟거나 시야에 넣으면 발견됩니다.</summary>
    private void DiscoverNearbyLootMarkers(Vector3 playerPos)
    {
        float discoverDist = GridSystem.Instance.TileSize * 1.15f;
        float discoverDistSq = discoverDist * discoverDist;

        for (int i = 0; i < current.activeChests.Count; i++)
        {
            Transform chest = current.activeChests[i];
            if (chest == null) continue;

            float dx = playerPos.x - chest.position.x;
            float dz = playerPos.z - chest.position.z;
            if (dx * dx + dz * dz > discoverDistSq) continue;

            Vector3Int cell = GridSystem.Instance.WorldToCell(chest.position);
            MarkVisitedCell(cell.x, cell.z);
        }

        for (int i = 0; i < current.activeBonuses.Count; i++)
        {
            Transform bonus = current.activeBonuses[i];
            if (bonus == null) continue;

            float dx = playerPos.x - bonus.position.x;
            float dz = playerPos.z - bonus.position.z;
            if (dx * dx + dz * dz > discoverDistSq) continue;

            Vector3Int cell = GridSystem.Instance.WorldToCell(bonus.position);
            MarkVisitedCell(cell.x, cell.z);
        }
    }

    private void MarkVisitedAtWorldPosition(Vector3 worldPos)
    {
        // 실제로 밟을 수 있는 타일(Road/Floor/Trap 등) 중심과의 거리로만 방문 처리합니다.
        // 폴백으로 임의 셀을 찍지 않아 Wall이 회색으로 물들지 않습니다.
        float maxDist = GridSystem.Instance.TileSize * 0.55f;
        float maxDistSq = maxDist * maxDist;

        for (int i = 0; i < current.walkableWorldPositions.Count; i++)
        {
            Vector3 tilePos = current.walkableWorldPositions[i];
            float dx = worldPos.x - tilePos.x;
            float dz = worldPos.z - tilePos.z;
            if (dx * dx + dz * dz > maxDistSq) continue;

            Vector3Int cell = GridSystem.Instance.WorldToCell(tilePos);
            MarkVisitedCell(cell.x, cell.z);
        }
    }

    private void MarkVisitedCell(int x, int z)
    {
        if (!IsValidGrid(x, z, current.gridSize)) return;
        // Wall은 항상 검은색(미탐험) — 방문 표시 금지
        CellType type = current.grid.Get(x, z);
        if (type == CellType.Wall) return;
        if (type == CellType.None) return;
        if (current.visited[x, z]) return;

        current.visited[x, z] = true;

        if (LocationGrid.IsWalkableTerrain(type))
            current.visitedTileCount++;
    }

    private void RenderMinimap()
    {
        Vector2Int grid = current.gridSize;
        pushableTilesThisFrame.Clear();
        pushablesToDrawThisFrame.Clear();

        for (int i = current.activePushables.Count - 1; i >= 0; i--)
        {
            Transform p = current.activePushables[i];
            if (p == null)
            {
                current.activePushables.RemoveAt(i);
                continue;
            }

            if (!p.gameObject.activeInHierarchy || p.position.y < -5f)
                continue;

            int id = p.GetInstanceID();
            if (!current.discoveredPushableIds.Contains(id))
                continue;

            pushablesToDrawThisFrame.Add(p);

            Vector3Int cell = GridSystem.Instance.WorldToCell(p.position);
            if (IsValidGrid(cell.x, cell.z, grid))
                pushableTilesThisFrame.Add(new Vector2Int(cell.x, cell.z));
        }

        for (int i = current.activeTorches.Count - 1; i >= 0; i--)
        {
            Transform torch = current.activeTorches[i];
            if (torch == null) { current.activeTorches.RemoveAt(i); continue; }
            MarkVisitedInRadius(torch.position, torchVisionRadius);
        }

        for (int ty = 0; ty < grid.y; ty++)
        {
            for (int tx = 0; tx < grid.x; tx++)
                PaintTileBlock(tx, ty, GetMinimapTileColor(tx, ty));
        }

        DrawEntities();

        current.texture.SetPixels(current.pixelBuffer);
        current.texture.Apply();
    }

    private Color GetMinimapTileColor(int tx, int ty)
    {
        // Wall은 방문 여부와 관계없이 항상 검은색
        CellType type = current.grid.Get(tx, ty);
        if (type == CellType.Wall)
            return unexploredColor;

        if (!current.visited[tx, ty])
            return unexploredColor;

        // Pushable은 DrawEntities에서 주황 점으로 라이브 표시 (타일 전체를 칠하지 않음)
        if (LocationGrid.IsTrapTerrain(type))
            return trapColor;

        return visitedTrailColor;
    }

    private void MarkVisitedInRadius(Vector3 worldPos, float radiusInTiles)
    {
        Vector2 center = WorldToPixel(worldPos);
        float radiusPx = radiusInTiles * pixelsPerTile;

        int minTx = Mathf.Max(0, Mathf.FloorToInt((center.x - radiusPx) / pixelsPerTile));
        int maxTx = Mathf.Min(current.gridSize.x - 1, Mathf.CeilToInt((center.x + radiusPx) / pixelsPerTile));
        int minTy = Mathf.Max(0, Mathf.FloorToInt((center.y - radiusPx) / pixelsPerTile));
        int maxTy = Mathf.Min(current.gridSize.y - 1, Mathf.CeilToInt((center.y + radiusPx) / pixelsPerTile));

        float radiusSq = radiusPx * radiusPx;

        for (int ty = minTy; ty <= maxTy; ty++)
        {
            for (int tx = minTx; tx <= maxTx; tx++)
            {
                float cx = tx * pixelsPerTile + pixelsPerTile * 0.5f;
                float cy = ty * pixelsPerTile + pixelsPerTile * 0.5f;
                float dx = cx - center.x;
                float dy = cy - center.y;
                if (dx * dx + dy * dy > radiusSq) continue;

                if (current.grid.Get(tx, ty) == CellType.Wall) continue;
                MarkVisitedCell(tx, ty);
            }
        }
    }

    private void DrawEntities()
    {
        DrawDot(playerTransform.position, playerColor, playerDotRadiusPx);

        for (int i = current.activeEnemies.Count - 1; i >= 0; i--)
        {
            EnemyController enemy = current.activeEnemies[i];
            if (enemy == null) { current.activeEnemies.RemoveAt(i); continue; }

            Vector3Int cell = GridSystem.Instance.WorldToCell(enemy.transform.position);
            if (IsValidGrid(cell.x, cell.z, current.gridSize) && current.visited[cell.x, cell.z])
                DrawDot(enemy.transform.position, enemyColor, enemyDotRadiusPx);
        }

        for (int i = current.activeChests.Count - 1; i >= 0; i--)
        {
            Transform chest = current.activeChests[i];
            if (chest == null) { current.activeChests.RemoveAt(i); continue; }

            Vector3Int cell = GridSystem.Instance.WorldToCell(chest.position);
            if (IsValidGrid(cell.x, cell.z, current.gridSize) && current.visited[cell.x, cell.z])
                DrawDot(chest.position, chestColor, chestDotRadiusPx);
        }

        for (int i = current.activeBonuses.Count - 1; i >= 0; i--)
        {
            Transform bonus = current.activeBonuses[i];
            if (bonus == null) { current.activeBonuses.RemoveAt(i); continue; }

            Vector3Int cell = GridSystem.Instance.WorldToCell(bonus.position);
            if (IsValidGrid(cell.x, cell.z, current.gridSize) && current.visited[cell.x, cell.z])
                DrawDot(bonus.position, chestColor, chestDotRadiusPx);
        }

        for (int i = 0; i < pushablesToDrawThisFrame.Count; i++)
        {
            Transform pushable = pushablesToDrawThisFrame[i];
            if (pushable == null) continue;

            // 현재 월드 위치에 주황 점 — 밀면 매 프레임 따라갑니다.
            DrawDot(pushable.position, pushWallColor, Mathf.Max(3, chestDotRadiusPx + 1));
        }

        for (int i = current.activeTorches.Count - 1; i >= 0; i--)
        {
            Transform torch = current.activeTorches[i];
            if (torch == null) { current.activeTorches.RemoveAt(i); continue; }
            DrawDot(torch.position, torchColor, torchDotRadiusPx);
        }

        DrawGravesFromScene();
    }

    private void DrawGravesFromScene()
    {
        if (currentLocationInstance == null) return;

        foreach (PlayerGrave grave in currentLocationInstance.GetComponentsInChildren<PlayerGrave>(false))
        {
            if (grave == null || !grave.isActiveAndEnabled || !grave.gameObject.activeInHierarchy)
                continue;

            Vector3Int cell = GridSystem.Instance.WorldToCell(grave.transform.position);
            if (IsValidGrid(cell.x, cell.z, current.gridSize))
                PaintTileBlock(cell.x, cell.z, graveColor);
        }
    }

    private void DrawDot(Vector3 worldPos, Color color, int radiusPx)
    {
        Vector2 center = WorldToPixel(worldPos);

        int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radiusPx));
        int maxX = Mathf.Min(current.texWidth - 1, Mathf.CeilToInt(center.x + radiusPx));
        int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radiusPx));
        int maxY = Mathf.Min(current.texHeight - 1, Mathf.CeilToInt(center.y + radiusPx));

        float radiusSq = radiusPx * radiusPx;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x + 0.5f - center.x;
                float dy = y + 0.5f - center.y;
                if (dx * dx + dy * dy <= radiusSq)
                    current.pixelBuffer[y * current.texWidth + x] = color;
            }
        }
    }

    private Vector2 WorldToPixel(Vector3 worldPos)
    {
        float tileSize = GridSystem.Instance.TileSize;
        Vector3 originWorld = GridSystem.Instance.Origin;

        float startX = originWorld.x - tileSize / 2f;
        float startZ = originWorld.z - tileSize / 2f;

        float px = (worldPos.x - startX) / tileSize * pixelsPerTile;
        float py = (worldPos.z - startZ) / tileSize * pixelsPerTile;
        return new Vector2(px, py);
    }

    private void PaintTileBlock(int tx, int ty, Color color)
    {
        int startX = tx * pixelsPerTile;
        int startY = ty * pixelsPerTile;

        for (int y = 0; y < pixelsPerTile; y++)
        {
            int row = (startY + y) * current.texWidth;
            for (int x = 0; x < pixelsPerTile; x++)
                current.pixelBuffer[row + startX + x] = color;
        }
    }

    private static bool IsValidGrid(int x, int y, Vector2Int gridSize)
    {
        return x >= 0 && x < gridSize.x && y >= 0 && y < gridSize.y;
    }
}
