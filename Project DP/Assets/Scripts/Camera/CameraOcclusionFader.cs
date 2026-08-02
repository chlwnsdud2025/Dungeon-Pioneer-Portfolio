using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카메라와 플레이어 사이를 가리는 벽을 찾아, 디더로 부드럽게 뚫어 보이게 한다.
/// 벽마다 FadeableWall을 붙여 _DitherFade를 0(불투명)↔occludedFade(뚫림)로 보간시킨다.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraOcclusionFader : MonoBehaviour
{
    [Header("추적 대상")]
    [Tooltip("비우면 같은 오브젝트의 CameraFollow.target을 자동 사용")]
    public Transform target;
    [Tooltip("발밑이 아니라 몸통 기준으로 가림 판정")]
    public float targetHeightOffset = 1.5f;

    [Header("가림 감지")]
    public string wallTag = "Wall";
    [Tooltip("레이 대신 굵은 구로 쏴서 살짝 빗겨난 벽까지 잡음")]
    public float castRadius = 0.4f;
    [Tooltip("검사할 레이어(성능). Wall 전용 레이어 권장, 없으면 Everything")]
    public LayerMask detectionMask = ~0;

    [Header("디더 페이드")]
    [Tooltip("★ WallDither 셰이더를 여기에 드래그")]
    [SerializeField] private Shader ditherShader;
    [Tooltip("가릴 때 얼마나 뚫을지 (0=안뚫음, 1=완전투명)")]
    [Range(0f, 1f)] public float occludedFade = 0.7f;
    [Tooltip("낮출수록 더 천천히 뚫림")]
    public float fadeSpeed = 8f;

    private CameraFollow camFollow;
    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private readonly HashSet<FadeableWall> lastOccluders = new HashSet<FadeableWall>();
    private readonly HashSet<FadeableWall> currentOccluders = new HashSet<FadeableWall>();

    private void Awake() => camFollow = GetComponent<CameraFollow>();

    private Transform ResolveTarget()
    {
        if (target != null) return target;
        return camFollow != null ? camFollow.target : null;
    }

    private void LateUpdate()
    {
        Transform t = ResolveTarget();
        if (t == null) return;

        Vector3 origin = transform.position;
        Vector3 playerPoint = t.position + Vector3.up * targetHeightOffset;
        Vector3 dir = playerPoint - origin;
        float dist = dir.magnitude;
        if (dist < 0.01f) return;
        dir /= dist;

        currentOccluders.Clear();

        int count = Physics.SphereCastNonAlloc(
            origin, castRadius, dir, hitBuffer, dist, detectionMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Transform hitT = hitBuffer[i].transform;
            if (!hitT.CompareTag(wallTag)) continue;

            FadeableWall wall = hitT.GetComponent<FadeableWall>();
            if (wall == null)
            {
                wall = hitT.gameObject.AddComponent<FadeableWall>();
                wall.Init(ditherShader);           // ★ 최초 1회 디더 셰이더 전달
            }

            wall.SetTargetFade(occludedFade, fadeSpeed);   // 가림 → 뚫기
            currentOccluders.Add(wall);
        }

        // 지난 프레임엔 가렸지만 지금은 아닌 벽 → 다시 불투명(0)으로
        lastOccluders.ExceptWith(currentOccluders);
        foreach (FadeableWall wall in lastOccluders)
            if (wall != null) wall.SetTargetFade(0f, fadeSpeed);

        // 다음 프레임 비교용 갱신
        lastOccluders.Clear();
        lastOccluders.UnionWith(currentOccluders);
    }
}