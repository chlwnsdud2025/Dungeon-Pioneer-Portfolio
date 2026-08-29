using System;
using UnityEngine;

/// <summary>
/// 플레이어의 퀵슬롯(단축키 1~4)을 관리하는 모델. Player 오브젝트에 부착한다.
///
/// ★ 슬롯은 아이템 스택을 '소유'하지 않고 ItemData 참조만 기억합니다(재고 공유 방식).
///   사용하면 인벤토리 재고에서 1개가 빠지므로,
///    - 등록해도 인벤토리에서 물약이 사라지지 않고
///    - 나중에 주운 물약이 자동으로 개수에 합산되며
///    - 스택을 옮기거나 버려도 참조가 깨지지 않습니다.
///   (장비는 EquipmentHolder가 '소유' 방식입니다. 착용 중인 검은 인벤토리에 없어야 하니까요)
///
/// Inventory는 UI 프리팹에 있으므로 EquipmentHolder처럼 Init()으로 런타임 바인딩합니다.
/// 바인딩은 항상 살아 있는 UIInGameHUD가 담당합니다 — 인벤토리를 한 번도 열지 않아도 단축키가 동작해야 하므로.
/// </summary>
public class QuickSlotHolder : MonoBehaviour
{
    /// <summary>퀵슬롯 개수. 단축키 1~4와 1:1 대응됩니다.</summary>
    public const int SlotCount = 4;

    private readonly ItemData[] _slots = new ItemData[SlotCount];
    private Inventory _inventory;

    public bool IsInitialized => _inventory != null;

    /// <summary>슬롯 구성이 바뀔 때 발생. UIQuickSlotBar가 구독해 갱신합니다.</summary>
    public event Action OnChanged;

    /// <summary>HUD가 인벤토리 참조를 넘겨 초기화합니다.</summary>
    public void Init(Inventory inventory) => _inventory = inventory;

    public static bool IsValidIndex(int index) => index >= 0 && index < SlotCount;

    /// <summary>슬롯에 등록된 아이템. 비어 있으면 null.</summary>
    public ItemData Get(int index) => IsValidIndex(index) ? _slots[index] : null;

    /// <summary>이 아이템이 등록된 슬롯 번호. 등록되어 있지 않으면 -1. (등록/해제 토글 판정에 사용)</summary>
    public int IndexOf(ItemData data)
    {
        if (data == null) return -1;
        for (int i = 0; i < SlotCount; i++)
            if (_slots[i] == data) return i;
        return -1;
    }

    /// <summary>이 슬롯 아이템의 현재 인벤토리 재고. 등록이 없거나 다 썼으면 0.</summary>
    public int StockOf(int index)
    {
        ItemData data = Get(index);
        return (data != null && _inventory != null) ? _inventory.CountOf(data) : 0;
    }

    /// <summary>지정한 슬롯에 등록. 소모품만 가능하며, 다른 칸에 이미 있으면 그 칸을 비웁니다.</summary>
    public bool Assign(int index, ItemData data)
    {
        if (!IsValidIndex(index)) return false;
        if (data is not ConsumableItemData) return false;   // 장비 등은 퀵슬롯 대상이 아님

        int existing = IndexOf(data);
        if (existing == index) return true;                 // 이미 그 자리에 있음
        if (existing >= 0) _slots[existing] = null;         // 같은 아이템이 두 칸을 차지하지 않도록

        _slots[index] = data;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>비어 있는 첫 칸에 등록. 이미 등록된 아이템이면 그 칸을 알려줍니다. 4칸이 다 차 있으면 false.</summary>
    public bool AssignToFirstEmpty(ItemData data, out int index)
    {
        index = -1;
        if (data is not ConsumableItemData) return false;

        int existing = IndexOf(data);
        if (existing >= 0) { index = existing; return true; }

        for (int i = 0; i < SlotCount; i++)
        {
            if (_slots[i] != null) continue;
            index = i;
            return Assign(i, data);
        }
        return false;
    }

    /// <summary>슬롯을 비웁니다. 잘못된 인덱스나 이미 빈 칸이면 아무 일도 하지 않습니다.</summary>
    public void Clear(int index)
    {
        if (!IsValidIndex(index) || _slots[index] == null) return;
        _slots[index] = null;
        OnChanged?.Invoke();
    }



    /// <summary>
    /// 슬롯의 아이템을 1개 사용합니다. 재고가 없으면 아무 일도 일어나지 않고 false.
    /// 효과 실행과 개수 차감은 Inventory.UseItem이 기존 경로 그대로 처리합니다.
    /// </summary>
    public bool Use(int index, PlayerController user)
    {
        ItemData data = Get(index);
        if (data == null || _inventory == null || user == null) return false;
        return _inventory.UseItem(data, user);
    }
}
