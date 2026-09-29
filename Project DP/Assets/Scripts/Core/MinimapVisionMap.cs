using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 층 하나의 미니맵 시야와 탐색 기록을 픽셀 단위로 계산합니다. (그리는 쪽은 MinimapManager)
///
/// 좌표는 '타일 공간'을 씁니다. 1 = 타일 한 칸이고, 칸 (x, z)는 [x, x+1) × [z, z+1) 범위입니다.
/// 픽셀 (px, py)는 칸 (px / pixelsPerTile, py / pixelsPerTile)에 속합니다.
///
/// 광원(플레이어, 횃불)에서 반경 안의 픽셀마다 격자를 따라 선을 그어, 중간에 벽이나 밀 수 있는
/// 오브젝트가 있으면 밝히지 않습니다. 벽은 바닥과 맞닿은 표면(wallFaceThickness 두께)만 보이고,
/// 선이 그 두께보다 깊이 들어가야 닿는 픽셀은 보이지 않습니다. 그래서 벽 반대편 면은 가려집니다.
///
/// 비춰진 픽셀은 밝기(1~255)를 가집니다. 광원에서 멀수록 어두워지며, 모양은 MinimapManager가 넘겨주는 밝기표로 정합니다.
///
/// 밀 수 있는 오브젝트가 선 칸은 오브젝트 표면이 비춰지면 칸 바닥 전체가 그 표면 밝기로 보이고,
/// '밝혀진 곳'으로도 기록됩니다. 미니맵에서는 그 위에 아이콘만 얹습니다.
/// </summary>
public class MinimapVisionMap
{
    public enum PixelKind : byte
    {
        Solid,      // 벽 안쪽, 맵 밖. 항상 검정
        Floor,      // 밟을 수 있는 칸 전부 (함정 포함)
        WallFace    // 벽 칸 중 바닥과 맞닿은 표면
    }

    public int Width { get; }
    public int Height { get; }
    public int FloorPixelCount { get; }
    public float ExploredRatio => FloorPixelCount > 0 ? (float)exploredFloorCount / FloorPixelCount : 0f;

    private readonly LocationGrid grid;
    private readonly int pixelsPerTile;
    private readonly float wallFaceThickness;

    private readonly PixelKind[] kinds;
    private readonly bool[] exploredFloor;
    private readonly bool[] seenWall;
    private readonly byte[] playerLit;   // 밝기. 0 = 비추지 않음
    private readonly byte[] torchLit;
    private int exploredFloorCount;
    private RectInt playerLitRect;

    // 밀 수 있는 오브젝트가 선 칸의 밝기 (칸 단위). 그 칸의 바닥 픽셀은 픽셀 밝기 대신 이 값을 씁니다.
    private readonly byte[] playerBlockerLight;
    private readonly byte[] torchBlockerLight;

    // 시야를 막는 칸 (벽, 맵 밖, 밀 수 있는 오브젝트). 픽셀마다 여러 번 조회하므로 1차원 배열로 둡니다.
    private readonly int cellsX, cellsZ;
    private readonly bool[] staticBlocked;
    private readonly bool[] blocked;
    private readonly List<Vector2Int> lastBlockerInput = new List<Vector2Int>();

    public MinimapVisionMap(LocationGrid grid, int pixelsPerTile, float wallFaceThickness)
    {
        this.grid = grid;
        this.pixelsPerTile = Mathf.Max(1, pixelsPerTile);
        this.wallFaceThickness = wallFaceThickness;

        Width = grid.Size.x * this.pixelsPerTile;
        Height = grid.Size.y * this.pixelsPerTile;

        int count = Width * Height;
        kinds = new PixelKind[count];
        exploredFloor = new bool[count];
        seenWall = new bool[count];
        playerLit = new byte[count];
        torchLit = new byte[count];

        cellsX = grid.Size.x;
        cellsZ = grid.Size.y;
        staticBlocked = new bool[cellsX * cellsZ];
        for (int z = 0; z < cellsZ; z++)
        {
            for (int x = 0; x < cellsX; x++)
            {
                CellType type = grid.Cells[x, z];
                staticBlocked[z * cellsX + x] = type == CellType.Wall || type == CellType.None;
            }
        }
        blocked = (bool[])staticBlocked.Clone();
        playerBlockerLight = new byte[cellsX * cellsZ];
        torchBlockerLight = new byte[cellsX * cellsZ];

        FloorPixelCount = ClassifyPixels();
    }

