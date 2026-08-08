using UnityEngine;

public abstract class EquipmentItemData : ItemData
{
    public sealed override ItemCategory Category => ItemCategory.Equipment;

    /// <summary>어느 칸에 착용되는지. 각 장비 서브클래스가 반환 (설정 실수 방지).</summary>
    public abstract EquipmentSlot Slot { get; }

    [Header("공통 장비 (필요 시)")]
    public int requiredLevel;   // 예시 공통 필드. 없으면 지워도 됨

    protected virtual void Reset()   // 새로 만들 때 기본값: 장비는 중첩 불가
    {
        isStackable = false;
        maxStackSize = 1;
    }
}