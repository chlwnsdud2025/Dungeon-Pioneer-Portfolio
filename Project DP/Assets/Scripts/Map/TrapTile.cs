using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class TrapTile : MonoBehaviour, IFloorPersistent
{
    [Header("Trap Settings")]
    public float trapDamage = 15f;
    public float triggerCooldown = 1.5f; // 한 번 발동 후 다시 발동하기까지의 대기 시간

    // ★ 추가: 가시 연출 설정
    [Header("Spike Animation")]
    [Tooltip("obstacle 하위에 넣은 가시 오브젝트. 이 오브젝트의 로컬 Y만 움직입니다.")]
    public Transform spikeObject;
    public float spikeRaisedY = 0f;      // 데미지 주는 순간(튀어나옴)
    public float spikeLoweredY = -1.7f;  // 숨어있을 때(바닥 아래)
    [Tooltip("빠르게 튀어나오는 시간(초). 내려가는 시간은 쿨타임에서 자동 계산됩니다.")]
    public float raiseDuration = 0.08f;

    private float lastTriggerTime = -999f;
    private bool isFilled = false;

    // ★ 추가: 가시가 올라가는 중(true) / 내려가는 중(false)
    private bool spikeGoingUp = false;

    private Vector2Int cell;

    // 함정 셀에 현재 올라와 있는 적 (쿨타임 판정용 — 진입 1회 제한 없음)
    private readonly HashSet<EnemyController> enemiesOnCell = new HashSet<EnemyController>();
    // 적별 마지막 함정 피격 시각 (플레이어와 동일하게 쿨타임마다 피해)
    private readonly Dictionary<EnemyController, float> enemyLastDamageTime = new Dictionary<EnemyController, float>();

    // ── 저장·복원 (IFloorPersistent) ─────────────────────────

    public string PersistentKind => "Trap";
    public Vector2Int PersistentCell => cell;

    public void Latch()
    {
        Vector3Int c = GridSystem.Instance.WorldToCell(transform.position);
        cell = new Vector2Int(c.x, c.z);
        FitTriggerToPit();
    }

    /// <summary>
    /// XZ는 구덩이 바닥(Xfloor)에 맞추고, Y는 구덩이 바닥~캡슐 높이까지 넓힙니다.
    /// 실제 발동 여부는 IsActorInsidePit()에서 발 위치로 다시 검증합니다.
    /// </summary>
    private void FitTriggerToPit()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) return;

        col.isTrigger = true;

        if (TryGetPitWorldBounds(out Bounds pitBounds))
        {
            float rimY = GetTrapRimWorldY();
            float bottomY = pitBounds.min.y - 0.1f;
            float topY = rimY + 1.6f;

            Vector3 worldCenter = new Vector3(
                pitBounds.center.x,
                (bottomY + topY) * 0.5f,
                pitBounds.center.z);
            Vector3 localCenter = transform.InverseTransformPoint(worldCenter);

            Vector3 xzWorldSize = pitBounds.size;
            Vector3 xzLocalSize = transform.InverseTransformVector(xzWorldSize);
            float localHeight = Mathf.Abs(transform.InverseTransformVector(new Vector3(0f, topY - bottomY, 0f)).y);

            col.center = localCenter;
            col.size = new Vector3(
                Mathf.Abs(xzLocalSize.x) * 0.92f,
                localHeight,
                Mathf.Abs(xzLocalSize.z) * 0.92f);
            return;
        }

        // 프리팹 기본값
        col.center = new Vector3(0f, 0.5f, 0f);
        col.size = new Vector3(2f, 0.9f, 2f);
    }

    private bool TryGetPitWorldBounds(out Bounds pitBounds)
    {
        Transform pitFloor = FindPitFloorTransform();
        if (pitFloor != null && TryGetRendererBounds(pitFloor, out pitBounds))
            return true;

        pitBounds = default;
        return false;
    }

    private Transform FindPitFloorTransform()
    {
        foreach (Transform child in transform)
        {
            if (child.CompareTag("Floor")) return child;
        }

        return null;
    }

    private static bool TryGetRendererBounds(Transform target, out Bounds bounds)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            bounds = renderer.bounds;
            return true;
        }

        bounds = default;
        return false;
    }

    /// <summary>발 위치가 구덩이 XZ 범위 안이고, 입구 높이 이하일 때만 함정 내부로 판정합니다.</summary>
    private bool IsPlayerFeetInsidePit(PlayerController player)
    {
        if (player == null) return false;
        return IsFeetInsidePit(player.transform);
    }

    private bool IsFeetInsidePit(Transform actorRoot)
    {
        if (actorRoot == null) return false;
        if (!TryGetPitWorldBounds(out Bounds pitBounds)) return false;

        Vector3 feet = GetActorFeetWorldPosition(actorRoot);
        float rimY = GetTrapRimWorldY();

        float inset = Mathf.Min(pitBounds.size.x, pitBounds.size.z) * 0.08f;
        Vector3 min = pitBounds.min + new Vector3(inset, 0f, inset);
        Vector3 max = pitBounds.max - new Vector3(inset, 0f, inset);

        if (feet.x < min.x || feet.x > max.x || feet.z < min.z || feet.z > max.z)
            return false;

        return feet.y <= rimY + 0.12f;
    }

    /// <summary>구덩이에 빠진 액터를 함정 NavMesh 보행면(입구 높이)으로 올립니다.</summary>
    public bool TryGetRescueWorldPosition(out Vector3 rescuePos)
    {
        rescuePos = transform.position;

        Transform walkSurface = transform.Find("NavWalkSurface");
        if (walkSurface != null)
        {
            rescuePos = walkSurface.position;
            return true;
        }

        float rimY = GetTrapRimWorldY();
        Vector3 center = transform.position;
        if (TryGetPitWorldBounds(out Bounds pitBounds))
            center = new Vector3(pitBounds.center.x, rimY, pitBounds.center.z);
        else
            center.y = rimY;

        if (NavMesh.SamplePosition(center + Vector3.up * 0.5f, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            rescuePos = hit.position;
            return true;
        }

        rescuePos = center;
        return true;
    }

    /// <summary>월드 위치가 이 함정 구덩이 안에 있는지 (Enemy 탈출 판정용).</summary>
    public bool IsWorldPositionInsidePit(Vector3 worldPos)
    {
        if (isFilled) return false;
        if (!TryGetPitWorldBounds(out Bounds pitBounds)) return false;

        float rimY = GetTrapRimWorldY();
        float inset = Mathf.Min(pitBounds.size.x, pitBounds.size.z) * 0.08f;
        Vector3 min = pitBounds.min + new Vector3(inset, 0f, inset);
        Vector3 max = pitBounds.max - new Vector3(inset, 0f, inset);

        if (worldPos.x < min.x || worldPos.x > max.x || worldPos.z < min.z || worldPos.z > max.z)
            return false;

        return worldPos.y <= rimY + 0.2f;
    }

    private static Vector3 GetActorFeetWorldPosition(Transform actorRoot)
    {
        Vector3 feet = actorRoot.position;
        CharacterController cc = actorRoot.GetComponent<CharacterController>();
        if (cc != null)
            feet.y = actorRoot.position.y + cc.center.y - cc.height * 0.5f;
        return feet;
    }

    /// <summary>함정 구덩이 입구 높이(월드 Y). 가시 위치가 아니라 바닥 메시 상단을 기준으로 합니다.</summary>
    private float GetTrapRimWorldY()
    {
        if (TryGetPitWorldBounds(out Bounds pitBounds))
            return pitBounds.max.y + 0.05f;

        return transform.position.y + 0.55f;
    }

    public bool TryCapture(out FloorObjectState state)
    {
        state = new FloorObjectState { flag = true };
        return isFilled;   // 메워진 함정만 기록한다
    }

    public void Restore(FloorObjectState state)
    {
        if (!state.flag) return;

        isFilled = true;
        spikeGoingUp = false;

        // 메워진 함정은 가시가 완전히 내려간 상태로 고정한다.
        if (spikeObject != null)
        {
            Vector3 lp = spikeObject.localPosition;
            spikeObject.localPosition = new Vector3(lp.x, spikeLoweredY, lp.z);
        }
    }

    // ★ 추가: 시작 시 가시를 내려간 상태로 초기화
    private void Awake()
    {
        if (spikeObject != null)
        {
            Vector3 lp = spikeObject.localPosition;
            spikeObject.localPosition = new Vector3(lp.x, spikeLoweredY, lp.z);
        }

        FitTriggerToPit();
    }

    // ★ 가시 연출 + 구덩이 안 적의 쿨타임 데미지
    private void Update()
    {
        UpdateSpikeAnimation();
        TryDamageEnemiesOnCell();
    }

    private void UpdateSpikeAnimation()
    {
        if (spikeObject == null) return;

        float targetY = spikeGoingUp ? spikeRaisedY : spikeLoweredY;
        float duration = spikeGoingUp
            ? raiseDuration
            : Mathf.Max(0.01f, triggerCooldown - raiseDuration);
        float speed = Mathf.Abs(spikeRaisedY - spikeLoweredY) / duration;

        Vector3 lp = spikeObject.localPosition;
        float newY = Mathf.MoveTowards(lp.y, targetY, speed * Time.deltaTime);
        spikeObject.localPosition = new Vector3(lp.x, newY, lp.z);

        if (spikeGoingUp && Mathf.Approximately(newY, spikeRaisedY))
            spikeGoingUp = false;
    }

    /// <summary>
    /// 구덩이 안의 적: 플레이어와 동일하게 쿨타임마다 반복 데미지.
    /// (이전: 셀 진입 1회 + NavMesh 이동 중일 때만 → 빠지면 추가 피해/탈출이 막힘)
    /// </summary>
    private void TryDamageEnemiesOnCell()
    {
        if (isFilled) return;

        HashSet<EnemyController> currentlyOnCell = GatherEnemiesOnCell(out bool foundAny);
        if (!foundAny)
        {
            enemiesOnCell.Clear();
            CleanupEnemyDamageTimers(null);
            return;
        }

        foreach (EnemyController enemy in currentlyOnCell)
        {
            if (enemy == null) continue;
            if (!IsFeetInsidePit(enemy.transform)) continue;

            enemyLastDamageTime.TryGetValue(enemy, out float lastHit);
            if (Time.time < lastHit + triggerCooldown) continue;

            enemy.TakeDamage(trapDamage);
            enemyLastDamageTime[enemy] = Time.time;
            lastTriggerTime = Time.time;
            PlayTrapEffect();
            break; // 한 틱에 가시 연출은 한 번
        }

        enemiesOnCell.Clear();
        foreach (EnemyController enemy in currentlyOnCell)
            enemiesOnCell.Add(enemy);

        CleanupEnemyDamageTimers(currentlyOnCell);
    }

    private void CleanupEnemyDamageTimers(HashSet<EnemyController> stillPresent)
    {
        List<EnemyController> remove = null;
        foreach (KeyValuePair<EnemyController, float> pair in enemyLastDamageTime)
        {
            if (pair.Key == null || (stillPresent != null && !stillPresent.Contains(pair.Key)))
            {
                remove ??= new List<EnemyController>();
                remove.Add(pair.Key);
            }
        }

        if (remove == null) return;
        foreach (EnemyController e in remove)
            enemyLastDamageTime.Remove(e);
    }

    private HashSet<EnemyController> GatherEnemiesOnCell(out bool foundAny)
    {
        foundAny = false;
        HashSet<EnemyController> result = new HashSet<EnemyController>();

        foreach (EnemyController enemy in EnemyController.EnumerateActiveEnemies())
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            // 그리드 셀 또는 실제 구덩이 안에 있으면 대상
            Vector3Int enemyCell = GridSystem.Instance.WorldToCell(enemy.transform.position);
            bool onCell = enemyCell.x == cell.x && enemyCell.z == cell.y;
            bool inPit = IsFeetInsidePit(enemy.transform);
            if (!onCell && !inPit) continue;

            result.Add(enemy);
            foundAny = true;
        }

        return result;
    }

    // Enter 대신 Stay를 사용하여, 함정 위에 머무르는 동안 쿨타임마다 계속 데미지를 줍니다.
    private void OnTriggerStay(Collider other)
    {
        if (isFilled) return;

        // 쿨타임 중이면 작동하지 않음
        if (Time.time < lastTriggerTime + triggerCooldown) return;

        // 무기 히트박스 등 자식 콜라이더는 무시 — 플레이어 본체 CC만 판정
        if (other.GetComponent<CharacterController>() == null) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (!IsPlayerFeetInsidePit(player)) return;

        player.TakeDamage(trapDamage, transform);
        lastTriggerTime = Time.time;
        PlayTrapEffect();
    }

    // ★ 변경: 데미지 주는 순간 가시를 빠르게 올림 (이후 Update에서 쿨타임 동안 천천히 내려감)
    private void PlayTrapEffect()
    {
        spikeGoingUp = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        PushableObject pushable = other.GetComponent<PushableObject>();
        if (pushable != null)
        {
            isFilled = true;
            // ★ 메워지면 가시를 완전히 내려서 더 이상 튀어나오지 않게 함
            spikeGoingUp = false;
            // ★ 함정을 메운 오브젝트는 다시 밀 수 없도록 잠급니다 (한 번 더 밀어서 빠지는 것 방지)
            pushable.Lock();
            Debug.Log("함정이 밀려온 오브젝트로 메워졌습니다! (해당 오브젝트 잠김)");
        }
    }
}