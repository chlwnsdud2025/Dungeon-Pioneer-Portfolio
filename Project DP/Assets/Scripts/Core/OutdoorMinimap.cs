using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 던전 밖 미니맵. 맵에 들어올 때 위에서 한 번 찍어(색·높이) 미니맵답게 가공해 두고,
/// 매 프레임 플레이어 주변 창만 잘라 그 위에 마커를 얹어 보여줍니다.
///
/// 찍을 때는 기능이 없는 전용 URP 렌더러로, 모든 물체의 머티리얼을 조명 없는 전용 셰이더로 잠깐 바꿔 끼워 그립니다.
/// 그래서 낮밤·그림자·후처리·구름 그림자와 상관없이 늘 같은 그림이 나옵니다.
/// 캐릭터(Player·NPC·Enemy 레이어)는 찍지 않고 마커로 따로 그립니다.
/// </summary>
internal class OutdoorMinimap
{
    private static readonly int HeightRangeId = Shader.PropertyToID("_MinimapHeightRange");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
    private static readonly int WorldTilingId = Shader.PropertyToID("_WorldTiling");
    private static readonly string[] MarkerLayers = { "Player", "NPC", "Enemy", "UI" };

    private const int MaxCaptureSize = 4096;
    private const float EnemyRescanSeconds = 1f;

    public SceneLocation Source { get; }
    public Texture ViewTexture => view != null ? view.texture : null;

    private readonly MinimapManager settings;

    // 찍은 그림 (맵 전체). 가공 설정만 바뀌면 다시 찍지 않고 raw에서 다시 가공할 수 있게 원본을 남겨 둡니다.
    private Color32[] rawColor;
    private float[] heights;       // 월드 높이
    private Color32[] background;
    private int mapWidth, mapHeight;
    private Vector2 mapOriginXZ;   // 그림 왼쪽 아래 모서리의 월드 XZ
    private float worldPerPixel;
    private float tileSize;
    private float heightMin, heightRange;

    // 보여줄 창
    private MinimapCanvas view;
    private readonly List<MinimapMarker> markers = new List<MinimapMarker>();
    private Vector2Int lastOrigin;
    private int lastSignature;
    private bool hasFrame;

    // 마커 대상
    private readonly List<Transform> portals = new List<Transform>();
    private readonly List<Transform> dungeonEntrances = new List<Transform>();
    private readonly List<EnemyController> enemies = new List<EnemyController>();
    private float enemyRescanTimer;

    public OutdoorMinimap(MinimapManager settings, SceneLocation source)
    {
        this.settings = settings;
        Source = source;
    }

    public void Dispose()
    {
        view?.Destroy();
        view = null;
        rawColor = null;
        heights = null;
        background = null;
    }

    // ── 찍기 ────────────────────────────────────────────────

    /// <summary>맵을 위에서 찍어 가공까지 끝냅니다. 준비가 안 돼 있으면 에러를 남기고 false.</summary>
    public bool Capture()
    {
        if (!TryGetCaptureRendererIndex(out int rendererIndex)) return false;

        if (settings.outdoorCaptureColorShader == null || settings.outdoorCaptureHeightShader == null)
        {
            Debug.LogError("[MinimapManager] 던전 밖 미니맵을 찍을 셰이더가 연결되지 않았습니다. " +
                           "Outdoor Map의 Capture Color/Height Shader에 Hidden/Minimap/CaptureColor·CaptureHeight를 넣어 주세요.", settings);
            return false;
        }

        int layerMask = settings.outdoorCaptureLayers & ~LayerMask.GetMask(MarkerLayers);
        List<Renderer> drawn = new List<Renderer>();
        List<Renderer> hidden = new List<Renderer>();
        CollectRenderers(layerMask, drawn, hidden);

        if (!TryGetMapBounds(drawn, out Bounds bounds))
        {
            Debug.LogError($"[MinimapManager] '{Source.name}' 아래에서 찍을 물체를 찾지 못해 던전 밖 미니맵을 만들 수 없습니다.", Source);
            return false;
        }

        SetUpMapArea(bounds);

        Camera camera = CreateCaptureCamera(rendererIndex, layerMask, bounds);
        try
        {
            rawColor = Render(camera, drawn, hidden, settings.outdoorCaptureColorShader, true);

            Shader.SetGlobalVector(HeightRangeId, new Vector4(heightMin, 1f / heightRange, 0f, 0f));
            ushort[] encoded = MinimapImageProcessing.DecodeHeight(Render(camera, drawn, hidden, settings.outdoorCaptureHeightShader, false));
            heights = MinimapImageProcessing.ToWorldHeights(encoded, heightMin, heightRange);
        }
        finally
        {
            Object.Destroy(camera.gameObject);
        }

        RebuildBackground();

        int viewSize = Mathf.Max(1, settings.outdoorViewTiles * settings.outdoorPixelsPerTile);
        view = new MinimapCanvas(viewSize, viewSize);

        CollectMarkerTargets();
        return true;
    }

