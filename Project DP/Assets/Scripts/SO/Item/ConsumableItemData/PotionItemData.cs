using UnityEngine;

[CreateAssetMenu(fileName = "Potion_", menuName = "Item/Consumable/Potion")]
public class PotionItemData : ConsumableItemData
{
    [Header("회복")] public float healAmount;
    public override void Use(PlayerController user)
    {
        user.stat.Heal(healAmount);
    }
}