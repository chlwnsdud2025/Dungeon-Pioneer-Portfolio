using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 인벤토리 + 장비창을 하나로 묶는 UIPopup.
/// UIManager.ShowPopup&lt;UIInventoryPopup&gt;() 으로 열고,
/// UIManager.HidePopup() 또는 dimmed 클릭으로 닫는다.
/// 커서 표시/숨김, 게임플레이 입력 잠금을 여기서 관리한다.
/// </summary>
public class UIInventoryPopup : UIPopup
{
    [Header("하위 패널")]
    [SerializeField] private Inventory inventory;          // 같은 프리팹 안의 Inventory
    [SerializeField] private UIEquipPanel equipPanel;      // 장비 패널
    [SerializeField] private ItemTooltipView tooltip;      // 아이템 툴팁

    private EquipmentHolder _holder;

    public Inventory Inventory => inventory;

    public override void OnBeforeShow()
    {
        base.OnBeforeShow();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (InputManager.Instance != null)
            InputManager.Instance.SetGameplayEnabled(false);

        // 툴팁 전역 인스턴스 설정
        if (tooltip != null) ItemTooltipView.Instance = tooltip;

        // Player의 EquipmentHolder를 찾아 장비 패널에 바인딩
        BindEquipment();
    }

    protected override UniTask OnHide()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (InputManager.Instance != null)
            InputManager.Instance.SetGameplayEnabled(true);

        if (tooltip != null) tooltip.Hide();
        if (equipPanel != null) equipPanel.Unbind();

        return UniTask.CompletedTask;
    }

    private void BindEquipment()
    {
        if (equipPanel == null) return;

        // Player 찾기
        var playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo == null) return;

        _holder = playerGo.GetComponent<EquipmentHolder>();
        if (_holder == null) return;

        // EquipmentHolder 초기화 (최초 1회)
        if (!_holder.IsInitialized)
            _holder.Init(inventory);

        equipPanel.Bind(_holder);
    }
}
