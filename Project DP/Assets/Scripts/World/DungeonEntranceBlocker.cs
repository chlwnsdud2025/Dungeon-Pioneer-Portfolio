using UnityEngine;

public enum DungeonEntranceOpenSide
{
    Forward,
    Back,
    Right,
    Left
}

public class DungeonEntranceBlocker : MonoBehaviour
{
    private const float MinWallSize = 0.001f;

    private static readonly string[] WallNames =
    {
        "Wall_Back",
        "Wall_Top",
        "Wall_Bottom",
        "Wall_Left",
        "Wall_Right"
    };

    [Header("Block Bounds (local space)")]
    [SerializeField] private Vector3 boundsSize = Vector3.one;
    [SerializeField] private Vector3 boundsCenter = Vector3.zero;

    [Header("Opening")]
    [SerializeField] private DungeonEntranceOpenSide openSide = DungeonEntranceOpenSide.Forward;
    [SerializeField] private Vector2 openingSize = new Vector2(0.7f, 0.7f);
    [SerializeField] private Vector2 openingCenter = Vector2.zero;

    [Header("Wall")]
    [SerializeField] private float wallThickness = 0.05f;

    private void Awake()
    {
        ApplyBlockers();
    }

    private void Reset()
    {
        ApplyBlockers();
    }

    private void OnValidate()
    {
        wallThickness = Mathf.Max(wallThickness, MinWallSize);
        // 개구부가 너무 작으면 CharacterController가 통과할 수 없음 (실수로 0에 가깝게 두면 입구가 막힘)
        openingSize.x = Mathf.Max(openingSize.x, 0.5f);
        openingSize.y = Mathf.Max(openingSize.y, 0.5f);
        ApplyBlockers();
    }

    private void ApplyBlockers()
    {
        Vector3 half = boundsSize * 0.5f;
        Vector3 min = boundsCenter - half;
        Vector3 max = boundsCenter + half;
        float t = wallThickness;

        float openMinU = openingCenter.x - openingSize.x * 0.5f;
        float openMaxU = openingCenter.x + openingSize.x * 0.5f;
        float openMinV = openingCenter.y - openingSize.y * 0.5f;
        float openMaxV = openingCenter.y + openingSize.y * 0.5f;

        GetAxisMapping(out int uAxis, out int vAxis, out int depthAxis, out float depthSign);

        float minU = GetAxis(min, uAxis);
        float maxU = GetAxis(max, uAxis);
        float minV = GetAxis(min, vAxis);
        float maxV = GetAxis(max, vAxis);
        float minD = GetAxis(min, depthAxis);
        float maxD = GetAxis(max, depthAxis);

        float openFaceD = depthSign > 0f ? maxD : minD;
        float backFaceD = depthSign > 0f ? minD : maxD;
        float backCenterD = backFaceD + depthSign * t * 0.5f;

        SetWall(0, true,
            CenterFromAxes(uAxis, vAxis, depthAxis, (minU + maxU) * 0.5f, (minV + maxV) * 0.5f, backCenterD),
            SizeFromAxes(uAxis, vAxis, depthAxis, maxU - minU, maxV - minV, t));

        SetWall(1, openMaxV < maxV,
            CenterFromAxes(uAxis, vAxis, depthAxis, (minU + maxU) * 0.5f, (openMaxV + maxV) * 0.5f, (minD + maxD) * 0.5f),
            SizeFromAxes(uAxis, vAxis, depthAxis, maxU - minU, maxV - openMaxV, maxD - minD));

        SetWall(2, minV < openMinV,
            CenterFromAxes(uAxis, vAxis, depthAxis, (minU + maxU) * 0.5f, (minV + openMinV) * 0.5f, (minD + maxD) * 0.5f),
            SizeFromAxes(uAxis, vAxis, depthAxis, maxU - minU, openMinV - minV, maxD - minD));

        SetWall(3, minU < openMinU,
            CenterFromAxes(uAxis, vAxis, depthAxis, (minU + openMinU) * 0.5f, (openMinV + openMaxV) * 0.5f, (minD + maxD) * 0.5f),
            SizeFromAxes(uAxis, vAxis, depthAxis, openMinU - minU, openMaxV - openMinV, maxD - minD));

        SetWall(4, openMaxU < maxU,
            CenterFromAxes(uAxis, vAxis, depthAxis, (openMaxU + maxU) * 0.5f, (openMinV + openMaxV) * 0.5f, (minD + maxD) * 0.5f),
            SizeFromAxes(uAxis, vAxis, depthAxis, maxU - openMaxU, openMaxV - openMinV, maxD - minD));
    }

    private void GetAxisMapping(out int uAxis, out int vAxis, out int depthAxis, out float depthSign)
    {
        switch (openSide)
        {
            case DungeonEntranceOpenSide.Back:
                uAxis = 0;
                vAxis = 1;
                depthAxis = 2;
                depthSign = -1f;
                break;
            case DungeonEntranceOpenSide.Right:
                uAxis = 2;
                vAxis = 1;
                depthAxis = 0;
                depthSign = 1f;
                break;
            case DungeonEntranceOpenSide.Left:
                uAxis = 2;
                vAxis = 1;
                depthAxis = 0;
                depthSign = -1f;
                break;
            default:
                uAxis = 0;
                vAxis = 1;
                depthAxis = 2;
                depthSign = 1f;
                break;
        }
    }

    private static float GetAxis(Vector3 value, int axis)
    {
        return axis switch
        {
            0 => value.x,
            1 => value.y,
            _ => value.z
        };
    }

    private static Vector3 CenterFromAxes(int uAxis, int vAxis, int depthAxis, float u, float v, float depth)
    {
        Vector3 center = Vector3.zero;
        SetAxis(ref center, uAxis, u);
        SetAxis(ref center, vAxis, v);
        SetAxis(ref center, depthAxis, depth);
        return center;
    }

    private static Vector3 SizeFromAxes(int uAxis, int vAxis, int depthAxis, float u, float v, float depth)
    {
        Vector3 size = Vector3.zero;
        SetAxis(ref size, uAxis, u);
        SetAxis(ref size, vAxis, v);
        SetAxis(ref size, depthAxis, depth);
        return size;
    }

    private static void SetAxis(ref Vector3 value, int axis, float axisValue)
    {
        switch (axis)
        {
            case 0:
                value.x = axisValue;
                break;
            case 1:
                value.y = axisValue;
                break;
            default:
                value.z = axisValue;
                break;
        }
    }

    private void SetWall(int index, bool enabled, Vector3 center, Vector3 size)
    {
        BoxCollider wall = GetOrCreateWall(WallNames[index]);
        bool hasVolume = size.x >= MinWallSize && size.y >= MinWallSize && size.z >= MinWallSize;
        wall.gameObject.SetActive(enabled && hasVolume);

        if (!enabled || !hasVolume)
            return;

        wall.isTrigger = false;
        wall.center = center;
        wall.size = size;
    }

    private BoxCollider GetOrCreateWall(string wallName)
    {
        Transform wallTransform = transform.Find(wallName);
        if (wallTransform == null)
        {
            var wallObject = new GameObject(wallName);
            wallObject.transform.SetParent(transform, false);
            wallTransform = wallObject.transform;
        }

        BoxCollider wall = wallTransform.GetComponent<BoxCollider>();
        if (wall == null)
            wall = wallTransform.gameObject.AddComponent<BoxCollider>();

        return wall;
    }
}
