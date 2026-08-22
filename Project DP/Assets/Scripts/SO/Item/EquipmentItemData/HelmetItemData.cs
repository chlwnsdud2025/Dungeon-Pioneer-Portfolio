using UnityEngine;

[CreateAssetMenu(fileName = "Helmet_", menuName = "Item/Equipment/Helmet")]
public class HelmetItemData : EquipmentItemData
{
    public override EquipmentSlot Slot => EquipmentSlot.Helmet;

    [Header("헬멧 스탯")]
    public int defense;

    [Header("헬멧 비주얼")]
    [Tooltip("교체할 머리 메시 (예: Knight FBX 안의 Knight_Head)")]
    public Mesh headMesh;

    [Tooltip("머리 위 악세사리 메시 (Hat/Helmet 등). null이면 악세사리 숨김")]
    public Mesh accessoryMesh;

    [Tooltip("헬멧 파츠에 적용할 머티리얼")]
    public Material material;
}
