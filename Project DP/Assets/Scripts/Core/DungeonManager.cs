using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

/// <summary>
/// ■ 던전 씬 하나를 통째로 책임지는 단일 매니저.
///
/// 던전과 관련된 모든 것을 여기서 처리합니다:
///  - 층 진행 규칙: 지금 몇 층인지(currentFloor), 몇 층에서 클리어인지(maxFloor)
///  - 층 로딩/전환: 층을 처음 방문하면 DungeonGenerator로 생성하고, 재방문이면 SetActive로 켜서 상태 복원
///  - 플레이어 스폰/이동, NavMesh 굽기, 미니맵 전환
///  - 씬 경계 이동: 던전 1층 입구로 나가거나(탈출) 최종 클리어/사망 시 → 오버월드 씬 로드
///
/// 층은 파괴하지 않고 SetActive로만 껐다 켭니다. 비활성 동안 적/애니메이션이 멈추고 HP·위치·열린 상자·
/// 설치한 횃불 값이 그대로 남아, 층을 오갈 때 탐색 상태가 별도 저장 로직 없이 보존됩니다. 던전을 완전히
/// 나가면 던전 씬이 통째로 언로드되어 이 캐시도 사라집니다(= 던전 리셋). 그래서 DontDestroyOnLoad를
/// 일부러 켜지 않습니다: 다시 들어오면 새 인스턴스로 생겨 새 런이 시작됩니다.
///
/// 외부(트리거/플레이어)는 아래 3개의 의도 함수만 호출하면 됩니다:
///   GoToNextFloor()      — 출구를 밟았을 때 (ExitPoint)
///   GoToPreviousFloor()  — 2층 이상에서 입구를 밟았을 때 (EntrancePoint)
///   ReturnToOverworld()  — 1층 입구로 나갈 때 (EntrancePoint)
/// 오버월드 → 던전 '진입'은 오버월드 씬의 DungeonEntrance가 담당합니다(이 스크립트를 거치지 않음).
/// </summary>
public class DungeonManager : MonoBehaviour
{
    public static DungeonManager Instance { get; private set; }

    [Header("Run 진행 상태")]
    [Tooltip("현재 몇 번째 던전 층인지. 1부터 시작")]
    public int currentFloor = 1;
    [Tooltip("이 층까지 가면 클리어 처리. 0이면 무제한(끝없이 다음 층 생성)")]
    public int maxFloor = 5;

    [Header("생성 / 배치")]
    [Tooltip("층 프리팹 생성과 층 배정 셔플을 담당하는 생성기.")]
    public DungeonGenerator dungeonGenerator;
    [Tooltip("런 전체에서 하나만 유지되는 플레이어 프리팹.")]
    public GameObject playerPrefab;
    [Tooltip("층을 옮길 때마다 활성 층 기준으로 다시 굽습니다. 비활성 층은 자동 제외.")]
    public NavMeshSurface navMeshSurface;

    [Header("씬 전환")]
    [Tooltip("던전을 나갈 때 로드할 오버월드 씬 이름. Build Settings에 등록되어 있어야 합니다.")]
    public string overworldSceneName = "Overworld";
    [Tooltip("최종 클리어 후 오버월드로 돌아가기까지의 대기 시간(초). 0이면 즉시.")]
    public float victoryReturnDelay = 3f;
    [Tooltip("플레이어 사망 후 사망 모션을 보여주고 오버월드로 돌아가기까지의 대기 시간(초).")]
    public float deathReturnDelay = 3f;

    // ── 이 씬 안에서 지금까지 만든 층들을 보관하는 캐시 ──
    private readonly Dictionary<LocationId, GameObject> loadedLocations = new Dictionary<LocationId, GameObject>();
    private GameObject persistentPlayer;   // 이 씬이 사는 동안 유지되는 단 하나의 플레이어
    private LocationId currentLocationId;   // 지금 활성화된 층
    private bool hasCurrentLocation;
    private bool isLeaving;                 // 오버월드로 나가는 중(클리어/사망/탈출) → 중복 처리 방지

