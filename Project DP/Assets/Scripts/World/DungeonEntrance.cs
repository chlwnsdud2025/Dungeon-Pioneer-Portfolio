using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class DungeonEntrance : TransitionArrivalPoint
{
    [Header("복귀 지점")]
    [Tooltip("던전에서 나올 때 플레이어가 도착할 지점의 id. 도착 연출도 이 id로 이 입구를 찾습니다.")]
    [SerializeField] private string returnAnchorId = "DungeonExit";
    [Tooltip("해당 id의 SpawnAnchor를 배치하지 않았을 때 쓸 예비 거리. 입구 정면(+Z) 기준.")]
    [SerializeField] private float fallbackSpawnDistance = 2f;

    [Header("Trigger (local space)")]
    [SerializeField] private Vector3 triggerSize = new Vector3(0.85f, 0.85f, 0.2f);
    [SerializeField] private Vector3 triggerCenter = new Vector3(0f, 0f, 0.5f);

    [Header("진입 / 복귀 연출")]
    [Tooltip("걸어 들어가고 걸어 나오는 경로. 비우면 페이드만 합니다.")]
    [SerializeField] private TransitionPath transitionPath;

    private BoxCollider trigger;
    private bool isTraveling;

    public override string ArrivalId => returnAnchorId;
    public override TransitionPath ArrivalPath => transitionPath;

    protected override Collider TriggerCollider
    {
        get
        {
            if (trigger == null) trigger = GetComponent<BoxCollider>();
            return trigger;
        }
    }

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
        if (isTraveling || ignoreUntilExit) return;
        if (!IsPlayer(other)) return;

        isTraveling = true;

        // 나올 때 어느 지역의 어느 입구로 돌아올지 기억해둡니다.
        WorldManager.RememberReturn(returnAnchorId);

        // 던전 안에서의 스폰 좌표는 DungeonManager가 층 구조를 보고 직접 계산하므로 앵커는 넘기지 않습니다.
        StartCoroutine(EnterDungeon());
    }

    private IEnumerator EnterDungeon()
    {
        yield return TransitionMotion.Depart(transitionPath);

        if (WorldManager.Travel(new LocationId(LocationType.Dungeon), null)) yield break;

        // 이동에 실패했으면 까만 화면에 갇히지 않도록 입구 밖으로 도로 걸어 나옵니다.
        isTraveling = false;
        yield return WalkOutRoutine();
    }
}
