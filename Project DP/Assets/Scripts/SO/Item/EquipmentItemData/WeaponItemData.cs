using UnityEngine;

[CreateAssetMenu(fileName = "Weapon_", menuName = "Item/Equipment/Weapon")]
public class WeaponItemData : EquipmentItemData
{
    public override EquipmentSlot Slot => EquipmentSlot.Weapon;   // 오른손

    [Header("무기 스탯")]
    public int attack;
    public float attackRange;
    public float attackSpeed;

    [Header("장착 비주얼")]
    [Tooltip("handslot.r에 생성할 무기 모델 (FBX 또는 프리팹)")]
    public GameObject equipPrefab;
}