using UnityEngine;

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
