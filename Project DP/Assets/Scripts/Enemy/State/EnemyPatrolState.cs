using System.Collections.Generic;
using UnityEngine;

public class EnemyPatrolState : EnemyBaseState
{
    private readonly int locomotionAnimHash = Animator.StringToHash("Chase");
    private const float PatrolAnimSpeed = 0.55f;
    private const int PatrolTileRadius = 4;
    private const float RouteRetryInterval = 1f;

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

        // 플레이어가 없을 때만 횃불을 노린다. 횃불도 같은 Chase 상태로 쫓아가 끈다.
        if (enemy.TryDetectTorch())
        {
            stateMachine.ChangeState<EnemyChaseState>();
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

        GridSearch.CollectFarthest(grid, enemy.spawnCell, PatrolTileRadius, false, farthestCells);
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
