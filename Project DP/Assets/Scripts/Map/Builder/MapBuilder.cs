using System.Collections.Generic;
using UnityEngine;

public class MapBuilder : MonoBehaviour
{
    // 맵 프리팹 안의 표준 묶음 이름. 격자 타일 묶음 이름은 MapTileType 이름과 같습니다.
    // 묶음 오브젝트는 태그가 없어야 합니다 — 태그가 붙으면 그 아래 타일이 LocationGrid에서 빠집니다.
    public static readonly string[] TileContainerNames = { "Floor", "Wall", "Entrance", "Exit", "Chest", "Trap", "Enemy", "Pushable" };
    public const string FreeContainerName = "FreeObjects";
    public const string DecorationContainerName = "DecorationRoot";
    public const string ObstacleContainerName = "Obstacles";   // 필드·마을의 자유 배치. 굽을 때 GridBlocker가 붙어 길을 막음

    [Header("Map Settings")]
    public int mapWidth = 17;
    public int mapHeight = 17;

    // 프로젝트의 모든 타일은 2×2×2 규격이라 맵마다 예외를 두지 않고 GridSystem 값을 그대로 씁니다.
    public float tileSize => GridSystem.Instance.TileSize;

    public Transform tileContainer;

    [Header("Brush Settings")]
    [Range(1, 5)]
    public int brushSize = 1;

    public int currentHeight = 0;
    public int currentRotation = 0;

    public bool useSelectiveEraser = false;

    [Header("Save & Load Settings")]
    public string mapPrefabName = "NewDungeonMap";
    public string savePath = "Assets/Prefabs/Maps/";
    // ★ 추가됨: 에디터로 다시 불러와서 수정할 원본 프리팹
    public GameObject prefabToLoad;

    [Header("Palette")]
    [Tooltip("던전·필드·마을 팔레트가 담긴 파일. 비어 있으면 인스펙터를 열 때 자동으로 찾거나 만듭니다.")]
    public MapPaletteLibrary paletteLibrary;

    // 지금 만드는 맵의 종류. 불러오기를 하면 맵 루트의 SceneLocation을 보고 자동으로 맞춰집니다.
    [HideInInspector] public LocationType mapKind = LocationType.Dungeon;

    [HideInInspector] public int selectedPaletteIndex = 0;
    [HideInInspector] public int selectedItemIndex = 0;

    [Header("Free Placement")]
    [Tooltip("자유 배치에서 Ctrl+휠 한 칸에 오르내리는 높이(m)")]
    [Min(0.01f)] public float freeHeightStep = 0.25f;

    [HideInInspector] public float freeHeight = 0f;

    // ── 옛 팔레트 슬롯 ──
    // 팔레트 파일이 생기기 전의 방식입니다. 지금은 쓰지 않고, 팔레트 파일을 처음 만들 때
    // 이 값들을 던전 팔레트로 옮기는 데만 씁니다. 지우면 MapBuildScene에 연결해 둔 값이 사라지므로 남겨 둡니다.
    [HideInInspector] public GameObject floorPrefab;
    [HideInInspector] public GameObject wallPrefab;
    [HideInInspector] public GameObject entrancePrefab;
    [HideInInspector] public GameObject exitPrefab;
    [HideInInspector] public GameObject chestPrefab;
    [HideInInspector] public List<GameObject> trapPrefabs = new List<GameObject>();
    [HideInInspector] public GameObject enemyPrefab;
    [HideInInspector] public GameObject pushablePrefab;
    [HideInInspector] public List<GameObject> freePrefabs = new List<GameObject>();

    public Dictionary<Vector3Int, GameObject> placedTiles = new Dictionary<Vector3Int, GameObject>();

    /// <summary>격자에 칠하는 타입인지. 자유 배치·개척 장식만 격자와 무관합니다.</summary>
    public static bool IsGridType(MapTileType type) => type != MapTileType.Free && type != MapTileType.Decoration;

    /// <summary>
    /// 이 맵 종류에서 쓸 수 있는 타입인지. 입구·출구·함정·밀기 블록·개척 장식은 던전 전용입니다
    /// (층 이동, 함정 발동, 개척도 같은 던전 규칙에 묶여 있음).
    /// </summary>
    public static bool IsTypeAllowed(MapTileType type, LocationType kind)
    {
        if (kind == LocationType.Dungeon) return true;

        switch (type)
        {
            case MapTileType.Floor:
            case MapTileType.Wall:
            case MapTileType.Chest:
            case MapTileType.Enemy:
            case MapTileType.Free:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 이 타입의 오브젝트가 들어갈 묶음 이름. 자유 배치는 던전에서는 길을 막지 않는 FreeObjects,
    /// 필드·마을에서는 길을 막는 Obstacles로 들어갑니다.
    /// </summary>
    public static string ContainerName(MapTileType type, LocationType kind)
    {
        switch (type)
        {
            case MapTileType.Free: return kind == LocationType.Dungeon ? FreeContainerName : ObstacleContainerName;
            case MapTileType.Decoration: return DecorationContainerName;
            default: return type.ToString();
        }
    }

    public static bool IsTileContainer(Transform t)
        => t.CompareTag("Untagged") && System.Array.IndexOf(TileContainerNames, t.name) >= 0;

    public static bool IsFreeContainer(Transform t)
        => t.CompareTag("Untagged") &&
           (t.name == FreeContainerName || t.name == DecorationContainerName || t.name == ObstacleContainerName);

    /// <summary>
    /// 칸 좌표 → 타일 사전을 계층에서 다시 만듭니다. 타일 묶음 안의 타일만 칸으로 등록하고,
    /// 자유 배치·개척 장식 묶음은 제외합니다 — 등록되면 같은 칸에 타일을 칠할 때 타일로 오인돼 지워집니다.
    /// 묶음으로 정리되기 전의 옛 맵처럼 컨테이너 바로 아래에 있는 타일도 그대로 등록합니다.
    /// </summary>
    public void SyncPlacedTiles()
    {
        placedTiles.Clear();

        if (tileContainer == null) return;

        foreach (Transform child in tileContainer)
        {
            if (IsFreeContainer(child)) continue;

            if (IsTileContainer(child))
            {
                foreach (Transform tile in child) RegisterTile(tile);
                continue;
            }

            RegisterTile(child);
        }
    }

    private void RegisterTile(Transform tile)
    {
        // MapBuilder가 쓰는 tileSize와 동일한 규칙으로 셀을 계산합니다.
        // (오버월드 override=1일 때 GridSystem.WorldToCell(3)을 쓰면 칸이 뭉개집니다)
        Vector3 p = tile.position;
        Vector3Int pos = new Vector3Int(
            Mathf.RoundToInt(p.x / tileSize),
            Mathf.RoundToInt(p.y / GridSystem.Instance.TileHeight),
            Mathf.RoundToInt(p.z / tileSize));

        if (!placedTiles.ContainsKey(pos))
            placedTiles.Add(pos, tile.gameObject);
    }
}
