using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    [Header("Grid System")]
    public Grid grid;

    [Header("Map Settings")]
    public int width = 17;
    public int height = 17;

    [Header("Prefabs")]
    public GameObject floorPrefab;
    public GameObject wallPrefab;
    public GameObject entrancePrefab;
    public GameObject exitPrefab;
    public GameObject chestPrefab;
    public GameObject trapPrefab;  // 함정 프리팹
    public GameObject enemyPrefab; // 적 프리팹

    [Header("Spawn Settings")]
    public int trapCount = 3;  // 생성할 함정 개수
    public int enemyCount = 3; // 생성할 적 개수

    public enum TileType
    {
        Empty = 0,
        Floor = 1,
        Wall = 2,
        Entrance = 3,
        Exit = 4,
        Chest = 5,
        Trap = 6,  // 함정 타입 추가
        Enemy = 7  // 적 타입 추가
    }

    private TileType[,] mapData;

    private void Start()
    {
        GenerateMaze();
        SpawnPrefabs();
    }

    private void GenerateMaze()
    {
        mapData = new TileType[width, height];

        // 1. 모든 칸을 벽으로 초기화
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                mapData[x, z] = TileType.Wall;
            }
        }

        // 2. 미로 길 뚫기
        CarvePath(1, 1);

        // 3. 외곽 벽에 입구, 출구 배치
        PlaceEntranceAndExit();

        // 4. 미로 내부의 막다른 길에 보물상자 배치
        PlaceTreasureChest();

        // 5. 남은 빈 바닥에 함정과 적 배치
        PlaceTraps();
        PlaceEnemies();
    }

    private void CarvePath(int x, int z)
    {
        mapData[x, z] = TileType.Floor;

        List<Vector2Int> directions = new List<Vector2Int>
        {
            new Vector2Int(0, 2), new Vector2Int(0, -2),
            new Vector2Int(2, 0), new Vector2Int(-2, 0)
        };

        for (int i = 0; i < directions.Count; i++)
        {
            Vector2Int temp = directions[i];
            int randomIndex = Random.Range(i, directions.Count);
            directions[i] = directions[randomIndex];
            directions[randomIndex] = temp;
        }

        foreach (Vector2Int dir in directions)
        {
            int nextX = x + dir.x;
            int nextZ = z + dir.y;

            if (nextX > 0 && nextX < width - 1 && nextZ > 0 && nextZ < height - 1 && mapData[nextX, nextZ] == TileType.Wall)
            {
                mapData[x + dir.x / 2, z + dir.y / 2] = TileType.Floor;
                CarvePath(nextX, nextZ);
            }
        }
    }

    // 외곽 벽에 입구와 출구 2개만 배치하는 함수
    private void PlaceEntranceAndExit()
    {
        List<Vector2Int> edgeCandidates = new List<Vector2Int>();

        for (int x = 1; x < width - 1; x++)
        {
            if (mapData[x, 1] == TileType.Floor) edgeCandidates.Add(new Vector2Int(x, 0));
            if (mapData[x, height - 2] == TileType.Floor) edgeCandidates.Add(new Vector2Int(x, height - 1));
        }

        for (int z = 1; z < height - 1; z++)
        {
            if (mapData[1, z] == TileType.Floor) edgeCandidates.Add(new Vector2Int(0, z));
            if (mapData[width - 2, z] == TileType.Floor) edgeCandidates.Add(new Vector2Int(width - 1, z));
        }

        for (int i = 0; i < edgeCandidates.Count; i++)
        {
            Vector2Int temp = edgeCandidates[i];
            int randomIndex = Random.Range(i, edgeCandidates.Count);
            edgeCandidates[i] = edgeCandidates[randomIndex];
            edgeCandidates[randomIndex] = temp;
        }

        // 섞인 후보지 중 2곳에 입구와 출구 배치
        if (edgeCandidates.Count >= 2)
        {
            mapData[edgeCandidates[0].x, edgeCandidates[0].y] = TileType.Entrance;
            mapData[edgeCandidates[1].x, edgeCandidates[1].y] = TileType.Exit;
        }
    }

    // 막다른 길에 보물상자 1개 배치
    private void PlaceTreasureChest()
    {
        List<Vector2Int> deadEnds = new List<Vector2Int>();

        for (int x = 1; x < width - 1; x++)
        {
            for (int z = 1; z < height - 1; z++)
            {
                if (mapData[x, z] == TileType.Floor)
                {
                    int wallCount = 0;
                    if (mapData[x + 1, z] == TileType.Wall) wallCount++;
                    if (mapData[x - 1, z] == TileType.Wall) wallCount++;
                    if (mapData[x, z + 1] == TileType.Wall) wallCount++;
                    if (mapData[x, z - 1] == TileType.Wall) wallCount++;

                    if (wallCount == 3)
                    {
                        deadEnds.Add(new Vector2Int(x, z));
                    }
                }
            }
        }

        if (deadEnds.Count > 0)
        {
            Vector2Int chestPos = deadEnds[Random.Range(0, deadEnds.Count)];
            mapData[chestPos.x, chestPos.y] = TileType.Chest;
        }
    }

    // 빈 바닥(Floor) 좌표들을 리스트로 가져오는 헬퍼 함수
    private List<Vector2Int> GetAvailableFloorTiles()
    {
        List<Vector2Int> availableFloors = new List<Vector2Int>();
        for (int x = 1; x < width - 1; x++)
        {
            for (int z = 1; z < height - 1; z++)
            {
                if (mapData[x, z] == TileType.Floor)
                {
                    availableFloors.Add(new Vector2Int(x, z));
                }
            }
        }
        return availableFloors;
    }

    // 함정 배치 로직
    private void PlaceTraps()
    {
        List<Vector2Int> floorTiles = GetAvailableFloorTiles();
        int countToPlace = Mathf.Min(trapCount, floorTiles.Count); // 바닥보다 많이 생성되는 것 방지

        for (int i = 0; i < countToPlace; i++)
        {
            int randomIndex = Random.Range(0, floorTiles.Count);
            Vector2Int pos = floorTiles[randomIndex];

            mapData[pos.x, pos.y] = TileType.Trap; // 타일 타입을 함정으로 변경
            floorTiles.RemoveAt(randomIndex);      // 중복 방지를 위해 리스트에서 제거
        }
    }

    // 적 배치 로직
    private void PlaceEnemies()
    {
        List<Vector2Int> floorTiles = GetAvailableFloorTiles();
        int countToPlace = Mathf.Min(enemyCount, floorTiles.Count);

        for (int i = 0; i < countToPlace; i++)
        {
            int randomIndex = Random.Range(0, floorTiles.Count);
            Vector2Int pos = floorTiles[randomIndex];

            mapData[pos.x, pos.y] = TileType.Enemy; // 타일 타입을 적으로 변경
            floorTiles.RemoveAt(randomIndex);
        }
    }

    private void SpawnPrefabs()
    {
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                Vector3Int cellPos = new Vector3Int(x, 0, z);

                // 기준 바닥 좌표
                Vector3 basePos = grid.GetCellCenterWorld(cellPos);

                TileType tileType = mapData[x, z];

                // 바닥 깔기 (단, 타일이 '벽'이거나 '함정'일 경우에는 깔지 않음)
                if (tileType != TileType.Wall && tileType != TileType.Trap)
                {
                    Instantiate(floorPrefab, basePos, Quaternion.identity, this.transform);
                }

                // 위로 한 칸 솟아오를 위치
                Vector3 elevatedPos = basePos + new Vector3(0, grid.cellSize.y, 0);

                switch (tileType)
                {
                    case TileType.Wall:
                        Instantiate(wallPrefab, elevatedPos, Quaternion.identity, this.transform);
                        break;
                    case TileType.Entrance:
                        Instantiate(entrancePrefab, elevatedPos, Quaternion.identity, this.transform);
                        break;
                    case TileType.Exit:
                        Instantiate(exitPrefab, elevatedPos, Quaternion.identity, this.transform);
                        break;
                    case TileType.Chest:
                        Instantiate(chestPrefab, elevatedPos, Quaternion.identity, this.transform);
                        break;
                    case TileType.Trap:
                        // 함정은 바닥을 대체하므로 basePos (아래쪽) 에 생성
                        Instantiate(trapPrefab, basePos, Quaternion.identity, this.transform);
                        break;
                    case TileType.Enemy:
                        // 적은 바닥 위에 있으므로 elevatedPos (위쪽) 에 생성
                        Instantiate(enemyPrefab, elevatedPos, Quaternion.identity, this.transform);
                        break;
                }
            }
        }
    }
}