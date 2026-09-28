using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 맵 프리팹 안의 표준 묶음(Floor / Wall / … / FreeObjects / Obstacles / DecorationRoot)과
/// 굽기 직전의 루트 컴포넌트(MapDecoration, SceneLocation, 장애물 GridBlocker)를 다룹니다.
///
/// 묶음은 필요할 때만 만들고, 만들 때마다 표준 순서로 정렬해 계층이 항상 같은 모양이 되게 합니다.
/// 모든 조작은 Undo에 등록합니다.
/// </summary>
public static class MapBuilderContainers
{
    private static readonly string[] CanonicalOrder = BuildCanonicalOrder();

    private static string[] BuildCanonicalOrder()
    {
        List<string> order = new List<string>(MapBuilder.TileContainerNames)
        {
            MapBuilder.FreeContainerName,
            MapBuilder.ObstacleContainerName,
            MapBuilder.DecorationContainerName
        };
        return order.ToArray();
    }

    public static Transform Find(Transform root, string name)
    {
        foreach (Transform child in root)
            if (child.name == name && child.CompareTag("Untagged")) return child;

        return null;
    }

    public static Transform GetOrCreate(Transform root, string name)
    {
        Transform existing = Find(root, name);
        if (existing != null) return existing;

        GameObject container = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(container, "Create Map Container");
        Undo.SetTransformParent(container.transform, root, "Create Map Container");
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;
        container.transform.localScale = Vector3.one;

        SortContainers(root);
        return container.transform;
    }

    // 순서 정렬은 Undo에 넣지 않습니다. 보기용일 뿐이고, 맵 전체 계층(600개 안팎)을 매번 기록하면 칠하기가 끊깁니다.
    private static void SortContainers(Transform root)
    {
        int next = 0;
        foreach (string name in CanonicalOrder)
        {
            Transform container = Find(root, name);
            if (container != null) container.SetSiblingIndex(next++);
        }
    }

