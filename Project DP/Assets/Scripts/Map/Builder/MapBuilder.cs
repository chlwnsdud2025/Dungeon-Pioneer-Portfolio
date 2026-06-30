using System.Collections.Generic;
using UnityEngine;

public class MapBuilder : MonoBehaviour
{
    [Header("Map Settings")]
    public int mapWidth = 17;
    public int mapHeight = 17;
    public float tileSize = 1f;

    [Header("Brush Settings")]
    [Range(1, 5)]
    public int brushSize = 1;

    public int currentHeight = 0;
    public int currentRotation = 0;

    // ★ 추가됨: 선택적 지우개 모드 On/Off
    public bool useSelectiveEraser = false;

    [Header("Save Settings")]
    public string mapPrefabName = "NewDungeonMap";
    public string savePath = "Assets/Prefabs/Maps/";

    [Header("Palette Prefabs")]
    public GameObject floorPrefab;
    public GameObject wallPrefab;
    public GameObject entrancePrefab;
    public GameObject exitPrefab;
    public GameObject chestPrefab;
    public GameObject trapPrefab;
    public GameObject enemyPrefab;
    public GameObject pushablePrefab;

    [HideInInspector]
    public int selectedPrefabIndex = 0;

    public Dictionary<Vector3Int, GameObject> placedTiles = new Dictionary<Vector3Int, GameObject>();

    public void SyncPlacedTiles()
    {
        placedTiles.Clear();
        foreach (Transform child in transform)
        {
            int x = Mathf.RoundToInt(child.position.x / tileSize);
            int y = Mathf.RoundToInt(child.position.y / tileSize);
            int z = Mathf.RoundToInt(child.position.z / tileSize);
            Vector3Int pos = new Vector3Int(x, y, z);

            if (!placedTiles.ContainsKey(pos))
            {
                placedTiles.Add(pos, child.gameObject);
            }
        }
    }
}