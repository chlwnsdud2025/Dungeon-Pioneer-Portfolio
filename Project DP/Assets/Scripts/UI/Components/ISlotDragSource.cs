/// <summary>
/// 인벤토리 영역에 드롭됐을 때 "슬롯에서 빼내기"를 수행할 수 있는 드래그 소스.
/// 장비칸(UIEquipSlotView)과 퀵슬롯(UIQuickSlotView)이 구현합니다.
///
/// 이 인터페이스를 끼워두면 인벤토리 쪽(InventoryDropZone)은 장비인지 퀵슬롯인지 몰라도 됩니다.
/// 빼내는 방식은 각자 다릅니다 — 장비는 인벤토리로 되돌아가고, 퀵슬롯은 등록만 풀립니다.
/// </summary>
public interface ISlotDragSource
{
    /// <summary>인벤토리 영역에 드롭됐을 때 호출됩니다.</summary>
    void OnDroppedToInventory();
}
