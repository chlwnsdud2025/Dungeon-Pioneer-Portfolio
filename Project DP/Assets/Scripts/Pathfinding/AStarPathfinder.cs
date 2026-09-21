using System.Collections.Generic;
using UnityEngine;

public enum PathResult
{
    None,
    Partial,
    Complete
}

public struct PathQuery
{
    public Vector2Int start;
    public Vector2Int goal;
    public bool allowPits;
    public float pitCost;
}

public static class AStarPathfinder
{
    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    private static Vector2Int bufferSize;
    private static float[] gScore;
    private static float[] fScore;
    private static int[] parent;
    private static bool[] closed;
    private static int[] heap;
    private static int[] heapIndex;
    private static int heapCount;

    public static PathResult FindPath(LocationGrid grid, PathQuery query, List<Vector2Int> result)
    {
        result.Clear();
        if (grid == null) return PathResult.None;
        if (!grid.InBounds(query.start) || !grid.InBounds(query.goal)) return PathResult.None;
        if (query.start == query.goal) return PathResult.Complete;

        EnsureBuffers(grid.Size);

        int width = grid.Size.x;
        int count = width * grid.Size.y;
        for (int i = 0; i < count; i++)
        {
            gScore[i] = float.PositiveInfinity;
            fScore[i] = float.PositiveInfinity;
            parent[i] = -1;
            closed[i] = false;
            heapIndex[i] = -1;
        }
        heapCount = 0;

        int startIndex = ToIndex(query.start, width);
        int goalIndex = ToIndex(query.goal, width);
        float pitCost = Mathf.Max(0.01f, query.pitCost);

        gScore[startIndex] = 0f;
        fScore[startIndex] = Heuristic(query.start, query.goal);
        HeapPush(startIndex);

        int bestIndex = startIndex;
        float bestHeuristic = fScore[startIndex];
        bool reachedGoal = false;

        while (heapCount > 0)
        {
            int current = HeapPop();
            if (current == goalIndex)
            {
                reachedGoal = true;
                bestIndex = current;
                break;
            }

            closed[current] = true;
            Vector2Int currentCell = ToCell(current, width);

            float h = Heuristic(currentCell, query.goal);
            if (h < bestHeuristic)
            {
                bestHeuristic = h;
                bestIndex = current;
            }

            for (int d = 0; d < Directions.Length; d++)
            {
                Vector2Int next = currentCell + Directions[d];
                if (!grid.InBounds(next)) continue;

                int nextIndex = ToIndex(next, width);
                if (closed[nextIndex]) continue;
                if (!grid.IsPassable(next, query.allowPits)) continue;

                float stepCost = grid.IsPitOpen(next) ? pitCost : 1f;
                float tentative = gScore[current] + stepCost;
                if (tentative >= gScore[nextIndex]) continue;

                parent[nextIndex] = current;
                gScore[nextIndex] = tentative;
                fScore[nextIndex] = tentative + Heuristic(next, query.goal);

                if (heapIndex[nextIndex] < 0) HeapPush(nextIndex);
                else HeapSiftUp(heapIndex[nextIndex]);
            }
        }

        if (bestIndex == startIndex) return PathResult.None;

        int node = bestIndex;
        while (node != startIndex && node >= 0)
        {
            result.Add(ToCell(node, width));
            node = parent[node];
        }
        result.Reverse();

        return reachedGoal ? PathResult.Complete : PathResult.Partial;
    }

    private static float Heuristic(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static int ToIndex(Vector2Int cell, int width) => cell.y * width + cell.x;

    private static Vector2Int ToCell(int index, int width) => new Vector2Int(index % width, index / width);

    private static void EnsureBuffers(Vector2Int size)
    {
        int count = size.x * size.y;
        if (gScore != null && bufferSize == size && gScore.Length == count) return;

        bufferSize = size;
        gScore = new float[count];
        fScore = new float[count];
        parent = new int[count];
        closed = new bool[count];
        heap = new int[count];
        heapIndex = new int[count];
    }

    private static void HeapPush(int node)
    {
        heap[heapCount] = node;
        heapIndex[node] = heapCount;
        heapCount++;
        HeapSiftUp(heapCount - 1);
    }

    private static int HeapPop()
    {
        int top = heap[0];
        heapCount--;
        heapIndex[top] = -1;

        if (heapCount > 0)
        {
            heap[0] = heap[heapCount];
            heapIndex[heap[0]] = 0;
            HeapSiftDown(0);
        }

        return top;
    }

    private static void HeapSiftUp(int position)
    {
        int node = heap[position];
        while (position > 0)
        {
            int parentPosition = (position - 1) / 2;
            int parentNode = heap[parentPosition];
            if (fScore[parentNode] <= fScore[node]) break;

            heap[position] = parentNode;
            heapIndex[parentNode] = position;
            position = parentPosition;
        }

        heap[position] = node;
        heapIndex[node] = position;
    }

    private static void HeapSiftDown(int position)
    {
        int node = heap[position];
        while (true)
        {
            int left = position * 2 + 1;
            if (left >= heapCount) break;

            int right = left + 1;
            int smallest = left;
            if (right < heapCount && fScore[heap[right]] < fScore[heap[left]])
                smallest = right;

            if (fScore[heap[smallest]] >= fScore[node]) break;

            heap[position] = heap[smallest];
            heapIndex[heap[position]] = position;
            position = smallest;
        }

        heap[position] = node;
        heapIndex[node] = position;
    }
}
