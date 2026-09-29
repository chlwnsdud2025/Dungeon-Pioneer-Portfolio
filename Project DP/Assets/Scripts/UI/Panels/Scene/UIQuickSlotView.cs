using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIQuickSlotView : MonoBehaviour,
    IDropHandler, IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, ISlotDragSource
{
    [Header("슬롯")]
    [Tooltip("이 칸의 번호(0부터). 단축키 1~4가 0~3에 대응합니다.")]
    [SerializeField] private int slotIndex;

    [Header("표시")]
    [Tooltip("등록된 아이템 아이콘. 빈 칸이면 꺼집니다. 마우스 판정은 칸 배경 Image가 받아야 빈 칸에도 끌어다 놓을 수 있습니다.")]
    [SerializeField] private Image iconImage;
    [Tooltip("인벤토리에 남은 개수. 정확히 1개일 때는 숨깁니다.")]
    [SerializeField] private TMP_Text countText;
    [Tooltip("(선택) 단축키 표시. 비워두면 슬롯 번호+1로 자동으로 채웁니다.")]
    [SerializeField] private TMP_Text hotkeyText;

    [Header("색")]
    [Tooltip("등록은 되어 있지만 인벤토리 재고가 0일 때의 색.")]
    [SerializeField] private Color outOfStockColor = new Color(1f, 1f, 1f, 0.35f);

    private UIQuickSlotBar _bar;
    private QuickSlotHolder _holder;   // Refresh에서 받아둔다 (드래그 시 아이콘 조회용)
    private bool _draggingOut;         // 이 칸에서 등록을 끌어내는 중

    public int SlotIndex => slotIndex;

    /// <summary>UIQuickSlotBar가 바인딩될 때 호출합니다.</summary>
    public void Setup(UIQuickSlotBar bar)
    {
        _bar = bar;

        if (hotkeyText != null && string.IsNullOrEmpty(hotkeyText.text))
            hotkeyText.text = (slotIndex + 1).ToString();
    }

    /// <summary>등록 상태와 재고를 화면에 반영합니다.</summary>
    public void Refresh(QuickSlotHolder holder)
    {
        _holder = holder;
        if (iconImage == null) return;

        // 세로로 긴 아이콘이 정사각형으로 짓눌리지 않도록 (UIEquipSlotView와 동일한 이유)
        iconImage.preserveAspect = true;

        ItemData data = holder != null ? holder.Get(slotIndex) : null;

        if (data == null)
        {
            iconImage.enabled = false;
            if (countText != null) countText.gameObject.SetActive(false);
            return;
        }

        int stock = holder.StockOf(slotIndex);

        iconImage.enabled = true;
        iconImage.sprite = data.SlotIcon;                              // 없으면 icon으로 폴백
        iconImage.color = stock > 0 ? Color.white : outOfStockColor;   // 다 썼어도 등록은 유지

        if (countText != null)
        {
            bool show = stock != 1;   // 1개일 땐 숫자가 지저분하니 생략, 0개는 보여줘야 함
            countText.gameObject.SetActive(show);
            if (show) countText.text = stock.ToString();
        }
    }

    // ── 인벤토리에서 끌어다 놓기 ─────────────────────────────
    public void OnDrop(PointerEventData eventData)
    {
        if (_bar == null || eventData.pointerDrag == null) return;

        InventoryItemView dragged = eventData.pointerDrag.GetComponent<InventoryItemView>();
        if (dragged == null || dragged.Item == null) return;

        if (!_bar.Assign(slotIndex, dragged.Item.Data)) return;   // 소모품이 아니면 무시

        // 등록에 성공했으니 '인벤토리 안에서의 이동'은 취소하고 원래 자리로 되돌립니다.
        // (uGUI는 OnDrop을 먼저, 그다음 드래그 소스의 OnEndDrag를 실행합니다)
        dragged.NotifyConsumedByDropTarget();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_bar == null) return;

        if (eventData.button == PointerEventData.InputButton.Right) _bar.Clear(slotIndex);
        else if (eventData.button == PointerEventData.InputButton.Left) _bar.Use(slotIndex);
    }

    // ── 이 칸 → 인벤토리 (등록 해제) ─────────────────────────
    // 장비칸과 조작은 같지만 결과가 다릅니다. 퀵슬롯은 ItemData 참조만 들고 있어서(재고 공유)
    // 해제해도 아이템이 인벤토리로 '돌아오는' 게 아니라 등록만 풀립니다.

    public void OnBeginDrag(PointerEventData eventData)
    {
        ItemData data = _holder != null ? _holder.Get(slotIndex) : null;
        if (data == null) return;   // 빈 칸은 끌리지 않음

        _draggingOut = true;

        Canvas canvas = GetComponentInParent<Canvas>();
        SlotDragGhost.Show(
            data.SlotIcon,
            iconImage != null ? iconImage.rectTransform.rect.size : new Vector2(64f, 64f),
            eventData.position,
            canvas != null ? canvas.rootCanvas.transform : null);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_draggingOut) SlotDragGhost.Move(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _draggingOut = false;
        SlotDragGhost.Hide();
    }

    /// <summary>인벤토리 영역에 떨어뜨렸을 때 — 등록만 해제한다. (인벤토리 재고는 그대로)</summary>
    public void OnDroppedToInventory()
    {
        if (!_draggingOut || _bar == null) return;
        _bar.Clear(slotIndex);
    }
}