    /// <summary>
    /// 불러온 맵의 자식을 종류별 묶음으로 옮깁니다. 이미 묶음으로 정리된 맵이면 아무것도 하지 않습니다.
    ///
    /// 판정 순서: ① 원래 MapDecoration이 가리키던 오브젝트 → DecorationRoot
    /// ② 팔레트에 들어 있는 원본 프리팹 → 그 팔레트 타입의 묶음 ③ 타일 태그/적 컴포넌트 → 해당 묶음
    /// ④ 나머지(손으로 놓은 Bonus·횃불·NPC 등) → 자유 배치 묶음 (던전: FreeObjects, 필드·마을: Obstacles)
    ///
    /// 필드·마을에서 장애물로 들어갈 것이 '빈 부모 그룹'이면 풀어서 안의 오브젝트를 하나씩 분류합니다.
    /// 그룹째 넣으면 빌더의 Shift+우클릭 삭제가 묶음 바로 아래 자식을 한 단위로 지우므로,
    /// 나무 하나를 지우려다 그룹 전체(오버월드 Background는 63개)가 지워집니다.
    /// </summary>
    public static int SortLoadedChildren(MapBuilder builder, IList<MapPalette> palettes, LocationType kind,
                                         GameObject originalDecorationRoot)
    {
        Transform root = builder.tileContainer;

        // 개척 장식 묶음이 더 깊은 곳에 있으면 맨 위로 꺼냅니다. 다른 오브젝트와 함께 FreeObjects로
        // 들어가면 굽기 때 DecorationRoot를 찾지 못해 장식이 처음부터 전부 보이게 됩니다.
        if (originalDecorationRoot != null && originalDecorationRoot.transform.parent != root)
            Undo.SetTransformParent(originalDecorationRoot.transform, root, "Sort Map");

        Queue<Transform> pending = new Queue<Transform>();
        foreach (Transform child in root) pending.Enqueue(child);

        List<Transform> unpackedGroups = new List<Transform>();
        string freeContainer = MapBuilder.ContainerName(MapTileType.Free, kind);
        bool flattenGroups = kind != LocationType.Dungeon;
        int moved = 0;

        // 맵 종류와 맞지 않는 자유 배치 묶음(필드 맵의 FreeObjects, 던전 맵의 Obstacles)은 풀어서 다시 분류합니다.
        // 종류를 바꿔 구웠거나 손으로 옮긴 맵에서 생기며, 그대로 두면 자유 배치가 두 묶음으로 갈립니다.
        string wrongFreeContainer = kind == LocationType.Dungeon ? MapBuilder.ObstacleContainerName : MapBuilder.FreeContainerName;

        while (pending.Count > 0)
        {
            Transform child = pending.Dequeue();
            if (child == null) continue;

            // 묶음 이름 판정과 개척 장식 인식은 맨 위 자식에만 합니다. 그룹 안쪽에 우연히 같은 이름이 있어도
            // 묶음으로 오인해 건너뛰면, 그 오브젝트가 그룹과 함께 지워집니다.
            if (child.parent == root)
            {
                if (child.name == wrongFreeContainer && MapBuilder.IsFreeContainer(child))
                {
                    foreach (Transform grandchild in child) pending.Enqueue(grandchild);
                    unpackedGroups.Add(child);
                    continue;
                }

                if (MapBuilder.IsTileContainer(child) || MapBuilder.IsFreeContainer(child)) continue;

                if (originalDecorationRoot != null && child.gameObject == originalDecorationRoot &&
                    Find(root, MapBuilder.DecorationContainerName) == null)
                {
                    Undo.RecordObject(child.gameObject, "Sort Map");
                    child.name = MapBuilder.DecorationContainerName;
                    SortContainers(root);
                    continue;
                }
            }

            string target = Classify(palettes, kind, child.gameObject) ?? freeContainer;

            if (flattenGroups && target == freeContainer && IsPlainGroup(child))
            {
                foreach (Transform grandchild in child) pending.Enqueue(grandchild);
                unpackedGroups.Add(child);
                continue;
            }

            Undo.SetTransformParent(child, GetOrCreate(root, target), "Sort Map");
            moved++;
        }

        // 안쪽 그룹부터 지웁니다. 안의 오브젝트가 전부 옮겨졌을 때만 지워서, 무엇도 함께 사라지지 않게 합니다.
        for (int i = unpackedGroups.Count - 1; i >= 0; i--)
        {
            Transform group = unpackedGroups[i];
            if (group != null && group.childCount == 0) Undo.DestroyObjectImmediate(group.gameObject);
        }

        return moved;
    }

    /// <summary>
    /// 모델도 콜라이더도 없이 자식만 묶어 둔 빈 부모인지. Transform과 GridBlocker 외의 컴포넌트가 있으면
    /// 역할이 있는 오브젝트로 보고 풀지 않습니다. 프리팹 인스턴스는 유니티가 자식을 꺼내는 걸 막으므로 풀지 않습니다.
    /// (그룹에 붙어 있던 GridBlocker는 그룹과 함께 없어지고, 굽기 때 Obstacles에 붙는 하나가 대신합니다)
    /// </summary>
    private static bool IsPlainGroup(Transform t)
    {
        if (t.childCount == 0) return false;
        if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) return false;

        foreach (Component component in t.GetComponents<Component>())
        {
            if (component == null) return false;   // Missing Script — 무엇이었는지 모르니 건드리지 않음
            if (component is Transform || component is GridBlocker) continue;
            return false;
        }

