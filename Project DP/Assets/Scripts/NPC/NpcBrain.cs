using UnityEngine;

/// <summary>
/// NPC 한 명의 두뇌. NpcActor당 정확히 하나만 붙입니다.
///
/// 몸(NpcActor)과 분리한 이유: 상인·경비병·모험가는 이동·피격·애니메이션이 완전히 같고
/// '무엇을 할지'만 다릅니다. 그 차이만 이 클래스로 갈아끼웁니다.
///
/// 파생 클래스가 Awake를 재정의한다면 반드시 base.Awake()를 불러야 actor가 채워집니다.
/// </summary>
[RequireComponent(typeof(NpcActor))]
public abstract class NpcBrain : MonoBehaviour
{
    protected NpcActor actor;

    protected virtual void Awake()
    {
        actor = GetComponent<NpcActor>();
    }

    /// <summary>
    /// 장소 준비 완료 시 호출됩니다. 던전이면 층에 들어설 때마다, 마을이면 씬 시작에 한 번.
    /// 여러 번 불릴 수 있으니 상태를 초기화하는 용도로 쓰세요.
    /// </summary>
    public virtual void OnLocationReady(NpcActor owner) => actor = owner;

    /// <summary>매 프레임. NpcActor.Update가 불러줍니다.</summary>
    public abstract void Tick();

    /// <summary>전투불능이 됐을 때. 진행 중인 행동을 정리합니다.</summary>
    public virtual void OnDowned() { }
}
