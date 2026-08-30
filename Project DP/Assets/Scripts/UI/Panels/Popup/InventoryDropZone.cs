using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// "인벤토리 영역에 놓았다"를 받아주는 드롭 존. 장비칸·퀵슬롯을 여기로 끌어다 놓으면 해제됩니다.
///
/// ★ 배치 위치 주의: 그리드 영역(CellRoot/ItemRoot/GridArea)에는 Image가 없고 Cell 프리팹도
///   raycastTarget이 꺼져 있어서, 그리드 위에서 놓은 드롭은 실제로는 그 뒤의 BG 이미지가 받습니다.
///   그래서 이 컴포넌트는 BG를 자식으로 갖는 'UIInven' 오브젝트에 붙여야 버블링된 드롭을 받습니다.
///
/// 끌려온 것이 ISlotDragSource가 아니면(예: 그리드 안에서 아이템을 옮기는 중) 아무 일도 하지 않습니다.
/// </summary>
public class InventoryDropZone : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        ISlotDragSource source = eventData.pointerDrag.GetComponent<ISlotDragSource>();
        source?.OnDroppedToInventory();
    }
}
