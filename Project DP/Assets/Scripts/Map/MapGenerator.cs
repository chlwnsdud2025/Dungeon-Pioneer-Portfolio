using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public float tileSize = 1f; // 기획 상의 1칸 크기

    [Header("Prefabs")]
    public GameObject floorPrefab;
    public GameObject wallPrefab;
    //public GameObject trapFloorPrefab;

    // 임시 16x16 맵 데이터 (추후 랜덤 알고리즘으로 채워넣을 배열)
    private int[,] mapData = new int[16, 16];

    private void Start()
    {
        for (int x = 0; x < 16; x++)
        {
            for (int z = 0; z < 16; z++)
            {
                // 가장자리(테두리)인 경우 2(벽)로 설정
                if (x == 0 || x == 15 || z == 0 || z == 15)
                {
                    mapData[x, z] = 2;
                }
                // 테두리가 아닌 내부는 모두 1(바닥)로 설정
                else
                {
                    mapData[x, z] = 1;
                }
            }
        }
        GenerateMap();
    }

    public void GenerateMap()
    {
        for (int x = 0; x < 16; x++)
        {
            for (int z = 0; z < 16; z++)
            {
                // 1. 배열의 인덱스(x, z)를 실제 월드 좌표로 변환
                // 피벗이 정중앙이라면 이 수식 하나로 완벽하게 타일이 맞물립니다.
                Vector3 spawnPos = new Vector3(x * tileSize, 0f, z * tileSize);

                // 2. 데이터에 맞는 프리팹 생성
                int tileType = mapData[x, z];

                if (tileType == 1)
                {
                    Instantiate(floorPrefab, spawnPos, Quaternion.identity, this.transform);
                }
                else if (tileType == 2)
                {
                    Instantiate(wallPrefab, spawnPos, Quaternion.identity, this.transform);
                }
            }
        }
    }
}