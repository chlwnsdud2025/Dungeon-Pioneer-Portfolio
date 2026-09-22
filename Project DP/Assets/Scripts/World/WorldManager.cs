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

    public static void Travel(LocationId destination, string arrivalAnchorId, bool revive = false)
    {
        if (Table == null || !Table.TryGetScene(destination, out string sceneName))
        {
            Debug.LogError($"[WorldManager] LocationTable에 {destination}이(가) 등록되어 있지 않습니다.");
            return;
        }

        // 같은 씬 안의 지역이면 씬을 다시 로드하지 않고 앵커로만 옮긴다.
        if (sceneName == SceneManager.GetActiveScene().name && !string.IsNullOrEmpty(arrivalAnchorId))
        {
            if (PlayerManager.Instance != null && PlayerManager.Instance.PlaceAtAnchor(arrivalAnchorId))
            {
                Current = destination;
                ApplyRegion(destination);
            }
            return;
        }

        SceneTransition.Go(sceneName, arrivalAnchorId, revive);
    }

    // 던전처럼 되돌아올 곳이 정해진 이동 전에 호출. 지금 있는 지역과 앵커를 기억한다.
    public static void RememberReturn(string anchorId)
    {
        if (Current.HasValue) returnLocation = Current.Value;
        if (!string.IsNullOrEmpty(anchorId)) returnAnchorId = anchorId;
    }

    public static void TravelBack(bool revive = false)
    {
        Travel(returnLocation, returnAnchorId, revive);
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
