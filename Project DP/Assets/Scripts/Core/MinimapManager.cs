using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 미니맵 전체를 오브젝트 "하나"(RawImage + 단일 Texture2D)로 그리는 매니저.
///
/// ★ 기존 구조와 달라진 점 ★
/// - 예전: 타일마다 Image 오브젝트(17*17개) + 플레이어/몬스터/상자마다 별도의 점(Dot) UI 오브젝트.
///   → 지금: 그 전부를 매 프레임 하나의 Texture2D에 픽셀로 그려서 RawImage 한 개에만 표시.
///     (오브젝트 개수: 17*17 + 엔티티 수 → 1개)
/// - 좌표계: 자체적으로 맵 바운드를 추정하던 기존 방식 대신, 프로젝트 공통 GridSystem을 그대로 사용
///   (MapGenerator가 만든 던전과 100% 동일한 격자 기준이라 별도 보정이 필요 없음)
/// - 안개(Fog) 규칙 변경:
///     1) 플레이어가 "밟고 지나간" 타일 → 이후 계속 회색(방문 흔적)으로 남음
///     2) 실시간으로 밝혀지는 범위는 사각형(3x3)이 아니라 플레이어 중심의 "원형" 범위
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
    [Tooltip("플레이어 기준 실시간으로 밝혀지는 반경 (타일 단위, 기존 3x3 사각형을 대체)")]
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
    public Color torchColor = new Color(1f, 0.6f, 0.1f, 1f); // ★ 아주 작게 표시할 횃불 아이콘 색

    [Header("Entity Marker Sizes (px, 텍스처 해상도 기준)")]
    public int playerDotRadiusPx = 3;
    public int enemyDotRadiusPx = 3;
    public int chestDotRadiusPx = 2;
    public int torchDotRadiusPx = 1; // ★ 매우 작게

    // ── 내부 데이터 ─────────────────────────────────────────────
    private enum TileKind { Unexplored = 0, Floor = 1, Wall = 2, Trap = 3 }
    private TileKind[,] staticMapData;
    private bool[,] visited;          // ★ 플레이어가 실제로 밟은 타일 (영구 회색 흔적용)
    private bool[,] currentlyLitTiles; // 이번 프레임에 원형 시야 안에 들어온 타일 (엔티티 표시 판정용)

    private Texture2D minimapTexture;
    private Color[] pixelBuffer;
    private int texWidth, texHeight;

    private Transform playerTransform;
    private readonly List<Transform> activePushables = new List<Transform>();
    private readonly List<Transform> activeChests = new List<Transform>();
    private readonly List<Transform> activeTorches = new List<Transform>(); // ★ 런타임에 설치된 횃불들
    private readonly List<EnemyController> activeEnemies = new List<EnemyController>();
    private readonly HashSet<Vector2Int> pushableTilesThisFrame = new HashSet<Vector2Int>();

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>새 층(맵)이 생성될 때 DungeonGenerator에서 호출합니다.</summary>
    public void InitializeMinimap(GameObject mapInstance, Transform newPlayer)
    {
        staticMapData = new TileKind[gridSize.x, gridSize.y];
        visited = new bool[gridSize.x, gridSize.y];
        currentlyLitTiles = new bool[gridSize.x, gridSize.y];

        activePushables.Clear();
        activeChests.Clear();
        activeTorches.Clear();
        activeEnemies.Clear();

        // 1. 맵 오브젝트를 한 번 훑으며 태그 기반으로 정적 데이터 + 동적 추적 목록 구성
        //    ★ 좌표 계산은 GridSystem.Instance.WorldToCell()로 통일 (자체 바운드 추정 로직 제거)
        foreach (Transform obj in mapInstance.GetComponentsInChildren<Transform>())
        {
            if (obj == mapInstance.transform) continue;

            Vector3Int cell = GridSystem.Instance.WorldToCell(obj.position);
            if (!IsValidGrid(cell.x, cell.z)) continue;

            if (obj.CompareTag("Wall"))
            {
                staticMapData[cell.x, cell.z] = TileKind.Wall;
            }
            else if (obj.CompareTag("Floor") || obj.CompareTag("Entrance") || obj.CompareTag("Exit"))
            {
                if (staticMapData[cell.x, cell.z] == TileKind.Unexplored)
                    staticMapData[cell.x, cell.z] = TileKind.Floor;
            }
            else if (obj.CompareTag("Trap"))
            {
                staticMapData[cell.x, cell.z] = TileKind.Trap;
            }
            else if (obj.CompareTag("Pushable"))
            {
                activePushables.Add(obj);
            }
            else if (obj.CompareTag("Chest"))
            {
                activeChests.Add(obj);
            }
        }

        playerTransform = newPlayer;

        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        activeEnemies.AddRange(enemies);

        // 2. 텍스처 준비 (해상도가 바뀌지 않았다면 재사용)
        texWidth = gridSize.x * pixelsPerTile;
        texHeight = gridSize.y * pixelsPerTile;

        if (minimapTexture == null || minimapTexture.width != texWidth || minimapTexture.height != texHeight)
        {
            minimapTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
            minimapTexture.filterMode = FilterMode.Point;
            minimapTexture.wrapMode = TextureWrapMode.Clamp;
            pixelBuffer = new Color[texWidth * texHeight];

            if (minimapDisplay != null) minimapDisplay.texture = minimapTexture;

            ApplyAspectRatio();
        }
    }

    /// <summary>
    /// 텍스처 비율을 AspectRatioFitter에 반영하고, 레이아웃을 즉시 강제 갱신합니다.
    /// ★ 부모 패널 크기가 아직 확정되지 않은 시점(씬 시작 직후 등)에 InitializeMinimap이 호출돼도
    ///   첫 프레임부터 타일맵 전체가 잘리거나 치우치지 않고 정확히 중앙에 맞도록 보장합니다.
    /// </summary>
    private void ApplyAspectRatio()
    {
        if (minimapAspectFitter == null) return;

        minimapAspectFitter.aspectRatio = (float)texWidth / texHeight;

        RectTransform rt = minimapAspectFitter.GetComponent<RectTransform>();
        if (rt != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
    }

    /// <summary>
    /// 횃불을 설치했을 때 호출하여 미니맵에 등록합니다. (PlaceTorchState_Player에서 호출)
    /// 등록된 횃불은 그 순간부터 매 프레임 작은 점으로 표시되고, 주변이 영구적으로 밝게 유지됩니다.
    /// </summary>
    public void RegisterTorch(Transform torchTransform)
    {
        if (torchTransform != null) activeTorches.Add(torchTransform);
    }

    private void Update()
    {
        if (minimapTexture == null || playerTransform == null) return;

        // ★ 플레이어가 현재 밟고 있는 타일을 "방문함"으로 영구 기록
        Vector3Int playerCell = GridSystem.Instance.WorldToCell(playerTransform.position);
        if (IsValidGrid(playerCell.x, playerCell.z))
        {
            visited[playerCell.x, playerCell.z] = true;
        }

        RenderMinimap();
    }

    private void RenderMinimap()
    {
        // 이번 프레임의 밀리는 오브젝트 위치 캐싱 (타일 색 계산 시 여러 번 재사용)
        pushableTilesThisFrame.Clear();
        for (int i = activePushables.Count - 1; i >= 0; i--)
        {
            Transform p = activePushables[i];
            if (p == null) { activePushables.RemoveAt(i); continue; }
            Vector3Int cell = GridSystem.Instance.WorldToCell(p.position);
            if (IsValidGrid(cell.x, cell.z)) pushableTilesThisFrame.Add(new Vector2Int(cell.x, cell.z));
        }

        // 1. 배경(방문 흔적 / 미탐사) 채우기
        for (int ty = 0; ty < gridSize.y; ty++)
        {
            for (int tx = 0; tx < gridSize.x; tx++)
            {
                currentlyLitTiles[tx, ty] = false;
                Color baseColor = visited[tx, ty] ? visitedTrailColor : unexploredColor;
                PaintTileBlock(tx, ty, baseColor);
            }
        }

        // 2. 플레이어의 실시간 원형 시야 반영 (진짜 타일 색으로 덮어씀)
        ApplyVisionCircle(playerTransform.position, playerVisionRadius);

        // 3. 설치된 횃불들의 영구 원형 시야 반영
        for (int i = activeTorches.Count - 1; i >= 0; i--)
        {
            Transform torch = activeTorches[i];
            if (torch == null) { activeTorches.RemoveAt(i); continue; }
            ApplyVisionCircle(torch.position, torchVisionRadius);
        }

        // 4. 엔티티(플레이어/몬스터/상자/횃불) 점 찍기
        DrawEntities();

        minimapTexture.SetPixels(pixelBuffer);
        minimapTexture.Apply();
    }

    private void ApplyVisionCircle(Vector3 worldPos, float radiusInTiles)
    {
        Vector2 center = WorldToPixel(worldPos);
        float radiusPx = radiusInTiles * pixelsPerTile;

        int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radiusPx));
        int maxX = Mathf.Min(texWidth - 1, Mathf.CeilToInt(center.x + radiusPx));
        int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radiusPx));
        int maxY = Mathf.Min(texHeight - 1, Mathf.CeilToInt(center.y + radiusPx));

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

                currentlyLitTiles[tx, ty] = true;
                pixelBuffer[y * texWidth + x] = GetTrueTileColor(tx, ty);
            }
        }
    }

    private Color GetTrueTileColor(int tx, int ty)
    {
        if (pushableTilesThisFrame.Contains(new Vector2Int(tx, ty))) return pushWallColor;

        switch (staticMapData[tx, ty])
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
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            EnemyController enemy = activeEnemies[i];
            if (enemy == null) { activeEnemies.RemoveAt(i); continue; }

            Vector3Int cell = GridSystem.Instance.WorldToCell(enemy.transform.position);
            if (IsValidGrid(cell.x, cell.z) && currentlyLitTiles[cell.x, cell.z])
            {
                DrawDot(enemy.transform.position, enemyColor, enemyDotRadiusPx);
            }
        }

        // 보물상자 - 현재 밝혀진 타일 위에 있을 때만 표시
        for (int i = activeChests.Count - 1; i >= 0; i--)
        {
            Transform chest = activeChests[i];
            if (chest == null) { activeChests.RemoveAt(i); continue; }

            Vector3Int cell = GridSystem.Instance.WorldToCell(chest.position);
            if (IsValidGrid(cell.x, cell.z) && currentlyLitTiles[cell.x, cell.z])
            {
                DrawDot(chest.position, chestColor, chestDotRadiusPx);
            }
        }

        // ★ 횃불: 스스로 자기 주변을 밝히므로 항상 아주 작은 점으로 표시
        for (int i = activeTorches.Count - 1; i >= 0; i--)
        {
            Transform torch = activeTorches[i];
            if (torch == null) { activeTorches.RemoveAt(i); continue; }
            DrawDot(torch.position, torchColor, torchDotRadiusPx);
        }
    }

    private void DrawDot(Vector3 worldPos, Color color, int radiusPx)
    {
        Vector2 center = WorldToPixel(worldPos);

        int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radiusPx));
        int maxX = Mathf.Min(texWidth - 1, Mathf.CeilToInt(center.x + radiusPx));
        int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radiusPx));
        int maxY = Mathf.Min(texHeight - 1, Mathf.CeilToInt(center.y + radiusPx));

        float radiusSq = radiusPx * radiusPx;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x + 0.5f - center.x;
                float dy = y + 0.5f - center.y;
                if (dx * dx + dy * dy <= radiusSq)
                {
                    pixelBuffer[y * texWidth + x] = color;
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
            int row = (startY + y) * texWidth;
            for (int x = 0; x < pixelsPerTile; x++)
            {
                pixelBuffer[row + startX + x] = color;
            }
        }
    }

    private bool IsValidGrid(int x, int y)
    {
        return x >= 0 && x < gridSize.x && y >= 0 && y < gridSize.y;
    }
}