    /// <summary>현재 활성화된 층 인스턴스. (횃불 설치 시 부모로 사용)</summary>
    public GameObject CurrentLocationInstance =>
        hasCurrentLocation && loadedLocations.TryGetValue(currentLocationId, out GameObject go) ? go : null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 던전 씬이 로드되는 순간(= 오버월드에서 입구로 들어온 순간) 자동으로 새 런을 시작합니다.
        StartRun();
    }

    // ── 외부에서 호출하는 의도 함수들 ─────────────────────────────

    /// <summary>새 런을 처음부터 시작. 던전 씬 로드 직후 Start()에서 자동 호출됩니다.</summary>
    public void StartRun()
    {
        isLeaving = false;
        currentFloor = 1;

        dungeonGenerator.PrepareNewRun(maxFloor > 0 ? maxFloor : 1); // 이번 런의 층별 맵 프리팹을 새로 셔플
        ResetWorld();                                                // 이전에 캐시된 층/플레이어 정리

        LoadFloor(currentFloor, "Entrance"); // 1층 '입구'에 스폰
    }

    /// <summary>출구를 밟았을 때 (ExitPoint). 다음 층으로 내려가고, 마지막 층이면 클리어 → 오버월드.</summary>
    public void GoToNextFloor()
    {
        if (isLeaving) return;

        currentFloor++;

        if (maxFloor > 0 && currentFloor > maxFloor)
        {
            // 최종 클리어 → 잠깐 연출 뒤 오버월드로 귀환
            isLeaving = true;
            if (victoryReturnDelay > 0f) StartCoroutine(LoadOverworldAfterDelay(victoryReturnDelay));
            else LoadOverworldScene();
            return;
        }

        LoadFloor(currentFloor, "Entrance"); // 새 층의 '입구'에 스폰
    }

    /// <summary>2층 이상에서 입구를 밟았을 때 (EntrancePoint). 한 층 위로 이동.</summary>
    public void GoToPreviousFloor()
    {
        if (isLeaving) return;

        currentFloor = Mathf.Max(1, currentFloor - 1);
        LoadFloor(currentFloor, "Exit"); // 이전 층의 '출구'(원래 나갔던 자리)에 스폰
    }

    /// <summary>던전 1층 입구로 나갈 때 (EntrancePoint). 오버월드 씬으로 즉시 복귀 = 던전 리셋.</summary>
    public void ReturnToOverworld()
    {
        if (isLeaving) return;
        isLeaving = true;
        LoadOverworldScene();
    }

    /// <summary>
    /// 플레이어 사망 시 PlayerController에서 호출. timeScale을 얼리지 않으므로 DeadState 애니메이션이
    /// 정상 재생되며, 잠깐 보여준 뒤 오버월드로 복귀합니다.
    /// </summary>
    public void ReportPlayerDeath()
    {
        if (isLeaving) return;
        isLeaving = true;
        StartCoroutine(LoadOverworldAfterDelay(deathReturnDelay));
    }

    // ── 내부 구현 ────────────────────────────────────────────────

    /// <summary>지정 층을 활성화(없으면 생성)하고 플레이어를 스폰 지점으로 옮깁니다. '씬 내부' 전환입니다.</summary>
    private void LoadFloor(int floor, string arrivalSpawnPointId)
    {
        if (dungeonGenerator == null)
        {
            Debug.LogError("[DungeonManager] DungeonGenerator가 연결되어 있지 않습니다! 인스펙터를 확인해주세요.");
            return;
        }

        LocationId destination = new LocationId(LocationType.Dungeon, floor);

        // 목적지 층만 켜고 나머지는 끈다 (끈 층의 상태는 메모리에 그대로 보존)
        foreach (KeyValuePair<LocationId, GameObject> kvp in loadedLocations)
        {
            if (kvp.Value != null) kvp.Value.SetActive(kvp.Key.Equals(destination));
        }

        bool isNew = !loadedLocations.TryGetValue(destination, out GameObject instance);
        if (isNew)
        {
            instance = dungeonGenerator.CreateLocation(destination);
            loadedLocations[destination] = instance;
        }
        else
        {
            instance.SetActive(true);
        }

        currentLocationId = destination;
        hasCurrentLocation = true;

        if (navMeshSurface != null) navMeshSurface.BuildNavMesh();

        // 처음 생성한 층만: 베이크 직후 후처리(예: Trap NavMeshLink). 재방문이면 이미 만들어져 있음.
        if (isNew) dungeonGenerator.OnNavMeshBaked(instance);

        PlacePlayer(instance, arrivalSpawnPointId);

        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.SwitchLocation(destination, instance, isNew, persistentPlayer.transform);
        }
    }

    /// <summary>런을 새로 시작할 때 호출. 만들어 둔 모든 층과 플레이어를 파괴해 백지 상태로 되돌립니다.</summary>
    private void ResetWorld()
    {
        foreach (GameObject loc in loadedLocations.Values)
        {
            if (loc != null) Destroy(loc);
        }
        loadedLocations.Clear();
        hasCurrentLocation = false;

        if (persistentPlayer != null)
        {
            Destroy(persistentPlayer);
            persistentPlayer = null;
        }

        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.ResetAllLocations();
        }
    }

    private void LoadOverworldScene()
    {
        SceneManager.LoadScene(overworldSceneName);
    }

    private IEnumerator LoadOverworldAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay); // timeScale과 무관하게 대기
        LoadOverworldScene();
    }

    /// <summary>플레이어를 목적지 층의 스폰 지점으로 배치. 없으면 새로 만들고, 있으면 위치만 옮깁니다.</summary>
    private void PlacePlayer(GameObject locationInstance, string spawnPointId)
    {
        LocationRoot root = locationInstance != null ? locationInstance.GetComponent<LocationRoot>() : null;
        Transform spawnPoint = root != null ? root.GetSpawnPoint(spawnPointId) : null;

        Vector3 spawnPosition;
        if (spawnPoint != null)
        {
            // ★ Entrance/Exit 타일 위에 그대로 스폰하면 그 타일의 트리거(ExitPoint/EntrancePoint)와 곧바로
            //   겹쳐서 도착하자마자 반대 방향으로 튕겨나갑니다. 타일 자체가 아니라 그 주변 4방향 중
            //   실제로 타일이 있고 벽이 없는 칸 위에 스폰합니다.
            spawnPosition = GetSpawnPositionNearTile(locationInstance, spawnPoint.position);
        }
        else
        {
            Debug.LogWarning($"[DungeonManager] '{spawnPointId}' 스폰 지점을 찾을 수 없어 맵 원점 근처에 강제 스폰합니다.");
            spawnPosition = (locationInstance != null ? locationInstance.transform.position : Vector3.zero) + Vector3.up * 1.5f;
        }

        if (persistentPlayer == null)
        {
            persistentPlayer = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            // ★ CharacterController가 켜진 채로 position을 직접 바꾸면 충돌 보정이 끼어들 수 있어 잠깐 꺼줍니다.
            CharacterController cc = persistentPlayer.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            persistentPlayer.transform.position = spawnPosition;

            if (cc != null) cc.enabled = true;
        }

        CameraFollow camFollow = Camera.main.GetComponent<CameraFollow>();
        if (camFollow != null)
        {
            camFollow.target = persistentPlayer.transform;
            Camera.main.transform.position = persistentPlayer.transform.position + camFollow.offset;
        }
    }

    /// <summary>
    /// tilePosition(Entrance/Exit 타일 자체)의 상하좌우 4방향 중, 실제로 타일이 있고 벽이 없는 칸을 찾아
    /// 그 위의 스폰 좌표를 반환합니다.
    /// </summary>
    private Vector3 GetSpawnPositionNearTile(GameObject locationInstance, Vector3 tilePosition)
    {
        float tileSize = GridSystem.Instance.TileSize;
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

        foreach (Vector3 dir in directions)
        {
            Vector3 candidate = tilePosition + dir * tileSize;
            if (IsWalkableTile(locationInstance, candidate))
            {
                return candidate + Vector3.up * 1.5f;
            }
        }

        Debug.LogWarning("[DungeonManager] 스폰 타일 주변 4방향에서 걸어갈 수 있는 타일을 찾지 못해 타일 위에 그대로 스폰합니다.");
        return tilePosition + Vector3.up * 1.5f;
    }

    /// <summary>
    /// worldPos가 속한 그리드 칸에 Wall 태그 오브젝트가 없고, 실제 타일(Floor/Entrance/Exit/Trap/Chest/Pushable)이
    /// 존재하는지 확인합니다. (Wall/Entrance/Exit는 바닥보다 한 칸 높아 y는 무시하고 x, z만 비교)
    /// </summary>
    private bool IsWalkableTile(GameObject locationInstance, Vector3 worldPos)
    {
        if (locationInstance == null) return false;

        Vector3Int cell = GridSystem.Instance.WorldToCell(worldPos);
        bool hasWall = false;
        bool hasTile = false;

        foreach (Transform child in locationInstance.GetComponentsInChildren<Transform>(true))
        {
            if (child == locationInstance.transform) continue;

            Vector3Int childCell = GridSystem.Instance.WorldToCell(child.position);
            if (childCell.x != cell.x || childCell.z != cell.z) continue;

            if (child.CompareTag("Wall"))
            {
                hasWall = true;
                break;
            }

            if (child.CompareTag("Floor") || child.CompareTag("Entrance") || child.CompareTag("Exit") ||
                child.CompareTag("Trap") || child.CompareTag("Chest") || child.CompareTag("Pushable"))
            {
                hasTile = true;
            }
        }

        return hasTile && !hasWall;
    }
}
