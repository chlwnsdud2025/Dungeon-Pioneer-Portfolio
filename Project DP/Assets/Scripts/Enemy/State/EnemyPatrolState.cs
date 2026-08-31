using UnityEngine;



public class EnemyPatrolState : EnemyBaseState

{

    private readonly int locomotionAnimHash = Animator.StringToHash("Chase");

    private const float PatrolAnimSpeed = 0.55f;



    private const int PatrolTileRadius = 4;



    private float tileSize;

    private Vector3 spawnNavPoint;

    private Vector3 farNavPoint;

    private bool movingToFarPoint;

    private bool agentReady;

    private float stuckTimer;

    private Vector3 lastTrackedPosition;



    public EnemyPatrolState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }



    public override void Enter()

    {

        enemy.ResetAnimatorLocomotionSpeed();

        enemy.animator.CrossFadeInFixedTime(locomotionAnimHash, 0.1f);

        enemy.SetAnimatorLocomotionSpeed(PatrolAnimSpeed);



        tileSize = GridSystem.Instance.TileSize;

        stuckTimer = 0f;

        lastTrackedPosition = enemy.transform.position;

        agentReady = enemy.TryActivateNavMeshAgent(enemy.moveSpeed * 0.5f, 0.1f);



        if (!agentReady)

            return;



        SetupPatrolRoute();

        SetPatrolDestination(movingToFarPoint ? farNavPoint : spawnNavPoint);

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



        if (!agentReady)

        {

            agentReady = enemy.TryActivateNavMeshAgent(enemy.moveSpeed * 0.5f, 0.1f);

            if (!agentReady) return;



            SetupPatrolRoute();

            SetPatrolDestination(movingToFarPoint ? farNavPoint : spawnNavPoint);

        }



        if (enemy.agent == null || !enemy.agent.isOnNavMesh) return;



        TrackStuckAndRepath();

        if (!enemy.agent.pathPending && enemy.agent.remainingDistance <= enemy.agent.stoppingDistance + 0.05f)
        {
            movingToFarPoint = !movingToFarPoint;
            SetPatrolDestination(movingToFarPoint ? farNavPoint : spawnNavPoint);
        }
    }



    public override void Exit()

    {

        enemy.ResetAnimatorLocomotionSpeed();

        enemy.DeactivateNavMeshAgent();

    }



    private void SetupPatrolRoute()

    {

        spawnNavPoint = enemy.SampleReachableNavPoint(enemy.spawnPosition, enemy.spawnPosition);



        float patrolDistance = PatrolTileRadius * tileSize;

        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };



        farNavPoint = spawnNavPoint;

        float bestDistance = 0f;



        foreach (Vector3 dir in directions)

        {

            Vector3 rawFarPoint = enemy.spawnPosition + dir * patrolDistance;

            Vector3 candidate = enemy.SampleReachableNavPoint(enemy.spawnPosition, rawFarPoint);

            float distance = Vector3.Distance(spawnNavPoint, candidate);



            if (distance > bestDistance)

            {

                bestDistance = distance;

                farNavPoint = candidate;

            }

        }



        if (bestDistance < tileSize * 0.25f)

        {

            foreach (Vector3 dir in directions)

            {

                Vector3 rawFarPoint = enemy.spawnPosition + dir * tileSize;

                Vector3 candidate = enemy.SampleReachableNavPoint(enemy.spawnPosition, rawFarPoint);

                float distance = Vector3.Distance(spawnNavPoint, candidate);



                if (distance > bestDistance)

                {

                    bestDistance = distance;

                    farNavPoint = candidate;

                }

            }

        }



        movingToFarPoint = true;

    }



    private void SetPatrolDestination(Vector3 destination)

    {

        if (enemy.agent != null && enemy.agent.isOnNavMesh)

            enemy.agent.SetDestination(destination);

    }



    private void TrackStuckAndRepath()

    {

        Vector3 pos = enemy.transform.position;

        float movedSq = (pos - lastTrackedPosition).sqrMagnitude;

        lastTrackedPosition = pos;



        if (movedSq > 0.0025f)

        {

            stuckTimer = 0f;

            return;

        }



        stuckTimer += Time.deltaTime;

        if (stuckTimer < 1.25f) return;



        stuckTimer = 0f;

        movingToFarPoint = !movingToFarPoint;

        SetPatrolDestination(movingToFarPoint ? farNavPoint : spawnNavPoint);

    }

}


