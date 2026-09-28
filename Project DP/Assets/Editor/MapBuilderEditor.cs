using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapBuilder))]
public class MapBuilderEditor : Editor
{
    private MapBuilder builder;
    private GameObject[] palette;
    private string[] paletteNames = { "Floor", "Wall", "Entrance", "Exit", "Chest", "Trap", "Enemy", "Pushable" };

    // ★ 팀원 피드백 반영: 각도 대신 직관적인 화살표(시선 방향)로 UI 텍스트 변경
    private string[] rotationNames = { "↑ (앞/0°)", "→ (우/90°)", "↓ (뒤/180°)", "← (좌/270°)" };

    private GameObject ghostObject;
    private Material ghostMaterial;
    private int lastGhostIndex = -1;

    // Shift+드래그 한 번(한 붓질)을 Undo 한 단계로 묶기 위한 그룹 번호
    private int strokeUndoGroup = -1;
    private bool strokeBlocked;

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

    private const int TrapPaletteIndex = 5; // paletteNames에서 "Trap"의 위치

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

    // 'Trap' 팔레트 선택 시, 등록된 함정 프리팹 목록에서 하나를 고르는 하위 선택 UI.
    private void DrawTrapVariantSelector()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("🪤 Trap 종류", EditorStyles.miniBoldLabel);

        if (builder.trapPrefabs == null || builder.trapPrefabs.Count == 0)
        {
            EditorGUILayout.HelpBox("MapBuilder의 'Trap Prefabs' 목록이 비어 있습니다. 인스펙터에서 함정 프리팹을 등록하세요.", MessageType.Warning);
            return;
        }

        string[] names = new string[builder.trapPrefabs.Count];
        for (int i = 0; i < names.Length; i++)
            names[i] = builder.trapPrefabs[i] != null ? builder.trapPrefabs[i].name : "(비어있음)";

        int clamped = Mathf.Clamp(builder.selectedTrapIndex, 0, names.Length - 1);
        int newIndex = GUILayout.SelectionGrid(clamped, names, 3);

        if (newIndex != builder.selectedTrapIndex)
        {
            builder.selectedTrapIndex = newIndex;
            lastGhostIndex = -1; // 고스트를 새로 선택한 함정으로 다시 생성하도록 강제
            EditorUtility.SetDirty(builder);
        }
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

