using UnityEngine;

public abstract class ConsumableItemData : ItemData
{
    public sealed override ItemCategory Category => ItemCategory.Consumable;

    /// <summary>사용 효과. 각 소모품이 구현 (switch문 없이 폴리모피즘으로 확장).</summary>
    public abstract void Use(PlayerController user);

    protected virtual void Reset()   // 새로 만들 때 기본값: 소모품은 중첩 가능
    {
        isStackable = true;
        maxStackSize = 99;
    }
}