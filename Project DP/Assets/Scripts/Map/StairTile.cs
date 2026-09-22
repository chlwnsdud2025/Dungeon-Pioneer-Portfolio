using UnityEngine;

[RequireComponent(typeof(TransitionPath))]
public class StairTile : MonoBehaviour
{
    public enum StairKind { Entrance, Exit }

    [SerializeField] private StairKind kind;

    private TransitionPath path;

    public StairKind Kind => kind;
    public Vector2Int Cell => LocationGrid.WorldToCell(transform.position);

    public TransitionPath Path
    {
        get
        {
            if (path == null) path = GetComponent<TransitionPath>();
            return path;
        }
    }

    /// <summary>플레이어가 걸어 나와 서게 되는 칸. 여기가 막혀 있으면 다른 칸을 찾아야 합니다.</summary>
    public Vector2Int FrontCell => LocationGrid.WorldToCell(Path.OutsidePosition);
}
