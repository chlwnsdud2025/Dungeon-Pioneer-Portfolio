using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 오버월드 씬에 두는 "던전 입구" 트리거.
/// 플레이어가 이 콜라이더에 닿으면 던전 씬을 로드합니다.
///
/// 던전 씬은 로드되는 순간 DungeonManager.Start() → StartRun()이 돌면서 1층을 새로 생성하므로,
/// 오버월드에서 들어올 때마다 항상 새 런(초기화된 던전)으로 시작됩니다.
///
/// ★ 사용법:
///   DungeonEntrance (루트, 비주얼)
///   ├── EntranceTrigger  ← 이 스크립트 + BoxCollider(IsTrigger)
///   └── Blocker          ← DungeonEntranceBlocker (Open Side / Opening Size를 입구 면에 맞출 것)
///   플레이어 오브젝트의 Tag는 "Player"여야 합니다.
///   dungeonSceneName에 적은 씬은 File > Build Settings의 Scenes In Build에 등록돼 있어야 합니다.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class DungeonEntrance : MonoBehaviour
{
    [Tooltip("이 입구로 들어갔을 때 로드할 던전 씬 이름. Build Settings에 등록되어 있어야 합니다.")]
    [SerializeField] private string dungeonSceneName = "Dungeon";

    [Header("Trigger (local space)")]
    [SerializeField] private Vector3 triggerSize = new Vector3(0.85f, 0.85f, 0.2f);
    [SerializeField] private Vector3 triggerCenter = new Vector3(0f, 0f, 0.5f);

    private bool hasLoaded;

    private void Reset()
    {
        ApplyTrigger();
    }

    private void Awake()
    {
        ApplyTrigger();
    }

    private void OnValidate()
    {
        ApplyTrigger();
    }

    private void ApplyTrigger()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null)
            return;

        col.isTrigger = true;
        col.size = triggerSize;
        col.center = triggerCenter;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasLoaded) return;
        if (!IsPlayer(other)) return;

        if (string.IsNullOrEmpty(dungeonSceneName))
        {
            Debug.LogWarning("[DungeonEntrance] 로드할 던전 씬 이름이 비어 있습니다. 인스펙터를 확인해주세요.");
            return;
        }

        hasLoaded = true;
        // 던전 씬을 단일(Single) 모드로 로드 → 오버월드 씬은 자동 언로드됩니다.
        SceneManager.LoadScene(dungeonSceneName);
    }

    private static bool IsPlayer(Collider other)
    {
        if (other == null) return false;
        if (other.CompareTag("Player")) return true;

        // CharacterController / 자식 콜라이더가 다른 태그를 가질 때 대비
        Transform root = other.transform.root;
        return root != null && root.CompareTag("Player");
    }
}
