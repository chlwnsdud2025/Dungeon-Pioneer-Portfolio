using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapBuilder))]
public class MapBuilderEditor : Editor
{
    private const float ThumbnailSize = 58f;
    private const string GuidePlaneName = "Editor_GuidePlane";
    private const float MaxFreeHeight = 40f;

    private const string FoldMapSettingsKey = "MapBuilderEditor.FoldMapSettings";
    private const string FoldPaletteKeyPrefix = "MapBuilderEditor.FoldPalette.";
    private const string EditPalettesKey = "MapBuilderEditor.EditPalettes";

    // 목록을 그리는 도중에 순서를 바꾸거나 지우면 이번 프레임 레이아웃이 어긋나므로, 요청만 받아 두고 다 그린 뒤 처리합니다.
    private enum PaletteAction { None, MoveUp, MoveDown, Delete }

    private static readonly Color GridColor = new Color(0.40f, 0.68f, 1.00f);
    private static readonly Color FreeColor = new Color(0.45f, 0.85f, 0.50f);
    private static readonly Color DecorationColor = new Color(1.00f, 0.72f, 0.30f);
    private static readonly Color BakeColor = new Color(0.45f, 0.80f, 1.00f);
    private static readonly Color DangerColor = new Color(1.00f, 0.45f, 0.45f);

    // ★ 팀원 피드백 반영: 각도 대신 직관적인 화살표(시선 방향)로 UI 텍스트 변경
    private readonly string[] rotationNames = { "↑ (앞/0°)", "→ (우/90°)", "↓ (뒤/180°)", "← (좌/270°)" };

    private static readonly MapTileType[] AllTypes = (MapTileType[])System.Enum.GetValues(typeof(MapTileType));
    private static readonly LocationType[] KindOrder = { LocationType.Dungeon, LocationType.Overworld, LocationType.Town };
    private static readonly Dictionary<LocationType, List<MapTileType>> AllowedTypeCache = new Dictionary<LocationType, List<MapTileType>>();
    private static readonly Dictionary<LocationType, string[]> TypeOptionCache = new Dictionary<LocationType, string[]>();

    private MapBuilder builder;

    private GameObject ghostObject;
    private Material ghostMaterial;
    private GameObject lastGhostPrefab;

    // Shift+드래그 한 번(한 붓질)을 Undo 한 단계로 묶기 위한 그룹 번호
    private int strokeUndoGroup = -1;
    private bool strokeBlocked;

    // 스크립트를 다시 컴파일하면 유니티가 에디터의 private 필드까지 보존했다가 되살립니다. 그러면 예전부터 있던
    // 스타일은 살아 있고 새로 추가한 스타일만 비어, EnsureStyles가 "이미 준비됨"으로 보고 건너뛰어 NullReference가 납니다.
    // 보존하지 않게 해서 컴파일 뒤에는 전부 비었다가 한꺼번에 다시 만들어지게 합니다.
    [System.NonSerialized] private GUIStyle titleStyle;
    [System.NonSerialized] private GUIStyle sectionStyle;
    [System.NonSerialized] private GUIStyle paletteHeaderStyle;
    [System.NonSerialized] private GUIStyle paletteTagStyle;
    [System.NonSerialized] private GUIStyle paletteButtonStyle;
    [System.NonSerialized] private GUIStyle dropStyle;
    [System.NonSerialized] private GUIStyle hudStyle;
    [System.NonSerialized] private GUIStyle shortcutStyle;

    // ── 현재 선택 ──────────────────────────────────────────────────────
    // 칠하기·고스트·선택 지우개·HUD가 모두 아래 값만 보게 해서, 무엇을 어떻게 놓는지가 한 곳에서 정해지게 합니다.

    /// <summary>지금 만드는 맵 종류(던전·필드·마을)의 팔레트 목록.</summary>
    private List<MapPalette> CurrentPalettes => PalettesOf(builder.mapKind);

    private List<MapPalette> PalettesOf(LocationType kind)
    {
        MapPaletteLibrary library = builder.paletteLibrary;
        if (library == null) return null;

        switch (kind)
        {
            case LocationType.Overworld: return library.field;
            case LocationType.Town: return library.town;
            default: return library.dungeon;
        }
    }

    private MapPalette CurrentPalette
    {
        get
        {
            List<MapPalette> list = CurrentPalettes;
            if (list == null || builder.selectedPaletteIndex < 0 || builder.selectedPaletteIndex >= list.Count) return null;
            return list[builder.selectedPaletteIndex];
        }
    }

