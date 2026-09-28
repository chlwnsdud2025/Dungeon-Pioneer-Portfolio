using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC가 순서대로 도는 경로. RouteBrain이 읽습니다.
///
/// 경로를 브레인이 아니라 별도 오브젝트로 두는 이유: 경비병 여럿이 같은 순찰로를 공유할 수 있고,
/// 씬 뷰에서 경로를 선으로 보면서 지점만 옮겨 다듬을 수 있습니다.
///
/// 지점 목록을 비워 두면 자식 오브젝트를 계층 순서대로 씁니다. 빈 오브젝트를 자식으로 넣기만 하면 됩니다.
/// </summary>
public class NpcWaypointRoute : MonoBehaviour
{
    public enum RouteMode
    {
        Loop,       // 마지막 지점에서 첫 지점으로 돌아감
        PingPong    // 끝에 닿으면 왔던 길을 거슬러 감
    }

    [SerializeField] private RouteMode mode = RouteMode.Loop;

    [Tooltip("비워 두면 자식 오브젝트를 계층 순서대로 씁니다.")]
    [SerializeField] private List<Transform> points = new List<Transform>();

    private readonly List<Transform> resolved = new List<Transform>();

    public int Count => Resolve().Count;

    public Vector3 GetPoint(int index) => Resolve()[index].position;

    /// <summary>다음 지점 번호. PingPong에서 방향이 바뀌므로 direction을 함께 넘겨받아 고칩니다.</summary>
    public int NextIndex(int current, ref int direction)
    {
        int count = Count;
        if (count <= 1) return 0;

        if (mode == RouteMode.Loop) return (current + 1) % count;

        int next = current + direction;
        if (next < 0 || next >= count)
        {
            direction = -direction;
            next = current + direction;
        }

        return next;
    }

    private List<Transform> Resolve()
    {
        resolved.Clear();

        if (points.Count > 0)
        {
            foreach (Transform point in points)
                if (point != null) resolved.Add(point);
        }
        else
        {
            foreach (Transform child in transform)
                resolved.Add(child);
        }

        return resolved;
    }

    private void OnDrawGizmos()
    {
        List<Transform> list = Resolve();
        if (list.Count == 0) return;

        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.9f);
        for (int i = 0; i < list.Count; i++)
        {
            Gizmos.DrawWireSphere(list[i].position, 0.3f);
            if (i + 1 < list.Count) Gizmos.DrawLine(list[i].position, list[i + 1].position);
        }

        if (mode == RouteMode.Loop && list.Count > 2)
            Gizmos.DrawLine(list[list.Count - 1].position, list[0].position);
    }
}
