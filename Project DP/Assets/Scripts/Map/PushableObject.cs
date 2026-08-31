using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PushableObject : MonoBehaviour, IFloorPersistent
{
    [Header("Crush")]
    public float crushDamage = 9999f;

    [Header("Gravity (Kinematic Rigidbody는 중력을 직접 계산해야 함)")]
    public float gravity = -9.81f;
    public float gravityMultiplier = 2f;
    [Tooltip("바닥으로 취급할 레이어. Floor뿐 아니라 TrapTile의 파여진 바닥도 반드시 포함해야 합니다.")]
    public LayerMask groundLayer;
    public float groundCheckDistance = 0.15f;

    private Rigidbody rb;
    private float verticalVelocity;

    // 밀기 진행 상태 (PushState_Player가 아니라 여기서 소유)
    private bool isPushing;
    private Vector3 pushStartPos, pushTargetPos;
    private float pushTimer, pushDuration;

    public bool IsBeingPushed => isPushing;
    public bool IsPushComplete => !isPushing;

    // ★ 함정을 메운 뒤에는 잠겨서 더 이상 밀 수 없게 됩니다. (TrapTile이 Lock()을 호출)
    private bool isLocked;
    public bool IsLocked => isLocked;
    public void Lock() => isLocked = true;

    // ★ 밀려서 움직이는 오브젝트라 '현재 셀'은 식별자가 될 수 없습니다.
    //   생성 직후(아직 안 움직였을 때) 확정한 원래 셀을 id로 쓰고, 현재 셀은 상태로 저장합니다.
    private Vector2Int originCell;

    // ── 저장·복원 (IFloorPersistent) ─────────────────────────

    public string PersistentKind => "Pushable";
    public Vector2Int PersistentCell => originCell;

    public void Latch()
    {
        Vector3Int c = GridSystem.Instance.WorldToCell(transform.position);
        originCell = new Vector2Int(c.x, c.z);
    }

    public bool TryCapture(out FloorObjectState state)
    {
        Vector3Int c = GridSystem.Instance.WorldToCell(transform.position);
        Vector2Int currentCell = new Vector2Int(c.x, c.z);

        state = new FloorObjectState { flag = isLocked, cell = currentCell };

        // 제자리에 그대로 있고 잠기지도 않았으면 기록할 게 없다.
        return isLocked || currentCell != originCell;
    }

    public void Restore(FloorObjectState state)
    {
        // XZ만 옮기고 Y는 그대로 둔다 — Update()의 중력 처리가 바닥
        // (또는 메워진 함정의 파인 바닥)에 알아서 붙여준다.
        Vector3 target = GridSystem.Instance.GetCellCenterWorld(new Vector3Int(state.cell.x, 0, state.cell.y));
        transform.position = new Vector3(target.x, transform.position.y, target.z);

        isPushing = false;
        if (state.flag) isLocked = true;
        SnapToGround();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;

        // Road 등 Floor 태그 타일이 Default 레이어인 경우가 많아 Floor 레이어만으로는 바닥을 못 잡습니다.
        int floorLayer = LayerMask.NameToLayer("Floor");
        if (floorLayer >= 0)
            groundLayer |= 1 << floorLayer;
        groundLayer |= 1; // Default
    }

    /// <summary>맵 스폰 직후 호출. 바닥을 못 찾으면 아래로 빠지지 않도록 붙입니다.</summary>
    public void SnapToGround()
    {
        Vector3 probe = transform.position + Vector3.up * 2f;
        if (TryFindGround(probe, 6f, out RaycastHit hit))
        {
            transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
            verticalVelocity = 0f;
        }
    }

    /// <summary>PushState_Player.Enter()에서 호출. "이 위치까지, 이 시간 동안 밀어줘"만 요청합니다.</summary>
    public void BeginPush(Vector3 targetXZ, float duration)
    {
        pushStartPos = transform.position;
        pushTargetPos = targetXZ;
        pushDuration = duration;
        pushTimer = 0f;
        isPushing = true;
    }

    private void Update()
    {
        Vector3 current = transform.position;
        float nextX = current.x;
        float nextZ = current.z;

        // 1. 수평 밀기 진행 (밀리는 중일 때만)
        if (isPushing)
        {
            pushTimer += Time.deltaTime;
            float t = Mathf.Clamp01(pushTimer / pushDuration);
            Vector3 horizontal = Vector3.Lerp(pushStartPos, pushTargetPos, t);
            nextX = horizontal.x;
            nextZ = horizontal.z;
            if (t >= 1f) isPushing = false;
        }

        // 2. 수직 중력 (Kinematic이라 자동으로 안 떨어지므로 직접 계산)
        bool grounded = TryFindGround(current + Vector3.up * 0.1f, groundCheckDistance + 0.1f, out RaycastHit hit);

        float nextY;
        if (grounded && verticalVelocity <= 0f)
        {
            verticalVelocity = 0f;
            nextY = hit.point.y; // 바닥(또는 함정 바닥)에 딱 붙임
        }
        else
        {
            verticalVelocity += gravity * gravityMultiplier * Time.deltaTime;
            nextY = current.y + verticalVelocity * Time.deltaTime;
        }

        // 3. 수평+수직을 한 번에 합쳐서 단 한 번만 MovePosition 호출
        rb.MovePosition(new Vector3(nextX, nextY, nextZ));
    }

    private bool TryFindGround(Vector3 origin, float maxDistance, out RaycastHit hit)
    {
        hit = default;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, maxDistance, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit candidate in hits)
        {
            if (candidate.collider == null) continue;
            if (candidate.collider.transform.IsChildOf(transform) || candidate.collider.transform == transform)
                continue;

            bool layerOk = ((1 << candidate.collider.gameObject.layer) & groundLayer) != 0;
            bool tagOk = candidate.collider.CompareTag("Floor")
                || candidate.collider.CompareTag("Trap")
                || candidate.collider.CompareTag("Entrance")
                || candidate.collider.CompareTag("Exit")
                || candidate.collider.GetComponentInParent<TrapTile>() != null;

            if (!layerOk && !tagOk) continue;

            hit = candidate;
            return true;
        }

        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isPushing) return;

        EnemyController enemy = other.GetComponent<EnemyController>();
        if (enemy != null)
        {
            enemy.TakeDamage(crushDamage);
        }
    }
}