    /// <summary>지금 놓을 프리팹. 이 맵 종류에서 쓸 수 없는 타입의 팔레트를 골랐으면 아무것도 놓지 않습니다.</summary>
    private GameObject CurrentPrefab
    {
        get
        {
            MapPalette palette = CurrentPalette;
            if (palette == null || palette.prefabs.Count == 0) return null;
            if (!MapBuilder.IsTypeAllowed(palette.type, builder.mapKind)) return null;
            return palette.prefabs[Mathf.Clamp(builder.selectedItemIndex, 0, palette.prefabs.Count - 1)];
        }
    }

    private MapTileType CurrentType => CurrentPalette != null ? CurrentPalette.type : MapTileType.Floor;

    /// <summary>팔레트 타입이 배치 방식을 정합니다. 격자 타입이면 칠하기, 자유 배치·개척 장식이면 하나씩 놓기.</summary>
    private bool IsGridPlacement => MapBuilder.IsGridType(CurrentType);

    private void OnEnable()
    {
        builder = (MapBuilder)target;
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
        Repaint();
        SceneView.RepaintAll();
    }

    private static string KindLabel(LocationType kind)
    {
        switch (kind)
        {
            case LocationType.Overworld: return "필드";
            case LocationType.Town: return "마을";
            default: return "던전";
        }
    }

    /// <summary>이 맵 종류에서 고를 수 있는 타입. 팔레트 타입 목록에는 이것만 보입니다.</summary>
    private static List<MapTileType> AllowedTypes(LocationType kind)
    {
        if (AllowedTypeCache.TryGetValue(kind, out List<MapTileType> cached)) return cached;

        List<MapTileType> allowed = new List<MapTileType>();
        foreach (MapTileType type in AllTypes)
            if (MapBuilder.IsTypeAllowed(type, kind)) allowed.Add(type);

        AllowedTypeCache[kind] = allowed;
        return allowed;
    }

    private static string[] TypeOptions(LocationType kind)
    {
        if (TypeOptionCache.TryGetValue(kind, out string[] cached)) return cached;

        List<MapTileType> allowed = AllowedTypes(kind);
        string[] options = new string[allowed.Count];
        for (int i = 0; i < options.Length; i++)
            options[i] = $"{TypeLabel(allowed[i], kind)} ({allowed[i]})";

        TypeOptionCache[kind] = options;
        return options;
    }

    /// <summary>자유 배치는 필드·마을에서 길을 막는 장애물이 되므로 이름도 '장애물'로 보여줍니다.</summary>
    private static string TypeLabel(MapTileType type, LocationType kind)
    {
        switch (type)
        {
            case MapTileType.Floor: return "바닥";
            case MapTileType.Wall: return "벽";
            case MapTileType.Entrance: return "입구";
            case MapTileType.Exit: return "출구";
            case MapTileType.Chest: return "상자";
            case MapTileType.Trap: return "함정";
            case MapTileType.Enemy: return "적";
            case MapTileType.Pushable: return "밀기 블록";
            case MapTileType.Free: return kind == LocationType.Dungeon ? "자유 배치" : "장애물";
            case MapTileType.Decoration: return "개척 장식";
            default: return type.ToString();
        }
    }

    private static Color TypeColor(MapTileType type)
    {
        switch (type)
        {
            case MapTileType.Free: return FreeColor;
            case MapTileType.Decoration: return DecorationColor;
            default: return GridColor;
        }
    }

    private static string PlacementLabel(MapTileType type, LocationType kind)
    {
        switch (type)
        {
            case MapTileType.Free: return kind == LocationType.Dungeon ? "✦ 자유 배치" : "✦ 장애물";
            case MapTileType.Decoration: return "✧ 개척 장식";
            default: return "■ 격자";
        }
    }

