using UnityEngine;

[CreateAssetMenu(fileName = "Weapon_", menuName = "Item/Equipment/Weapon")]
public class WeaponItemData : EquipmentItemData
{
    public override EquipmentSlot Slot => EquipmentSlot.Weapon;   // 오른손
    [Header("무기 스탯")]
    public int attack;
    public float attackRange;
    public float attackSpeed;
}