using UnityEngine;

public class TreasureChest : MonoBehaviour, IFloorPersistent, IInteractable
{
    public bool isOpen = false;

    public Animator animator;

    private readonly int openTriggerHash = Animator.StringToHash("OpenTrigger");
    private Vector2Int cell;

    // OpenChestState_Player에서 이 함수를 호출할 예정입니다.
    public void Open()
    {
        isOpen = true;
        Debug.Log("상자가 열렸습니다! 아이템 획득!");
        animator.SetTrigger(openTriggerHash);
    }

    // ── 상호작용 (IInteractable) ─────────────────────────────

    public bool CanInteract(PlayerController player) => !isOpen;

    // 열리는 연출(플레이어 모션 + Open() 호출)은 OpenChestState_Player가 담당합니다.
    public void Interact(PlayerController player)
        => player.stateMachine.ChangeState<OpenChestState_Player>();

    // ── 저장·복원 (IFloorPersistent) ─────────────────────────

    public string PersistentKind => "Chest";
    public Vector2Int PersistentCell => cell;

    public void Latch()
    {
        Vector3Int c = GridSystem.Instance.WorldToCell(transform.position);
        cell = new Vector2Int(c.x, c.z);
    }

    public bool TryCapture(out FloorObjectState state)
    {
        state = new FloorObjectState { flag = true };
        return isOpen;   // 열린 상자만 기록한다
    }

    public void Restore(FloorObjectState state)
    {
        if (!state.flag) return;

        isOpen = true;
        if (animator == null) return;

        // 열리는 연출이 다시 재생되면 어색하므로 끝 포즈까지 즉시 감는다.
        animator.SetTrigger(openTriggerHash);
        animator.Update(10f);
    }
}
