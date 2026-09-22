using System;
using System.Collections.Generic;
using UnityEngine;

public class StartingItems : MonoBehaviour
{
    [Serializable]
    public class ConsumableGrant
    {
        public ConsumableItemData item;
        [Min(1)] public int count = 1;
    }

    [Tooltip("비워두면 같은 오브젝트의 Inventory를 자동으로 찾습니다.")]
    [SerializeField] private Inventory inventory;

    [Header("소모품 (개수 지정)")]
    [SerializeField] private List<ConsumableGrant> consumables = new();

    [Header("장비 (부위별 1개씩)")]
    [SerializeField] private List<EquipmentItemData> equipment = new();

    private bool _granted;

    private void Awake()
    {
        if (inventory == null) inventory = GetComponent<Inventory>();
    }

    private void Start() => Grant();

    /// <summary>기본 아이템을 지급한다. 여러 번 불러도 처음 한 번만 동작한다.</summary>
    public void Grant()
    {
        if (_granted) return;

        if (inventory == null || inventory.Grid == null)
        {
            Debug.LogWarning("[StartingItems] 인벤토리가 아직 준비되지 않아 기본 지급을 건너뜁니다.", this);
            return;
        }

        _granted = true;

        foreach (ConsumableGrant grant in consumables)
        {
            if (grant?.item == null || grant.count <= 0) continue;
            if (!inventory.AddItem(grant.item, grant.count))
                Debug.LogWarning($"[StartingItems] 자리가 부족해 '{grant.item.displayName}'을(를) 다 넣지 못했습니다.", this);
        }

        foreach (EquipmentItemData item in equipment)
        {
            if (item == null) continue;
            if (!inventory.AddItem(item, 1))
                Debug.LogWarning($"[StartingItems] 자리가 부족해 '{item.displayName}'을(를) 넣지 못했습니다.", this);
        }
    }
}
