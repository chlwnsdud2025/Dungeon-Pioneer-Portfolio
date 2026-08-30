using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 시작 시 인벤토리에 기본 아이템을 한 번 지급한다.
///
/// 인벤토리와 같은 오브젝트에 붙여두고 인스펙터에서 목록을 채운다.
///  - 소모품: 아이템 + 지급 개수 (물약 5개, 횃불 100개 등)
///  - 장비  : 부위별로 1개씩
///
/// ★ 지급 시점: UIManager는 팝업을 만들자마자 SetActive(false)로 꺼두기 때문에 Start()가
///   '인벤토리를 처음 열 때'까지 미뤄질 수 있다. 횃불처럼 인벤토리를 열지 않고도 쓰는 아이템이
///   있으므로, UIInventoryPopup.Initialize()가 로드 직후 Grant()를 직접 부른다.
///   (Start()는 이 컴포넌트를 다른 곳에 붙였을 때를 위한 보조 경로 — _granted로 중복을 막는다)
/// </summary>
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