    private void EnsureStyles()
    {
        if (titleStyle != null) return;

        titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        sectionStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
        paletteHeaderStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
        paletteTagStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
        paletteButtonStyle = new GUIStyle(GUI.skin.button)
        {
            imagePosition = ImagePosition.ImageAbove,
            alignment = TextAnchor.MiddleCenter,
            fontSize = 10,
            clipping = TextClipping.Clip,
            padding = new RectOffset(3, 3, 4, 2)
        };
        dropStyle = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
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

    // 고스트는 선택한 '프리팹'이 바뀔 때만 다시 만듭니다.
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
            // MapBuilder의 자식으로 만들지 않습니다. 이유는 MoveContainerOutOfBuilder 참고.
            GameObject containerObj = new GameObject(builder.mapPrefabName + "_Root");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(containerObj, builder.gameObject.scene);
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

    /// <summary>
    /// 팔레트 파일 연결을 보장합니다. 비어 있으면 프로젝트에서 찾고, 없으면 옛 슬롯 값으로 새로 만듭니다.
    /// MapBuildScene은 커밋하지 않으므로 팀원 PC에서는 연결이 빈 채로 열리는 게 보통입니다.
    /// </summary>
    private void EnsureLibrary()
    {
        if (builder.paletteLibrary != null) return;

        MapPaletteLibrary library = MapPaletteLibraryAssets.FindOrCreate(builder, out bool created);
        if (library == null) return;

        Undo.RecordObject(builder, "Assign Palette Library");
        builder.paletteLibrary = library;
        builder.selectedPaletteIndex = 0;
        builder.selectedItemIndex = 0;

        string path = AssetDatabase.GetAssetPath(library);
        Debug.Log(created
            ? $"[MapBuilder] 팔레트 파일을 만들고 기존 슬롯을 던전 팔레트 {library.dungeon.Count}개로 옮겼습니다: {path}"
            : $"[MapBuilder] 팔레트 파일을 연결했습니다: {path}");
    }

    /// <summary>
    /// 맵 컨테이너가 MapBuilder 아래에 있으면 씬 최상단으로 꺼냅니다(월드 위치 유지, Ctrl+Z 가능).
    ///
    /// 유니티는 오브젝트를 선택하면 그 자식 전체를 선택된 것으로 보고, 모든 타일의 콜라이더 외곽선과
    /// 선택 외곽선, 적 시야 범위 같은 '선택 시 기즈모'를 전부 그립니다. 칠하려면 MapBuilder를 선택해야 하므로,
    /// 맵이 그 자식이면 편집 내내 맵 전체가 선택된 모습이 됩니다.
    /// </summary>
    private void MoveContainerOutOfBuilder()
    {
        Transform container = builder.tileContainer;
        if (container == null || container == builder.transform || !container.IsChildOf(builder.transform)) return;

        Undo.SetTransformParent(container, null, "Move Map Container Out Of Builder");
        Debug.Log($"[MapBuilder] 맵 컨테이너 '{container.name}'를 MapBuilder 밖(씬 최상단)으로 옮겼습니다. " +
                  "MapBuilder를 선택해도 맵의 콜라이더·기즈모가 보이지 않게 됩니다.");
    }

    /// <summary>팔레트 파일을 고치기 직전에 부릅니다. Undo에 남기고, 저장 대상으로 표시합니다.</summary>
    private void RecordLibrary(string undoName)
    {
        Undo.RecordObject(builder.paletteLibrary, undoName);
        EditorUtility.SetDirty(builder.paletteLibrary);
    }

    // ── 인스펙터 ──────────────────────────────────────────────────────

    public override void OnInspectorGUI()
    {
        EnsureStyles();
        EnsureLibrary();
        MoveContainerOutOfBuilder();
        EditorGUI.BeginChangeCheck();

        DrawHeaderBox();
        DrawKindTabs();
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

    /// <summary>
    /// 지금 만드는 맵의 종류. 종류마다 팔레트 목록이 따로 있고, 굽기 결과도 달라집니다
    /// (필드·마을은 SceneLocation과 장애물 GridBlocker가 붙음). 불러오기를 하면 자동으로 맞춰집니다.
    /// </summary>
    private void DrawKindTabs()
    {
        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        foreach (LocationType kind in KindOrder)
        {
            bool active = builder.mapKind == kind;
            Color previous = GUI.backgroundColor;
            if (active) GUI.backgroundColor = BakeColor;

            bool clicked = GUILayout.Toggle(active, KindLabel(kind), "Button", GUILayout.Height(26));
            GUI.backgroundColor = previous;

            if (clicked && !active) SetKind(kind);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void SetKind(LocationType kind)
    {
        Undo.RecordObject(builder, "Change Map Kind");
        builder.mapKind = kind;
        builder.selectedPaletteIndex = 0;
        builder.selectedItemIndex = 0;
    }

    private void DrawPaletteBox()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        bool editing = SessionState.GetBool(EditPalettesKey, false);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"팔레트  ·  {KindLabel(builder.mapKind)}", sectionStyle);
        GUILayout.FlexibleSpace();
        bool nextEditing = GUILayout.Toggle(editing, "편집", EditorStyles.miniButton, GUILayout.Width(44));
        EditorGUILayout.EndHorizontal();
        if (nextEditing != editing) SessionState.SetBool(EditPalettesKey, nextEditing);

        List<MapPalette> palettes = CurrentPalettes;
        if (palettes == null)
        {
            EditorGUILayout.HelpBox("팔레트 파일을 찾거나 만들지 못했습니다. Console을 확인하세요.", MessageType.Error);
            EditorGUILayout.EndVertical();
            return;
        }

        if (palettes.Count == 0)
            EditorGUILayout.HelpBox("팔레트가 없습니다. 아래 '+ 새 팔레트'로 시작하세요.", MessageType.Info);

        PaletteAction action = PaletteAction.None;
        int actionIndex = -1;
        for (int i = 0; i < palettes.Count; i++)
        {
            PaletteAction requested = DrawPalette(palettes, i, nextEditing);
            if (requested == PaletteAction.None) continue;

            action = requested;
            actionIndex = i;
        }

        EditorGUILayout.Space(2);
        if (GUILayout.Button("+ 새 팔레트", GUILayout.Height(22)))
        {
            AddPalette(palettes);
            SessionState.SetBool(EditPalettesKey, true);   // 새 팔레트는 이름과 타입부터 정해야 하므로 편집을 켭니다
        }

        EditorGUILayout.EndVertical();

        if (action != PaletteAction.None) ApplyPaletteAction(palettes, action, actionIndex);
    }

    /// <summary>
    /// 팔레트 한 칸. 머리줄은 칸 너비에서 직접 잘라 배치합니다 — 고정 폭 컨트롤을 늘어놓으면
    /// 인스펙터가 좁을 때 오른쪽 버튼이 화면 밖으로 밀려납니다.
    /// 편집 중이 아니면 이름·타입만 보여주고, 편집 중이면 순서 이동·삭제와 이름·타입 칸이 나옵니다.
    /// </summary>
    private PaletteAction DrawPalette(List<MapPalette> palettes, int index, bool editing)
    {
        MapPalette palette = palettes[index];
        LocationType kind = builder.mapKind;
        bool isCurrent = index == builder.selectedPaletteIndex;
        bool isGrid = MapBuilder.IsGridType(palette.type);
        bool allowed = MapBuilder.IsTypeAllowed(palette.type, kind);
        Color typeColor = allowed ? TypeColor(palette.type) : DangerColor;
        PaletteAction action = PaletteAction.None;

        Color previous = GUI.backgroundColor;
        if (isCurrent) GUI.backgroundColor = Color.Lerp(Color.white, typeColor, 0.45f);
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUI.backgroundColor = previous;

        // ── 머리줄 ──
        Rect header = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
        float right = header.xMax;

        if (editing)
        {
            Rect delete = new Rect(right - 20f, header.y + 1f, 20f, 18f);
            Rect down = new Rect(delete.x - 21f, header.y + 1f, 20f, 18f);
            Rect up = new Rect(down.x - 21f, header.y + 1f, 20f, 18f);
            right = up.x - 4f;

            using (new EditorGUI.DisabledScope(index == 0))
                if (GUI.Button(up, "▲", EditorStyles.miniButtonLeft)) action = PaletteAction.MoveUp;
            using (new EditorGUI.DisabledScope(index == palettes.Count - 1))
                if (GUI.Button(down, "▼", EditorStyles.miniButtonRight)) action = PaletteAction.MoveDown;

            GUI.backgroundColor = DangerColor;
            if (GUI.Button(delete, "✕", EditorStyles.miniButton)) action = PaletteAction.Delete;
            GUI.backgroundColor = previous;
        }
        else
        {
            string tag = allowed
                ? $"{TypeLabel(palette.type, kind)} · {(isGrid ? "격자" : "자유")}"
                : $"⚠ {TypeLabel(palette.type, kind)} — {KindLabel(kind)}에서 못 씀";
            float tagWidth = paletteTagStyle.CalcSize(new GUIContent(tag)).x;
            Rect tagRect = new Rect(right - tagWidth, header.y + 2f, tagWidth, 16f);
            right = tagRect.x - 6f;

            Color previousContent = GUI.contentColor;
            GUI.contentColor = typeColor;
            GUI.Label(tagRect, tag, paletteTagStyle);
            GUI.contentColor = previousContent;
        }

        // 유니티의 Foldout은 화살표를 영역 왼쪽 바깥에 그립니다. 그만큼 안으로 밀어 화살표가 칸 안에 오게 합니다.
        string foldKey = FoldKey(index);
        bool open = SessionState.GetBool(foldKey, true);
        Rect foldRect = new Rect(header.x + 14f, header.y + 1f, Mathf.Max(20f, right - header.x - 14f), 18f);
        string title = $"{palette.name}  ({palette.prefabs.Count})";
        bool nextOpen = EditorGUI.Foldout(foldRect, open, FitText(title, paletteHeaderStyle, foldRect.width - 14f), true, paletteHeaderStyle);
        if (nextOpen != open) SessionState.SetBool(foldKey, nextOpen);

        // ── 편집 칸 ──
        if (editing)
        {
            // 타입 목록에는 이 맵 종류에서 쓸 수 있는 것만 보입니다. 지금 타입이 목록에 없으면 빈 칸으로 보입니다.
            List<MapTileType> allowedTypes = AllowedTypes(kind);
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 36f;
            string name = EditorGUILayout.TextField("이름", palette.name);
            int picked = EditorGUILayout.Popup("타입", allowedTypes.IndexOf(palette.type), TypeOptions(kind));
            EditorGUIUtility.labelWidth = previousLabelWidth;

            MapTileType type = picked >= 0 ? allowedTypes[picked] : palette.type;
            if (name != palette.name || type != palette.type)
            {
                RecordLibrary("Edit Palette");
                palette.name = name;
                palette.type = type;
            }

            if (!allowed)
                EditorGUILayout.HelpBox($"{KindLabel(kind)} 맵에서는 쓸 수 없는 타입입니다. 타입을 바꾸거나 팔레트를 지우세요.", MessageType.Warning);
        }

        // ── 썸네일 ──
        if (nextOpen)
        {
            if (palette.prefabs.Count > 0)
            {
                int selected = isCurrent ? Mathf.Clamp(builder.selectedItemIndex, 0, palette.prefabs.Count - 1) : -1;
                int picked = DrawThumbnailGrid(palette.prefabs, selected, typeColor,
                    item => ShowItemContextMenu(palette, index, item));

                if (picked >= 0 && picked != selected) Select(index, picked);
            }
            else if (!editing)
            {
                GUILayout.Label("비어 있음 — 프리팹을 이 칸으로 끌어다 놓으세요", EditorStyles.centeredGreyMiniLabel);
            }

            if (editing)
            {
                Rect dropHint = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true));
                GUI.Box(dropHint, "프리팹을 이 칸 어디에든 끌어다 놓으면 추가", dropStyle);
            }
        }

        EditorGUILayout.EndVertical();

        HandlePaletteDrop(GUILayoutUtility.GetLastRect(), palette, index, typeColor);
        return action;
    }

