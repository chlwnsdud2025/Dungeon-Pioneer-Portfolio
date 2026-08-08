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

    protected override void Awake()
    {
        base.Awake();                 // MonoSingleton: Instance 설정 + DontDestroyOnLoad
        if (Instance != this) return; // 중복 인스턴스면 초기화 스킵

        Actions = new PlayerInputActions();
        Actions.UIControll.Inventory.performed += OnInventoryPerformed;   // 액션 → 이벤트 중계
    }

    private void OnEnable() => Actions?.Enable();     // Awake가 먼저 도므로 Actions 존재
    private void OnDisable() => Actions?.Disable();

    private void OnDestroy()
    {
        if (Instance != this || Actions == null) return;
        Actions.UIControll.Inventory.performed -= OnInventoryPerformed;
        Actions.Dispose();
    }

    private void OnInventoryPerformed(InputAction.CallbackContext _) => OnInventoryToggle?.Invoke();

    /// <summary>게임플레이 조작 on/off. 인벤토리 열면 false로 이동/공격을 막는다 (UI 맵은 그대로 유지).</summary>
    public void SetGameplayEnabled(bool on)
    {
        if (Actions == null) return;
        if (on) Actions.PlayerControll.Enable();
        else Actions.PlayerControll.Disable();
    }
}