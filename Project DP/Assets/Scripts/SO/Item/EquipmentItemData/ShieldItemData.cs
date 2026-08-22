using UnityEngine;

[CreateAssetMenu(fileName = "Shield_", menuName = "Item/Equipment/Shield")]
public class ShieldItemData : EquipmentItemData
{
    public override EquipmentSlot Slot => EquipmentSlot.Shield;   // 왼손

    [Header("방패 스탯")]
    public int defense;
    [Range(0, 100)] public float blockChance;

    [Header("장착 비주얼")]
    [Tooltip("handslot.l에 생성할 방패 모델 (FBX 또는 프리팹)")]
    public GameObject equipPrefab;
}