using System.Collections.Generic;

/// <summary>
/// 던전 씬을 나갔다 들어와도 유지되어야 하는 상호작용 기록을 담는 전역 저장소.
/// SceneTransition·RegionState와 같은 정적 홀더 패턴이라 씬 오브젝트가 필요 없습니다.
///
/// ★ 키가 '층 번호'가 아니라 '맵 프리팹 이름'입니다.
///   층 배정은 입장할 때마다 다시 셔플되므로(DungeonGenerator.PrepareNewRun), 상태를 층에 묶으면
///   1층에서 연 상자 기록이 다음 입장의 전혀 다른 맵에 적용됩니다. 맵에 묶어두면 그 맵이 몇 층에
///   나오든 자기 상태를 그대로 들고 갑니다.
///
/// 지금은 메모리에만 있어 앱을 껐다 켜면 사라집니다. MapSnapshot이 순수 데이터라 나중에
/// 디스크 저장을 얹기는 쉽습니다.
/// </summary>
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

    /// <summary>기록이 남아 있는 맵 수. (디버그용)</summary>
    public static int SavedMapCount => maps.Count;
}
