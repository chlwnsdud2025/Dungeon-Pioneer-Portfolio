using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapBuilder))]
public class MapBuilderEditor : Editor
{
    private const int TrapPaletteIndex = 5; // TileContainerNames에서 "Trap"의 위치
    private const float TileThumbnailSize = 64f;
    private const float TrapThumbnailSize = 52f;
    private const float FreeThumbnailSize = 64f;
    private const float MaxFreeHeight = 40f;

    private const string FoldMapSettingsKey = "MapBuilderEditor.FoldMapSettings";
    private const string FoldSlotSettingsKey = "MapBuilderEditor.FoldSlotSettings";

    private static readonly Color TileColor = new Color(0.40f, 0.68f, 1.00f);
    private static readonly Color FreeColor = new Color(0.45f, 0.85f, 0.50f);
    private static readonly Color DecorationColor = new Color(1.00f, 0.72f, 0.30f);
    private static readonly Color BakeColor = new Color(0.45f, 0.80f, 1.00f);
    private static readonly Color DangerColor = new Color(1.00f, 0.45f, 0.45f);

    // ★ 팀원 피드백 반영: 각도 대신 직관적인 화살표(시선 방향)로 UI 텍스트 변경
    private readonly string[] rotationNames = { "↑ (앞/0°)", "→ (우/90°)", "↓ (뒤/180°)", "← (좌/270°)" };

    private MapBuilder builder;
    private GameObject[] palette;

    private GameObject ghostObject;
    private Material ghostMaterial;
    private GameObject lastGhostPrefab;

    // Shift+드래그 한 번(한 붓질)을 Undo 한 단계로 묶기 위한 그룹 번호
    private int strokeUndoGroup = -1;
    private bool strokeBlocked;

    private GUIStyle titleStyle;
    private GUIStyle sectionStyle;
    private GUIStyle paletteButtonStyle;
    private GUIStyle dropStyle;
    private GUIStyle hudStyle;
    private GUIStyle shortcutStyle;

    private bool IsTileMode => builder.buildMode == MapBuilder.BuildMode.Tile;

    /// <summary>
    /// 지금 놓을 프리팹. 타일 모드는 슬롯 8칸에서, 자유 배치·개척 장식은 자유 배치 목록에서 고릅니다.
    /// 칠하기·고스트·선택 지우개가 모두 이 값 하나만 보게 해서 모드가 섞이지 않게 합니다.
    /// </summary>
    private GameObject CurrentPrefab
    {
        get
        {
            if (IsTileMode) return palette[builder.selectedPrefabIndex];

            List<GameObject> list = builder.freePrefabs;
            if (list == null || list.Count == 0) return null;
            return list[Mathf.Clamp(builder.selectedFreeIndex, 0, list.Count - 1)];
        }
    }

    private void OnEnable()
    {
        builder = (MapBuilder)target;
        UpdatePalette();
        builder.SyncPlacedTiles();
        CreateGhostMaterial();
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        if (ghostObject != null) DestroyImmediate(ghostObject);
        if (ghostMaterial != null) DestroyImmediate(ghostMaterial);
    }

    // Undo/Redo로 타일이 생기거나 사라지면 좌표 사전이 어긋나므로 다시 맞춥니다.
    private void OnUndoRedo()
    {
        if (builder == null) return;
        builder.SyncPlacedTiles();
        SceneView.RepaintAll();
    }

    private void UpdatePalette()
    {
        palette = new GameObject[]
        {
            builder.floorPrefab, builder.wallPrefab, builder.entrancePrefab,
            builder.exitPrefab, builder.chestPrefab, GetSelectedTrapPrefab(),
            builder.enemyPrefab, builder.pushablePrefab
        };
    }

    // 팔레트의 Trap 슬롯에 쓸, 현재 선택된 함정 프리팹을 반환합니다.
    private GameObject GetSelectedTrapPrefab()
    {
        if (builder.trapPrefabs == null || builder.trapPrefabs.Count == 0) return null;
        int idx = Mathf.Clamp(builder.selectedTrapIndex, 0, builder.trapPrefabs.Count - 1);
        return builder.trapPrefabs[idx];
    }

    private static Color ModeColor(MapBuilder.BuildMode mode)
    {
        switch (mode)
        {
            case MapBuilder.BuildMode.Free: return FreeColor;
            case MapBuilder.BuildMode.Decoration: return DecorationColor;
            default: return TileColor;
        }
    }

    private static string ModeLabel(MapBuilder.BuildMode mode)
    {
        switch (mode)
        {
            case MapBuilder.BuildMode.Free: return "✦ 자유 배치";
            case MapBuilder.BuildMode.Decoration: return "✧ 개척 장식";
            default: return "■ 타일";
        }
    }

    private string ModeContainerName
        => builder.buildMode == MapBuilder.BuildMode.Decoration ? MapBuilder.DecorationContainerName : MapBuilder.FreeContainerName;

    private void EnsureStyles()
    {
        if (titleStyle != null) return;

        titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        sectionStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
        paletteButtonStyle = new GUIStyle(GUI.skin.button)
        {
            imagePosition = ImagePosition.ImageAbove,
            alignment = TextAnchor.MiddleCenter,
            fontSize = 10,
            clipping = TextClipping.Clip,
            padding = new RectOffset(3, 3, 4, 2)
        };
        dropStyle = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
        hudStyle = new GUIStyle(EditorStyles.helpBox) { fontSize = 11, richText = true, padding = new RectOffset(8, 8, 6, 6) };
        shortcutStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
    }

