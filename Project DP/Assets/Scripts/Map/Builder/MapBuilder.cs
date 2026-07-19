using System.Collections.Generic;
using UnityEngine;

public class MapBuilder : MonoBehaviour
{
    [Header("Map Settings")]
    public int mapWidth = 17;
    public int mapHeight = 17;

    // ★ 변경: tileSize를 이 스크립트가 직접 들고 있지 않고, 프로젝트 공통 GridSystem에서 읽어옵니다.
    //   값을 바꾸고 싶으면 Assets/Resources/GridSystem.asset의 Tile Size를 수정하세요.
    //   (MapBuilderEditor.cs 등 기존에 builder.tileSize를 참조하던 코드는 그대로 동작합니다)
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

    [HideInInspector]
    public int selectedPrefabIndex = 0;
    [HideInInspector]
    public int selectedTrapIndex = 0; // 팔레트에서 'Trap' 선택 시, 어떤 함정 종류를 쓸지의 인덱스

    public Dictionary<Vector3Int, GameObject> placedTiles = new Dictionary<Vector3Int, GameObject>();

    public void SyncPlacedTiles()
    {
        placedTiles.Clear();

        if (tileContainer == null) return;

        foreach (Transform child in tileContainer)
        {
            // ★ 변경: GridSystem의 WorldToCell로 통일 (MapGenerator/MinimapManager와 동일한 변환 규칙 사용)
            Vector3Int pos = GridSystem.Instance.WorldToCell(child.position);

            if (!placedTiles.ContainsKey(pos))
            {
                placedTiles.Add(pos, child.gameObject);
            }
        }
    }
}