using UnityEngine;

[CreateAssetMenu(fileName = "Helmet_", menuName = "Item/Equipment/Helmet")]
public class HelmetItemData : EquipmentItemData
{
    public override EquipmentSlot Slot => EquipmentSlot.Helmet;
    [Header("헬멧 스탯")] public int defense;
}
