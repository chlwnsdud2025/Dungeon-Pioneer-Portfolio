using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 맵 오브젝트 하나의 저장 상태. 타입마다 의미가 다릅니다.
///  · 상자     flag = 열림
///  · 바닥함정 flag = 메워짐
///  · Pushable flag = 잠김, cell = 현재 셀
/// </summary>
public struct FloorObjectState
{
    public bool flag;
    public Vector2Int cell;
}

/// <summary>
/// 던전을 나갔다 들어와도 상태가 유지되어야 하는 맵 오브젝트가 구현하는 인터페이스.
///
/// 새로운 상호작용 오브젝트(함정 파훼 장치 등)를 추가할 때 이 인터페이스만 구현하면
/// 저장·복원 시스템은 손대지 않아도 자동으로 대상에 포함됩니다.
///
/// 식별은 '셀 좌표'로 합니다. DungeonGenerator.NormalizeMapOrigin()이 맵을 GridSystem 원점에
/// 맞춰주기 때문에, 같은 프리팹은 언제 생성해도 오브젝트가 같은 셀에 놓입니다.
/// </summary>
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

/// <summary>플레이어 사망 위치에 설치된 묘비 하나의 기록.</summary>
public struct GraveRecord
{
    public Vector2Int cell;
    public int variant;
    public Vector3 position;
    public Quaternion rotation;
}

/// <summary>설치된 횃불 하나의 기록.</summary>
public struct TorchRecord
{
    public Vector3 position;
    public Quaternion rotation;

    // 위치·회전만으로도 다시 세울 수 있지만, WallObject.torchFaces까지 복원해야
    // 복원된 횃불이 붙은 면에 또 설치되는 걸 막을 수 있습니다.
    public Vector2Int wallCell;
    public Vector3Int faceKey;
}

/// <summary>맵 하나(프리팹 하나)의 상호작용 스냅샷. 순수 데이터라 나중에 직렬화해 디스크에 얹기 쉽습니다.</summary>
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
