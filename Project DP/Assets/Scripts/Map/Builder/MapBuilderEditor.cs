using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapBuilder))]
public class MapBuilderEditor : Editor
{
    private MapBuilder builder;
    private GameObject[] palette;
    private string[] paletteNames = { "Floor", "Wall", "Entrance", "Exit", "Chest", "Trap", "Enemy", "Pushable" };
    private string[] rotationNames = { "0° (기본)", "90°", "180°", "270°" };

    private GameObject ghostObject;
    private Material ghostMaterial;
    private int lastGhostIndex = -1;

    private void OnEnable()
    {
        builder = (MapBuilder)target;
        UpdatePalette();
        builder.SyncPlacedTiles();
        CreateGhostMaterial();
    }

    private void OnDisable()
    {
        if (ghostObject != null) DestroyImmediate(ghostObject);
        if (ghostMaterial != null) DestroyImmediate(ghostMaterial);
    }

    private void UpdatePalette()
    {
        palette = new GameObject[]
        {
            builder.floorPrefab, builder.wallPrefab, builder.entrancePrefab,
            builder.exitPrefab, builder.chestPrefab, builder.trapPrefab,
            builder.enemyPrefab, builder.pushablePrefab
        };
    }

    private void CreateGhostMaterial()
    {
        Shader shader = Shader.Find("Standard");
        ghostMaterial = new Material(shader);
        ghostMaterial.SetFloat("_Mode", 3); // Transparent
        ghostMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        ghostMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        ghostMaterial.SetInt("_ZWrite", 0);
        ghostMaterial.DisableKeyword("_ALPHATEST_ON");
        ghostMaterial.EnableKeyword("_ALPHABLEND_ON");
        ghostMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        ghostMaterial.renderQueue = 3000;
        ghostMaterial.color = new Color(0.2f, 1f, 0.2f, 0.6f);
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

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        UpdatePalette();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("🎨 Tile Palette", EditorStyles.boldLabel);
        builder.selectedPrefabIndex = GUILayout.SelectionGrid(builder.selectedPrefabIndex, paletteNames, 4);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("⚙️ Placement Settings", EditorStyles.boldLabel);
        builder.currentHeight = EditorGUILayout.IntSlider("설치 높이 (층)", builder.currentHeight, 0, 10);
        builder.currentRotation = GUILayout.SelectionGrid(builder.currentRotation, rotationNames, 4);

        EditorGUILayout.Space();
        builder.useSelectiveEraser = EditorGUILayout.ToggleLeft(" 선택적 지우개 필터 (현재 선택된 타일만 지움)", builder.useSelectiveEraser);

        // ★ 추가된 바닥 가이드 기능
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("🗺️ Map Guide", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("바닥 가이드 Plane 깔기", GUILayout.Height(30))) CreateGuidePlane();
        if (GUILayout.Button("가이드 Plane 지우기", GUILayout.Height(30))) RemoveGuidePlane();
        GUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Scene 뷰 조작법:\n[Shift + 좌클릭] : 타일 설치\n[Shift + 우클릭] : 타일 지우기\n[R 키] : 타일 90도 회전", MessageType.Info);

        EditorGUILayout.Space();
        if (GUILayout.Button("전체 맵 지우기", GUILayout.Height(30))) ClearMap();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("💾 Save Map", EditorStyles.boldLabel);
        if (GUILayout.Button("프리팹으로 굽기 (저장/덮어쓰기)", GUILayout.Height(40))) SaveMapAsPrefab();
    }

    // ★ 임시 바닥을 생성하는 함수
    private void CreateGuidePlane()
    {
        RemoveGuidePlane(); // 중복 생성 방지

        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "Editor_GuidePlane";
        plane.transform.SetParent(builder.transform);

        // 정중앙 좌표 계산
        float centerX = (builder.mapWidth - 1) * builder.tileSize / 2f;
        float centerZ = (builder.mapHeight - 1) * builder.tileSize / 2f;
        // 바닥 타일과 Z-fighting(텍스처 겹침 껌뻑임)이 발생하지 않도록 y값을 살짝 낮춤
        plane.transform.position = new Vector3(centerX, -0.01f, centerZ);

        // 유니티 기본 Plane은 10x10이므로 크기에 맞게 스케일링
        float scaleX = (builder.mapWidth * builder.tileSize) / 10f;
        float scaleZ = (builder.mapHeight * builder.tileSize) / 10f;
        plane.transform.localScale = new Vector3(scaleX, 1, scaleZ);

        // 시각적으로 덜 눈부시게 어두운 회색 머티리얼 적용
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.2f, 0.2f, 0.2f, 1f);
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
        UpdateGhost();

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.R)
        {
            builder.currentRotation = (builder.currentRotation + 1) % 4;
            Repaint();
            e.Use();
        }

        float currentYHeight = builder.currentHeight * builder.tileSize;

        // ★ 허공에서도 볼 수 있는 전체 17x17 그리드 렌더링
        Handles.color = new Color(1f, 1f, 1f, 0.15f); // 희미한 흰색
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
                else
                {
                    ghostObject.SetActive(false);
                }
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

                if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
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

        if (e.type == EventType.MouseMove) SceneView.RepaintAll();
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

        Vector3Int gridPos = new Vector3Int(x, y, z);

        if (builder.placedTiles.ContainsKey(gridPos))
        {
            if (builder.placedTiles[gridPos] != null) DestroyImmediate(builder.placedTiles[gridPos]);
            builder.placedTiles.Remove(gridPos);
        }

        GameObject newTile = (GameObject)PrefabUtility.InstantiatePrefab(palette[builder.selectedPrefabIndex]);
        newTile.transform.position = new Vector3(x * builder.tileSize, spawnY, z * builder.tileSize);
        newTile.transform.rotation = Quaternion.Euler(0, builder.currentRotation * 90f, 0);
        newTile.transform.SetParent(builder.transform);

        builder.placedTiles.Add(gridPos, newTile);
        EditorUtility.SetDirty(builder);
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

            if (targetObj != null) DestroyImmediate(targetObj);
            builder.placedTiles.Remove(gridPos);
            EditorUtility.SetDirty(builder);
        }
    }

    private void ClearMap()
    {
        for (int i = builder.transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = builder.transform.GetChild(i).gameObject;
            // ★ 맵을 지울 때 바닥 가이드는 지워지지 않도록 보호
            if (child.name != "Editor_GuidePlane")
            {
                DestroyImmediate(child);
            }
        }
        builder.placedTiles.Clear();
        EditorUtility.SetDirty(builder);
    }

    private void SaveMapAsPrefab()
    {
        // ★ 프리팹을 굽기 전에 임시 가이드 Plane을 먼저 삭제합니다. (프리팹에 쓰레기 데이터가 남지 않음)
        RemoveGuidePlane();

        if (!System.IO.Directory.Exists(builder.savePath)) System.IO.Directory.CreateDirectory(builder.savePath);
        string fullPath = builder.savePath + builder.mapPrefabName + ".prefab";
        bool success;
        PrefabUtility.SaveAsPrefabAsset(builder.gameObject, fullPath, out success);

        if (success) Debug.Log($"<color=green>맵 저장 성공! 경로: {fullPath}</color>");
        else Debug.LogError("맵 저장에 실패했습니다. 경로를 확인해주세요.");
    }
}