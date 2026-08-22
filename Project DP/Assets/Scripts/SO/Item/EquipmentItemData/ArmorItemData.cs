using UnityEngine;

[CreateAssetMenu(fileName = "Armor_", menuName = "Item/Equipment/Armor")]
public class ArmorItemData : EquipmentItemData
{
    public override EquipmentSlot Slot => EquipmentSlot.Armor;

    [Header("갑옷 스탯")]
    public int defense;

    [Header("갑옷 비주얼")]
    [Tooltip("교체할 몸통 메시 (예: Knight FBX 안의 Knight_Body)")]
    public Mesh bodyMesh;

    [Tooltip("교체할 왼팔 메시")]
    public Mesh armLeftMesh;

    [Tooltip("교체할 오른팔 메시")]
    public Mesh armRightMesh;

    [Tooltip("교체할 왼다리 메시")]
    public Mesh legLeftMesh;

    [Tooltip("교체할 오른다리 메시")]
    public Mesh legRightMesh;

    [Tooltip("갑옷 파츠에 적용할 머티리얼 (5개 파츠 공용)")]
    public Material material;
}
