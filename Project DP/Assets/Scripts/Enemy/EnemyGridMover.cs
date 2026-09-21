using System.Collections.Generic;
using UnityEngine;

public class EnemyGridMover
{
    private const float WaypointReachDistance = 0.15f;
    private const float FollowRepathInterval = 0.5f;
    private const float StuckRepathTime = 0.5f;
    private const float ClimbGroundProbeHeight = 6f;
    private const int SmoothingLookahead = 6;

    private readonly EnemyController enemy;
    private readonly List<Vector2Int> path = new List<Vector2Int>();

    private bool hasDestination;
    private Vector2Int goalCell;
    private bool allowPits;
    private float speed;
    private Transform followTarget;
    private float stopDistance;
    private Vector2Int lastFollowCell;
    private int pathIndex;
    private float lastPathTime;
    private float stuckTimer;

    private bool climbing;
    private float climbTimer;
    private Vector3 climbStart;
    private Vector3 climbEnd;

    public bool FaceMovementDirection = true;

    public bool HasDestination => hasDestination;
    public bool IsClimbing => climbing;
    public bool IsMoving { get; private set; }
    public PathResult LastResult { get; private set; }
    public IReadOnlyList<Vector2Int> CurrentPath => path;
    public int PathIndex => pathIndex;

    public bool HasArrived =>
        hasDestination && !climbing && pathIndex >= path.Count
        && (followTarget == null || IsWithinStopDistance());

    public EnemyGridMover(EnemyController enemy)
    {
        this.enemy = enemy;
    }

    public void SetDestination(Vector2Int goal, bool allowPits, float speed, Transform follow = null, float stopDistance = 0f)
    {
        goalCell = goal;
        this.allowPits = allowPits;
        this.speed = speed;
        followTarget = follow;
        this.stopDistance = stopDistance;
        lastFollowCell = follow != null ? LocationGrid.WorldToCell(follow.position) : goal;
        hasDestination = true;
        Repath();
    }

    public void Stop()
    {
        hasDestination = false;
        followTarget = null;
        path.Clear();
        pathIndex = 0;
        stuckTimer = 0f;
        IsMoving = false;

        if (climbing)
        {
            enemy.transform.position = climbEnd;
            FinishClimb();
        }
    }

    public void Tick()
    {
        IsMoving = false;
        if (!hasDestination) return;

        if (climbing)
        {
            TickClimb();
            IsMoving = true;
            return;
        }

        if (followTarget != null && IsWithinStopDistance())
        {
            enemy.MoveEnemy(Vector3.zero);
            stuckTimer = 0f;
            return;
        }

        if (ShouldRepath()) Repath();
        AdvancePassedWaypoints();

        if (TryStartClimb())
        {
            IsMoving = true;
            return;
        }

        if (!TryGetMoveTarget(out Vector3 moveTarget, out int targetIndex))
        {
            enemy.MoveEnemy(Vector3.zero);
            return;
        }

        Vector3 delta = moveTarget - enemy.transform.position;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance <= WaypointReachDistance)
        {
            if (targetIndex >= 0) pathIndex = targetIndex + 1;
            enemy.MoveEnemy(Vector3.zero);
            return;
        }

        Vector3 direction = delta / distance;
        float step = Mathf.Min(speed * Time.deltaTime, distance);
        if (FaceMovementDirection) enemy.RotateToward(direction);

