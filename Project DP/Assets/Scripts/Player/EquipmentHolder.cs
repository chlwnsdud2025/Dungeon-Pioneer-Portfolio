using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 장비 슬롯(Helmet, Armor, Weapon, Shield)을 관리하는 모델.
/// 장착/해제 시 PlayerStat의 보너스 스탯을 재계산한다.
/// Inventory는 UI 프리팹에 있으므로 Init()으로 런타임 바인딩한다.
/// </summary>
public class EquipmentHolder : MonoBehaviour
{
    [SerializeField] private PlayerStat stat;   // 같은 Player 오브젝트 — 인스펙터 연결

    private Inventory _inventory;               // UI 쪽이라 런타임에 Init으로 받음
    private readonly Dictionary<EquipmentSlot, InventoryItem> _slots = new();

    public bool IsInitialized => _inventory != null && stat != null;

    /// <summary>장비가 바뀔 때마다 발생. UIEquipPanel이 구독해서 갱신한다.</summary>
    public event Action OnEquipmentChanged;

    /// <summary>UI 팝업이 열릴 때 Inventory 참조를 넘겨 초기화한다.</summary>
    public void Init(Inventory inventory)
    {
        _inventory = inventory;
        if (stat == null) stat = GetComponent<PlayerStat>();
    }

    /// <summary>해당 슬롯에 장착된 아이템 조회. 없으면 null.</summary>
    public InventoryItem GetEquipped(EquipmentSlot slot)
    {
        _slots.TryGetValue(slot, out var item);
        return item;
    }

    /// <summary>
    /// 장착: 인벤토리에서 제거 → 슬롯에 넣기.
    /// 이미 해당 슬롯에 장비가 있으면 먼저 해제(인벤토리로 복귀)한다.
    /// </summary>
    public bool Equip(InventoryItem item)
    {
        if (item?.Data is not EquipmentItemData equipData) return false;
        if (_inventory == null) return false;

        // 기존 장비가 있으면 먼저 해제
        if (_slots.ContainsKey(equipData.Slot))
        {
            if (!Unequip(equipData.Slot)) return false;   // 인벤토리 꽉 차서 해제 실패
        }

        // 인벤토리에서 제거 → 슬롯에 저장
        _inventory.RemoveItem(item);
        _slots[equipData.Slot] = item;

        RecalculateStats();
        OnEquipmentChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 해제: 슬롯에서 빼기 → 인벤토리에 자동 배치.
    /// 인벤토리에 자리가 없으면 실패(장비를 되돌린다).
    /// </summary>
    public bool Unequip(EquipmentSlot slot)
    {
        if (!_slots.TryGetValue(slot, out var item)) return false;
        if (_inventory == null) return false;

        // 인벤토리에 복귀 시도
        _slots.Remove(slot);
        bool added = _inventory.AddItem(item.Data, item.Count);

        if (!added)
        {
            // 자리 부족 → 다시 슬롯에 넣고 실패 반환
            _slots[slot] = item;
            return false;
        }

        RecalculateStats();
        OnEquipmentChanged?.Invoke();
        return true;
    }

    /// <summary>장착 장비들의 스탯을 합산해서 PlayerStat 보너스 필드에 기록.</summary>
    private void RecalculateStats()
    {
        if (stat == null) return;

        float atk = 0f, def = 0f, block = 0f;

        foreach (var kvp in _slots)
        {
            switch (kvp.Value.Data)
            {
                case WeaponItemData w:  atk += w.attack;                        break;
                case ArmorItemData a:   def += a.defense;                       break;
                case HelmetItemData h:  def += h.defense;                       break;
                case ShieldItemData s:  def += s.defense; block += s.blockChance; break;
            }
        }

        stat.equipBonusAttack = atk;
        stat.equipBonusDefense = def;
        stat.equipBonusBlockChance = block;
    }
}
