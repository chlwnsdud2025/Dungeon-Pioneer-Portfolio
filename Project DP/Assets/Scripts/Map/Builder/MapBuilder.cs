using System.Collections.Generic;
using UnityEngine;

public class MapBuilder : MonoBehaviour
{
    [Header("Map Settings")]
    public int mapWidth = 17;
    public int mapHeight = 17;

    // GridSystem을 기본으로 쓰되, 오버월드(KayKit 1유닛 타일)처럼 칸 크기가 다를 때 override 합니다.
    // 0 이하면 GridSystem.TileSize(던전=3)를 사용합니다.
    [Tooltip("0 이하면 GridSystem.TileSize 사용. 오버월드 Plane(scale 1)은 spacing=2 → 2로 설정하세요.")]
    public float tileSizeOverride = 0f;

    public float tileSize => tileSizeOverride > 0f ? tileSizeOverride : GridSystem.Instance.TileSize;

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

    [HideInInspector]
    public int selectedPrefabIndex = 0;
    [HideInInspector]
    public int selectedTrapIndex = 0; // 팔레트에서 'Trap' 선택 시, 어떤 함정 종류를 쓸지의 인덱스

    public Dictionary<Vector3Int, GameObject> placedTiles = new Dictionary<Vector3Int, GameObject>();

    public void SyncPlacedTiles()
    {
        placedTiles.Clear();

        if (tileContainer == null) return;

        float size = tileSize;
        float height = tileSizeOverride > 0f ? tileSizeOverride : GridSystem.Instance.TileHeight;

        foreach (Transform child in tileContainer)
        {
            // MapBuilder가 쓰는 tileSize와 동일한 규칙으로 셀을 계산합니다.
            // (오버월드 override=1일 때 GridSystem.WorldToCell(3)을 쓰면 칸이 뭉개집니다)
            Vector3 p = child.position;
            Vector3Int pos = new Vector3Int(
                Mathf.RoundToInt(p.x / size),
                Mathf.RoundToInt(p.y / height),
                Mathf.RoundToInt(p.z / size));

            if (!placedTiles.ContainsKey(pos))
            {
                placedTiles.Add(pos, child.gameObject);
            }
        }
    }
}