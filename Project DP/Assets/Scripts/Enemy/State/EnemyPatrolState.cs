using System.Collections.Generic;
using UnityEngine;

public class EnemyPatrolState : EnemyBaseState
{
    private readonly int locomotionAnimHash = Animator.StringToHash("Chase");
    private const float PatrolAnimSpeed = 0.55f;
    private const int PatrolTileRadius = 4;
    private const float RouteRetryInterval = 1f;

    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    private readonly Queue<Vector2Int> bfsQueue = new Queue<Vector2Int>();
    private readonly Dictionary<Vector2Int, int> bfsDistance = new Dictionary<Vector2Int, int>();
    private readonly List<Vector2Int> farthestCells = new List<Vector2Int>();

    private Vector2Int farCell;
    private bool movingToFarCell;
    private bool hasRoute;
    private float routeRetryTimer;

    public EnemyPatrolState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.ResetAnimatorLocomotionSpeed();
        enemy.animator.CrossFadeInFixedTime(locomotionAnimHash, 0.1f);
        enemy.SetAnimatorLocomotionSpeed(PatrolAnimSpeed);

        routeRetryTimer = 0f;
        hasRoute = SetupPatrolRoute();
        if (!hasRoute) return;

        movingToFarCell = true;
        SetPatrolDestination();
    }

    public override void Update()
    {
        if (enemy.TryDetectPlayer())
        {
            stateMachine.ChangeState<EnemyChaseState>();
            return;
        }

        if (enemy.TryDetectRunningPlayerNearby(out _))
        {
            stateMachine.ChangeState<EnemyAlertState>();
            return;
        }

        if (!hasRoute)
        {
            enemy.MoveEnemy(Vector3.zero);

            routeRetryTimer += Time.deltaTime;
            if (routeRetryTimer < RouteRetryInterval) return;

            routeRetryTimer = 0f;
            hasRoute = SetupPatrolRoute();
            if (!hasRoute) return;

            movingToFarCell = true;
            SetPatrolDestination();
            return;
        }

        if (enemy.Mover.HasArrived)
        {
            if (enemy.Mover.LastResult == PathResult.None)
            {
                routeRetryTimer += Time.deltaTime;
                if (routeRetryTimer < RouteRetryInterval) return;
                routeRetryTimer = 0f;
            }

            movingToFarCell = !movingToFarCell;
            SetPatrolDestination();
        }
    }

    public override void Exit()
    {
        enemy.ResetAnimatorLocomotionSpeed();
        enemy.Mover.Stop();
    }

    private bool SetupPatrolRoute()
    {
        LocationGrid grid = enemy.Grid;
        if (grid == null) return false;

        Vector2Int start = enemy.spawnCell;
        bfsQueue.Clear();
        bfsDistance.Clear();
        farthestCells.Clear();

        bfsQueue.Enqueue(start);
        bfsDistance[start] = 0;
        int bestDistance = 0;

        while (bfsQueue.Count > 0)
        {
            Vector2Int cell = bfsQueue.Dequeue();
            int distance = bfsDistance[cell];

            if (distance > bestDistance)
            {
                bestDistance = distance;
                farthestCells.Clear();
            }
            if (distance == bestDistance && distance > 0)
                farthestCells.Add(cell);

            if (distance >= PatrolTileRadius) continue;

            foreach (Vector2Int dir in Directions)
            {
                Vector2Int next = cell + dir;
                if (bfsDistance.ContainsKey(next)) continue;
                if (!grid.IsPassable(next, false)) continue;

                bfsDistance[next] = distance + 1;
                bfsQueue.Enqueue(next);
            }
        }

        if (farthestCells.Count == 0) return false;

        farCell = farthestCells[Random.Range(0, farthestCells.Count)];
        return true;
    }

    private void SetPatrolDestination()
    {
        Vector2Int goal = movingToFarCell ? farCell : enemy.spawnCell;
        enemy.Mover.SetDestination(goal, false, enemy.moveSpeed * 0.5f);
    }
}
