using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 맵빌더 0단계 복구 도구.
///
/// 1) Variant 맵 → 독립 프리팹 변환
///    OverworldMapSetup이 MapBuildScene의 타일 컨테이너를 OverworldMap 프리팹 인스턴스로 바꿔둔 상태에서
///    구운 맵(Demomap1~5)은 전부 "OverworldMap 타일 289개 제거 + 던전 타일 추가"로 된 Variant입니다.
///    루트를 일반 오브젝트로 바꿔 OverworldMap 의존을 끊습니다.
///    같은 경로에 덮어쓰므로 GUID가 유지되고, 루트 GameObject의 fileID도 원래 값으로 되돌려
///    Dungeon 씬(DungeonGenerator.mapPrefabs) 등의 기존 참조가 그대로 유지됩니다.
///
/// 2) MapBuildScene 컨테이너 초기화
///    씬에 남아있는 OverworldMap 인스턴스(맵 전체가 오버라이드로 저장된 오래된 사본)를 제거하고
///    프리팹 연결이 없는 빈 컨테이너로 교체합니다.
/// </summary>
public static class MapPrefabMigration
{
    private const string MapsFolder = "Assets/Prefabs/Maps";
    private const string MapBuildScenePath = "Assets/Scenes/MapBuildScene.unity";

    // ── 1) Variant 맵 → 독립 프리팹 ──────────────────────────────

    [MenuItem("Tools/Map Builder/Variant 맵을 독립 프리팹으로 변환")]
    public static void MigrateVariantMapsFromMenu()
    {
        List<string> targets = FindVariantMaps();
        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog("맵 변환", $"{MapsFolder}에 변환할 Variant 맵이 없습니다.", "OK");
            return;
        }

        StringBuilder list = new StringBuilder();
        foreach (string path in targets) list.AppendLine("• " + Path.GetFileNameWithoutExtension(path));

        bool proceed = EditorUtility.DisplayDialog(
            "맵 변환",
            "아래 Variant 맵을 독립 프리팹으로 변환합니다.\n" +
            "(같은 경로에 덮어쓰며 GUID와 기존 참조는 유지됩니다)\n\n" + list,
            "변환", "취소");
        if (!proceed) return;

        int success = 0;
        StringBuilder report = new StringBuilder();
        try
        {
            for (int i = 0; i < targets.Count; i++)
            {
                string path = targets[i];
                EditorUtility.DisplayProgressBar("맵 변환", path, (float)i / targets.Count);

                if (MigrateVariantMap(path, out string message)) success++;
                report.AppendLine(message);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[MapPrefabMigration] 변환 결과 ({success}/{targets.Count})\n{report}");
        EditorUtility.DisplayDialog("맵 변환", $"{success}/{targets.Count}개 변환 완료.\n\n{report}", "OK");
    }

    private static List<string> FindVariantMaps()
    {
        List<string> result = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { MapsFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset != null && PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Variant)
                result.Add(path);
        }
        result.Sort();
        return result;
    }

