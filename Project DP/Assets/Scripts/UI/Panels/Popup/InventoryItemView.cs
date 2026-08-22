using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventoryItemView : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text countText;

    public RectTransform RectTransform => (RectTransform)transform;
    public InventoryItem Item { get; private set; }

    private InventoryGridView view;
    private Vector2Int grabOffset;   // 잡은 칸이 앵커에서 떨어진 정도
    private Vector3 dragOffset;      // 커서-아이콘 위치 차 (잡은 지점 유지)
    private bool isDragging;

    public void Setup(InventoryItem it, InventoryGridView owner)
    {
        Item = it; view = owner;
        if (iconImage != null) { iconImage.sprite = it.Data.icon; iconImage.preserveAspect = true; iconImage.raycastTarget = true; }
        if (countText != null) { bool s = it.Count > 1; countText.gameObject.SetActive(s); if (s) countText.text = it.Count.ToString(); }
    }

    public void OnBeginDrag(PointerEventData e)
    {
        isDragging = true;

        // 드래그 시작 시 툴팁 숨김
        if (ItemTooltipView.Instance != null) ItemTooltipView.Instance.Hide();

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
        isDragging = false;

        view.ScreenToCell(e.position, out Vector2Int cursorCell);
        Vector2Int anchor = cursorCell - grabOffset;

        bool placed = view.Inventory.MoveItem(Item, anchor);
        // placed=true  → MoveItem 내부에서 OnChanged → Rebuild가 동기 실행되어
        //                 이 뷰는 이미 풀에 반납/재활용 처리됨. 추가 작업 불필요.
        // placed=false → 모델이 안 바뀌었으므로 수동으로 원위치 복구
        if (!placed)
        {
            gameObject.SetActive(false);
            view.Rebuild();
        }
    }

    // ── 툴팁: 호버 ──
    public void OnPointerEnter(PointerEventData e)
    {
        if (isDragging || Item == null) return;
        ItemTooltipView.Instance?.Show(Item.Data, e.position);
    }

    public void OnPointerMove(PointerEventData e)
    {
        if (isDragging) return;
        ItemTooltipView.Instance?.UpdatePosition(e.position);
    }

    public void OnPointerExit(PointerEventData e)
    {
        ItemTooltipView.Instance?.Hide();
    }
}