    private void UpdateGhost()
    {
        if (palette[builder.selectedPrefabIndex] == null) return;

        if (lastGhostIndex != builder.selectedPrefabIndex || ghostObject == null)
        {
            if (ghostObject != null) DestroyImmediate(ghostObject);

            ghostObject = (GameObject)PrefabUtility.InstantiatePrefab(palette[builder.selectedPrefabIndex]);
            ghostObject.hideFlags = HideFlags.HideAndDontSave;

            foreach (var col in ghostObject.GetComponentsInChildren<Collider>()) col.enabled = false;
            foreach (var renderer in ghostObject.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = ghostMaterial;

            lastGhostIndex = builder.selectedPrefabIndex;
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

    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();

        DrawDefaultInspector();
        UpdatePalette();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("🎨 Tile Palette", EditorStyles.boldLabel);
        builder.selectedPrefabIndex = GUILayout.SelectionGrid(builder.selectedPrefabIndex, paletteNames, 4);

        // 'Trap' 팔레트가 선택됐을 때만, 어떤 함정 종류를 칠할지 하위 선택 UI를 표시합니다.
        if (builder.selectedPrefabIndex == TrapPaletteIndex)
        {
            DrawTrapVariantSelector();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("⚙️ Placement Settings", EditorStyles.boldLabel);

        builder.tileContainer = (Transform)EditorGUILayout.ObjectField("타일 컨테이너 (부모)", builder.tileContainer, typeof(Transform), true);

        builder.currentHeight = EditorGUILayout.IntSlider("설치 높이 (층)", builder.currentHeight, 0, 10);
        // 화살표 방향 UI가 그려지는 곳
        builder.currentRotation = GUILayout.SelectionGrid(builder.currentRotation, rotationNames, 4);

        EditorGUILayout.Space();
        builder.useSelectiveEraser = EditorGUILayout.ToggleLeft(" 선택적 지우개 필터 (현재 선택된 타일만 지움)", builder.useSelectiveEraser);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("🗺️ Map Guide", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("바닥 가이드 Plane 깔기", GUILayout.Height(30))) CreateGuidePlane();
        if (GUILayout.Button("가이드 Plane 지우기", GUILayout.Height(30))) RemoveGuidePlane();
        GUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Scene 뷰 조작법:\n[Shift + 좌클릭] : 타일 설치\n[Shift + 우클릭] : 타일 지우기\n[Shift + R 키] : 타일 90도 우회전 (→)\n[Ctrl + 마우스 휠] : 브러시 크기 조절", MessageType.Info);

        EditorGUILayout.Space();
        if (GUILayout.Button("전체 맵 지우기", GUILayout.Height(30))) ClearMap();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("📂 Load & Save Map", EditorStyles.boldLabel);

        builder.prefabToLoad = (GameObject)EditorGUILayout.ObjectField("불러올 맵 프리팹", builder.prefabToLoad, typeof(GameObject), false);
        if (GUILayout.Button("프리팹 불러오기 (수정 모드)", GUILayout.Height(30)))
        {
            LoadMapFromPrefab();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("프리팹으로 굽기 (저장/덮어쓰기)", GUILayout.Height(40))) SaveMapAsPrefab();

        if (EditorGUI.EndChangeCheck())
        {
            SceneView.RepaintAll();
        }
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
            if (e.delta.y > 0) builder.brushSize = Mathf.Max(1, builder.brushSize - 1);
            else if (e.delta.y < 0) builder.brushSize = Mathf.Min(5, builder.brushSize + 1);

            Repaint();
            SceneView.RepaintAll();
            e.Use();
        }

        float currentYHeight = builder.currentHeight * GridSystem.Instance.TileHeight;

        Handles.color = new Color(1f, 1f, 1f, 0.15f);
        float minBound = -builder.tileSize / 2f;
        float maxXBound = (builder.mapWidth - 1) * builder.tileSize + builder.tileSize / 2f;
        float maxZBound = (builder.mapHeight - 1) * builder.tileSize + builder.tileSize / 2f;

        for (int x = 0; x <= builder.mapWidth; x++)
        {
            float posX = x * builder.tileSize - (builder.tileSize / 2f);
            Handles.DrawLine(new Vector3(posX, currentYHeight, minBound), new Vector3(posX, currentYHeight, maxZBound));
        }
        for (int z = 0; z <= builder.mapHeight; z++)
        {
            float posZ = z * builder.tileSize - (builder.tileSize / 2f);
            Handles.DrawLine(new Vector3(minBound, currentYHeight, posZ), new Vector3(maxXBound, currentYHeight, posZ));
        }

        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, currentYHeight, 0));
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        if (groundPlane.Raycast(ray, out float enter))
        {
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

            Handles.color = Color.green;
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

            if (e.shift)
            {
                int controlID = GUIUtility.GetControlID(FocusType.Passive);
                HandleUtility.AddDefaultControl(controlID);

                if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
                {
                    Undo.IncrementCurrentGroup();
                    Undo.SetCurrentGroupName(e.button == 0 ? "Paint Map Tiles" : "Erase Map Tiles");
                    strokeUndoGroup = Undo.GetCurrentGroup();
                    // 컨테이너를 쓸 수 없으면 경고창을 한 번만 띄우고 이번 붓질은 무시합니다.
                    strokeBlocked = !EnsureEditableContainer();
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
        }
        else
        {
            if (ghostObject != null) ghostObject.SetActive(false);
        }

        if (e.rawType == EventType.MouseUp && strokeUndoGroup >= 0)
        {
            Undo.CollapseUndoOperations(strokeUndoGroup);
            strokeUndoGroup = -1;
        }

        if (e.type == EventType.MouseMove || e.type == EventType.ScrollWheel) SceneView.RepaintAll();
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
        if (palette[builder.selectedPrefabIndex] == null) return;

        if (!EnsureEditableContainer()) return;

        Vector3Int gridPos = new Vector3Int(x, y, z);

        if (builder.placedTiles.ContainsKey(gridPos))
        {
            if (builder.placedTiles[gridPos] != null) Undo.DestroyObjectImmediate(builder.placedTiles[gridPos]);
            builder.placedTiles.Remove(gridPos);
        }

        GameObject newTile = (GameObject)PrefabUtility.InstantiatePrefab(palette[builder.selectedPrefabIndex], builder.tileContainer);
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
                GameObject selectedPrefab = palette[builder.selectedPrefabIndex];

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

        Undo.CollapseUndoOperations(group);
        builder.SyncPlacedTiles();

        Debug.Log($"<color=cyan>맵 불러오기 완료! ({builder.prefabToLoad.name})</color>");
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

        if (AssetDatabase.LoadAssetAtPath<GameObject>(fullPath) != null)
        {
            bool proceed = EditorUtility.DisplayDialog("프리팹으로 굽기",
                $"'{fullPath}'을(를) 현재 씬의 내용으로 덮어씁니다.\n\n" +
                "다른 팀원이 이 프리팹을 직접 수정했다면 그 변경은 사라집니다. " +
                "작업 전에 '프리팹 불러오기'로 최신 버전을 불러왔는지 확인하세요.",
                "덮어쓰기", "취소");
            if (!proceed) return;
        }

        PrefabUtility.SaveAsPrefabAsset(builder.tileContainer.gameObject, fullPath, out bool success);

        if (success) Debug.Log($"<color=green>맵 저장 성공! 경로: {fullPath}</color>");
        else Debug.LogError("맵 저장에 실패했습니다. 경로를 확인해주세요.");
    }
}