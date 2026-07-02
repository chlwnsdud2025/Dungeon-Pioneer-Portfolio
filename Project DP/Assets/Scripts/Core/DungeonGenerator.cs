using UnityEngine;
using Unity.AI.Navigation; // NavMesh 실시간 베이킹용

public class DungeonGenerator : MonoBehaviour
{
    [Header("Dungeon Prefabs")]
    [Tooltip("에디터로 구워둔 맵 프리팹들을 배열에 넣어주세요.")]
    public GameObject[] mapPrefabs;

    [Header("Player Settings")]
    public GameObject playerPrefab; // 플레이어 프리팹

    [Header("AI Navigation")]
    public NavMeshSurface navMeshSurface; // (선택 사항) 몬스터 길찾기용 Surface

    private void Start()
    {
        GenerateDungeonAndSpawnPlayer();
    }

    public void GenerateDungeonAndSpawnPlayer()
    {
        if (mapPrefabs.Length == 0)
        {
            Debug.LogError("맵 프리팹이 등록되지 않았습니다! 인스펙터를 확인해주세요.");
            return;
        }

        // 1. 등록된 맵들 중 하나를 무작위로 선택하여 씬에 스폰 (0,0,0 위치 기준)
        int randomIndex = Random.Range(0, mapPrefabs.Length);
        GameObject selectedMapPrefab = mapPrefabs[randomIndex];
        GameObject spawnedMap = Instantiate(selectedMapPrefab, Vector3.zero, Quaternion.identity);

        Debug.Log($"<color=cyan>랜덤 던전 생성 완료: {spawnedMap.name}</color>");

        // 2. 실시간 NavMesh 베이킹 (몬스터가 맵을 인식할 수 있도록 0.1초 만에 길을 굽습니다)
        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
            Debug.Log("실시간 NavMesh 베이킹 완료!");
        }

        // 3. 입구(Entrance) 타일을 찾아 플레이어 스폰
        SpawnPlayer(spawnedMap);
    }

    private void SpawnPlayer(GameObject mapInstance)
    {
        if (playerPrefab == null)
        {
            Debug.LogWarning("플레이어 프리팹이 등록되지 않아 스폰을 건너뜁니다.");
            return;
        }

        Transform entranceTransform = null;

        // 맵 하위의 모든 오브젝트(타일)를 검사하여 이름에 "Entrance"가 포함된 오브젝트를 찾습니다.
        // 맵 에디터에서 entrancePrefab을 설치했으므로 이름으로 쉽게 찾을 수 있습니다.
        foreach (Transform child in mapInstance.GetComponentsInChildren<Transform>())
        {
            if (child.name.Contains("Entrance"))
            {
                entranceTransform = child;
                break;
            }
        }

        if (entranceTransform != null)
        {
            // 입구 타일의 위치에서 플레이어를 스폰합니다. (바닥 타일에 파묻히지 않도록 높이 Y를 살짝 띄웁니다)
            Vector3 spawnPosition = entranceTransform.position + Vector3.up * 1.5f;
            Instantiate(playerPrefab, spawnPosition, Quaternion.identity);

            Debug.Log("<color=yellow>플레이어가 던전 입구에 성공적으로 스폰되었습니다!</color>");
        }
        else
        {
            // 만약 실수로 맵에 입구를 안 만들었을 경우를 대비한 예외 처리
            Debug.LogWarning("이 맵에는 'Entrance' 타일이 없습니다! 맵 원점(0,0,0)에 강제 스폰합니다.");
            Instantiate(playerPrefab, Vector3.up * 1.5f, Quaternion.identity);
        }
    }
}