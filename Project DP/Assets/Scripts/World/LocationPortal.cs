using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LocationPortal : MonoBehaviour
{
    [Header("목적지")]
    [SerializeField] private LocationType destination = LocationType.Overworld;
    [Tooltip("도착 씬에서 플레이어를 놓을 SpawnAnchor의 id.")]
    [SerializeField] private string arrivalAnchorId = "Start";

    [Header("복귀 지점")]
    [Tooltip("목적지에서 이쪽으로 되돌아올 때 쓸 이 씬의 SpawnAnchor id. 비우면 기억하지 않습니다.")]
    [SerializeField] private string returnAnchorId = "";

    [Header("연출")]
    [Min(0f), SerializeField] private float fadeDuration = 0.4f;

    private bool isTraveling;
    private bool ignoreUntilExit;   // 도착 앵커가 포탈과 겹쳐 있을 때 나오자마자 되돌아가는 것 방지

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Start()
    {
        ignoreUntilExit = IsPlayerOverlapping();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isTraveling || ignoreUntilExit) return;
        if (!IsPlayer(other)) return;

        isTraveling = true;
        StartCoroutine(TravelRoutine());
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other)) ignoreUntilExit = false;
    }

    private IEnumerator TravelRoutine()
    {
        PlayerController player = PlayerManager.Instance != null ? PlayerManager.Instance.Controller : null;
        if (player != null) player.MoveCharacter(Vector3.zero);

        WorldManager.RememberReturn(returnAnchorId);

        yield return ScreenFader.Instance.FadeTo(1f, fadeDuration);
        ScreenFader.Instance.RevealAfterNextSceneLoad(fadeDuration);
        WorldManager.Travel(new LocationId(destination), arrivalAnchorId);
    }

    private bool IsPlayerOverlapping()
    {
        Collider col = GetComponent<Collider>();
        Bounds bounds = col.bounds;

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