    // ── 조회 ────────────────────────────────────────────────

    public PixelKind KindAt(int index) => kinds[index];

    /// <summary>
    /// 지금 비추고 있는 바닥·벽면의 밝기. 0이면 지금은 안 보이는 곳이고, 1~255는 광원에 가까울수록 큽니다.
    /// 밀 수 있는 오브젝트가 선 칸의 바닥은 칸 전체가 오브젝트 표면의 밝기를 따릅니다.
    /// </summary>
    public byte SurfaceLight(int x, int y)
    {
        int i = y * Width + x;
        if (kinds[i] == PixelKind.Floor)
        {
            int cell = (y / pixelsPerTile) * cellsX + x / pixelsPerTile;
            if (blocked[cell]) return Max(playerBlockerLight[cell], torchBlockerLight[cell]);
        }

        return Max(playerLit[i], torchLit[i]);
    }

    /// <summary>지금 광원이 이 픽셀을 비추고 있는지.</summary>
    public bool IsLit(int x, int y) => SurfaceLight(x, y) > 0;

    /// <summary>
    /// 칸이 지금 받는 가장 밝은 빛 (0 = 칸 어디에도 빛이 안 닿음). 함정·밀 수 있는 오브젝트처럼 칸에 붙은 표시용.
    /// 밀 수 있는 오브젝트가 선 칸은 오브젝트 표면의 밝기입니다.
    /// </summary>
    public byte CellLight(Vector2Int cell)
    {
        if (!grid.InBounds(cell)) return 0;

        int c = cell.y * cellsX + cell.x;
        if (blocked[c]) return Max(playerBlockerLight[c], torchBlockerLight[c]);

        byte brightest = 0;
        for (int y = cell.y * pixelsPerTile; y < (cell.y + 1) * pixelsPerTile; y++)
        {
            for (int x = cell.x * pixelsPerTile; x < (cell.x + 1) * pixelsPerTile; x++)
            {
                int i = y * Width + x;
                brightest = Max(brightest, Max(playerLit[i], torchLit[i]));
            }
        }

        return brightest;
    }

    public bool IsCellLit(Vector2Int cell) => CellLight(cell) > 0;

    private static byte Max(byte a, byte b) => a > b ? a : b;

    public bool IsExploredFloor(int index) => exploredFloor[index];

    public bool IsSeenWall(int index) => seenWall[index];

    // ── 시야 차단 (밀 수 있는 오브젝트) ────────────────────────

    /// <summary>
    /// 시야를 막는 칸 목록을 갱신합니다. 지난번과 같으면 false를 돌려주므로,
    /// true일 때만 광원을 다시 계산하면 됩니다.
    /// </summary>
    public bool SetDynamicBlockers(List<Vector2Int> cells)
    {
        bool changed = cells.Count != lastBlockerInput.Count;
        for (int i = 0; !changed && i < cells.Count; i++)
            changed = cells[i] != lastBlockerInput[i];

        if (!changed) return false;

        Array.Copy(staticBlocked, blocked, blocked.Length);

        lastBlockerInput.Clear();
        lastBlockerInput.AddRange(cells);

        foreach (Vector2Int cell in cells)
            if (grid.InBounds(cell)) blocked[cell.y * cellsX + cell.x] = true;

        return true;
    }

    // ── 광원 ────────────────────────────────────────────────

    /// <summary>
    /// 플레이어 시야를 새 위치로 옮깁니다. 다시 그려야 할 픽셀 범위(이전 ∪ 새 범위)를 돌려줍니다.
    /// falloff는 밝기표입니다. 칸 k는 (거리 / 반경)² = k / (길이 - 1)인 곳의 밝기(1~255)입니다.
    /// </summary>
    public RectInt LightPlayer(Vector2 center, float radius, byte[] falloff)
    {
        RectInt previous = playerLitRect;
        ClearRect(playerLit, previous);
        Array.Clear(playerBlockerLight, 0, playerBlockerLight.Length);

        RectInt lit = Illuminate(center, radius, falloff, playerLit, playerBlockerLight);
        Accumulate(playerLit, playerBlockerLight, lit);

        // 오브젝트 칸은 칸 전체를 한 밝기로 칠하므로, 반경 밖으로 삐져나간 부분까지 다시 그리게 합니다.
        playerLitRect = IncludeLitBlockerCells(lit, playerBlockerLight);
        return Union(previous, playerLitRect);
    }

