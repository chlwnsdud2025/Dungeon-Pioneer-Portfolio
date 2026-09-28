using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 타일이 아닌 자유 배치 소품(언덕·나무·울타리·건물)을 A* 장애물로 등록합니다.
/// 자식까지 포함한 콜라이더 영역이 덮는 칸이 막힌 칸이 됩니다.
///
/// 트리거와 꺼진 콜라이더는 무시합니다 — 실제로 몸이 부딪히는 영역만 막아야
/// 지나갈 수 있는 칸을 A*가 돌아가는 일이 없습니다.
/// </summary>
[DisallowMultipleComponent]
public class GridBlocker : MonoBehaviour
{
    [Tooltip("콜라이더가 칸 가장자리를 이 거리(m)까지 파고드는 것은 무시합니다. " +
             "나뭇가지 끝이 옆 칸을 살짝 걸쳤다고 그 칸까지 막히지 않게 합니다.")]
    [SerializeField, Range(0f, 0.9f)] private float edgeTolerance = 0.2f;

    // 경고는 런타임에 한 번만. CollectCoveredCells는 에디터 기즈모가 매 프레임 부르므로 거기서 내면 로그가 쏟아집니다.
    private void Start()
    {
        foreach (Collider col in GetComponentsInChildren<Collider>())
            if (!col.isTrigger && col.enabled) return;

        Debug.LogWarning($"[GridBlocker] '{name}'에 막을 콜라이더가 없습니다. 트리거가 아닌 콜라이더를 붙여주세요.", this);
    }

    public void CollectCoveredCells(List<Vector2Int> result)
    {
        float half = GridSystem.Instance.TileSize * 0.5f - edgeTolerance;

        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            if (col.isTrigger || !col.enabled) continue;

            Bounds bounds = col.bounds;
            Vector2Int min = LocationGrid.WorldToCell(bounds.min);
            Vector2Int max = LocationGrid.WorldToCell(bounds.max);

            for (int x = min.x; x <= max.x; x++)
            {
                for (int z = min.y; z <= max.y; z++)
                {
                    Vector2Int cell = new Vector2Int(x, z);
                    Vector3 center = LocationGrid.CellToWorld(cell);

                    bool overlaps = bounds.min.x < center.x + half && bounds.max.x > center.x - half
                                 && bounds.min.z < center.z + half && bounds.max.z > center.z - half;

                    if (overlaps && !result.Contains(cell))
                        result.Add(cell);
                }
            }
        }
    }
}
