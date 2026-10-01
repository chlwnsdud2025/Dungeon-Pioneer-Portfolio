using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DungeonManager : MonoBehaviour, IRegionProvider
{
    public static DungeonManager Instance { get; private set; }

    /// <summary>이 매니저가 있는 던전 씬의 지역은 항상 Dungeon입니다. (IRegionProvider)</summary>
    public RegionType Region => RegionType.Dungeon;

    [Header("Run 진행 상태")]
    [Tooltip("현재 몇 번째 던전 층인지. 1부터 시작")]
    public int currentFloor = 1;
    [Tooltip("이 층까지 가면 클리어 처리. 0이면 무제한(끝없이 다음 층 생성)")]
    public int maxFloor = 5;

    [Header("생성 / 배치")]
    [Tooltip("층 프리팹 생성과 층 배정 셔플을 담당하는 생성기.")]
    public DungeonGenerator dungeonGenerator;

    [Header("씬 전환")]
    [Tooltip("최종 클리어 후 오버월드로 돌아가기까지의 대기 시간(초). 0이면 즉시.")]
    public float victoryReturnDelay = 3f;
    [Tooltip("플레이어 사망 후 사망 모션을 보여주고 오버월드로 돌아가기까지의 대기 시간(초).")]
    public float deathReturnDelay = 3f;

    [Header("Player Graves")]
    [Tooltip("사망 위치에 랜덤으로 설치할 묘비 프리팹.")]
    public GameObject gravePrefabA;
    public GameObject gravePrefabB;

    // ── 이 씬 안에서 지금까지 만든 층들을 보관하는 캐시 ──
    private readonly Dictionary<LocationId, GameObject> loadedLocations = new Dictionary<LocationId, GameObject>();
    private LocationId currentLocationId;   // 지금 활성화된 층
    private bool hasCurrentLocation;
    private bool isLeaving;                 // 오버월드로 나가는 중(클리어/사망/탈출) → 중복 처리 방지
    private bool isTransitioning;
    private StairTile blockedArrivalStair;

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

        RegionState.SetCurrent(this); // 던전 씬에 들어왔음을 전역에 알림 (HUD의 지역 표시 등에서 참조)
    }

    private void Start()
    {
        // 던전 씬이 로드되는 순간(= 오버월드에서 입구로 들어온 순간) 자동으로 새 런을 시작합니다.
        StartRun();
    }

    private void Update()
    {
        if (blockedArrivalStair == null || PlayerManager.Instance == null || PlayerManager.Instance.Player == null) return;
        if (LocationGrid.WorldToCell(PlayerManager.Instance.Player.transform.position) != blockedArrivalStair.Cell)
            blockedArrivalStair = null;
    }

    public bool CanTriggerStairs(StairTile stair) =>
        !isLeaving && !isTransitioning && stair != null && stair != blockedArrivalStair;

    // ── 외부에서 호출하는 의도 함수들 ─────────────────────────────

    /// <summary>새 런을 처음부터 시작. 던전 씬 로드 직후 Start()에서 자동 호출됩니다.</summary>
    public void StartRun()
    {
        isLeaving = false;
        isTransitioning = true;
        blockedArrivalStair = null;
        currentFloor = 1;

        ScreenFader.Instance.SetOpaque();

        dungeonGenerator.PrepareNewRun(maxFloor > 0 ? maxFloor : 1); // 이번 런의 층별 맵 프리팹을 새로 셔플
        ResetWorld();                                                // 이전에 캐시된 층/플레이어 정리

        StairTile arrival = LoadFloor(currentFloor, "Entrance", true);
        StartCoroutine(ArriveAtStair(arrival));
    }

    /// <summary>출구를 밟았을 때 (ExitPoint). 다음 층으로 내려가고, 마지막 층이면 클리어 → 오버월드.</summary>
    public void GoToNextFloor(StairTile source = null)
    {
        if (!CanTriggerStairs(source)) return;

        if (maxFloor > 0 && currentFloor >= maxFloor)
        {
            // 최종 클리어 → 잠깐 연출 뒤 오버월드로 귀환
            isLeaving = true;
            if (victoryReturnDelay > 0f) StartCoroutine(LoadOverworldAfterDelay(victoryReturnDelay, revive: false));
            else LoadOverworldScene(revive: false);
            return;
        }

        StartCoroutine(ChangeFloor(source, currentFloor + 1, "Entrance"));
    }

    /// <summary>2층 이상에서 입구를 밟았을 때 (EntrancePoint). 한 층 위로 이동.</summary>
    public void GoToPreviousFloor(StairTile source = null)
    {
        if (!CanTriggerStairs(source) || currentFloor <= 1) return;
        StartCoroutine(ChangeFloor(source, currentFloor - 1, "Exit"));
    }

    /// <summary>던전 1층 입구로 나갈 때 (EntrancePoint). 오버월드 씬으로 즉시 복귀 = 던전 리셋.</summary>
    public void ReturnToOverworld(StairTile source = null)
    {
        if (!CanTriggerStairs(source)) return;
        isLeaving = true;
        StartCoroutine(LeaveThroughStair(source));
    }

    private IEnumerator ChangeFloor(StairTile source, int destinationFloor, string arrivalId)
    {
        isTransitioning = true;
        yield return DepartFromStair(source);

        currentFloor = destinationFloor;
        StairTile arrival = LoadFloor(destinationFloor, arrivalId, true);
        yield return ArriveAtStair(arrival);
    }

    private IEnumerator LeaveThroughStair(StairTile source)
    {
        isTransitioning = true;
        yield return DepartFromStair(source);

        // 씬이 언로드되면 층 인스턴스가 전부 파괴되므로, 나가기 직전에 상호작용 기록을 챙긴다.
        CaptureAllMapStates();

        // 들어올 때 쓴 던전 입구로 걸어 나옵니다. 그 입구를 찾지 못하면 앵커 복귀로 물러섭니다.
        if (WorldManager.TravelBackThroughEntrance()) yield break;

        ScreenFader.Instance.RevealAfterNextSceneLoad(source != null ? source.Path.fadeDuration : 0.4f);
        WorldManager.TravelBack(revive: false);
    }

    private IEnumerator DepartFromStair(StairTile stair)
    {
        yield return TransitionMotion.Depart(stair != null ? stair.Path : null);
    }

    private IEnumerator ArriveAtStair(StairTile stair)
    {
        if (stair != null) blockedArrivalStair = stair;

        yield return TransitionMotion.Arrive(stair != null ? stair.Path : null);
        isTransitioning = false;
    }

    /// <summary>
    /// 플레이어 사망 시 PlayerController에서 호출. timeScale을 얼리지 않으므로 DeadState 애니메이션이
    /// 정상 재생되며, 잠깐 보여준 뒤 오버월드로 복귀합니다.
    /// Player가 전역 상주라 씬 전환만으로는 되살아나지 않으므로, 부활을 명시적으로 요청합니다.
    /// </summary>
    public void ReportPlayerDeath()
    {
        if (isLeaving) return;
        isLeaving = true;
        SpawnGraveAtCurrentPlayerPosition();
        StartCoroutine(LoadOverworldAfterDelay(deathReturnDelay, revive: true));
    }

    // ── 내부 구현 ────────────────────────────────────────────────

    /// <summary>지정 층을 활성화(없으면 생성)하고 플레이어를 스폰 지점으로 옮깁니다. '씬 내부' 전환입니다.</summary>
    private StairTile LoadFloor(int floor, string arrivalSpawnPointId, bool arriveInside)
    {
        if (dungeonGenerator == null)
        {
            Debug.LogError("[DungeonManager] DungeonGenerator가 연결되어 있지 않습니다! 인스펙터를 확인해주세요.");
            return null;
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

            // ★ 저장된 상호작용 기록을 되돌리는 자리. 적에게 층 준비 완료를 알리기 '전'이어야 합니다.
            RestoreMapState(instance);
        }
        else
        {
            instance.SetActive(true);
        }

        currentLocationId = destination;
        hasCurrentLocation = true;

        NotifyActorsLocationReady(instance);

        StairTile arrival = PlacePlayer(instance, arrivalSpawnPointId, arriveInside);

        NotifyMinimap(destination, instance, isNew);
        RevealDecorationIfExplored(instance);
        return arrival;
    }

    /// <summary>
    /// 미니맵(HUD의 MinimapManager)이 지금 층보다 늦게 생겼을 때 지금 층을 다시 알려줍니다.
    /// 던전 씬에서 바로 시작하면 HUD를 비동기로 불러오는 동안 첫 층이 먼저 만들어져 알림을 놓치기 때문입니다.
    /// 미니맵에는 처음 보는 층이므로 새 층처럼 만들고, 저장해둔 탐색 기록이 있으면 되돌립니다.
    /// </summary>
    public void AttachMinimap()
    {
        GameObject instance = CurrentLocationInstance;
        if (instance == null) return;

        NotifyMinimap(currentLocationId, instance, isNew: true);
        RevealDecorationIfExplored(instance);
    }

    private static void NotifyMinimap(LocationId location, GameObject instance, bool isNew)
    {
        GameObject player = PlayerManager.Instance != null ? PlayerManager.Instance.Player : null;
        if (MinimapManager.Instance == null || player == null) return;

        MinimapManager.Instance.SwitchLocation(location, instance, isNew, player.transform);

        // 새로 만든 층이면 저장해둔 탐색 기록을 덮어씌운다. (SwitchLocation이 백지 상태를 만든 뒤여야 함)
        if (!isNew) return;

        MapSnapshot snapshot = DungeonRunState.Get(GetMapId(instance));
        if (snapshot != null && snapshot.minimapExploration != null)
            MinimapManager.Instance.RestoreExploration(location, snapshot.minimapExploration);
    }

    /// <summary>
    /// 이미 개척을 마친 맵이면 장식을 켭니다. 개척을 '완료한 순간'이 아니라 그 층에 다시 들어설 때
    /// 나타나는 연출이라, 층 전환이 끝나는 이 지점에서 한 번만 판정합니다.
    ///
    /// 호출 위치가 중요합니다. RestoreExploration '뒤'여야 재입장 경로에서 복원된 방문 기록이 반영되고,
    /// isNew 분기 '밖'이어야 층 재방문(SetActive) 경로까지 한 번에 커버됩니다.
    /// </summary>
    private static void RevealDecorationIfExplored(GameObject instance)
    {
        if (instance == null || MinimapManager.Instance == null) return;

        MapDecoration decoration = instance.GetComponent<MapDecoration>();
        if (decoration == null) return;   // 장식을 두지 않은 맵

        decoration.SetRevealed(MinimapManager.Instance.IsCurrentExplored);
    }

    /// <summary>
    /// 층에 배치된 액터들에게 그리드가 준비됐음을 알립니다.
    /// 적과 NPC가 같은 시점에 깨어나야 서로를 인지할 때 한쪽만 준비된 상황이 생기지 않습니다.
    /// </summary>
    private static void NotifyActorsLocationReady(GameObject locationInstance)
    {
        if (locationInstance == null) return;

        foreach (EnemyController enemy in locationInstance.GetComponentsInChildren<EnemyController>(true))
            enemy.NotifyLocationReady();

        foreach (NpcActor npc in locationInstance.GetComponentsInChildren<NpcActor>(true))
            npc.NotifyLocationReady();
    }

    /// <summary>
    /// 런을 새로 시작할 때 호출. 만들어 둔 모든 층을 파괴해 백지 상태로 되돌립니다.
    /// 플레이어는 여기서 건드리지 않습니다 — 던전이 리셋돼도 체력·장비·인벤토리는 그대로 이어집니다.
    /// </summary>
    private void ResetWorld()
    {
        foreach (GameObject loc in loadedLocations.Values)
        {
            if (loc != null) Destroy(loc);
        }
        loadedLocations.Clear();
        hasCurrentLocation = false;

        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.ResetAllLocations();
        }
    }

    /// <param name="revive">사망 후 복귀라 도착 시 체력을 최대치로 되돌려야 하면 true.</param>
    private void LoadOverworldScene(bool revive)
    {
        // 씬이 언로드되면 층 인스턴스가 전부 파괴되므로, 나가기 직전에 상호작용 기록을 챙긴다.
        CaptureAllMapStates();

        // 들어올 때 DungeonEntrance가 WorldManager에 기억시킨 지역·앵커로 되돌아갑니다. (입구가 여러 개여도 정확히 그 문 앞)
        WorldManager.TravelBack(revive);
    }

    // ── 상호작용 기록 저장·복원 ──────────────────────────────
    // 기록은 '몇 층'이 아니라 '어느 맵'에 묶습니다. 층 배정은 입장할 때마다 다시 셔플되므로,
    // 층에 묶으면 다음 입장에 전혀 다른 맵으로 기록이 옮겨붙습니다.

    private static string GetMapId(GameObject locationInstance)
    {
        LocationRoot root = locationInstance != null ? locationInstance.GetComponent<LocationRoot>() : null;
        return root != null ? root.MapId : null;
    }

    /// <summary>
    /// 지금 만들어져 있는 모든 층의 상태를 저장합니다.
    /// 비활성 층도 GameObject는 살아 있어서 한 번에 훑을 수 있습니다.
    /// </summary>
    private void CaptureAllMapStates()
    {
        foreach (KeyValuePair<LocationId, GameObject> kvp in loadedLocations)
        {
            GameObject instance = kvp.Value;
            string mapId = GetMapId(instance);
            if (instance == null || string.IsNullOrEmpty(mapId)) continue;

            MapSnapshot snapshot = new MapSnapshot();

            // 1. 맵에 원래 있던 오브젝트 중 기본값과 달라진 것만
            foreach (IFloorPersistent persistent in instance.GetComponentsInChildren<IFloorPersistent>(true))
            {
                if (persistent.TryCapture(out FloorObjectState state))
                    snapshot.objects[(persistent.PersistentKind, persistent.PersistentCell)] = state;
            }

            // 2. 플레이어가 설치한 횃불 (원래 맵에 없던 오브젝트라 따로 기록)
            //    비활성 층에서는 Update가 돌지 않아 다 탄 횃불이 아직 파괴되지 않은 채 남아 있을 수 있다.
            foreach (TorchInstance torch in instance.GetComponentsInChildren<TorchInstance>(true))
            {
                if (torch.IsExpired) continue;
                snapshot.torches.Add(torch.ToRecord());
            }

            // 3. 사망 위치에 설치된 묘비 (같은 셀은 하나만)
            foreach (PlayerGrave grave in instance.GetComponentsInChildren<PlayerGrave>(true))
            {
                GraveRecord record = grave.ToRecord();
                ReplaceGraveRecord(snapshot.graves, record);
            }

            // 4. 미니맵 탐색 기록 (개척도 포함)
            if (MinimapManager.Instance != null &&
                MinimapManager.Instance.TryCaptureExploration(kvp.Key, out MinimapExplorationRecord exploration))
            {
                snapshot.minimapExploration = exploration;
            }

            DungeonRunState.Save(mapId, snapshot);
        }
    }

    /// <summary>새로 만든 층에 저장된 기록을 되돌립니다.</summary>
    private void RestoreMapState(GameObject instance)
    {
        MapSnapshot snapshot = DungeonRunState.Get(GetMapId(instance));
        if (instance == null || snapshot == null) return;

        // 1. 원래 있던 오브젝트 되돌리기 (Pushable 위치 이동 포함)
        foreach (IFloorPersistent persistent in instance.GetComponentsInChildren<IFloorPersistent>(true))
        {
            if (snapshot.objects.TryGetValue((persistent.PersistentKind, persistent.PersistentCell), out FloorObjectState state))
                persistent.Restore(state);
        }

        // 2. 설치했던 횃불 다시 세우기
        RestoreTorches(instance, snapshot);

        // 3. 사망 위치에 설치했던 묘비 다시 세우기
        RestoreGraves(instance, snapshot);
    }

    private void RestoreTorches(GameObject instance, MapSnapshot snapshot)
    {
        if (snapshot.torches.Count == 0) return;

        // 횃불 프리팹은 플레이어가 들고 있는 것을 그대로 쓴다 (설정을 이중으로 두지 않기 위해).
        PlayerController player = PlayerManager.Instance != null ? PlayerManager.Instance.Controller : null;
        GameObject torchPrefab = player != null ? player.torchPrefab : null;
        if (torchPrefab == null)
        {
            Debug.LogWarning("[DungeonManager] 횃불 프리팹을 찾지 못해 설치했던 횃불을 복원하지 못했습니다.");
            return;
        }

        // 횃불은 월드 좌표가 아니라 '어느 벽(셀)의 어느 면'으로 기록되므로, 벽을 셀로 다시 찾아
        // 그 벽 콜라이더 기준으로 위치·회전을 재계산한다. (설치 때와 같은 GetMountPose 규칙)
        Dictionary<Vector2Int, WallObject> wallsByCell = new Dictionary<Vector2Int, WallObject>();
        foreach (WallObject wall in instance.GetComponentsInChildren<WallObject>(true))
        {
            Vector3Int c = GridSystem.Instance.WorldToCell(wall.transform.position);
            wallsByCell[new Vector2Int(c.x, c.z)] = wall;
        }

        foreach (TorchRecord record in snapshot.torches)
        {
            // 횃불은 던전 밖에 있는 동안에도 계속 탄다. 기록 후 흐른 시간을 빼서 이미 다 탔으면
            // 세우지 않고, 벽의 '설치됨' 표시도 남기지 않는다 (그 면에 새로 설치할 수 있어야 하므로).
            float remaining = record.remainingSeconds - (Time.time - record.capturedAt);
            if (remaining <= 0f) continue;

            // 벽을 못 찾으면 붙일 곳이 없다. 엉뚱한 자리에 세우느니 건너뛰고 원인을 남긴다.
            if (!wallsByCell.TryGetValue(record.wallCell, out WallObject wall))
            {
                Debug.LogWarning($"[DungeonManager] 횃불 복원 실패: 셀 {record.wallCell}에 WallObject가 없습니다. " +
                                 $"(맵 {GetMapId(instance)}, 면 {record.faceKey})");
                continue;
            }

            Collider wallCollider = wall.GetComponent<Collider>();
            if (wallCollider == null) wallCollider = wall.GetComponentInChildren<Collider>(true);
            if (wallCollider == null)
            {
                Debug.LogWarning($"[DungeonManager] 횃불 복원 실패: 셀 {record.wallCell}의 벽에 콜라이더가 없습니다.");
                continue;
            }

            TorchInstance.GetMountPose(wallCollider, record.faceKey, out Vector3 position, out Quaternion rotation);
            GameObject torch = Instantiate(torchPrefab, position, rotation, instance.transform);

            TorchInstance marker = torch.GetComponent<TorchInstance>();
            if (marker == null) marker = torch.AddComponent<TorchInstance>();
            marker.Initialize(wall, record.wallCell, record.faceKey, remaining);

            // 이 면에 다시 설치되지 않도록 벽에 표시를 되돌린다.
            wall.MarkTorchPlaced(record.faceKey);
        }
    }

    private void RestoreGraves(GameObject instance, MapSnapshot snapshot)
    {
        if (snapshot.graves.Count == 0) return;

        foreach (GraveRecord record in snapshot.graves)
            SpawnGrave(instance, record.cell, (PlayerGrave.Variant)record.variant, record.rotation);
    }

    /// <summary>현재 활성 층의 플레이어 사망 위치에 묘비를 설치합니다.</summary>
    private void SpawnGraveAtCurrentPlayerPosition()
    {
        GameObject location = CurrentLocationInstance;
        if (location == null) return;

        PlayerController player = PlayerManager.Instance != null ? PlayerManager.Instance.Controller : null;
        if (player == null) return;

        Vector3Int cell3 = GridSystem.Instance.WorldToCell(player.transform.position);
        Vector2Int cell = new Vector2Int(cell3.x, cell3.z);
        PlayerGrave.Variant variant = Random.value < 0.5f ? PlayerGrave.Variant.A : PlayerGrave.Variant.B;

        SpawnGrave(location, cell, variant, Quaternion.identity);

        if (MinimapManager.Instance != null)
        {
            foreach (PlayerGrave grave in location.GetComponentsInChildren<PlayerGrave>(true))
            {
                if (grave.Cell == cell)
                {
                    MinimapManager.Instance.RegisterGrave(grave.transform);
                    break;
                }
            }
        }
    }

    private void SpawnGrave(GameObject location, Vector2Int cell, PlayerGrave.Variant variant, Quaternion rotation)
    {
        GameObject prefab = variant == PlayerGrave.Variant.A ? gravePrefabA : gravePrefabB;
        if (prefab == null)
        {
            Debug.LogWarning("[DungeonManager] 묘비 프리팹이 연결되어 있지 않아 설치하지 못했습니다.");
            return;
        }

        RemoveGraveAtCell(location, cell);

        Vector3 worldPos = GridSystem.Instance.GetCellCenterWorld(new Vector3Int(cell.x, 0, cell.y))
                         + Vector3.up;
        GameObject graveObj = Instantiate(prefab, worldPos, rotation, location.transform);
        ConfigureGraveObject(graveObj, cell, variant);
    }

    private static void ConfigureGraveObject(GameObject graveObj, Vector2Int cell, PlayerGrave.Variant variant)
    {
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
        {
            // SphereCast는 맞은 콜라이더의 레이어를 보므로 자식까지 Interactable로 맞춥니다.
            foreach (Transform t in graveObj.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = interactableLayer;
        }

        foreach (Collider col in graveObj.GetComponentsInChildren<Collider>(true))
            col.isTrigger = true;

        PlayerGrave marker = graveObj.GetComponent<PlayerGrave>();
        if (marker == null) marker = graveObj.AddComponent<PlayerGrave>();
        marker.Initialize(cell, variant);
    }

    private static void RemoveGraveAtCell(GameObject location, Vector2Int cell)
    {
        foreach (PlayerGrave grave in location.GetComponentsInChildren<PlayerGrave>(true))
        {
            if (grave.Cell != cell) continue;

            if (MinimapManager.Instance != null)
                MinimapManager.Instance.UnregisterGrave(grave.transform);

            Destroy(grave.gameObject);
        }
    }

    private static void ReplaceGraveRecord(List<GraveRecord> graves, GraveRecord record)
    {
        for (int i = graves.Count - 1; i >= 0; i--)
        {
            if (graves[i].cell == record.cell)
            {
                graves[i] = record;
                return;
            }
        }

        graves.Add(record);
    }

    /// <summary>F키로 묘비를 제거했을 때 저장 기록에서도 지웁니다.</summary>
    public void RemoveGraveRecord(Vector2Int cell)
    {
        GameObject location = CurrentLocationInstance;
        if (location == null) return;

        string mapId = GetMapId(location);
        MapSnapshot snapshot = DungeonRunState.Get(mapId);
        if (snapshot == null) return;

        for (int i = snapshot.graves.Count - 1; i >= 0; i--)
        {
            if (snapshot.graves[i].cell == cell)
                snapshot.graves.RemoveAt(i);
        }

        if (MinimapManager.Instance != null)
            MinimapManager.Instance.RefreshGraves();
    }

    /// <summary>테스트용. 저장된 던전 진행 기록을 전부 지웁니다. (플레이 중 인스펙터에서 우클릭)</summary>
    [ContextMenu("던전 진행 상태 초기화")]
    public void ClearDungeonProgress()
    {
        DungeonRunState.Clear();
        Debug.Log("<color=yellow>[DungeonManager] 던전 진행 기록을 초기화했습니다. 다음 입장부터 새 던전입니다.</color>");
    }

    private IEnumerator LoadOverworldAfterDelay(float delay, bool revive)
    {
        yield return new WaitForSecondsRealtime(delay); // timeScale과 무관하게 대기
        LoadOverworldScene(revive);
    }

    /// <summary>
    /// 플레이어를 목적지 층의 스폰 지점으로 옮깁니다. 생성/파괴는 PlayerManager가 전담하므로
    /// 여기서는 좌표만 계산해 넘깁니다. (층 간 이동은 '씬 안' 이동이라 바라보던 방향은 유지)
    /// </summary>
    private StairTile PlacePlayer(GameObject locationInstance, string spawnPointId, bool arriveInside)
    {
        LocationRoot root = locationInstance != null ? locationInstance.GetComponent<LocationRoot>() : null;
        Transform spawnPoint = root != null ? root.GetSpawnPoint(spawnPointId) : null;
        StairTile stair = spawnPoint != null ? spawnPoint.GetComponent<StairTile>() : null;

        Vector3 spawnPosition;
        if (stair != null)
        {
            if (root.Grid == null || !root.Grid.IsPassable(stair.FrontCell, false))
            {
                Debug.LogWarning($"[DungeonManager] {spawnPointId} 계단 앞 칸이 막혀 있어 인접한 통행 가능 칸을 찾습니다.");
                stair = null;
                spawnPosition = GetFallbackSpawn(root, spawnPoint.position);
            }
            else spawnPosition = arriveInside ? stair.Path.InsidePosition : stair.Path.OutsidePosition;
        }
        else
        {
            Debug.LogWarning($"[DungeonManager] '{spawnPointId}' 스폰 지점을 찾을 수 없어 맵 원점 근처에 강제 스폰합니다.");
            spawnPosition = (locationInstance != null ? locationInstance.transform.position : Vector3.zero) + Vector3.up * 1.5f;
        }

        if (PlayerManager.Instance == null)
        {
            Debug.LogError("[DungeonManager] PlayerManager가 없어 플레이어를 배치할 수 없습니다. " +
                           "씬에 PlayerManager를 배치해주세요.");
            return null;
        }

        // CharacterController 처리, 상태 정리, 카메라 재연결은 전부 PlaceAt 안에서 처리됩니다.
        PlayerManager.Instance.PlaceAt(spawnPosition);
        return stair;
    }

    /// <summary>
    /// tilePosition(Entrance/Exit 타일 자체)의 상하좌우 4방향 중, 실제로 타일이 있고 벽이 없는 칸을 찾아
    /// 그 위의 스폰 좌표를 반환합니다.
    /// </summary>
    private Vector3 GetFallbackSpawn(LocationRoot root, Vector3 tilePosition)
    {
        Vector2Int cell = LocationGrid.WorldToCell(tilePosition);
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (Vector2Int direction in directions)
        {
            Vector2Int candidate = cell + direction;
            if (root != null && root.Grid != null && root.Grid.IsPassable(candidate, false))
                return LocationGrid.CellToWorld(candidate) + Vector3.up * 1.5f;
        }

        Debug.LogWarning("[DungeonManager] 스폰 타일 주변 4방향에서 걸어갈 수 있는 타일을 찾지 못해 타일 위에 그대로 스폰합니다.");
        return tilePosition + Vector3.up * 1.5f;
    }

}
