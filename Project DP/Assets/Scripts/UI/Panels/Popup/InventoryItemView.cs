using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class InventoryItemView : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler,
    IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text countText;

    // ★ 드래그 중에는 이 아이콘 전체가 레이캐스트를 통과시켜야 밑의 드롭 타깃(퀵슬롯·장비칸)이 판정을 받습니다.
    //   아이콘 Image만 꺼서는 부족합니다 — 개수 텍스트처럼 같이 딸려 움직이는 Graphic이 커서 아래에 오면
    //   그게 판정을 가로챕니다(중첩 아이템에서 드롭이 자주 실패하던 원인). CanvasGroup으로 한 번에 처리합니다.
    private CanvasGroup canvasGroup;

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

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Setup(InventoryItem it, InventoryGridView owner)
    {
        Item = it; view = owner;

        // 풀에서 재사용되는 뷰라, 이전 드래그의 잔여 상태가 따라오지 않도록 초기화한다.
        isDragging = false; ConsumedByDropTarget = false;
        SetDragPassThrough(false);
        if (iconImage != null) { iconImage.sprite = it.Data.icon; iconImage.preserveAspect = true; iconImage.raycastTarget = true; }
        if (countText != null) { bool s = it.Count > 1; countText.gameObject.SetActive(s); if (s) countText.text = it.Count.ToString(); }
    }

    /// <summary>
    /// 드롭 타깃이 이번 드래그를 처리했음을 알린다. 드래그 시각 상태를 여기서 즉시 정리하는 이유:
    /// 장비 장착처럼 드롭 처리가 아이템을 인벤토리에서 빼면 Rebuild가 동기로 돌면서 이 뷰가
    /// SetActive(false)로 풀에 반납되고, 비활성 오브젝트에는 uGUI가 OnEndDrag를 보내지 않는다.
    /// 그러면 하이라이트가 화면에 남고 isDragging도 켜진 채로 재사용된다.
    /// </summary>
    public void NotifyConsumedByDropTarget()
    {
        ConsumedByDropTarget = true;
        isDragging = false;
        SetDragPassThrough(false);
        view.ClearHighlight();
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
        SetDragPassThrough(true);                                 // 드롭 판정이 밑으로 통과하게
    }

    /// <summary>드래그 중인 아이콘이 레이캐스트를 통과시킬지. 자식(개수 텍스트 등)까지 한 번에 적용됩니다.</summary>
    private void SetDragPassThrough(bool passThrough)
    {
        if (canvasGroup != null) canvasGroup.blocksRaycasts = !passThrough;
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
        SetDragPassThrough(false);

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