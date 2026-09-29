using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WorldManager
{
    private const string TableResourcePath = "LocationTable";   // Resources/LocationTable.asset
    private const string TitleSceneName = "Title";

    private static LocationTable table;
    private static int handledSceneHandle;

    private static LocationId returnLocation = new LocationId(LocationType.Overworld);
    private static string returnAnchorId = "DungeonExit";

    // 다음 씬에서 걸어 나올 도착 지점. 그 지점이 Start에서 가져가며, 아무도 가져가지 않으면 경고합니다.
    private static string pendingArrivalId;

    private static readonly LocationRegionProvider regionProvider = new LocationRegionProvider();

    public static LocationId? Current { get; private set; }

    private static LocationTable Table
    {
        get
        {
            if (table == null)
            {
                table = Resources.Load<LocationTable>(TableResourcePath);
                if (table == null)
                    Debug.LogError($"[WorldManager] Resources/{TableResourcePath}.asset이 없어 지역 이동을 할 수 없습니다.");
            }
            return table;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    // 에디터에서 임의의 씬으로 바로 Play했을 때도 첫 씬을 같은 경로로 처리한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void HandleInitialScene()
    {
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    // ── 이동 ────────────────────────────────────────────────

    public static bool Travel(LocationId destination, string arrivalAnchorId, bool revive = false)
    {
        if (Table == null || !Table.TryGetScene(destination, out string sceneName))
        {
            Debug.LogError($"[WorldManager] LocationTable에 {destination}이(가) 등록되어 있지 않습니다.");
            return false;
        }

        // 같은 씬 안의 지역이면 씬을 다시 로드하지 않고 앵커로만 옮긴다.
        if (sceneName == SceneManager.GetActiveScene().name && !string.IsNullOrEmpty(arrivalAnchorId))
        {
            if (PlayerManager.Instance == null || !PlayerManager.Instance.PlaceAtAnchor(arrivalAnchorId))
                return false;

            Current = destination;
            ApplyRegion(destination);
            return true;
        }

        return SceneTransition.Go(sceneName, arrivalAnchorId, revive);
    }

    /// <summary>
    /// 도착 지역의 특정 지점(포탈 / 던전 입구)으로 걸어 나오며 이동합니다.
    /// 플레이어 배치와 도착 연출은 그 지점이 맡습니다.
    /// </summary>
    public static bool TravelToArrival(LocationId destination, string arrivalId)
    {
        if (Table == null || !Table.TryGetScene(destination, out string sceneName))
        {
            Debug.LogError($"[WorldManager] LocationTable에 {destination}이(가) 등록되어 있지 않습니다.");
            return false;
        }

        return TravelToArrival(sceneName, arrivalId);
    }

    /// <summary>
    /// 도착 씬의 특정 지점(포탈 / 던전 입구)으로 걸어 나오며 이동합니다. 포탈처럼 씬을 직접 지목할 때 씁니다.
    /// 도착한 씬의 지역(Current)은 씬 로드 시 LocationTable에서 씬 이름으로 찾아 정합니다.
    /// </summary>
    public static bool TravelToArrival(string sceneName, string arrivalId)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError($"[WorldManager] 도착 지점 '{arrivalId}'으로 갈 씬 이름이 비어 있습니다.");
            return false;
        }

        if (string.IsNullOrEmpty(arrivalId))
        {
            Debug.LogError($"[WorldManager] {sceneName}으로 갈 도착 지점 id가 비어 있습니다.");
            return false;
        }

        // 같은 씬이면 씬을 다시 로드하지 않고 그 지점에서 바로 나온다.
        if (sceneName == SceneManager.GetActiveScene().name)
        {
            TransitionArrivalPoint point = TransitionArrivalPoint.Find(arrivalId);
            if (point == null)
            {
                Debug.LogError($"[WorldManager] 현재 씬에서 도착 지점 '{arrivalId}'을(를) 찾지 못했습니다.");
                return false;
            }

            point.StartCoroutine(point.ArriveRoutine());
            return true;
        }

        pendingArrivalId = arrivalId;
        if (SceneTransition.Go(sceneName, null)) return true;

        pendingArrivalId = null;
        return false;
    }

    /// <summary>
    /// 이 지점이 이번 도착의 주인인지 확인하고, 맞으면 요청을 소비합니다.
    /// (TransitionArrivalPoint.Start에서 호출)
    /// </summary>
    public static bool TryConsumeArrival(string arrivalId)
    {
        if (string.IsNullOrEmpty(pendingArrivalId)) return false;
        if (pendingArrivalId != arrivalId) return false;

        pendingArrivalId = null;
        return true;
    }

    // 던전처럼 되돌아올 곳이 정해진 이동 전에 호출. 지금 있는 지역과 앵커를 기억한다.
    public static void RememberReturn(string anchorId)
    {
        if (Current.HasValue) returnLocation = Current.Value;
        if (!string.IsNullOrEmpty(anchorId)) returnAnchorId = anchorId;
    }

    public static bool TravelBack(bool revive = false)
    {
        return Travel(returnLocation, returnAnchorId, revive);
    }

    /// <summary>
    /// 기억해둔 입구로 걸어 나오며 복귀합니다. 그 입구를 찾지 못하면 false를 돌려주므로,
    /// 호출한 쪽이 앵커 복귀(TravelBack)로 물러설 수 있습니다.
    /// </summary>
    public static bool TravelBackThroughEntrance()
    {
        return TravelToArrival(returnLocation, returnAnchorId);
    }

    // ── 씬 도착 처리 ───────────────────────────────────────

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.handle == handledSceneHandle) return;
        handledSceneHandle = scene.handle;

        if (Table != null && Table.TryGetLocation(scene.name, out LocationId location))
        {
            Current = location;
            ApplyRegion(location);
        }
        else
        {
            Current = null;
        }

        ShowSceneUIAsync(scene).Forget();
        WarnIfArrivalMissingAsync(scene).Forget();
    }

    // 도착 지점 id가 틀리면 아무도 플레이어를 배치하지 않아 화면이 까만 채로 멈춥니다. 그 경우를 잡아 알립니다.
    private static async UniTaskVoid WarnIfArrivalMissingAsync(Scene scene)
    {
        if (string.IsNullOrEmpty(pendingArrivalId)) return;

        string requested = pendingArrivalId;
        await UniTask.Yield();   // 모든 오브젝트의 Start가 끝난 뒤 판정
        await UniTask.Yield();

        if (pendingArrivalId != requested) return;   // 도착 지점이 정상적으로 가져갔다
        pendingArrivalId = null;

        Debug.LogError($"[WorldManager] '{scene.name}' 씬에 도착 지점 '{requested}'이(가) 없습니다. " +
                       "포탈의 Portal Id / 던전 입구의 Return Anchor Id가 상대편이 지목한 id와 같은지 확인하세요.");

        // 플레이어가 꺼진 채로 남아 있으면 화면만 밝아지고 아무것도 조작할 수 없습니다.
        if (PlayerManager.Instance != null && !PlayerManager.Instance.PlaceAtAnchor(requested))
            Debug.LogError($"[WorldManager] 예비로 쓸 SpawnAnchor '{requested}'도 없습니다.");

        ScreenFader.Instance.StartCoroutine(ScreenFader.Instance.FadeTo(0f, 0.4f));
    }

    private static async UniTaskVoid ShowSceneUIAsync(Scene scene)
    {
        // UIManager는 Start에서 비동기로 패널을 로드하므로 준비될 때까지 기다린다. 씬이 바뀌면 중단.
        while (UIManager.Instance == null || !UIManager.Instance.IsInitialized)
        {
            await UniTask.Yield();
            if (SceneManager.GetActiveScene().handle != scene.handle) return;
        }

        if (scene.name == TitleSceneName)
        {
            if (UIManager.Instance.GetPanel<UITitle>() != null)
                await UIManager.Instance.ShowScene<UITitle>();
        }
        else if (UIManager.Instance.GetPanel<UIInGameHUD>() != null)
        {
            await UIManager.Instance.ShowScene<UIInGameHUD>();
        }
    }

    private static void ApplyRegion(LocationId location)
    {
        regionProvider.Region = ToRegion(location.type);
        RegionState.SetCurrent(regionProvider);
    }

    private static RegionType ToRegion(LocationType type)
    {
        switch (type)
        {
            case LocationType.Town: return RegionType.Town;
            case LocationType.Dungeon: return RegionType.Dungeon;
            default: return RegionType.Overworld;
        }
    }

    private class LocationRegionProvider : IRegionProvider
    {
        public RegionType Region { get; set; }
    }
}
