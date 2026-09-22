using System.Collections.Generic;
using UnityEngine;

public class InventoryItem
{
    public ItemData Data { get; }         // 어떤 아이템인지 (정의)
    public int Count;                     // 중첩 개수 (물약 20개 등)
    public Vector2Int Anchor;             // 점유 영역의 '좌상단' 셀 위치

    public InventoryItem(ItemData data, int count)
    {
        Data = data;
        Count = count;
    }

    /// <summary>이 아이템이 실제로 차지하는 '월드 셀'들. (모양 마스크를 앵커만큼 이동)</summary>
    public IEnumerable<Vector2Int> WorldCells()
    {
        foreach (Vector2Int offset in Data.OccupiedCells())
            yield return Anchor + offset;
    }
}