        return true;
    }

    /// <summary>오브젝트가 들어갈 묶음 이름. 팔레트에도 없고 타일 태그도 없으면 null.</summary>
    public static string Classify(IList<MapPalette> palettes, LocationType kind, GameObject obj)
    {
        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(obj);
        if (source != null && palettes != null)
        {
            foreach (MapPalette palette in palettes)
                if (palette != null && palette.prefabs.Contains(source)) return MapBuilder.ContainerName(palette.type, kind);
        }

        if (obj.GetComponentInChildren<EnemyController>(true) != null) return "Enemy";

        switch (obj.tag)
        {
            case "Floor": return "Floor";
            case "Wall": return "Wall";
            case "Entrance": return "Entrance";
            case "Exit": return "Exit";
            case "Chest": return "Chest";
            case "Trap": return "Trap";
            case "Pushable": return "Pushable";
            default: return null;
        }
    }

    /// <summary>
    /// 굽기 직전에 루트의 MapDecoration을 맞춥니다. 개척 장식이 있으면 붙이고 DecorationRoot를 연결하고,
    /// 없으면 떼어냅니다(연결 안 된 MapDecoration은 실행 중 경고만 남깁니다).
    ///
    /// 예전에는 불러오기가 원본 루트를 버리면서 MapDecoration도 함께 사라져, 굽고 나면 장식이
    /// 처음부터 전부 보이는 문제가 있었습니다.
    /// </summary>
    public static void ApplyDecoration(Transform root)
    {
        Transform decoration = Find(root, MapBuilder.DecorationContainerName);
        MapDecoration component = root.GetComponent<MapDecoration>();

        if (decoration == null || decoration.childCount == 0)
        {
            if (component != null) Undo.DestroyObjectImmediate(component);
            return;
        }

        if (component == null) component = Undo.AddComponent<MapDecoration>(root.gameObject);

        SerializedObject so = new SerializedObject(component);
        so.FindProperty("decorationRoot").objectReferenceValue = decoration.gameObject;
        so.ApplyModifiedProperties();
    }

    /// <summary>
    /// 굽기 직전에 맵 종류에 맞는 컴포넌트를 맞춥니다.
    /// 필드·마을: 루트에 SceneLocation(종류 지정), Obstacles 묶음에 GridBlocker 하나.
    ///   GridBlocker는 자식 콜라이더를 전부 모아 칸을 막으므로 장애물마다 붙일 필요가 없습니다.
    /// 던전: SceneLocation을 떼어냅니다 — 던전은 DungeonGenerator가 장소를 만들어 두 번 만들어지면 안 됩니다.
    /// </summary>
    public static void ApplySceneSetup(Transform root, LocationType kind)
    {
        SceneLocation location = root.GetComponent<SceneLocation>();

        if (kind == LocationType.Dungeon)
        {
            if (location != null) Undo.DestroyObjectImmediate(location);
            return;
        }

        if (location == null) location = Undo.AddComponent<SceneLocation>(root.gameObject);

        SerializedObject so = new SerializedObject(location);
        so.FindProperty("type").enumValueIndex = (int)kind;
        so.ApplyModifiedProperties();

        Transform obstacles = Find(root, MapBuilder.ObstacleContainerName);
        if (obstacles == null) return;

        GridBlocker blocker = obstacles.GetComponent<GridBlocker>();
        if (obstacles.childCount > 0 && blocker == null) Undo.AddComponent<GridBlocker>(obstacles.gameObject);
        else if (obstacles.childCount == 0 && blocker != null) Undo.DestroyObjectImmediate(blocker);
    }

    /// <summary>불러오기 전에 원본 맵의 종류를 읽습니다. 루트에 SceneLocation이 있으면 그 종류, 없으면 던전입니다.</summary>
    public static LocationType ReadKind(GameObject mapRoot)
    {
        SceneLocation location = mapRoot.GetComponent<SceneLocation>();
        if (location == null) return LocationType.Dungeon;

        return (LocationType)new SerializedObject(location).FindProperty("type").enumValueIndex;
    }

    /// <summary>불러오기 전에 원본 프리팹의 MapDecoration이 가리키던 오브젝트를 찾습니다.</summary>
    public static GameObject ReadDecorationRoot(GameObject mapRoot)
    {
        MapDecoration component = mapRoot.GetComponent<MapDecoration>();
        if (component == null) return null;

        return new SerializedObject(component).FindProperty("decorationRoot").objectReferenceValue as GameObject;
    }
}
