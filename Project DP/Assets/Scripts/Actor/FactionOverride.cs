using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 개체 단위 진영 예외. "이 NPC만 플레이어를 적대한다" 같은 비대칭 관계를 표현합니다.
/// 붙어 있지 않으면 FactionTable의 기본 관계가 그대로 쓰입니다.
///
/// 우선순위: 개체 지정 &gt; 진영 지정 &gt; FactionTable 기본값.
/// </summary>
[DisallowMultipleComponent]
public class FactionOverride : MonoBehaviour
{
    [Serializable]
    public struct FactionEntry
    {
        public FactionId faction;
        public Stance stance;
    }

    [Tooltip("이 액터가 특정 진영을 보는 태도. 인스펙터에서 미리 정해 두는 예외입니다. " +
             "예: 배신한 모험가 프리팹에 Player=Hostile을 넣어 두기.")]
    [SerializeField] private List<FactionEntry> factionEntries = new List<FactionEntry>();

    // 런타임 예외: 특정 개체 하나만 대상으로 합니다. (나를 먼저 때린 상대 등)
    private readonly Dictionary<IFactionMember, Stance> memberEntries = new Dictionary<IFactionMember, Stance>();

    public bool TryGetStanceToward(IFactionMember subject, out Stance stance)
    {
        stance = Stance.Neutral;
        if (subject == null) return false;

        if (memberEntries.TryGetValue(subject, out stance)) return true;

        for (int i = 0; i < factionEntries.Count; i++)
        {
            if (factionEntries[i].faction != subject.Faction) continue;

            stance = factionEntries[i].stance;
            return true;
        }

        return false;
    }

    /// <summary>특정 개체만 이렇게 보도록 런타임에 지정합니다. (선빵을 맞아 적대로 돌아서는 경우 등)</summary>
    public void SetStanceToward(IFactionMember subject, Stance stance)
    {
        if (subject == null) return;
        memberEntries[subject] = stance;
    }

    /// <summary>
    /// 런타임 개체 예외를 모두 지웁니다.
    /// 개체를 키로 들고 있어 파괴된 대상의 항목이 남을 수 있으므로,
    /// 런을 새로 시작할 때 불러주는 것이 좋습니다.
    /// </summary>
    public void ClearRuntimeOverrides() => memberEntries.Clear();
}
