using System;
using System.Collections.Generic;
using UnityEngine;

public class TreasureChest : MonoBehaviour, IFloorPersistent, IInteractable
{
    /// <summary>보상 아이템 한 줄: 아이템 + 지급 개수. (StartingItems.ConsumableGrant와 같은 패턴)</summary>
    [Serializable]
    public class LootEntry
    {
        public ItemData item;
        [Min(1)] public int count = 1;
    }

    [Header("보상 (열었을 때 지급)")]
    [Tooltip("지급할 아이템 목록. 장비/소모품 모두 넣을 수 있고, 비워두면 골드만 지급합니다.")]
    [SerializeField] private List<LootEntry> loot = new();

    [Tooltip("지급 골드 범위 [min, max]. 같은 값이면 고정 지급합니다.")]
    [SerializeField, Min(0)] private int goldMin = 0;
    [SerializeField, Min(0)] private int goldMax = 0;

    public bool isOpen = false;

    public Animator animator;

    private readonly int openTriggerHash = Animator.StringToHash("OpenTrigger");
    private Vector2Int cell;

    // 인스펙터에서 max를 min보다 작게 넣는 실수 방지 (ItemDataEditor의 maxStackSize 보정과 같은 결)
    private void OnValidate()
    {
        if (goldMax < goldMin) goldMax = goldMin;
    }

    // 열리는 연출(플레이어 모션)은 OpenChestState_Player가, 열림 확정 + 보상 지급은 여기가 담당합니다.
    public void Open(PlayerController player)
    {
        isOpen = true;
        animator.SetTrigger(openTriggerHash);
        GrantLoot(player);
    }

    private void GrantLoot(PlayerController player)
    {
        // 골드 — 범위 롤은 여는 순간에 확정 (using System 탓에 Random이 모호해져 UnityEngine을 명시)
        int goldAmount = UnityEngine.Random.Range(goldMin, goldMax + 1);
        if (goldAmount > 0)
        {
            if (player.wallet != null)
            {
                player.wallet.AddGold(goldAmount);
                Debug.Log($"<color=yellow>골드 {goldAmount} 획득!</color>");
            }
            else
            {
                Debug.LogWarning("[TreasureChest] Player에 PlayerWallet이 연결되지 않아 골드를 지급하지 못했습니다.", this);
            }
        }

        if (loot.Count == 0) return;   // 아이템이 없는 상자면 골드만 지급

        Inventory inventory = player.Inventory;
        if (inventory == null)
        {
            Debug.LogWarning("[TreasureChest] 인벤토리 연결 전이라 아이템을 지급하지 못했습니다.", this);
            return;
        }

        foreach (LootEntry entry in loot)
        {
            if (entry?.item == null || entry.count <= 0) continue;

            if (inventory.AddItem(entry.item, entry.count))
                Debug.Log($"<color=yellow>'{entry.item.displayName}' x{entry.count} 획득!</color>");
            else
                Debug.LogWarning($"[TreasureChest] 자리가 부족해 '{entry.item.displayName}'을(를) 다 넣지 못했습니다.", this);
        }
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
