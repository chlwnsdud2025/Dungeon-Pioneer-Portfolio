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

    public enum TileType
    {
        Empty = 0,
        Floor = 1,
        Wall = 2,
        Entrance = 3,
        Exit = 4,
        Chest = 5
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

        // 외곽 벽(테두리)을 제외한 내부 맵(1 ~ width-2)만 검사
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

                    // 3면이 벽으로 둘러싸여 있다면 막다른 길
                    if (wallCount == 3)
                    {
                        deadEnds.Add(new Vector2Int(x, z));
                    }
                }
            }
        }

        // 막다른 길이 하나 이상 존재하면 무작위로 하나 골라서 상자 배치
        if (deadEnds.Count > 0)
        {
            Vector2Int chestPos = deadEnds[Random.Range(0, deadEnds.Count)];
            mapData[chestPos.x, chestPos.y] = TileType.Chest;
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

                // 바닥은 빈 공간이 아니면 무조건 먼저 깔기
                if (tileType != TileType.Wall)
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
                        // 상자를 바닥(basePos)에 둘지 올려둘지(elevatedPos) 프리팹 모양에 맞춰 선택하세요.
                        Instantiate(chestPrefab, elevatedPos, Quaternion.identity, this.transform);
                        break;
                }
            }
        }
    }
}