using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 하나의 "장소" 인스턴스(던전 한 층, 마을, 오버월드 등)의 루트 오브젝트에 붙는 컴포넌트.
/// 어떤 LocationId인지, 그리고 이 장소 안에서 플레이어가 도착할 수 있는 스폰 지점들이 무엇인지를 들고 있습니다.
///
/// 던전은 DungeonGenerator가 인스턴스 생성 직후 자동으로 Entrance/Exit 태그를 스캔해 스폰 지점으로 등록합니다.
/// </summary>
public class LocationRoot : MonoBehaviour
{
    public LocationId Id { get; private set; }

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
