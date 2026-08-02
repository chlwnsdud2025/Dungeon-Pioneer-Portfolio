using UnityEngine;

/// <summary>아이템의 큰 분류. 새 분류(재료/퀘스트 등)가 생기면 여기 값 추가 + 파생 클래스 하나 만들면 됩니다.</summary>
public enum ItemCategory { Equipment, Consumable }

/// <summary>
/// 모든 아이템 SO의 공통 부모. 이름/아이콘/설명/중첩 같은 공통 정보를 담고,
/// 실제 카테고리(장비/소모)와 세부 데이터는 파생 클래스가 정의합니다.
/// 추상 클래스라 이 자체로는 애셋을 못 만들고, EquipmentItemData/ConsumableItemData로만 생성됩니다.
/// </summary>
public abstract class ItemData : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("저장/조회용 고유 ID (겹치지 않게)")]
    public string itemId;
    public string displayName;
    [TextArea(2, 4)] public string description;
    public Sprite icon;

    [Header("중첩")]
    public bool isStackable = false;
    [Min(1)] public int maxStackSize = 1;

    /// <summary>이 아이템의 카테고리. 파생 클래스가 반환 (분기/필터에 사용).</summary>
    public abstract ItemCategory Category { get; }
}