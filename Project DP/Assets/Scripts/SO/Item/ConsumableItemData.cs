using UnityEngine;

/// <summary>소모품 종류. 새 효과 종류가 생기면 여기 값만 추가.</summary>
public enum ConsumableType { Heal, Buff, Usable }

[CreateAssetMenu(fileName = "Consumable_", menuName = "Item/Consumable")]
public class ConsumableItemData : ItemData
{
    public override ItemCategory Category => ItemCategory.Consumable;

    [Header("소모품")]
    public ConsumableType type;

    [Header("효과")]
    [Tooltip("회복량 / 버프 수치 / 데미지 등 (type에 따라 의미가 달라짐)")]
    public float amount;
    [Tooltip("버프 지속시간 등 (해당될 때만)")]
    public float duration;

    // 소모품은 기본적으로 중첩 가능 → 새로 만들 때 기본값 세팅
    private void Reset()
    {
        isStackable = true;
        maxStackSize = 99;
    }
}