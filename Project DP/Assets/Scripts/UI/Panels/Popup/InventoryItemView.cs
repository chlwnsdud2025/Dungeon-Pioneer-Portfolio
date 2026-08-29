using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventoryItemView : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler,
    IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text countText;

    public RectTransform RectTransform => (RectTransform)transform;
    public InventoryItem Item { get; private set; }

    /// <summary>
    /// 퀵슬롯 같은 다른 드롭 타깃이 이번 드래그를 처리했는지. true면 OnEndDrag가 인벤토리 이동을 건너뜁니다.
    /// 드롭 타깃(UIQuickSlotView.OnDrop)이 세팅합니다.
    /// </summary>
    public bool ConsumedByDropTarget { get; set; }

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
        ConsumedByDropTarget = false;

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

        // 퀵슬롯 등 드롭 타깃이 이미 처리했다면 그리드 안에서의 이동은 하지 않고 원래 자리로 되돌립니다.
        // (uGUI는 대상의 OnDrop을 먼저 실행하고 그다음 여기로 옵니다)
        if (ConsumedByDropTarget)
        {
            ConsumedByDropTarget = false;
            gameObject.SetActive(false);
            view.Rebuild();
            return;
        }

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

    // ── 우클릭: 컨텍스트 메뉴 ──
    public void OnPointerClick(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Right) return;
        if (isDragging || Item == null) return;

        // 툴팁 숨기고 컨텍스트 메뉴 표시
        ItemTooltipView.Instance?.Hide();
        ItemContextMenu.Instance?.Show(Item, e.position);
    }
}