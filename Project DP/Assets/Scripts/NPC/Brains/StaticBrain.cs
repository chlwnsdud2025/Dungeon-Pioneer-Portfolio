using UnityEngine;

/// <summary>
/// 아무것도 하지 않는 브레인. 제자리에 서 있는 NPC용입니다.
///
/// '움직이지 않음'을 NpcActor의 bool 옵션이 아니라 별도 브레인으로 둔 이유:
/// 옵션은 두 개까지는 괜찮지만 세 번째(배회·순찰·추종·스케줄)가 오는 순간
/// if 분기가 얽힙니다. 브레인 교체는 종류가 늘어도 선형입니다.
///
/// 중력은 NpcActor가 목적지가 없는 프레임마다 적용하므로 여기서 할 일이 없습니다.
/// </summary>
public class StaticBrain : NpcBrain
{
    public override void Tick() { }
}
