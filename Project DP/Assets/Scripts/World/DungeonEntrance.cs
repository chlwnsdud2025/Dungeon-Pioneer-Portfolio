using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 오버월드 씬에 두는 "던전 입구" 트리거.
/// 플레이어가 이 콜라이더에 닿으면 던전 씬을 로드합니다.
///
/// 던전 씬은 로드되는 순간 DungeonManager.Start() → StartRun()이 돌면서 1층을 새로 생성하므로,
/// 오버월드에서 들어올 때마다 항상 새 런(초기화된 던전)으로 시작됩니다.
///
/// ★ 사용법: 오버월드 씬의 던전 입구 오브젝트에 이 스크립트와 Collider(IsTrigger)를 부착하세요.
///   (Reset()에서 자동으로 트리거로 켜줍니다) 플레이어 오브젝트의 Tag는 "Player"여야 합니다.
///   dungeonSceneName에 적은 씬은 File > Build Settings의 Scenes In Build에 등록돼 있어야 합니다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DungeonEntrance : MonoBehaviour
{
    [Tooltip("이 입구로 들어갔을 때 로드할 던전 씬 이름. Build Settings에 등록되어 있어야 합니다.")]
    [SerializeField] private string dungeonSceneName = "Dungeon";

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (string.IsNullOrEmpty(dungeonSceneName))
        {
            Debug.LogWarning("[DungeonEntrance] 로드할 던전 씬 이름이 비어 있습니다. 인스펙터를 확인해주세요.");
            return;
        }

        // 던전 씬을 단일(Single) 모드로 로드 → 오버월드 씬은 자동 언로드됩니다.
        SceneManager.LoadScene(dungeonSceneName);
    }
}
