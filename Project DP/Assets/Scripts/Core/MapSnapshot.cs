using System.Collections.Generic;
using UnityEngine;

public struct FloorObjectState
{
    public bool flag;
    public Vector2Int cell;
}

public interface IFloorPersistent
{
    /// <summary>타입 구분. 같은 셀에 서로 다른 종류가 겹쳐도 기록이 섞이지 않게 합니다.</summary>
    string PersistentKind { get; }

    /// <summary>이 오브젝트를 식별하는 셀. 움직이는 오브젝트는 '원래' 셀이어야 합니다.</summary>
    Vector2Int PersistentCell { get; }

    /// <summary>
    /// 생성 직후 1회 호출. 자기 셀을 확정합니다.
    /// Awake가 아니라 이 시점인 이유: Instantiate 직후 NormalizeMapOrigin()이 맵 전체를 옮기므로
    /// Awake에서 좌표를 읽으면 정규화 '이전' 셀이 잡힙니다.
    /// </summary>
    void Latch();

    /// <summary>기본 상태와 달라 저장할 필요가 있을 때만 true.</summary>
    bool TryCapture(out FloorObjectState state);

    /// <summary>저장된 상태로 되돌립니다.</summary>
    void Restore(FloorObjectState state);
}

public struct GraveRecord
{
    public Vector2Int cell;
    public int variant;
    public Vector3 position;
    public Quaternion rotation;
}

public struct TorchRecord
{
    public Vector2Int wallCell;
    public Vector3Int faceKey;

    // 횃불은 던전 밖에 있는 동안에도 계속 탑니다. 기록 시각(Time.time)을 함께 담아두면 복원할 때
    // '기록 후 흐른 시간'을 빼서 남은 시간을 이어갈 수 있습니다.
    // 마감 시각을 그대로 넣지 않는 이유: Time.time은 앱을 켤 때마다 0부터라, 나중에 디스크 저장을
    // 얹을 때 capturedAt만 무시하면 남은 초를 그대로 쓸 수 있게 하기 위함입니다.
    public float remainingSeconds;
    public float capturedAt;
}

public class MapSnapshot
{
    /// <summary>(종류, 셀) → 상태. 기본값과 다른 오브젝트만 들어 있습니다.</summary>
    public readonly Dictionary<(string kind, Vector2Int cell), FloorObjectState> objects = new();

    public readonly List<TorchRecord> torches = new();

    /// <summary>사망 위치에 설치된 묘비. 같은 셀에는 하나만 기록됩니다.</summary>
    public readonly List<GraveRecord> graves = new();

    // 미니맵 탐색 기록. visited는 2차원 배열을 1차원으로 편 것.
    public bool[] minimapVisited;
    public int minimapVisitedCount;
    public Vector2Int minimapGridSize;
}
