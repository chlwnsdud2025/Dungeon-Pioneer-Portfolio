using UnityEngine;

public abstract class ConsumableItemData : ItemData
{
    public sealed override ItemCategory Category => ItemCategory.Consumable;

    /// <summary>사용 효과. 각 소모품이 구현 (switch문 없이 폴리모피즘으로 확장).</summary>
    public abstract void Use(PlayerController user);

    /// <summary>
    /// 지금 사용할 수 있는 상태인지. false면 Inventory.UseItem이 개수를 차감하지 않고 실패를 반환한다.
    /// 물약처럼 언제든 쓸 수 있는 아이템은 그대로 두고, 횃불처럼 상황을 타는 아이템만 재정의한다.
    /// </summary>
    public virtual bool CanUse(PlayerController user) => true;

    protected virtual void Reset()   // 새로 만들 때 기본값: 소모품은 중첩 가능
    {
        isStackable = true;
        maxStackSize = 99;
    }
}