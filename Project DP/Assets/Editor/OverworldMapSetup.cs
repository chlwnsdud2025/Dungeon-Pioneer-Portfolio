using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Overworld Plane 타일 프리팹 생성 → OverworldMap 베이크 → Overworld 씬 배치.
///
/// ★ Position 규칙 (Plane scale = 1,1,1 고정):
///   localPosition = (gridX, 0, gridZ) * OverworldTileSpacing
///   OverworldTileSpacing = 2
///   예) Plane (3,5) → (6, 0, 10)
///
/// KayKit dirt_with_grass 메시는 스케일 1에서 약 2월드유닛 폭에 맞추는 것이
/// 맞물리므로, GridSystem(던전=3)과 별도로 spacing=2를 사용합니다.
/// MapBuilder.tileSizeOverride = 2 로 편집합니다.
/// </summary>
public static class OverworldMapSetup
{
    private const string PlanePrefabPath = "Assets/Prefabs/Tile/Overworld/Plane.prefab";
    private const string MapPrefabPath = "Assets/Prefabs/Maps/OverworldMap.prefab";
    private const string OverworldScenePath = "Assets/Scenes/Game/Overworld.unity";
    private const string MapBuildScenePath = "Assets/Scenes/MapBuildScene.unity";
    private const string DirtWithGrassFbxPath =
        "Assets/ImportedAsset/KayKit Block Bits 1.0/Assets/fbx(unity)/dirt_with_grass.fbx";

    /// <summary>
    /// Plane 프리팹 Scale은 (1,1,1)로 두고, 타일 간 월드 간격만 이 값을 씁니다.
    /// (유저 보정: grid 좌표 × 2)
    /// </summary>
    public const float OverworldTileSpacing = 2f;

    private const int DefaultMapWidth = 17;
    private const int DefaultMapHeight = 17;

    /// <summary>자동 실행/배치 모드에서 씬 저장 확인 다이얼로그를 건너뜁니다.</summary>
    public static bool SkipSavePrompt { get; set; }

    [MenuItem("Tools/Overworld/Setup Overworld Map (Bake + Place in Scene)")]
    public static void SetupOverworldMapFromMenu()
    {
        try
        {
            SetupOverworldMap();
            EditorUtility.DisplayDialog(
                "Overworld Map Setup",
                "완료되었습니다.\n\n" +
                $"• {PlanePrefabPath} (scale = 1,1,1)\n" +
                $"• {MapPrefabPath} (position = grid * {OverworldTileSpacing})\n" +
                "• Overworld 씬에 OverworldMap 배치됨\n" +
                $"• MapBuilder tileSizeOverride = {OverworldTileSpacing}",
                "OK");
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Overworld Map Setup 실패", ex.Message, "OK");
        }
    }

