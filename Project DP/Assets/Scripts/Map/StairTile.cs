using UnityEngine;

public class StairTile : MonoBehaviour
{
    public enum StairKind { Entrance, Exit }

    [SerializeField] private StairKind kind;
    [Tooltip("플레이어가 서는 앞 타일 방향. 계단 안쪽은 이 방향의 반대입니다.")]
    [SerializeField] private Vector3 frontLocal = Vector3.forward;
    [Min(0f), SerializeField] private float insideDepth = 1f;
    [Min(0f), SerializeField] private float heightDelta = 1.5f;
    [Tooltip("타일 바닥에서 플레이어 Transform까지의 높이. 기존 스폰 높이와 같은 1.5가 기본값입니다.")]
    [SerializeField] private float playerHeight = 1.5f;

    public StairKind Kind => kind;
    public Vector2Int Cell => LocationGrid.WorldToCell(transform.position);

    public Vector3 FrontDirection
    {
        get
        {
            Vector3 direction = transform.TransformDirection(frontLocal);
            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
        }
    }

    public Vector3 FootPoint => transform.position + Vector3.up * playerHeight;
    public Vector3 HiddenPoint => FootPoint - FrontDirection * insideDepth +
                                  Vector3.up * (kind == StairKind.Entrance ? heightDelta : -heightDelta);

    public Vector2Int FrontCell
    {
        get
        {
            Vector3 position = transform.position + FrontDirection * GridSystem.Instance.TileSize;
            return LocationGrid.WorldToCell(position);
        }
    }

    public Vector3 FrontPoint => LocationGrid.CellToWorld(FrontCell) + Vector3.up * playerHeight;

    private void OnDrawGizmosSelected()
    {
        if (GridSystem.Instance == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(FrontPoint, FootPoint);
        Gizmos.DrawLine(FootPoint, HiddenPoint);
        Gizmos.DrawWireSphere(FrontPoint, 0.2f);
        Gizmos.DrawWireSphere(HiddenPoint, 0.2f);
    }
}