    private void ApplyPaletteAction(List<MapPalette> palettes, PaletteAction action, int index)
    {
        switch (action)
        {
            case PaletteAction.MoveUp:
                MovePalette(palettes, index, index - 1);
                break;
            case PaletteAction.MoveDown:
                MovePalette(palettes, index, index + 1);
                break;
            case PaletteAction.Delete:
                MapPalette target = palettes[index];
                bool confirmed = target.prefabs.Count == 0 || EditorUtility.DisplayDialog("팔레트 삭제",
                    $"'{target.name}' 팔레트를 지웁니다. 프리팹 {target.prefabs.Count}개의 연결이 빠집니다.\n" +
                    "(프리팹 파일과 이미 맵에 놓인 오브젝트는 그대로입니다. Ctrl+Z로 되돌릴 수 있습니다)",
                    "삭제", "취소");
                if (confirmed) DeletePalette(palettes, index);
                break;
        }

        // 순서가 바뀌거나 항목이 빠진 채로 이번 프레임을 계속 그리면 레이아웃이 어긋나므로 여기서 끝냅니다.
        GUIUtility.ExitGUI();
    }

    private void MovePalette(List<MapPalette> palettes, int from, int to)
    {
        if (to < 0 || to >= palettes.Count) return;

        RecordLibrary("Move Palette");
        (palettes[from], palettes[to]) = (palettes[to], palettes[from]);

        // 펼침 상태는 번호로 기억하므로 함께 바꿉니다.
        bool fromOpen = SessionState.GetBool(FoldKey(from), true);
        bool toOpen = SessionState.GetBool(FoldKey(to), true);
        SessionState.SetBool(FoldKey(from), toOpen);
        SessionState.SetBool(FoldKey(to), fromOpen);

        Undo.RecordObject(builder, "Move Palette");
        if (builder.selectedPaletteIndex == from) builder.selectedPaletteIndex = to;
        else if (builder.selectedPaletteIndex == to) builder.selectedPaletteIndex = from;
    }

