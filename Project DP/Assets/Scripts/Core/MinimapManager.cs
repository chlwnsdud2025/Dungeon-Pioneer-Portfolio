using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MinimapManager : MonoBehaviour
{
    public static MinimapManager Instance;

    [Header("UI Settings")]
    public RectTransform gridContainer; // GridLayoutGroup이 붙은 패널
    public GameObject uiTilePrefab;     // 기본 사각형 이미지 프리팹

    [Header("Grid Settings")]
    public Vector2Int gridSize = new Vector2Int(17, 17);
    public float tileSize = 3f; // 3x3 타일이므로 3f

    [Header("Tile Colors (Grid Based)")]
    public Color unexploredColor = new Color(0f, 0f, 0f, 1f);       // 검은색 (미탐색 구역)
    public Color floorColor = new Color(0.8f, 0.8f, 0.8f, 1f);      // 밝은 회색 (일반 바닥)
    public Color wallColor = new Color(0.3f, 0.3f, 0.3f, 1f);       // 어두운 회색 (일반 벽)
    public Color pushWallColor = new Color(0.5f, 0f, 0.5f, 1f);     // 보라색 (밀리는 벽 타일)
    public Color trapColor = new Color(0f, 0f, 1f, 1f);             // 파란색 (함정 타일)

    [Header("Dot Colors (Dynamic Objects)")]
    public Color playerColor = new Color(0.2f, 0.8f, 0.2f, 1f);     // 녹색 점 (플레이어)
    public Color enemyColor = new Color(0.9f, 0.2f, 0.2f, 1f);      // 빨간색 점 (몬스터)
    public Color chestColor = new Color(0.9f, 0.9f, 0.2f, 1f);      // 노란색 점 (보물상자)

    private Image[,] minimapTiles;
    private bool[,] isExplored;
    private int[,] staticMapData; // 정적 베이스 맵 데이터 (0:허공, 1:바닥, 2:일반벽, 3:함정)

    // 동적 오브젝트 실시간 추적용 리스트
    private List<Transform> activePushWalls = new List<Transform>();
    private List<Transform> activeChests = new List<Transform>();
    private List<EnemyController> activeEnemies = new List<EnemyController>();
    private Transform playerTransform;

    private int[,] dynamicMapData; // 실시간 점(Dot) 마킹용 버퍼

    private float mapMinX = 0f;
    private float mapMinZ = 0f;

    private void Awake()
    {
        Instance = this;
    }

    // 던전 제너레이터가 생성 직후 호출
    public void InitializeMinimap(GameObject mapInstance)
    {
        minimapTiles = new Image[gridSize.x, gridSize.y];
        isExplored = new bool[gridSize.x, gridSize.y];
        staticMapData = new int[gridSize.x, gridSize.y];
        dynamicMapData = new int[gridSize.x, gridSize.y];

        activePushWalls.Clear();
        activeChests.Clear();
        activeEnemies.Clear();

        foreach (Transform child in gridContainer) Destroy(child.gameObject);

        // 1. 동적 원점(기준점) 산출
        float minX = float.MaxValue;
        float minZ = float.MaxValue;
        Transform[] allObjects = mapInstance.GetComponentsInChildren<Transform>();

        foreach (Transform obj in allObjects)
        {
            if (obj == mapInstance.transform) continue;
            if (obj.position.x < minX) minX = obj.position.x;
            if (obj.position.z < minZ) minZ = obj.position.z;
        }

        mapMinX = Mathf.Round(minX / tileSize) * tileSize;
        mapMinZ = Mathf.Round(minZ / tileSize) * tileSize;

        // 2. 기본 검은색 안개 UI 바둑판 생성
        for (int y = gridSize.y - 1; y >= 0; y--)
        {
            for (int x = 0; x < gridSize.x; x++)
            {
                GameObject tileObj = Instantiate(uiTilePrefab, gridContainer);
                Image tileImg = tileObj.GetComponent<Image>();
                tileImg.color = unexploredColor;
                minimapTiles[x, y] = tileImg;
                staticMapData[x, y] = 0; // 기본은 허공(아무것도 없음)
            }
        }

        // 3. ★ 전체 수정: 오직 '태그(Tag)'만을 기준으로 타일 속성 판단 및 분류
        foreach (Transform obj in allObjects)
        {
            if (obj == mapInstance.transform) continue;

            int gridX = Mathf.RoundToInt((obj.position.x - mapMinX) / tileSize);
            int gridY = Mathf.RoundToInt((obj.position.z - mapMinZ) / tileSize);

            if (gridX < 0 || gridX >= gridSize.x || gridY < 0 || gridY >= gridSize.y) continue;

            // 정적 타일류 태그 검사
            if (obj.CompareTag("Wall"))
            {
                staticMapData[gridX, gridY] = 2; // 일반 벽
            }
            else if (obj.CompareTag("Floor") || obj.CompareTag("Entrance") || obj.CompareTag("Exit"))
            {
                // 다른 특수 바닥 데이터가 이미 덮어씌워지지 않았다면 일반 바닥 지정
                if (staticMapData[gridX, gridY] == 0) staticMapData[gridX, gridY] = 1;
            }
            else if (obj.CompareTag("Trap"))
            {
                staticMapData[gridX, gridY] = 3; // 함정 타일
            }
            // 동적 추적 오브젝트 태그 검사
            else if (obj.CompareTag("Pushable"))
            {
                activePushWalls.Add(obj); // 밀 수 있는 벽은 런타임 추적 리스트로
            }
            else if (obj.CompareTag("Chest"))
            {
                activeChests.Add(obj); // 보물상자도 런타임 추적 리스트로
            }
        }

        // 4. 캐릭터 및 몬스터 컴포넌트 수집
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;

        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        activeEnemies.AddRange(enemies);
    }

    private void Update()
    {
        if (minimapTiles == null || playerTransform == null) return;

        // 1. 플레이어 위치 기반 안개 해제
        int pGridX = Mathf.RoundToInt((playerTransform.position.x - mapMinX) / tileSize);
        int pGridY = Mathf.RoundToInt((playerTransform.position.z - mapMinZ) / tileSize);
        RevealTilesAround(pGridX, pGridY);

        // 2. 동적 실시간 점(Dot) 버퍼 초기화
        System.Array.Clear(dynamicMapData, 0, dynamicMapData.Length);

        // 3. 상자 위치 맵핑
        for (int i = activeChests.Count - 1; i >= 0; i--)
        {
            if (activeChests[i] == null) { activeChests.RemoveAt(i); continue; }
            Vector2Int grid = GetGridPos(activeChests[i].position);
            if (IsValidGrid(grid)) dynamicMapData[grid.x, grid.y] = 1;
        }

        // 4. 몬스터 실시간 위치 맵핑
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i] == null) { activeEnemies.RemoveAt(i); continue; }
            Vector2Int grid = GetGridPos(activeEnemies[i].transform.position);
            if (IsValidGrid(grid)) dynamicMapData[grid.x, grid.y] = 2;
        }

        // 5. 플레이어 실시간 위치 맵핑
        if (IsValidGrid(new Vector2Int(pGridX, pGridY)))
        {
            dynamicMapData[pGridX, pGridY] = 3;
        }

        // 6. 렌더링 루프
        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                if (isExplored[x, y])
                {
                    // [실시간 움직이는 오브젝트 - 점 레이어 점등]
                    if (dynamicMapData[x, y] == 3) minimapTiles[x, y].color = playerColor; // 플레이어 (녹색 점)
                    else if (dynamicMapData[x, y] == 2) minimapTiles[x, y].color = enemyColor; // 몹 (빨간색 점)
                    else if (dynamicMapData[x, y] == 1) minimapTiles[x, y].color = chestColor; // 상자 (노란색 점)
                    else
                    {
                        // [정적 / 밀리는 타일 레이어 점등]
                        if (IsPushWallAt(x, y))
                        {
                            minimapTiles[x, y].color = pushWallColor; // 밀리는 벽 (보라색 타일)
                        }
                        else
                        {
                            if (staticMapData[x, y] == 2) minimapTiles[x, y].color = wallColor;       // 일반 벽 (어두운 회색)
                            else if (staticMapData[x, y] == 3) minimapTiles[x, y].color = trapColor;  // 함정 (파란색 타일)
                            else if (staticMapData[x, y] == 1) minimapTiles[x, y].color = floorColor; // 일반 바닥 (밝은 회색)
                            else minimapTiles[x, y].color = unexploredColor; // 허공
                        }
                    }
                }
                else
                {
                    minimapTiles[x, y].color = unexploredColor; // 미탐색 영역 검은색
                }
            }
        }
    }

    private void RevealTilesAround(int px, int py)
    {
        for (int x = px - 1; x <= px + 1; x++)
        {
            for (int y = py - 1; y <= py + 1; y++)
            {
                if (x >= 0 && x < gridSize.x && y >= 0 && y < gridSize.y)
                {
                    isExplored[x, y] = true;
                }
            }
        }
    }

    private bool IsPushWallAt(int targetGridX, int targetGridY)
    {
        for (int i = activePushWalls.Count - 1; i >= 0; i--)
        {
            if (activePushWalls[i] == null) { activePushWalls.RemoveAt(i); continue; }

            int wX = Mathf.RoundToInt((activePushWalls[i].position.x - mapMinX) / tileSize);
            int wY = Mathf.RoundToInt((activePushWalls[i].position.z - mapMinZ) / tileSize);

            if (wX == targetGridX && wY == targetGridY) return true;
        }
        return false;
    }

    private Vector2Int GetGridPos(Vector3 worldPos)
    {
        int x = Mathf.RoundToInt((worldPos.x - mapMinX) / tileSize);
        int y = Mathf.RoundToInt((worldPos.z - mapMinZ) / tileSize);
        return new Vector2Int(x, y);
    }

    private bool IsValidGrid(Vector2Int grid)
    {
        return grid.x >= 0 && grid.x < gridSize.x && grid.y >= 0 && grid.y < gridSize.y;
    }
}