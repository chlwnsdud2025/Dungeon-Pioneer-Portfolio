using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬에 직접 놓인 맵(오버월드·마을)을 장소로 만듭니다. 던전의 DungeonGenerator가 하는 일의 씬 버전입니다.
/// 맵 루트에 붙이면 LocationRoot와 그리드를 만들고, 씬 안의 적·NPC를 깨웁니다.
///
/// 실행 순서를 앞당기는 이유: 그리드는 Awake에서 만들고 액터는 Start에서 깨웁니다.
/// NpcActor.Start는 아직 준비 알림을 못 받았으면 스스로 준비하는데, 그보다 먼저 그리드가 있어야
/// NPC가 Grid == null을 받지 않습니다.
///
/// 던전처럼 맵을 원점으로 옮기지 않습니다. 씬 맵은 맵 밖에 포탈·스폰 앵커·NPC가 따로 놓여 있어서,
/// 맵만 옮기면 서로 어긋납니다. 대신 원점 규칙을 어기면 에러를 냅니다.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class SceneLocation : MonoBehaviour
{
    private static readonly List<SceneLocation> active = new List<SceneLocation>();

    [SerializeField] private LocationType type = LocationType.Overworld;
    [SerializeField, Min(0)] private int index;

    [Header("디버그")]
    [Tooltip("이 오브젝트를 선택했을 때 씬 뷰에 그리드를 그립니다. 플레이 전에는 미리보기로 계산합니다.")]
    [SerializeField] private bool drawGrid = true;

    public LocationRoot Root { get; private set; }

    /// <summary>
    /// 맵의 자식이 아닌 액터(씬 최상단에 놓인 NPC 등)가 장소를 찾을 때 씁니다.
    /// 던전에는 SceneLocation이 없으므로 던전 액터의 동작은 바뀌지 않습니다.
    /// </summary>
    public static LocationRoot FindInScene(Scene scene)
    {
        foreach (SceneLocation location in active)
        {
            if (location != null && location.gameObject.scene == scene)
                return location.Root;
        }

        return null;
    }

    private void Awake()
    {
        Root = GetComponent<LocationRoot>();
        if (Root == null) Root = gameObject.AddComponent<LocationRoot>();

        Root.Initialize(new LocationId(type, index));
        Root.SetMapId(name);

        ValidateOrigin();
        Root.SetGrid(BuildGrid());
    }

    private void OnEnable()
    {
        if (FindInScene(gameObject.scene) != null)
            Debug.LogError($"[SceneLocation] '{gameObject.scene.name}' 씬에 SceneLocation이 둘 이상입니다. " +
                           "맵 밖 액터가 어느 장소에 속하는지 알 수 없게 됩니다.", this);

        active.Add(this);
    }

    private void OnDisable() => active.Remove(this);

    private void Start() => NotifyActorsLocationReady();

    private LocationGrid BuildGrid()
    {
        LocationGrid grid = LocationGridBuilder.Build(gameObject);

        // 언덕·나무 같은 소품은 맵의 자식이 아니라 씬 최상단에 놓이는 경우가 많아 씬 전체에서 모읍니다.
        LocationGridBuilder.ApplyBlockers(grid, CollectSceneComponents<GridBlocker>(false));
        return grid;
    }

    private void ValidateOrigin()
    {
        if (!LocationGridBuilder.TryGetFloorMinCell(gameObject, out Vector2Int min))
        {
            Debug.LogWarning($"[SceneLocation] '{name}'에서 바닥 타일(Floor/Entrance/Exit/Trap 태그)을 찾지 못했습니다. " +
                             "그리드가 비어 NPC가 움직이지 않습니다.", this);
            return;
        }

        if (min.x < 0 || min.y < 0)
        {
            Debug.LogError($"[SceneLocation] '{name}'의 바닥이 음수 칸 {min}까지 있습니다. " +
                           "음수 칸은 그리드에서 빠져 NPC가 그쪽으로 가지 못합니다. 바닥이 (0,0) 칸부터 시작하도록 맵을 옮겨주세요.", this);
        }
        else if (min != Vector2Int.zero)
        {
            Debug.LogWarning($"[SceneLocation] '{name}'의 바닥이 (0,0)이 아니라 {min} 칸에서 시작합니다. " +
                             "동작은 하지만 앞쪽 칸만큼 그리드가 낭비됩니다.", this);
        }
    }

    /// <summary>
    /// 켜져 있는 액터만 깨웁니다. 꺼진 NpcActor는 Awake가 돌지 않아 이동기가 없으므로,
    /// 나중에 켜질 때 NpcActor.Start가 스스로 준비합니다.
    /// </summary>
    private void NotifyActorsLocationReady()
    {
        foreach (EnemyController enemy in CollectSceneComponents<EnemyController>(false))
            enemy.NotifyLocationReady();

        foreach (NpcActor npc in CollectSceneComponents<NpcActor>(false))
            npc.NotifyLocationReady();
    }

    private List<T> CollectSceneComponents<T>(bool includeInactive) where T : Component
    {
        List<T> result = new List<T>();
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            result.AddRange(root.GetComponentsInChildren<T>(includeInactive));

        return result;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGrid) return;

        LocationGrid grid = Application.isPlaying && Root != null ? Root.Grid : BuildGrid();
        if (grid == null) return;

        float size = GridSystem.Instance.TileSize * 0.9f;
        float y = transform.position.y + 0.05f;
        Vector3 cellSize = new Vector3(size, 0.02f, size);

        for (int x = 0; x < grid.Size.x; x++)
        {
            for (int z = 0; z < grid.Size.y; z++)
            {
                CellType cellType = grid.Cells[x, z];
                bool blocked = grid.StaticBlockers[x, z];
                if (cellType == CellType.None && !blocked) continue;

                if (blocked) Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.45f);
                else if (cellType == CellType.Wall) Gizmos.color = new Color(0.4f, 0.4f, 0.4f, 0.45f);
                else if (LocationGrid.IsTrapTerrain(cellType)) Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.45f);
                else Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.3f);

                Vector3 center = LocationGrid.CellToWorld(new Vector2Int(x, z));
                center.y = y;
                Gizmos.DrawCube(center, cellSize);
            }
        }
    }
}
