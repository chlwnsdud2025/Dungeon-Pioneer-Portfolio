using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스폰 칸 주변을 한가롭게 돌아다니는 브레인. 대기 → 주변의 갈 수 있는 칸 하나로 걸어감 → 대기를 반복합니다.
///
/// 목적지 후보는 GridSearch로 스폰 칸에서 실제로 걸어서 닿는 칸만 고릅니다. 벽이나 GridBlocker 너머의
/// 칸은 애초에 후보가 아니라서, 갈 수 없는 곳을 향해 제자리걸음을 하는 일이 없습니다.
/// 중심을 현재 위치가 아니라 스폰 칸으로 두는 이유는, 그래야 오래 두어도 NPC가 제자리를 벗어나지 않기 때문입니다.
/// </summary>
public class WanderBrain : NpcBrain
{
    private const float RetryInterval = 1f;

    [Tooltip("스폰 칸에서 몇 칸 안쪽까지 돌아다닐지 (걸어서 가는 거리 기준)")]
    [SerializeField, Min(1)] private int radius = 4;

    [Tooltip("한 곳에 도착한 뒤 다음 목적지로 떠나기까지 기다리는 시간(초). x~y 사이에서 무작위")]
    [SerializeField] private Vector2 idleSecondsRange = new Vector2(2f, 5f);

    [Tooltip("NpcDefinition.moveSpeed에 곱하는 배율. 배회는 달리지 않으므로 낮게 둡니다.")]
    [SerializeField, Range(0.1f, 1f)] private float speedScale = 0.5f;

    private readonly List<Vector2Int> candidates = new List<Vector2Int>();
    private bool walking;
    private float idleTimer;

    public override void OnLocationReady(NpcActor owner)
    {
        base.OnLocationReady(owner);
        walking = false;
        idleTimer = RandomIdleTime();
    }

    public override void Tick()
    {
        if (actor == null || actor.Grid == null) return;

        if (actor.IsEngaged)
        {
            StopWalking();
            idleTimer = RandomIdleTime();
            return;
        }

        if (walking)
        {
            if (!actor.Mover.HasArrived) return;

            bool noPath = actor.Mover.LastResult == PathResult.None;
            StopWalking();
            idleTimer = noPath ? RetryInterval : RandomIdleTime();
            return;
        }

        idleTimer -= Time.deltaTime;
        if (idleTimer > 0f) return;

        GridSearch.CollectReachable(actor.Grid, actor.spawnCell, radius, false, candidates);
        if (candidates.Count == 0)
        {
            idleTimer = RetryInterval;
            return;
        }

        Vector2Int goal = candidates[Random.Range(0, candidates.Count)];
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

    private float RandomIdleTime()
        => Random.Range(Mathf.Min(idleSecondsRange.x, idleSecondsRange.y), Mathf.Max(idleSecondsRange.x, idleSecondsRange.y));
}
