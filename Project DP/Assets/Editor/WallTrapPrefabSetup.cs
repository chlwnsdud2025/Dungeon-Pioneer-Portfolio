#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// WallTrap.prefab의 인라인 Wall 복사본을 Wall.prefab Nested Prefab으로 교체하고,
/// 루트에 중복된 WallObject를 제거합니다.
/// </summary>
public static class WallTrapPrefabSetup
{
    private const string WallTrapPath = "Assets/Prefabs/Tile/Test/WallTrap.prefab";
    private const string WallPath = "Assets/Prefabs/Tile/Test/Wall.prefab";

    [MenuItem("Tools/Dungeon/Fix WallTrap Prefab")]
    public static void FixWallTrapPrefab()
    {
        if (!ApplyFix())
            Debug.LogError("[WallTrapPrefabSetup] WallTrap.prefab 수정에 실패했습니다.");
        else
            Debug.Log("[WallTrapPrefabSetup] WallTrap.prefab을 Wall.prefab Nested Prefab 구조로 정리했습니다.");
    }

    public static bool ApplyFix()
    {
        GameObject wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallPath);
        GameObject wallTrapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallTrapPath);

        if (wallPrefab == null || wallTrapPrefab == null)
        {
            Debug.LogError($"[WallTrapPrefabSetup] 프리팹을 찾을 수 없습니다. Wall={wallPrefab != null}, WallTrap={wallTrapPrefab != null}");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(WallTrapPath);
        if (root == null)
            return false;

        try
        {
            Transform rootTransform = root.transform;

            // 인라인 Wall(메시 복사본) 제거 — Nested Prefab이 아닌 동명 자식
            for (int i = rootTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = rootTransform.GetChild(i);
                if (child.name != "Wall")
                    continue;

                if (PrefabUtility.GetPrefabAssetType(child.gameObject) == PrefabAssetType.NotAPrefab)
                    Object.DestroyImmediate(child.gameObject);
            }

            // Wall.prefab Nested 인스턴스가 없으면 추가
            bool hasWallPrefabInstance = false;
            for (int i = 0; i < rootTransform.childCount; i++)
            {
                Transform child = rootTransform.GetChild(i);
                if (child.name != "Wall")
                    continue;

                Object source = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                if (source != null && AssetDatabase.GetAssetPath(source) == WallPath)
                {
                    hasWallPrefabInstance = true;
                    break;
                }
            }

            if (!hasWallPrefabInstance)
            {
                GameObject wallInstance = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab, rootTransform);
                wallInstance.name = "Wall";
                wallInstance.transform.localPosition = Vector3.zero;
                wallInstance.transform.localRotation = Quaternion.identity;
                wallInstance.transform.localScale = Vector3.one;
            }

            // 루트 WallObject는 Wall.prefab에 있으므로 중복 제거
            WallObject duplicateWallObject = root.GetComponent<WallObject>();
            if (duplicateWallObject != null)
                Object.DestroyImmediate(duplicateWallObject);

            PrefabUtility.SaveAsPrefabAsset(root, WallTrapPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
