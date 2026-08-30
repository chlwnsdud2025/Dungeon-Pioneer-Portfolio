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
    [SerializeField] private ItemContextMenu contextMenu;  // 우클릭 컨텍스트 메뉴
    [SerializeField] private StartingItems startingItems;  // 시작 시 기본 아이템 지급

    private EquipmentHolder _holder;
    private PlayerController _player;

    public Inventory Inventory => inventory;

    public override void Initialize()
    {
        base.Initialize();

        // UIManager는 팝업을 만들자마자 비활성화하므로 StartingItems.Start()가 '인벤토리를 처음 열 때'까지
        // 미뤄질 수 있습니다. 횃불처럼 인벤토리를 열지 않고도 쓰는 아이템이 있어 로드 직후 확정 지급합니다.
        if (startingItems != null) startingItems.Grant();
    }

    public override void OnBeforeShow()
    {
        base.OnBeforeShow();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (InputManager.Instance != null)
            InputManager.Instance.SetGameplayEnabled(false);

        // 전역 인스턴스 설정
        if (tooltip != null) ItemTooltipView.Instance = tooltip;
        if (contextMenu != null) ItemContextMenu.Instance = contextMenu;

        // Player의 EquipmentHolder를 찾아 장비 패널에 바인딩
        BindEquipment();
    }

    protected override UniTask OnHide()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (InputManager.Instance != null)
            InputManager.Instance.SetGameplayEnabled(true);

        if (contextMenu != null) contextMenu.Hide();
        if (tooltip != null) tooltip.Hide();
        if (equipPanel != null) equipPanel.Unbind();

        return UniTask.CompletedTask;
    }

    private void BindEquipment()
    {
        // Player는 PlayerManager가 전역으로 하나만 들고 있습니다.
        // (씬 전환 중에는 잠시 비활성이라 태그 검색으로는 못 찾으므로 PlayerManager를 먼저 봅니다)
        var playerGo = PlayerManager.Instance != null ? PlayerManager.Instance.Player : null;
        if (playerGo == null) playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo == null) return;

        _player = playerGo.GetComponent<PlayerController>();
        _holder = playerGo.GetComponent<EquipmentHolder>();

        // EquipmentHolder 초기화 (최초 1회)
        if (_holder != null && !_holder.IsInitialized)
            _holder.Init(inventory);

        if (equipPanel != null && _holder != null)
            equipPanel.Bind(_holder);

        // 컨텍스트 메뉴에 공통 참조 바인딩
        // QuickSlotHolder의 Init은 HUD가 담당하므로 여기서는 참조만 넘긴다.
        if (contextMenu != null)
            contextMenu.Bind(inventory, _holder, _player, playerGo.GetComponent<QuickSlotHolder>());
    }
}
