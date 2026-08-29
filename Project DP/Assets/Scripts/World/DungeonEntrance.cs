using UnityEngine;

/// <summary>
/// 오버월드 씬에 두는 "던전 입구" 트리거.
/// 플레이어가 이 콜라이더에 닿으면 던전 씬을 로드합니다.
///
/// 던전 씬은 로드되는 순간 DungeonManager.Start() → StartRun()이 돌면서 1층을 새로 생성하므로,
/// 오버월드에서 들어올 때마다 항상 새 런(초기화된 던전)으로 시작됩니다.
/// (플레이어의 체력·장비·인벤토리는 PlayerManager가 전역으로 유지하므로 그대로 이어집니다)
///
/// 나올 때는 returnAnchorId에 적힌 SpawnAnchor 위치로 되돌아옵니다. 들어가기 직전에 그 id를
/// SceneTransition에 기록해두므로, 나중에 던전 입구가 여러 개가 되어도 들어간 문 앞으로 정확히 나옵니다.
///
/// ★ 사용법:
///   DungeonEntrance (루트, 비주얼)
///   ├── EntranceTrigger  ← 이 스크립트 + BoxCollider(IsTrigger)
///   └── Blocker          ← DungeonEntranceBlocker (Open Side / Opening Size를 입구 면에 맞출 것)
///   플레이어 오브젝트의 Tag는 "Player"여야 합니다.
///   dungeonSceneName에 적은 씬은 File > Build Settings의 Scenes In Build에 등록돼 있어야 합니다.
///   returnAnchorId와 같은 id의 SpawnAnchor를 입구 앞(트리거 바깥)에 하나 배치해주세요.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class DungeonEntrance : MonoBehaviour
{
    [Tooltip("이 입구로 들어갔을 때 로드할 던전 씬 이름. Build Settings에 등록되어 있어야 합니다.")]
    [SerializeField] private string dungeonSceneName = "Dungeon";

    [Header("복귀 지점")]
    [Tooltip("던전에서 나올 때 플레이어가 도착할 SpawnAnchor의 id.")]
    [SerializeField] private string returnAnchorId = "DungeonExit";
    [Tooltip("해당 id의 SpawnAnchor를 배치하지 않았을 때 쓸 예비 거리. 입구 정면(+Z) 기준.")]
    [SerializeField] private float fallbackSpawnDistance = 2f;

    [Header("Trigger (local space)")]
    [SerializeField] private Vector3 triggerSize = new Vector3(0.85f, 0.85f, 0.2f);
    [SerializeField] private Vector3 triggerCenter = new Vector3(0f, 0f, 0.5f);

    private bool hasLoaded;
    private bool ignoreUntilExit; // 던전에서 막 나와 입구와 겹쳐 있을 때 즉시 재진입 방지

    private void Reset()
    {
        ApplyTrigger();
    }

    private void Awake()
    {
        ApplyTrigger();

        RegisterFallbackSpawn();
    }

    /// <summary>
    /// returnAnchorId에 해당하는 SpawnAnchor를 깜빡 배치하지 않았을 때 쓸 예비 좌표를 등록합니다.
    ///
    /// 트리거는 언제나 입구의 '바깥쪽 면'에 붙어 있으므로, 입구 원점에서 트리거 중심을 지나 조금 더
    /// 밀어낸 지점이 곧 바깥입니다. triggerCenter를 어느 방향으로 옮겨 놓든(+Z든 -Z든) 그대로 따라가며,
    /// 그 방향을 바라보게 회전시키면 플레이어가 던전을 등지고 서게 됩니다.
    /// </summary>
    private void RegisterFallbackSpawn()
    {
        Vector3 outerFace = transform.TransformPoint(new Vector3(triggerCenter.x, 0f, triggerCenter.z));

        Vector3 direction = outerFace - transform.position;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;

        Vector3 spawnPosition = outerFace + direction * fallbackSpawnDistance;
        spawnPosition.y = transform.position.y; // 입구 발치 높이를 유지

        SpawnAnchor.RegisterFallback(returnAnchorId, spawnPosition, Quaternion.LookRotation(direction, Vector3.up));
    }

    private void Start()
    {
        // 씬이 새로 로드되면서 hasLoaded가 초기화되므로, 도착 지점이 트리거와 겹쳐 있으면
        // 나오자마자 그대로 다시 들어가버립니다. 한 번 벗어나기 전까지는 무시합니다.
        ignoreUntilExit = IsPlayerOverlapping();
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
        if (hasLoaded || ignoreUntilExit) return;
        if (!IsPlayer(other)) return;

        if (string.IsNullOrEmpty(dungeonSceneName))
        {
            Debug.LogWarning("[DungeonEntrance] 로드할 던전 씬 이름이 비어 있습니다. 인스펙터를 확인해주세요.");
            return;
        }

        hasLoaded = true;

        // 나올 때 어디로 돌아올지 기록해둡니다.
        SceneTransition.ReturnAnchorId = returnAnchorId;

        // 던전 안에서의 스폰 좌표는 DungeonManager가 층 구조를 보고 직접 계산하므로 앵커는 넘기지 않습니다.
        SceneTransition.Go(dungeonSceneName, null);
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other)) ignoreUntilExit = false;
    }

    /// <summary>지금 이 트리거 안에 플레이어가 들어와 있는지 확인합니다.</summary>
    private bool IsPlayerOverlapping()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) return false;

        Collider[] hits = Physics.OverlapBox(
            transform.TransformPoint(col.center),
            Vector3.Scale(col.size, transform.lossyScale) * 0.5f,
            transform.rotation,
            ~0,
            QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            if (IsPlayer(hit)) return true;
        }

        return false;
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
