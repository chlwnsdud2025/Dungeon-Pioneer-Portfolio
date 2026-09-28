using UnityEngine;

/// <summary>
/// NPC가 플레이어에게 제공하는 능력 하나. 대화·상점·퀘스트가 각각 하나씩입니다.
///
/// 이 클래스는 일부러 IInteractable이 아닙니다. 한 NPC에 모듈이 둘 이상 붙으면
/// PlayerController의 GetComponentInParent&lt;IInteractable&gt;()가 어느 것을 집을지 알 수 없어,
/// '가끔 상점 대신 대화가 열리는' 식의 조용한 버그가 됩니다.
/// 플레이어가 상대하는 창구는 NpcActor 하나뿐이고, NpcActor가 우선순위로 골라 넘겨줍니다.
/// </summary>
[RequireComponent(typeof(NpcActor))]
public abstract class NpcInteractionModule : MonoBehaviour
{
    [Tooltip("모듈이 여럿일 때 높은 쪽이 F키를 가져갑니다.")]
    [SerializeField] private int priority = 0;

    public int Priority => priority;

    /// <summary>플레이어와 상호작용이 진행 중인지(대화 중, 상점 창이 열림 등). true인 동안 브레인이 이동을 멈춥니다.</summary>
    public virtual bool IsEngaged => false;

    /// <summary>지금 이 모듈을 쓸 수 있는지. false면 NpcActor가 다음 순위 모듈을 봅니다.</summary>
    public abstract bool CanInteract(PlayerController player);

    /// <summary>F키가 이 모듈로 왔을 때 실행됩니다. 대화처럼 여러 번 눌리는 모듈도 있습니다.</summary>
    public abstract void Interact(PlayerController player);
}
