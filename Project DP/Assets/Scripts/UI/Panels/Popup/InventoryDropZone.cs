using UnityEngine;
using UnityEngine.EventSystems;

public class InventoryDropZone : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        ISlotDragSource source = eventData.pointerDrag.GetComponent<ISlotDragSource>();
        source?.OnDroppedToInventory();
    }
}
