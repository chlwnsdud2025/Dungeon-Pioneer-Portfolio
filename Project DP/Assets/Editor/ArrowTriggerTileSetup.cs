#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// ArrowTriggerTile의 발판 트리거를 floor_tile_small 비주얼 크기에 맞춥니다.
/// </summary>
public static class ArrowTriggerTileSetup
{
    private const string PrefabPath = "Assets/Prefabs/Tile/Test/ArrowTriggerTile.prefab";

    [MenuItem("Tools/Dungeon/Fix Arrow Trigger Tile")]
    public static void FixFromMenu()
    {
        if (ApplyFix())
            Debug.Log("[ArrowTriggerTileSetup] ArrowTriggerTile 프리팹 트리거를 수정했습니다.");
    }

    public static bool ApplyFix()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null) return false;

        try
        {
            BoxCollider rootCollider = root.GetComponent<BoxCollider>();
            if (rootCollider == null)
                rootCollider = root.AddComponent<BoxCollider>();
            rootCollider.isTrigger = true;

            PressurePlate plate = root.GetComponent<PressurePlate>();
            if (plate == null)
                plate = root.AddComponent<PressurePlate>();

            plate.fitTriggerToFloorTile = true;
            plate.waitForArrowResolve = true;
            plate.FitTriggerToFloorTile();

            foreach (PressurePlate childPlate in root.GetComponentsInChildren<PressurePlate>(true))
            {
                if (childPlate.gameObject == root) continue;
                Object.DestroyImmediate(childPlate);
            }

            foreach (BoxCollider childCollider in root.GetComponentsInChildren<BoxCollider>(true))
            {
                if (childCollider.gameObject == root) continue;
                if (childCollider.isTrigger)
                    Object.DestroyImmediate(childCollider);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}

#endif
