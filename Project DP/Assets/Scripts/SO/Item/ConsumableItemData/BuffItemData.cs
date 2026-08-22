using UnityEngine;

[CreateAssetMenu(fileName = "Buff_", menuName = "Item/Consumable/Buff")]
public class BuffItemData : ConsumableItemData
{
    [Header("버프")] public float amount; public float duration;
    public override void Use(PlayerController user)
    {
        // TODO: 버프 시스템 구현 후 교체
        Debug.Log($"[Buff] {displayName} 사용: 효과 {amount}, 지속 {duration}초");
    }
}