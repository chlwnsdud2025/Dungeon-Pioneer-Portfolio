using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 경계를 넘어 "어느 씬으로, 어느 도착 지점에, 부활한 채로 갈지"를 전달하는 전역 요청 상자.
/// RegionState와 같은 결의 정적 상태 홀더입니다.
///
/// 씬 이동은 SceneManager.LoadScene을 직접 부르지 말고 항상 Go()를 통해 하세요.
/// 전환 직전에 플레이어를 잠시 꺼두는 처리(낙하/트리거 오발동 방지)가 여기에 묶여 있습니다.
/// </summary>
public static class SceneTransition
{
    private static string pendingAnchorId;
    private static bool reviveOnArrival;

    /// <summary>
    /// 던전에서 나올 때 돌아갈 오버월드 SpawnAnchor의 id.
    /// 오버월드의 DungeonEntrance가 '들어갈 때' 자기 id를 기록해두고, DungeonManager가 '나올 때' 그대로 씁니다.
    /// 그래서 던전 입구가 여러 개로 늘어나도 들어간 문으로 정확히 되돌아옵니다.
    /// </summary>
    public static string ReturnAnchorId { get; set; } = "DungeonExit";

    /// <summary>
    /// 씬을 전환합니다.
    /// </summary>
    /// <param name="sceneName">로드할 씬 이름. Build Settings에 등록되어 있어야 합니다.</param>
    /// <param name="anchorId">
    /// 도착 씬에서 플레이어를 놓을 SpawnAnchor의 id. 비워두면 PlayerManager는 배치하지 않습니다
    /// (던전 씬처럼 그 씬의 매니저가 직접 배치하는 경우).
    /// </param>
    /// <param name="revive">사망 후 복귀라 도착 시 체력을 최대치로 되돌려야 하면 true.</param>
    public static void Go(string sceneName, string anchorId, bool revive = false)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SceneTransition] 로드할 씬 이름이 비어 있습니다.");
            return;
        }

        pendingAnchorId = anchorId;
        reviveOnArrival = revive;

        // 전환 중 플레이어가 허공에서 낙하하거나 트리거를 잘못 밟지 않도록 잠시 꺼둡니다.
        // 도착 지점에 배치될 때 PlayerManager.PlaceAt이 다시 켭니다.
        if (PlayerManager.Instance != null) PlayerManager.Instance.PrepareForSceneChange();

        SceneManager.LoadScene(sceneName);
    }

    /// <summary>도착 씬에서 한 번만 읽어갑니다. (읽고 나면 비워짐)</summary>
    public static string ConsumePendingAnchorId()
    {
        string id = pendingAnchorId;
        pendingAnchorId = null;
        return id;
    }

    /// <summary>
    /// 도착 씬에서 한 번만 읽어갑니다. 던전 층 간 이동에서도 PlaceAt이 호출되므로,
    /// 1회성으로 소비해야 엉뚱한 타이밍에 체력이 회복되지 않습니다.
    /// </summary>
    public static bool ConsumeRevive()
    {
        bool revive = reviveOnArrival;
        reviveOnArrival = false;
        return revive;
    }
}
