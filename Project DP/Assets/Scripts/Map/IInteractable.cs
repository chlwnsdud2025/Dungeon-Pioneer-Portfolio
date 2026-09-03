using UnityEngine;

/// <summary>
/// 플레이어가 F키(Interact)로 상호작용할 수 있는 오브젝트의 공통 인터페이스.
///
/// PlayerController.UpdateFocus()가 전방 SphereCast로 맞은 콜라이더에서
/// GetComponentInParent&lt;IInteractable&gt;()로 이 인터페이스를 찾아 포커스를 잡고,
/// PlayerBaseState.OnInteractInput()이 CanInteract → Interact 순서로 호출합니다.
/// 새 상호작용 오브젝트(문, 레버, NPC 등)는 이 인터페이스만 구현하면
/// 플레이어 쪽 코드를 고치지 않고 추가됩니다.
///
/// 상태 전환이 필요한 상호작용(상자 열기, 밀기)은 Interact() 안에서
/// player.stateMachine.ChangeState&lt;...&gt;()를 직접 호출합니다. 그 상태의 Enter()는
/// player.focusedObject(이 컴포넌트의 transform)로 대상을 다시 찾습니다.
/// </summary>
public interface IInteractable
{
    /// <summary>구현체가 MonoBehaviour라면 Component.transform으로 자동 충족됩니다.</summary>
    Transform transform { get; }

    /// <summary>
    /// 지금 이 오브젝트와 상호작용이 가능한지. (예: 이미 열린 상자 = false, 잠긴 밀기 오브젝트 = false)
    /// false면 F키 입력이 조용히 무시됩니다. 추후 상호작용 UI 프롬프트 표시 여부에도 쓸 수 있습니다.
    /// </summary>
    bool CanInteract(PlayerController player);

    /// <summary>F키 입력 시 실행. CanInteract가 true일 때만 호출됩니다.</summary>
    void Interact(PlayerController player);
}
