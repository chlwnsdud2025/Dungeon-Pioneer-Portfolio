using UnityEngine;

// 밟으면 연결된 ArrowTrap(들)을 발사시키는 발판.
// 발판과 발사기를 분리했으므로 "발판은 바닥, 화살은 맞은편 벽에서" 같은 배치가 가능합니다.
[RequireComponent(typeof(Collider))]
public class PressurePlate : MonoBehaviour
{
    [Header("연결된 화살 함정 (여럿 가능)")]
    [Tooltip("이 발판을 밟으면 발사시킬 ArrowTrap들. 여러 개를 연결하면 동시에 일제사격합니다.")]
    public ArrowTrap[] arrowTraps;

    [Header("Trigger Settings")]
    [Tooltip("적이 밟아도 발동할지 여부. 꺼두면 플레이어만 발동합니다.")]
    public bool triggeredByEnemy = false;
    [Tooltip("true면 화살이 플레이어/방패/벽에 맞아 사라진 뒤에만 다시 발사합니다.")]
    public bool waitForArrowResolve = true;
    [Tooltip("waitForArrowResolve가 꺼져 있을 때만 사용하는 시간 쿨다운(초).")]
    public float cooldown = 1f;
    [Tooltip("true면 최초 1회만 발동합니다.")]
    public bool singleUse = false;
    [Tooltip("발판 트리거 크기를 floor_tile_small 비주얼의 XZ에 맞춥니다.")]
    public bool fitTriggerToFloorTile = true;
    [Tooltip("트리거 높이(로컬 Y). 플레이어 발 위치에 맞춥니다.")]
    public float triggerHeight = 2f;
    [Tooltip("트리거 중심 높이(로컬 Y).")]
    public float triggerCenterY = 1f;

    private bool spent = false;
    private bool canFire = true;
    private int pendingArrows = 0;
    private bool occupiedLastFrame = false;
    private float lastFireTime = -999f;
    private int lastFireFrame = -1;

    private void Awake()
    {
        if (fitTriggerToFloorTile)
            FitTriggerToFloorTile();
    }

    private void Start()
    {
        if (!HasValidTrapReference())
        {
            Transform root = transform;
            while (root.parent != null) root = root.parent;
            ArrowTrapRuntimeWire.WireMap(root.gameObject);
        }
    }

    private void Update()
    {
        if (spent) return;
        if (!waitForArrowResolve && Time.time < lastFireTime + cooldown) return;

        bool occupied = IsOccupied();
        if (occupied && !occupiedLastFrame)
            TryActivateFromStep();
        occupiedLastFrame = occupied;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsValidActivator(other)) return;
        TryActivateFromStep();
    }

    public void NotifyArrowResolved()
    {
        if (!waitForArrowResolve) return;

        pendingArrows = Mathf.Max(0, pendingArrows - 1);
        if (pendingArrows == 0)
            canFire = true;
    }

    private void TryActivateFromStep()
    {
        if (Time.frameCount == lastFireFrame) return;
        if (spent) return;
        if (!canFire) return;
        if (!waitForArrowResolve && Time.time < lastFireTime + cooldown) return;

        lastFireFrame = Time.frameCount;
        FireAll();
    }

    private bool IsOccupied()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger == null || !trigger.isTrigger) return false;

        Bounds bounds = trigger.bounds;
        if (IsPlayerInTrigger(bounds)) return true;
        return triggeredByEnemy && IsEnemyInTrigger(bounds);
    }

    private bool IsValidActivator(Collider other)
    {
        bool isPlayer = IsPlayer(other);
        bool isEnemy = other.GetComponentInParent<EnemyController>() != null;
        return isPlayer || (triggeredByEnemy && isEnemy);
    }

    private bool HasValidTrapReference()
    {
        if (arrowTraps == null || arrowTraps.Length == 0) return false;
        foreach (ArrowTrap trap in arrowTraps)
        {
            if (trap != null && trap.gameObject.scene.IsValid()) return true;
        }
        return false;
    }

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private static bool IsPlayerInTrigger(Bounds bounds)
    {
        PlayerController player = PlayerManager.Instance != null ? PlayerManager.Instance.Controller : null;
        if (player == null) return false;

        CharacterController cc = player.characterController != null
            ? player.characterController
            : player.GetComponent<CharacterController>();
        if (cc == null) return bounds.Contains(player.transform.position);

        Vector3 center = player.transform.position + cc.center;
        float verticalReach = cc.height * 0.5f;
        Vector3 feet = center - Vector3.up * verticalReach;
        return bounds.Contains(feet) || bounds.Contains(center);
    }

    private static bool IsEnemyInTrigger(Bounds bounds)
    {
        EnemyController[] enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        foreach (EnemyController enemy in enemies)
        {
            if (enemy != null && bounds.Contains(enemy.transform.position)) return true;
        }
        return false;
    }

    private static bool IsPlayer(Collider other)
    {
        if (other == null) return false;
        if (other.CompareTag("Player")) return true;
        if (other.GetComponentInParent<PlayerController>() != null) return true;
        Transform root = other.transform.root;
        return root != null && root.CompareTag("Player");
    }

    private void FireAll()
    {
        if (arrowTraps == null || arrowTraps.Length == 0)
        {
            Debug.LogWarning("[PressurePlate] arrowTraps가 비어 있음! 인스펙터에서 ArrowTrap을 연결했는지 확인하세요.", this);
            return;
        }

        int firedCount = 0;
        for (int i = 0; i < arrowTraps.Length; i++)
        {
            ArrowTrap trap = arrowTraps[i];
            if (trap == null) continue;
            if (trap.Fire(this)) firedCount++;
        }

        if (firedCount == 0) return;

        lastFireTime = Time.time;
        if (singleUse)
        {
            spent = true;
            return;
        }

        if (waitForArrowResolve)
        {
            canFire = false;
            pendingArrows += firedCount;
        }
    }

    public void FitTriggerToFloorTile()
    {
        Transform floorTile = FindFloorTileVisual();
        Renderer renderer = floorTile != null ? floorTile.GetComponentInChildren<Renderer>() : null;
        BoxCollider box = GetComponent<BoxCollider>();
        if (renderer == null || box == null) return;

        Bounds worldBounds = renderer.bounds;
        Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);
        Vector3 localSize = transform.InverseTransformVector(worldBounds.size);

        box.isTrigger = true;
        box.center = new Vector3(localCenter.x, triggerCenterY, localCenter.z);
        box.size = new Vector3(Mathf.Abs(localSize.x), triggerHeight, Mathf.Abs(localSize.z));
    }

    private Transform FindFloorTileVisual()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name.Contains("floor_tile_small"))
                return child;
        }
        return null;
    }
}
