using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class TransitionArrivalPoint : MonoBehaviour
{
    private static readonly Dictionary<string, TransitionArrivalPoint> points =
        new Dictionary<string, TransitionArrivalPoint>();

    protected bool ignoreUntilExit;

    public abstract string ArrivalId { get; }
    public abstract TransitionPath ArrivalPath { get; }

    protected abstract Collider TriggerCollider { get; }

    public static TransitionArrivalPoint Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return points.TryGetValue(id, out TransitionArrivalPoint point) ? point : null;
    }

    protected virtual void OnEnable()
    {
        if (string.IsNullOrEmpty(ArrivalId)) return;
        points[ArrivalId] = this;
    }

    protected virtual void OnDisable()
    {
        if (string.IsNullOrEmpty(ArrivalId)) return;
        if (points.TryGetValue(ArrivalId, out TransitionArrivalPoint current) && current == this)
            points.Remove(ArrivalId);
    }

    protected virtual void Start()
    {
        if (WorldManager.TryConsumeArrival(ArrivalId))
        {
            ignoreUntilExit = true;
            StartCoroutine(ArriveRoutine());
            return;
        }

        // 씬이 새로 로드되면 플래그가 초기화되므로, 도착 지점이 트리거와 겹쳐 있으면
        // 나오자마자 그대로 다시 들어가버립니다. 한 번 벗어나기 전까지는 무시합니다.
        ignoreUntilExit = IsPlayerOverlapping();
    }

    protected virtual void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other)) ignoreUntilExit = false;
    }

    /// <summary>
    /// 이 지점으로 도착하는 연출. 플레이어를 문 안쪽에 세운 뒤 바깥으로 걸어 나오게 합니다.
    /// WorldManager가 같은 씬 안에서 이동할 때도 직접 호출합니다.
    /// </summary>
    public IEnumerator ArriveRoutine()
    {
        ignoreUntilExit = true;

        if (ArrivalPath != null && ArrivalPath.IsValid)
            TransitionMotion.PlaceAtInside(ArrivalPath);
        else
            PlaceWithoutMotion();

        yield return WalkOutRoutine();
    }

    /// <summary>
    /// 문 바깥으로 걸어 나온 뒤 재진입 차단을 푸는 부분. 전환이 실패해 되돌아올 때도 씁니다.
    /// 연출 중에는 CharacterController가 꺼져 있어 트리거 이벤트가 오지 않으므로,
    /// OnTriggerExit를 기다리지 않고 겹침 여부를 직접 확인해야 다음 접촉이 씹히지 않습니다.
    /// </summary>
    public IEnumerator WalkOutRoutine()
    {
        ignoreUntilExit = true;
        yield return TransitionMotion.Arrive(ArrivalPath);

        yield return null;
        ignoreUntilExit = IsPlayerOverlapping();
    }

    private void PlaceWithoutMotion()
    {
        Debug.LogError($"[{GetType().Name}] '{ArrivalId}'에 쓸 TransitionPath가 없어 도착 연출을 건너뜁니다.", this);

        if (PlayerManager.Instance == null) return;
        if (PlayerManager.Instance.PlaceAtAnchor(ArrivalId)) return;

        PlayerManager.Instance.PlaceAt(transform.position, transform.rotation);
    }

    /// <summary>지금 이 트리거 안에 플레이어가 들어와 있는지 확인합니다.</summary>
    protected bool IsPlayerOverlapping()
    {
        Collider col = TriggerCollider;
        if (col == null) return false;

        Collider[] hits;
        if (col is BoxCollider box)
        {
            hits = Physics.OverlapBox(
                transform.TransformPoint(box.center),
                Vector3.Scale(box.size, transform.lossyScale) * 0.5f,
                transform.rotation,
                ~0,
                QueryTriggerInteraction.Ignore);
        }
        else
        {
            Bounds bounds = col.bounds;
            hits = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity, ~0,
                                      QueryTriggerInteraction.Ignore);
        }

        foreach (Collider hit in hits)
        {
            if (IsPlayer(hit)) return true;
        }

        return false;
    }

    protected static bool IsPlayer(Collider other)
    {
        if (other == null) return false;
        if (other.CompareTag("Player")) return true;

        // CharacterController / 자식 콜라이더가 다른 태그를 가질 때 대비
        Transform root = other.transform.root;
        return root != null && root.CompareTag("Player");
    }
}
