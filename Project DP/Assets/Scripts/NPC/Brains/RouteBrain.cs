using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 정해진 경로(NpcWaypointRoute)를 순서대로 도는 브레인. 경비병·순찰병용입니다.
/// 각 지점에 도착하면 잠시 서 있다가 다음 지점으로 갑니다.
///
/// 대화 중에는 멈췄다가, 대화가 끝나면 가던 지점으로 다시 갑니다(다음 지점으로 건너뛰지 않음).
/// 막힌 칸에 놓인 지점은 경고를 한 번 남기고 건너뜁니다 — 지점 하나 때문에 순찰 전체가 멈추지 않게 합니다.
/// </summary>
public class RouteBrain : NpcBrain
{
    [SerializeField] private NpcWaypointRoute route;

    [Tooltip("각 지점에 도착한 뒤 머무는 시간(초). x~y 사이에서 무작위")]
    [SerializeField] private Vector2 waitSecondsRange = new Vector2(1f, 3f);

    [Tooltip("NpcDefinition.moveSpeed에 곱하는 배율")]
    [SerializeField, Range(0.1f, 1f)] private float speedScale = 0.5f;

    private readonly HashSet<int> warnedPoints = new HashSet<int>();
    private int index;
    private int direction = 1;
    private bool walking;
    private float waitTimer;

    public override void OnLocationReady(NpcActor owner)
    {
        base.OnLocationReady(owner);
        index = 0;
        direction = 1;
        walking = false;
        waitTimer = 0f;

        if (route == null)
            Debug.LogWarning($"[RouteBrain] '{name}'에 경로(NpcWaypointRoute)가 연결되지 않아 움직이지 않습니다.", this);
    }

    public override void Tick()
    {
        if (actor == null || actor.Grid == null || route == null || route.Count == 0) return;

        if (actor.IsEngaged)
        {
            StopWalking();
            waitTimer = 0f;
            return;
        }

        if (walking)
        {
            if (!actor.Mover.HasArrived) return;

            if (actor.Mover.LastResult == PathResult.None)
                WarnOnce($"{index}번 지점까지 가는 길을 찾지 못해 건너뜁니다.");

            StopWalking();
            index = route.NextIndex(index, ref direction);
            waitTimer = RandomWaitTime();
            return;
        }

        waitTimer -= Time.deltaTime;
        if (waitTimer > 0f) return;

        Vector2Int goal = LocationGrid.WorldToCell(route.GetPoint(index));
        if (!actor.Grid.IsPassable(goal, false))
        {
            WarnOnce($"{index}번 지점이 걸을 수 없는 칸 {goal}에 있어 건너뜁니다.");
            index = route.NextIndex(index, ref direction);
            return;
        }

        actor.Mover.SetDestination(goal, false, actor.MoveSpeed * speedScale);
        walking = true;
    }

    public override void OnDowned() => walking = false;

    private void StopWalking()
    {
        if (!walking) return;

        actor.Mover.Stop();
        walking = false;
    }

    private void WarnOnce(string message)
    {
        if (!warnedPoints.Add(index)) return;
        Debug.LogWarning($"[RouteBrain] '{name}': {message}", this);
    }

    private float RandomWaitTime()
        => Random.Range(Mathf.Min(waitSecondsRange.x, waitSecondsRange.y), Mathf.Max(waitSecondsRange.x, waitSecondsRange.y));
}