    // 펼침 상태는 맵 종류별로 따로 기억합니다. 번호만 쓰면 던전 0번과 필드 0번이 같은 상태를 공유합니다.
    private string FoldKey(int index) => $"{FoldPaletteKeyPrefix}{builder.mapKind}.{index}";

    private void Select(int paletteIndex, int itemIndex)
    {
        Undo.RecordObject(builder, "Select Palette Item");
        builder.selectedPaletteIndex = paletteIndex;
        builder.selectedItemIndex = itemIndex;
    }

    private void AddPalette(List<MapPalette> palettes)
    {
        RecordLibrary("Add Palette");
        palettes.Add(new MapPalette());
        SessionState.SetBool(FoldKey(palettes.Count - 1), true);
        Select(palettes.Count - 1, 0);
    }

    private void DeletePalette(List<MapPalette> palettes, int index)
    {
        RecordLibrary("Delete Palette");
        palettes.RemoveAt(index);

        Undo.RecordObject(builder, "Delete Palette");
        if (builder.selectedPaletteIndex == index)
        {
            builder.selectedPaletteIndex = 0;
            builder.selectedItemIndex = 0;
        }
        else if (builder.selectedPaletteIndex > index)
        {
            builder.selectedPaletteIndex--;
        }
    }

    private void ShowItemContextMenu(MapPalette palette, int paletteIndex, int itemIndex)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("팔레트에서 빼기"), false, () =>
        {
            RecordLibrary("Remove Palette Item");
            palette.prefabs.RemoveAt(itemIndex);

            if (builder.selectedPaletteIndex == paletteIndex && builder.selectedItemIndex >= palette.prefabs.Count)
            {
                Undo.RecordObject(builder, "Remove Palette Item");
                builder.selectedItemIndex = Mathf.Max(0, palette.prefabs.Count - 1);
            }
            Repaint();
        });
        menu.AddItem(new GUIContent("Project 창에서 보기"), false, () => EditorGUIUtility.PingObject(palette.prefabs[itemIndex]));
        menu.ShowAsContext();
    }

    /// <summary>
    /// 썸네일 버튼 그리드. 선택된 칸은 팔레트 타입 색으로 칠합니다. 고른 번호를 돌려줍니다.
    /// 이름이 칸보다 길면 뒤를 '…'로 줄이고, 전체 이름은 마우스를 올리면 보입니다.
    /// </summary>
    private int DrawThumbnailGrid(IList<GameObject> prefabs, int selected, Color selectedColor, System.Action<int> onContextClick)
    {
        const float spacing = 4f;
        float available = EditorGUIUtility.currentViewWidth - 64f;
        int columns = Mathf.Max(1, Mathf.FloorToInt((available + spacing) / (ThumbnailSize + spacing)));
        float labelWidth = ThumbnailSize - paletteButtonStyle.padding.horizontal - 2f;
        int result = selected;

        for (int start = 0; start < prefabs.Count; start += columns)
        {
            EditorGUILayout.BeginHorizontal();
            for (int i = start; i < Mathf.Min(start + columns, prefabs.Count); i++)
            {
                if (i > start) GUILayout.Space(spacing);

                GameObject prefab = prefabs[i];
                string fullName = prefab != null ? prefab.name : "(삭제됨)";
                string tooltip = prefab != null ? prefab.name : "프리팹이 삭제되었습니다. 우클릭해서 팔레트에서 빼세요.";
                GUIContent content = new GUIContent(FitText(fullName, paletteButtonStyle, labelWidth), Thumbnail(prefab), tooltip);

                Rect rect = GUILayoutUtility.GetRect(ThumbnailSize, ThumbnailSize + 14f,
                    GUILayout.Width(ThumbnailSize), GUILayout.Height(ThumbnailSize + 14f));

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
            GUILayout.Space(2f);
        }

        return result;
    }

    /// <summary>글자가 폭을 넘으면 뒤를 잘라 '…'를 붙입니다. 가운데가 잘려 앞뒤가 사라지는 것보다 알아보기 쉽습니다.</summary>
    private static string FitText(string text, GUIStyle style, float width)
    {
        if (string.IsNullOrEmpty(text) || style.CalcSize(new GUIContent(text)).x <= width) return text;

        for (int length = text.Length - 1; length > 0; length--)
        {
            string trimmed = text.Substring(0, length) + "…";
            if (style.CalcSize(new GUIContent(trimmed)).x <= width) return trimmed;
        }

        return "…";
    }

    private static Texture Thumbnail(GameObject prefab)
    {
        if (prefab == null) return null;

        Texture preview = AssetPreview.GetAssetPreview(prefab);
        return preview != null ? preview : AssetPreview.GetMiniThumbnail(prefab);
    }

    /// <summary>
    /// 팔레트 칸 어디에든 프리팹을 끌어다 놓으면 그 팔레트에 추가합니다. 이미 있는 것은 건너뜁니다.
    /// 끌고 있는 동안 칸을 팔레트 타입 색으로 옅게 칠해 어디에 들어갈지 보여줍니다.
    /// </summary>
    private void HandlePaletteDrop(Rect area, MapPalette palette, int paletteIndex, Color typeColor)
    {
        Event e = Event.current;
        bool hovering = area.Contains(e.mousePosition) && DragAndDrop.objectReferences.Length > 0;

        if (e.type == EventType.Repaint && hovering && DragAndDrop.visualMode == DragAndDropVisualMode.Copy)
            EditorGUI.DrawRect(area, new Color(typeColor.r, typeColor.g, typeColor.b, 0.18f));

        if ((e.type != EventType.DragUpdated && e.type != EventType.DragPerform) || !area.Contains(e.mousePosition))
            return;

        bool anyPrefab = false;
        foreach (Object obj in DragAndDrop.objectReferences)
            if (obj is GameObject go && PrefabUtility.IsPartOfPrefabAsset(go)) anyPrefab = true;

        DragAndDrop.visualMode = anyPrefab ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

        if (e.type == EventType.DragPerform && anyPrefab)
        {
            DragAndDrop.AcceptDrag();
            RecordLibrary("Add Palette Item");

            foreach (Object obj in DragAndDrop.objectReferences)
            {
                if (!(obj is GameObject go) || !PrefabUtility.IsPartOfPrefabAsset(go)) continue;
                if (palette.prefabs.Contains(go)) continue;

                palette.prefabs.Add(go);
            }

            Select(paletteIndex, palette.prefabs.Count - 1);
        }

        Repaint();
        e.Use();
    }

    private void DrawPlacementBox()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("배치", sectionStyle);

        if (IsGridPlacement)
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
        string text = IsGridPlacement
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
        DrawFoldout(FoldMapSettingsKey, "맵 설정 (크기 · 저장 경로 · 컨테이너 · 팔레트 파일)",
            "mapWidth", "mapHeight", "savePath", "tileContainer", "paletteLibrary");

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
    /// 여기서만 SerializedObject를 써서, 프리팹·파일 연결을 유니티 기본 UI 그대로 쓰게 합니다.
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

        // 맵 컨테이너와 같은 이유로 MapBuilder의 자식으로 두지 않습니다 — 선택 외곽선이 맵 크기만큼 그려집니다.
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = GuidePlaneName;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(plane, builder.gameObject.scene);

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
        // 예전 버전은 MapBuilder 아래에 만들었으므로 그 자리도 함께 찾습니다.
        Transform legacy = builder.transform.Find(GuidePlaneName);
        if (legacy != null) DestroyImmediate(legacy.gameObject);

        foreach (GameObject root in builder.gameObject.scene.GetRootGameObjects())
            if (root.name == GuidePlaneName) DestroyImmediate(root);
    }

    // ── 씬 뷰 ─────────────────────────────────────────────────────────

    private void OnSceneGUI()
    {
        Event e = Event.current;
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
            if (IsGridPlacement)
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

        if (IsGridPlacement) HandleGridScene(e);
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

        MapPalette palette = CurrentPalette;
        GameObject prefab = CurrentPrefab;
        string color = ColorUtility.ToHtmlStringRGB(TypeColor(CurrentType));
        string what;
        if (palette == null) what = "(팔레트를 고르세요)";
        else if (!MapBuilder.IsTypeAllowed(palette.type, builder.mapKind)) what = $"{palette.name} · ⚠ {KindLabel(builder.mapKind)}에서 못 쓰는 타입";
        else what = $"{palette.name} · {(prefab != null ? prefab.name : "(프리팹 없음)")}";
        string rotation = rotationNames[builder.currentRotation];
        string detail = IsGridPlacement
            ? $"높이 {builder.currentHeight}층 · 브러시 {builder.brushSize} · 회전 {rotation}"
            : $"높이 {builder.freeHeight:0.##} m · 회전 {rotation}";

        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10f, 10f, 320f, 60f));
        GUILayout.Label($"[{KindLabel(builder.mapKind)}] <b><color=#{color}>{PlacementLabel(CurrentType, builder.mapKind)}</color></b> · {what}\n{detail}", hudStyle);
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    private void DrawGrid(float y, float alpha)
    {
        Handles.color = new Color(1f, 1f, 1f, alpha);
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

    private void HandleGridScene(Event e)
    {
        float currentYHeight = builder.currentHeight * GridSystem.Instance.TileHeight;
        DrawGrid(currentYHeight, 0.15f);

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

        Handles.color = GridColor;
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
        DrawGrid(y, 0.07f);

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
        Handles.color = TypeColor(CurrentType);
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

        Transform parent = MapBuilderContainers.GetOrCreate(builder.tileContainer, MapBuilder.ContainerName(CurrentType, builder.mapKind));
        GameObject placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        placed.transform.SetPositionAndRotation(position, rotation);
        Undo.RegisterCreatedObjectUndo(placed, "Place Free Object");
    }

    /// <summary>마우스 아래 오브젝트 중 '지금 팔레트 타입의 묶음'에 속한 것 하나를 지웁니다. 타일이나 다른 묶음은 건드리지 않습니다.</summary>
    private void RemoveFreeUnderMouse(Vector2 mousePosition)
    {
        Transform container = MapBuilderContainers.Find(builder.tileContainer, MapBuilder.ContainerName(CurrentType, builder.mapKind));
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

        Transform parent = MapBuilderContainers.GetOrCreate(builder.tileContainer, MapBuilder.ContainerName(CurrentType, builder.mapKind));
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

        // 원본 루트는 곧 지워지므로, 그 전에 맵 종류와 개척 장식 묶음이 무엇이었는지 읽어 둡니다.
        LocationType kind = MapBuilderContainers.ReadKind(loadedMap);
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

        // 불러온 맵의 종류로 탭을 맞춥니다. 필드 맵을 던전 탭 그대로 구우면 SceneLocation이 떨어져 나갑니다.
        if (builder.mapKind != kind) SetKind(kind);
        int sorted = MapBuilderContainers.SortLoadedChildren(builder, PalettesOf(kind), kind, decorationRoot);

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

        // 필드·마을은 바닥이 (0,0) 칸에서 시작해야 합니다. 던전은 DungeonGenerator가 원점으로 옮겨주지만
        // 씬에 놓는 맵은 그대로 쓰여서, 음수 칸의 바닥은 그리드에서 빠져 NPC가 가지 못합니다.
        if (builder.mapKind != LocationType.Dungeon &&
            LocationGridBuilder.TryGetFloorMinCell(builder.tileContainer.gameObject, out Vector2Int minCell) &&
            minCell != Vector2Int.zero)
        {
            bool bakeAnyway = EditorUtility.DisplayDialog("바닥 시작 위치 확인",
                $"바닥이 (0,0) 칸이 아니라 {minCell} 칸에서 시작합니다.\n\n" +
                (minCell.x < 0 || minCell.y < 0
                    ? "음수 칸의 바닥은 게임에서 그리드에 들어가지 않아 NPC가 그쪽으로 가지 못합니다."
                    : "동작은 하지만 앞쪽 칸만큼 그리드가 낭비됩니다.") +
                "\n\n그래도 굽을까요?",
                "굽기", "취소");
            if (!bakeAnyway) return;
        }

        // 개척 장식이 있으면 루트에 MapDecoration을 붙이고 DecorationRoot를 연결합니다.
        MapBuilderContainers.ApplyDecoration(builder.tileContainer);

        // 필드·마을은 루트에 SceneLocation, 장애물 묶음에 GridBlocker를 붙이고, 던전은 SceneLocation을 뗍니다.
        MapBuilderContainers.ApplySceneSetup(builder.tileContainer, builder.mapKind);

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
