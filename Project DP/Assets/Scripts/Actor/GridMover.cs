using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 그리드 위를 A*로 이동시키는 공용 이동기. 적·NPC가 함께 씁니다.
/// 소유자를 IGridAgent로만 알기 때문에 EnemyController에 의존하지 않습니다.
///
/// 담당 범위는 목적지까지 가는 것까지입니다. 언제 어디로 갈지(정책)는 상태 머신이나
/// 브레인이 정하고, 여기서는 경로 탐색·코너 평활화·끼임 복구·구덩이 등반만 처리합니다.
/// </summary>
public class GridMover
{
    private const float WaypointReachDistance = 0.15f;
    private const float FollowRepathInterval = 0.5f;
    private const float StuckRepathTime = 0.5f;
    private const float ClimbGroundProbeHeight = 6f;
    private const int SmoothingLookahead = 6;

    private readonly IGridAgent agent;
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

    public GridMover(IGridAgent agent)
    {
        this.agent = agent;
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
            agent.Transform.position = climbEnd;
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
            agent.Move(Vector3.zero);
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
            agent.Move(Vector3.zero);
            return;
        }

        Vector3 delta = moveTarget - agent.Transform.position;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance <= WaypointReachDistance)
        {
            if (targetIndex >= 0) pathIndex = targetIndex + 1;
            agent.Move(Vector3.zero);
            return;
        }

        Vector3 direction = delta / distance;
        float step = Mathf.Min(speed * Time.deltaTime, distance);
        if (FaceMovementDirection) agent.RotateToward(direction);

        Vector3 before = agent.Transform.position;
        agent.Move(direction * step);
        IsMoving = true;
        TrackStuck(before, step);
    }

    private bool IsWithinStopDistance()
    {
        if (followTarget == null) return false;
        return ActorMath.HorizontalDistance(agent.Transform.position, followTarget.position) <= stopDistance;
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

        if (pathIndex < path.Count && agent.Grid != null && !agent.Grid.IsPassable(path[pathIndex], allowPits))
            return true;

        return stuckTimer >= StuckRepathTime;
    }

    private void Repath()
    {
        path.Clear();
        pathIndex = 0;
        stuckTimer = 0f;
        lastPathTime = Time.time;

        if (agent.Grid == null)
        {
            LastResult = PathResult.None;
            return;
        }

        PathQuery query = new PathQuery
        {
            start = agent.CurrentCell,
            goal = goalCell,
            allowPits = allowPits,
            pitCost = agent.PathTrapCost
        };

        LastResult = AStarPathfinder.FindPath(agent.Grid, query, path);

        if (path.Count == 0 && followTarget == null && LastResult == PathResult.Complete)
            path.Add(goalCell);
    }

    private void AdvancePassedWaypoints()
    {
        Vector2Int currentCell = agent.CurrentCell;
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

            Vector3 delta = LocationGrid.CellToWorld(cell) - agent.Transform.position;
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
        Vector3 position = agent.Transform.position;
        LocationGrid grid = agent.Grid;
        float radius = agent.Controller != null ? agent.Controller.radius : 0.5f;

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
        Vector3 moved = agent.Transform.position - before;
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
        LocationGrid grid = agent.Grid;
        if (grid == null) return false;

        Vector2Int currentCell = agent.CurrentCell;
        if (grid.Get(currentCell) != CellType.Pit) return false;

        TrapTile trap = grid.Traps[currentCell.x, currentCell.y];
        if (trap == null || trap.IsFilled) return false;
        if (!trap.IsActorInsidePit(agent.Transform)) return false;

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

        climbStart = agent.Transform.position;
        climbEnd = end;
        climbTimer = 0f;
        climbing = true;

        if (agent.Controller != null)
            agent.Controller.enabled = false;

        if (FaceMovementDirection)
            ActorMath.FaceToward(agent.Transform, end);

        return true;
    }

    private void TickClimb()
    {
        climbTimer += Time.deltaTime;
        float duration = agent.PitClimbDuration;
        float t = duration > 0f ? Mathf.Clamp01(climbTimer / duration) : 1f;

        Vector3 pos = Vector3.Lerp(climbStart, climbEnd, t);
        pos.y = Mathf.Lerp(climbStart.y, climbEnd.y, Mathf.Sin(t * Mathf.PI * 0.5f));
        agent.Transform.position = pos;

        if (t >= 1f)
            FinishClimb();
    }

    private void FinishClimb()
    {
        climbing = false;
        climbTimer = 0f;
        agent.VerticalVelocity = -2f;

        // 구덩이 안에서 죽었다면 컨트롤러를 되살리지 않습니다. (기존의 EnemyDeadState 검사와 같은 의미)
        if (agent.Controller != null && agent.IsAlive)
            agent.Controller.enabled = true;
    }

    private bool TryGetGroundHeight(Vector3 worldXZ, out float groundY)
    {
        groundY = 0f;

        Vector3 origin = new Vector3(worldXZ.x, agent.Transform.position.y + ClimbGroundProbeHeight, worldXZ.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, ClimbGroundProbeHeight * 2f,
            agent.GroundProbeMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;

        bool found = false;
        float bestDistance = float.MaxValue;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null) continue;
            if (hit.collider.transform.IsChildOf(agent.Transform)) continue;
            if (hit.distance >= bestDistance) continue;

            bestDistance = hit.distance;
            groundY = hit.point.y;
            found = true;
        }

        return found;
    }
}
