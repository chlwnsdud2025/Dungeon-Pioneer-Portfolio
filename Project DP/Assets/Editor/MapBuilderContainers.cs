using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 맵 프리팹 안의 표준 묶음(Floor / Wall / … / FreeObjects / DecorationRoot)을 다룹니다.
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
    /// ④ 나머지(손으로 놓은 Bonus·횃불·NPC 등) → FreeObjects
    /// </summary>
    public static int SortLoadedChildren(MapBuilder builder, IList<MapPalette> palettes, GameObject originalDecorationRoot)
    {
        Transform root = builder.tileContainer;

        // 개척 장식 묶음이 더 깊은 곳에 있으면 맨 위로 꺼냅니다. 다른 오브젝트와 함께 FreeObjects로
        // 들어가면 굽기 때 DecorationRoot를 찾지 못해 장식이 처음부터 전부 보이게 됩니다.
        if (originalDecorationRoot != null && originalDecorationRoot.transform.parent != root)
            Undo.SetTransformParent(originalDecorationRoot.transform, root, "Sort Map");

        List<Transform> children = new List<Transform>();
        foreach (Transform child in root) children.Add(child);

        int moved = 0;
        foreach (Transform child in children)
        {
            if (MapBuilder.IsTileContainer(child) || MapBuilder.IsFreeContainer(child)) continue;

            if (originalDecorationRoot != null && child.gameObject == originalDecorationRoot)
            {
                if (Find(root, MapBuilder.DecorationContainerName) == null)
                {
                    Undo.RecordObject(child.gameObject, "Sort Map");
                    child.name = MapBuilder.DecorationContainerName;
                    SortContainers(root);
                    continue;
                }
            }

            string target = Classify(palettes, child.gameObject) ?? MapBuilder.FreeContainerName;
            Undo.SetTransformParent(child, GetOrCreate(root, target), "Sort Map");
            moved++;
        }

        return moved;
    }

    /// <summary>오브젝트가 들어갈 묶음 이름. 팔레트에도 없고 타일 태그도 없으면 null.</summary>
    public static string Classify(IList<MapPalette> palettes, GameObject obj)
    {
        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(obj);
        if (source != null && palettes != null)
        {
            foreach (MapPalette palette in palettes)
                if (palette != null && palette.prefabs.Contains(source)) return MapBuilder.ContainerName(palette.type);
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

    /// <summary>불러오기 전에 원본 프리팹의 MapDecoration이 가리키던 오브젝트를 찾습니다.</summary>
    public static GameObject ReadDecorationRoot(GameObject mapRoot)
    {
        MapDecoration component = mapRoot.GetComponent<MapDecoration>();
        if (component == null) return null;

        return new SerializedObject(component).FindProperty("decorationRoot").objectReferenceValue as GameObject;
    }
}
