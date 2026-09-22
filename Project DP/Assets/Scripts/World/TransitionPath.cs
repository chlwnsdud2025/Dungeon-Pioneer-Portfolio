using UnityEngine;

public class TransitionPath : MonoBehaviour
{
    [Tooltip("플레이어가 서 있는 바깥 지점. 도착 연출의 끝, 출발 연출의 시작입니다.")]
    [SerializeField] private Transform outsidePoint;
    [Tooltip("경로가 꺾이는 중간 지점. 문턱이나 계단 발치에 둡니다.")]
    [SerializeField] private Transform pivotPoint;
    [Tooltip("플레이어가 사라지는 안쪽 지점. 출발 연출의 끝, 도착 연출의 시작입니다.")]
    [SerializeField] private Transform insidePoint;

    [Header("기본값")]
    [Min(0.01f)] public float moveSpeed = 4f;
    [Min(0f)] public float fadeDuration = 0.4f;

    public bool IsValid => outsidePoint != null && pivotPoint != null && insidePoint != null;

    public Vector3 OutsidePosition => outsidePoint != null ? outsidePoint.position : transform.position;
    public Vector3 PivotPosition => pivotPoint != null ? pivotPoint.position : transform.position;
    public Vector3 InsidePosition => insidePoint != null ? insidePoint.position : transform.position;

    /// <summary>바깥 → 안쪽. 문으로 걸어 들어가며 사라지는 경로.</summary>
    public Vector3[] EnterPath => new[] { PivotPosition, InsidePosition };

    /// <summary>안쪽 → 바깥. 문에서 걸어 나오는 경로.</summary>
    public Vector3[] ExitPath => new[] { PivotPosition, OutsidePosition };

    private void Reset()
    {
        EnsurePoints();
    }

    /// <summary>비어 있는 지점을 자식 오브젝트로 만들어 둡니다. 씬 뷰에서 끌어다 배치하세요.</summary>
    [ContextMenu("경로 지점 만들기")]
    public void EnsurePoints()
    {
        outsidePoint = EnsurePoint(outsidePoint, "Outside", new Vector3(0f, 0f, 1.5f));
        pivotPoint = EnsurePoint(pivotPoint, "Pivot", Vector3.zero);
        insidePoint = EnsurePoint(insidePoint, "Inside", new Vector3(0f, 1.5f, -1.5f));
    }

    private Transform EnsurePoint(Transform existing, string pointName, Vector3 localPosition)
    {
        if (existing != null) return existing;

        Transform found = transform.Find(pointName);
        if (found != null) return found;

        GameObject go = new GameObject(pointName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPosition;
        return go.transform;
    }

    private void OnDrawGizmos()
    {
        if (!IsValid) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(OutsidePosition, PivotPosition);
        Gizmos.DrawLine(PivotPosition, InsidePosition);
        Gizmos.DrawWireSphere(OutsidePosition, 0.25f);
        Gizmos.DrawWireSphere(PivotPosition, 0.15f);
        Gizmos.DrawWireSphere(InsidePosition, 0.25f);
    }
}
