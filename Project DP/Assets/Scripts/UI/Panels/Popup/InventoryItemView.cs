using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventoryItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text countText;

    public RectTransform RectTransform => (RectTransform)transform;
    public InventoryItem Item { get; private set; }

    private InventoryGridView view;
    private Vector2Int grabOffset;   // 잡은 칸이 앵커에서 떨어진 정도
    private Vector3 dragOffset;      // 커서-아이콘 위치 차 (잡은 지점 유지)

    public void Setup(InventoryItem it, InventoryGridView owner)
    {
        Item = it; view = owner;
        if (iconImage != null) { iconImage.sprite = it.Data.icon; iconImage.preserveAspect = true; iconImage.raycastTarget = true; }
        if (countText != null) { bool s = it.Count > 1; countText.gameObject.SetActive(s); if (s) countText.text = it.Count.ToString(); }
    }

    public void OnBeginDrag(PointerEventData e)
    {
        view.ScreenToCell(e.position, out Vector2Int cursorCell);
        grabOffset = cursorCell - Item.Anchor;                    // 어느 칸을 잡았나
        dragOffset = RectTransform.position - (Vector3)e.position;

        transform.SetParent(view.DragLayer, true);                // 최상위로 올림
        transform.SetAsLastSibling();
        if (iconImage != null) iconImage.raycastTarget = false;   // 드롭 판정이 밑으로 통과하게
    }

    public void OnDrag(PointerEventData e)
    {
        RectTransform.position = (Vector3)e.position + dragOffset; // 커서 따라가기

        view.ScreenToCell(e.position, out Vector2Int cursorCell);
        Vector2Int anchor = cursorCell - grabOffset;
        bool valid = view.Inventory.Grid.CanPlace(Item.Data, anchor, Item); // 자기 자신 무시
        view.ShowHighlight(Item.Data, anchor, valid);
    }

    public void OnEndDrag(PointerEventData e)
    {
        view.ClearHighlight();

        view.ScreenToCell(e.position, out Vector2Int cursorCell);
        Vector2Int anchor = cursorCell - grabOffset;

        bool placed = view.Inventory.MoveItem(Item, anchor);      // 유효하면 이동(모델→Rebuild)

        Destroy(gameObject);          // 드래그용으로 떠 있던 '이 뷰' 제거
        if (!placed) view.Rebuild();  // 모델이 안 바뀌었으면 수동으로 원위치 복구
    }
}