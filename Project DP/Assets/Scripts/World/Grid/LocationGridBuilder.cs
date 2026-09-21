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
        }

        return grid;
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
