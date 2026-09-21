using System.Collections.Generic;
using UnityEngine;

public enum CellType : byte
{
    None,
    Floor,
    Wall,
    Pit,
    Plate,
    Entrance,
    Exit
}

public class LocationGrid
{
    public Vector2Int Size { get; }
    public CellType[,] Cells { get; }
    public TrapTile[,] Traps { get; }
    public bool[,] StaticBlockers { get; }

    private readonly List<PushableObject> pushables = new List<PushableObject>();
    private readonly List<Transform> chests = new List<Transform>();
    private readonly List<Vector3> walkableCellCenters = new List<Vector3>();

    public IReadOnlyList<PushableObject> Pushables => pushables;
    public IReadOnlyList<Transform> Chests => chests;
    public IReadOnlyList<Vector3> WalkableCellCenters => walkableCellCenters;

    public LocationGrid(Vector2Int size)
    {
        Size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
        Cells = new CellType[Size.x, Size.y];
        Traps = new TrapTile[Size.x, Size.y];
        StaticBlockers = new bool[Size.x, Size.y];
    }

    public bool InBounds(int x, int z) => x >= 0 && x < Size.x && z >= 0 && z < Size.y;
    public bool InBounds(Vector2Int cell) => InBounds(cell.x, cell.y);

    public CellType Get(int x, int z) => InBounds(x, z) ? Cells[x, z] : CellType.None;
    public CellType Get(Vector2Int cell) => Get(cell.x, cell.y);

    public static Vector2Int WorldToCell(Vector3 world)
    {
        Vector3Int c = GridSystem.Instance.WorldToCell(world);
        return new Vector2Int(c.x, c.z);
    }

    public static Vector3 CellToWorld(Vector2Int cell)
    {
        return GridSystem.Instance.GetCellCenterWorld(new Vector3Int(cell.x, 0, cell.y));
    }

    public static bool IsWalkableTerrain(CellType type)
    {
        return type == CellType.Floor || type == CellType.Pit || type == CellType.Plate
            || type == CellType.Entrance || type == CellType.Exit;
    }

    public static bool IsTrapTerrain(CellType type)
    {
        return type == CellType.Pit || type == CellType.Plate;
    }

    public static bool HasTileTag(Transform obj)
    {
        return obj.CompareTag("Wall") || obj.CompareTag("Floor") || obj.CompareTag("Entrance") ||
               obj.CompareTag("Exit") || obj.CompareTag("Trap") || obj.CompareTag("Chest") ||
               obj.CompareTag("Pushable");
    }

    public static bool IsTileRoot(Transform obj, Transform locationRoot)
    {
        if (obj == locationRoot || !HasTileTag(obj)) return false;

        Transform parent = obj.parent;
        while (parent != null && parent != locationRoot)
        {
            if (HasTileTag(parent)) return false;
            parent = parent.parent;
        }

        return true;
    }

    internal void AddPushable(PushableObject pushable)
    {
        if (pushable != null && !pushables.Contains(pushable))
            pushables.Add(pushable);
    }

    internal void AddChest(Transform chest)
    {
        if (chest != null && !chests.Contains(chest))
            chests.Add(chest);
    }

    internal void AddWalkableCenter(Vector3 worldPosition)
    {
        walkableCellCenters.Add(worldPosition);
    }

    public bool IsPitOpen(Vector2Int cell)
    {
        if (!InBounds(cell) || Cells[cell.x, cell.y] != CellType.Pit) return false;

        TrapTile trap = Traps[cell.x, cell.y];
        return trap == null || !trap.IsFilled;
    }

    public bool IsOccupiedByPushable(Vector2Int cell)
    {
        for (int i = 0; i < pushables.Count; i++)
        {
            PushableObject pushable = pushables[i];
            if (pushable == null || !pushable.gameObject.activeInHierarchy || pushable.IsLocked) continue;
            if (pushable.CurrentCell == cell) return true;
            if (pushable.TryGetPushTargetCell(out Vector2Int target) && target == cell) return true;
        }

        return false;
    }

    public bool IsPassable(Vector2Int cell, bool allowPits)
    {
        if (!InBounds(cell)) return false;

        CellType type = Cells[cell.x, cell.y];
        if (!IsWalkableTerrain(type)) return false;
        if (StaticBlockers[cell.x, cell.y]) return false;
        if (IsOccupiedByPushable(cell)) return false;
        if (type == CellType.Pit && !allowPits && IsPitOpen(cell)) return false;

        return true;
    }
}
