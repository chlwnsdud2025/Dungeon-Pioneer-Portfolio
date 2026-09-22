using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SpawnAnchor : MonoBehaviour
{
    [Tooltip("이 앵커를 찾을 때 쓰는 이름. 한 씬 안에서 겹치지 않게 지정하세요. (예: Start, DungeonExit)")]
    [SerializeField] private string id = "Start";

    // 씬에 실제로 배치된 앵커들. 씬이 언로드되면 OnDisable이 돌면서 자동으로 빠집니다.
    private static readonly Dictionary<string, SpawnAnchor> anchors = new Dictionary<string, SpawnAnchor>();

    // 앵커를 깜빡 배치하지 않았을 때를 위한 예비 좌표(DungeonEntrance 등이 Awake에서 등록).
    // 실제 앵커가 있으면 언제나 그쪽이 우선합니다.
    private static readonly Dictionary<string, Pose> fallbacks = new Dictionary<string, Pose>();

    public string Id => id;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void HookSceneUnload()
    {
        // 예비 좌표는 씬 전용이므로 씬이 내려갈 때 비웁니다.
        // (sceneUnloaded는 다음 씬의 Awake보다 먼저 돌기 때문에 새 씬의 등록을 지우지 않습니다)
        SceneManager.sceneUnloaded -= ClearFallbacks;
        SceneManager.sceneUnloaded += ClearFallbacks;
    }

    private static void ClearFallbacks(Scene scene) => fallbacks.Clear();

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning($"[SpawnAnchor] id가 비어 있어 등록하지 않습니다: {name}", this);
            return;
        }

        if (anchors.TryGetValue(id, out SpawnAnchor existing) && existing != null && existing != this)
            Debug.LogWarning($"[SpawnAnchor] id '{id}'가 중복됩니다. 나중에 등록된 '{name}'으로 덮어씁니다.", this);

        anchors[id] = this;
    }

    private void OnDisable()
    {
        if (string.IsNullOrEmpty(id)) return;
        if (anchors.TryGetValue(id, out SpawnAnchor current) && current == this)
            anchors.Remove(id);
    }

    /// <summary>
    /// 실제 앵커가 없을 때 사용할 예비 좌표를 등록합니다. 씬이 바뀌면 자동으로 지워집니다.
    /// (던전 입구가 "입구 정면 한 칸 앞"을 미리 등록해두는 용도)
    /// </summary>
    public static void RegisterFallback(string id, Vector3 position, Quaternion rotation)
    {
        if (string.IsNullOrEmpty(id)) return;
        fallbacks[id] = new Pose(position, rotation);
    }

    /// <summary>
    /// id에 해당하는 도착 좌표를 찾습니다. 실제 앵커 → 예비 좌표 순으로 확인하며, 둘 다 없으면 false.
    /// </summary>
    public static bool TryGetPose(string id, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (string.IsNullOrEmpty(id)) return false;

        if (anchors.TryGetValue(id, out SpawnAnchor anchor))
        {
            if (anchor != null)
            {
                position = anchor.transform.position;
                rotation = anchor.transform.rotation;
                return true;
            }

            anchors.Remove(id); // 파괴된 앵커가 남아 있었다면 정리
        }

        if (fallbacks.TryGetValue(id, out Pose fallback))
        {
            Debug.LogWarning($"[SpawnAnchor] id '{id}' 앵커가 씬에 없어 예비 좌표를 사용합니다. " +
                             "정확한 위치를 원하면 SpawnAnchor를 배치해주세요.");
            position = fallback.position;
            rotation = fallback.rotation;
            return true;
        }

        return false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 1f);
        Gizmos.DrawWireSphere(transform.position, 0.4f);
        Gizmos.DrawRay(transform.position, transform.forward * 1.5f); // 플레이어가 바라볼 방향
    }
}
