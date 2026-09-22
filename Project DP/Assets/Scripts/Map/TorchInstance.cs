using System.Collections.Generic;
using UnityEngine;

public class TorchInstance : MonoBehaviour, ITarget
{
    [Header("Lifetime")]
    [Tooltip("설치 후 완전히 사라지기까지의 시간(초).")]
    [Min(1f), SerializeField] private float lifetimeSeconds = 180f;
    [Tooltip("소멸 직전 이 시간(초) 동안 불빛이 서서히 약해집니다. 0이면 페이드 없이 바로 꺼집니다.")]
    [Min(0f), SerializeField] private float fadeOutSeconds = 10f;
    [Tooltip("페이드가 끝나도 빛 범위는 원래의 이 비율 밑으로 줄이지 않습니다. (불씨가 남은 느낌)")]
    [Range(0f, 1f), SerializeField] private float minRangeRatio = 0.3f;

    [Header("Damage")]
    [Tooltip("한 대 맞을 때마다 깎이는 남은 시간(초). 피해량과 무관합니다. 수명 180초에 60이면 세 방에 꺼집니다.")]
    [Min(0f), SerializeField] private float secondsPerHit = 60f;

    private WallObject wall;
    private Vector2Int wallCell;
    private Vector3Int faceKey;

    private float expiresAt;      // Time.time 기준 마감 시각
    private bool initialized;     // Initialize가 불렸는지 (Instantiate 직후의 OnEnable을 건너뛰기 위함)
    private bool expired;

    private Light torchLight;
    private float baseIntensity;
    private float baseRange;

    // 적이 횃불을 찾을 때 씁니다. 콜라이더 검색 대신 켜져 있는 횃불만 모아두므로 비활성 층은 자동 제외됩니다.
    private static readonly HashSet<TorchInstance> ActiveTorches = new HashSet<TorchInstance>();

    public static IEnumerable<TorchInstance> EnumerateActiveTorches() => ActiveTorches;

    /// <summary>처음 설치될 때의 수명(초). 게이지가 남은 비율을 구할 때 씁니다.</summary>
    public float Lifetime => lifetimeSeconds;

    /// <summary>남은 시간(초). 만료됐으면 0 이하. Initialize 전에는 수명 전체.</summary>
    public float Remaining => initialized ? expiresAt - Time.time : lifetimeSeconds;

    /// <summary>남은 비율 0~1. 게이지 fill에 그대로 쓸 수 있습니다.</summary>
    public float RemainingRatio => Mathf.Clamp01(Remaining / lifetimeSeconds);

    public bool IsExpired => initialized && Remaining <= 0f;

    /// <summary>붙어 있는 면의 바깥 방향 (6방향 중 하나).</summary>
    public Vector3 FaceNormal => faceKey;

    // ── ITarget ────────────────────────────────────────────────

    // 'this != null'은 파괴된 오브젝트 검사입니다. 적은 이 횃불을 ITarget으로 들고 있어 Unity의 null 판정이
    // 안 걸리므로, 파괴 뒤에도 IsAlive가 확실히 false를 돌려줘야 Transform 접근 전에 걸러집니다.
    public bool IsAlive => this != null && initialized && !expired && Remaining > 0f;
    public Transform Transform => transform;
    public Vector3 AimPoint => transform.position + FaceNormal * 0.3f;
    public Vector2Int ApproachCell => wallCell + new Vector2Int(faceKey.x, faceKey.z);
    public bool IsMobile => false;

    /// <summary>
    /// 한 대에 secondsPerHit만큼 남은 시간을 깎습니다. 다 깎이면 다음 Update에서 소멸합니다.
    /// 피해량은 '맞았는지'만 판단하는 데 씁니다(0 이하는 무시).
    /// </summary>
    public void TakeDamage(float damage, Transform attacker)
    {
        if (!IsAlive || damage <= 0f) return;

        expiresAt -= secondsPerHit;
        ApplyFade();   // 게이지·밝기에 바로 반영
    }

    private void Awake()
    {
        torchLight = GetComponentInChildren<Light>(true);
        if (torchLight != null)
        {
            baseIntensity = torchLight.intensity;
            baseRange = torchLight.range;
        }
    }

    private void OnDisable() => ActiveTorches.Remove(this);

    /// <summary>
    /// 설치·복원 직후 1회 호출. 남은 시간을 받아 마감 시각을 정합니다.
    /// 새로 설치할 때는 Lifetime을, 복원할 때는 기록에서 계산한 남은 초를 넘깁니다.
    /// </summary>
    public void Initialize(WallObject wall, Vector2Int wallCell, Vector3Int faceKey, float remainingSeconds)
    {
        this.wall = wall;
        this.wallCell = wallCell;
        this.faceKey = faceKey;
        expiresAt = Time.time + remainingSeconds;
        initialized = true;

        ApplyFade();
    }

    private void OnEnable()
    {
        ActiveTorches.Add(this);

        // 비활성 층에 있는 동안 Update가 멈춰 있었으므로, 층이 다시 켜지는 순간 만료를 바로 판정한다.
        if (!initialized) return;
        if (Remaining <= 0f) Expire();
        else ApplyFade();
    }

    private void Update()
    {
        if (!initialized || expired) return;

        if (Remaining <= 0f)
        {
            Expire();
            return;
        }
        ApplyFade();
    }

    /// <summary>남은 시간에서 밝기를 파생시킵니다. 누적값이 없어 재활성·복원 직후 첫 프레임부터 맞습니다.</summary>
    private void ApplyFade()
    {
        if (torchLight == null) return;

        float t = fadeOutSeconds > 0f ? Mathf.Clamp01(Remaining / fadeOutSeconds) : 1f;
        torchLight.intensity = baseIntensity * t;
        torchLight.range = baseRange * Mathf.Lerp(minRangeRatio, 1f, t);
    }

    private void Expire()
    {
        if (expired) return;
        expired = true;

        // 같은 면에 다시 설치할 수 있게 벽의 표시를 지운다.
        if (wall != null) wall.ClearTorchFace(faceKey);

        // 미니맵의 activeTorches는 파괴된 Transform(null)을 스스로 걸러내므로 따로 알릴 필요가 없다.
        Destroy(gameObject);
    }

    public TorchRecord ToRecord() => new TorchRecord
    {
        wallCell = wallCell,
        faceKey = faceKey,
        remainingSeconds = Remaining,
        capturedAt = Time.time
    };

    /// <summary>
    /// 벽 콜라이더의 한 면(faceKey)에 횃불을 붙일 위치·회전. 설치와 복원이 반드시 같은 규칙을 써야
    /// 복원된 횃불이 처음 설치했던 자리에 그대로 나오므로, 두 경로가 이 함수 하나를 공유합니다.
    /// 위치는 벽 AABB 중심에서 그 면의 표면까지 민 지점이고, 회전은 면 바깥을 바라봅니다.
    /// </summary>
    public static void GetMountPose(Collider wallCollider, Vector3Int faceKey, out Vector3 position, out Quaternion rotation)
    {
        Vector3 normal = faceKey;
        Bounds bounds = wallCollider.bounds;
        position = bounds.center + Vector3.Scale(normal, bounds.extents);
        rotation = Quaternion.LookRotation(normal);
    }

    // ── 테스트용 (플레이 중 컴포넌트 우클릭) ──────────────────────

    [ContextMenu("즉시 소멸")]
    private void DebugExpireNow() => expiresAt = Time.time;

    [ContextMenu("10초 남기기")]
    private void DebugLeaveTenSeconds() => expiresAt = Time.time + 10f;
}
