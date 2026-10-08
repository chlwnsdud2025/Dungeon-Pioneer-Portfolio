using System;
using UnityEngine;

/// <summary>
/// 벽 하나를 서서히 비치게 했다가 되돌립니다. CameraOcclusionFader가 처음 투명해질 벽에 붙입니다.
///
/// 비칠 때만 머티리얼을 복제해 셰이더를 반투명용으로 바꿔 끼웁니다. 반투명용 셰이더(WallSeeThrough)는 원래 벽 셰이더를
/// 복사해 반투명(알파 = 1 - _DitherFade)으로만 바꾼 것이라 색·무늬가 같고, _DitherFade가 0~목표값을 가속·감속하며
/// 움직여 서서히 비치고 돌아옵니다.
///
/// 반투명 물체는 디퍼드 조명과 깊이 기반 화면 효과(HBAO·안개·Umbra 그림자 등) 밖에서 그려지므로,
/// 바꿔 끼우는 순간 투명도 0이어도 원래 벽과 밝기가 조금 다를 수 있습니다.
/// </summary>
[DisallowMultipleComponent]
public class FadeableWall : MonoBehaviour
{
    private static readonly int DitherFadeID = Shader.PropertyToID("_DitherFade");

    // 벽 셰이더(TEST_TRIPLANAR_LIT)의 오버레이 무늬(바둑판 등)를 섞는 양. 알파가 섞는 비율입니다.
    // 반투명이 되면 뒤 배경과 대비돼 무늬가 훨씬 도드라지므로, 비치는 정도에 맞춰 같이 옅게 합니다.
    private static readonly int OverlayMixID = Shader.PropertyToID("_OverlayMix");

    private Func<Shader, Shader> seeThroughFor;   // 원래 셰이더 → 반투명용 셰이더 (CameraOcclusionFader가 넘겨줌)
    private bool hideOverlay;                     // 비칠 때 오버레이 무늬를 옅게 할지
    private Renderer[] renderers;
    private Material[] originalShared;            // 복구용 원본 (공유 머티리얼)
    private Material[] ditherInstances;           // 반투명용으로 교체한 복제본
    private Color[] originalOverlayMix;           // 복제본의 원래 오버레이 섞는 양 (오버레이가 없는 머티리얼은 쓰지 않음)

    private float progress;                       // 0 = 원래 벽, 1 = 목표만큼 비침 (시간에 따라 일정하게 움직임)
    private float targetProgress;
    private float maxFade;
    private float seconds = 0.4f;
    private bool applied;                          // 지금 반투명용 머티리얼로 교체된 상태인지

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalShared = new Material[renderers.Length];
        ditherInstances = new Material[renderers.Length];
        originalOverlayMix = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalShared[i] = renderers[i].sharedMaterial;
    }

    /// <summary>최초 1회. 반투명용 셰이더 고르는 방법과, 비칠 때 오버레이 무늬를 옅게 할지를 받습니다.</summary>
    public void Init(Func<Shader, Shader> seeThroughShaderFor, bool hideOverlayWhenSeeThrough)
    {
        seeThroughFor = seeThroughShaderFor;
        hideOverlay = hideOverlayWhenSeeThrough;
    }

    /// <summary>
    /// 가려지면 fade(0~1)만큼 투명하게 하고, 아니면 원래대로 되돌립니다. 어느 쪽이든 duration초에 걸쳐 바뀝니다.
    /// 중간에 방향이 바뀌면 지금 자리에서 이어서 움직입니다.
    /// </summary>
    public void SetOccluded(bool occluded, float fade, float duration)
    {
        targetProgress = occluded ? 1f : 0f;
        maxFade = Mathf.Clamp01(fade);
        seconds = Mathf.Max(0.01f, duration);
        enabled = true;
    }

    /// <summary>서서히가 아니라 즉시 원래 머티리얼로 되돌립니다. (층이 꺼져 Update가 돌지 않을 때)</summary>
    public void ResetToOpaque()
    {
        targetProgress = 0f;
        progress = 0f;
        if (applied) RestoreOriginal();
        enabled = false;
    }

    private void Update()
    {
        if (Mathf.Approximately(progress, targetProgress))
        {
            if (targetProgress <= 0f && applied) RestoreOriginal();  // 완전히 돌아옴 → 원본 복원 + 정리
            enabled = false;
            return;
        }

        progress = Mathf.MoveTowards(progress, targetProgress, Time.deltaTime / seconds);
        if (!applied) ApplyDither();

        // 시작과 끝을 느리게 (가속·감속)
        SetFade(Mathf.SmoothStep(0f, 1f, progress));
    }

    private void ApplyDither()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Shader shader = originalShared[i] != null && seeThroughFor != null ? seeThroughFor(originalShared[i].shader) : null;
            if (shader == null) continue;

            Material clone = new Material(originalShared[i]); // ① 원본 복제(텍스처/색 포함)
            clone.shader = shader;                            // ② 셰이더만 반투명용으로 (이름 같은 값은 유지됨)
            ditherInstances[i] = clone;
            if (clone.HasProperty(OverlayMixID)) originalOverlayMix[i] = clone.GetColor(OverlayMixID);
            renderers[i].material = clone;                    // ③ 이 렌더러에만 적용
        }
        applied = true;
    }

    private void SetFade(float eased)
    {
        for (int i = 0; i < ditherInstances.Length; i++)
        {
            Material m = ditherInstances[i];
            if (m == null) continue;

            m.SetFloat(DitherFadeID, maxFade * eased);

            // 갈아 끼우는 순간 무늬가 한 번에 사라지지 않도록, 비치는 진행만큼 서서히 옅게 합니다.
            if (hideOverlay && m.HasProperty(OverlayMixID))
            {
                Color mix = originalOverlayMix[i];
                mix.a *= 1f - eased;
                m.SetColor(OverlayMixID, mix);
            }
        }
    }

    private void RestoreOriginal()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (ditherInstances[i] == null) continue;

            renderers[i].sharedMaterial = originalShared[i];  // 원본 공유 머티리얼로 복귀
            Destroy(ditherInstances[i]);
            ditherInstances[i] = null;
        }
        applied = false;
    }

    private void OnDestroy()
    {
        if (ditherInstances == null) return;
        foreach (Material m in ditherInstances) if (m != null) Destroy(m);
    }
}