    private void CreateGhostMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

        ghostMaterial = new Material(shader);
        ghostMaterial.SetFloat("_Surface", 1);
        ghostMaterial.SetFloat("_Blend", 0);
        ghostMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        ghostMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        ghostMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        ghostMaterial.SetInt("_ZWrite", 0);
        ghostMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        ghostMaterial.SetColor("_BaseColor", new Color(0.2f, 1f, 0.2f, 0.6f));
    }

    // 고스트는 선택한 '프리팹'이 바뀔 때만 다시 만듭니다. 번호로 비교하면 모드와 함정 종류 선택이
    // 서로 다른 번호 체계라 바뀐 것을 놓칩니다.
    private void UpdateGhost()
    {
        GameObject prefab = CurrentPrefab;
        if (prefab == null)
        {
            if (ghostObject != null) DestroyImmediate(ghostObject);
            lastGhostPrefab = null;
            return;
        }

        if (lastGhostPrefab != prefab || ghostObject == null)
        {
            if (ghostObject != null) DestroyImmediate(ghostObject);

            ghostObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            ghostObject.hideFlags = HideFlags.HideAndDontSave;

            foreach (var col in ghostObject.GetComponentsInChildren<Collider>()) col.enabled = false;
            foreach (var renderer in ghostObject.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = ghostMaterial;

            lastGhostPrefab = prefab;
        }
    }

    /// <summary>
    /// 편집 가능한 컨테이너를 보장합니다. 없으면 만들고, 프리팹 인스턴스면 연결을 끊습니다.
    /// ★ 컨테이너가 프리팹 인스턴스인 채로 쓰면 타일 삭제가 막히고,
    ///   '굽기' 결과가 그 프리팹의 Variant가 되어 원본 프리팹에 종속됩니다. (Demomap이 OverworldMap Variant가 된 원인)
    /// </summary>
    private bool EnsureEditableContainer()
    {
        if (builder.tileContainer == null)
        {
            GameObject containerObj = new GameObject(builder.mapPrefabName + "_Root");
            containerObj.transform.SetParent(builder.transform, false);
            Undo.RegisterCreatedObjectUndo(containerObj, "Create Map Container");
            Undo.RecordObject(builder, "Create Map Container");
            builder.tileContainer = containerObj.transform;
            return true;
        }

        GameObject container = builder.tileContainer.gameObject;
        if (!PrefabUtility.IsPartOfPrefabInstance(container)) return true;

        if (PrefabUtility.IsOutermostPrefabInstanceRoot(container))
        {
            PrefabUtility.UnpackPrefabInstance(container, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);
            Debug.LogWarning($"[MapBuilder] 타일 컨테이너 '{container.name}'가 프리팹 인스턴스여서 연결을 해제했습니다.");
            return true;
        }

        EditorUtility.DisplayDialog("Map Builder",
            $"타일 컨테이너 '{container.name}'가 다른 프리팹 인스턴스의 일부라 편집할 수 없습니다.\n" +
            "컨테이너를 비우고 다시 시도하거나 [Tools > Map Builder > MapBuildScene 컨테이너 초기화]를 실행하세요.",
            "OK");
        return false;
    }

    // ── 인스펙터 ──────────────────────────────────────────────────────

    public override void OnInspectorGUI()
    {
        EnsureStyles();
        UpdatePalette();
        EditorGUI.BeginChangeCheck();

        DrawHeaderBox();
        DrawModeTabs();
        DrawPaletteBox();
        DrawPlacementBox();
        DrawShortcutBox();
        DrawFileBox();

        if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();

        // 썸네일은 유니티가 비동기로 만들어서, 다 만들어질 때까지 다시 그려야 빈 칸이 채워집니다.
        if (AssetPreview.IsLoadingAssetPreviews()) Repaint();
    }

    private void DrawHeaderBox()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("🗺  MAP BUILDER", titleStyle);

        string working = builder.tileContainer != null ? builder.tileContainer.name : "(아직 없음 — 처음 칠할 때 만들어집니다)";
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"작업 중  {working} · {builder.mapWidth} × {builder.mapHeight}", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        using (new EditorGUI.DisabledScope(builder.prefabToLoad == null))
        {
            if (GUILayout.Button("원본 보기", EditorStyles.miniButton, GUILayout.Width(64)))
                EditorGUIUtility.PingObject(builder.prefabToLoad);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawModeTabs()
    {
        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        DrawModeTab(MapBuilder.BuildMode.Tile);
        DrawModeTab(MapBuilder.BuildMode.Free);
        DrawModeTab(MapBuilder.BuildMode.Decoration);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawModeTab(MapBuilder.BuildMode mode)
    {
        bool active = builder.buildMode == mode;
        Color previous = GUI.backgroundColor;
        if (active) GUI.backgroundColor = ModeColor(mode);

        bool clicked = GUILayout.Toggle(active, ModeLabel(mode), "Button", GUILayout.Height(30));
        GUI.backgroundColor = previous;

        if (clicked && !active)
        {
            Undo.RecordObject(builder, "Change Map Builder Mode");
            builder.buildMode = mode;
        }
    }

    private void DrawPaletteBox()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("팔레트", sectionStyle);

        if (IsTileMode) DrawTilePalette();
        else DrawFreePalette();

        EditorGUILayout.EndVertical();
    }

    private void DrawTilePalette()
    {
        int picked = DrawThumbnailGrid(palette, MapBuilder.TileContainerNames, builder.selectedPrefabIndex, TileThumbnailSize, null);
        if (picked != builder.selectedPrefabIndex)
        {
            Undo.RecordObject(builder, "Select Map Tile");
            builder.selectedPrefabIndex = picked;
        }

        // 'Trap' 팔레트가 선택됐을 때만, 어떤 함정 종류를 칠할지 하위 선택 UI를 표시합니다.
        if (builder.selectedPrefabIndex != TrapPaletteIndex) return;

        EditorGUILayout.Space(4);
        GUILayout.Label("함정 종류", EditorStyles.miniBoldLabel);

        if (builder.trapPrefabs == null || builder.trapPrefabs.Count == 0)
        {
            EditorGUILayout.HelpBox("'팔레트 슬롯 설정'의 Trap Prefabs 목록이 비어 있습니다.", MessageType.Warning);
            return;
        }

        string[] names = new string[builder.trapPrefabs.Count];
        for (int i = 0; i < names.Length; i++)
            names[i] = builder.trapPrefabs[i] != null ? builder.trapPrefabs[i].name : "(비어 있음)";

        int current = Mathf.Clamp(builder.selectedTrapIndex, 0, names.Length - 1);
        int trap = DrawThumbnailGrid(builder.trapPrefabs, names, current, TrapThumbnailSize, null);
        if (trap != builder.selectedTrapIndex)
        {
            Undo.RecordObject(builder, "Select Trap");
            builder.selectedTrapIndex = trap;
            UpdatePalette();
        }
    }

    private void DrawFreePalette()
    {
        List<GameObject> list = builder.freePrefabs;

        if (list != null && list.Count > 0)
        {
            string[] names = new string[list.Count];
            for (int i = 0; i < names.Length; i++)
                names[i] = list[i] != null ? list[i].name : "(비어 있음)";

            int current = Mathf.Clamp(builder.selectedFreeIndex, 0, list.Count - 1);
            int picked = DrawThumbnailGrid(list, names, current, FreeThumbnailSize, ShowFreeContextMenu);
            if (picked != builder.selectedFreeIndex)
            {
                Undo.RecordObject(builder, "Select Free Prefab");
                builder.selectedFreeIndex = picked;
            }
        }

        EditorGUILayout.Space(4);
        DrawDropArea();

        string where = builder.buildMode == MapBuilder.BuildMode.Decoration
            ? "개척 장식은 DecorationRoot에 들어가고, 던전에서 개척을 완료해야 보입니다."
            : "자유 배치는 FreeObjects에 들어가고, 항상 보입니다.";
        GUILayout.Label(where + " 버튼을 우클릭하면 팔레트에서 뺄 수 있습니다.", shortcutStyle);
    }

    /// <summary>
    /// 썸네일 버튼 그리드. 선택된 칸은 모드 색으로 칠합니다. 고른 번호를 돌려줍니다.
    /// 썸네일은 유니티의 프리팹 미리보기를 쓰고, 아직 만들어지지 않았으면 작은 아이콘으로 대신합니다.
    /// </summary>
    private int DrawThumbnailGrid(IList<GameObject> prefabs, IList<string> labels, int selected, float size,
                                  System.Action<int> onContextClick)
    {
        float available = EditorGUIUtility.currentViewWidth - 44f;
        int columns = Mathf.Max(1, Mathf.FloorToInt(available / (size + 4f)));
        int result = selected;
        Color selectedColor = ModeColor(builder.buildMode);

        for (int start = 0; start < prefabs.Count; start += columns)
        {
            EditorGUILayout.BeginHorizontal();
            for (int i = start; i < Mathf.Min(start + columns, prefabs.Count); i++)
            {
                GameObject prefab = prefabs[i];
                string tooltip = prefab != null ? prefab.name : "슬롯이 비어 있습니다. '팔레트 슬롯 설정'에서 연결하세요.";
                GUIContent content = new GUIContent(labels[i], Thumbnail(prefab), tooltip);

                Rect rect = GUILayoutUtility.GetRect(size, size + 14f, GUILayout.Width(size), GUILayout.Height(size + 14f));

                Event e = Event.current;
                if (onContextClick != null && e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
                {
                    onContextClick(i);
                    e.Use();
                }

                Color previous = GUI.backgroundColor;
                if (i == selected) GUI.backgroundColor = selectedColor;
                if (GUI.Toggle(rect, i == selected, content, paletteButtonStyle) && i != selected) result = i;
                GUI.backgroundColor = previous;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        return result;
    }

    private static Texture Thumbnail(GameObject prefab)
    {
        if (prefab == null) return null;

        Texture preview = AssetPreview.GetAssetPreview(prefab);
        return preview != null ? preview : AssetPreview.GetMiniThumbnail(prefab);
    }

    private void ShowFreeContextMenu(int index)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("팔레트에서 빼기"), false, () =>
        {
            Undo.RecordObject(builder, "Remove Free Prefab");
            builder.freePrefabs.RemoveAt(index);
            builder.selectedFreeIndex = Mathf.Clamp(builder.selectedFreeIndex, 0, Mathf.Max(0, builder.freePrefabs.Count - 1));
            Repaint();
        });
        menu.ShowAsContext();
    }

    /// <summary>Project 창의 프리팹을 끌어다 놓으면 자유 배치 목록에 추가합니다. 이미 있는 것은 건너뜁니다.</summary>
    private void DrawDropArea()
    {
        Rect rect = GUILayoutUtility.GetRect(0f, 38f, GUILayout.ExpandWidth(true));
        GUI.Box(rect, "프리팹을 여기로 끌어다 놓아 팔레트에 추가", dropStyle);

        Event e = Event.current;
        if ((e.type != EventType.DragUpdated && e.type != EventType.DragPerform) || !rect.Contains(e.mousePosition))
            return;

        bool anyPrefab = false;
        foreach (Object obj in DragAndDrop.objectReferences)
            if (obj is GameObject go && PrefabUtility.IsPartOfPrefabAsset(go)) anyPrefab = true;

        DragAndDrop.visualMode = anyPrefab ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

        if (e.type == EventType.DragPerform && anyPrefab)
        {
            DragAndDrop.AcceptDrag();
            Undo.RecordObject(builder, "Add Free Prefab");

            foreach (Object obj in DragAndDrop.objectReferences)
            {
                if (!(obj is GameObject go) || !PrefabUtility.IsPartOfPrefabAsset(go)) continue;
                if (builder.freePrefabs.Contains(go)) continue;

                builder.freePrefabs.Add(go);
                builder.selectedFreeIndex = builder.freePrefabs.Count - 1;
            }
        }

        e.Use();
    }

    private void DrawPlacementBox()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("배치", sectionStyle);

        if (IsTileMode)
        {
            int brush = EditorGUILayout.IntSlider("브러시 크기", builder.brushSize, 1, 5);
            int height = EditorGUILayout.IntSlider("설치 높이 (층)", builder.currentHeight, 0, 10);
            bool selective = EditorGUILayout.ToggleLeft("선택한 타일만 지우기", builder.useSelectiveEraser);

            if (brush != builder.brushSize || height != builder.currentHeight || selective != builder.useSelectiveEraser)
            {
                Undo.RecordObject(builder, "Change Tile Settings");
                builder.brushSize = brush;
                builder.currentHeight = height;
                builder.useSelectiveEraser = selective;
            }
        }
        else
        {
            float height = EditorGUILayout.FloatField("높이 (m)", builder.freeHeight);
            float step = EditorGUILayout.FloatField("조절 간격 (m)", builder.freeHeightStep);

            if (!Mathf.Approximately(height, builder.freeHeight) || !Mathf.Approximately(step, builder.freeHeightStep))
            {
                Undo.RecordObject(builder, "Change Free Settings");
                builder.freeHeight = Mathf.Clamp(height, -MaxFreeHeight, MaxFreeHeight);
                builder.freeHeightStep = Mathf.Max(0.01f, step);
            }
        }

        EditorGUILayout.Space(2);
        GUILayout.Label("회전", EditorStyles.miniBoldLabel);
        int rotation = GUILayout.SelectionGrid(builder.currentRotation, rotationNames, 4);
        if (rotation != builder.currentRotation)
        {
            Undo.RecordObject(builder, "Change Rotation");
            builder.currentRotation = rotation;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawShortcutBox()
    {
        string text = IsTileMode
            ? "Shift+클릭 칠하기 · Shift+우클릭 지우기 · Shift+R 회전 · Ctrl+휠 브러시 크기"
            : "Shift+클릭 하나 배치 · Shift+우클릭 마우스 아래 오브젝트 삭제 · Shift+R 회전 · Ctrl+휠 높이";

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label(text, shortcutStyle);
        EditorGUILayout.EndVertical();
    }

    private void DrawFileBox()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("맵 파일", sectionStyle);

        EditorGUILayout.BeginHorizontal();
        GameObject toLoad = (GameObject)EditorGUILayout.ObjectField("불러올 프리팹", builder.prefabToLoad, typeof(GameObject), false);
        if (toLoad != builder.prefabToLoad)
        {
            Undo.RecordObject(builder, "Change Map To Load");
            builder.prefabToLoad = toLoad;
        }
        if (GUILayout.Button("불러오기", GUILayout.Width(72))) LoadMapFromPrefab();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        string saveName = EditorGUILayout.TextField("저장 이름", builder.mapPrefabName);
        if (saveName != builder.mapPrefabName)
        {
            Undo.RecordObject(builder, "Change Map Name");
            builder.mapPrefabName = saveName;
        }
        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = BakeColor;
        if (GUILayout.Button("굽기", GUILayout.Width(72))) SaveMapAsPrefab();
        GUI.backgroundColor = previous;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        DrawFoldout(FoldMapSettingsKey, "맵 설정 (크기 · 저장 경로 · 컨테이너)",
            "mapWidth", "mapHeight", "savePath", "tileContainer");
        DrawFoldout(FoldSlotSettingsKey, "팔레트 슬롯 설정",
            "floorPrefab", "wallPrefab", "entrancePrefab", "exitPrefab", "chestPrefab", "trapPrefabs",
            "enemyPrefab", "pushablePrefab", "freePrefabs");

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("가이드 Plane 깔기")) CreateGuidePlane();
        if (GUILayout.Button("가이드 지우기")) RemoveGuidePlane();
        GUILayout.FlexibleSpace();
        GUI.backgroundColor = DangerColor;
        if (GUILayout.Button("전체 맵 지우기", GUILayout.Width(100))) ClearMap();
        GUI.backgroundColor = previous;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 자주 안 쓰는 설정은 접어 둡니다. 펼침 상태는 에디터 세션 동안만 기억합니다(씬에 저장하지 않음).
    /// 여기서만 SerializedObject를 써서, 목록 편집·프리팹 연결을 유니티 기본 UI 그대로 쓰게 합니다.
    /// </summary>
    private void DrawFoldout(string key, string title, params string[] properties)
    {
        bool open = SessionState.GetBool(key, false);
        bool next = EditorGUILayout.Foldout(open, title, true);
        if (next != open) SessionState.SetBool(key, next);
        if (!next) return;

        serializedObject.Update();
        EditorGUI.indentLevel++;
        foreach (string name in properties)
        {
            SerializedProperty property = serializedObject.FindProperty(name);
            if (property != null) EditorGUILayout.PropertyField(property, true);
        }
        EditorGUI.indentLevel--;
        serializedObject.ApplyModifiedProperties();
    }

    private void CreateGuidePlane()
    {
        RemoveGuidePlane();

        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "Editor_GuidePlane";
        plane.transform.SetParent(builder.transform);

        float centerX = (builder.mapWidth - 1) * builder.tileSize / 2f;
        float centerZ = (builder.mapHeight - 1) * builder.tileSize / 2f;
        plane.transform.position = new Vector3(centerX, -0.01f, centerZ);

        float scaleX = (builder.mapWidth * builder.tileSize) / 10f;
        float scaleZ = (builder.mapHeight * builder.tileSize) / 10f;
        plane.transform.localScale = new Vector3(scaleX, 1, scaleZ);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(0.2f, 0.2f, 0.2f, 1f));

        plane.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    private void RemoveGuidePlane()
    {
        Transform plane = builder.transform.Find("Editor_GuidePlane");
        if (plane != null) DestroyImmediate(plane.gameObject);
    }

    // ── 씬 뷰 ─────────────────────────────────────────────────────────

    private void OnSceneGUI()
    {
        Event e = Event.current;
        UpdatePalette(); // Trap 종류 변경 등이 즉시 고스트/설치에 반영되도록 팔레트를 갱신
        UpdateGhost();

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.R && e.shift)
        {
            builder.currentRotation = (builder.currentRotation + 1) % 4;
            if (ghostObject != null)
            {
                ghostObject.transform.rotation = Quaternion.Euler(0, builder.currentRotation * 90f, 0);
            }
            Repaint();
            SceneView.RepaintAll();
            e.Use();
        }

        if (e.type == EventType.ScrollWheel && e.control)
        {
            if (IsTileMode)
            {
                if (e.delta.y > 0) builder.brushSize = Mathf.Max(1, builder.brushSize - 1);
                else if (e.delta.y < 0) builder.brushSize = Mathf.Min(5, builder.brushSize + 1);
            }
            else
            {
                // 휠을 위로 굴리면 올라갑니다. 간격의 배수에 맞춰 두어 손으로 친 값도 다음 조절부터 격자에 붙습니다.
                float step = Mathf.Max(0.01f, builder.freeHeightStep);
                float snapped = Mathf.Round(builder.freeHeight / step) * step;
                builder.freeHeight = Mathf.Clamp(snapped + (e.delta.y < 0 ? step : -step), -MaxFreeHeight, MaxFreeHeight);
            }

            Repaint();
            SceneView.RepaintAll();
            e.Use();
        }

        DrawSceneHud();

        if (IsTileMode) HandleTileScene(e);
        else HandleFreeScene(e);

        if (e.rawType == EventType.MouseUp && strokeUndoGroup >= 0)
        {
            Undo.CollapseUndoOperations(strokeUndoGroup);
            strokeUndoGroup = -1;
        }

        if (e.type == EventType.MouseMove || e.type == EventType.ScrollWheel) SceneView.RepaintAll();
    }

    /// <summary>씬 뷰 왼쪽 위 안내판. 인스펙터를 보지 않아도 지금 무엇을 어떻게 놓는지 알 수 있게 합니다.</summary>
    private void DrawSceneHud()
    {
        EnsureStyles();

        GameObject prefab = CurrentPrefab;
        string color = ColorUtility.ToHtmlStringRGB(ModeColor(builder.buildMode));
        string prefabName = prefab != null ? prefab.name : "(선택 없음)";
        string rotation = rotationNames[builder.currentRotation];
        string detail = IsTileMode
            ? $"높이 {builder.currentHeight}층 · 브러시 {builder.brushSize} · 회전 {rotation}"
            : $"높이 {builder.freeHeight:0.##} m · 회전 {rotation}";

        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10f, 10f, 300f, 60f));
        GUILayout.Label($"<b><color=#{color}>{ModeLabel(builder.buildMode)}</color></b> · {prefabName}\n{detail}", hudStyle);
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    private void DrawGrid(float y)
    {
        Handles.color = new Color(1f, 1f, 1f, IsTileMode ? 0.15f : 0.07f);
        float minBound = -builder.tileSize / 2f;
        float maxXBound = (builder.mapWidth - 1) * builder.tileSize + builder.tileSize / 2f;
        float maxZBound = (builder.mapHeight - 1) * builder.tileSize + builder.tileSize / 2f;

        for (int x = 0; x <= builder.mapWidth; x++)
        {
            float posX = x * builder.tileSize - (builder.tileSize / 2f);
            Handles.DrawLine(new Vector3(posX, y, minBound), new Vector3(posX, y, maxZBound));
        }
        for (int z = 0; z <= builder.mapHeight; z++)
        {
            float posZ = z * builder.tileSize - (builder.tileSize / 2f);
            Handles.DrawLine(new Vector3(minBound, y, posZ), new Vector3(maxXBound, y, posZ));
        }
    }

    private void HandleTileScene(Event e)
    {
        float currentYHeight = builder.currentHeight * GridSystem.Instance.TileHeight;
        DrawGrid(currentYHeight);

        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, currentYHeight, 0));
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        if (!groundPlane.Raycast(ray, out float enter))
        {
            if (ghostObject != null) ghostObject.SetActive(false);
            return;
        }

        Vector3 hitPoint = ray.GetPoint(enter);
        int gridX = Mathf.RoundToInt(hitPoint.x / builder.tileSize);
        int gridZ = Mathf.RoundToInt(hitPoint.z / builder.tileSize);

        int offsetStart = -(builder.brushSize - 1) / 2;
        int offsetEnd = builder.brushSize / 2;
        bool isMouseInBounds = (gridX >= 0 && gridX < builder.mapWidth && gridZ >= 0 && gridZ < builder.mapHeight);

        if (ghostObject != null)
        {
            if (isMouseInBounds)
            {
                ghostObject.SetActive(true);
                ghostObject.transform.position = new Vector3(gridX * builder.tileSize, currentYHeight, gridZ * builder.tileSize);
                ghostObject.transform.rotation = Quaternion.Euler(0, builder.currentRotation * 90f, 0);
            }
            else ghostObject.SetActive(false);
        }

        Handles.color = TileColor;
        for (int x = offsetStart; x <= offsetEnd; x++)
        {
            for (int z = offsetStart; z <= offsetEnd; z++)
            {
                int targetX = gridX + x;
                int targetZ = gridZ + z;

                if (targetX >= 0 && targetX < builder.mapWidth && targetZ >= 0 && targetZ < builder.mapHeight)
                {
                    Vector3 snapPos = new Vector3(targetX * builder.tileSize, currentYHeight, targetZ * builder.tileSize);
                    Handles.DrawWireCube(snapPos, new Vector3(builder.tileSize, 0.1f, builder.tileSize));
                }
            }
        }

        if (!e.shift) return;

        int controlID = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(controlID);

        if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
        {
            BeginStroke(e.button == 0 ? "Paint Map Tiles" : "Erase Map Tiles");
        }

        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && strokeBlocked)
        {
            e.Use();
        }
        else if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
        {
            if (e.button == 0)
            {
                ApplyBrush(gridX, builder.currentHeight, gridZ, currentYHeight, true);
                e.Use();
            }
            else if (e.button == 1)
            {
                ApplyBrush(gridX, builder.currentHeight, gridZ, currentYHeight, false);
                e.Use();
            }
        }
    }

    /// <summary>
    /// 자유 배치·개척 장식. 격자에 맞추지 않고 '높이' 평면 위 마우스 위치에 하나씩 놓습니다.
    /// 드래그로 연속 배치하지 않습니다 — 한 번 그으면 수십 개가 깔립니다.
    /// </summary>
    private void HandleFreeScene(Event e)
    {
        float y = builder.freeHeight;
        DrawGrid(y);

        Plane plane = new Plane(Vector3.up, new Vector3(0, y, 0));
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        if (!plane.Raycast(ray, out float enter))
        {
            if (ghostObject != null) ghostObject.SetActive(false);
            return;
        }

        Vector3 point = ray.GetPoint(enter);
        Quaternion rotation = Quaternion.Euler(0, builder.currentRotation * 90f, 0);

        if (ghostObject != null)
        {
            ghostObject.SetActive(true);
            ghostObject.transform.SetPositionAndRotation(point, rotation);
        }

        // 공중에 뜬 높이를 눈으로 볼 수 있게 바닥(y=0)까지 세로선을 긋습니다.
        Color modeColor = ModeColor(builder.buildMode);
        Handles.color = modeColor;
        Vector3 ground = new Vector3(point.x, 0f, point.z);
        if (Mathf.Abs(y) > 0.01f) Handles.DrawDottedLine(point, ground, 3f);
        Handles.DrawWireDisc(ground, Vector3.up, 0.25f);
        Handles.DrawWireDisc(point, Vector3.up, 0.35f);

        if (!e.shift) return;

        int controlID = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(controlID);

        if (e.type == EventType.MouseDrag)
        {
            e.Use();
            return;
        }

        if (e.type != EventType.MouseDown || (e.button != 0 && e.button != 1)) return;

        BeginStroke(e.button == 0 ? "Place Free Object" : "Delete Free Object");
        if (!strokeBlocked)
        {
            if (e.button == 0) PlaceFree(point, rotation);
            else RemoveFreeUnderMouse(e.mousePosition);
        }
        e.Use();
    }

    private void BeginStroke(string undoName)
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(undoName);
        strokeUndoGroup = Undo.GetCurrentGroup();
        // 컨테이너를 쓸 수 없으면 경고창을 한 번만 띄우고 이번 붓질은 무시합니다.
        strokeBlocked = !EnsureEditableContainer();
    }

    private void PlaceFree(Vector3 position, Quaternion rotation)
    {
        GameObject prefab = CurrentPrefab;
        if (prefab == null) return;

        Transform parent = MapBuilderContainers.GetOrCreate(builder.tileContainer, ModeContainerName);
        GameObject placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        placed.transform.SetPositionAndRotation(position, rotation);
        Undo.RegisterCreatedObjectUndo(placed, "Place Free Object");
    }

    /// <summary>마우스 아래 오브젝트 중 '지금 모드의 묶음'에 속한 것 하나를 지웁니다. 타일이나 다른 묶음은 건드리지 않습니다.</summary>
    private void RemoveFreeUnderMouse(Vector2 mousePosition)
    {
        Transform container = MapBuilderContainers.Find(builder.tileContainer, ModeContainerName);
        if (container == null) return;

        // 고스트가 마우스 바로 아래에 있어 그대로 두면 고스트가 집힙니다.
        bool ghostWasActive = ghostObject != null && ghostObject.activeSelf;
        if (ghostWasActive) ghostObject.SetActive(false);
        GameObject picked = HandleUtility.PickGameObject(mousePosition, false);
        if (ghostWasActive) ghostObject.SetActive(true);

        Transform target = picked != null ? picked.transform : null;
        while (target != null && target.parent != container) target = target.parent;

        if (target != null) Undo.DestroyObjectImmediate(target.gameObject);
    }

    private void ApplyBrush(int centerX, int gridY, int centerZ, float spawnY, bool isPlace)
    {
        int offsetStart = -(builder.brushSize - 1) / 2;
        int offsetEnd = builder.brushSize / 2;

        for (int x = offsetStart; x <= offsetEnd; x++)
        {
            for (int z = offsetStart; z <= offsetEnd; z++)
            {
                int targetX = centerX + x;
                int targetZ = centerZ + z;

                if (targetX >= 0 && targetX < builder.mapWidth && targetZ >= 0 && targetZ < builder.mapHeight)
                {
                    if (isPlace) PlaceTile(targetX, gridY, targetZ, spawnY);
                    else RemoveTile(targetX, gridY, targetZ);
                }
            }
        }
    }

    private void PlaceTile(int x, int y, int z, float spawnY)
    {
        GameObject prefab = CurrentPrefab;
        if (prefab == null) return;

        if (!EnsureEditableContainer()) return;

        Vector3Int gridPos = new Vector3Int(x, y, z);

        if (builder.placedTiles.ContainsKey(gridPos))
        {
            if (builder.placedTiles[gridPos] != null) Undo.DestroyObjectImmediate(builder.placedTiles[gridPos]);
            builder.placedTiles.Remove(gridPos);
        }

        Transform parent = MapBuilderContainers.GetOrCreate(builder.tileContainer, MapBuilder.TileContainerNames[builder.selectedPrefabIndex]);
        GameObject newTile = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        newTile.transform.position = new Vector3(x * builder.tileSize, spawnY, z * builder.tileSize);
        newTile.transform.rotation = Quaternion.Euler(0, builder.currentRotation * 90f, 0);
        Undo.RegisterCreatedObjectUndo(newTile, "Place Map Tile");

        builder.placedTiles.Add(gridPos, newTile);
    }

    private void RemoveTile(int x, int y, int z)
    {
        Vector3Int gridPos = new Vector3Int(x, y, z);
        if (builder.placedTiles.ContainsKey(gridPos))
        {
            GameObject targetObj = builder.placedTiles[gridPos];

            if (builder.useSelectiveEraser && targetObj != null)
            {
                GameObject targetPrefab = PrefabUtility.GetCorrespondingObjectFromSource(targetObj);
                GameObject selectedPrefab = CurrentPrefab;

                if (targetPrefab != selectedPrefab) return;
            }

            if (targetObj != null)
            {
                if (!EnsureEditableContainer()) return;
                Undo.DestroyObjectImmediate(targetObj);
            }
            builder.placedTiles.Remove(gridPos);
        }
    }

    // ── 불러오기 · 굽기 · 지우기 ─────────────────────────────────────────

    private void ClearMap()
    {
        if (builder.tileContainer == null || builder.tileContainer.childCount == 0) return;

        bool proceed = EditorUtility.DisplayDialog("전체 맵 지우기",
            $"컨테이너의 오브젝트 {builder.tileContainer.childCount}개를 모두 지웁니다. (Ctrl+Z로 되돌릴 수 있습니다)",
            "지우기", "취소");
        if (!proceed) return;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Clear Map");
        int group = Undo.GetCurrentGroup();

        ClearContainer();

        Undo.CollapseUndoOperations(group);
    }

    private bool ClearContainer()
    {
        if (!EnsureEditableContainer()) return false;

        for (int i = builder.tileContainer.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(builder.tileContainer.GetChild(i).gameObject);
        }
        builder.placedTiles.Clear();
        return true;
    }

    private void LoadMapFromPrefab()
    {
        if (builder.prefabToLoad == null)
        {
            Debug.LogWarning("불러올 프리팹을 인스펙터 슬롯에 등록해주세요!");
            return;
        }

        // Variant 맵을 풀면 베이스 프리팹 인스턴스가 남아 다시 Variant로 저장되는 문제가 반복됩니다.
        if (PrefabUtility.GetPrefabAssetType(builder.prefabToLoad) == PrefabAssetType.Variant)
        {
            EditorUtility.DisplayDialog("Map Builder",
                $"'{builder.prefabToLoad.name}'은(는) Prefab Variant라 불러올 수 없습니다.\n" +
                "[Tools > Map Builder > Variant 맵을 독립 프리팹으로 변환]을 먼저 실행하세요.",
                "OK");
            return;
        }

        if (builder.tileContainer != null && builder.tileContainer.childCount > 0)
        {
            bool proceed = EditorUtility.DisplayDialog("프리팹 불러오기",
                "현재 컨테이너의 내용을 지우고 불러옵니다. 저장하지 않은 작업은 사라집니다. (Ctrl+Z로 되돌릴 수 있습니다)",
                "불러오기", "취소");
            if (!proceed) return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Load Map Prefab");
        int group = Undo.GetCurrentGroup();

        if (!ClearContainer()) return;

        GameObject loadedMap = (GameObject)PrefabUtility.InstantiatePrefab(builder.prefabToLoad);
        Undo.RegisterCreatedObjectUndo(loadedMap, "Load Map Prefab");
        PrefabUtility.UnpackPrefabInstance(loadedMap, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);

        // 원본 루트는 곧 지워지므로, 그 전에 개척 장식 묶음이 무엇이었는지 읽어 둡니다.
        GameObject decorationRoot = MapBuilderContainers.ReadDecorationRoot(loadedMap);

        Transform[] children = new Transform[loadedMap.transform.childCount];
        for (int i = 0; i < children.Length; i++) children[i] = loadedMap.transform.GetChild(i);
        foreach (Transform child in children)
        {
            Undo.SetTransformParent(child, builder.tileContainer, "Load Map Prefab");
        }

        Undo.DestroyObjectImmediate(loadedMap);

        Undo.RecordObject(builder, "Load Map Prefab");
        builder.mapPrefabName = builder.prefabToLoad.name;
        Undo.RecordObject(builder.tileContainer.gameObject, "Load Map Prefab");
        builder.tileContainer.name = builder.mapPrefabName + "_Root";

        int sorted = MapBuilderContainers.SortLoadedChildren(builder, decorationRoot);

        Undo.CollapseUndoOperations(group);
        builder.SyncPlacedTiles();

        string sortNote = sorted > 0 ? $" — 오브젝트 {sorted}개를 종류별 묶음으로 정리했습니다. 굽기 전까지 원본은 그대로입니다." : "";
        Debug.Log($"<color=cyan>맵 불러오기 완료! ({builder.prefabToLoad.name}){sortNote}</color>");
    }

    private void SaveMapAsPrefab()
    {
        if (builder.tileContainer == null || builder.tileContainer.childCount == 0)
        {
            Debug.LogWarning("저장할 타일이 없습니다! 씬에 타일을 먼저 설치해주세요.");
            return;
        }

        // 컨테이너가 프리팹 인스턴스면 결과물이 그 프리팹의 Variant가 되므로 먼저 연결을 끊습니다.
        if (!EnsureEditableContainer()) return;

        string folder = builder.savePath.TrimEnd('/', '\\');
        if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);
        string fullPath = $"{folder}/{builder.mapPrefabName}.prefab";

        // 덮어쓰기는 파일 guid는 유지하지만 루트 오브젝트 ID를 새로 만듭니다. Dungeon 씬의 mapPrefabs는
        // {루트 ID, guid}로 맵을 가리키므로, 원래 ID를 읽어 두었다가 굽고 나서 되돌려야 참조가 끊기지 않습니다.
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(fullPath);
        long oldRootId = 0;
        bool hasOldRootId = existing != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(existing, out _, out oldRootId);

        if (existing != null)
        {
            bool proceed = EditorUtility.DisplayDialog("프리팹으로 굽기",
                $"'{fullPath}'을(를) 현재 씬의 내용으로 덮어씁니다.\n\n" +
                "다른 팀원이 이 프리팹을 직접 수정했다면 그 변경은 사라집니다. " +
                "작업 전에 '프리팹 불러오기'로 최신 버전을 불러왔는지 확인하세요.",
                "덮어쓰기", "취소");
            if (!proceed) return;
        }

        // 개척 장식이 있으면 루트에 MapDecoration을 붙이고 DecorationRoot를 연결합니다.
        MapBuilderContainers.ApplyDecoration(builder.tileContainer);

        PrefabUtility.SaveAsPrefabAsset(builder.tileContainer.gameObject, fullPath, out bool success);

        if (!success)
        {
            Debug.LogError("맵 저장에 실패했습니다. 경로를 확인해주세요.");
            return;
        }

        string idNote = hasOldRootId ? MapPrefabMigration.RestoreRootFileId(fullPath, oldRootId) : "";
        if (string.IsNullOrEmpty(idNote)) Debug.Log($"<color=green>맵 저장 성공! 경로: {fullPath}</color>");
        else Debug.LogWarning($"맵은 저장됐지만 참조 ID를 되돌리지 못했습니다: {fullPath}{idNote}");
    }
}
