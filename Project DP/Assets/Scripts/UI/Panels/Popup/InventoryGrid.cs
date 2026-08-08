using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 그리드 인벤토리의 '데이터/규칙'만 담당하는 순수 로직 클래스 (UI 없음).
/// - cells[,] : 각 칸을 어떤 아이템이 점유했는지 (빠른 조회/판정용)
/// - items    : 배치된 아이템 목록 (순회용)
/// 바뀔 때마다 OnChanged를 쏴서, UI(View)가 알아서 다시 그리게 함.
/// </summary>
public class InventoryGrid
{
    public int Width { get; }
    public int Height { get; }

    private readonly InventoryItem[,] cells;                  // 셀 → 점유 아이템 (없으면 null)
    private readonly List<InventoryItem> items = new List<InventoryItem>();

    public IReadOnlyList<InventoryItem> Items => items;
    public event Action OnChanged;

    public InventoryGrid(int width, int height)
    {
        Width = width;
        Height = height;
        cells = new InventoryItem[width, height];
    }

    // ── 기본 조회 ──
    public bool InBounds(Vector2Int c) => c.x >= 0 && c.x < Width && c.y >= 0 && c.y < Height;
    public InventoryItem GetAt(Vector2Int c) => InBounds(c) ? cells[c.x, c.y] : null;

    // ── 배치 가능 판정 ──
    public bool CanPlace(ItemData item, Vector2Int anchor, InventoryItem ignore = null)
    {
        foreach (Vector2Int offset in item.OccupiedCells())
        {
            Vector2Int c = anchor + offset;
            if (!InBounds(c)) return false;                  // 그리드 밖이면 불가
            InventoryItem occupant = cells[c.x, c.y];
            if (occupant != null && occupant != ignore)      // 이미 '다른' 아이템이 있으면 불가
                return false;
        }
        return true;
    }

    // ── 배치 / 이동 ──
    public bool Place(InventoryItem it, Vector2Int anchor)
    {
        if (!CanPlace(it.Data, anchor, it)) return false;    // 자기 자신은 무시하고 판정

        if (items.Contains(it)) ClearCells(it);              // 이미 놓여있던 거면 기존 점유 지움(=이동)

        it.Anchor = anchor;
        foreach (Vector2Int c in it.WorldCells())
            cells[c.x, c.y] = it;                            // 점유맵 채우기

        if (!items.Contains(it)) items.Add(it);
        OnChanged?.Invoke();
        return true;
    }

    // ── 제거 ──
    public void Remove(InventoryItem it)
    {
        if (!items.Contains(it)) return;
        ClearCells(it);
        items.Remove(it);
        OnChanged?.Invoke();
    }

    private void ClearCells(InventoryItem it)
    {
        foreach (Vector2Int c in it.WorldCells())
            if (InBounds(c) && cells[c.x, c.y] == it)
                cells[c.x, c.y] = null;
    }

    // ── 자동 추가 (월드에서 주웠을 때 등): 중첩 병합 → 남으면 빈자리에 새 스택 ──
    public bool TryAutoAdd(ItemData item, int count)
    {
        // 1) 중첩 가능하면, 기존 같은 아이템 스택부터 채운다
        if (item.isStackable)
        {
            foreach (InventoryItem existing in items)
            {
                if (existing.Data != item) continue;
                int space = item.maxStackSize - existing.Count;
                if (space <= 0) continue;

                int move = Mathf.Min(space, count);
                existing.Count += move;
                count -= move;
                if (count <= 0) { OnChanged?.Invoke(); return true; }
            }
        }

        // 2) 남은 개수를 새 스택으로 빈자리에 배치
        while (count > 0)
        {
            int stackCount = item.isStackable ? Mathf.Min(item.maxStackSize, count) : 1;
            if (!TryFindSpot(item, out Vector2Int anchor))
            {
                OnChanged?.Invoke();
                return false;                                // 자리 부족 (일부만 들어갔을 수 있음)
            }
            Place(new InventoryItem(item, stackCount), anchor);
            count -= stackCount;
        }
        return true;
    }

    public void Consume(InventoryItem it, int amount = 1)
    {
        if (it == null || !items.Contains(it)) return;
        it.Count -= amount;
        if (it.Count <= 0) Remove(it);   // Remove가 점유 해제 + OnChanged
        else OnChanged?.Invoke();         // 개수만 줄었으니 갱신 알림
    }

    // ── 왼쪽 위부터 훑어 놓을 수 있는 첫 자리 찾기 ──
    private bool TryFindSpot(ItemData item, out Vector2Int anchor)
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                Vector2Int a = new Vector2Int(x, y);
                if (CanPlace(item, a)) { anchor = a; return true; }
            }
        anchor = default;
        return false;
    }
}