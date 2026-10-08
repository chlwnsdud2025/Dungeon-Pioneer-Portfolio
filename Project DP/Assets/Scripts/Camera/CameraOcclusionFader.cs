using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카메라와 플레이어 사이에서 플레이어 주변을 가리는 벽을 서서히 비치게 만듭니다.
///
/// 레이를 쏘지 않고 격자 칸으로 판단합니다. 플레이어 칸에서 카메라 쪽으로 앞 regionDepth줄,
/// 좌우 regionHalfWidth칸 안의 벽을 모두 투명하게 합니다 (WallOcclusionRegion).
/// 레이 하나로 판단하면 카메라가 높아 선이 벽 위로 지나가서, 플레이어에 바짝 붙은 벽만 잡히고 주변이 답답했습니다.
///
/// 벽은 층마다 칸별 목록(WallCellIndex)으로 한 번 모아 두고, 플레이어 칸이나 카메라 방향이 바뀔 때만 다시 고릅니다.
/// 투명해지는 동작 자체는 FadeableWall이 맡습니다.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraOcclusionFader : MonoBehaviour
{
    [Header("추적 대상")]
    [Tooltip("비우면 같은 오브젝트의 CameraFollow.target을 자동 사용")]
    public Transform target;

    [Header("가림 영역 (격자 칸)")]
    [Tooltip("플레이어 칸에서 카메라 쪽으로 몇 줄까지 투명하게 할지. 플레이어가 서 있는 줄은 넣지 않습니다.")]
    [Min(0)] public int regionDepth = 3;
    [Tooltip("줄마다 플레이어 좌우로 몇 칸까지 투명하게 할지. 2면 가운데 포함 5칸입니다.")]
    [Min(0)] public int regionHalfWidth = 2;

    [System.Serializable]
    public class SeeThroughShader
    {
        [Tooltip("벽의 원래 셰이더")]
        public Shader source;
        [Tooltip("그 셰이더를 복사해 반투명(알파 = 1 - _DitherFade)으로 바꾼 셰이더.")]
        public Shader seeThrough;
    }

    [Header("벽 비치기")]
    [Tooltip("원래 셰이더별 반투명용 셰이더. 던전 벽(TEST_TRIPLANAR_LIT) → WallSeeThrough")]
    public SeeThroughShader[] seeThroughShaders;
    [Tooltip("위 목록에 없는 셰이더의 벽에 쓸 반투명용 셰이더 (예전 WallDither). 원래 벽과 모양이 달라 바꿔 끼울 때 티가 납니다. " +
             "비우면 그런 벽은 비치지 않습니다.")]
    [SerializeField] private Shader ditherShader;
    [Tooltip("가릴 때 벽을 얼마나 투명하게 할지 (0=그대로, 0.5=반투명, 1=완전히 투명).")]
    [Range(0f, 1f)] public float occludedFade = 0.5f;
    [Tooltip("벽이 비치기까지 걸리는 시간 (초)")]
    [Min(0.01f)] public float disappearSeconds = 0.35f;
    [Tooltip("벽이 다시 나타나기까지 걸리는 시간 (초)")]
    [Min(0.01f)] public float reappearSeconds = 0.5f;
    [Tooltip("비칠 때 벽 셰이더의 오버레이 무늬(바둑판 등, _OverlayMix)를 비치는 정도에 맞춰 옅게 합니다. " +
             "반투명이 되면 뒤 배경과 대비돼 무늬가 도드라지기 때문입니다. 원래 벽 머티리얼은 바꾸지 않습니다.")]
    public bool hideOverlayWhenSeeThrough = true;

    private CameraFollow camFollow;

    // 층마다 한 번 만든 벽 목록. 층 인스턴스는 다른 층으로 가도 꺼진 채 남아 있으므로 계속 씁니다.
    private readonly Dictionary<GameObject, WallCellIndex> indices = new Dictionary<GameObject, WallCellIndex>();
    private WallCellIndex currentIndex;

    private readonly List<Vector2Int> regionCells = new List<Vector2Int>();
    private readonly HashSet<FadeableWall> fadedWalls = new HashSet<FadeableWall>();
    private readonly HashSet<FadeableWall> nextWalls = new HashSet<FadeableWall>();

    // 지난번에 고른 조건. 같으면 다시 고르지 않습니다.
    private bool hasRegion;
    private Vector2Int lastCell, lastToward;

    private void Awake() => camFollow = GetComponent<CameraFollow>();

    private void OnValidate() => hasRegion = false;   // 플레이 중 인스펙터 값을 바꾸면 바로 다시 고름

    private Transform ResolveTarget()
    {
        if (target != null) return target;
        return camFollow != null ? camFollow.target : null;
    }

    private void LateUpdate()
    {
        SyncLocation();

        Transform t = ResolveTarget();
        if (t == null || currentIndex == null)
        {
            ClearFaded();
            return;
        }

        Vector2Int cell = LocationGrid.WorldToCell(t.position);
        Vector2Int toward = WallOcclusionRegion.TowardCamera(transform.forward);
        if (hasRegion && cell == lastCell && toward == lastToward) return;

        lastCell = cell;
        lastToward = toward;
        hasRegion = true;

        WallOcclusionRegion.Collect(cell, toward, regionDepth, regionHalfWidth, regionCells);

        nextWalls.Clear();
        foreach (Vector2Int regionCell in regionCells)
        {
            foreach (Transform wallTransform in currentIndex.Get(regionCell))
            {
                if (wallTransform == null) continue;

                FadeableWall wall = GetOrAddFadeable(wallTransform);
                wall.SetOccluded(true, occludedFade, disappearSeconds);
                nextWalls.Add(wall);
            }
        }

        // 영역에서 빠진 벽 → 다시 불투명(0)으로
        foreach (FadeableWall wall in fadedWalls)
        {
            if (wall != null && !nextWalls.Contains(wall)) wall.SetOccluded(false, occludedFade, reappearSeconds);
        }

        fadedWalls.Clear();
        fadedWalls.UnionWith(nextWalls);
    }

    /// <summary>지금 층이 바뀌었으면 그 층의 벽 목록으로 갈아탑니다. 이전 층의 투명한 벽은 바로 되돌립니다.</summary>
    private void SyncLocation()
    {
        GameObject location = DungeonManager.Instance != null ? DungeonManager.Instance.CurrentLocationInstance : null;

        // 카메라는 씬을 넘어 유지되므로, 씬이 바뀌어 층이 통째로 사라진 경우를 먼저 정리합니다.
        if (currentIndex != null && currentIndex.Location == null)
        {
            currentIndex = null;
            fadedWalls.Clear();
            hasRegion = false;
        }

        if (currentIndex != null && currentIndex.Location == location) return;
        if (currentIndex == null && location == null) return;

        // 꺼진 층에서는 FadeableWall의 Update가 돌지 않아 서서히 돌아올 수 없으므로 즉시 되돌립니다.
        foreach (FadeableWall wall in fadedWalls)
        {
            if (wall != null) wall.ResetToOpaque();
        }
        fadedWalls.Clear();
        hasRegion = false;

        if (location == null)
        {
            currentIndex = null;
            return;
        }

        RemoveDestroyedLocations();
        if (!indices.TryGetValue(location, out currentIndex))
        {
            currentIndex = WallCellIndex.Build(location);
            indices[location] = currentIndex;
        }
    }

    private void RemoveDestroyedLocations()
    {
        List<GameObject> destroyed = null;
        foreach (GameObject location in indices.Keys)
        {
            if (location != null) continue;
            destroyed ??= new List<GameObject>();
            destroyed.Add(location);
        }

        if (destroyed == null) return;
        foreach (GameObject location in destroyed) indices.Remove(location);
    }

    private void ClearFaded()
    {
        if (fadedWalls.Count == 0) return;

        foreach (FadeableWall wall in fadedWalls)
        {
            if (wall != null) wall.SetOccluded(false, occludedFade, reappearSeconds);
        }
        fadedWalls.Clear();
        hasRegion = false;
    }

    // 벽 수백 개에 미리 붙이지 않고, 처음 투명해질 때 붙입니다.
    private FadeableWall GetOrAddFadeable(Transform wallTransform)
    {
        FadeableWall wall = wallTransform.GetComponent<FadeableWall>();
        if (wall != null) return wall;

        wall = wallTransform.gameObject.AddComponent<FadeableWall>();
        wall.Init(SeeThroughShaderFor, hideOverlayWhenSeeThrough);   // ★ 최초 1회 반투명용 셰이더 고르는 방법 전달
        return wall;
    }

    private Shader SeeThroughShaderFor(Shader source)
    {
        if (seeThroughShaders != null)
        {
            foreach (SeeThroughShader pair in seeThroughShaders)
            {
                if (pair != null && pair.source == source && pair.seeThrough != null) return pair.seeThrough;
            }
        }

        return ditherShader;
    }

    // 카메라를 선택하면 지금 투명하게 고른 영역을 씬 뷰에 보여줍니다. (값 조절용)
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !hasRegion) return;

        float size = GridSystem.Instance.TileSize;
        Gizmos.color = new Color(0.1f, 0.8f, 0.6f, 0.9f);
        foreach (Vector2Int cell in regionCells)
            Gizmos.DrawWireCube(LocationGrid.CellToWorld(cell) + Vector3.up * (size * 0.5f), new Vector3(size, size, size) * 0.95f);
    }
}