    /// <summary>Unity -executeMethod OverworldMapSetup.BakeFromCommandLine 용</summary>
    public static void BakeFromCommandLine()
    {
        try
        {
            SkipSavePrompt = true;
            SetupOverworldMap();
            EditorApplication.Exit(0);
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
        finally
        {
            SkipSavePrompt = false;
        }
    }

    public static void SetupOverworldMap()
    {
        EnsureFolder("Assets/Prefabs/Tile/Overworld");
        EnsureFolder("Assets/Prefabs/Maps");

        GameObject planePrefab = RecreatePlanePrefab();
        if (planePrefab == null)
            throw new System.InvalidOperationException("Plane 프리팹 생성에 실패했습니다.");

        GameObject mapPrefab = BakeOverworldMap(planePrefab);
        if (mapPrefab == null)
            throw new System.InvalidOperationException("OverworldMap 프리팹 저장에 실패했습니다.");

        ConfigureMapBuildScene(planePrefab, mapPrefab);
        PlaceMapInOverworldScene(mapPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=green>[OverworldMapSetup] 완료! {DefaultMapWidth}x{DefaultMapHeight}, spacing={OverworldTileSpacing} → {MapPrefabPath}</color>");
    }

    private static GameObject RecreatePlanePrefab()
    {
        GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(DirtWithGrassFbxPath);
        if (sourceModel == null)
        {
            sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Tile/Overworld/dirt_with_grass.fbx");
        }

        if (sourceModel == null)
            throw new System.InvalidOperationException($"FBX를 찾을 수 없습니다: {DirtWithGrassFbxPath}");

        if (AssetDatabase.LoadAssetAtPath<Object>(PlanePrefabPath) != null)
            AssetDatabase.DeleteAsset(PlanePrefabPath);

        GameObject temp = Object.Instantiate(sourceModel);
        temp.name = "Plane";

        MeshFilter meshFilter = temp.GetComponentInChildren<MeshFilter>();
        if (meshFilter == null)
        {
            Object.DestroyImmediate(temp);
            throw new System.InvalidOperationException("dirt_with_grass FBX에 MeshFilter가 없습니다.");
        }

        MeshCollider collider = meshFilter.GetComponent<MeshCollider>();
        if (collider == null)
            collider = meshFilter.gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = meshFilter.sharedMesh;
        collider.convex = false;

        GameObject prefabRoot = temp;
        if (meshFilter.gameObject != temp)
        {
            prefabRoot = meshFilter.gameObject;
            prefabRoot.name = "Plane";
            prefabRoot.transform.SetParent(null, true);
            Object.DestroyImmediate(temp);
        }

        // Scale은 항상 (1,1,1). 간격은 Bake 시 OverworldTileSpacing으로만 조절.
        prefabRoot.transform.position = Vector3.zero;
        prefabRoot.transform.rotation = Quaternion.identity;
        prefabRoot.transform.localScale = Vector3.one;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlanePrefabPath);
        Object.DestroyImmediate(prefabRoot);

        AssetDatabase.ImportAsset(PlanePrefabPath, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<GameObject>(PlanePrefabPath) ?? saved;
    }

    private static GameObject BakeOverworldMap(GameObject planePrefab)
    {
        float spacing = OverworldTileSpacing;
        GameObject mapRoot = new GameObject("OverworldMap");

        try
        {
            for (int x = 0; x < DefaultMapWidth; x++)
            {
                for (int z = 0; z < DefaultMapHeight; z++)
                {
                    GameObject tile = (GameObject)PrefabUtility.InstantiatePrefab(planePrefab);
                    tile.name = $"Plane ({x},{z})";
                    tile.transform.SetParent(mapRoot.transform, false);
                    // ★ 핵심: grid 인덱스 × spacing (Plane scale 1 유지)
                    tile.transform.localPosition = new Vector3(x * spacing, 0f, z * spacing);
                    tile.transform.localRotation = Quaternion.identity;
                    tile.transform.localScale = Vector3.one;
                }
            }

            if (AssetDatabase.LoadAssetAtPath<Object>(MapPrefabPath) != null)
                AssetDatabase.DeleteAsset(MapPrefabPath);

            PrefabUtility.SaveAsPrefabAsset(mapRoot, MapPrefabPath);
            AssetDatabase.ImportAsset(MapPrefabPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<GameObject>(MapPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(mapRoot);
        }
    }

    private static void ConfigureMapBuildScene(GameObject planePrefab, GameObject mapPrefab)
    {
        if (!Application.isBatchMode && !SkipSavePrompt)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new System.InvalidOperationException("씬 저장이 취소되어 작업을 중단했습니다.");
        }
        else
        {
            EditorSceneManager.SaveOpenScenes();
        }

        Scene mapBuildScene = EditorSceneManager.OpenScene(MapBuildScenePath, OpenSceneMode.Single);
        MapBuilder builder = Object.FindFirstObjectByType<MapBuilder>();
        if (builder == null)
        {
            Debug.LogWarning("[OverworldMapSetup] MapBuildScene에 MapBuilder가 없습니다. MapBuilder 설정은 건너뜁니다.");
            EditorSceneManager.SaveScene(mapBuildScene);
            return;
        }

        builder.mapWidth = DefaultMapWidth;
        builder.mapHeight = DefaultMapHeight;
        builder.mapPrefabName = "OverworldMap";
        builder.savePath = "Assets/Prefabs/Maps/";
        builder.prefabToLoad = mapPrefab;
        builder.floorPrefab = planePrefab;
        builder.tileSizeOverride = OverworldTileSpacing;
        builder.currentHeight = 0;
        builder.currentRotation = 0;
        builder.selectedPrefabIndex = 0;
        builder.brushSize = 1;

        if (builder.tileContainer != null)
        {
            Object.DestroyImmediate(builder.tileContainer.gameObject);
            builder.tileContainer = null;
        }

        for (int i = builder.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = builder.transform.GetChild(i);
            if (child.name.Contains("Root") || child.name.Contains("OverworldMap") || child.name.Contains("Firstmap"))
                Object.DestroyImmediate(child.gameObject);
        }

        GameObject loadedMap = (GameObject)PrefabUtility.InstantiatePrefab(mapPrefab);
        loadedMap.name = "OverworldMap_Root";
        loadedMap.transform.SetParent(builder.transform, false);
        loadedMap.transform.localPosition = Vector3.zero;
        builder.tileContainer = loadedMap.transform;
        builder.SyncPlacedTiles();

        EditorUtility.SetDirty(builder);
        EditorSceneManager.MarkSceneDirty(mapBuildScene);
        EditorSceneManager.SaveScene(mapBuildScene);
    }

    private static void PlaceMapInOverworldScene(GameObject mapPrefab)
    {
        Scene overworldScene = EditorSceneManager.OpenScene(OverworldScenePath, OpenSceneMode.Single);

        foreach (GameObject root in overworldScene.GetRootGameObjects())
        {
            string name = root.name;
            if (name == "Plane" || name == "OverworldMap" || name == "OverworldMap_Root" ||
                name.StartsWith("OverworldMap") || name == "Missing Prefab")
            {
                Object.DestroyImmediate(root);
            }
        }

        GameObject mapInstance = (GameObject)PrefabUtility.InstantiatePrefab(mapPrefab, overworldScene);
        mapInstance.name = "OverworldMap";
        mapInstance.transform.position = Vector3.zero;
        mapInstance.transform.rotation = Quaternion.identity;

        EditorSceneManager.MarkSceneDirty(overworldScene);
        EditorSceneManager.SaveScene(overworldScene);
    }

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
            return;

        string[] parts = assetPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
