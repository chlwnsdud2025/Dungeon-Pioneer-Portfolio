using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PushableObject : MonoBehaviour
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

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
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
        bool grounded = Physics.Raycast(current + Vector3.up * 0.1f, Vector3.down,
            out RaycastHit hit, groundCheckDistance + 0.1f, groundLayer);

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