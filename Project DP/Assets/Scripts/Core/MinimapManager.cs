using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MinimapManager : MonoBehaviour
{
    public static MinimapManager Instance;

    [Header("UI Containers")]
    public RectTransform tileGridContainer;     // GridLayoutGroup이 붙은 정적 타일 패널 (구 gridContainer)
    public RectTransform dynamicObjectContainer; // ★ 추가됨: 자유 좌표계로 점들이 움직일 패널 (DynamicObjectPanel)

    [Header("UI Prefabs")]
    public GameObject uiTilePrefab;    // 안개/바닥/벽 표현용 사각형 타일 프리팹
    public GameObject uiDotPrefab;     // ★ 추가됨: 실시간 미끄러지듯 움직일 원형 점 프리팹

    [Header("Grid Settings")]
    public Vector2Int gridSize = new Vector2Int(17, 17);
    // ★ 변경: tileSize를 직접 들고 있지 않고 GridSystem에서 읽어옵니다. (MapGenerator/MapBuilder와 항상 동일한 값 보장)
    private float tileSize => GridSystem.Instance.TileSize;

    [Header("Tile Colors (Fog & Map)")]
    public Color unexploredColor = new Color(0f, 0f, 0f, 1f);
    public Color floorColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    public Color wallColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    public Color pushWallColor = new Color(0.5f, 0f, 0.5f, 1f);
    public Color trapColor = new Color(0f, 0f, 1f, 1f);

    [Header("Dot Colors (Realtime Moving)")]
    public Color playerColor = new Color(0.2f, 0.8f, 0.2f, 1f);     // 플레이어 실시간 녹색 점
    public Color enemyColor = new Color(0.9f, 0.2f, 0.2f, 1f);      // 몬스터 실시간 빨간색 점
    public Color chestColor = new Color(0.9f, 0.9f, 0.2f, 1f);      // 상자 노란색 점

    private Image[,] minimapTiles;
    private bool[,] isExplored;
    private int[,] staticMapData;

    // 실시간 동적 추적을 위한 구조
    private List<Transform> activePushWalls = new List<Transform>();
    private Transform playerTransform;
    private Image playerDotImage; // 플레이어용 실시간 UI 점

    // 몬스터와 상자를 실시간 매핑 추적하기 위한 딕셔너리 구조 쌍
    private Dictionary<EnemyController, Image> enemyDotMap = new Dictionary<EnemyController, Image>();
    private Dictionary<Transform, Image> chestDotMap = new Dictionary<Transform, Image>();

    // 상대 좌표 변환을 위한 원점 및 최대 크기 수치
    private float mapMinX = 0f;
    private float mapMinZ = 0f;
    private float mapMaxX = 0f;
    private float mapMaxZ = 0f;

    private void Awake()
    {
        Instance = this;
    }

    public void InitializeMinimap(GameObject mapInstance)
    {
        minimapTiles = new Image[gridSize.x, gridSize.y];
        isExplored = new bool[gridSize.x, gridSize.y];
        staticMapData = new int[gridSize.x, gridSize.y];

        // 리스트 및 딕셔너리 초기화 (기존 UI 오브젝트 삭제)
        activePushWalls.Clear();
        foreach (var kvp in enemyDotMap) if (kvp.Value != null) Destroy(kvp.Value.gameObject);
        foreach (var kvp in chestDotMap) if (kvp.Value != null) Destroy(kvp.Value.gameObject);
        enemyDotMap.Clear();
        chestDotMap.Clear();
        if (playerDotImage != null) Destroy(playerDotImage.gameObject);

        foreach (Transform child in tileGridContainer) Destroy(child.gameObject);
        foreach (Transform child in dynamicObjectContainer) Destroy(child.gameObject);

        // 1. 동적 원점(Min) 및 끝점(Max) 바운드 산출
        float minX = float.MaxValue; float minZ = float.MaxValue;
        float maxX = float.MinValue; float maxZ = float.MinValue;
        Transform[] allObjects = mapInstance.GetComponentsInChildren<Transform>();

        foreach (Transform obj in allObjects)
        {
            if (obj == mapInstance.transform) continue;
            if (obj.position.x < minX) minX = obj.position.x;
            if (obj.position.z < minZ) minZ = obj.position.z;
            if (obj.position.x > maxX) maxX = obj.position.x;
            if (obj.position.z > maxZ) maxZ = obj.position.z;
        }

        // 패딩과 크기 오차를 고려해 깔끔하게 반올림 정렬
        mapMinX = Mathf.Round(minX / tileSize) * tileSize;
        mapMinZ = Mathf.Round(minZ / tileSize) * tileSize;
        // 17칸 크기이므로 최소 원점으로부터 (그리드크기 * 타일크기) 만큼이 최대 가용 범위가 됩니다.
        mapMaxX = mapMinX + (gridSize.x * tileSize);
        mapMaxZ = mapMinZ + (gridSize.y * tileSize);

        // 2. 바둑판 UI 안개 생성
        for (int y = gridSize.y - 1; y >= 0; y--)
        {
            for (int x = 0; x < gridSize.x; x++)
            {
                GameObject tileObj = Instantiate(uiTilePrefab, tileGridContainer);
                Image tileImg = tileObj.GetComponent<Image>();
                tileImg.color = unexploredColor;
                minimapTiles[x, y] = tileImg;
                staticMapData[x, y] = 0;
            }
        }

        // 3. 태그 기반 정적 데이터 맵핑 및 동적 추적 오브젝트 생성
        foreach (Transform obj in allObjects)
        {
            if (obj == mapInstance.transform) continue;

            int gridX = Mathf.RoundToInt((obj.position.x - mapMinX) / tileSize);
            int gridY = Mathf.RoundToInt((obj.position.z - mapMinZ) / tileSize);

            if (gridX < 0 || gridX >= gridSize.x || gridY < 0 || gridY >= gridSize.y) continue;

            if (obj.CompareTag("Wall"))
            {
                staticMapData[gridX, gridY] = 2;
            }
            else if (obj.CompareTag("Floor") || obj.CompareTag("Entrance") || obj.CompareTag("Exit"))
            {
                if (staticMapData[gridX, gridY] == 0) staticMapData[gridX, gridY] = 1;
            }
            else if (obj.CompareTag("Trap"))
            {
                staticMapData[gridX, gridY] = 3;
            }
            else if (obj.CompareTag("Pushable"))
            {
                activePushWalls.Add(obj); // 밀리는 벽은 타일 색 교체 방식 유지
            }
            else if (obj.CompareTag("Chest"))
            {
                // ★ 보물상자용 전용 실시간 점UI 생성 및 리스트 바인딩
                GameObject dotObj = Instantiate(uiDotPrefab, dynamicObjectContainer);
                Image dotImg = dotObj.GetComponent<Image>();
                dotImg.color = chestColor;
                dotObj.SetActive(false); // 안개 걷히기 전엔 숨김
                chestDotMap.Add(obj, dotImg);
            }
        }

        // 4. 플레이어 실시간 점UI 생성
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (playerTransform != null)
        {
            GameObject pDot = Instantiate(uiDotPrefab, dynamicObjectContainer);
            playerDotImage = pDot.GetComponent<Image>();
            playerDotImage.color = playerColor;
        }

        // 5. 몬스터 실시간 점UI 생성
        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        foreach (EnemyController enemy in enemies)
        {
            GameObject eDot = Instantiate(uiDotPrefab, dynamicObjectContainer);
            Image eImg = eDot.GetComponent<Image>();
            eImg.color = enemyColor;
            eDot.SetActive(false); // 안개 전 숨김
            enemyDotMap.Add(enemy, eImg);
        }
    }

    private void Update()
    {
        if (minimapTiles == null || playerTransform == null) return;

        // 1. 현재 플레이어의 대략적인 칸수 계산 후 3x3 안개 제거
        int pGridX = Mathf.RoundToInt((playerTransform.position.x - mapMinX) / tileSize);
        int pGridY = Mathf.RoundToInt((playerTransform.position.z - mapMinZ) / tileSize);
        RevealTilesAround(pGridX, pGridY);

        // 2. 정적 지형 및 밀리는 벽 타일 베이스 색상 실시간 렌더링
        RenderStaticTerrain();

        // 3. ★ 핵심 기획: 플레이어, 몬스터, 상자 실제 3D 좌표 기준 실시간 UI 매핑 변환
        UpdateDynamicObjectPositions();
    }

    private void RenderStaticTerrain()
    {
        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                if (isExplored[x, y])
                {
                    if (IsPushWallAt(x, y))
                    {
                        minimapTiles[x, y].color = pushWallColor; // 보라색 타일 (밀리는 벽 실시간 연동)
                    }
                    else
                    {
                        if (staticMapData[x, y] == 2) minimapTiles[x, y].color = wallColor;
                        else if (staticMapData[x, y] == 3) minimapTiles[x, y].color = trapColor;
                        else if (staticMapData[x, y] == 1) minimapTiles[x, y].color = floorColor;
                        else minimapTiles[x, y].color = unexploredColor;
                    }
                }
                else
                {
                    minimapTiles[x, y].color = unexploredColor;
                }
            }
        }
    }

    private void UpdateDynamicObjectPositions()
    {
        // 1. 플레이어 실시간 스무스 위치 맵핑
        SetUIPositionFromWorld(playerTransform.position, playerDotImage.rectTransform, true);

        // 2. 보물상자 실시간 맵핑
        foreach (var kvp in chestDotMap)
        {
            if (kvp.Key == null) { Destroy(kvp.Value.gameObject); continue; }

            // 상자가 위치한 칸이 밝혀졌을 때만 점을 보여줍니다.
            Vector2Int gridPos = GetGridPos(kvp.Key.position);
            bool visible = IsValidGrid(gridPos) && isExplored[gridPos.x, gridPos.y];
            kvp.Value.gameObject.SetActive(visible);

            if (visible) SetUIPositionFromWorld(kvp.Key.position, kvp.Value.rectTransform, false);
        }

        // 3. 몬스터 실시간 스무스 움직임 맵핑
        foreach (var kvp in enemyDotMap)
        {
            if (kvp.Key == null) { Destroy(kvp.Value.gameObject); continue; }

            Vector2Int gridPos = GetGridPos(kvp.Key.transform.position);
            bool visible = IsValidGrid(gridPos) && isExplored[gridPos.x, gridPos.y];
            kvp.Value.gameObject.SetActive(visible);

            if (visible) SetUIPositionFromWorld(kvp.Key.transform.position, kvp.Value.rectTransform, false);
        }
    }

    // 3D 월드 좌표(X, Z)를 패널의 2D UI 좌표(AnchoredPosition)로 백분율 환산하는 유틸리티
    private void SetUIPositionFromWorld(Vector3 worldPos, RectTransform dotRect, bool isPlayer)
    {
        // 1. 점(Dot)의 UI 앵커를 패널의 '좌측 하단(Bottom-Left)'으로 강제 고정합니다.
        // (패널이 우측 상단에 있더라도, 점들의 이동 좌표계는 무조건 0,0을 좌측 하단으로 잡도록 만듦)
        dotRect.anchorMin = Vector2.zero;
        dotRect.anchorMax = Vector2.zero;

        // 2. 맵의 '진짜 테두리(Edge)' 계산
        // mapMinX는 첫 번째 타일의 '정중앙' 좌표입니다. 
        // 맵의 '맨 왼쪽 바깥 끝'부터 재려면 타일 크기의 절반(tileSize / 2)을 빼주어야 오차가 사라집니다.
        float startX = mapMinX - (tileSize / 2f);
        float startZ = mapMinZ - (tileSize / 2f);

        float totalWidth = gridSize.x * tileSize;
        float totalHeight = gridSize.y * tileSize;

        // 3. 전체 맵 크기 대비 현재 캐릭터가 어느 비율(0.0 ~ 1.0)에 서 있는지 구함
        float percentX = (worldPos.x - startX) / totalWidth;
        float percentY = (worldPos.z - startZ) / totalHeight;

        // 4. UI 패널의 실제 해상도 픽셀 크기를 가져옴
        float panelWidth = dynamicObjectContainer.rect.width;
        float panelHeight = dynamicObjectContainer.rect.height;

        // 5. 비율에 맞게 UI 상의 좌표 대입
        dotRect.anchoredPosition = new Vector2(percentX * panelWidth, percentY * panelHeight);
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
            Vector2Int grid = GetGridPos(activePushWalls[i].position);
            if (grid.x == targetGridX && grid.y == targetGridY) return true;
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