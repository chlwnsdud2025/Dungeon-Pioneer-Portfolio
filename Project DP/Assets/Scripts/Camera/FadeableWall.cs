using UnityEngine;

[DisallowMultipleComponent]
public class FadeableWall : MonoBehaviour
{
    private static readonly int DitherFadeID = Shader.PropertyToID("_DitherFade");

    private Shader ditherShader;                 // CameraOcclusionFader가 넘겨줌
    private Renderer[] renderers;
    private Material[] originalShared;            // 복구용 원본 (공유 머티리얼)
    private Material[] ditherInstances;           // 디더로 교체한 복제본

    private float currentFade, targetFade, speed = 8f;
    private bool applied;                          // 지금 디더 머티리얼로 교체된 상태인지

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalShared = new Material[renderers.Length];
        ditherInstances = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalShared[i] = renderers[i].sharedMaterial;
    }

    public void Init(Shader shader) => ditherShader = shader;   // 최초 1회

    public void SetTargetFade(float fade, float fadeSpeed)
    {
        targetFade = Mathf.Clamp01(fade);
        speed = fadeSpeed;
        enabled = true;
    }

    private void Update()
    {
        if (Mathf.Approximately(currentFade, targetFade))
        {
            if (targetFade <= 0f && applied) RestoreOriginal();  // 완전 불투명 → 원본 복원 + 정리
            enabled = false;
            return;
        }

        currentFade = Mathf.MoveTowards(currentFade, targetFade, speed * Time.deltaTime);
        if (!applied) ApplyDither();
        SetFade(currentFade);
    }

    private void ApplyDither()
    {
        if (ditherShader == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            Material clone = new Material(originalShared[i]); // ① 원본 복제(텍스처/색 포함)
            clone.shader = ditherShader;                      // ② 셰이더만 디더로 (이름 같은 값은 유지됨)
            ditherInstances[i] = clone;
            renderers[i].material = clone;                    // ③ 이 렌더러에만 적용
        }
        applied = true;
    }

    private void SetFade(float fade)
    {
        foreach (Material m in ditherInstances)
            if (m != null) m.SetFloat(DitherFadeID, fade);
    }

    private void RestoreOriginal()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sharedMaterial = originalShared[i];  // 원본 공유 머티리얼로 복귀
            if (ditherInstances[i] != null) Destroy(ditherInstances[i]);
            ditherInstances[i] = null;
        }
        applied = false;
        currentFade = 0f;
    }

    private void OnDestroy()
    {
        if (ditherInstances == null) return;
        foreach (Material m in ditherInstances) if (m != null) Destroy(m);
    }
}