    /// <summary>
    /// 찍기 전용 렌더러가 지금 URP 설정 파일의 몇 번째 렌더러인지 찾습니다. 게임 렌더러로 찍으면
    /// HBAO·SSR·GI·볼륨 안개 같은 기능이 그림에 섞이므로, 못 찾으면 찍지 않습니다.
    /// </summary>
    private bool TryGetCaptureRendererIndex(out int index)
    {
        index = -1;

        var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (asset == null)
        {
            Debug.LogError("[MinimapManager] URP 설정 파일을 찾지 못해 던전 밖 미니맵을 찍을 수 없습니다.", settings);
            return false;
        }

        if (settings.outdoorCaptureRenderer == null)
        {
            Debug.LogError("[MinimapManager] Outdoor Map의 Capture Renderer에 찍기 전용 렌더러(MinimapCapture_Renderer)를 넣어 주세요.", settings);
            return false;
        }

        System.ReadOnlySpan<ScriptableRendererData> renderers = asset.rendererDataList;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != settings.outdoorCaptureRenderer) continue;
            index = i;
            return true;
        }

        Debug.LogError($"[MinimapManager] 지금 쓰는 URP 설정 파일 '{asset.name}'의 Renderer List에 " +
                       $"'{settings.outdoorCaptureRenderer.name}'이(가) 없어 던전 밖 미니맵을 찍을 수 없습니다. 목록에 추가해 주세요.", asset);
        return false;
    }

    /// <summary>
    /// 찍을 렌더러(머티리얼을 바꿔 끼워 그림)와 잠깐 숨길 렌더러를 나눕니다.
    /// 숨기는 것: 마커로 따로 그리는 오브젝트(캐릭터·포탈·던전 입구), 파티클·글자처럼 모양을 알 수 없는 것.
    /// 레이어가 빠진 렌더러는 카메라가 알아서 거르므로 건드리지 않습니다.
    /// </summary>
    private static void CollectRenderers(int layerMask, List<Renderer> drawn, List<Renderer> hidden)
    {
        HashSet<Transform> markerRoots = CollectMarkerObjectRoots();

        foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!renderer.enabled || renderer.forceRenderingOff) continue;
            if ((layerMask & (1 << renderer.gameObject.layer)) == 0) continue;

            bool isMesh = renderer is MeshRenderer || renderer is SkinnedMeshRenderer;
            bool drawable = isMesh && renderer.GetComponent<TMPro.TMP_Text>() == null
                            && !IsMarkerObject(renderer.transform, markerRoots);

            if (drawable) drawn.Add(renderer);
            else hidden.Add(renderer);
        }
    }

    /// <summary>
    /// 마커로 따로 그리는 오브젝트인지. 레이어가 아니라 오브젝트로 가립니다. 루트만 전용 레이어이고 메시는
    /// 다른 레이어인 경우가 많기 때문입니다 (플레이어 몸체는 Torch 레이어, NPC 몸체는 Default 레이어).
    /// </summary>
    private static bool IsMarkerObject(Transform target, HashSet<Transform> markerRoots)
    {
        if (target.GetComponentInParent<PlayerController>(true) != null) return true;
        if (target.GetComponentInParent<NpcActor>(true) != null) return true;
        if (target.GetComponentInParent<EnemyController>(true) != null) return true;

        for (Transform t = target; t != null; t = t.parent)
        {
            if (markerRoots.Contains(t)) return true;
        }

        return false;
    }

    /// <summary>포탈·던전 입구처럼 아이콘으로 대신 그리는 오브젝트의 모델 루트들.</summary>
    private static HashSet<Transform> CollectMarkerObjectRoots()
    {
        var roots = new HashSet<Transform>();

        foreach (LocationPortal portal in Object.FindObjectsByType<LocationPortal>(FindObjectsSortMode.None))
            roots.Add(portal.transform);

        foreach (DungeonEntrance entrance in Object.FindObjectsByType<DungeonEntrance>(FindObjectsSortMode.None))
        {
            roots.Add(entrance.transform);

            // 던전 입구 컴포넌트는 입구 모델의 자식(트리거)에 붙습니다. 부모가 모델(렌더러가 있음)이면 모델도 뺍니다.
            // 부모가 Obstacles 같은 묶음(렌더러 없음)이면 건드리지 않아 다른 장애물이 같이 빠지지 않습니다.
            Transform parent = entrance.transform.parent;
            if (parent != null && parent.GetComponent<Renderer>() != null) roots.Add(parent);
        }

        return roots;
    }

    /// <summary>찍을 범위 = 이 맵(SceneLocation) 아래에 있는, 찍힐 물체들을 모두 담는 상자.</summary>
    private bool TryGetMapBounds(List<Renderer> drawn, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        Transform root = Source.transform;

        foreach (Renderer renderer in drawn)
        {
            if (!renderer.transform.IsChildOf(root)) continue;

            if (!found) bounds = renderer.bounds;
            else bounds.Encapsulate(renderer.bounds);
            found = true;
        }

        return found;
    }

    private void SetUpMapArea(Bounds bounds)
    {
        tileSize = GridSystem.Instance.TileSize;
        float margin = settings.outdoorCaptureMarginTiles * tileSize;
        float width = bounds.size.x + margin * 2f;
        float depth = bounds.size.z + margin * 2f;

        worldPerPixel = tileSize / Mathf.Max(1, settings.outdoorPixelsPerTile);

        // 아주 큰 맵이면 텍스처 한계를 넘지 않도록 해상도를 낮춥니다.
        float largest = Mathf.Max(width, depth) / worldPerPixel;
        if (largest > MaxCaptureSize)
        {
            worldPerPixel *= largest / MaxCaptureSize;
            Debug.LogWarning($"[MinimapManager] '{Source.name}'이(가) 커서 미니맵 해상도를 한 칸 {tileSize / worldPerPixel:0.#}픽셀로 낮춥니다.", Source);
        }

        mapWidth = Mathf.Max(1, Mathf.CeilToInt(width / worldPerPixel));
        mapHeight = Mathf.Max(1, Mathf.CeilToInt(depth / worldPerPixel));
        mapOriginXZ = new Vector2(bounds.min.x - margin, bounds.min.z - margin);

        // 가장 낮은 물체도 0보다 큰 높이로 찍히도록(0 = 아무것도 없음) 아래위로 여유를 둡니다.
        heightMin = bounds.min.y - 1f;
        heightRange = Mathf.Max(0.01f, bounds.max.y + 1f - heightMin);
    }

    private Camera CreateCaptureCamera(int rendererIndex, int layerMask, Bounds bounds)
    {
        var go = new GameObject("MinimapCaptureCamera") { hideFlags = HideFlags.HideAndDontSave };
        Camera camera = go.AddComponent<Camera>();
        camera.enabled = false;   // 직접 Render할 때만 그림

        float worldWidth = mapWidth * worldPerPixel;
        float worldDepth = mapHeight * worldPerPixel;
        float top = bounds.max.y + 10f;

        // 정확히 아래를 봅니다. 이때 화면 위쪽 = 월드 +Z, 오른쪽 = +X라 던전 미니맵과 방향이 같습니다.
        camera.transform.SetPositionAndRotation(
            new Vector3(mapOriginXZ.x + worldWidth * 0.5f, top, mapOriginXZ.y + worldDepth * 0.5f),
            Quaternion.Euler(90f, 0f, 0f));

        camera.orthographic = true;
        camera.orthographicSize = worldDepth * 0.5f;
        camera.aspect = worldWidth / worldDepth;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = top - heightMin + 10f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);   // 알파 0 = 아무것도 안 찍힌 곳
        camera.cullingMask = layerMask;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.useOcclusionCulling = false;

        UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
        data.SetRenderer(rendererIndex);
        data.renderPostProcessing = false;
        data.renderShadows = false;
        data.antialiasing = AntialiasingMode.None;
        data.requiresColorTexture = false;
        data.requiresDepthTexture = false;

        return camera;
    }

    /// <summary>찍을 렌더러의 머티리얼을 shader로 잠깐 바꿔 끼워 한 장 찍고, 모두 원래대로 돌려놓습니다.</summary>
    private Color32[] Render(Camera camera, List<Renderer> drawn, List<Renderer> hidden, Shader shader, bool sRGB)
    {
        var originals = new Material[drawn.Count][];
        var created = new Dictionary<Material, Material>();
        Material missing = null;
        RenderTexture target = null;
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            for (int i = 0; i < drawn.Count; i++)
            {
                Material[] materials = drawn[i].sharedMaterials;
                originals[i] = materials;

                var replaced = new Material[materials.Length];
                for (int m = 0; m < materials.Length; m++)
                {
                    if (materials[m] == null)
                    {
                        // 머티리얼이 빠진 칸은 게임 화면처럼 마젠타로 찍습니다.
                        if (missing == null) missing = CreateCaptureMaterial(null, shader);
                        replaced[m] = missing;
                    }
                    else if (!created.TryGetValue(materials[m], out replaced[m]))
                    {
                        replaced[m] = CreateCaptureMaterial(materials[m], shader);
                        created[materials[m]] = replaced[m];
                    }
                }

                drawn[i].sharedMaterials = replaced;
            }

            foreach (Renderer renderer in hidden) renderer.forceRenderingOff = true;

            target = RenderTexture.GetTemporary(mapWidth, mapHeight, 24, RenderTextureFormat.ARGB32,
                                                sRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;
            var readback = new Texture2D(mapWidth, mapHeight, TextureFormat.RGBA32, false, !sRGB);
            readback.ReadPixels(new Rect(0, 0, mapWidth, mapHeight), 0, 0);
            readback.Apply(false);

            Color32[] pixels = readback.GetPixels32();
            Object.Destroy(readback);
            return pixels;
        }
        finally
        {
            RenderTexture.active = previousActive;
            camera.targetTexture = null;
            if (target != null) RenderTexture.ReleaseTemporary(target);

            for (int i = 0; i < drawn.Count; i++)
            {
                if (originals[i] != null && drawn[i] != null) drawn[i].sharedMaterials = originals[i];
            }

            foreach (Renderer renderer in hidden)
            {
                if (renderer != null) renderer.forceRenderingOff = false;
            }

            foreach (Material material in created.Values) Object.Destroy(material);
            if (missing != null) Object.Destroy(missing);
        }
    }

    /// <summary>
    /// 원래 머티리얼의 색 텍스처·색·알파 컷을 옮긴 찍기용 머티리얼. source가 null이면 마젠타.
    /// URP Lit 규칙(_BaseMap × _BaseColor) 말고도, 셰이더 그래프에서 흔한 _MainTex·_Tint와
    /// 월드 좌표로 붙이는 트라이플래너(_Tiling)도 알아봅니다. (오버월드 바닥 TEST_TRIPLANAR_LIT이 이 경우)
    /// </summary>
    private static Material CreateCaptureMaterial(Material source, Shader shader)
    {
        var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

        if (source == null)
        {
            material.SetColor(BaseColorId, Color.magenta);
            return material;
        }

        // HasProperty는 머티리얼에 남은 옛 값이 아니라 지금 셰이더에 있는 속성인지를 봅니다.
        string textureName = source.HasProperty("_BaseMap") ? "_BaseMap" : source.HasProperty("_MainTex") ? "_MainTex" : null;
        if (textureName != null)
        {
            material.SetTexture(BaseMapId, source.GetTexture(textureName));
            material.SetTextureScale(BaseMapId, source.GetTextureScale(textureName));
            material.SetTextureOffset(BaseMapId, source.GetTextureOffset(textureName));
        }

        // 색을 곱하는 속성은 있는 대로 모두 곱합니다.
        Color color = Color.white;
        if (source.HasProperty("_BaseColor")) color *= source.GetColor("_BaseColor");
        else if (source.HasProperty("_Color")) color *= source.GetColor("_Color");
        if (source.HasProperty("_Tint")) color *= source.GetColor("_Tint");
        material.SetColor(BaseColorId, color);

        material.SetFloat(WorldTilingId, IsTriplanar(source) ? TriplanarTiling(source) : 0f);

        bool alphaClip = (source.HasProperty("_AlphaClip") && source.GetFloat("_AlphaClip") > 0.5f)
                         || source.IsKeywordEnabled("_ALPHATEST_ON");
        material.SetFloat(CutoffId, alphaClip && source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : -1f);

        return material;
    }

    private static bool IsTriplanar(Material source)
    {
        if (source.IsKeywordEnabled("_MAINTEXUSEUV")) return false;

        return source.HasProperty("_TriplanarBlend")
               || source.shader.name.IndexOf("Triplanar", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>트라이플래너의 텍스처 반복 비율 (월드 1m당). 셰이더 그래프 Triplanar 노드의 Tile 값입니다.</summary>
    private static float TriplanarTiling(Material source)
    {
        if (source.HasFloat("_Tiling")) return Mathf.Max(0.0001f, source.GetFloat("_Tiling"));
        if (source.HasVector("_Tiling")) return Mathf.Max(0.0001f, source.GetVector("_Tiling").x);
        return 1f;
    }

    // ── 가공 ────────────────────────────────────────────────

    /// <summary>찍어 둔 원본(색·높이)을 손으로 칠한 지도처럼 가공합니다.</summary>
    private void RebuildBackground()
    {
        // 그림자는 맵에서 가장 높은 물체가 드리울 길이까지만, 그래도 6칸은 넘지 않게 봅니다.
        float sunElevation = Mathf.Clamp(settings.outdoorSunElevation, 5f, 89f);
        float shadowDistance = Mathf.Min(heightRange / Mathf.Tan(sunElevation * Mathf.Deg2Rad), tileSize * 6f);

        var style = new MinimapPaintStyle
        {
            brushRadius = settings.outdoorBrushRadius,
            saturation = settings.outdoorSaturation,
            brightness = settings.outdoorBrightness,
            paperColor = settings.outdoorPaperColor,
            paperAmount = settings.outdoorPaperAmount,
            sunAzimuth = settings.outdoorSunAzimuth,
            sunElevation = sunElevation,
            shadeStrength = settings.outdoorShadeStrength,
            shadowStrength = settings.outdoorShadowStrength,
            shadowMaxDistance = shadowDistance,
            reliefSoftness = Mathf.Max(1, Mathf.RoundToInt(tileSize * 0.1f / worldPerPixel)),   // 한 칸의 1/10
            outlineStrength = settings.outdoorOutlineStrength,
            outlineHeightThreshold = settings.outdoorOutlineHeightThreshold,
            outlineThickness = settings.outdoorOutlineThickness,
            emptyColor = settings.outdoorEmptyColor
        };

        background = new Color32[mapWidth * mapHeight];
        MinimapImageProcessing.Paint(rawColor, heights, mapWidth, mapHeight, worldPerPixel, style, background);
        hasFrame = false;
    }

    // ── 보여주기 ────────────────────────────────────────────

    /// <summary>플레이어 주변 창을 잘라 마커를 얹습니다. 창이나 마커가 바뀐 프레임에만 텍스처를 다시 올립니다.</summary>
    public void Update(float deltaTime, Transform player)
    {
        if (view == null) return;

        enemyRescanTimer -= deltaTime;
        if (enemyRescanTimer <= 0f) RescanEnemies();

        Vector2 playerPixel = player != null ? WorldToPixel(player.position) : new Vector2(mapWidth * 0.5f, mapHeight * 0.5f);
        var origin = new Vector2Int(
            MinimapImageProcessing.WindowStart(playerPixel.x, mapWidth, view.width),
            MinimapImageProcessing.WindowStart(playerPixel.y, mapHeight, view.height));

        int signature = CollectMarkers(player);
        if (hasFrame && origin == lastOrigin && signature == lastSignature) return;

        MinimapImageProcessing.CopyWindow(background, mapWidth, mapHeight, origin.x, origin.y,
                                          view.pixels, view.width, view.height, settings.outdoorEmptyColor);

        foreach (MinimapMarker marker in markers)
            view.Draw(marker, origin);

        view.Upload();

        lastOrigin = origin;
        lastSignature = signature;
        hasFrame = true;
    }

    private void CollectMarkerTargets()
    {
        portals.Clear();
        foreach (LocationPortal portal in Object.FindObjectsByType<LocationPortal>(FindObjectsSortMode.None))
            portals.Add(portal.transform);

        dungeonEntrances.Clear();
        foreach (DungeonEntrance entrance in Object.FindObjectsByType<DungeonEntrance>(FindObjectsSortMode.None))
            dungeonEntrances.Add(entrance.transform);

        RescanEnemies();
    }

    /// <summary>나중에 생기는 적도 보이도록 주기적으로 다시 찾습니다.</summary>
    private void RescanEnemies()
    {
        enemies.Clear();
        enemies.AddRange(Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None));
        enemyRescanTimer = EnemyRescanSeconds;
    }

    /// <summary>
    /// 이번 프레임에 얹을 마커를 모읍니다. 안개가 없으니 모두 또렷하게 그립니다.
    /// 나중에 넣은 것이 위에 그려집니다: 포탈 → 던전 입구 → 적 → 플레이어.
    /// 돌려주는 값은 마커 배치의 요약값이라, 지난 프레임과 같으면 다시 그릴 필요가 없습니다.
    /// </summary>
    private int CollectMarkers(Transform player)
    {
        markers.Clear();
        int signature = 17;
        int objectDotRadius = Mathf.Max(3, settings.chestDotRadiusPx + 1);

        foreach (Transform portal in portals)
        {
            if (portal != null && portal.gameObject.activeInHierarchy)
                AddMarker(ref signature, portal.position, settings.portalIcon, settings.portalColor, objectDotRadius);
        }

        foreach (Transform entrance in dungeonEntrances)
        {
            if (entrance != null && entrance.gameObject.activeInHierarchy)
                AddMarker(ref signature, entrance.position, settings.dungeonEntranceIcon, settings.dungeonEntranceColor, objectDotRadius);
        }

        foreach (EnemyController enemy in enemies)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy)
                AddMarker(ref signature, enemy.transform.position, settings.enemyIcon, settings.enemyColor, settings.enemyDotRadiusPx);
        }

        if (player != null)
            AddMarker(ref signature, player.position, null, settings.playerColor, settings.playerDotRadiusPx);

        return signature;
    }

    private void AddMarker(ref int signature, Vector3 worldPos, MinimapManager.MarkerIcon iconSetting, Color fallbackColor, int fallbackRadius)
    {
        Vector2 center = WorldToPixel(worldPos);
        MinimapIconImage icon = settings.GetIcon(iconSetting);

        markers.Add(new MinimapMarker
        {
            center = center,
            icon = icon,
            radius = fallbackRadius,
            color = fallbackColor,
            opacity = 1f
        });

        unchecked
        {
            signature = signature * 31 + Mathf.FloorToInt(center.x);
            signature = signature * 31 + Mathf.FloorToInt(center.y);
            signature = signature * 31 + (icon != null ? icon.GetHashCode() : fallbackRadius);
        }
    }

    private Vector2 WorldToPixel(Vector3 worldPos)
    {
        return new Vector2((worldPos.x - mapOriginXZ.x) / worldPerPixel, (worldPos.z - mapOriginXZ.y) / worldPerPixel);
    }
}
