using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 장비 패널의 슬롯 하나. 장착된 아이템 아이콘을 표시하고,
/// 우클릭으로 장비를 해제(인벤토리로 복귀)한다.
/// </summary>
public class UIEquipSlotView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private EquipmentSlot slotType;    // 이 슬롯이 어느 부위인지
    [SerializeField] private Image iconImage;           // 장착 아이템 아이콘
    [SerializeField] private Sprite emptySprite;        // 빈 슬롯 기본 이미지

    private EquipmentHolder _holder;

    public EquipmentSlot SlotType => slotType;

    public void Setup(EquipmentHolder holder)
    {
        _holder = holder;
    }

    /// <summary>슬롯 아이콘을 현재 장착 상태에 맞게 갱신.</summary>
    public void Refresh()
    {
        if (_holder == null) return;

        var item = _holder.GetEquipped(slotType);

        if (item != null)
        {
            iconImage.sprite = item.Data.icon;
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
}