        Vector3 before = enemy.transform.position;
        enemy.MoveEnemy(direction * step);
        IsMoving = true;
        TrackStuck(before, step);
    }

    private bool IsWithinStopDistance()
    {
        if (followTarget == null) return false;
        return enemy.GetHorizontalDistanceTo(followTarget.position) <= stopDistance;
    }

    private bool ShouldRepath()
    {
        if (followTarget != null)
        {
            Vector2Int followCell = LocationGrid.WorldToCell(followTarget.position);
            if (followCell != lastFollowCell)
            {
                lastFollowCell = followCell;
                goalCell = followCell;
                return true;
            }

            if (Time.time - lastPathTime >= FollowRepathInterval) return true;
        }

        if (pathIndex < path.Count && enemy.Grid != null && !enemy.Grid.IsPassable(path[pathIndex], allowPits))
            return true;

        return stuckTimer >= StuckRepathTime;
    }

    private void Repath()
    {
        path.Clear();
        pathIndex = 0;
        stuckTimer = 0f;
        lastPathTime = Time.time;

        if (enemy.Grid == null)
        {
            LastResult = PathResult.None;
            return;
        }

        PathQuery query = new PathQuery
        {
            start = enemy.CurrentCell,
            goal = goalCell,
            allowPits = allowPits,
            pitCost = enemy.trapPathCost
        };

        LastResult = AStarPathfinder.FindPath(enemy.Grid, query, path);

        if (path.Count == 0 && followTarget == null && LastResult == PathResult.Complete)
            path.Add(goalCell);
    }

    private void AdvancePassedWaypoints()
    {
        Vector2Int currentCell = enemy.CurrentCell;
        float reachSq = WaypointReachDistance * WaypointReachDistance;

        while (pathIndex < path.Count)
        {
            Vector2Int cell = path[pathIndex];
            bool isLast = pathIndex == path.Count - 1;

            if (!isLast && cell == currentCell)
            {
                pathIndex++;
                continue;
            }

            Vector3 delta = LocationGrid.CellToWorld(cell) - enemy.transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= reachSq)
            {
                pathIndex++;
                continue;
            }

            break;
        }
    }

    private bool TryGetMoveTarget(out Vector3 target, out int targetIndex)
    {
        targetIndex = -1;
        Vector3 position = enemy.transform.position;
        LocationGrid grid = enemy.Grid;
        float radius = enemy.characterController != null ? enemy.characterController.radius : 0.5f;

        if (pathIndex >= path.Count)
        {
            if (followTarget != null && grid != null
                && grid.IsSegmentClear(position, followTarget.position, allowPits, radius))
            {
                target = followTarget.position;
                return true;
            }

            target = position;
            return false;
        }

        int best = pathIndex;
        if (grid != null)
        {
            int maxIndex = Mathf.Min(path.Count - 1, pathIndex + SmoothingLookahead);
            for (int i = maxIndex; i > pathIndex; i--)
            {
                if (!grid.IsSegmentClear(position, LocationGrid.CellToWorld(path[i]), allowPits, radius)) continue;

                best = i;
                break;
            }

            if (followTarget != null && best == path.Count - 1
                && grid.IsSegmentClear(position, followTarget.position, allowPits, radius))
            {
                target = followTarget.position;
                return true;
            }
        }

        target = LocationGrid.CellToWorld(path[best]);
        targetIndex = best;
        return true;
    }

    private void TrackStuck(Vector3 before, float expectedStep)
    {
        Vector3 moved = enemy.transform.position - before;
        moved.y = 0f;

        float minProgress = expectedStep * 0.25f;
        if (moved.sqrMagnitude < minProgress * minProgress)
            stuckTimer += Time.deltaTime;
        else
            stuckTimer = 0f;
    }

    private static bool IsAdjacent(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
    }

    private bool TryStartClimb()
    {
        LocationGrid grid = enemy.Grid;
        if (grid == null) return false;

        Vector2Int currentCell = enemy.CurrentCell;
        if (grid.Get(currentCell) != CellType.Pit) return false;

        TrapTile trap = grid.Traps[currentCell.x, currentCell.y];
        if (trap == null || trap.IsFilled) return false;
        if (!trap.IsActorInsidePit(enemy.transform)) return false;

        if (pathIndex >= path.Count || !IsAdjacent(currentCell, path[pathIndex]))
        {
            Repath();
            if (path.Count == 0 || !IsAdjacent(currentCell, path[0])) return false;
        }

        Vector2Int nextCell = path[pathIndex];
        Vector3 end = LocationGrid.CellToWorld(nextCell);
        if (!TryGetGroundHeight(end, out float groundY))
            groundY = trap.SurfaceWorldY;
        end.y = groundY;

        climbStart = enemy.transform.position;
        climbEnd = end;
        climbTimer = 0f;
        climbing = true;

        if (enemy.characterController != null)
            enemy.characterController.enabled = false;

        if (FaceMovementDirection)
            enemy.FaceToward(end);

        return true;
    }

    private void TickClimb()
    {
        climbTimer += Time.deltaTime;
        float duration = enemy.pitClimbDuration;
        float t = duration > 0f ? Mathf.Clamp01(climbTimer / duration) : 1f;

        Vector3 pos = Vector3.Lerp(climbStart, climbEnd, t);
        pos.y = Mathf.Lerp(climbStart.y, climbEnd.y, Mathf.Sin(t * Mathf.PI * 0.5f));
        enemy.transform.position = pos;

        if (t >= 1f)
            FinishClimb();
    }

    private void FinishClimb()
    {
        climbing = false;
        climbTimer = 0f;
        enemy.verticalVelocity = -2f;

        if (enemy.characterController != null && !(enemy.stateMachine.CurrentState is EnemyDeadState))
            enemy.characterController.enabled = true;
    }

    private bool TryGetGroundHeight(Vector3 worldXZ, out float groundY)
    {
        groundY = 0f;

        Vector3 origin = new Vector3(worldXZ.x, enemy.transform.position.y + ClimbGroundProbeHeight, worldXZ.z);
        int mask = ~((1 << enemy.gameObject.layer) | enemy.playerLayer.value);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, ClimbGroundProbeHeight * 2f, mask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;

        bool found = false;
        float bestDistance = float.MaxValue;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null) continue;
            if (hit.collider.transform.IsChildOf(enemy.transform)) continue;
            if (hit.distance >= bestDistance) continue;

            bestDistance = hit.distance;
            groundY = hit.point.y;
            found = true;
        }

        return found;
    }
}