    /// <summary>
    /// 켜져 있는 횃불들로 횃불 빛을 새로 만듭니다. 꺼진 횃불의 범위는 여기서 빠지지만,
    /// 탐색 기록에는 남아 있어 '밝혀졌던 곳'으로 보입니다.
    /// </summary>
    public void LightTorches(List<Vector2> centers, float radius, byte[] falloff)
    {
        Array.Clear(torchLit, 0, torchLit.Length);
        Array.Clear(torchBlockerLight, 0, torchBlockerLight.Length);

        foreach (Vector2 center in centers)
            Accumulate(torchLit, torchBlockerLight, Illuminate(center, radius, falloff, torchLit, torchBlockerLight));
    }

    private RectInt Illuminate(Vector2 center, float radius, byte[] falloff, byte[] mask, byte[] blockerLight)
    {
        if (radius <= 0f) return default;

        int x0 = Mathf.Max(0, Mathf.FloorToInt((center.x - radius) * pixelsPerTile));
        int x1 = Mathf.Min(Width - 1, Mathf.CeilToInt((center.x + radius) * pixelsPerTile));
        int y0 = Mathf.Max(0, Mathf.FloorToInt((center.y - radius) * pixelsPerTile));
        int y1 = Mathf.Min(Height - 1, Mathf.CeilToInt((center.y + radius) * pixelsPerTile));
        if (x1 < x0 || y1 < y0) return default;

        float radiusSq = radius * radius;
        float toFalloffIndex = (falloff.Length - 1) / radiusSq;
        float inv = 1f / pixelsPerTile;

        for (int y = y0; y <= y1; y++)
        {
            float py = (y + 0.5f) * inv;
            float dy = py - center.y;
            int cellZ = y / pixelsPerTile;

            for (int x = x0; x <= x1; x++)
            {
                int i = y * Width + x;
                if (kinds[i] == PixelKind.Solid) continue;

                float px = (x + 0.5f) * inv;
                float dx = px - center.x;
                float distanceSq = dx * dx + dy * dy;
                if (distanceSq > radiusSq) continue;

                int cellX = x / pixelsPerTile;
                bool endBlocked = IsBlocking(cellX, cellZ);
                if (!CanSee(center.x, center.y, px, py, endBlocked)) continue;

                // 횃불이 겹치는 곳은 더 밝은 쪽을 씁니다.
                byte light = falloff[(int)(distanceSq * toFalloffIndex)];

                if (endBlocked && kinds[i] == PixelKind.Floor)
                {
                    // 막혔는데 바닥 = 밀 수 있는 오브젝트의 표면. 칸 전체가 가장 밝은 표면 밝기를 따릅니다.
                    int cell = cellZ * cellsX + cellX;
                    if (light > blockerLight[cell]) blockerLight[cell] = light;
                }
                else if (light > mask[i])
                {
                    mask[i] = light;
                }
            }
        }

        return new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    /// <summary>
    /// 광원에서 픽셀까지 격자를 따라가며(DDA) 가리는 칸이 있는지 봅니다.
    /// 픽셀이 막힌 칸(벽·밀 수 있는 오브젝트) 안에 있으면, 그 칸에 들어선 뒤 표면 두께 이내에서 닿을 때만 보입니다.
    /// </summary>
    private bool CanSee(float ax, float ay, float bx, float by, bool endBlocked)
    {
        // 픽셀마다 불리는 가장 뜨거운 코드라 Mathf 호출 대신 직접 계산합니다. (좌표는 맵 안이라 항상 0 이상)
        int cx = (int)ax, cy = (int)ay;
        int ex = (int)bx, ey = (int)by;

        float dx = bx - ax, dy = by - ay;
        int stepX = dx > 0f ? 1 : -1;
        int stepY = dy > 0f ? 1 : -1;
        float deltaX = dx > 0f ? 1f / dx : dx < 0f ? -1f / dx : float.PositiveInfinity;
        float deltaY = dy > 0f ? 1f / dy : dy < 0f ? -1f / dy : float.PositiveInfinity;
        float nextX = dx != 0f ? (dx > 0f ? cx + 1 - ax : ax - cx) * deltaX : float.PositiveInfinity;
        float nextY = dy != 0f ? (dy > 0f ? cy + 1 - ay : ay - cy) * deltaY : float.PositiveInfinity;

        float enterT = 0f;   // 마지막 칸에 들어선 지점 (선분 비율 0~1)
        int guard = 0;

        while ((cx != ex || cy != ey) && guard++ < 512)
        {
            if (nextX < nextY) { enterT = nextX; nextX += deltaX; cx += stepX; }
            else               { enterT = nextY; nextY += deltaY; cy += stepY; }

            if (cx == ex && cy == ey) break;
            if (IsBlocking(cx, cy)) return false;
        }

        if (!endBlocked) return true;

        float insideDistance = (1f - enterT) * Mathf.Sqrt(dx * dx + dy * dy);
        return insideDistance <= wallFaceThickness + 0.001f;
    }

    private bool IsBlocking(int x, int z)
    {
        return x < 0 || z < 0 || x >= cellsX || z >= cellsZ || blocked[z * cellsX + x];
    }

    private void Accumulate(byte[] mask, byte[] blockerLight, RectInt rect)
    {
        for (int y = rect.yMin; y < rect.yMax; y++)
        {
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                int i = y * Width + x;
                if (mask[i] == 0) continue;

                if (kinds[i] == PixelKind.WallFace) seenWall[i] = true;
                else MarkExplored(i);
            }
        }

        // 표면이 보인 오브젝트 칸은 밑의 바닥도 본 것으로 칩니다. (개척도에 들어감)
        foreach (Vector2Int cell in lastBlockerInput)
        {
            if (!grid.InBounds(cell) || blockerLight[cell.y * cellsX + cell.x] == 0) continue;

            for (int y = cell.y * pixelsPerTile; y < (cell.y + 1) * pixelsPerTile; y++)
            {
                for (int x = cell.x * pixelsPerTile; x < (cell.x + 1) * pixelsPerTile; x++)
                {
                    int i = y * Width + x;
                    if (kinds[i] == PixelKind.Floor) MarkExplored(i);
                }
            }
        }
    }

