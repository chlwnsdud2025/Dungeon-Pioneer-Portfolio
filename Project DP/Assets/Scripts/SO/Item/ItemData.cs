using System.Collections.Generic;
using UnityEngine;

public enum ItemCategory { Equipment, Consumable }
public enum EquipmentSlot { Helmet, Armor, Weapon, Shield }

public abstract class ItemData : ScriptableObject
{
    [Header("기본 정보")]
    public string itemId;
    public string displayName;
    [TextArea(2, 4)] public string description;
    public Sprite icon;

    [Header("인벤토리 그리드")]
    public Vector2Int gridSize = Vector2Int.one;
    [HideInInspector] public bool[] shapeMask;             

    [Header("아이콘 배치 (그리드 기준)")]
    [Tooltip("바운딩 박스 대비 이미지 배율")] public float iconScale = 1f;
    [Tooltip("칸 단위 이미지 이동 오프셋")] public Vector2 iconOffset = Vector2.zero;

    [Header("중첩")]
    public bool isStackable;
    [Min(1)] public int maxStackSize = 1;

    public abstract ItemCategory Category { get; }

    public int GridWidth => Mathf.Max(1, gridSize.x);
    public int GridHeight => Mathf.Max(1, gridSize.y);

    /// <summary>(x,y) 칸이 점유되는지. 좌상단 (0,0), y는 아래로 증가.
    /// 마스크가 없으면 전체 사각형으로 간주(사각형 아이템 하위호환).</summary>
    public bool IsCellOccupied(int x, int y)
    {
        if (x < 0 || x >= GridWidth || y < 0 || y >= GridHeight) return false;
        if (shapeMask == null || shapeMask.Length != GridWidth * GridHeight) return true;
        return shapeMask[y * GridWidth + x];
    }

    /// <summary>실제로 차지하는 칸 수 (인벤토리 무게/용량 계산 등에 유용).</summary>
    public int OccupiedCellCount
    {
        get
        {
            if (shapeMask == null || shapeMask.Length != GridWidth * GridHeight)
                return GridWidth * GridHeight;
            int n = 0; foreach (bool b in shapeMask) if (b) n++; return n;
        }
    }

    /// <summary>점유하는 칸들의 (x,y) 오프셋. 인벤토리 배치 판정에 사용.</summary>
    public IEnumerable<Vector2Int> OccupiedCells()
    {
        for (int y = 0; y < GridHeight; y++)
            for (int x = 0; x < GridWidth; x++)
                if (IsCellOccupied(x, y))
                    yield return new Vector2Int(x, y);
    }
}