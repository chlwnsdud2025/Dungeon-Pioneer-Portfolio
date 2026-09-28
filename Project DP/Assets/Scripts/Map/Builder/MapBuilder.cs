using System.Collections.Generic;
using UnityEngine;

public class MapBuilder : MonoBehaviour
{
    /// <summary>
    /// 타일: 격자 칸에 칠하기. 자유 배치: 격자와 무관하게 하나씩 놓기(항상 보임).
    /// 개척 장식: 자유 배치와 같지만 개척 완료 시에만 보이는 DecorationRoot 아래에 놓임.
    /// </summary>
    public enum BuildMode
    {
        Tile,
        Free,
        Decoration
    }

    // 맵 프리팹 안의 표준 묶음 이름. 타일 묶음은 팔레트 슬롯과 1:1이고 순서도 같습니다.
    // 묶음 오브젝트는 태그가 없어야 합니다 — 태그가 붙으면 그 아래 타일이 LocationGrid에서 빠집니다.
    // 이름이 "Bonus"로 시작하면 안 됩니다 — 미니맵이 이름으로 보너스 오브젝트를 찾습니다.
    public static readonly string[] TileContainerNames = { "Floor", "Wall", "Entrance", "Exit", "Chest", "Trap", "Enemy", "Pushable" };
    public const string FreeContainerName = "FreeObjects";
    public const string DecorationContainerName = "DecorationRoot";

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

    [Header("Palette Prefabs")]
    public GameObject floorPrefab;
    public GameObject wallPrefab;
    public GameObject entrancePrefab;
    public GameObject exitPrefab;
    public GameObject chestPrefab;
    [Tooltip("여러 함정 프리팹을 등록해두고, 에디터 팔레트에서 'Trap' 선택 시 그 중 하나를 골라 칠합니다.")]
    public List<GameObject> trapPrefabs = new List<GameObject>();
    public GameObject enemyPrefab;
    public GameObject pushablePrefab;

    [Header("Free Placement")]
    [Tooltip("자유 배치·개척 장식 모드에서 쓸 프리팹. 인스펙터 팔레트에 끌어다 놓아 추가합니다.")]
    public List<GameObject> freePrefabs = new List<GameObject>();

    [Tooltip("자유 배치에서 Ctrl+휠 한 칸에 오르내리는 높이(m)")]
    [Min(0.01f)] public float freeHeightStep = 0.25f;

    [HideInInspector] public BuildMode buildMode = BuildMode.Tile;
    [HideInInspector] public int selectedFreeIndex = 0;
    [HideInInspector] public float freeHeight = 0f;

    [HideInInspector]
    public int selectedPrefabIndex = 0;
    [HideInInspector]
    public int selectedTrapIndex = 0; // 팔레트에서 'Trap' 선택 시, 어떤 함정 종류를 쓸지의 인덱스

    public Dictionary<Vector3Int, GameObject> placedTiles = new Dictionary<Vector3Int, GameObject>();

    public static bool IsTileContainer(Transform t)
        => t.CompareTag("Untagged") && System.Array.IndexOf(TileContainerNames, t.name) >= 0;

    public static bool IsFreeContainer(Transform t)
        => t.CompareTag("Untagged") && (t.name == FreeContainerName || t.name == DecorationContainerName);

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
