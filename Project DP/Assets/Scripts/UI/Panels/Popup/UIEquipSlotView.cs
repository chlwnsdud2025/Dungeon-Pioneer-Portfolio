using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 장비 패널의 슬롯 하나. 장착된 아이템 아이콘을 표시하고 아래 세 가지 조작을 받는다.
///  · 인벤토리 아이템을 여기에 드롭 → 장착 (아이템의 Slot이 이 칸과 일치할 때만)
///  · 이 칸을 인벤토리로 끌어다 놓기 → 해제
///  · 우클릭 → 해제
/// </summary>
public class UIEquipSlotView : MonoBehaviour,
    IPointerClickHandler, IDropHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, ISlotDragSource
{
    [SerializeField] private EquipmentSlot slotType;    // 이 슬롯이 어느 부위인지
    [SerializeField] private Image iconImage;           // 장착 아이템 아이콘
    [SerializeField] private Sprite emptySprite;        // 빈 슬롯 기본 이미지

    private EquipmentHolder _holder;
    private bool _draggingOut;   // 이 칸에서 아이템을 끌어내는 중

    public EquipmentSlot SlotType => slotType;

    public void Setup(EquipmentHolder holder)
    {
        _holder = holder;
    }

    /// <summary>슬롯 아이콘을 현재 장착 상태에 맞게 갱신.</summary>
    public void Refresh()
    {
        if (_holder == null || iconImage == null) return;

        var item = _holder.GetEquipped(slotType);

        // 슬롯 프리팹의 Image들이 preserveAspect가 꺼진 채로 저장돼 있어, 세로로 긴 아이콘이
        // 정사각형으로 짓눌립니다. 프리팹마다 켜는 대신 여기서 항상 보장합니다.
        iconImage.preserveAspect = true;

        if (item != null)
        {
            // 그리드용 icon이 아니라 1칸 슬롯용 SlotIcon (slotIcon 미지정이면 icon으로 폴백)
            iconImage.sprite = item.Data.SlotIcon;
            iconImage.color = Color.white;
        }
        else
        {
            iconImage.sprite = emptySprite;
            iconImage.color = new Color(1f, 1f, 1f, 0.3f);
        }
    }

    /// <summary>우클릭으로 장비 해제.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;
        _holder?.Unequip(slotType);
    }

    // ── 인벤토리 → 이 칸 (장착) ──────────────────────────────

    public void OnDrop(PointerEventData eventData)
    {
        if (_holder == null || eventData.pointerDrag == null) return;

        InventoryItemView dragged = eventData.pointerDrag.GetComponent<InventoryItemView>();
        if (dragged == null || dragged.Item == null) return;

        // 이 칸에 맞는 부위만 받는다. (헬멧을 무기칸에 놓으면 무시하고 제자리로)
        if (dragged.Item.Data is not EquipmentItemData equipData) return;
        if (equipData.Slot != slotType) return;

        // Equip을 먼저 시도하고 성공했을 때만 정리를 알린다. 실패(교체하려는데 인벤토리가 꽉 참)하면
        // 아무 일도 없었던 것처럼 OnEndDrag가 평소 경로를 타고 아이템이 제자리로 돌아간다.
        if (!_holder.Equip(dragged.Item)) return;

        // Equip이 인벤토리에서 아이템을 빼면서 Rebuild가 동기로 돌아 이 뷰는 이미 비활성일 수 있다.
        // 그래서 OnEndDrag를 기다리지 않고 여기서 직접 정리한다.
        dragged.NotifyConsumedByDropTarget();
    }

    // ── 이 칸 → 인벤토리 (해제) ──────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_holder == null) return;

        InventoryItem equipped = _holder.GetEquipped(slotType);
        if (equipped == null) return;   // 빈 칸은 끌리지 않음

        _draggingOut = true;

        Canvas canvas = GetComponentInParent<Canvas>();
        SlotDragGhost.Show(
            equipped.Data.SlotIcon,
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
        // 실제 해제는 인벤토리에 떨어졌을 때 OnDroppedToInventory에서 처리된다(이 콜백보다 먼저 실행).
        _draggingOut = false;
        SlotDragGhost.Hide();
    }

    /// <summary>인벤토리 영역에 떨어뜨렸을 때 — 장비를 해제해 인벤토리로 되돌린다.</summary>
    public void OnDroppedToInventory()
    {
        if (!_draggingOut || _holder == null) return;
        _holder.Unequip(slotType);   // 인벤토리에 자리가 없으면 false — 장착 상태가 그대로 유지된다
    }
}