    private static bool MigrateVariantMap(string path, out string message)
    {
        string mapName = Path.GetFileNameWithoutExtension(path);
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out _, out long oldRootId))
        {
            message = $"✗ {mapName}: 루트 fileID를 읽지 못했습니다.";
            return false;
        }

        GameObject oldRoot = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // 베이스 프리팹(OverworldMap)에 원래 있던 자식이 남아있으면 옮길 수 없으므로 중단합니다.
            // (지금 맵들은 베이스 타일이 전부 제거된 상태라 해당 없음)
            List<Transform> children = new List<Transform>();
            List<string> baseChildren = new List<string>();
            foreach (Transform child in oldRoot.transform)
            {
                if (PrefabUtility.IsAddedGameObjectOverride(child.gameObject)) children.Add(child);
                else baseChildren.Add(child.name);
            }

            if (baseChildren.Count > 0)
            {
                message = $"✗ {mapName}: 베이스 프리팹 소속 자식 {baseChildren.Count}개가 남아 있어 건너뜀 " +
                          $"(예: {baseChildren[0]}). 수동 확인이 필요합니다.";
                return false;
            }

            GameObject newRoot = new GameObject(oldRoot.name);
            SceneManager.MoveGameObjectToScene(newRoot, oldRoot.scene);
            newRoot.tag = oldRoot.tag;
            newRoot.layer = oldRoot.layer;
            newRoot.SetActive(oldRoot.activeSelf);
            GameObjectUtility.SetStaticEditorFlags(newRoot, GameObjectUtility.GetStaticEditorFlags(oldRoot));
            newRoot.transform.localPosition = oldRoot.transform.localPosition;
            newRoot.transform.localRotation = oldRoot.transform.localRotation;
            newRoot.transform.localScale = oldRoot.transform.localScale;

            // 루트의 컴포넌트(MapDecoration 등)를 복사하고, 옛 루트를 가리키던 참조를 새 루트로 바꿔 끼웁니다.
            Dictionary<Object, Object> remap = new Dictionary<Object, Object>
            {
                { oldRoot, newRoot },
                { oldRoot.transform, newRoot.transform }
            };

            foreach (Component component in oldRoot.GetComponents<Component>())
            {
                if (component == null)
                {
                    message = $"✗ {mapName}: 루트에 Missing Script가 있어 건너뜀.";
                    Object.DestroyImmediate(newRoot);
                    return false;
                }
                if (component is Transform) continue;

                Component copy = newRoot.AddComponent(component.GetType());
                EditorUtility.CopySerialized(component, copy);
                remap[component] = copy;
            }

            foreach (Transform child in children)
                child.SetParent(newRoot.transform, false);

            RemapReferences(newRoot, remap);

            PrefabUtility.SaveAsPrefabAsset(newRoot, path, out bool saved);
            if (!saved)
            {
                message = $"✗ {mapName}: 저장 실패.";
                return false;
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(oldRoot);
        }

        string idNote = RestoreRootFileId(path, oldRootId);
        message = $"✓ {mapName}{idNote}";
        return true;
    }

    private static void RemapReferences(GameObject root, Dictionary<Object, Object> remap)
    {
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null) continue;

            SerializedObject so = new SerializedObject(component);
            SerializedProperty property = so.GetIterator();
            bool changed = false;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                Object value = property.objectReferenceValue;
                if (value != null && remap.TryGetValue(value, out Object replacement))
                {
                    property.objectReferenceValue = replacement;
                    changed = true;
                }
            }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>
    /// 새로 저장된 루트 GameObject의 fileID를 변환 전 값으로 되돌립니다.
    /// 씬에서 프리팹을 참조할 때는 {fileID: 루트, guid} 형태라 이 값이 바뀌면 참조가 끊어집니다.
    /// 맵빌더의 굽기(덮어쓰기)도 같은 문제가 있어 함께 씁니다.
    /// </summary>
    internal static string RestoreRootFileId(string path, long oldRootId)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out _, out long newRootId))
            return " (⚠ 루트 fileID 확인 실패 — Dungeon 씬의 mapPrefabs 참조를 확인하세요)";

        if (newRootId == oldRootId) return "";

        string text = File.ReadAllText(path);
        string oldToken = oldRootId.ToString();
        if (Regex.IsMatch(text, $"(?<![0-9-]){oldToken}(?![0-9])"))
            return $" (⚠ fileID 충돌로 복원 생략 — Dungeon 씬의 mapPrefabs 참조를 다시 연결하세요)";

        text = Regex.Replace(text, $"(?<![0-9-]){newRootId}(?![0-9])", oldToken);
        File.WriteAllText(path, text);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out _, out long restoredId);
        return restoredId == oldRootId
            ? ""
            : " (⚠ 루트 fileID 복원 실패 — Dungeon 씬의 mapPrefabs 참조를 확인하세요)";
    }

    // ── 2) MapBuildScene 컨테이너 초기화 ─────────────────────────

    [MenuItem("Tools/Map Builder/MapBuildScene 컨테이너 초기화")]
    public static void ResetMapBuildSceneContainer()
    {
        bool proceed = EditorUtility.DisplayDialog(
            "MapBuildScene 초기화",
            "MapBuildScene의 타일 컨테이너를 비우고 프리팹 연결이 없는 새 컨테이너로 교체합니다.\n" +
            "씬에 남아있는 맵 사본은 삭제됩니다. (맵 프리팹 파일은 영향 없음)",
            "초기화", "취소");
        if (!proceed) return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.OpenScene(MapBuildScenePath, OpenSceneMode.Single);
        MapBuilder builder = Object.FindFirstObjectByType<MapBuilder>();
        if (builder == null)
        {
            EditorUtility.DisplayDialog("MapBuildScene 초기화", "씬에서 MapBuilder를 찾지 못했습니다.", "OK");
            return;
        }

        Transform container = builder.tileContainer;
        if (container != null)
        {
            GameObject target = container.gameObject;
            if (PrefabUtility.IsPartOfPrefabInstance(target))
                target = PrefabUtility.GetOutermostPrefabInstanceRoot(target);

            // 컨테이너가 MapBuilder 자신이거나 그 상위라면 지우지 않습니다.
            if (target == builder.gameObject || builder.transform.IsChildOf(target.transform))
            {
                EditorUtility.DisplayDialog("MapBuildScene 초기화",
                    "컨테이너 구조가 예상과 달라 자동으로 지우지 않았습니다. 수동으로 확인해주세요.", "OK");
                return;
            }

            Object.DestroyImmediate(target);
        }

        string mapName = builder.prefabToLoad != null ? builder.prefabToLoad.name : builder.mapPrefabName;
        // MapBuilder의 자식으로 두지 않습니다. MapBuilder를 선택하면 맵 전체가 선택된 것처럼 기즈모가 그려지기 때문입니다.
        GameObject newContainer = new GameObject(mapName + "_Root");
        SceneManager.MoveGameObjectToScene(newContainer, scene);
        builder.tileContainer = newContainer.transform;
        builder.mapPrefabName = mapName;
        builder.SyncPlacedTiles();

        EditorUtility.SetDirty(builder);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("<color=green>[MapPrefabMigration] MapBuildScene 컨테이너를 초기화했습니다. " +
                  "'프리팹 불러오기'로 편집할 맵을 다시 불러오세요.</color>");
    }
}
