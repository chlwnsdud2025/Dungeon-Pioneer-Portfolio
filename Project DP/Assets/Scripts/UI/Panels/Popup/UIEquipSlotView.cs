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
}
