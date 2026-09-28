using System.Collections.Generic;
using UnityEngine;

public static class LocationGridBuilder
{
    public static LocationGrid Build(GameObject locationInstance)
    {
        return Build(locationInstance, new Vector2Int(1, 1));
    }

    public static LocationGrid Build(GameObject locationInstance, Vector2Int fallbackSize)
    {
        List<Transform> roots = CollectTileRoots(locationInstance);
        LocationGrid grid = new LocationGrid(MeasureSize(roots, fallbackSize));

        foreach (Transform root in roots)
        {
            if (!root.CompareTag("Wall")) continue;

            Vector2Int cell = LocationGrid.WorldToCell(root.position);
            if (!grid.InBounds(cell)) continue;

            grid.Cells[cell.x, cell.y] = CellType.Wall;
        }

        foreach (Transform root in roots)
        {
            Vector2Int cell = LocationGrid.WorldToCell(root.position);
            if (!grid.InBounds(cell)) continue;

            if (root.CompareTag("Pushable"))
            {
                grid.AddPushable(root.GetComponent<PushableObject>());
                continue;
            }

            if (root.CompareTag("Chest"))
            {
                grid.AddChest(root);
                grid.StaticBlockers[cell.x, cell.y] = true;
                continue;
            }

            if (grid.Cells[cell.x, cell.y] == CellType.Wall) continue;

            if (root.CompareTag("Trap"))
            {
                TrapTile trap = root.GetComponent<TrapTile>();
                if (trap != null)
                {
                    grid.Cells[cell.x, cell.y] = CellType.Pit;
                    grid.Traps[cell.x, cell.y] = trap;
                    grid.AddWalkableCenter(root.position);
                }
                else if (root.GetComponent<PressurePlate>() != null)
                {
                    grid.Cells[cell.x, cell.y] = CellType.Plate;
                    grid.AddWalkableCenter(root.position);
                }
                continue;
            }

            CellType existing = grid.Cells[cell.x, cell.y];
            if (LocationGrid.IsTrapTerrain(existing))
            {
                grid.AddWalkableCenter(root.position);
                continue;
            }

            if (root.CompareTag("Entrance"))
                grid.Cells[cell.x, cell.y] = CellType.Entrance;
            else if (root.CompareTag("Exit"))
                grid.Cells[cell.x, cell.y] = CellType.Exit;
            else if (root.CompareTag("Floor") && existing == CellType.None)
                grid.Cells[cell.x, cell.y] = CellType.Floor;

            grid.AddWalkableCenter(root.position);
        }

        if (locationInstance != null)
        {
            foreach (PushableObject pushable in locationInstance.GetComponentsInChildren<PushableObject>(true))
                grid.AddPushable(pushable);

            ApplyBlockers(grid, locationInstance.GetComponentsInChildren<GridBlocker>());
        }

        return grid;
    }

    /// <summary>
    /// 자유 배치 소품(언덕·나무·울타리)이 덮는 칸을 지나갈 수 없게 표시합니다.
    /// 타일 태그만으로는 A*가 이런 소품을 모르기 때문에 필요합니다. 던전 맵에는 GridBlocker가 없어 영향이 없습니다.
    /// </summary>
    public static void ApplyBlockers(LocationGrid grid, IEnumerable<GridBlocker> blockers)
    {
        if (grid == null || blockers == null) return;

        List<Vector2Int> cells = new List<Vector2Int>();
        foreach (GridBlocker blocker in blockers)
        {
            if (blocker == null) continue;

            cells.Clear();
            blocker.CollectCoveredCells(cells);

            foreach (Vector2Int cell in cells)
            {
                if (grid.InBounds(cell))
                    grid.StaticBlockers[cell.x, cell.y] = true;
            }
        }
    }

    /// <summary>
    /// 바닥 계열 타일 중 가장 작은 칸. 장소 맵은 이 값이 (0,0)이어야 합니다 —
    /// LocationGrid에는 원점 오프셋이 없어 음수 칸은 그리드에서 조용히 빠집니다.
    /// 원점 판정은 DungeonGenerator.NormalizeMapOrigin과 같은 기준(벽 제외)을 씁니다.
    /// </summary>
    public static bool TryGetFloorMinCell(GameObject locationInstance, out Vector2Int minCell)
    {
        minCell = Vector2Int.zero;
        bool found = false;

        foreach (Transform root in CollectTileRoots(locationInstance))
        {
            if (!(root.CompareTag("Floor") || root.CompareTag("Entrance") ||
                  root.CompareTag("Exit") || root.CompareTag("Trap")))
                continue;

            Vector2Int cell = LocationGrid.WorldToCell(root.position);
            if (!found)
            {
                minCell = cell;
                found = true;
                continue;
            }

            minCell = Vector2Int.Min(minCell, cell);
        }

        return found;
    }

    private static List<Transform> CollectTileRoots(GameObject locationInstance)
    {
        List<Transform> roots = new List<Transform>();
        if (locationInstance == null) return roots;

        Transform locationRoot = locationInstance.transform;
        foreach (Transform obj in locationInstance.GetComponentsInChildren<Transform>(true))
        {
            if (LocationGrid.IsTileRoot(obj, locationRoot))
                roots.Add(obj);
        }

        return roots;
    }

    private static Vector2Int MeasureSize(List<Transform> roots, Vector2Int fallbackSize)
    {
        if (roots == null || roots.Count == 0) return fallbackSize;

        int maxX = 0;
        int maxZ = 0;
        foreach (Transform root in roots)
        {
            Vector2Int cell = LocationGrid.WorldToCell(root.position);
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y > maxZ) maxZ = cell.y;
        }

        return new Vector2Int(Mathf.Max(1, maxX + 1), Mathf.Max(1, maxZ + 1));
    }
}
