using System.Collections.Generic;

/// <summary>
/// 활성 NPC 목록. EnemyController.ActiveEnemies와 같은 패턴입니다.
///
/// 적·NPC가 서로를 탐지할 때 씬 전체를 FindObjectsOfType으로 훑는 것을 피하려고
/// 미리 만들어 둡니다. OnEnable/OnDisable로만 관리하므로 층이 비활성화되면 자동으로 빠집니다.
/// </summary>
public static class NpcRegistry
{
    private static readonly HashSet<NpcActor> active = new HashSet<NpcActor>();

    public static IEnumerable<NpcActor> Active => active;
    public static int Count => active.Count;

    public static void Add(NpcActor npc)
    {
        if (npc != null) active.Add(npc);
    }

    public static void Remove(NpcActor npc)
    {
        if (npc != null) active.Remove(npc);
    }
}
