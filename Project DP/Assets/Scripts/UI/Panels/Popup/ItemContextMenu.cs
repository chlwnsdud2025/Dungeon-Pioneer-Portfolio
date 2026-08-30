using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 아이템 우클릭 시 나타나는 컨텍스트 메뉴.
/// 사용(소모품) / 장착(장비) / 버리기 버튼을 아이템 타입에 따라 표시한다.
/// UIInventoryPopup이 static Instance를 설정하고, InventoryItemView가 호출한다.
/// </summary>
public class ItemContextMenu : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] private Button useButton;       // 사용 (소모품)
    [SerializeField] private Button equipButton;     // 장착 (장비)
    [SerializeField] private Button dropButton;      // 버리기
    [Tooltip("퀵슬롯 등록/해제 토글 (소모품일 때만 표시)")]
    [SerializeField] private Button quickSlotButton;
    [Tooltip("(선택) 퀵슬롯 버튼의 라벨. 등록 여부에 따라 문구가 바뀝니다.")]
    [SerializeField] private TMP_Text quickSlotLabel;

    [Header("외부 클릭 차단")]
    [SerializeField] private Button blocker;         // 투명 풀스크린 — 바깥 클릭 시 닫기

    [Header("메뉴 패널")]
    [SerializeField] private RectTransform menuPanel;  // 버튼들을 담는 패널 (위치 이동 대상)

    private InventoryItem _item;
    private Inventory _inventory;
    private EquipmentHolder _equipHolder;
    private QuickSlotHolder _quickSlots;
    private PlayerController _player;

    /// <summary>InventoryItemView가 접근하는 전역 인스턴스.</summary>
    public static ItemContextMenu Instance { get; set; }

    public bool IsOpen => gameObject.activeSelf;

    /// <summary>팝업이 열릴 때 공통 참조를 한 번 바인딩한다.</summary>
    public void Bind(Inventory inventory, EquipmentHolder holder, PlayerController player, QuickSlotHolder quickSlots)
    {
        _inventory = inventory;
        _equipHolder = holder;
        _player = player;
        _quickSlots = quickSlots;
    }

    private void Awake()
    {
        // 버튼 리스너 등록
        useButton?.onClick.AddListener(OnUseClicked);
        equipButton?.onClick.AddListener(OnEquipClicked);
        dropButton?.onClick.AddListener(OnDropClicked);
        quickSlotButton?.onClick.AddListener(OnQuickSlotClicked);
        blocker?.onClick.AddListener(Hide);

        gameObject.SetActive(false);
    }

    /// <summary>
    /// 컨텍스트 메뉴를 표시한다. Bind()로 공통 참조가 설정된 상태여야 한다.
    /// </summary>
    public void Show(InventoryItem item, Vector2 screenPos)
    {
        if (item == null) return;

        _item = item;

        // 부모를 먼저 활성화해야 자식 SetActive가 레이아웃 dirty를 제대로 전파한다
        gameObject.SetActive(true);

        // 아이템 타입에 따라 버튼 표시/숨김
        bool isConsumable = item.Data is ConsumableItemData;
        bool isEquipment  = item.Data is EquipmentItemData;

        // Button 컴포넌트가 자식에 있을 수 있으므로, MenuPanel 직속 부모를 토글한다
        SetButtonVisible(useButton, isConsumable);
        SetButtonVisible(equipButton, isEquipment);
        SetButtonVisible(dropButton, true);

        // 퀵슬롯은 소모품만. 이미 등록된 아이템이면 같은 버튼이 '해제'로 동작한다.
        bool canQuickSlot = isConsumable && _quickSlots != null;
        SetButtonVisible(quickSlotButton, canQuickSlot);
        if (canQuickSlot && quickSlotLabel != null)
            quickSlotLabel.text = _quickSlots.IndexOf(item.Data) >= 0 ? "Unassign" : "Assign";

        // 레이아웃 강제 갱신
        Canvas.ForceUpdateCanvases();
        if (menuPanel != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(menuPanel);

        // 메뉴 위치 설정
        if (menuPanel != null)
            SetPosition(screenPos);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        _item = null;
    }

    // ── 버튼 액션 ──

    private void OnUseClicked()
    {
        if (_item == null || _inventory == null || _player == null) return;
        _inventory.UseItem(_item, _player);
        Hide();
    }

    private void OnEquipClicked()
    {
        if (_item == null || _equipHolder == null) return;
        _equipHolder.Equip(_item);
        Hide();
    }

    private void OnDropClicked()
    {
        if (_item == null || _inventory == null) return;
        _inventory.RemoveItem(_item);
        Hide();
    }

    /// <summary>
    /// 퀵슬롯 등록/해제 토글. 등록해도 아이템은 인벤토리에 그대로 남는다(재고 공유 방식).
    /// </summary>
    private void OnQuickSlotClicked()
    {
        if (_item == null || _quickSlots == null) return;

        int registered = _quickSlots.IndexOf(_item.Data);
        if (registered >= 0)
            _quickSlots.Clear(registered);
        else if (!_quickSlots.AssignToFirstEmpty(_item.Data, out _))
            Debug.Log("<color=yellow>퀵슬롯이 가득 찼습니다. 기존 슬롯을 우클릭하거나 Unassign으로 비워주세요.</color>");

        Hide();
    }

    /// <summary>
    /// Button 컴포넌트가 MenuPanel 직속 자식이 아니라 래퍼 오브젝트 안에 있을 수 있으므로,
    /// MenuPanel의 직속 자식(레이아웃 단위)을 찾아 SetActive한다.
    /// </summary>
    private void SetButtonVisible(Button btn, bool visible)
    {
        if (btn == null || menuPanel == null) return;

        // Button이 MenuPanel 직속 자식이면 그대로, 아니면 MenuPanel 직속 부모를 찾는다
        Transform target = btn.transform;
        while (target.parent != null && target.parent != menuPanel)
            target = target.parent;

        target.gameObject.SetActive(visible);
    }

    // ── 위치 설정 (화면 밖 나가지 않도록 클램핑) ──

    private void SetPosition(Vector2 screenPos)
    {
        menuPanel.position = screenPos;

        // 화면 밖 클램핑
        Canvas rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (rootCanvas == null) return;

        float scale = rootCanvas.scaleFactor;
        Vector2 menuSize = menuPanel.sizeDelta * scale;

        Vector3 pos = menuPanel.position;

        if (pos.x + menuSize.x > Screen.width)
            pos.x = Screen.width - menuSize.x;
        if (pos.y - menuSize.y < 0f)
            pos.y = menuSize.y;

        menuPanel.position = pos;
    }
}
