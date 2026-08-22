using UnityEngine;

/// <summary>
/// 장비 패널 뷰. 4개의 UIEquipSlotView를 관리하고,
/// EquipmentHolder.OnEquipmentChanged를 구독해 자동 갱신한다.
/// </summary>
public class UIEquipPanel : MonoBehaviour
{
    [SerializeField] private UIEquipSlotView helmetSlot;
    [SerializeField] private UIEquipSlotView armorSlot;
    [SerializeField] private UIEquipSlotView weaponSlot;
    [SerializeField] private UIEquipSlotView shieldSlot;

    private EquipmentHolder _holder;

    /// <summary>EquipmentHolder를 연결하고 이벤트 구독.</summary>
    public void Bind(EquipmentHolder holder)
    {
        Unbind();   // 기존 구독 정리

        _holder = holder;
        if (_holder == null) return;

        // 각 슬롯에 holder 전달
        helmetSlot?.Setup(_holder);
        armorSlot?.Setup(_holder);
        weaponSlot?.Setup(_holder);
        shieldSlot?.Setup(_holder);

        _holder.OnEquipmentChanged += Refresh;
        Refresh();
    }

    /// <summary>이벤트 구독 해제.</summary>
    public void Unbind()
    {
        if (_holder != null)
            _holder.OnEquipmentChanged -= Refresh;
        _holder = null;
    }

    /// <summary>4개 슬롯 아이콘을 현재 장착 상태로 갱신.</summary>
    public void Refresh()
    {
        helmetSlot?.Refresh();
        armorSlot?.Refresh();
        weaponSlot?.Refresh();
        shieldSlot?.Refresh();
    }
}
