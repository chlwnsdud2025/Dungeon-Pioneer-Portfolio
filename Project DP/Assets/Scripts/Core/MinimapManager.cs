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
    [Tooltip("타일 1칸을 텍스처 몇 픽셀로 그릴지. 값이 클수록 원형 시야 경계가 부드러워지지만 계산량이 늘어납니다.")]
    public int pixelsPerTile = 8;

    [Header("개척도")]
    [Tooltip("밟을 수 있는 칸의 바닥 면적 중 이 비율 이상을 밝히면 '개척 완료'로 봅니다. " +
             "밀 수 있는 오브젝트 밑이나 구석처럼 시야가 닿지 않는 곳이 있어 100%가 안 나올 수 있으니 " +
             "실제로 한 층을 다 돌아본 뒤 도달 가능한 값으로 맞춰주세요.")]
    [Range(0f, 1f)] public float explorationCompleteThreshold = 0.9f;

    [Header("Fog / Vision Settings")]
    [Tooltip("플레이어 주변을 지금 밝히는 반경 (타일 단위). 벽과 밀 수 있는 오브젝트에 가려집니다.")]
    public float playerVisionRadius = 1.5f;
    [Tooltip("켜진 횃불 주변을 밝히는 반경 (타일 단위). 횃불이 꺼지면 그 범위는 '밝혀졌던 곳'으로 남습니다.")]
    public float torchVisionRadius = 1f;
    [Tooltip("벽을 표시할 두께 (타일 단위). 시야가 닿은 벽 표면만 이 두께로 그립니다. 층을 새로 만들 때 적용됩니다.")]
    [Range(0.05f, 0.5f)] public float wallFaceThickness = 0.3f;
    [Tooltip("광원에서 멀어질수록 얼마나 밝은지. 플레이어와 횃불에 함께 쓰입니다.\n" +
             "가로축: 0 = 광원 위치, 1 = 반경 끝\n" +
             "세로축: 1 = '지금 보이는' 색, 0 = '밝혀졌던' 색\n" +
             "끝을 0으로 두면 시야 경계가 밝혀졌던 색으로 자연스럽게 이어집니다.")]
    public AnimationCurve lightFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Tile Colors")]
    [Tooltip("한 번도 밝혀지지 않은 곳, 벽 안쪽")]
    public Color unexploredColor = new Color(0f, 0f, 0f, 1f);
    [Tooltip("밝혀졌던 바닥 (지금은 시야 밖)")]
    public Color visitedTrailColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    [Tooltip("지금 보이는 바닥")]
    public Color floorColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    [Tooltip("지금 보이는 벽면")]
    public Color litWallColor = new Color(0.84f, 0.68f, 0.45f, 1f);
    [Tooltip("봤던 벽면 (지금은 시야 밖)")]
    public Color seenWallColor = new Color(0.42f, 0.34f, 0.22f, 1f);

    [Header("Marker Icons")]
    [Tooltip("적·상자·함정·밀 수 있는 오브젝트는 그 자리가 받는 빛만큼 진하게 그립니다. " +
             "밝기(0~1, lightFalloff 기준)가 이 값 이상이면 또렷하고, 그보다 어두운 시야 가장자리에서는 옅어지며, " +
             "벽에 가려져 빛이 안 닿으면 사라집니다.")]
    [Range(0.01f, 1f)] public float iconFullOpacityLight = 0.3f;
    [Tooltip("아이콘이 나타나거나 사라지는 데 걸리는 시간(초). 벽 뒤로 들어가는 순간처럼 밝기가 갑자기 바뀌어도 서서히 바뀝니다.")]
    [Min(0f)] public float iconFadeSeconds = 0.25f;
    [Tooltip("횃불 아이콘을 벽 표면에서 바닥 쪽으로 얼마나 들여 그릴지 (타일 단위). 0이면 벽 표면 위에 걸칩니다.")]
    [Range(0f, 0.5f)] public float torchIconInset = 0.35f;
    [Tooltip("적. 지금 비춰질 때만 그립니다.")]
    public MarkerIcon enemyIcon = new MarkerIcon();
    [Tooltip("상자. 지금 비춰질 때만 그립니다.")]
    public MarkerIcon chestIcon = new MarkerIcon();
    [Tooltip("횃불. 항상 그립니다.")]
    public MarkerIcon torchIcon = new MarkerIcon();
    [Tooltip("묘비. 항상 그립니다.")]
    public MarkerIcon graveIcon = new MarkerIcon();
    [Tooltip("함정(구덩이, 발판). 칸이 지금 비춰질 때만 그리고, 메워진 구덩이는 그리지 않습니다.")]
    public MarkerIcon trapIcon = new MarkerIcon();
    [Tooltip("밀 수 있는 오브젝트. 지금 비춰질 때만 그립니다.")]
    public MarkerIcon pushableIcon = new MarkerIcon();

    [Header("Marker Colors (플레이어 점, 그리고 아이콘이 없을 때 대신 그리는 점)")]
    public Color playerColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    public Color enemyColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    public Color chestColor = new Color(0.9f, 0.9f, 0.2f, 1f);
    public Color torchColor = new Color(1f, 0.6f, 0.1f, 1f);
    [Tooltip("묘비 아이콘이 없으면 묘비 칸 전체를 이 색으로 칠합니다.")]
    public Color graveColor = new Color(0.65f, 0.2f, 0.9f, 1f);
    public Color trapColor = new Color(0f, 0f, 1f, 1f);
    public Color pushWallColor = new Color(1f, 0.55f, 0.1f, 1f);

    [Header("Marker Dot Sizes (px, 플레이어 점과 아이콘이 없을 때의 점)")]
    public int playerDotRadiusPx = 3;
    public int enemyDotRadiusPx = 3;
    public int chestDotRadiusPx = 2;
    public int torchDotRadiusPx = 1;

    [System.Serializable]
    public class MarkerIcon
    {
        [Tooltip("미니맵 위에 얹을 이미지. 정사각형·투명 배경을 권장하며, Import Settings에서 Read/Write를 켜야 합니다. " +
                 "비워 두면 대체 색 점으로 그립니다.")]
        public Sprite sprite;
        [Tooltip("아이콘 한 변의 크기 (텍스처 픽셀). 한 칸이 pixelsPerTile 픽셀입니다.")]
        [Min(1)] public int sizePx = 16;
        [Tooltip("이미지에 곱할 색. 흰색이면 이미지 그대로입니다.")]
        public Color tint = Color.white;
    }

    // ── 내부 데이터 ─────────────────────────────────────────────
    private class LocationMinimapState
    {
        public LocationGrid grid;
        public MinimapVisionMap vision;
        public Texture2D texture;
        public Color32[] terrainBuffer;   // 시야·탐색 상태만 칠한 바탕. 바뀐 범위만 다시 칠합니다.
        public Color32[] frameBuffer;     // 바탕 + 마커. 텍스처에 올리는 최종본
        public int texWidth, texHeight;

        public readonly List<PushableObject> pushables = new List<PushableObject>();
        public readonly List<Transform> chests = new List<Transform>();
        public readonly List<Transform> torches = new List<Transform>();
        public readonly List<EnemyController> enemies = new List<EnemyController>();
        public readonly List<PlayerGrave> graves = new List<PlayerGrave>();
        public readonly List<Vector2Int> trapCells = new List<Vector2Int>();

        // 지금 화면에 보이는 아이콘 진하기 (0~1). 목표 진하기를 향해 iconFadeSeconds에 걸쳐 따라갑니다.
        public readonly Dictionary<int, float> objectOpacity = new Dictionary<int, float>();          // 오브젝트 InstanceID
        public readonly Dictionary<Vector2Int, float> trapOpacity = new Dictionary<Vector2Int, float>(); // 함정 칸

        // 다시 계산할지 판단하는 값들
        public Vector2Int lastPlayerPixel = new Vector2Int(int.MinValue, int.MinValue);
        public float lastPlayerRadius = -1f;
        public float lastTorchRadius = -1f;
        public int lastTorchCount = -1;
        public bool torchesDirty = true;
        public bool terrainDirtyAll = true;
        public RectInt terrainDirtyRect;
        public bool hasFrame;
        public int lastMarkerSignature;
    }

    private struct Marker
    {
        public Vector2 center;   // 픽셀 좌표
        public IconImage icon;   // 있으면 아이콘을 그리고, 없으면 아래 대체 모양으로 그립니다.
        public int radius;       // 점: 반지름, 칸: 한 변의 절반
        public Color32 color;
        public bool isTile;
        public float opacity;    // 0~1
    }

    /// <summary>아이콘 칸의 sizePx 크기로 줄이고 색을 곱해 둔 아이콘 픽셀. (아래가 0번 줄)</summary>
    private class IconImage
    {
        public Color32[] pixels;
        public int size;
    }

    private readonly Dictionary<LocationId, LocationMinimapState> locationStates = new Dictionary<LocationId, LocationMinimapState>();
    private LocationMinimapState current;
    private GameObject currentLocationInstance;
    private Transform playerTransform;

    private readonly List<Vector2Int> blockerCells = new List<Vector2Int>();
    private readonly List<Vector2> torchCenters = new List<Vector2>();
    private readonly List<Marker> markers = new List<Marker>();

    // lightFalloff를 픽셀마다 Evaluate하지 않도록 미리 뽑아 둔 밝기표. (MinimapVisionMap.LightPlayer 참고)
    private const int FalloffTableSize = 256;
    private byte[] falloffTable;

    // 아이콘 설정 → 줄여 둔 픽셀. 읽기에 실패한 아이콘은 null로 기억해 에러를 한 번만 띄웁니다.
    private readonly Dictionary<MarkerIcon, IconImage> iconCache = new Dictionary<MarkerIcon, IconImage>();

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

    // 플레이 중 인스펙터에서 색·밝기 곡선·아이콘을 바꾸면 바로 다시 칠합니다.
    private void OnValidate()
    {
        falloffTable = null;
        iconCache.Clear();
        if (current == null) return;

        current.torchesDirty = true;
        current.lastPlayerRadius = -1f;
        current.terrainDirtyAll = true;
        current.hasFrame = false;
    }

    private byte[] FalloffTable
    {
        get
        {
            if (falloffTable != null) return falloffTable;

            // 픽셀마다 제곱근을 구하지 않도록 칸을 '거리 제곱 비율'로 나눕니다. 반경 안은 최소 1 = '보임'입니다.
            falloffTable = new byte[FalloffTableSize];
            for (int k = 0; k < FalloffTableSize; k++)
            {
                float distance = Mathf.Sqrt(k / (float)(FalloffTableSize - 1));
                float brightness = lightFalloff != null ? Mathf.Clamp01(lightFalloff.Evaluate(distance)) : 1f;
                falloffTable[k] = (byte)(1 + Mathf.RoundToInt(brightness * 254f));
            }

            return falloffTable;
        }
    }

    public void ResetAllLocations()
    {
        locationStates.Clear();
        current = null;
    }

    public Texture CurrentTexture => current != null ? current.texture : null;

    public float CurrentAspect => (current != null && current.texHeight > 0) ? (float)current.texWidth / current.texHeight : 1f;

    public bool HasCurrentLocation => current != null && current.vision.FloorPixelCount > 0;

    /// <summary>밝혀진 바닥 면적 비율 (0~1).</summary>
    public float CurrentExploration => HasCurrentLocation ? current.vision.ExploredRatio : 0f;

    public bool IsCurrentExplored => HasCurrentLocation && CurrentExploration >= explorationCompleteThreshold;

    public bool TryCaptureExploration(LocationId id, out MinimapExplorationRecord record)
    {
        record = null;
        if (!locationStates.TryGetValue(id, out LocationMinimapState state)) return false;

        record = state.vision.Capture();
        return true;
    }

    public void RestoreExploration(LocationId id, MinimapExplorationRecord record)
    {
        if (!locationStates.TryGetValue(id, out LocationMinimapState state)) return;
        if (!state.vision.Restore(record)) return;

        state.terrainDirtyAll = true;
        state.hasFrame = false;
    }

    public void SwitchLocation(LocationId id, GameObject locationInstance, bool isNewLocation, Transform playerRef)
    {
        playerTransform = playerRef;
        currentLocationInstance = locationInstance;

        if (isNewLocation || !locationStates.TryGetValue(id, out current))
        {
            current = BuildLocationState(locationInstance);
            locationStates[id] = current;
        }

        // 다시 들어온 층이면 떠나 있던 동안의 변화(묘비 등)를 반영하도록 처음부터 다시 계산합니다.
        CollectGraves();
        current.lastPlayerPixel = new Vector2Int(int.MinValue, int.MinValue);
        current.torchesDirty = true;
        current.terrainDirtyAll = true;
        current.hasFrame = false;

        ApplyAspectRatio();
        if (playerTransform != null) Refresh(0f);
    }

    private LocationMinimapState BuildLocationState(GameObject locationInstance)
    {
        LocationGrid grid = ResolveGrid(locationInstance);
        var vision = new MinimapVisionMap(grid, pixelsPerTile, wallFaceThickness);

        var state = new LocationMinimapState
        {
            grid = grid,
            vision = vision,
            texWidth = vision.Width,
            texHeight = vision.Height
        };

        foreach (PushableObject pushable in grid.Pushables)
        {
            if (pushable != null) state.pushables.Add(pushable);
        }

        state.chests.AddRange(grid.Chests);

        for (int z = 0; z < grid.Size.y; z++)
        {
            for (int x = 0; x < grid.Size.x; x++)
            {
                if (LocationGrid.IsTrapTerrain(grid.Cells[x, z])) state.trapCells.Add(new Vector2Int(x, z));
            }
        }

        if (locationInstance != null)
        {
            state.enemies.AddRange(locationInstance.GetComponentsInChildren<EnemyController>(true));

            foreach (TorchInstance torch in locationInstance.GetComponentsInChildren<TorchInstance>(true))
                state.torches.Add(torch.transform);
        }

        state.texture = new Texture2D(state.texWidth, state.texHeight, TextureFormat.RGBA32, false);
        state.texture.filterMode = FilterMode.Point;
        state.texture.wrapMode = TextureWrapMode.Clamp;
        state.terrainBuffer = new Color32[state.texWidth * state.texHeight];
        state.frameBuffer = new Color32[state.texWidth * state.texHeight];

        return state;
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
        if (current.torches.Contains(torchTransform)) return;

        current.torches.Add(torchTransform);
        current.torchesDirty = true;
    }

    public void RegisterGrave(Transform graveTransform)
    {
        if (graveTransform == null || current == null) return;

        PlayerGrave grave = graveTransform.GetComponent<PlayerGrave>();
        if (grave != null && !current.graves.Contains(grave)) current.graves.Add(grave);
    }

    /// <summary>묘비를 없애기 직전에 불립니다. 목록에서 바로 빼서 보라색 표식이 남지 않게 합니다.</summary>
    public void UnregisterGrave(Transform graveTransform)
    {
        if (graveTransform == null || current == null) return;

        PlayerGrave grave = graveTransform.GetComponent<PlayerGrave>();
        if (grave != null) current.graves.Remove(grave);
    }

    public void RefreshGraves()
    {
        if (current == null) return;
        CollectGraves();
    }

    private void CollectGraves()
    {
        current.graves.Clear();
        if (currentLocationInstance == null) return;

        current.graves.AddRange(currentLocationInstance.GetComponentsInChildren<PlayerGrave>(false));
    }

    private void Update()
    {
        if (current == null || current.texture == null || playerTransform == null) return;
        Refresh(Time.deltaTime);
    }

    /// <param name="deltaTime">아이콘이 서서히 나타나고 사라지는 데 쓰는 경과 시간. 층을 막 옮겼을 때는 0.</param>
    private void Refresh(float deltaTime)
    {
        UpdateVision();
        RenderIfChanged(deltaTime);
    }

    // ── 시야 ────────────────────────────────────────────────

    private void UpdateVision()
    {
        MinimapVisionMap vision = current.vision;

        // 1. 밀 수 있는 오브젝트가 선 칸은 시야를 막습니다. 옮겨지면 모든 광원을 다시 계산합니다.
        CollectBlockerCells();
        bool blockersChanged = vision.SetDynamicBlockers(blockerCells);

        // 2. 횃불: 다 타서 파괴되면 목록에서 빠지고, 그 범위는 '밝혀졌던 곳'으로 남습니다.
        current.torches.RemoveAll(t => t == null);
        if (blockersChanged || current.torches.Count != current.lastTorchCount
            || !Mathf.Approximately(current.lastTorchRadius, torchVisionRadius))
        {
            current.torchesDirty = true;
        }

        if (current.torchesDirty)
        {
            torchCenters.Clear();
            foreach (Transform torch in current.torches)
                torchCenters.Add(TorchLightCenter(torch));

            vision.LightTorches(torchCenters, torchVisionRadius, FalloffTable);

            current.torchesDirty = false;
            current.lastTorchCount = current.torches.Count;
            current.lastTorchRadius = torchVisionRadius;
            current.terrainDirtyAll = true;
        }

        // 3. 플레이어: 픽셀 한 칸 이상 움직였을 때만 다시 계산합니다.
        Vector2 playerTile = WorldToTile(playerTransform.position);
        Vector2Int playerPixel = Vector2Int.FloorToInt(playerTile * pixelsPerTile);

        if (blockersChanged || playerPixel != current.lastPlayerPixel
            || !Mathf.Approximately(current.lastPlayerRadius, playerVisionRadius))
        {
            MarkTerrainDirty(vision.LightPlayer(playerTile, playerVisionRadius, FalloffTable));
            current.lastPlayerPixel = playerPixel;
            current.lastPlayerRadius = playerVisionRadius;
        }
    }

    /// <summary>
    /// 횃불은 벽 표면에 붙어 있어 위치가 벽 칸 안쪽으로 떨어질 수 있습니다. 그러면 얇은 벽 너머 방까지 빛이 새므로,
    /// 횃불이 바라보는 바닥 칸(ApproachCell) 안으로 광원을 끌어옵니다.
    /// </summary>
    private Vector2 TorchLightCenter(Transform torch)
    {
        Vector2 center = WorldToTile(torch.position);

        TorchInstance instance = torch.GetComponent<TorchInstance>();
        if (instance == null || instance.FaceNormal == Vector3.zero) return center;

        const float inset = 0.02f;
        Vector2Int cell = instance.ApproachCell;
        center.x = Mathf.Clamp(center.x, cell.x + inset, cell.x + 1 - inset);
        center.y = Mathf.Clamp(center.y, cell.y + inset, cell.y + 1 - inset);
        return center;
    }

    private void CollectBlockerCells()
    {
        blockerCells.Clear();

        for (int i = current.pushables.Count - 1; i >= 0; i--)
        {
            if (current.pushables[i] == null) current.pushables.RemoveAt(i);
        }

        // 함정에 빠져 바닥을 메운(IsLocked) 오브젝트는 더 이상 시야를 막지 않습니다.
        foreach (PushableObject pushable in current.pushables)
        {
            if (IsBlockingPushable(pushable)) blockerCells.Add(pushable.CurrentCell);
        }
    }

    private static bool IsBlockingPushable(PushableObject pushable)
    {
        return pushable.gameObject.activeInHierarchy && !pushable.IsLocked && pushable.transform.position.y > -5f;
    }

    private void MarkTerrainDirty(RectInt rect)
    {
        if (rect.width <= 0 || rect.height <= 0) return;

        RectInt dirty = current.terrainDirtyRect;
        if (dirty.width <= 0 || dirty.height <= 0)
        {
            current.terrainDirtyRect = rect;
            return;
        }

        int xMin = Mathf.Min(dirty.xMin, rect.xMin), yMin = Mathf.Min(dirty.yMin, rect.yMin);
        int xMax = Mathf.Max(dirty.xMax, rect.xMax), yMax = Mathf.Max(dirty.yMax, rect.yMax);
        current.terrainDirtyRect = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    // ── 그리기 ──────────────────────────────────────────────

    /// <summary>시야가 바뀌었거나 보이는 마커가 움직이거나 옅어지는 중일 때만 텍스처를 다시 올립니다.</summary>
    private void RenderIfChanged(float deltaTime)
    {
        bool terrainChanged = PaintTerrain();

        int signature = CollectMarkers(deltaTime);
        if (!terrainChanged && current.hasFrame && signature == current.lastMarkerSignature) return;

        System.Array.Copy(current.terrainBuffer, current.frameBuffer, current.frameBuffer.Length);
        foreach (Marker marker in markers)
        {
            if (marker.icon != null) DrawIcon(marker.center, marker.icon, marker.opacity);
            else if (marker.isTile) DrawSquare(marker.center, marker.radius, marker.color);
            else DrawDot(marker.center, marker.radius, marker.color, marker.opacity);
        }

        current.texture.SetPixels32(current.frameBuffer);
        current.texture.Apply();

        current.hasFrame = true;
        current.lastMarkerSignature = signature;
    }

    private bool PaintTerrain()
    {
        RectInt rect;
        if (current.terrainDirtyAll) rect = new RectInt(0, 0, current.texWidth, current.texHeight);
        else if (current.terrainDirtyRect.width > 0 && current.terrainDirtyRect.height > 0) rect = current.terrainDirtyRect;
        else return false;

        MinimapVisionMap vision = current.vision;
        Color32 unexplored = unexploredColor, trail = visitedTrailColor, floor = floorColor;
        Color32 litWall = litWallColor, seenWall = seenWallColor;

        // 비춰진 곳은 광원에서 멀수록 '밝혀졌던' 색에 가까워지고, 반경 끝에서 그 색과 이어집니다.
        // 함정·밀 수 있는 오브젝트 칸도 여기서는 그냥 바닥이고, 그 위에 아이콘만 얹습니다.
        for (int y = rect.yMin; y < rect.yMax; y++)
        {
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                int i = y * current.texWidth + x;
                byte light = vision.SurfaceLight(x, y);
                Color32 color;

                switch (vision.KindAt(i))
                {
                    case MinimapVisionMap.PixelKind.Floor:
                        color = light > 0 ? Blend(trail, floor, light) : vision.IsExploredFloor(i) ? trail : unexplored;
                        break;
                    case MinimapVisionMap.PixelKind.WallFace:
                        color = light > 0 ? Blend(seenWall, litWall, light) : vision.IsSeenWall(i) ? seenWall : unexplored;
                        break;
                    default:
                        color = unexplored;
                        break;
                }

                current.terrainBuffer[i] = color;
            }
        }

        current.terrainDirtyAll = false;
        current.terrainDirtyRect = default;
        return true;
    }

    /// <summary>from에서 to로 weight(0~255)만큼 섞습니다. 255면 to 그대로입니다.</summary>
    private static Color32 Blend(Color32 from, Color32 to, byte weight)
    {
        int w = weight, rest = 255 - weight;
        return new Color32(
            (byte)((from.r * rest + to.r * w) / 255),
            (byte)((from.g * rest + to.g * w) / 255),
            (byte)((from.b * rest + to.b * w) / 255),
            (byte)((from.a * rest + to.a * w) / 255));
    }

    /// <summary>
    /// 이번 프레임에 그릴 마커를 모읍니다. 묘비·횃불·플레이어는 항상 또렷하게,
    /// 나머지는 그 자리가 받는 빛만큼 진하게 그립니다 (벽에 가려지면 빛이 안 닿아 사라짐).
    /// 나중에 넣은 것이 위에 그려집니다: 묘비 → 함정 → 상자 → 밀 수 있는 오브젝트 → 횃불 → 적 → 플레이어.
    /// 돌려주는 값은 마커 배치의 요약값이라, 지난 프레임과 같으면 다시 그릴 필요가 없습니다.
    /// </summary>
    private int CollectMarkers(float deltaTime)
    {
        markers.Clear();
        int signature = 17;
        int ppt = pixelsPerTile;
        MinimapVisionMap vision = current.vision;

        current.graves.RemoveAll(g => g == null);
        foreach (PlayerGrave grave in current.graves)
        {
            if (!grave.isActiveAndEnabled) continue;

            Vector3Int cell = GridSystem.Instance.WorldToCell(grave.transform.position);
            Vector2 center = new Vector2((cell.x + 0.5f) * ppt, (cell.z + 0.5f) * ppt);
            AddMarker(ref signature, center, graveIcon, graveColor, ppt / 2, true, 1f);   // 아이콘이 없으면 예전처럼 칸을 칠함
        }

        foreach (Vector2Int cell in current.trapCells)
        {
            // 메워진 구덩이는 목표 진하기 0 → 서서히 사라집니다.
            float target = IsTrapActive(cell) ? LightToOpacity(vision.CellLight(cell)) : 0f;
            float opacity = Fade(current.trapOpacity, cell, target, deltaTime);
            if (opacity <= 0f) continue;

            Vector2 center = new Vector2((cell.x + 0.5f) * ppt, (cell.y + 0.5f) * ppt);
            AddMarker(ref signature, center, trapIcon, trapColor, chestDotRadiusPx, false, opacity);
        }

        for (int i = current.chests.Count - 1; i >= 0; i--)
        {
            Transform chest = current.chests[i];
            if (chest == null) { current.chests.RemoveAt(i); continue; }

            float opacity = Fade(current.objectOpacity, chest.GetInstanceID(), LightToOpacity(LightAt(chest.position)), deltaTime);
            if (opacity > 0f)
                AddMarker(ref signature, WorldToPixel(chest.position), chestIcon, chestColor, chestDotRadiusPx, false, opacity);
        }

        foreach (PushableObject pushable in current.pushables)
        {
            float target = IsBlockingPushable(pushable) ? LightToOpacity(vision.CellLight(pushable.CurrentCell)) : 0f;
            float opacity = Fade(current.objectOpacity, pushable.GetInstanceID(), target, deltaTime);
            if (opacity <= 0f) continue;

            // 밀리는 동안 부드럽게 따라가도록 칸이 아니라 실제 위치에 그립니다.
            AddMarker(ref signature, WorldToPixel(pushable.transform.position), pushableIcon, pushWallColor,
                      Mathf.Max(3, chestDotRadiusPx + 1), false, opacity);
        }

        foreach (Transform torch in current.torches)
            AddMarker(ref signature, TorchIconPixel(torch), torchIcon, torchColor, torchDotRadiusPx, false, 1f);

        for (int i = current.enemies.Count - 1; i >= 0; i--)
        {
            EnemyController enemy = current.enemies[i];
            if (enemy == null) { current.enemies.RemoveAt(i); continue; }

            Vector3 pos = enemy.transform.position;
            float target = enemy.gameObject.activeInHierarchy ? LightToOpacity(LightAt(pos)) : 0f;
            float opacity = Fade(current.objectOpacity, enemy.GetInstanceID(), target, deltaTime);
            if (opacity > 0f)
                AddMarker(ref signature, WorldToPixel(pos), enemyIcon, enemyColor, enemyDotRadiusPx, false, opacity);
        }

        AddMarker(ref signature, WorldToPixel(playerTransform.position), null, playerColor, playerDotRadiusPx, false, 1f);

        return signature;
    }

    /// <summary>아이콘이 있으면 아이콘으로, 없으면 대체 모양(점 또는 칸)으로 그릴 마커를 넣습니다.</summary>
    private void AddMarker(ref int signature, Vector2 center, MarkerIcon iconSetting, Color fallbackColor, int fallbackRadius,
                           bool fallbackIsTile, float opacity)
    {
        IconImage icon = GetIcon(iconSetting);
        markers.Add(new Marker
        {
            center = center,
            icon = icon,
            radius = fallbackRadius,
            color = fallbackColor,
            isTile = fallbackIsTile,
            opacity = opacity
        });

        unchecked
        {
            signature = signature * 31 + Mathf.FloorToInt(center.x);
            signature = signature * 31 + Mathf.FloorToInt(center.y);
            signature = signature * 31 + (icon != null ? icon.GetHashCode() : fallbackRadius * 2 + (fallbackIsTile ? 1 : 0));
            signature = signature * 31 + Mathf.RoundToInt(opacity * 64f);
        }
    }

    /// <summary>그 위치가 지금 받는 빛의 밝기 (0 = 빛이 안 닿음, 벽에 가려진 곳 포함).</summary>
    private byte LightAt(Vector3 worldPos)
    {
        Vector2Int pixel = Vector2Int.FloorToInt(WorldToPixel(worldPos));
        if (pixel.x < 0 || pixel.y < 0 || pixel.x >= current.texWidth || pixel.y >= current.texHeight) return 0;

        return current.vision.SurfaceLight(pixel.x, pixel.y);
    }

    /// <summary>빛의 밝기를 아이콘 진하기로 바꿉니다. iconFullOpacityLight 이상이면 또렷하고, 그 아래에서는 옅어집니다.</summary>
    private float LightToOpacity(byte light)
    {
        if (light == 0) return 0f;
        return Mathf.Clamp01(light / 255f / iconFullOpacityLight);
    }

    /// <summary>
    /// 지금 보이는 진하기를 목표 진하기 쪽으로 iconFadeSeconds에 걸쳐 옮기고, 새 진하기를 돌려줍니다.
    /// 벽 뒤로 들어가는 순간처럼 목표가 갑자기 바뀌어도 아이콘이 튀지 않습니다.
    /// </summary>
    private float Fade<TKey>(Dictionary<TKey, float> shownOpacity, TKey key, float target, float deltaTime)
    {
        shownOpacity.TryGetValue(key, out float shown);

        float step = iconFadeSeconds > 0f ? deltaTime / iconFadeSeconds : 1f;
        shown = Mathf.MoveTowards(shown, target, step);

        if (shown <= 0f) shownOpacity.Remove(key);
        else shownOpacity[key] = shown;

        return shown;
    }

    /// <summary>밀 수 있는 오브젝트로 메운 구덩이는 더 이상 함정이 아니므로 표시하지 않습니다. 발판은 항상 함정입니다.</summary>
    private bool IsTrapActive(Vector2Int cell)
    {
        return current.grid.Get(cell) == CellType.Plate || current.grid.IsPitOpen(cell);
    }

    /// <summary>
    /// 횃불 아이콘 위치. 횃불은 벽 표면에 붙어 있어 그대로 그리면 절반이 벽에 걸리므로,
    /// 붙어 있는 면의 바깥 방향(바닥 쪽)으로 torchIconInset만큼 들여 그립니다.
    /// </summary>
    private Vector2 TorchIconPixel(Transform torch)
    {
        Vector2 point = WorldToTile(torch.position);

        TorchInstance instance = torch.GetComponent<TorchInstance>();
        if (instance != null)
        {
            Vector3 normal = instance.FaceNormal;
            point += new Vector2(normal.x, normal.z) * torchIconInset;
        }

        return point * pixelsPerTile;
    }

    // ── 아이콘 ──────────────────────────────────────────────

    private IconImage GetIcon(MarkerIcon setting)
    {
        if (setting == null || setting.sprite == null) return null;

        if (!iconCache.TryGetValue(setting, out IconImage icon))
        {
            icon = BuildIcon(setting);
            iconCache[setting] = icon;
        }

        return icon;
    }

    /// <summary>스프라이트 픽셀을 읽어 아이콘 칸의 sizePx 크기로 줄여 둡니다. 읽을 수 없으면 에러를 남기고 null.</summary>
    private IconImage BuildIcon(MarkerIcon setting)
    {
        Sprite sprite = setting.sprite;
        Texture2D source = sprite.texture;
        if (source == null || !source.isReadable)
        {
            Debug.LogError($"[MinimapManager] 미니맵 아이콘 '{sprite.name}'의 픽셀을 읽을 수 없어 대신 점으로 그립니다. " +
                           "Project 창에서 이미지를 선택하고 Import Settings의 Read/Write를 켜 주세요.", sprite);
            return null;
        }

        Rect rect = sprite.textureRect;
        int srcW = Mathf.Max(1, Mathf.RoundToInt(rect.width));
        int srcH = Mathf.Max(1, Mathf.RoundToInt(rect.height));
        Color[] src = source.GetPixels(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), srcW, srcH);

        int size = Mathf.Max(1, setting.sizePx);
        return new IconImage { pixels = ShrinkIcon(src, srcW, srcH, size, setting.tint), size = size };
    }

    /// <summary>
    /// 이미지를 size × size로 줄이고(영역 평균) 색을 곱합니다. 원본이 더 작으면 가장 가까운 픽셀로 늘립니다.
    /// 가장자리의 투명 픽셀 색이 섞여 테두리가 탁해지지 않도록 투명도로 가중해 평균을 냅니다.
    /// </summary>
    private static Color32[] ShrinkIcon(Color[] src, int srcW, int srcH, int size, Color tint)
    {
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            int y0 = y * srcH / size, y1 = Mathf.Max(y0 + 1, (y + 1) * srcH / size);

            for (int x = 0; x < size; x++)
            {
                int x0 = x * srcW / size, x1 = Mathf.Max(x0 + 1, (x + 1) * srcW / size);
                float r = 0f, g = 0f, b = 0f, a = 0f;

                for (int sy = y0; sy < y1; sy++)
                {
                    for (int sx = x0; sx < x1; sx++)
                    {
                        Color c = src[sy * srcW + sx];
                        r += c.r * c.a;
                        g += c.g * c.a;
                        b += c.b * c.a;
                        a += c.a;
                    }
                }

                if (a <= 0f) continue;   // 완전히 투명 (Color32 기본값)

                float coverage = a / ((x1 - x0) * (y1 - y0));
                pixels[y * size + x] = new Color(r / a * tint.r, g / a * tint.g, b / a * tint.b, coverage * tint.a);
            }
        }

        return pixels;
    }

    private void DrawIcon(Vector2 center, IconImage icon, float opacity)
    {
        int size = icon.size;
        int left = Mathf.RoundToInt(center.x - size * 0.5f);
        int bottom = Mathf.RoundToInt(center.y - size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            int ty = bottom + y;
            if (ty < 0 || ty >= current.texHeight) continue;

            for (int x = 0; x < size; x++)
            {
                int tx = left + x;
                if (tx < 0 || tx >= current.texWidth) continue;

                Color32 src = icon.pixels[y * size + x];
                Composite(ty * current.texWidth + tx, src, Mathf.RoundToInt(src.a * opacity));
            }
        }
    }

    /// <summary>frameBuffer의 픽셀 위에 src를 alpha(0~255)만큼 덮습니다. 미니맵 텍스처는 늘 불투명하게 유지합니다.</summary>
    private void Composite(int index, Color32 src, int alpha)
    {
        if (alpha <= 0) return;

        Color32 result = alpha >= 255 ? src : Blend(current.frameBuffer[index], src, (byte)alpha);
        result.a = 255;
        current.frameBuffer[index] = result;
    }

    private void DrawDot(Vector2 center, int radiusPx, Color32 color, float opacity)
    {
        int alpha = Mathf.RoundToInt(opacity * 255f);
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
                    Composite(y * current.texWidth + x, color, alpha);
            }
        }
    }

    private void DrawSquare(Vector2 center, int halfSize, Color32 color)
    {
        int minX = Mathf.Max(0, Mathf.RoundToInt(center.x - halfSize));
        int maxX = Mathf.Min(current.texWidth, Mathf.RoundToInt(center.x + halfSize));
        int minY = Mathf.Max(0, Mathf.RoundToInt(center.y - halfSize));
        int maxY = Mathf.Min(current.texHeight, Mathf.RoundToInt(center.y + halfSize));

        for (int y = minY; y < maxY; y++)
        {
            int row = y * current.texWidth;
            for (int x = minX; x < maxX; x++)
                current.frameBuffer[row + x] = color;
        }
    }

    /// <summary>월드 좌표 → 타일 공간 (1 = 한 칸, 칸 (x, z)는 [x, x+1)).</summary>
    private Vector2 WorldToTile(Vector3 worldPos)
    {
        float tileSize = GridSystem.Instance.TileSize;
        Vector3 originWorld = GridSystem.Instance.Origin;

        float startX = originWorld.x - tileSize / 2f;
        float startZ = originWorld.z - tileSize / 2f;

        return new Vector2((worldPos.x - startX) / tileSize, (worldPos.z - startZ) / tileSize);
    }

    private Vector2 WorldToPixel(Vector3 worldPos) => WorldToTile(worldPos) * pixelsPerTile;
}
