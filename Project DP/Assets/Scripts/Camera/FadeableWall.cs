using System;
using UnityEngine;

/// <summary>
/// 벽 하나를 서서히 비치게 했다가 되돌립니다. CameraOcclusionFader가 처음 투명해질 벽에 붙입니다.
///
/// 비칠 때만 머티리얼을 복제해 셰이더를 뚫기용으로 바꿔 끼웁니다. 뚫기용 셰이더(WallSeeThrough)는 원래 벽 셰이더를
/// 복사해 스크린도어(픽셀을 바둑판 순서로 걸러 그리기)만 더한 것이라 진행도 0에서는 원래 벽과 똑같이 보입니다.
/// 끝까지 불투명으로 그려 화면 효과(AO·GI·안개 등)도 그대로 받으므로 바꿔 끼우는 순간이 보이지 않습니다.
///
/// 전환은 진행도(_SeeThroughProgress)가 0~1을 가속·감속하며 움직이고, 셰이더가 그 진행도만큼 노이즈 경계를 훑어
/// 지나간 자리를 바둑판(_DitherFade만큼)으로 바꿉니다. 그래서 벽이 녹듯이 스르륵 비치고 돌아옵니다.
/// 진행도 속성이 없는 셰이더(예전 WallDither)는 _DitherFade 자체를 0~목표값으로 움직입니다.
/// </summary>
[DisallowMultipleComponent]
public class FadeableWall : MonoBehaviour
{
    private static readonly int DitherFadeID = Shader.PropertyToID("_DitherFade");
    private static readonly int ProgressID = Shader.PropertyToID("_SeeThroughProgress");

    private Func<Shader, Shader> seeThroughFor;   // 원래 셰이더 → 뚫기용 셰이더 (CameraOcclusionFader가 넘겨줌)
    private Renderer[] renderers;
    private Material[] originalShared;            // 복구용 원본 (공유 머티리얼)
    private Material[] ditherInstances;           // 뚫기용으로 교체한 복제본

    private float progress;                       // 0 = 원래 벽, 1 = 목표만큼 뚫림 (시간에 따라 일정하게 움직임)
    private float targetProgress;
    private float maxFade;
    private float seconds = 0.4f;
    private bool applied;                          // 지금 뚫기용 머티리얼로 교체된 상태인지

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalShared = new Material[renderers.Length];
        ditherInstances = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalShared[i] = renderers[i].sharedMaterial;
    }

    public void Init(Func<Shader, Shader> seeThroughShaderFor) => seeThroughFor = seeThroughShaderFor;   // 최초 1회

    /// <summary>
    /// 가려지면 fade(0~1)만큼 뚫고, 아니면 원래대로 되돌립니다. 어느 쪽이든 duration초에 걸쳐 바뀝니다.
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
            clone.shader = shader;                            // ② 셰이더만 뚫기용으로 (이름 같은 값은 유지됨)
            ditherInstances[i] = clone;
            renderers[i].material = clone;                    // ③ 이 렌더러에만 적용
        }
        applied = true;
    }

    private void SetFade(float eased)
    {
        foreach (Material m in ditherInstances)
        {
            if (m == null) continue;

            if (m.HasProperty(ProgressID))
            {
                m.SetFloat(DitherFadeID, maxFade);
                m.SetFloat(ProgressID, eased);
            }
            else
            {
                m.SetFloat(DitherFadeID, maxFade * eased);
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
