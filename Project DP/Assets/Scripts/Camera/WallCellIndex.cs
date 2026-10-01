using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 층의 벽을 칸별로 묶어 둔 목록. 격자를 만들 때와 같은 기준(Wall 태그 타일 루트, 위치로 칸 계산)을 씁니다.
/// 벽은 플레이 중에 생기거나 없어지지 않으므로 층마다 한 번만 만듭니다.
/// </summary>
internal class WallCellIndex
{
    private static readonly List<Transform> Empty = new List<Transform>();
    private readonly Dictionary<Vector2Int, List<Transform>> walls = new Dictionary<Vector2Int, List<Transform>>();

    public GameObject Location { get; }

    private WallCellIndex(GameObject location)
    {
        Location = location;
    }

    public static WallCellIndex Build(GameObject location)
    {
        var index = new WallCellIndex(location);
        Transform root = location.transform;

        foreach (Transform t in location.GetComponentsInChildren<Transform>(true))
        {
            if (!t.CompareTag("Wall") || !LocationGrid.IsTileRoot(t, root)) continue;

            Vector2Int cell = LocationGrid.WorldToCell(t.position);
            if (!index.walls.TryGetValue(cell, out List<Transform> list))
            {
                list = new List<Transform>();
                index.walls[cell] = list;
            }

            list.Add(t);
        }

        return index;
    }

    /// <summary>이 칸에 있는 벽들. 없으면 빈 목록.</summary>
    public IReadOnlyList<Transform> Get(Vector2Int cell) => walls.TryGetValue(cell, out List<Transform> list) ? list : Empty;
}
