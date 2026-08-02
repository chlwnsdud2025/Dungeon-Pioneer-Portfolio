using UnityEngine;

/// <summary>장비 착용 부위. 무기=오른손, 방패=왼손. 새 부위가 생기면 여기 값만 추가.</summary>
public enum EquipmentSlot { Helmet, Armor, Weapon, Shield }

[CreateAssetMenu(fileName = "Equipment_", menuName = "Item/Equipment")]
public class EquipmentItemData : ItemData
{
    public override ItemCategory Category => ItemCategory.Equipment;

    [Header("장비")]
    public EquipmentSlot slot;

    [Header("스탯")]
    public int attack;
    public int defense;
    // 필요 스탯 자유롭게 추가 (체력, 이동속도 등)

    // 장비는 기본적으로 중첩 불가 → 새로 만들 때 기본값 세팅
    private void Reset()
    {
        isStackable = false;
        maxStackSize = 1;
    }
}