using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 팔레트 파일(MapPaletteLibrary.asset)을 찾거나 만듭니다.
///
/// 파일은 프로젝트에 하나만 둡니다. MapBuilder에 연결이 없으면 먼저 프로젝트에서 찾고(팀원이 이미 만들어
/// 커밋한 파일), 정말 없을 때만 새로 만들어 옛 슬롯 값을 던전 팔레트로 옮깁니다.
/// MapBuildScene은 커밋하지 않는 게 원칙이라, 연결이 비어 있는 채로 열리는 경우가 흔합니다.
/// </summary>
public static class MapPaletteLibraryAssets
{
    public const string DefaultPath = "Assets/SO/MapBuilder/MapPaletteLibrary.asset";

    public static MapPaletteLibrary FindOrCreate(MapBuilder builder, out bool created)
    {
        created = false;

        string[] guids = AssetDatabase.FindAssets("t:MapPaletteLibrary");
        if (guids.Length > 0)
        {
            if (guids.Length > 1)
                Debug.LogWarning($"[MapBuilder] 팔레트 파일이 {guids.Length}개 있습니다. 첫 번째를 씁니다: " +
                                 AssetDatabase.GUIDToAssetPath(guids[0]));

            return AssetDatabase.LoadAssetAtPath<MapPaletteLibrary>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        EnsureFolder(Path.GetDirectoryName(DefaultPath).Replace('\\', '/'));

        MapPaletteLibrary library = ScriptableObject.CreateInstance<MapPaletteLibrary>();
        library.dungeon = BuildFromLegacySlots(builder);
        AssetDatabase.CreateAsset(library, DefaultPath);
        AssetDatabase.SaveAssets();

        created = true;
        return library;
    }

    /// <summary>
    /// 옛 슬롯 8칸 + 함정 목록 + 자유 배치 목록을 던전 팔레트로 옮깁니다. 빈 슬롯은 건너뜁니다.
    /// 자유 배치와 개척 장식은 비어 있어도 만들어 둡니다 — 팔레트에 보여야 끌어다 놓을 곳이 생깁니다.
    /// </summary>
    private static List<MapPalette> BuildFromLegacySlots(MapBuilder builder)
    {
        List<MapPalette> palettes = new List<MapPalette>();

        AddIfAny(palettes, "바닥", MapTileType.Floor, builder.floorPrefab);
        AddIfAny(palettes, "벽", MapTileType.Wall, builder.wallPrefab);
        AddIfAny(palettes, "입구", MapTileType.Entrance, builder.entrancePrefab);
        AddIfAny(palettes, "출구", MapTileType.Exit, builder.exitPrefab);
        AddIfAny(palettes, "상자", MapTileType.Chest, builder.chestPrefab);
        AddIfAny(palettes, "함정", MapTileType.Trap, builder.trapPrefabs?.ToArray());
        AddIfAny(palettes, "적", MapTileType.Enemy, builder.enemyPrefab);
        AddIfAny(palettes, "밀기 블록", MapTileType.Pushable, builder.pushablePrefab);

        palettes.Add(Create("자유 배치", MapTileType.Free, builder.freePrefabs?.ToArray()));
        palettes.Add(Create("개척 장식", MapTileType.Decoration));
        return palettes;
    }

    private static void AddIfAny(List<MapPalette> palettes, string name, MapTileType type, params GameObject[] prefabs)
    {
        MapPalette palette = Create(name, type, prefabs);
        if (palette.prefabs.Count > 0) palettes.Add(palette);
    }

    private static MapPalette Create(string name, MapTileType type, params GameObject[] prefabs)
    {
        MapPalette palette = new MapPalette { name = name, type = type };
        if (prefabs != null)
            foreach (GameObject prefab in prefabs)
                if (prefab != null && !palette.prefabs.Contains(prefab)) palette.prefabs.Add(prefab);

        return palette;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}

/// <summary>팔레트 파일을 Project 창에서 선택했을 때의 화면. 편집은 MapBuilder에서만 하도록 안내만 합니다.</summary>
[CustomEditor(typeof(MapPaletteLibrary))]
public class MapPaletteLibraryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MapPaletteLibrary library = (MapPaletteLibrary)target;

        EditorGUILayout.HelpBox("맵빌더 팔레트 파일입니다. 편집은 MapBuildScene의 MapBuilder 인스펙터에서 하세요.\n" +
                                "여기서 고치면 두 곳에서 편집하게 되어 헷갈립니다.", MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("던전 팔레트", $"{library.dungeon.Count}개");
        EditorGUILayout.LabelField("필드 팔레트", $"{library.field.Count}개");
        EditorGUILayout.LabelField("마을 팔레트", $"{library.town.Count}개");
    }
}
