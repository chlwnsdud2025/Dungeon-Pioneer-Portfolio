using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(TransitionPath))]
public class LocationPortal : MonoBehaviour
{
    [Header("이 문의 이름")]
    [Tooltip("다른 지역의 포탈이 이 문을 도착지로 지목할 때 쓰는 id. 한 씬 안에서 겹치지 않게 지정하세요.")]
    [SerializeField] private string portalId = "";

    [Header("목적지")]
    [SerializeField] private LocationType destination = LocationType.Overworld;
    [Tooltip("도착 지역에서 플레이어가 나올 LocationPortal의 id. 비우면 같은 id의 포탈을 찾습니다.")]
    [SerializeField] private string arrivalPortalId = "";

    private static readonly Dictionary<string, LocationPortal> portals = new Dictionary<string, LocationPortal>();

    private TransitionPath path;
    private bool isTraveling;
    private bool ignoreUntilExit;   // 여기로 도착했을 때 나오자마자 되돌아가는 것 방지

    public TransitionPath Path
    {
        get
        {
            if (path == null) path = GetComponent<TransitionPath>();
            return path;
        }
    }

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(portalId)) return;
        portals[portalId] = this;
    }

    private void OnDisable()
    {
        if (string.IsNullOrEmpty(portalId)) return;
        if (portals.TryGetValue(portalId, out LocationPortal current) && current == this)
            portals.Remove(portalId);
    }

    private void Start()
    {
        // 이 문으로 도착하는 중이면 걸어 나오는 연출을 재생하고, 아니면 겹침만 검사한다.
        if (WorldManager.TryConsumeArrival(portalId))
        {
            ignoreUntilExit = true;
            StartCoroutine(ArriveRoutine());
            return;
        }

        ignoreUntilExit = IsPlayerOverlapping();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isTraveling || ignoreUntilExit) return;
        if (!IsPlayer(other)) return;

        isTraveling = true;
        StartCoroutine(DepartRoutine());
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other)) ignoreUntilExit = false;
    }

    private IEnumerator DepartRoutine()
    {
        yield return TransitionMotion.Depart(Path);

        string targetId = !string.IsNullOrEmpty(arrivalPortalId) ? arrivalPortalId : portalId;
        WorldManager.TravelToPortal(new LocationId(destination), targetId);
    }

    /// <summary>도착 연출. WorldManager가 같은 씬 안에서 이동할 때도 직접 호출합니다.</summary>
    public IEnumerator ArriveRoutine()
    {
        ignoreUntilExit = true;
        TransitionMotion.PlaceAtInside(Path);
        yield return TransitionMotion.Arrive(Path);
    }

    public static LocationPortal Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return portals.TryGetValue(id, out LocationPortal portal) ? portal : null;
    }

    private bool IsPlayerOverlapping()
    {
        Bounds bounds = GetComponent<Collider>().bounds;
        Collider[] hits = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity, ~0,
                                             QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits)
        {
            if (IsPlayer(hit)) return true;
        }
        return false;
    }

    private static bool IsPlayer(Collider other)
    {
        if (other == null) return false;
        if (other.CompareTag("Player")) return true;

        Transform root = other.transform.root;
        return root != null && root.CompareTag("Player");
    }
}
