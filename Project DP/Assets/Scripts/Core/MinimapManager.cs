using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 미니맵 전체를 오브젝트 "하나"(RawImage + 단일 Texture2D)로 그리는 매니저.
///
/// ★ 장소(던전 층/마을/오버월드)별로 미니맵 상태를 분리 보관합니다 ★
/// - WorldManager는 한 번 생성된 장소를 파괴하지 않고 SetActive로만 전환합니다. 그래서 미니맵도
///   장소마다 별도의 텍스처/방문기록/엔티티 목록(LocationMinimapState)을 갖고 있다가, 그 장소로
///   돌아오면 저장해둔 상태를 그대로 복원합니다 (탐색 흔적, 설치된 횃불, 적/상자 위치 전부 유지).
/// - 좌표계: 프로젝트 공통 GridSystem을 그대로 사용 (MapGenerator가 만든 던전과 100% 동일한 격자 기준).
/// - 안개(Fog) 규칙:
///     1) 플레이어가 "밟고 지나간" 타일 → 이후 계속 회색(방문 흔적)으로 남음
///     2) 실시간으로 밝혀지는 범위는 플레이어 중심의 "원형" 범위
///     3) 횃불을 설치하면 미니맵에 아주 작은 점으로 표시되고, 횃불 주변도 원형으로 "영구히" 밝게 유지됨
/// </summary>
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
    [Tooltip("맵의 가로/세로 타일 개수. MapGenerator의 width/height와 반드시 동일해야 합니다.")]
    public Vector2Int gridSize = new Vector2Int(17, 17);
    [Tooltip("타일 1칸을 텍스처 몇 픽셀로 그릴지. 값이 클수록 원형 시야 경계가 부드러워집니다.")]
    public int pixelsPerTile = 8;

    [Header("Fog / Vision Settings")]
    [Tooltip("플레이어 기준 실시간으로 밝혀지는 반경 (타일 단위)")]
    public float playerVisionRadius = 1.5f;
    [Tooltip("횃불 기준 영구적으로 밝혀지는 반경 (타일 단위)")]
    public float torchVisionRadius = 1.2f;

    [Header("Tile Colors")]
    public Color unexploredColor = new Color(0f, 0f, 0f, 1f);
    public Color visitedTrailColor = new Color(0.35f, 0.35f, 0.35f, 1f); // ★ 지나간 타일 = 회색 흔적
    public Color floorColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    public Color wallColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    public Color pushWallColor = new Color(0.5f, 0f, 0.5f, 1f);
    public Color trapColor = new Color(0f, 0f, 1f, 1f);

    [Header("Entity Colors")]
    public Color playerColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    public Color enemyColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    public Color chestColor = new Color(0.9f, 0.9f, 0.2f, 1f);
    public Color torchColor = new Color(1f, 0.6f, 0.1f, 1f);

    [Header("Entity Marker Sizes (px, 텍스처 해상도 기준)")]
    public int playerDotRadiusPx = 3;
    public int enemyDotRadiusPx = 3;
    public int chestDotRadiusPx = 2;
    public int torchDotRadiusPx = 1;

    // ── 내부 데이터 ─────────────────────────────────────────────
    private enum TileKind { Unexplored = 0, Floor = 1, Wall = 2, Trap = 3 }

    // ★ 장소 하나 분량의 미니맵 상태를 통째로 보관하는 컨테이너.
    //   장소를 나갔다 돌아와도 이 객체가 그대로 살아있으므로 탐색 흔적이 유지됩니다.
    private class LocationMinimapState
    {
        public TileKind[,] staticMapData;
        public bool[,] visited;
        public bool[,] currentlyLitTiles;
        public Texture2D texture;
        public Color[] pixelBuffer;
        public int texWidth, texHeight;
        public readonly List<Transform> activePushables = new List<Transform>();
        public readonly List<Transform> activeChests = new List<Transform>();
        public readonly List<Transform> activeTorches = new List<Transform>();
        public readonly List<EnemyController> activeEnemies = new List<EnemyController>();
    }

    private readonly Dictionary<LocationId, LocationMinimapState> locationStates = new Dictionary<LocationId, LocationMinimapState>();
    private LocationMinimapState current;

    private Transform playerTransform;
    private readonly HashSet<Vector2Int> pushableTilesThisFrame = new HashSet<Vector2Int>();

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>런을 새로 시작(재시작 포함)할 때 WorldManager.ResetWorld에서 호출. 저장된 모든 장소 상태를 지웁니다.</summary>
    public void ResetAllLocations()
    {
        locationStates.Clear();
        current = null;
    }

    /// <summary>
    /// 장소를 새로 생성했거나 이미 방문한 장소로 되돌아올 때 WorldManager에서 호출합니다.
    /// isNewLocation이 true면 locationInstance를 스캔해 새로 그리고, false면 저장된 상태를 그대로 복원합니다.
    /// </summary>
    public void SwitchLocation(LocationId id, GameObject locationInstance, bool isNewLocation, Transform playerRef)
    {
        playerTransform = playerRef;

        if (isNewLocation || !locationStates.TryGetValue(id, out current))
        {
            current = BuildLocationState(locationInstance);
            locationStates[id] = current;
        }

        if (minimapDisplay != null) minimapDisplay.texture = current.texture;
        ApplyAspectRatio();
    }

    private LocationMinimapState BuildLocationState(GameObject locationInstance)
    {
        var state = new LocationMinimapState
        {
            staticMapData = new TileKind[gridSize.x, gridSize.y],
            visited = new bool[gridSize.x, gridSize.y],
            currentlyLitTiles = new bool[gridSize.x, gridSize.y]
        };

        if (locationInstance != null)
        {
            // 1. 장소 오브젝트를 한 번 훑으며 태그 기반으로 정적 데이터 + 동적 추적 목록 구성
            //    좌표 계산은 GridSystem.Instance.WorldToCell()로 통일
            foreach (Transform obj in locationInstance.GetComponentsInChildren<Transform>(true))
            {
                if (obj == locationInstance.transform) continue;

                Vector3Int cell = GridSystem.Instance.WorldToCell(obj.position);
                if (!IsValidGrid(cell.x, cell.z)) continue;

                if (obj.CompareTag("Wall"))
                {
                    state.staticMapData[cell.x, cell.z] = TileKind.Wall;
                }
                else if (obj.CompareTag("Floor") || obj.CompareTag("Entrance") || obj.CompareTag("Exit"))
                {
                    if (state.staticMapData[cell.x, cell.z] == TileKind.Unexplored)
                        state.staticMapData[cell.x, cell.z] = TileKind.Floor;
                }
                else if (obj.CompareTag("Trap"))
                {
                    state.staticMapData[cell.x, cell.z] = TileKind.Trap;
                }
                else if (obj.CompareTag("Pushable"))
                {
                    state.activePushables.Add(obj);
                }
                else if (obj.CompareTag("Chest"))
                {
                    state.activeChests.Add(obj);
                }
            }

            // ★ 중요: FindObjectsByType 대신 이 장소의 자식만 스캔합니다.
            //   여러 장소가 동시에(하나는 활성, 나머지는 비활성) 씬에 존재하므로,
            //   전역 검색을 쓰면 다른 층의 적까지 이 미니맵에 섞여버립니다.
            EnemyController[] enemies = locationInstance.GetComponentsInChildren<EnemyController>(true);
            state.activeEnemies.AddRange(enemies);
        }

        // 2. 텍스처 준비
        state.texWidth = gridSize.x * pixelsPerTile;
        state.texHeight = gridSize.y * pixelsPerTile;
        state.texture = new Texture2D(state.texWidth, state.texHeight, TextureFormat.RGBA32, false);
        state.texture.filterMode = FilterMode.Point;
        state.texture.wrapMode = TextureWrapMode.Clamp;
        state.pixelBuffer = new Color[state.texWidth * state.texHeight];

        return state;
    }

    /// <summary>
    /// 텍스처 비율을 AspectRatioFitter에 반영하고, 레이아웃을 즉시 강제 갱신합니다.
    /// </summary>
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

    /// <summary>
    /// 횃불을 설치했을 때 호출하여 현재 장소의 미니맵에 등록합니다. (PlaceTorchState_Player에서 호출)
    /// </summary>
    public void RegisterTorch(Transform torchTransform)
    {
        if (torchTransform != null && current != null) current.activeTorches.Add(torchTransform);
    }

    private void Update()
    {
        if (current == null || current.texture == null || playerTransform == null) return;

        // ★ 플레이어가 현재 밟고 있는 타일을 "방문함"으로 영구 기록
        Vector3Int playerCell = GridSystem.Instance.WorldToCell(playerTransform.position);
        if (IsValidGrid(playerCell.x, playerCell.z))
        {
            current.visited[playerCell.x, playerCell.z] = true;
        }

        RenderMinimap();
    }

    private void RenderMinimap()
    {
        pushableTilesThisFrame.Clear();
        for (int i = current.activePushables.Count - 1; i >= 0; i--)
        {
            Transform p = current.activePushables[i];
            if (p == null) { current.activePushables.RemoveAt(i); continue; }
            Vector3Int cell = GridSystem.Instance.WorldToCell(p.position);
            if (IsValidGrid(cell.x, cell.z)) pushableTilesThisFrame.Add(new Vector2Int(cell.x, cell.z));
        }

        // 1. 배경(방문 흔적 / 미탐사) 채우기
        for (int ty = 0; ty < gridSize.y; ty++)
        {
            for (int tx = 0; tx < gridSize.x; tx++)
            {
                current.currentlyLitTiles[tx, ty] = false;
                Color baseColor = current.visited[tx, ty] ? visitedTrailColor : unexploredColor;
                PaintTileBlock(tx, ty, baseColor);
            }
        }

        // 2. 플레이어의 실시간 원형 시야 반영
        ApplyVisionCircle(playerTransform.position, playerVisionRadius);

        // 3. 설치된 횃불들의 영구 원형 시야 반영
        for (int i = current.activeTorches.Count - 1; i >= 0; i--)
        {
            Transform torch = current.activeTorches[i];
            if (torch == null) { current.activeTorches.RemoveAt(i); continue; }
            ApplyVisionCircle(torch.position, torchVisionRadius);
        }

        // 4. 엔티티(플레이어/몬스터/상자/횃불) 점 찍기
        DrawEntities();

        current.texture.SetPixels(current.pixelBuffer);
        current.texture.Apply();
    }

    private void ApplyVisionCircle(Vector3 worldPos, float radiusInTiles)
    {
        Vector2 center = WorldToPixel(worldPos);
        float radiusPx = radiusInTiles * pixelsPerTile;

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
                if (dx * dx + dy * dy > radiusSq) continue;

                int tx = x / pixelsPerTile;
                int ty = y / pixelsPerTile;
                if (!IsValidGrid(tx, ty)) continue;

                current.currentlyLitTiles[tx, ty] = true;
                current.pixelBuffer[y * current.texWidth + x] = GetTrueTileColor(tx, ty);
            }
        }
    }

    private Color GetTrueTileColor(int tx, int ty)
    {
        if (pushableTilesThisFrame.Contains(new Vector2Int(tx, ty))) return pushWallColor;

        switch (current.staticMapData[tx, ty])
        {
            case TileKind.Wall: return wallColor;
            case TileKind.Trap: return trapColor;
            case TileKind.Floor: return floorColor;
            default: return floorColor; // 안전 기본값
        }
    }

    private void DrawEntities()
    {
        // 플레이어는 항상 표시 (자기 자신의 시야 안에는 항상 있음)
        DrawDot(playerTransform.position, playerColor, playerDotRadiusPx);

        // 몬스터 - 현재 밝혀진 타일 위에 있을 때만 표시
        for (int i = current.activeEnemies.Count - 1; i >= 0; i--)
        {
            EnemyController enemy = current.activeEnemies[i];
            if (enemy == null) { current.activeEnemies.RemoveAt(i); continue; }

            Vector3Int cell = GridSystem.Instance.WorldToCell(enemy.transform.position);
            if (IsValidGrid(cell.x, cell.z) && current.currentlyLitTiles[cell.x, cell.z])
            {
                DrawDot(enemy.transform.position, enemyColor, enemyDotRadiusPx);
            }
        }

        // 보물상자 - 현재 밝혀진 타일 위에 있을 때만 표시
        for (int i = current.activeChests.Count - 1; i >= 0; i--)
        {
            Transform chest = current.activeChests[i];
            if (chest == null) { current.activeChests.RemoveAt(i); continue; }

            Vector3Int cell = GridSystem.Instance.WorldToCell(chest.position);
            if (IsValidGrid(cell.x, cell.z) && current.currentlyLitTiles[cell.x, cell.z])
            {
                DrawDot(chest.position, chestColor, chestDotRadiusPx);
            }
        }

        // 횃불: 스스로 자기 주변을 밝히므로 항상 아주 작은 점으로 표시
        for (int i = current.activeTorches.Count - 1; i >= 0; i--)
        {
            Transform torch = current.activeTorches[i];
            if (torch == null) { current.activeTorches.RemoveAt(i); continue; }
            DrawDot(torch.position, torchColor, torchDotRadiusPx);
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
                {
                    current.pixelBuffer[y * current.texWidth + x] = color;
                }
            }
        }
    }

    /// <summary>월드 좌표(X, Z) → 미니맵 텍스처 픽셀 좌표로 변환 (연속값, 부드러운 이동/원형 표현용)</summary>
    private Vector2 WorldToPixel(Vector3 worldPos)
    {
        float tileSize = GridSystem.Instance.TileSize;
        Vector3 originWorld = GridSystem.Instance.Origin;

        // 타일 (0,0)의 '왼쪽 아래 바깥 모서리'를 텍스처 원점(0,0)에 대응시킴
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
            {
                current.pixelBuffer[row + startX + x] = color;
            }
        }
    }

    private bool IsValidGrid(int x, int y)
    {
        return x >= 0 && x < gridSize.x && y >= 0 && y < gridSize.y;
    }
}
