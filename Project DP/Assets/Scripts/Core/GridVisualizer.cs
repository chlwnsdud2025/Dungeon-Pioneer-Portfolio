using UnityEngine;

public class GridVisualizer : MonoBehaviour
{
    [Header("표시할 격자 범위 (칸 수)")]
    public int width = 17;
    public int height = 17;

    [Header("표시 옵션")]
    [Tooltip("격자를 그릴 높이(Y). 바닥 높이에 맞추세요.")]
    public float yHeight = 0f;
    public Color lineColor = new Color(1f, 1f, 1f, 0.25f);
    [Tooltip("체크하면 이 오브젝트를 '선택했을 때만' 표시 (평소엔 안 보여 깔끔)")]
    public bool onlyWhenSelected = false;

    private void OnDrawGizmos()
    {
        if (!onlyWhenSelected) DrawGrid();
    }

    private void OnDrawGizmosSelected()
    {
        if (onlyWhenSelected) DrawGrid();
    }

    private void DrawGrid()
    {
        GridSystem grid = GridSystem.Instance;   // ★ 그리드 규칙을 여기서 그대로 읽어옴 (동기화 보장)
        if (grid == null) return;

        float tile = grid.TileSize;
        Vector3 origin = grid.Origin;
        float half = tile * 0.5f;                 // 칸 '경계'는 칸 중심에서 tileSize/2 만큼 떨어진 곳

        Gizmos.color = lineColor;

        // 격자 전체가 차지하는 범위 (칸 0의 왼쪽 경계 ~ 마지막 칸의 오른쪽 경계)
        float minX = origin.x - half;
        float maxX = origin.x + width * tile - half;
        float minZ = origin.z - half;
        float maxZ = origin.z + height * tile - half;

        // 세로선들 (Z 방향으로 긋는 선) — width+1개
        for (int x = 0; x <= width; x++)
        {
            float px = origin.x + x * tile - half;
            Gizmos.DrawLine(new Vector3(px, yHeight, minZ), new Vector3(px, yHeight, maxZ));
        }
        // 가로선들 (X 방향으로 긋는 선) — height+1개
        for (int z = 0; z <= height; z++)
        {
            float pz = origin.z + z * tile - half;
            Gizmos.DrawLine(new Vector3(minX, yHeight, pz), new Vector3(maxX, yHeight, pz));
        }
    }
}