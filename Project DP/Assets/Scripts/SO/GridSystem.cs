using UnityEngine;

/// <summary>
/// 게임 전체(던전 맵, 이후 필드 맵 등)에서 공통으로 사용되는 "단 하나의" 타일 그리드 규칙을 정의하는 ScriptableObject.
///
/// ★ 기존 문제: MapGenerator(Unity Grid 컴포넌트), MapBuilder/MapBuilderEditor(public float tileSize),
///   MinimapManager(public float tileSize)가 각자 따로 타일 크기를 들고 있어서
///   한쪽 값만 바꾸면 나머지가 다 어긋났습니다.
/// ★ 개선: 이제부터는 이 GridSystem 애셋 하나만 수정하면
///   맵 생성 / 에디터 배치 / 미니맵 / 상호작용 판정이 전부 동기화됩니다.
///
/// ── 사용법 ──
/// 1. 프로젝트에 "Resources" 라는 이름의 폴더를 만드세요. (예: Assets/Resources)
/// 2. 유니티 메뉴 [Assets > Create > Dungeon > Grid System] 으로 애셋을 하나 생성하고,
///    반드시 파일명을 "GridSystem" 으로 하여 Resources 폴더 안에 저장하세요.
///    (예: Assets/Resources/GridSystem.asset)
/// 3. 이후에는 코드 어디서든 GridSystem.Instance.TileSize 처럼 접근하면 됩니다.
///    (씬에 오브젝트로 배치할 필요 없음, 인스펙터 참조 연결도 필요 없음)
/// 4. 나중에 타일 크기를 3x3에서 다른 크기로 바꾸고 싶다면,
///    이 애셋의 Tile Size 값 하나만 바꾸면 던전/에디터/미니맵이 전부 같이 바뀝니다.
/// </summary>
[CreateAssetMenu(fileName = "GridSystem", menuName = "Dungeon/Grid System", order = 0)]
public class GridSystem : ScriptableObject
{
    private const string ResourcePath = "GridSystem"; // Resources/GridSystem.asset

    private static GridSystem _instance;

    public static GridSystem Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<GridSystem>(ResourcePath);

                if (_instance == null)
                {
                    // 애셋이 아직 없다면 임시 인스턴스를 메모리에서만 만들어 최소한 게임이 죽지 않게 함.
                    // (저장되지는 않으니, 아래 경고를 보면 반드시 Resources 폴더에 실제 애셋을 만들어주세요)
                    _instance = CreateInstance<GridSystem>();
                    Debug.LogWarning("[GridSystem] Resources 폴더에 GridSystem.asset이 없습니다! " +
                        "임시 기본값(tileSize=3)으로 동작합니다. " +
                        "[Assets > Create > Dungeon > Grid System] 메뉴로 애셋을 만들어 " +
                        "Assets/Resources/GridSystem.asset 경로에 저장해주세요.");
                }
            }
            return _instance;
        }
    }

    [Header("전역 타일 설정 (이 값 하나만 바꾸면 게임 전체에 반영됩니다)")]
    [Tooltip("타일 한 칸의 가로/세로(X, Z) 크기. 프로젝트 규격은 2입니다.")]
    [SerializeField] private float tileSize = 2f;

    [Tooltip("층(높이) 한 칸의 크기. 타일이 2×2×2 규격이므로 tileSize와 동일하게 둡니다.")]
    [SerializeField] private float tileHeight = 2f;

    [Tooltip("그리드 좌표 (0,0)이 월드 공간 어디에 위치하는지")]
    [SerializeField] private Vector3 origin = Vector3.zero;

    // ★ 프로젝트의 모든 타일은 2×2×2 규격입니다. 런타임에 맵마다 간격을 추론해 덮어쓰던 구조를
    //   걷어내고, 이 애셋 값 하나만을 단일 진실 공급원으로 삼습니다.
    public float TileSize => tileSize;
    public float TileHeight => tileHeight;
    public Vector3 Origin => origin;

    // ── 좌표 변환 유틸 ──────────────────────────────────────────

    /// <summary>
    /// 그리드 셀 좌표(Vector3Int: x, y=층, z) → 월드 좌표 (셀의 바닥 중심).
    /// 기존 Unity Grid.GetCellCenterWorld와 이름/사용법을 동일하게 맞춰서 마이그레이션이 쉽도록 함.
    /// </summary>
    public Vector3 GetCellCenterWorld(Vector3Int cell)
    {
        return Origin + new Vector3(cell.x * TileSize, cell.y * TileHeight, cell.z * TileSize);
    }

    /// <summary>셀의 바닥 기준에서 한 칸 위로 솟아오른 위치. (벽/상자/적처럼 바닥 위에 놓이는 오브젝트용)</summary>
    public Vector3 GetElevatedWorld(Vector3Int cell)
    {
        return GetCellCenterWorld(cell) + new Vector3(0f, TileHeight, 0f);
    }

    /// <summary>월드 좌표 → 그리드 셀 좌표(반올림 변환)</summary>
    public Vector3Int WorldToCell(Vector3 worldPos)
    {
        Vector3 local = worldPos - Origin;
        int x = Mathf.RoundToInt(local.x / TileSize);
        int y = Mathf.RoundToInt(local.y / TileHeight);
        int z = Mathf.RoundToInt(local.z / TileSize);
        return new Vector3Int(x, y, z);
    }

    /// <summary>두 월드 좌표가 같은 그리드 칸에 속하는지 (미니맵/함정/상호작용 판정에 사용)</summary>
    public bool IsSameCell(Vector3 worldA, Vector3 worldB)
    {
        return WorldToCell(worldA) == WorldToCell(worldB);
    }
}