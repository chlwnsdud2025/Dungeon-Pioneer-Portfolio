using UnityEngine;

/// <summary>
/// 벽 하나의 투명/불투명 전환을 담당. CameraOcclusionFader가 자동으로 부착합니다.
/// 벽 메시가 자식에 있으므로(WallTile 구조) 자식 Renderer들을 모두 대상으로 합니다.
/// </summary>
[DisallowMultipleComponent]
public class FadeableWall : MonoBehaviour
{
    private Renderer[] renderers;
    private Material[] originalShared;  // 복구용 원본 공유 머티리얼
    private Material[] fadeInstances;   // 이 벽 전용 투명 복제본

    private float currentAlpha = 1f;
    private float targetAlpha = 1f;
    private float speed = 8f;
    private bool transparentMode = false;

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor"); // URP Lit
    private static readonly int ColorID = Shader.PropertyToID("_Color");         // 빌트인 대비

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalShared = new Material[renderers.Length];
        fadeInstances = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalShared[i] = renderers[i].sharedMaterial;
    }

    public void SetTargetAlpha(float alpha, float fadeSpeed)
    {
        targetAlpha = Mathf.Clamp01(alpha);
        speed = fadeSpeed;
        enabled = true; // 페이드 진행을 위해 Update 재개
    }

    private void Update()
    {
        if (Mathf.Approximately(currentAlpha, targetAlpha))
        {
            // 완전 불투명으로 돌아왔으면 원본 머티리얼 복구 + 인스턴스 정리
            if (targetAlpha >= 1f && transparentMode) RestoreOpaque();
            enabled = false; // 변화 없음 → Update 정지(성능)
            return;
        }

        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, speed * Time.deltaTime);

        if (!transparentMode) EnterTransparent();
        ApplyAlpha(currentAlpha);
    }

    private void EnterTransparent()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Material inst = new Material(originalShared[i]);
            ToTransparent(inst);
            fadeInstances[i] = inst;
            renderers[i].material = inst; // 인스턴스로 교체 → 다른 벽엔 영향 없음
        }
        transparentMode = true;
    }

    private void ApplyAlpha(float a)
    {
        foreach (Material m in fadeInstances)
        {
            if (m == null) continue;
            if (m.HasProperty(BaseColorID))
            { Color c = m.GetColor(BaseColorID); c.a = a; m.SetColor(BaseColorID, c); }
            else if (m.HasProperty(ColorID))
            { Color c = m.GetColor(ColorID); c.a = a; m.SetColor(ColorID, c); }
        }
    }

    private void RestoreOpaque()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sharedMaterial = originalShared[i];
            if (fadeInstances[i] != null) Destroy(fadeInstances[i]);
            fadeInstances[i] = null;
        }
        transparentMode = false;
        currentAlpha = 1f;
    }

    // URP Lit/Simple Lit을 런타임에 Transparent(알파 블렌드)로 전환.
    // MapBuilderEditor.cs의 고스트 머티리얼 셋업과 동일한 방식입니다.
    private static void ToTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f);   // 0=Opaque, 1=Transparent
        m.SetFloat("_Blend", 0f);     // 0=Alpha
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void OnDestroy()
    {
        if (fadeInstances == null) return;
        foreach (Material m in fadeInstances)
            if (m != null) Destroy(m);
    }
}