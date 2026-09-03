using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 프로젝트 전역 입력 소유자. PlayerInputActions 인스턴스를 '단 하나' 만들어 들고,
/// 다른 스크립트는 각자 new 하지 않고 이걸 가져다 씁니다.
///  - Player : 이동/공격 등 게임플레이 (PlayerControll 맵)
///  - UI     : 인벤토리 토글 등 (UIControll 맵)
/// 전역 상주(MonoSingleton)라 씬을 넘어도 유지됩니다.
/// </summary>
public class InputManager : MonoSingleton<InputManager>
{
    public PlayerInputActions Actions { get; private set; }

    // 맵 바로 접근: 예) InputManager.Instance.Player.Movement.ReadValue<Vector2>()
    public PlayerInputActions.PlayerControllActions Player => Actions.PlayerControll;
    public PlayerInputActions.UIControllActions UI => Actions.UIControll;

    // 인벤토리 토글 이벤트 (HUD가 구독)
    public event Action OnInventoryToggle;

    // ESC 등 UI 취소 이벤트 (UIManager가 구독해서 맨 위 팝업을 닫습니다)
    public event Action OnUICancel;

    // 퀵슬롯 사용 이벤트. 인자는 슬롯 번호(0부터) — 단축키 1~4가 0~3에 대응합니다. (HUD가 구독)
    public event Action<int> OnQuickSlotUsed;

    // 퀵슬롯은 인덱스로 다루는 게 자연스러워, 생성 래퍼의 타입 프로퍼티 대신 이름으로 찾아 씁니다.
    // (에셋에 액션을 추가한 뒤 유니티가 래퍼를 재생성하기 전에도 컴파일·동작합니다)
    private InputAction[] _quickSlotActions;
    private Action<InputAction.CallbackContext>[] _quickSlotHandlers;

    protected override void Awake()
    {
        base.Awake();                 // MonoSingleton: Instance 설정 + DontDestroyOnLoad
        if (Instance != this) return; // 중복 인스턴스면 초기화 스킵

        Actions = new PlayerInputActions();
        Actions.UIControll.Inventory.performed += OnInventoryPerformed;   // 액션 → 이벤트 중계
        Actions.UIControll.Cancel.performed += OnCancelPerformed;
        SubscribeQuickSlots();
    }

    private void OnEnable() => Actions?.Enable();     // Awake가 먼저 도므로 Actions 존재
    private void OnDisable() => Actions?.Disable();

    private void OnDestroy()
    {
        if (Instance != this || Actions == null) return;
        Actions.UIControll.Inventory.performed -= OnInventoryPerformed;
        Actions.UIControll.Cancel.performed -= OnCancelPerformed;
        UnsubscribeQuickSlots();
        Actions.Dispose();
    }

    private void OnInventoryPerformed(InputAction.CallbackContext _) => OnInventoryToggle?.Invoke();
    private void OnCancelPerformed(InputAction.CallbackContext _) => OnUICancel?.Invoke();

    // ── 퀵슬롯 ───────────────────────────────────────────────

    private void SubscribeQuickSlots()
    {
        _quickSlotActions = new InputAction[QuickSlotHolder.SlotCount];
        _quickSlotHandlers = new Action<InputAction.CallbackContext>[QuickSlotHolder.SlotCount];

        for (int i = 0; i < QuickSlotHolder.SlotCount; i++)
        {
            string actionName = $"QuickSlot{i + 1}";
            InputAction action = Actions.FindAction(actionName);

            if (action == null)
            {
                Debug.LogWarning($"[InputManager] 입력 액션 '{actionName}'을 찾을 수 없어 {i + 1}번 퀵슬롯이 동작하지 않습니다. " +
                                 "PlayerInputActions 에셋의 PlayerControll 맵을 확인해주세요.");
                continue;
            }

            int index = i;   // 클로저가 루프 변수를 그대로 캡처하지 않도록 복사
            _quickSlotHandlers[i] = _ => OnQuickSlotUsed?.Invoke(index);
            action.performed += _quickSlotHandlers[i];
            _quickSlotActions[i] = action;
        }
    }

    private void UnsubscribeQuickSlots()
    {
        if (_quickSlotActions == null) return;

        for (int i = 0; i < _quickSlotActions.Length; i++)
            if (_quickSlotActions[i] != null && _quickSlotHandlers[i] != null)
                _quickSlotActions[i].performed -= _quickSlotHandlers[i];

        _quickSlotActions = null;
        _quickSlotHandlers = null;
    }

    /// <summary>게임플레이 조작 on/off. 인벤토리 열면 false로 이동/공격을 막는다 (UI 맵은 그대로 유지).</summary>
    public void SetGameplayEnabled(bool on)
    {
        if (Actions == null) return;
        if (on) Actions.PlayerControll.Enable();
        else Actions.PlayerControll.Disable();
    }
}
