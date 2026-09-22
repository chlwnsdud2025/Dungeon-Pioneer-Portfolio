using System.Collections.Generic;

public static class DungeonRunState
{
    private static readonly Dictionary<string, MapSnapshot> maps = new();

    /// <summary>해당 맵의 스냅샷을 가져옵니다. 없으면 null.</summary>
    public static MapSnapshot Get(string mapId)
    {
        if (string.IsNullOrEmpty(mapId)) return null;
        return maps.TryGetValue(mapId, out MapSnapshot snapshot) ? snapshot : null;
    }

    /// <summary>해당 맵의 스냅샷을 저장(덮어쓰기)합니다.</summary>
    public static void Save(string mapId, MapSnapshot snapshot)
    {
        if (string.IsNullOrEmpty(mapId) || snapshot == null) return;
        maps[mapId] = snapshot;
    }

    /// <summary>모든 진행 기록을 지웁니다. 다음 입장은 완전히 새 던전이 됩니다.</summary>
    public static void Clear() => maps.Clear();

    /// <summary>저장된 모든 맵에서 묘비 기록만 지웁니다. (새 던전 입장 시 호출)</summary>
    public static void ClearGraves()
    {
        foreach (MapSnapshot snapshot in maps.Values)
            snapshot.graves.Clear();
    }

    /// <summary>기록이 남아 있는 맵 수. (디버그용)</summary>
    public static int SavedMapCount => maps.Count;
}
