using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 그리드 위 너비 우선 탐색(BFS). 적 순찰과 NPC 배회가 "여기서 N칸 안에 갈 수 있는 곳"을 고를 때 씁니다.
///
/// EnemyPatrolState에 있던 탐색을 그대로 옮긴 것입니다. 탐색 방향 순서와 방문 순서를 바꾸지 않아서
/// 같은 입력이면 순찰이 고르는 후보 칸이 이전과 한 칸도 다르지 않습니다.
///
/// 버퍼를 static으로 재사용합니다. Unity 메인 스레드에서만 부르므로 안전하고, 순찰·배회가 자주
/// 다시 탐색해도 할당이 생기지 않습니다. 결과는 반드시 호출자의 리스트로 복사해 돌려줍니다.
/// </summary>
public static class GridSearch
{
    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    private static readonly Queue<Vector2Int> queue = new Queue<Vector2Int>();
    private static readonly Dictionary<Vector2Int, int> distances = new Dictionary<Vector2Int, int>();
    private static readonly List<Vector2Int> visitOrder = new List<Vector2Int>();

    /// <summary>start에서 minSteps 이상 maxSteps 이하로 갈 수 있는 칸을 방문 순서대로 담습니다.</summary>
    public static void CollectReachable(LocationGrid grid, Vector2Int start, int maxSteps, bool allowPits,
                                        List<Vector2Int> result, int minSteps = 1)
    {
        result.Clear();
        if (!Search(grid, start, maxSteps, allowPits)) return;

        foreach (Vector2Int cell in visitOrder)
        {
            int distance = distances[cell];
            if (distance >= minSteps && distance <= maxSteps)
                result.Add(cell);
        }
    }

    /// <summary>maxSteps 안에서 가장 먼 칸들을 방문 순서대로 담습니다. start밖에 갈 곳이 없으면 비어 있습니다.</summary>
    public static void CollectFarthest(LocationGrid grid, Vector2Int start, int maxSteps, bool allowPits,
                                       List<Vector2Int> result)
    {
        result.Clear();
        if (!Search(grid, start, maxSteps, allowPits)) return;

        int bestDistance = 0;
        foreach (Vector2Int cell in visitOrder)
            bestDistance = Mathf.Max(bestDistance, distances[cell]);

        if (bestDistance == 0) return;

        foreach (Vector2Int cell in visitOrder)
        {
            if (distances[cell] == bestDistance)
                result.Add(cell);
        }
    }

    private static bool Search(LocationGrid grid, Vector2Int start, int maxSteps, bool allowPits)
    {
        queue.Clear();
        distances.Clear();
        visitOrder.Clear();
        if (grid == null) return false;

        queue.Enqueue(start);
        distances[start] = 0;

        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            visitOrder.Add(cell);

            int distance = distances[cell];
            if (distance >= maxSteps) continue;

            foreach (Vector2Int dir in Directions)
            {
                Vector2Int next = cell + dir;
                if (distances.ContainsKey(next)) continue;
                if (!grid.IsPassable(next, allowPits)) continue;

                distances[next] = distance + 1;
                queue.Enqueue(next);
            }
        }

        return true;
    }
}
