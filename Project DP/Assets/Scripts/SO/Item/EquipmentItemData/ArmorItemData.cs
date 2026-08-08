using UnityEngine;

[CreateAssetMenu(fileName = "Armor_", menuName = "Item/Equipment/Armor")]
public class ArmorItemData : EquipmentItemData
{
    public override EquipmentSlot Slot => EquipmentSlot.Armor;
    [Header("갑옷 스탯")] public int defense;
}
