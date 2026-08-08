using UnityEngine;

[CreateAssetMenu(fileName = "Potion_", menuName = "Item/Consumable/Potion")]
public class PotionItemData : ConsumableItemData
{
    [Header("회복")] public float healAmount;
    public override void Use(PlayerController user)
    {
        // 예) user.Heal(healAmount);   ← PlayerController에 회복 API 추가 시
    }
}