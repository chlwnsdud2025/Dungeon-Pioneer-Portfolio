using System.Collections.Generic;
using UnityEngine;

public class LocationRoot : MonoBehaviour
{
    public LocationId Id { get; private set; }

    /// <summary>
    /// 이 장소를 만든 맵 프리팹의 이름. 상호작용 기록을 '몇 층'이 아니라 '어느 맵'에 묶기 위한 키입니다.
    /// 층 배정은 입장할 때마다 다시 셔플되므로, 같은 맵이 몇 층에 나오든 자기 상태를 들고 다니게 합니다.
    /// </summary>
    public string MapId { get; private set; }

    public void SetMapId(string mapId) => MapId = mapId;

    public LocationGrid Grid { get; private set; }

    public void SetGrid(LocationGrid grid) => Grid = grid;

    private readonly Dictionary<string, Transform> spawnPoints = new Dictionary<string, Transform>();

    public void Initialize(LocationId id)
    {
        Id = id;
    }

    public void RegisterSpawnPoint(string pointId, Transform point)
    {
        if (string.IsNullOrEmpty(pointId) || point == null) return;
        spawnPoints[pointId] = point;
    }

    public Transform GetSpawnPoint(string pointId)
    {
        if (string.IsNullOrEmpty(pointId)) return null;
        return spawnPoints.TryGetValue(pointId, out Transform point) ? point : null;
    }
}
