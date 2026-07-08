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

    // ★ 추가: 가장 최근에 생성한 맵/플레이어 참조. GameManager가 이 값을 읽어가서
    //   층 전환 시 정리(Destroy)하거나 상태를 추적하는 데 사용합니다.
    public GameObject LastSpawnedMap { get; private set; }
    public GameObject LastSpawnedPlayer { get; private set; }

    private void Start()
    {
        // ★ 변경: 씬에 GameManager가 있으면 GameManager가 인게임 루프(언제 생성할지)를 총괄하므로
        //   여기서 중복 생성하지 않습니다. GameManager가 없는 테스트 씬 등에서는 기존처럼 자체적으로 생성합니다.
        if (GameManager.Instance == null)
        {
            GenerateDungeonAndSpawnPlayer();
        }
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
        LastSpawnedMap = spawnedMap;

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

        foreach (Transform child in mapInstance.GetComponentsInChildren<Transform>())
        {
            if (child.name.Contains("Entrance"))
            {
                entranceTransform = child;
                break;
            }
        }

        Vector3 spawnPosition = Vector3.zero;

        if (entranceTransform != null)
        {
            spawnPosition = entranceTransform.position + Vector3.up * 1.5f;
            Debug.Log("<color=yellow>플레이어가 던전 입구에 성공적으로 스폰되었습니다!</color>");
        }
        else
        {
            Debug.LogWarning("이 맵에는 'Entrance' 타일이 없습니다! 맵 원점(0,0,0)에 강제 스폰합니다.");
            spawnPosition = Vector3.up * 1.5f;
        }

        // 1. 플레이어 스폰
        GameObject spawnedPlayer = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        LastSpawnedPlayer = spawnedPlayer;

        // 2. 씬에 있는 메인 카메라를 찾아서, 방금 스폰한 플레이어를 타겟으로 지정해줍니다!
        CameraFollow camFollow = Camera.main.GetComponent<CameraFollow>();
        if (camFollow != null)
        {
            camFollow.target = spawnedPlayer.transform;

            // 시작하자마자 카메라가 부드럽게 날아오지 않고 즉시 플레이어 위치로 순간이동하도록 위치 초기화
            Camera.main.transform.position = spawnedPlayer.transform.position + camFollow.offset;
        }

        // ★ 함수 내부 환경이므로 매개변수명인 'mapInstance'를 넣어주어야 정상 작동합니다!
        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.InitializeMinimap(mapInstance);
        }
    }
}