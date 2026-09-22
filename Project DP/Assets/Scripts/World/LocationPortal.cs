using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(TransitionPath))]
public class LocationPortal : TransitionArrivalPoint
{
    [Header("이 문의 이름")]
    [Tooltip("다른 지역의 포탈이 이 문을 도착지로 지목할 때 쓰는 id. 한 씬 안에서 겹치지 않게 지정하세요.")]
    [SerializeField] private string portalId = "";

    [Header("목적지")]
    [SerializeField] private LocationType destination = LocationType.Overworld;
    [Tooltip("도착 지역에서 플레이어가 나올 LocationPortal의 id. 비우면 같은 id의 포탈을 찾습니다.")]
    [SerializeField] private string arrivalPortalId = "";

    private TransitionPath path;
    private Collider trigger;
    private bool isTraveling;

    public override string ArrivalId => portalId;
    public override TransitionPath ArrivalPath => Path;

    protected override Collider TriggerCollider
    {
        get
        {
            if (trigger == null) trigger = GetComponent<Collider>();
            return trigger;
        }
    }

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
        if (TriggerCollider != null) TriggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isTraveling || ignoreUntilExit) return;
        if (!IsPlayer(other)) return;

        isTraveling = true;
        StartCoroutine(DepartRoutine());
    }

    private IEnumerator DepartRoutine()
    {
        yield return TransitionMotion.Depart(Path);

        string targetId = !string.IsNullOrEmpty(arrivalPortalId) ? arrivalPortalId : portalId;
        if (WorldManager.TravelToArrival(new LocationId(destination), targetId)) yield break;

        // 이동에 실패했으면 까만 화면에 갇히지 않도록 문 밖으로 도로 걸어 나옵니다.
        isTraveling = false;
        yield return WalkOutRoutine();
    }
}
