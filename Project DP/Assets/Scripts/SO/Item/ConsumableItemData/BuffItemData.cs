using UnityEngine;

[CreateAssetMenu(fileName = "Buff_", menuName = "Item/Consumable/Buff")]
public class BuffItemData : ConsumableItemData
{
    [Header("버프")] public float amount; public float duration;
    public override void Use(PlayerController user)
    {
        // 예) user.ApplyBuff(amount, duration);
    }
}