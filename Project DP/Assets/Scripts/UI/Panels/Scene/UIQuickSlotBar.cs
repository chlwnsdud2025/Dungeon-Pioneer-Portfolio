using System.Collections.Generic;
using UnityEngine;

public class UIQuickSlotBar : MonoBehaviour
{
    [Tooltip("퀵슬롯 칸들. 각 칸의 slotIndex를 0부터 순서대로 지정해주세요.")]
    [SerializeField] private List<UIQuickSlotView> slots = new();

    private QuickSlotHolder _holder;
    private Inventory _inventory;
    private PlayerController _player;

    public bool IsBound => _holder != null && _inventory != null;

    /// <summary>모델과 연결하고 이벤트를 구독합니다.</summary>
    public void Bind(QuickSlotHolder holder, Inventory inventory, PlayerController player)
    {
        Unbind();   // 기존 구독 정리

        if (holder == null || inventory == null) return;

        _holder = holder;
        _inventory = inventory;
        _player = player;

        foreach (UIQuickSlotView slot in slots)
            if (slot != null) slot.Setup(this);

        _holder.OnChanged += Refresh;      // 등록/해제
        _inventory.OnChanged += Refresh;   // 개수 변화
        Refresh();
    }

    /// <summary>이벤트 구독을 해제합니다.</summary>
    public void Unbind()
    {
        if (_holder != null) _holder.OnChanged -= Refresh;
        if (_inventory != null) _inventory.OnChanged -= Refresh;

        _holder = null;
        _inventory = null;
        _player = null;
    }

    private void OnDisable() => Unbind();   // 다시 켜지면 HUD가 알아서 재바인딩

    /// <summary>모든 칸을 현재 상태로 갱신합니다.</summary>
    public void Refresh()
    {
        foreach (UIQuickSlotView slot in slots)
            if (slot != null) slot.Refresh(_holder);
    }

    // ── 슬롯 뷰 / HUD가 호출하는 동작 ────────────────────────

    public bool Assign(int index, ItemData data) => _holder != null && _holder.Assign(index, data);

    public void Clear(int index) => _holder?.Clear(index);

    public bool Use(int index) => _holder != null && _holder.Use(index, _player);
}