    private void MarkExplored(int index)
    {
        if (exploredFloor[index]) return;

        exploredFloor[index] = true;
        exploredFloorCount++;
    }

    private RectInt IncludeLitBlockerCells(RectInt rect, byte[] blockerLight)
    {
        foreach (Vector2Int cell in lastBlockerInput)
        {
            if (!grid.InBounds(cell) || blockerLight[cell.y * cellsX + cell.x] == 0) continue;

            RectInt cellRect = new RectInt(cell.x * pixelsPerTile, cell.y * pixelsPerTile, pixelsPerTile, pixelsPerTile);
            rect = Union(rect, cellRect);
        }

        return rect;
    }

    private void ClearRect(byte[] mask, RectInt rect)
    {
        for (int y = rect.yMin; y < rect.yMax; y++)
            Array.Clear(mask, y * Width + rect.xMin, rect.width);
    }

    private static RectInt Union(RectInt a, RectInt b)
    {
        if (a.width <= 0 || a.height <= 0) return b;
        if (b.width <= 0 || b.height <= 0) return a;

        int xMin = Mathf.Min(a.xMin, b.xMin), yMin = Mathf.Min(a.yMin, b.yMin);
        int xMax = Mathf.Max(a.xMax, b.xMax), yMax = Mathf.Max(a.yMax, b.yMax);
        return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    // ── 픽셀 분류 (층을 만들 때 한 번) ──────────────────────────

    private int ClassifyPixels()
    {
        int floorPixels = 0;
        int ppt = pixelsPerTile;

        for (int cz = 0; cz < grid.Size.y; cz++)
        {
            for (int cx = 0; cx < grid.Size.x; cx++)
            {
                CellType type = grid.Cells[cx, cz];

                if (LocationGrid.IsWalkableTerrain(type))
                {
                    FillCell(cx, cz, PixelKind.Floor);
                    floorPixels += ppt * ppt;
                }
                else if (type == CellType.Wall)
                {
                    ClassifyWallCell(cx, cz);
                }
            }
        }

        return floorPixels;
    }

    private void FillCell(int cx, int cz, PixelKind kind)
    {
        for (int y = 0; y < pixelsPerTile; y++)
        {
            int row = (cz * pixelsPerTile + y) * Width + cx * pixelsPerTile;
            for (int x = 0; x < pixelsPerTile; x++)
                kinds[row + x] = kind;
        }
    }

    /// <summary>벽 칸의 픽셀 중 바닥과 맞닿은 면에서 wallFaceThickness 이내인 것을 벽면으로 표시합니다.</summary>
    private void ClassifyWallCell(int cx, int cz)
    {
        bool left = IsOpen(cx - 1, cz), right = IsOpen(cx + 1, cz);
        bool down = IsOpen(cx, cz - 1), up = IsOpen(cx, cz + 1);
        bool downLeft = IsOpen(cx - 1, cz - 1), downRight = IsOpen(cx + 1, cz - 1);
        bool upLeft = IsOpen(cx - 1, cz + 1), upRight = IsOpen(cx + 1, cz + 1);

        if (!(left || right || down || up || downLeft || downRight || upLeft || upRight)) return;

        float inv = 1f / pixelsPerTile;

        for (int y = 0; y < pixelsPerTile; y++)
        {
            float fromDown = (y + 0.5f) * inv, fromUp = 1f - fromDown;

            for (int x = 0; x < pixelsPerTile; x++)
            {
                float fromLeft = (x + 0.5f) * inv, fromRight = 1f - fromLeft;
                float nearest = float.MaxValue;

                if (left) nearest = Mathf.Min(nearest, fromLeft);
                if (right) nearest = Mathf.Min(nearest, fromRight);
                if (down) nearest = Mathf.Min(nearest, fromDown);
                if (up) nearest = Mathf.Min(nearest, fromUp);
                if (downLeft) nearest = Mathf.Min(nearest, Mathf.Sqrt(fromLeft * fromLeft + fromDown * fromDown));
                if (downRight) nearest = Mathf.Min(nearest, Mathf.Sqrt(fromRight * fromRight + fromDown * fromDown));
                if (upLeft) nearest = Mathf.Min(nearest, Mathf.Sqrt(fromLeft * fromLeft + fromUp * fromUp));
                if (upRight) nearest = Mathf.Min(nearest, Mathf.Sqrt(fromRight * fromRight + fromUp * fromUp));

                if (nearest <= wallFaceThickness)
                    kinds[(cz * pixelsPerTile + y) * Width + cx * pixelsPerTile + x] = PixelKind.WallFace;
            }
        }
    }

    private bool IsOpen(int x, int z) => grid.InBounds(x, z) && LocationGrid.IsWalkableTerrain(grid.Cells[x, z]);

    // ── 저장·복원 ───────────────────────────────────────────

    public MinimapExplorationRecord Capture()
    {
        return new MinimapExplorationRecord
        {
            width = Width,
            height = Height,
            exploredFloor = (bool[])exploredFloor.Clone(),
            seenWall = (bool[])seenWall.Clone()
        };
    }

    /// <summary>저장해 둔 기록을 덮어씁니다. 크기가 다르면(맵이나 pixelsPerTile이 바뀐 경우) 무시하고 false.</summary>
    public bool Restore(MinimapExplorationRecord record)
    {
        if (record == null || record.width != Width || record.height != Height) return false;
        if (record.exploredFloor == null || record.exploredFloor.Length != exploredFloor.Length) return false;
        if (record.seenWall == null || record.seenWall.Length != seenWall.Length) return false;

        Array.Copy(record.exploredFloor, exploredFloor, exploredFloor.Length);
        Array.Copy(record.seenWall, seenWall, seenWall.Length);

        exploredFloorCount = 0;
        foreach (bool explored in exploredFloor)
            if (explored) exploredFloorCount++;

        // 복원 전에 이미 밝혀 둔 지금 시야가 기록에서 빠지지 않도록 다시 합칩니다.
        Accumulate(playerLit, playerBlockerLight, new RectInt(0, 0, Width, Height));
        Accumulate(torchLit, torchBlockerLight, new RectInt(0, 0, Width, Height));
        return true;
    }
}

/// <summary>층 하나의 미니맵 탐색 기록. 픽셀 배열은 가로줄 순서(y * width + x)입니다.</summary>
public class MinimapExplorationRecord
{
    public int width;
    public int height;
    public bool[] exploredFloor;
    public bool[] seenWall;
}
