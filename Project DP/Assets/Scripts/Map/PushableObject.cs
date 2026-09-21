using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PushableObject : MonoBehaviour, IFloorPersistent, IInteractable
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

    public Vector2Int CurrentCell => LocationGrid.WorldToCell(transform.position);

    public bool TryGetPushTargetCell(out Vector2Int cell)
    {
        cell = default;
        if (!isPushing) return false;

        cell = LocationGrid.WorldToCell(pushTargetPos);
        return true;
    }

    // ★ 함정을 메운 뒤에는 잠겨서 더 이상 밀 수 없게 됩니다. (TrapTile이 Lock()을 호출)
    private bool isLocked;
    public bool IsLocked => isLocked;
    public void Lock() => isLocked = true;

    private bool settling;
    private float settleY;

    public void SettleIntoPit(float surfaceY, bool immediate)
    {
        settleY = surfaceY - GetColliderTopOffset();
        isLocked = true;

        if (immediate || transform.position.y <= settleY + 0.001f)
        {
            FinishSettle();
            return;
        }

        settling = true;
    }

    private void FinishSettle()
    {
        settling = false;
        verticalVelocity = 0f;

        Vector3 pos = transform.position;
        pos.y = settleY;
        if (isPushing)
        {
            pos.x = pushTargetPos.x;
            pos.z = pushTargetPos.z;
            isPushing = false;
        }

        transform.position = pos;
        if (rb != null) rb.position = pos;
    }

    private float GetColliderTopOffset()
    {
        float best = 0f;
        foreach (BoxCollider box in GetComponents<BoxCollider>())
        {
            if (box.isTrigger) continue;
            float top = (box.center.y + box.size.y * 0.5f) * transform.lossyScale.y;
            if (top > best) best = top;
        }

        return best > 0f ? best : GridSystem.Instance.TileHeight;
    }

    private bool TrySettleIntoTrapAtCurrentCell()
    {
        LocationRoot root = GetComponentInParent<LocationRoot>();
        LocationGrid grid = root != null ? root.Grid : null;
        if (grid == null) return false;

        Vector2Int cell = CurrentCell;
        if (!grid.InBounds(cell)) return false;

        TrapTile trap = grid.Traps[cell.x, cell.y];
        if (trap == null) return false;

        trap.SettlePushable(this, immediate: true);
        return true;
    }

    // ★ 밀려서 움직이는 오브젝트라 '현재 셀'은 식별자가 될 수 없습니다.
    //   생성 직후(아직 안 움직였을 때) 확정한 원래 셀을 id로 쓰고, 현재 셀은 상태로 저장합니다.
    private Vector2Int originCell;

    // ── 상호작용 (IInteractable) ─────────────────────────────

    // 함정을 메워 잠긴 오브젝트는 밀 수 없습니다.
    public bool CanInteract(PlayerController player) => !isLocked;

    // 방향 스냅·장애물 검사·실제 밀기 요청은 PushState_Player.Enter()가 담당합니다.
    public void Interact(PlayerController player)
        => player.stateMachine.ChangeState<PushState_Player>();

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

        if (state.flag)
        {
            isLocked = true;
            if (TrySettleIntoTrapAtCurrentCell()) return;
        }

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
        if (isLocked && !settling) return;

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
        float nextY;
        if (settling)
        {
            bool onFloor = TryFindGround(current + Vector3.up * 0.1f, groundCheckDistance + 0.1f, out RaycastHit floorHit, ignoreTrapFloors: true);

            if (onFloor && verticalVelocity <= 0f && floorHit.point.y > settleY + 0.001f)
            {
                verticalVelocity = 0f;
                nextY = floorHit.point.y;
            }
            else
            {
                verticalVelocity += gravity * gravityMultiplier * Time.deltaTime;
                nextY = current.y + verticalVelocity * Time.deltaTime;
                if (nextY <= settleY)
                {
                    FinishSettle();
                    return;
                }
            }
        }
        else
        {
            bool grounded = TryFindGround(current + Vector3.up * 0.1f, groundCheckDistance + 0.1f, out RaycastHit hit);

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
        }

        // 3. 수평+수직을 한 번에 합쳐서 단 한 번만 MovePosition 호출
        rb.MovePosition(new Vector3(nextX, nextY, nextZ));
    }

    private bool TryFindGround(Vector3 origin, float maxDistance, out RaycastHit hit, bool ignoreTrapFloors = false)
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
            if (ignoreTrapFloors && candidate.collider.GetComponentInParent<TrapTile>() != null)
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
