using DG.Tweening;
using UnityEngine;

public class Door : MonoBehaviour, IInteractable, IFloorPersistent
{
    [Header("Open Motion")]
    [Tooltip("회전시킬 경첩. 문짝의 회전축 모서리에 둔 빈 오브젝트. 비우면 이 오브젝트 자신을 돌립니다.")]
    [SerializeField] private Transform hinge;
    [Tooltip("열릴 때 로컬 Y축으로 회전할 각도. 부호로 여닫는 방향을 정합니다.")]
    [SerializeField] private float openAngle = -90f;
    [SerializeField] private float openDuration = 0.6f;
    [SerializeField] private Ease openEase = Ease.OutCubic;

    [Header("Collider")]
    [Tooltip("닫혀 있는 동안 통행을 막는 문짝 콜라이더. 열기 시작하는 순간 꺼집니다.")]
    [SerializeField] private Collider blockingCollider;

    private bool isOpen;
    public bool IsOpen => isOpen;

    // 배치할 때 문을 어느 방향으로 돌려 놓아도 '닫힌 상태 기준 openAngle'이 되도록,
    // 닫힌 로컬 회전을 쿼터니언으로 캡처해 기준으로 씁니다.
    // (localEulerAngles에 덧셈하는 방식은 유니티가 오일러를 0~360으로 정규화해서
    //  회전된 프리팹에서 엉뚱한 각도로 도는 경우가 생깁니다)
    private Quaternion closedLocalRotation;
    private Tween openTween;
    private Vector2Int cell;

    private void Awake()
    {
        if (hinge == null) hinge = transform;
        closedLocalRotation = hinge.localRotation;
    }

    private void OnDestroy()
    {
        // 트윈 도중 층 이동·씬 전환으로 파괴되면 DOTween이 죽은 대상을 계속 돌리므로 정리합니다.
        openTween?.Kill();
    }

    // ── 상호작용 (IInteractable) ─────────────────────────────

    public bool CanInteract(PlayerController player) => !isOpen;

    public void Interact(PlayerController player) => Open();

    private void Open()
    {
        isOpen = true; // 첫 줄에서 세워 트윈이 도는 동안의 재입력을 차단

        // 회전하는 문짝이 플레이어를 밀어내지 않도록, 그리고 열기 전용이라 다시 포커스 잡힐
        // 일이 없으므로 콜라이더는 꺼진 채로 둡니다. (꺼지면 SphereCast에 안 잡혀 포커스도 해제됨)
        if (blockingCollider != null) blockingCollider.enabled = false;

        openTween = hinge
            .DOLocalRotateQuaternion(OpenLocalRotation(), openDuration)
            .SetEase(openEase);
    }

    private Quaternion OpenLocalRotation() => closedLocalRotation * Quaternion.Euler(0f, openAngle, 0f);

    // ── 저장·복원 (IFloorPersistent) ─────────────────────────

    public string PersistentKind => "Door";
    public Vector2Int PersistentCell => cell;

    public void Latch()
    {
        Vector3Int c = GridSystem.Instance.WorldToCell(transform.position);
        cell = new Vector2Int(c.x, c.z);
    }

    public bool TryCapture(out FloorObjectState state)
    {
        state = new FloorObjectState { flag = true };
        return isOpen;   // 열린 문만 기록한다
    }

    public void Restore(FloorObjectState state)
    {
        if (!state.flag) return;

        // 여는 연출이 다시 재생되면 어색하므로 즉시 열린 포즈로 스냅합니다.
        isOpen = true;
        if (blockingCollider != null) blockingCollider.enabled = false;
        hinge.localRotation = OpenLocalRotation();
    }
}
